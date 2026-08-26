namespace GitHubDeploymentManager.Models;

public enum GitHubReleasePackageKind
{
    ZipAsset,
    ExeAsset,
    SourceCodeZip
}

public sealed record GitHubReleasePackage
{
    public GitHubReleasePackageKind Kind { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public GitHubReleaseAsset? Asset { get; init; }
    public string DisplayLabel { get; init; } = string.Empty;
    public string? Notes { get; init; }

    public bool IsArchive => Kind is GitHubReleasePackageKind.ZipAsset or GitHubReleasePackageKind.SourceCodeZip;
}
