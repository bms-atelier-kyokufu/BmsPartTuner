using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BmsAtelierKyokufu.BmsPartTuner.Services;

/// <summary>
/// Chrome-style の自動アップデートサービス。
/// </summary>
/// <remarks>
/// <para>【動作概要】</para>
/// <list type="number">
/// <item>アプリ起動時にバックグラウンドでGitHub Releases APIを確認</item>
/// <item>新しいバージョンがあればインストーラーを一時フォルダにダウンロード</item>
/// <item>アプリ終了時にインストーラーを起動</item>
/// </list>
/// 
/// <para>【Why 終了時更新】</para>
/// ユーザーの作業を中断させず、次回起動時に最新版にするため。
/// </remarks>
public class UpdateService : IDisposable
{
    private const string GitHubApiUrl = "https://api.github.com/repos/rian-eimu/BmsPartTuner/releases/latest";
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
    /// バックグラウンドでアップデートを確認します。
    /// </summary>
    /// <remarks>
    /// <para>【Why Task.Run不使用】</para>
    /// 呼び出し元でTask.Runを使用することを想定しているため、
    /// このメソッド自体はasyncのみとしています。
    /// </remarks>
    public async Task CheckForUpdatesAsync()
    {
        try
        {
            Debug.WriteLine("=== Checking for updates ===");

            Version? currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            Debug.WriteLine($"Current version: {currentVersion}");

            GitHubRelease? releaseInfo = await GetLatestReleaseInfoAsync();
            if (releaseInfo == null)
            {
                Debug.WriteLine("Failed to get release info");
                return;
            }

            Version? latestVersion = ParseVersion(releaseInfo.TagName);
            if (latestVersion == null)
            {
                Debug.WriteLine($"Failed to parse version from tag: {releaseInfo.TagName}");
                return;
            }

            Debug.WriteLine($"Latest version: {latestVersion}");

            if (currentVersion != null && latestVersion > currentVersion)
            {
                AvailableVersion = latestVersion;
                Debug.WriteLine($"New version available: {latestVersion}");

                await DownloadInstallerAsync(releaseInfo);
            }
            else
            {
                Debug.WriteLine("Already up to date");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check failed: {ex.Message}");
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
                Debug.WriteLine($"GitHub API returned {response.StatusCode}");
                return null;
            }

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return await response.Content.ReadFromJsonAsync<GitHubRelease>(options);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to fetch release info: {ex.Message}");
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
            .FirstOrDefault(a =>
                a.Name?.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) == true ||
                a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);

        if (installerAsset?.Name == null || string.IsNullOrEmpty(installerAsset.BrowserDownloadUrl))
        {
            Debug.WriteLine("No installer asset found in release");
            return;
        }

        try
        {
            Debug.WriteLine($"Downloading installer: {installerAsset.Name}");

            var tempPath = Path.Combine(Path.GetTempPath(), installerAsset.Name);

            using HttpResponseMessage response = await _httpClient.GetAsync(installerAsset.BrowserDownloadUrl);
            response.EnsureSuccessStatusCode();

            await using FileStream fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);

            _updateInstallerPath = tempPath;
            Debug.WriteLine($"Installer downloaded to: {tempPath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to download installer: {ex.Message}");
        }
    }

    /// <summary>
    /// アップデートインストーラーを起動します。
    /// </summary>
    /// <remarks>
    /// <para>【呼び出しタイミング】</para>
    /// App.OnExitで呼び出されることを想定しています。
    /// </remarks>
    public void LaunchUpdateInstaller()
    {
        if (!IsUpdateReady)
        {
            Debug.WriteLine("No update ready to install");
            return;
        }

        try
        {
            Debug.WriteLine($"Launching installer: {_updateInstallerPath}");
            Process.Start(new ProcessStartInfo
            {
                FileName = _updateInstallerPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to launch installer: {ex.Message}");
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
        var versionString = tagName.TrimStart('v', 'V');

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
