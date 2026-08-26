using GitHubDeploymentManager.Services;

namespace GitHubDeploymentManager.Tests;

public sealed class FileLoggerTests
{
    [Fact]
    public void Constructor_CreatesConfiguredLogDirectory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), nameof(GitHubDeploymentManager), Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "Logs");

        try
        {
            var logger = new FileLogger(logPath);

            Assert.True(Directory.Exists(logger.LogDirectory));
            Assert.Equal(logPath, logger.LogDirectory, ignoreCase: OperatingSystem.IsWindows());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void BeginOperation_WritesOperationIdAndProperties()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), nameof(GitHubDeploymentManager), Guid.NewGuid().ToString("N"));
        var logger = new FileLogger(tempRoot);
        var expectedFile = Path.Combine(tempRoot, $"deployment-{DateTime.Now:yyyy-MM-dd}.log");

        try
        {
            var operationId = logger.BeginOperation("FetchRepositories", ("Owner", "octocat"));

            Assert.True(File.Exists(expectedFile));
            var content = File.ReadAllText(expectedFile);
            Assert.Contains("Operation started: FetchRepositories", content, StringComparison.Ordinal);
            Assert.Contains($"[OperationId={operationId}]", content, StringComparison.Ordinal);
            Assert.Contains("Owner=octocat", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Error_WritesLogEntryToDailyFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), nameof(GitHubDeploymentManager), Guid.NewGuid().ToString("N"));
        var logger = new FileLogger(tempRoot);
        var expectedFile = Path.Combine(tempRoot, $"deployment-{DateTime.Now:yyyy-MM-dd}.log");

        try
        {
            logger.Error("Sample failure", new InvalidOperationException("boom"));

            Assert.True(File.Exists(expectedFile));
            var content = File.ReadAllText(expectedFile);
            Assert.Contains("[ERROR]", content, StringComparison.Ordinal);
            Assert.Contains("Sample failure", content, StringComparison.Ordinal);
            Assert.Contains("boom", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }
}
