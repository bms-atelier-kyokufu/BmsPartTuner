using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BmsAtelierKyokufu.BmsPartTuner.ViewModels;

/// <summary>
/// 外部メディアプレイヤーの制御を担当するViewModel。
/// 責務: テスト再生、プレイヤー起動管理
/// </summary>
public partial class MediaPlaybackViewModel : ObservableObject
{
    /// <summary>プレイヤーパスが設定されているかどうか。</summary>
    [ObservableProperty]
    private bool isPlayerConfigured;

    /// <summary>テスト再生可能かどうか。</summary>
    [ObservableProperty]
    private bool canPlayback;

    /// <summary>
    /// テスト再生をリクエストするイベント。
    /// (UI層から呼び出す)
    /// </summary>
    public event EventHandler<PlaybackRequestEventArgs>? PlaybackRequested;

    /// <summary>
    /// プレイヤー起動エラーが発生したイベント。
    /// </summary>
    public event EventHandler<string>? PlaybackError;

    /// <summary>
    /// テスト再生状態が変わったイベント。
    /// </summary>
    public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;

    /// <summary>
    /// MediaPlaybackViewModelを初期化。
    /// </summary>
    public MediaPlaybackViewModel()
    {
        IsPlayerConfigured = false;
        CanPlayback = false;
    }

    /// <summary>
    /// プレイヤーパスを設定して状態を更新。
    /// </summary>
    public void SetPlayerPath(string? playerPath)
    {
        if (string.IsNullOrWhiteSpace(playerPath) || !File.Exists(playerPath))
        {
            IsPlayerConfigured = false;
            CanPlayback = false;
            return;
        }

        IsPlayerConfigured = true;
        CanPlayback = true;
    }

    /// <summary>
    /// テスト再生コマンド。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPlayback))]
    private void TestPlay()
    {
        PlaybackRequested?.Invoke(this, new PlaybackRequestEventArgs());
    }

    /// <summary>
    /// プレイヤーを起動。
    /// </summary>
    public void LaunchPlayer(string playerPath, string targetFile, string fileType)
    {
        if (!IsPlayerConfigured)
        {
            PlaybackError?.Invoke(this, "外部プレイヤーが設定されていません。");
            return;
        }

        if (!File.Exists(playerPath))
        {
            PlaybackError?.Invoke(this, $"プレイヤーが見つかりません: {playerPath}");
            return;
        }

        if (!File.Exists(targetFile))
        {
            PlaybackError?.Invoke(this, $"再生ファイルが見つかりません: {targetFile}");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = playerPath,
                Arguments = $"\"{targetFile}\"",
                UseShellExecute = true
            };

            Process.Start(psi);
            PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs
            {
                IsPlaying = true,
                FileName = Path.GetFileName(targetFile),
                FileType = fileType
            });
        }
        catch (Exception ex)
        {
            PlaybackError?.Invoke(this, $"プレイヤーの起動に失敗しました: {ex.Message}");
        }
    }

    #region イベント引数クラス

    /// <summary>
    /// テスト再生リクエストのイベント引数。
    /// </summary>
    public class PlaybackRequestEventArgs : EventArgs
    {
    }

    /// <summary>
    /// 再生状態変化のイベント引数。
    /// </summary>
    public class PlaybackStateChangedEventArgs : EventArgs
    {
        public bool IsPlaying { get; set; }
        public string? FileName { get; set; }
        public string? FileType { get; set; }
    }

    #endregion
}
