using System.Text.Json.Serialization;

namespace GitHubDeploymentManager.Models;

public sealed class GitHubRelease
{
    public long Id { get; init; }
    public string Name { get; init; } = "";

    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = "";

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; init; }

    [JsonPropertyName("zipball_url")]
    public string ZipballUrl { get; init; } = "";

    [JsonPropertyName("tarball_url")]
    public string TarballUrl { get; init; } = "";

    public bool Draft { get; init; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; init; }

    public List<GitHubReleaseAsset> Assets { get; init; } = [];
}

public sealed class GitHubReleaseAsset
{
    public long Id { get; init; }
    public string Name { get; init; } = "";
    public long Size { get; init; }

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; init; } = "";

    [JsonPropertyName("content_type")]
    public string ContentType { get; init; } = "";
}
