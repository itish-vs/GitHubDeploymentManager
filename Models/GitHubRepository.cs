namespace GitHubDeploymentManager.Models;

public sealed record GitHubRepository(
    string Name,
    string FullName,
    string HtmlUrl,
    string? Description = null);
