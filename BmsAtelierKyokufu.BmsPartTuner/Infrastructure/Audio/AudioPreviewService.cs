using BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Audio.AudioPlayer;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Audio;

/// <summary>
/// 音声ファイルのバックグラウンド再生・プレビュー機能を提供するサービス。
/// UIスレッドのブロック回避や連続再生要求のデバウンス処理を担います。
/// </summary>
public class AudioPreviewService(BmsPartTuner.UI.Services.IUIThreadDispatcher dispatcher, IAudioPlayerFactory? playerFactory = null) : IDisposable
{
    private bool _disposed;
    private IAudioPlayer? _currentPlayer;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly BmsPartTuner.UI.Services.IUIThreadDispatcher _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    private readonly IAudioPlayerFactory _playerFactory = playerFactory ?? new NAudioPlayerFactory();

    /// <summary>
    /// 再生状態（読み込み中、再生中、エラーなど）が変更された際に発生するイベント。
    /// </summary>
    public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;

    /// <summary>
    /// 指定された音声ファイルの再生（プレビュー）を非同期に開始します。
    /// 連続して呼び出された場合はデバウンス処理により最後の要求のみが処理されます。
    /// </summary>
    public async Task PreviewAudioAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        StopCurrentPlayback();

        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;

        try
        {
            await Task.Delay(Core.AppConstants.UI.AudioPreviewDelayMs, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            NotifyStateChanged(null, isLoading: true);

            await Task.Run(async () =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                await _dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        _currentPlayer = _playerFactory.CreatePlayer();
                        _currentPlayer.Play(filePath);

                        NotifyStateChanged(Path.GetFileName(filePath), isPlaying: true);
                    }
                    catch (Exception ex)
                    {
                        NotifyStateChanged(null, errorMessage: $"再生エラー: {ex.Message}");
                    }
                });
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            NotifyStateChanged(null, errorMessage: ex.Message);
        }
    }

    /// <summary>
    /// 現在の再生処理を停止し、リソースを解放します。
    /// </summary>
    public void StopCurrentPlayback()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _currentPlayer?.Stop();
        _currentPlayer?.Dispose();
        _currentPlayer = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopCurrentPlayback();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void NotifyStateChanged(string? fileName = null, bool isLoading = false,
        bool isPlaying = false, string? errorMessage = null)
    {
        PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs
        {
            FileName = fileName,
            IsLoading = isLoading,
            IsPlaying = isPlaying,
            ErrorMessage = errorMessage
        });
    }

    /// <summary>
    /// 再生状態変更イベントの引数を提供します。
    /// </summary>
    public class PlaybackStateChangedEventArgs : EventArgs
    {
        public string? FileName { get; set; }
        public bool IsLoading { get; set; }
        public bool IsPlaying { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
