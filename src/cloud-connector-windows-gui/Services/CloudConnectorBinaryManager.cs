using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CloudConnectorWindowsGui.Services;

internal sealed class CloudConnectorBinaryManager
{
    private const string ReleasesUrl = "https://api.github.com/repos/tony4outsystems/cloud-connector/releases";
    private const string BaseExecutableName = "outsystemscc";
    private const string VersionFileName = "version.txt";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient httpClient;
    private readonly string installDirectory;

    public CloudConnectorBinaryManager()
        : this(CreateHttpClient(), GetDefaultInstallDirectory())
    {
    }

    internal CloudConnectorBinaryManager(HttpClient httpClient, string installDirectory)
    {
        this.httpClient = httpClient;
        this.installDirectory = installDirectory;
    }

    public string ExecutableName => GetCurrentPlatform().ExecutableName;

    public string ExecutablePath => Path.Combine(installDirectory, ExecutableName);

    public string? InstalledVersion
    {
        get
        {
            var versionPath = Path.Combine(installDirectory, VersionFileName);
            return File.Exists(versionPath) ? File.ReadAllText(versionPath).Trim() : null;
        }
    }

    public async Task<BinaryVersionStatus> GetVersionStatusAsync(CancellationToken cancellationToken = default)
    {
        var release = await GetLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
        return new BinaryVersionStatus(InstalledVersion, release.TagName, IsInstalledVersionLatest(release.TagName));
    }

    public async Task<InstallResult> EnsureInstalledAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (File.Exists(ExecutablePath))
        {
            return new InstallResult(InstalledVersion, false);
        }

        return await InstallLatestAsync(progress, cancellationToken).ConfigureAwait(false);
    }

    public async Task<InstallResult> InstallLatestAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report("Checking GitHub releases...");
        var release = await GetLatestReleaseAsync(cancellationToken).ConfigureAwait(false);

        if (File.Exists(ExecutablePath) && IsInstalledVersionLatest(release.TagName))
        {
            return new InstallResult(release.TagName, false);
        }

        var platform = GetCurrentPlatform();
        var asset = SelectPlatformAsset(release, platform);
        progress?.Report($"Downloading {asset.Name}...");
        Directory.CreateDirectory(installDirectory);

        var archivePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-{asset.Name}");
        var stagingDirectory = Path.Combine(Path.GetTempPath(), $"cloud-connector-{Guid.NewGuid():N}");

        try
        {
            await DownloadFileAsync(asset.BrowserDownloadUrl, archivePath, cancellationToken).ConfigureAwait(false);
            await VerifyDigestAsync(asset, archivePath, cancellationToken).ConfigureAwait(false);

            progress?.Report("Installing connector binary...");
            Directory.CreateDirectory(stagingDirectory);
            ExtractTarGz(archivePath, stagingDirectory);

            var extractedExecutable = Directory
                .EnumerateFiles(stagingDirectory, platform.ExecutableName, SearchOption.AllDirectories)
                .FirstOrDefault();
            if (extractedExecutable is null)
            {
                throw new InvalidOperationException($"{platform.ExecutableName} was not found in {asset.Name}.");
            }

            Directory.CreateDirectory(installDirectory);
            File.Copy(extractedExecutable, ExecutablePath, overwrite: true);
            MakeExecutableOnUnix(ExecutablePath);
            File.WriteAllText(Path.Combine(installDirectory, VersionFileName), release.TagName);

            return new InstallResult(release.TagName, true);
        }
        finally
        {
            TryDeleteFile(archivePath);
            TryDeleteDirectory(stagingDirectory);
        }
    }

    private async Task<Release> GetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        using var stream = await httpClient.GetStreamAsync(ReleasesUrl, cancellationToken).ConfigureAwait(false);
        var releases = await JsonSerializer.DeserializeAsync<List<Release>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("GitHub returned an empty release list.");

        return releases
            .Where(release => !release.Draft && !release.Prerelease)
            .OrderByDescending(release => ParseVersion(release.TagName))
            .ThenByDescending(release => release.PublishedAt ?? release.CreatedAt)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No stable cloud-connector release was found.");
    }

    private static ReleaseAsset SelectPlatformAsset(Release release, ConnectorPlatform platform)
    {
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "386",
            Architecture.Arm64 => "arm64",
            _ => "amd64"
        };

        var asset = release.Assets.FirstOrDefault(candidate =>
            candidate.Name.Contains($"_{platform.ReleaseOsName}_", StringComparison.OrdinalIgnoreCase)
            && candidate.Name.Contains($"_{architecture}.", StringComparison.OrdinalIgnoreCase)
            && candidate.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase));

        return asset ?? throw new InvalidOperationException($"Release {release.TagName} does not include a {platform.DisplayName} {architecture} archive.");
    }

    private static ConnectorPlatform GetCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new ConnectorPlatform("windows", "Windows", $"{BaseExecutableName}.exe");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || OperatingSystem.IsMacCatalyst())
        {
            return new ConnectorPlatform("darwin", "macOS", BaseExecutableName);
        }

        throw new PlatformNotSupportedException("This launcher can download connector binaries only on Windows and macOS.");
    }

    private static void MakeExecutableOnUnix(string executablePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        File.SetUnixFileMode(
            executablePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    private async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    private static async Task VerifyDigestAsync(ReleaseAsset asset, string archivePath, CancellationToken cancellationToken)
    {
        if (asset.Digest is null || !asset.Digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await using var stream = File.OpenRead(archivePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        var expected = asset.Digest["sha256:".Length..].ToLowerInvariant();
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Downloaded connector archive failed SHA-256 verification.");
        }
    }

    private static void ExtractTarGz(string archivePath, string destinationDirectory)
    {
        using var file = File.OpenRead(archivePath);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        TarFile.ExtractToDirectory(gzip, destinationDirectory, overwriteFiles: true);
    }

    private bool IsInstalledVersionLatest(string latestVersion)
    {
        var installedVersion = InstalledVersion;
        return string.Equals(installedVersion, latestVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static Version ParseVersion(string tagName)
    {
        var normalized = tagName.TrimStart('v', 'V');
        return Version.TryParse(normalized, out var version) ? version : new Version(0, 0);
    }

    private static string GetDefaultInstallDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var productName = Assembly.GetExecutingAssembly().GetName().Name ?? "cloud-connector-windows-gui";
        return Path.Combine(localAppData, productName, "cloud-connector");
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("cloud-connector-windows-gui", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record Release(
        [property: JsonPropertyName("tag_name")] string TagName,
        bool Draft,
        bool Prerelease,
        [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
        [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt,
        IReadOnlyList<ReleaseAsset> Assets);

    private sealed record ReleaseAsset(
        string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl,
        string? Digest);

    private sealed record ConnectorPlatform(string ReleaseOsName, string DisplayName, string ExecutableName);
}

internal sealed record BinaryVersionStatus(string? CurrentVersion, string LatestVersion, bool IsLatest);

internal sealed record InstallResult(string? Version, bool Installed);
