using System.Text.Json.Serialization;

namespace CloudConnectorGui.App;

public sealed record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string TagName,
    bool Draft,
    bool Prerelease,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt,
    IReadOnlyList<GitHubReleaseAsset> Assets);

public sealed record GitHubReleaseAsset(
    string Name,
    [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl,
    string? Digest);
