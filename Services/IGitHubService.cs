using GitHubDeploymentManager.Models;

namespace GitHubDeploymentManager.Services;

public interface IGitHubService
{
    Task<List<GitHubRepository>> GetRepositoriesAsync(string owner, CancellationToken ct = default, int maxResults = int.MaxValue);
    Task<List<GitHubRelease>> GetReleasesAsync(string owner, string repository, CancellationToken ct = default);
    Task<GitHubReleasePackage> ValidateReleaseAsync(string owner, string repository, GitHubRelease release, CancellationToken ct = default);
    Task DownloadPackageAsync(string owner, string repository, GitHubReleasePackage package, string destinationFile, IProgress<long>? progress = null, CancellationToken ct = default);
    void ClearCache();
}
