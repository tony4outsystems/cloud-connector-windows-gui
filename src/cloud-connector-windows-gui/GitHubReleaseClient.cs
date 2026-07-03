using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CloudConnectorWindowsGui;

internal sealed class GitHubReleaseClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient httpClient;
    private readonly string releasesUrl;
    private readonly string emptyReleaseMessage;

    public GitHubReleaseClient(string owner, string repository, string emptyReleaseMessage)
        : this(CreateHttpClient(), owner, repository, emptyReleaseMessage)
    {
    }

    internal GitHubReleaseClient(HttpClient httpClient, string owner, string repository, string emptyReleaseMessage)
    {
        this.httpClient = httpClient;
        releasesUrl = $"https://api.github.com/repos/{owner}/{repository}/releases";
        this.emptyReleaseMessage = emptyReleaseMessage;
    }

    public async Task<GitHubRelease> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        using var stream = await httpClient.GetStreamAsync(releasesUrl, cancellationToken).ConfigureAwait(false);
        var releases = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("GitHub returned an empty release list.");

        return releases
            .Where(release => !release.Draft && !release.Prerelease)
            .OrderByDescending(release => ParseVersion(release.TagName))
            .ThenByDescending(release => release.PublishedAt ?? release.CreatedAt)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(emptyReleaseMessage);
    }

    public async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    public static bool IsVersionNewer(string candidateVersion, string currentVersion)
    {
        return ParseVersion(candidateVersion).CompareTo(ParseVersion(currentVersion)) > 0;
    }

    public static Version ParseVersion(string tagName)
    {
        var normalized = tagName.TrimStart('v', 'V');
        return Version.TryParse(normalized, out var version) ? version : new Version(0, 0);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("cloud-connector-windows-gui", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}

internal sealed record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string TagName,
    bool Draft,
    bool Prerelease,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt,
    IReadOnlyList<GitHubReleaseAsset> Assets);

internal sealed record GitHubReleaseAsset(
    string Name,
    [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl,
    string? Digest);
