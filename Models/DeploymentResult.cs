namespace GitHubDeploymentManager.Models;

public sealed class DeploymentResult
{
    public bool Success { get; init; }
    public int FilesCopied { get; init; }
    public string Message { get; init; } = "";
    public string? FailedFile { get; init; }
}
