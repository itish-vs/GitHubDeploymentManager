using System.Net;
using System.Net.Http;
using System.Text;
using GitHubDeploymentManager.Models;
using GitHubDeploymentManager.Services;

namespace GitHubDeploymentManager.Tests;

public sealed class GitHubServiceTests
{
    [Fact]
    public async Task GetRepositoriesAsync_ReturnsRepositoriesSortedByName()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[{\"name\":\"zeta\",\"full_name\":\"owner/zeta\",\"html_url\":\"https://example/zeta\"},{\"name\":\"alpha\",\"full_name\":\"owner/alpha\",\"html_url\":\"https://example/alpha\"}]", Encoding.UTF8, "application/json")
        });
        using var httpClient = CreateHttpClient(handler);
        var service = new GitHubService(httpClient);

        var repositories = await service.GetRepositoriesAsync("owner");

        Assert.Collection(
            repositories,
            repo => Assert.Equal("alpha", repo.Name),
            repo => Assert.Equal("zeta", repo.Name));
    }

    [Fact]
    public async Task GetRepositoriesAsync_UsesCache_WhenEnabled()
    {
        var requestCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"name\":\"alpha\",\"full_name\":\"owner/alpha\",\"html_url\":\"https://example/alpha\"}]", Encoding.UTF8, "application/json")
            };
        });
        using var httpClient = CreateHttpClient(handler);
        var service = new GitHubService(httpClient, cacheMinutes: 5);

        var first = await service.GetRepositoriesAsync("owner");
        var second = await service.GetRepositoriesAsync("owner");

        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task GetRepositoriesAsync_ThrowsFriendlyMessage_WhenRateLimitExceeded()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"message\":\"API rate limit exceeded for 1.2.3.4.\"}", Encoding.UTF8, "application/json")
        };
        response.Headers.Add("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds().ToString());

        var handler = new StubHttpMessageHandler(_ => response);
        using var httpClient = CreateHttpClient(handler);
        var service = new GitHubService(httpClient);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetRepositoriesAsync("owner"));

        Assert.Contains("rate limit exceeded", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GitHubToken", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateReleaseAsync_ReturnsZipPackage_WhenZipAssetContainsExecutable()
    {
        var zipUrl = "https://downloads.example/releases/app.zip";
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.ToString() == zipUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(CreateZipBytes(("app.exe", "binary"), ("docs/readme.txt", "docs")))
                };
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });
        using var httpClient = CreateHttpClient(handler);
        var service = new GitHubService(httpClient);
        var release = new GitHubRelease
        {
            Assets =
            [
                new GitHubReleaseAsset { Id = 1, Name = "app.zip", BrowserDownloadUrl = zipUrl, Size = 10 }
            ]
        };

        var package = await service.ValidateReleaseAsync("owner", "repo", release);

        Assert.Equal(GitHubReleasePackageKind.ZipAsset, package.Kind);
        Assert.Equal("ZIP package: app.zip", package.DisplayLabel);
        Assert.Equal("ZIP package containing an executable", package.Notes);
        Assert.NotNull(package.Asset);
        Assert.Equal("app.zip", package.Name);
    }

    [Fact]
    public async Task ValidateReleaseAsync_ReturnsExePackage_WhenExeAssetExists()
    {
        var exeUrl = "https://downloads.example/releases/app.exe";
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.ToString() == exeUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { 1, 2, 3, 4 })
                };
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });
        using var httpClient = CreateHttpClient(handler);
        var service = new GitHubService(httpClient);
        var release = new GitHubRelease
        {
            Assets =
            [
                new GitHubReleaseAsset { Id = 2, Name = "app.exe", BrowserDownloadUrl = exeUrl, Size = 4 }
            ]
        };

        var package = await service.ValidateReleaseAsync("owner", "repo", release);

        Assert.Equal(GitHubReleasePackageKind.ExeAsset, package.Kind);
        Assert.Equal("EXE package: app.exe", package.DisplayLabel);
        Assert.Equal("Direct executable asset", package.Notes);
        Assert.NotNull(package.Asset);
        Assert.Equal("app.exe", package.Name);
    }

    [Fact]
    public async Task ValidateReleaseAsync_ReturnsSourceCodeZip_WhenNoDeployableAssetsExist()
    {
        var zipballUrl = "https://api.github.com/repos/owner/repo/zipball/main";
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.ToString() == zipballUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(CreateZipBytes(("src/readme.txt", "source archive")))
                };
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });
        using var httpClient = CreateHttpClient(handler);
        var service = new GitHubService(httpClient);
        var release = new GitHubRelease
        {
            TagName = "v1.0.0",
            ZipballUrl = zipballUrl,
            Assets =
            [
                new GitHubReleaseAsset { Id = 3, Name = "notes.txt" }
            ]
        };

        var package = await service.ValidateReleaseAsync("owner", "repo", release);

        Assert.Equal(GitHubReleasePackageKind.SourceCodeZip, package.Kind);
        Assert.Equal("Source code ZIP", package.DisplayLabel);
        Assert.Equal("GitHub source archive", package.Notes);
        Assert.Null(package.Asset);
        Assert.EndsWith("-source.zip", package.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetRepositoriesAsync_MapsRepositoryDescription()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[{\"name\":\"alpha\",\"full_name\":\"owner/alpha\",\"html_url\":\"https://example/alpha\",\"description\":\"Primary deployment repo\"}]", Encoding.UTF8, "application/json")
        });
        using var httpClient = CreateHttpClient(handler);
        var service = new GitHubService(httpClient);

        var repositories = await service.GetRepositoriesAsync("owner");

        Assert.Single(repositories);
        Assert.Equal("Primary deployment repo", repositories[0].Description);
    }

    [Fact]
    public async Task ValidateReleaseAsync_Throws_WhenNoDeployablePackageExists()
    {
        using var httpClient = CreateHttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var service = new GitHubService(httpClient);
        var release = new GitHubRelease
        {
            Assets =
            [
                new GitHubReleaseAsset { Id = 1, Name = "readme.txt" }
            ]
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidateReleaseAsync("owner", "repo", release));

        Assert.Contains("deployable ZIP, EXE, or source code archive", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] CreateZipBytes(params (string Path, string Content)[] files)
    {
        using var memory = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(memory, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Path);
                using var stream = entry.Open();
                using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
                writer.Write(file.Content);
            }
        }

        return memory.ToArray();
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.com/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GitHubDeploymentManager.Tests/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request));
    }
}
