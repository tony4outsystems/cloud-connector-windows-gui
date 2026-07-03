using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace CloudConnectorGui.App;

public sealed class CloudConnectorBinaryManager
{
    private static readonly string ExecutableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? "outsystemscc.exe"
        : "outsystemscc";

    private const string VersionFileName = "version.txt";

    private readonly GitHubReleaseClient releaseClient;
    private readonly string installDirectory;

    public CloudConnectorBinaryManager()
        : this(new GitHubReleaseClient("tony4outsystems", "cloud-connector", "No stable cloud-connector release was found."), GetDefaultInstallDirectory())
    {
    }

    public CloudConnectorBinaryManager(GitHubReleaseClient releaseClient, string installDirectory)
    {
        this.releaseClient = releaseClient;
        this.installDirectory = installDirectory;
    }

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
        var release = await releaseClient.GetLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
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
        var release = await releaseClient.GetLatestReleaseAsync(cancellationToken).ConfigureAwait(false);

        if (File.Exists(ExecutablePath) && IsInstalledVersionLatest(release.TagName))
        {
            return new InstallResult(release.TagName, false);
        }

        var asset = SelectPlatformAsset(release);
        progress?.Report($"Downloading {asset.Name}...");
        Directory.CreateDirectory(installDirectory);

        var archivePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-{asset.Name}");
        var stagingDirectory = Path.Combine(Path.GetTempPath(), $"cloud-connector-{Guid.NewGuid():N}");

        try
        {
            await releaseClient.DownloadFileAsync(asset.BrowserDownloadUrl, archivePath, cancellationToken).ConfigureAwait(false);
            await VerifyDigestAsync(asset, archivePath, cancellationToken).ConfigureAwait(false);

            progress?.Report("Installing connector binary...");
            Directory.CreateDirectory(stagingDirectory);
            ExtractTarGz(archivePath, stagingDirectory);

            var extractedExecutable = Directory
                .EnumerateFiles(stagingDirectory, ExecutableName, SearchOption.AllDirectories)
                .FirstOrDefault();
            if (extractedExecutable is null)
            {
                throw new InvalidOperationException($"{ExecutableName} was not found in {asset.Name}.");
            }

            Directory.CreateDirectory(installDirectory);
            File.Copy(extractedExecutable, ExecutablePath, overwrite: true);
            MarkExecutable(ExecutablePath);
            File.WriteAllText(Path.Combine(installDirectory, VersionFileName), release.TagName);

            return new InstallResult(release.TagName, true);
        }
        finally
        {
            TryDeleteFile(archivePath);
            TryDeleteDirectory(stagingDirectory);
        }
    }

    private static GitHubReleaseAsset SelectPlatformAsset(GitHubRelease release)
    {
        var platform = GetPlatformName();
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "386",
            Architecture.Arm64 => "arm64",
            _ => "amd64"
        };

        var asset = release.Assets.FirstOrDefault(candidate =>
            candidate.Name.Contains($"_{platform}_", StringComparison.OrdinalIgnoreCase)
            && candidate.Name.Contains($"_{architecture}.", StringComparison.OrdinalIgnoreCase)
            && candidate.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase));

        return asset ?? throw new InvalidOperationException($"Release {release.TagName} does not include a {platform} {architecture} archive.");
    }

    private static string GetPlatformName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "darwin";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "linux";
        }

        throw new PlatformNotSupportedException($"Unsupported platform: {RuntimeInformation.OSDescription}.");
    }

    private static void MarkExecutable(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var mode = File.GetUnixFileMode(path);
        File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }

    private static async Task VerifyDigestAsync(GitHubReleaseAsset asset, string archivePath, CancellationToken cancellationToken)
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

    private static string GetDefaultInstallDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var productName = Assembly.GetExecutingAssembly().GetName().Name ?? "cloud-connector-gui";
        return Path.Combine(localAppData, productName, "cloud-connector");
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
}
