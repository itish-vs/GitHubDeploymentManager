using System.IO.Compression;
using GitHubDeploymentManager.Models;

namespace GitHubDeploymentManager.Services;

public sealed class DeploymentService : IDeploymentService
{
    private readonly IAppLogger? _logger;

    public DeploymentService(IAppLogger? logger = null)
    {
        _logger = logger;
    }

    public async Task<DeploymentResult> DeployZipAsync(
        string zipPath,
        string targetPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var operationId = _logger?.BeginOperation("DeployZip", ("ZipPath", zipPath), ("TargetPath", targetPath));
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("Deployment ZIP was not found.", zipPath);

        ValidateTarget(targetPath);

        var stagingRoot = Path.Combine(Path.GetTempPath(), "GitHubDeploymentManager", Guid.NewGuid().ToString("N"));
        var extractionPath = Path.Combine(stagingRoot, "extracted");

        try
        {
            Directory.CreateDirectory(extractionPath);
            _logger?.Info(operationId ?? string.Empty, "Deployment staging directory created.", ("StagingRoot", stagingRoot));
            progress?.Report("Validating ZIP package...");

            // Offload blocking ZIP extraction to the thread pool so the UI thread stays responsive.
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extractionPath, overwriteFiles: false), ct);
            ct.ThrowIfCancellationRequested();

            // Enumerate files and pre-create target directories on the thread pool —
            // both are blocking I/O calls that should not run on the UI thread.
            //
            // GitHub source-code ZIPs always contain a single top-level folder
            // (e.g. "repo-name-1.0.0/"). Strip it so we deploy only the actual
            // source files, not the wrapping folder.
            var (files, targetDirs, effectiveRoot) = await Task.Run(() =>
            {
                // Check for the single-folder wrapper pattern.
                var topLevelDirs = Directory.GetDirectories(extractionPath);
                var topLevelFiles = Directory.GetFiles(extractionPath);
                var root = topLevelDirs.Length == 1 && topLevelFiles.Length == 0
                    ? topLevelDirs[0]   // unwrap the single folder
                    : extractionPath;   // use as-is (normal ZIP or uploaded asset)

                var f = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
                var dirs = f
                    .Select(x => Path.GetDirectoryName(Path.GetRelativePath(root, x)))
                    .Where(d => !string.IsNullOrEmpty(d))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return (f, dirs, root);
            }, ct);

            if (files.Length == 0)
                throw new InvalidDataException("The ZIP deployment package contains no files.");

            progress?.Report($"Staged {files.Length:N0} file(s). Preparing deployment...");

            // Pre-create all target directories in one pass before any copy starts —
            // avoids repeated CreateDirectory calls inside the hot parallel loop.
            foreach (var dir in targetDirs)
                Directory.CreateDirectory(Path.Combine(targetPath, dir!));

            // Parallel bulk copy.
            // DOP capped at 8: beyond that, network shares saturate and local disks see
            // diminishing returns from seek contention.
            int copied = 0;
            DeploymentResult? failureResult = null;
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                await Parallel.ForEachAsync(
                    files,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8),
                        CancellationToken = linkedCts.Token
                    },
                    async (source, innerCt) =>
                    {
                        var relative = Path.GetRelativePath(effectiveRoot, source);
                        var destination = Path.Combine(targetPath, relative);

                        try
                        {
                            await CopyReplaceAsync(source, destination, innerCt);

                            var count = Interlocked.Increment(ref copied);
                            if (count % 5 == 0 || count == files.Length)
                                progress?.Report($"Deployed {count:N0} / {files.Length:N0} file(s)...");
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (IOException ex)
                        {
                            var current = Interlocked.CompareExchange(ref failureResult, new DeploymentResult
                            {
                                Success = false,
                                FilesCopied = Volatile.Read(ref copied),
                                FailedFile = relative,
                                Message = $"Unable to replace '{relative}'. The file may be locked or inaccessible. {ex.Message}"
                            }, null);

                            if (current is null)
                            {
                                _logger?.Error(operationId ?? string.Empty, "Deployment failed while copying file.", ex,
                                    ("RelativePath", relative), ("FilesCopied", Volatile.Read(ref copied)));
                                linkedCts.Cancel();
                            }
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            var current = Interlocked.CompareExchange(ref failureResult, new DeploymentResult
                            {
                                Success = false,
                                FilesCopied = Volatile.Read(ref copied),
                                FailedFile = relative,
                                Message = $"Access denied while deploying '{relative}'. {ex.Message}"
                            }, null);

                            if (current is null)
                            {
                                _logger?.Error(operationId ?? string.Empty, "Deployment failed due to access denied.", ex,
                                    ("RelativePath", relative), ("FilesCopied", Volatile.Read(ref copied)));
                                linkedCts.Cancel();
                            }
                        }
                    });
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Cancelled by a file-copy failure, not by the user — fall through to failureResult below.
            }

            if (failureResult is not null)
                return failureResult;

            ct.ThrowIfCancellationRequested();

            _logger?.Info(operationId ?? string.Empty, "Deployment completed successfully.", ("FilesCopied", copied));
            return new DeploymentResult
            {
                Success = true,
                FilesCopied = copied,
                Message = $"Deployment completed successfully. {copied:N0} file(s) deployed."
            };
        }
        catch (InvalidDataException ex)
        {
            _logger?.Error(operationId ?? string.Empty, "Deployment ZIP validation failed.", ex, ("ZipPath", zipPath));
            return new DeploymentResult
            {
                Success = false,
                Message = $"The deployment ZIP is invalid or could not be extracted. {ex.Message}"
            };
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingRoot))
                    Directory.Delete(stagingRoot, recursive: true);
            }
            catch
            {
                // Cleanup failure must not hide the deployment result.
            }
        }
    }

    public async Task<DeploymentResult> DeploySingleFileAsync(
        string filePath,
        string targetPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var operationId = _logger?.BeginOperation("DeploySingleFile", ("FilePath", filePath), ("TargetPath", targetPath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Deployment file was not found.", filePath);

        ValidateTarget(targetPath);
        ct.ThrowIfCancellationRequested();
        progress?.Report("Deploying single executable asset...");

        var destination = Path.Combine(targetPath, Path.GetFileName(filePath));
        try
        {
            await CopyReplaceAsync(filePath, destination, ct);
            _logger?.Info(operationId ?? string.Empty, "Single-file deployment completed successfully.", ("Destination", destination));
            return new DeploymentResult
            {
                Success = true,
                FilesCopied = 1,
                Message = $"Deployment completed successfully. 1 file deployed to {destination}."
            };
        }
        catch (IOException ex)
        {
            _logger?.Error(operationId ?? string.Empty, "Single-file deployment failed while copying file.", ex, ("Destination", destination));
            return new DeploymentResult
            {
                Success = false,
                FailedFile = Path.GetFileName(filePath),
                Message = $"Unable to replace '{Path.GetFileName(filePath)}'. The file may be locked or inaccessible. {ex.Message}"
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.Error(operationId ?? string.Empty, "Single-file deployment failed due to access denied.", ex, ("Destination", destination));
            return new DeploymentResult
            {
                Success = false,
                FailedFile = Path.GetFileName(filePath),
                Message = $"Access denied while deploying '{Path.GetFileName(filePath)}'. {ex.Message}"
            };
        }
    }

    public void ValidateTarget(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            throw new ArgumentException("Deployment path cannot be empty.");

        if (!Directory.Exists(targetPath))
            throw new DirectoryNotFoundException($"Deployment path does not exist: {targetPath}");

        var probe = Path.Combine(targetPath, $".github-deployment-manager-probe-{Guid.NewGuid():N}.tmp");
        try
        {
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
            }
            File.Delete(probe);
            _logger?.Info("Deployment target validated.", ("TargetPath", targetPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.Error("Deployment target validation failed.", ex, ("TargetPath", targetPath));
            throw new UnauthorizedAccessException(
                $"The deployment path exists but is not writable: {targetPath}", ex);
        }
    }

    private const int CopyBufferSize = 1024 * 512; // 512 KB — reduces syscall overhead for bulk/network transfers

    private static async Task CopyReplaceAsync(string source, string destination, CancellationToken ct)
    {
        await using var input  = new FileStream(source,      FileMode.Open,   FileAccess.Read,  FileShare.Read, CopyBufferSize, useAsync: true);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize, useAsync: true);
        await input.CopyToAsync(output, CopyBufferSize, ct);
    }
}
