namespace GitHubDeploymentManager.Services;

public sealed class GitHubServiceFactory : IGitHubServiceFactory
{
    private readonly AppConfiguration _configuration;
    private readonly IAppLogger _logger;

    public GitHubServiceFactory(AppConfiguration configuration, IAppLogger logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public IGitHubService Create(string? gitHubToken = null)
        => new GitHubService(
            _configuration.GitHubApiBaseUrl,
            _configuration.GitHubTimeoutSeconds,
            gitHubToken,
            _configuration.GitHubCacheMinutes,
            _logger);
}
