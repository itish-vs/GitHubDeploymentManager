using System.IO.Compression;
using GitHubDeploymentManager.Services;

namespace GitHubDeploymentManager.Tests;

public sealed class DeploymentServiceTests
{
    [Fact]
    public async Task DeploySingleFileAsync_CopiesFileToTarget()
    {
        var logger = new TestLogger();
        var service = new DeploymentService(logger);
        var sourceFile = Path.GetTempFileName();
        var targetDirectory = CreateTempDirectory();
        var destination = Path.Combine(targetDirectory, Path.GetFileName(sourceFile));

        try
        {
            await File.WriteAllTextAsync(sourceFile, "payload");

            var result = await service.DeploySingleFileAsync(sourceFile, targetDirectory);

            Assert.True(result.Success);
            Assert.Equal(1, result.FilesCopied);
            Assert.True(File.Exists(destination));
            Assert.Equal("payload", await File.ReadAllTextAsync(destination));
        }
        finally
        {
            TryDeleteFile(sourceFile);
            TryDeleteDirectory(targetDirectory);
        }
    }

    [Fact]
    public async Task DeployZipAsync_ExtractsZipToTarget()
    {
        var logger = new TestLogger();
        var service = new DeploymentService(logger);
        var sourceZip = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.zip");
        var targetDirectory = CreateTempDirectory();
        var expectedExe = Path.Combine(targetDirectory, "app.exe");
        var expectedText = Path.Combine(targetDirectory, "docs", "readme.txt");

        try
        {
            CreateZip(sourceZip,
            [
                ("app.exe", "binary"),
                ("docs/readme.txt", "readme")
            ]);

            var result = await service.DeployZipAsync(sourceZip, targetDirectory);

            Assert.True(result.Success);
            Assert.Equal(2, result.FilesCopied);
            Assert.True(File.Exists(expectedExe));
            Assert.True(File.Exists(expectedText));
            Assert.Equal("binary", await File.ReadAllTextAsync(expectedExe));
            Assert.Equal("readme", await File.ReadAllTextAsync(expectedText));
        }
        finally
        {
            TryDeleteFile(sourceZip);
            TryDeleteDirectory(targetDirectory);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GitHubDeploymentManager.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void CreateZip(string zipPath, IEnumerable<(string Path, string Content)> files)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var file in files)
        {
            var entry = archive.CreateEntry(file.Path);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream);
            writer.Write(file.Content);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private sealed class TestLogger : IAppLogger
    {
        public string LogDirectory => Path.GetTempPath();
        public string BeginOperation(string name, params (string Key, object? Value)[] properties) => Guid.NewGuid().ToString("N");
        public void Info(string message, params (string Key, object? Value)[] properties) { }
        public void Info(string operationId, string message, params (string Key, object? Value)[] properties) { }
        public void Error(string message, Exception? ex = null, params (string Key, object? Value)[] properties) { }
        public void Error(string operationId, string message, Exception? ex = null, params (string Key, object? Value)[] properties) { }
    }
}
