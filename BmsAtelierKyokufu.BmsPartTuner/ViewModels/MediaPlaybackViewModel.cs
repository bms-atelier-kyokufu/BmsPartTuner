namespace BmsAtelierKyokufu.BmsPartTuner.ViewModels;

/// <summary>
/// 外部メディアプレイヤーの制御を担当するViewModel。
/// </summary>
public partial class MediaPlaybackViewModel : ObservableObject
{
    /// <summary>
    /// 外部プレイヤーのパスが設定されているかどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsPlayerConfigured { get; set; }

    /// <summary>
    /// 現在の状態でテスト再生が可能かどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool CanPlayback { get; set; }

    /// <summary>
    /// UI層からのテスト再生リクエストを通知するイベント。
    /// </summary>
    public event EventHandler<PlaybackRequestEventArgs>? PlaybackRequested;

    /// <summary>
    /// プレイヤー起動時にエラーが発生した際に発生するイベント。
    /// </summary>
    public event EventHandler<string>? PlaybackError;

    /// <summary>
    /// テスト再生の状態が変化した際に発生するイベント。
    /// </summary>
    public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;

    public MediaPlaybackViewModel()
    {
        IsPlayerConfigured = false;
        CanPlayback = false;
    }

    /// <summary>
    /// 外部プレイヤーの実行ファイルパスを設定し、再生可能状態を更新します。
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

    [RelayCommand(CanExecute = nameof(CanPlayback))]
    private void TestPlay()
    {
        PlaybackRequested?.Invoke(this, new PlaybackRequestEventArgs());
    }

    /// <summary>
    /// 指定されたプレイヤーを使用して対象ファイルを再生します。
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

    /// <summary>
    /// テスト再生リクエストのイベント引数を提供します。
    /// </summary>
    public class PlaybackRequestEventArgs : EventArgs
    {
    }

    /// <summary>
    /// 再生状態変化のイベント引数を提供します。
    /// </summary>
    public class PlaybackStateChangedEventArgs : EventArgs
    {
        public bool IsPlaying { get; set; }
        public string? FileName { get; set; }
        public string? FileType { get; set; }
    }
}
