using GitHubDeploymentManager.Models;

namespace GitHubDeploymentManager.Services;

public interface IDeploymentService
{
    Task<DeploymentResult> DeployZipAsync(string zipPath, string targetPath, IProgress<string>? progress = null, CancellationToken ct = default);
    Task<DeploymentResult> DeploySingleFileAsync(string filePath, string targetPath, IProgress<string>? progress = null, CancellationToken ct = default);
    void ValidateTarget(string targetPath);
}
