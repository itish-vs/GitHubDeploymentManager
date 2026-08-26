namespace GitHubDeploymentManager.Services;

public interface IGitHubServiceFactory
{
    IGitHubService Create(string? gitHubToken = null);
}
