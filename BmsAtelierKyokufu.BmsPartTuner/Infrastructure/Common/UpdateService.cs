using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Common;

/// <summary>
/// アプリケーションの自動アップデート機能（Chromeスタイル）を提供するサービス。
/// バックグラウンドで新しいリリースを確認し、ダウンロード後、アプリ終了時にインストールを実行します。
/// これにより、ユーザーの作業を中断させることなく最新版への更新を実現します。
/// </summary>
[ADRAnchor("OPT-08", nameof(UpdateService))]
public class UpdateService : IUpdateService, IDisposable
{
    private static readonly IPerformanceLogger s_logger = new TypedLogger(typeof(UpdateService));
    private const string GitHubApiUrl = "https://api.github.com/repos/bms-atelier-kyokufu/BmsPartTuner/releases/latest";
    private const string UserAgent = "BmsPartTuner-UpdateChecker";

    private readonly HttpClient _httpClient;
    private string? _updateInstallerPath;
    private bool _disposed;

    /// <summary>
    /// アップデートの準備ができているかどうか。
    /// </summary>
    public bool IsUpdateReady => !string.IsNullOrEmpty(_updateInstallerPath) && File.Exists(_updateInstallerPath);

    /// <summary>
    /// 利用可能な新しいバージョン（nullの場合は最新）。
    /// </summary>
    public Version? AvailableVersion { get; private set; }

    public UpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// GitHub APIを利用してバックグラウンドでアップデートの有無を確認し、
    /// 新しいバージョンが利用可能な場合はインストーラーをダウンロードします。
    /// </summary>
    public async Task CheckForUpdatesAsync()
    {
        try
        {
            s_logger.WriteDebug( "=== Checking for updates ===");

            Version? currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            s_logger.WriteDebug( $"Current version: {currentVersion}");

            GitHubRelease? releaseInfo = await GetLatestReleaseInfoAsync();
            if (releaseInfo == null)
            {
                s_logger.WriteDebug( "Failed to get release info");
                return;
            }

            Version? latestVersion = ParseVersion(releaseInfo.TagName);
            if (latestVersion == null)
            {
                s_logger.WriteDebug( $"Failed to parse version from tag: {releaseInfo.TagName}");
                return;
            }

            s_logger.WriteDebug( $"Latest version: {latestVersion}");

            if (currentVersion != null && latestVersion > currentVersion)
            {
                AvailableVersion = latestVersion;
                s_logger.WriteDebug( $"New version available: {latestVersion}");

                await DownloadInstallerAsync(releaseInfo);
            }
            else
            {
                s_logger.WriteDebug( "Already up to date");
            }
        }
        catch (Exception ex)
        {
            s_logger.WriteDebug( $"Update check failed: {ex.Message}");
        }
    }

    /// <summary>
    /// GitHub Releases API から最新リリース情報を取得します。
    /// </summary>
    private async Task<GitHubRelease?> GetLatestReleaseInfoAsync()
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(GitHubApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                s_logger.WriteDebug( $"GitHub API returned {response.StatusCode}");
                return null;
            }

            JsonSerializerOptions options = new()
            {
                PropertyNameCaseInsensitive = true
            };

            return await response.Content.ReadFromJsonAsync<GitHubRelease>(options);
        }
        catch (Exception ex)
        {
            s_logger.WriteDebug( $"Failed to fetch release info: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// インストーラーをダウンロードします。
    /// </summary>
    private async Task DownloadInstallerAsync(GitHubRelease release)
    {
        // .msi または .exe アセットを探す
        GitHubAsset? installerAsset = release.Assets?
            .FirstOrDefault(static a =>
                a.Name?.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) is true ||
                a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) is true);

        if (installerAsset?.Name == null || string.IsNullOrEmpty(installerAsset.BrowserDownloadUrl))
        {
            s_logger.WriteDebug( "No installer asset found in release");
            return;
        }

        try
        {
            s_logger.WriteDebug( $"Downloading installer: {installerAsset.Name}");

            string tempPath = Path.Combine(Path.GetTempPath(), installerAsset.Name);

            using HttpResponseMessage response = await _httpClient.GetAsync(installerAsset.BrowserDownloadUrl);
            response.EnsureSuccessStatusCode();

            await using FileStream fileStream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);

            _updateInstallerPath = tempPath;
            s_logger.WriteDebug( $"Installer downloaded to: {tempPath}");
        }
        catch (Exception ex)
        {
            s_logger.WriteDebug( $"Failed to download installer: {ex.Message}");
        }
    }

    /// <summary>
    /// ダウンロード済みのアップデートインストーラーを起動します。
    /// 通常、アプリケーションの終了時（App.OnExit）に呼び出されます。
    /// </summary>
    public void LaunchUpdateInstaller()
    {
        if (!IsUpdateReady)
        {
            s_logger.WriteDebug( "No update ready to install");
            return;
        }

        try
        {
            s_logger.WriteDebug( $"Launching installer: {_updateInstallerPath}");
            Process.Start(new ProcessStartInfo
            {
                FileName = _updateInstallerPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            s_logger.WriteDebug( $"Failed to launch installer: {ex.Message}");
        }
    }

    /// <summary>
    /// バージョン文字列をパースします。
    /// </summary>
    /// <param name="tagName">タグ名（例: "v1.0.0"）</param>
    private static Version? ParseVersion(string? tagName)
    {
        if (string.IsNullOrEmpty(tagName))
            return null;

        // "v" プレフィックスを除去
        string versionString = tagName.TrimStart('v', 'V');

        return Version.TryParse(versionString, out Version? version) ? version : null;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _httpClient.Dispose();
        }
        _disposed = true;
    }

    #region GitHub API DTOs

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }

    #endregion
}
