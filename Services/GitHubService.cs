using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GitHubDeploymentManager.Models;

namespace GitHubDeploymentManager.Services;

public sealed class GitHubService : IGitHubService
{
    private readonly HttpClient _http;
    private readonly IAppLogger? _logger;
    private readonly TimeSpan _cacheDuration;
    private readonly ConcurrentDictionary<string, CacheEntry<List<GitHubRepository>>> _repositoryCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CacheEntry<List<GitHubRelease>>> _releaseCache = new(StringComparer.OrdinalIgnoreCase);

    public GitHubService(string baseUrl, int timeoutSeconds, string? gitHubToken = null, int cacheMinutes = 5, IAppLogger? logger = null)
        : this(CreateHttpClient(baseUrl, timeoutSeconds, gitHubToken), Math.Max(0, cacheMinutes), logger)
    {
    }

    public GitHubService(HttpClient httpClient, int cacheMinutes = 5, IAppLogger? logger = null)
    {
        _http = httpClient;
        _logger = logger;
        _cacheDuration = TimeSpan.FromMinutes(Math.Max(0, cacheMinutes));
    }

    private static HttpClient CreateHttpClient(string baseUrl, int timeoutSeconds, string? gitHubToken)
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitHubDeploymentManager", "1.0"));
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        if (!string.IsNullOrWhiteSpace(gitHubToken))
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", gitHubToken.Trim());

        return httpClient;
    }

    public async Task<List<GitHubRepository>> GetRepositoriesAsync(string owner, CancellationToken ct = default, int maxResults = int.MaxValue)
    {
        var operationId = _logger?.BeginOperation("GetRepositories", ("Owner", owner), ("MaxResults", maxResults));
        var cacheKey = owner.Trim();

        if (TryGetCacheValue(_repositoryCache, cacheKey, out var cachedRepositories))
        {
            var cached = cachedRepositories.Count > maxResults ? cachedRepositories.Take(maxResults).ToList() : cachedRepositories;
            _logger?.Info(operationId ?? string.Empty, "Repository cache hit.", ("Owner", owner), ("Count", cached.Count));
            return cached;
        }

        var result = new List<GitHubRepository>();
        var page = 1;

        // Try organization endpoint first, then fall back to user endpoint
        var isOrganization = true;
        var lastError = "";

        while (true)
        {
            // Request only as many items per page as still needed — avoids fetching 100 when 3 remain.
            var remaining = maxResults - result.Count;
            var perPage   = Math.Min(remaining, 100);

            // Use /orgs/{owner}/repos for organizations, /users/{owner}/repos for users
            var endpoint = isOrganization 
                ? $"orgs/{Uri.EscapeDataString(owner)}/repos?type=all&per_page={perPage}&page={page}"
                : $"users/{Uri.EscapeDataString(owner)}/repos?type=all&per_page={perPage}&page={page}";

            using var response = await _http.GetAsync(endpoint, ct);

            // If org endpoint fails, try user endpoint
            if (!response.IsSuccessStatusCode && isOrganization)
            {
                lastError = $"HTTP {(int)response.StatusCode}";
                _logger?.Info(operationId ?? string.Empty, $"Organization endpoint failed ({lastError}), trying user endpoint.", ("Owner", owner));
                isOrganization = false;
                page = 1; // Reset to first page
                continue;
            }

            // If both endpoints fail, throw the error
            if (!response.IsSuccessStatusCode)
            {
                await EnsureSuccess(response, "retrieve repositories");
                break; // This won't execute if EnsureSuccess throws, but kept for clarity
            }

            var json = await response.Content.ReadAsStringAsync(ct);

            _logger?.Info(operationId ?? string.Empty, $"Fetching from {(isOrganization ? "organization" : "user")} endpoint.", ("Owner", owner), ("Page", page));

            var items = JsonSerializer.Deserialize<List<JsonElement>>(json) ?? [];
            if (items.Count == 0) break;

            foreach (var item in items)
            {
                result.Add(new GitHubRepository(
                    item.GetProperty("name").GetString() ?? "",
                    item.GetProperty("full_name").GetString() ?? "",
                    item.GetProperty("html_url").GetString() ?? "",
                    item.TryGetProperty("description", out var descriptionElement) ? descriptionElement.GetString() : null));

                if (result.Count >= maxResults)
                    goto done; // limit reached — stop pagination immediately
            }

            if (items.Count < perPage) break;
            page++;
        }

        done:
        var ordered = result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();

        // Only cache full (unlimited) fetches so a later capped call can still serve
        // the full list from cache if the user raises the limit.
        if (maxResults == int.MaxValue)
            SetCacheValue(_repositoryCache, cacheKey, ordered);

        _logger?.Info(operationId ?? string.Empty, "Repositories loaded from GitHub.",
            ("Owner", owner), ("Count", ordered.Count), ("Type", isOrganization ? "Organization" : "User"), ("Capped", ordered.Count == maxResults && maxResults != int.MaxValue));
        return ordered;
    }

    public async Task<List<GitHubRelease>> GetReleasesAsync(string owner, string repository, CancellationToken ct = default)
    {
        var operationId = _logger?.BeginOperation("GetReleases", ("Owner", owner), ("Repository", repository));
        var cacheKey = $"{owner.Trim()}::{repository.Trim()}";

        if (TryGetCacheValue(_releaseCache, cacheKey, out var cachedReleases))
        {
            _logger?.Info(operationId ?? string.Empty, "Release cache hit.", ("Owner", owner), ("Repository", repository), ("Count", cachedReleases.Count));
            return cachedReleases;
        }

        var result = new List<GitHubRelease>();
        var page = 1;

        while (true)
        {
            using var response = await _http.GetAsync(
                $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}/releases?per_page=100&page={page}",
                ct);

            await EnsureSuccess(response, "retrieve releases");
            var json = await response.Content.ReadAsStringAsync(ct);
            var items = JsonSerializer.Deserialize<List<GitHubRelease>>(json, JsonOptions) ?? [];

            result.AddRange(items.Where(x => !x.Draft));

            if (items.Count < 100) break;
            page++;
        }

        SetCacheValue(_releaseCache, cacheKey, result);
        _logger?.Info(operationId ?? string.Empty, "Releases loaded from GitHub.", ("Owner", owner), ("Repository", repository), ("Count", result.Count));
        return result;
    }

    public async Task<GitHubReleasePackage> ValidateReleaseAsync(
        string owner, string repository, GitHubRelease release, CancellationToken ct = default)
    {
        var operationId = _logger?.BeginOperation("ValidateRelease", ("Owner", owner), ("Repository", repository), ("Release", release.TagName));
        var package = await SelectReleasePackageAsync(owner, repository, release, ct);

        if (string.IsNullOrWhiteSpace(package.DownloadUrl))
            throw new InvalidOperationException("The selected release does not provide a downloadable package.");

        await ValidatePackageAccessAsync(package, ct);
        _logger?.Info(operationId ?? string.Empty, "Release package validated.", ("Package", package.Name), ("Kind", package.Kind.ToString()));
        return package;
    }

    public async Task DownloadPackageAsync(
        string owner, string repository, GitHubReleasePackage package, string destinationFile,
        IProgress<long>? progress = null, CancellationToken ct = default)
    {
        var operationId = _logger?.BeginOperation("DownloadPackage", ("Owner", owner), ("Repository", repository), ("Package", package.Name), ("Kind", package.Kind.ToString()));
        await DownloadBinaryAsync(package.DownloadUrl, destinationFile, progress, ct);
        _logger?.Info(operationId ?? string.Empty, "Release package downloaded.", ("Destination", destinationFile), ("Kind", package.Kind.ToString()));
    }

    private async Task ValidatePackageAccessAsync(GitHubReleasePackage package, CancellationToken ct)
    {
        using var response = await SendDownloadRequestAsync(package.DownloadUrl, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"GitHub release package is not accessible. HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

        // ResponseHeadersRead + 2xx status is sufficient proof the resource is reachable.
        // Reading the body here would download the entire archive just for validation — avoid that.
    }

    private async Task DownloadBinaryAsync(
        string downloadUrl,
        string destinationFile,
        IProgress<long>? progress,
        CancellationToken ct)
    {
        using var response = await SendDownloadRequestAsync(downloadUrl, ct);
        await EnsureSuccess(response, "download release package");

        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 512, useAsync: true);

        var buffer = new byte[1024 * 512];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            total += read;
            progress?.Report(total);
        }
    }

    private async Task<HttpResponseMessage> SendDownloadRequestAsync(string downloadUrl, CancellationToken ct)
    {
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var absoluteUri))
            throw new InvalidOperationException($"Invalid package download URL: {downloadUrl}");

        // Downloads may point to GitHub's CDN or other hosts where the API-specific
        // default headers (Accept: application/vnd.github+json, X-GitHub-Api-Version, etc.)
        // can cause failures. For requests to a different host than the configured API
        // base address, use a lightweight HttpClient instance with only a User-Agent header.
        if (_http.BaseAddress is not null &&
            !string.Equals(absoluteUri.Host, _http.BaseAddress.Host, StringComparison.OrdinalIgnoreCase))
        {
            var tempClient = new HttpClient { Timeout = _http.Timeout };
            // Preserve Authorization if present so private asset downloads still work
            if (_http.DefaultRequestHeaders.Authorization is not null)
                tempClient.DefaultRequestHeaders.Authorization = _http.DefaultRequestHeaders.Authorization;
            tempClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitHubDeploymentManager", "1.0"));
            var tempRequest = new HttpRequestMessage(HttpMethod.Get, absoluteUri);

            var response = await tempClient.SendAsync(tempRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.IsSuccessStatusCode)
                return response;

            // Log the failure and try the configured API client as a fallback (some private
            // assets require the original HttpClient headers to authorize correctly).
            _logger?.Info("SendDownloadRequest", "Initial download attempt failed, trying API client as fallback.", ("Url", absoluteUri.ToString()), ("Status", (int)response.StatusCode));
            // Do not dispose the failed response yet; let it be collected by GC after tempClient is out of scope.

            var apiRequest = new HttpRequestMessage(HttpMethod.Get, absoluteUri);
            return await _http.SendAsync(apiRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        }

        var request = new HttpRequestMessage(HttpMethod.Get, absoluteUri);
        // If this is the asset API endpoint, request the binary stream explicitly.
        if (absoluteUri.AbsolutePath.Contains("/releases/assets/", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        }
        // Use the configured HttpClient (with API headers) for requests to the API host.
        return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private async Task<GitHubReleasePackage> SelectReleasePackageAsync(string owner, string repository, GitHubRelease release, CancellationToken ct)
    {
        var zipAsset = await SelectZipPackageAsync(owner, repository, release, ct);
        if (zipAsset is not null)
            return zipAsset;

        var exeAsset = SelectBestAsset(release.Assets, isExe: true);
        if (exeAsset is not null)
        {
            return new GitHubReleasePackage
            {
                Kind = GitHubReleasePackageKind.ExeAsset,
                Name = exeAsset.Name,
                DownloadUrl = exeAsset.Id > 0 ? BuildApiAssetUrl(owner, repository, exeAsset.Id) : exeAsset.BrowserDownloadUrl,
                Asset = exeAsset,
                DisplayLabel = $"EXE package: {exeAsset.Name}",
                Notes = "Direct executable asset"
            };
        }

        if (!string.IsNullOrWhiteSpace(release.ZipballUrl))
        {
            return new GitHubReleasePackage
            {
                Kind = GitHubReleasePackageKind.SourceCodeZip,
                Name = $"{GetReleaseLabel(release)}-source.zip",
                DownloadUrl = release.ZipballUrl,
                DisplayLabel = "Source code ZIP",
                Notes = "GitHub source archive"
            };
        }

        var sourceZipAsset = SelectBestAsset(release.Assets, isZip: true);
        if (sourceZipAsset is not null)
        {
            return new GitHubReleasePackage
            {
                Kind = GitHubReleasePackageKind.SourceCodeZip,
                Name = sourceZipAsset.Name,
                DownloadUrl = sourceZipAsset.Id > 0 ? BuildApiAssetUrl(owner, repository, sourceZipAsset.Id) : sourceZipAsset.BrowserDownloadUrl,
                Asset = sourceZipAsset,
                DisplayLabel = $"Source code ZIP: {sourceZipAsset.Name}",
                Notes = "ZIP asset without a discoverable executable"
            };
        }

        throw new InvalidOperationException("The selected release does not contain a deployable ZIP, EXE, or source code archive.");
    }

    private async Task<GitHubReleasePackage?> SelectZipPackageAsync(string owner, string repository, GitHubRelease release, CancellationToken ct)
    {
        var zipAsset = SelectBestAsset(release.Assets, isZip: true);
        if (zipAsset is null)
            return null;

        var tempZip = Path.Combine(Path.GetTempPath(), "GitHubDeploymentManager", Guid.NewGuid().ToString("N") + ".zip");
        Directory.CreateDirectory(Path.GetDirectoryName(tempZip)!);

        try
        {
            var tempDownloadUrl = zipAsset.Id > 0 ? BuildApiAssetUrl(owner, repository, zipAsset.Id) : zipAsset.BrowserDownloadUrl;
            await DownloadBinaryAsync(tempDownloadUrl, tempZip, progress: null, ct);
            ct.ThrowIfCancellationRequested();

            if (ArchiveContainsExecutable(tempZip))
            {
                return new GitHubReleasePackage
                {
                    Kind = GitHubReleasePackageKind.ZipAsset,
                    Name = zipAsset.Name,
                    DownloadUrl = zipAsset.Id > 0 ? BuildApiAssetUrl(owner, repository, zipAsset.Id) : zipAsset.BrowserDownloadUrl,
                    Asset = zipAsset,
                    DisplayLabel = $"ZIP package: {zipAsset.Name}",
                    Notes = "ZIP package containing an executable"
                };
            }
        }
        finally
        {
            try
            {
                if (File.Exists(tempZip))
                    File.Delete(tempZip);
            }
            catch
            {
            }
        }

        _logger?.Info("SelectZipPackage", "No executable found in ZIP asset.", ("Owner", owner), ("Repository", repository), ("Release", release.TagName), ("Asset", zipAsset.Name));
        return null;
    }

    private static bool ArchiveContainsExecutable(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        return archive.Entries.Any(entry => entry.FullName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }

    private GitHubReleaseAsset? SelectBestAsset(IEnumerable<GitHubReleaseAsset> assets, bool isZip = false, bool isExe = false)
    {
        var candidates = assets.Where(asset =>
            isZip ? asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) :
            isExe ? asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) :
            false).ToList();

        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        // When multiple ZIPs exist, prefer the one that explicitly targets Windows
        // (e.g. "Anatine-windows-1.1.0.zip") over other platforms and source archives.
        if (isZip)
        {
            var windowsCandidate = candidates
                .Where(a => a.Name.Contains("win", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => a.Size)
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (windowsCandidate is not null)
                return windowsCandidate;
        }

        return candidates
            .OrderByDescending(asset => asset.Size)
            .ThenBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static string GetReleaseLabel(GitHubRelease release)
        => string.IsNullOrWhiteSpace(release.TagName) ? release.Name : release.TagName;

    private string BuildApiAssetUrl(string owner, string repository, long assetId)
    {
        // Construct an absolute API URL for the release asset endpoint that returns the binary when
        // requested with Accept: application/octet-stream.
        var baseAddress = _http.BaseAddress?.ToString() ?? "https://api.github.com/";
        return new Uri(new Uri(baseAddress), $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}/releases/assets/{assetId}").ToString();
    }

    private static async Task EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync();
        var detail = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body;

        if (response.StatusCode == HttpStatusCode.Forbidden && IsRateLimitResponse(body))
        {
            var resetText = TryGetRateLimitResetText(response);
            var guidance = string.IsNullOrWhiteSpace(resetText)
                ? "Add a GitHubToken value in App.config to use authenticated requests with a higher rate limit."
                : $"Try again after {resetText}, or add a GitHubToken value in App.config to use authenticated requests with a higher rate limit.";

            throw new HttpRequestException($"Unable to {operation}. GitHub API rate limit exceeded. {guidance}");
        }

        throw new HttpRequestException($"Unable to {operation}. HTTP {(int)response.StatusCode}: {detail}");
    }

    private static bool IsRateLimitResponse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("message", out var messageElement))
            {
                var message = messageElement.GetString();
                return !string.IsNullOrWhiteSpace(message) &&
                    message.Contains("rate limit exceeded", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (JsonException)
        {
        }

        return body.Contains("rate limit exceeded", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetRateLimitResetText(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("X-RateLimit-Reset", out var values))
            return null;

        var resetValue = values.FirstOrDefault();
        if (!long.TryParse(resetValue, out var resetUnixSeconds))
            return null;

        var resetTime = DateTimeOffset.FromUnixTimeSeconds(resetUnixSeconds).ToLocalTime();
        return resetTime.ToString("yyyy-MM-dd HH:mm:ss zzz");
    }

    private bool TryGetCacheValue<T>(
        ConcurrentDictionary<string, CacheEntry<T>> cache,
        string key,
        [NotNullWhen(true)] out T? value)
    {
        value = default;
        if (_cacheDuration <= TimeSpan.Zero)
            return false;

        if (cache.TryGetValue(key, out var entry) && entry.ExpiresAtUtc > DateTimeOffset.UtcNow)
        {
            var cachedValue = entry.Value;
            if (cachedValue is not null)
            {
                value = cachedValue;
                return true;
            }
        }

        cache.TryRemove(key, out _);
        return false;
    }

    private void SetCacheValue<T>(ConcurrentDictionary<string, CacheEntry<T>> cache, string key, T value)
    {
        if (_cacheDuration <= TimeSpan.Zero)
            return;

        cache[key] = new CacheEntry<T>(value, DateTimeOffset.UtcNow.Add(_cacheDuration));
    }

    private sealed record CacheEntry<T>(T Value, DateTimeOffset ExpiresAtUtc);

    public void ClearCache()
    {
        _repositoryCache.Clear();
        _releaseCache.Clear();
        _logger?.Info("Cache cleared.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
