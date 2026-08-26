using System.Configuration;

namespace GitHubDeploymentManager.Services;

public sealed class AppConfiguration
{
    public string GitHubOwner { get; }
    public string DefaultDeploymentPath { get; }
    public string GitHubApiBaseUrl { get; }
    public int GitHubTimeoutSeconds { get; }
    public string GitHubToken { get; }
    public string LogFolderPath { get; }
    public int GitHubCacheMinutes { get; }
    public int MaxRepositoryResults { get; }

    public AppConfiguration()
    {
        GitHubOwner = ConfigurationManager.AppSettings["GitHubOwner"] ?? "";
        DefaultDeploymentPath = ConfigurationManager.AppSettings["DefaultDeploymentPath"] ?? "";
        GitHubApiBaseUrl = ConfigurationManager.AppSettings["GitHubApiBaseUrl"] ?? "https://api.github.com/";
        GitHubTimeoutSeconds = int.TryParse(ConfigurationManager.AppSettings["GitHubTimeoutSeconds"], out var seconds)
            ? seconds
            : 60;
        GitHubToken = ConfigurationManager.AppSettings["GitHubToken"] ?? string.Empty;
        LogFolderPath = ConfigurationManager.AppSettings["LogFolderPath"] ?? "Logs";
        GitHubCacheMinutes = int.TryParse(ConfigurationManager.AppSettings["GitHubCacheMinutes"], out var cacheMinutes)
            ? Math.Max(0, cacheMinutes)
            : 5;
        MaxRepositoryResults = int.TryParse(ConfigurationManager.AppSettings["MaxRepositoryResults"], out var maxRepos)
            ? Math.Max(1, maxRepos)
            : 50;
    }
}
