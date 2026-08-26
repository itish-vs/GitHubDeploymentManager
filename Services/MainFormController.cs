using GitHubDeploymentManager.Models;

namespace GitHubDeploymentManager.Services;

public sealed class MainFormController
{
    private readonly AppConfiguration _configuration;
    private readonly IAppLogger _logger;
    private readonly IDeploymentService _deploymentService;
    private readonly IGitHubServiceFactory _gitHubServiceFactory;

    public MainFormController(
        AppConfiguration configuration,
        IAppLogger logger,
        IDeploymentService deploymentService,
        IGitHubServiceFactory gitHubServiceFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _deploymentService = deploymentService;
        _gitHubServiceFactory = gitHubServiceFactory;
    }

    public AppConfiguration Configuration => _configuration;

    public IGitHubService CreateGitHubService(string? token)
    {
        var normalizedToken = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
        _logger.Info("Creating GitHub service.", ("Authenticated", !string.IsNullOrWhiteSpace(normalizedToken)));
        return _gitHubServiceFactory.Create(normalizedToken);
    }

    public async Task<List<GitHubRepository>> FetchRepositoriesAsync(string owner, CancellationToken ct = default)
    {
        var trimmedOwner = owner.Trim();
        _logger.Info("Fetching repositories.", ("Owner", trimmedOwner));
        return await CreateGitHubService(null).GetRepositoriesAsync(trimmedOwner, ct);
    }

    public async Task<List<GitHubRelease>> LoadReleasesAsync(IGitHubService gitHubService, string owner, string repository, CancellationToken ct = default)
    {
        _logger.Info("Loading releases.", ("Owner", owner), ("Repository", repository));
        return await gitHubService.GetReleasesAsync(owner.Trim(), repository.Trim(), ct);
    }

    public async Task<GitHubReleasePackage> ValidateReleaseAsync(
        IGitHubService gitHubService,
        string owner,
        string repository,
        GitHubRelease release,
        CancellationToken ct = default)
    {
        _logger.Info("Validating release.", ("Owner", owner), ("Repository", repository), ("Release", release.TagName));
        return await gitHubService.ValidateReleaseAsync(owner.Trim(), repository.Trim(), release, ct);
    }

    public Task ValidateTargetAsync(string targetPath, CancellationToken ct = default)
        => Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            _deploymentService.ValidateTarget(targetPath.Trim());
        }, ct);

    public async Task<DeploymentResult> DeployAsync(
        IGitHubService gitHubService,
        string owner,
        GitHubRepository repository,
        GitHubRelease release,
        GitHubReleasePackage package,
        string targetPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var operationId = _logger.BeginOperation(
            "DeployWorkflow",
            ("Owner", owner),
            ("Repository", repository.Name),
            ("Release", release.TagName),
            ("Package", package.Name),
            ("PackageKind", package.Kind.ToString()),
            ("TargetPath", targetPath));

        await ValidateTargetAsync(targetPath, ct);

        var staging = Path.Combine(Path.GetTempPath(), "GitHubDeploymentManager", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var downloadPath = Path.Combine(staging, package.Name);

        try
        {
            await gitHubService.DownloadPackageAsync(owner.Trim(), repository.Name, package, downloadPath, progress: null, ct);

            var result = package.Kind switch
            {
                GitHubReleasePackageKind.ZipAsset or GitHubReleasePackageKind.SourceCodeZip =>
                    await _deploymentService.DeployZipAsync(downloadPath, targetPath.Trim(), progress, ct),
                GitHubReleasePackageKind.ExeAsset =>
                    await _deploymentService.DeploySingleFileAsync(downloadPath, targetPath.Trim(), progress, ct),
                _ => throw new InvalidOperationException($"Unsupported release package kind: {package.Kind}")
            };

            if (result.Success)
                _logger.Info(operationId, "Deployment workflow completed.", ("FilesCopied", result.FilesCopied), ("PackageKind", package.Kind.ToString()));
            else
                _logger.Error(operationId, "Deployment workflow failed.", null, ("FilesCopied", result.FilesCopied), ("FailedFile", result.FailedFile), ("PackageKind", package.Kind.ToString()));

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error(operationId, "Deployment workflow threw an exception.", ex);
            throw;
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, true);
            }
            catch (Exception ex)
            {
                _logger.Error(operationId, "Failed to clean deployment staging directory.", ex, ("StagingPath", staging));
            }
        }
    }
}
