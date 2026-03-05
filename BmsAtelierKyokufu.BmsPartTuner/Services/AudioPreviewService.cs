using System.Threading;
using BmsAtelierKyokufu.BmsPartTuner.Services.AudioPlayer;

namespace BmsAtelierKyokufu.BmsPartTuner.Services;

/// <summary>
/// 音声プレビューサービス。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>WAVファイルの再生とプレビュー機能</item>
/// <item>デバウンス機能（連続クリック対策）</item>
/// <item>再生状態の通知（イベント駆動）</item>
/// </list>
/// 
/// <para>【デバウンス機構】</para>
/// ユーザーが連続してファイルを選択した場合、
/// 300msの遅延を設けることで、最後に選択されたファイルのみを再生します。
/// これにより、無駄なディスクI/Oと再生処理を削減します。
/// 
/// <para>【非同期処理】</para>
/// UIスレッドをブロックしないよう、ファイル読み込みと再生を
/// 非同期で実行します。
/// 
/// <para>【リソース管理】</para>
/// WaveOutとAudioFileReaderは適切にDispose処理を行い、
/// メモリリークを防ぎます。
/// </remarks>
public class AudioPreviewService : IDisposable
{
    #region フィールド

    private IAudioPlayer? _currentPlayer;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly IAudioPlayerFactory _playerFactory;

    #endregion

    #region イベント

    /// <summary>
    /// 再生状態が変更された時のイベント。
    /// </summary>
    public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;

    /// <summary>
    /// 再生状態変更イベントの引数。
    /// </summary>
    public class PlaybackStateChangedEventArgs : EventArgs
    {
        /// <summary>ファイル名。</summary>
        public string? FileName { get; set; }

        /// <summary>読み込み中かどうか。</summary>
        public bool IsLoading { get; set; }

        /// <summary>再生中かどうか。</summary>
        public bool IsPlaying { get; set; }

        /// <summary>エラーメッセージ。</summary>
        public string? ErrorMessage { get; set; }
    }

    #endregion

    #region コンストラクタ

    /// <summary>
    /// AudioPreviewServiceのインスタンスを作成。
    /// </summary>
    /// <param name="dispatcher">UIスレッドのディスパッチャー。</param>
    /// <param name="playerFactory">AudioPlayerのファクトリー。</param>
    /// <exception cref="ArgumentNullException">dispatcherまたはplayerFactoryがnullの場合。</exception>
    public AudioPreviewService(IUIThreadDispatcher dispatcher, IAudioPlayerFactory? playerFactory = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _playerFactory = playerFactory ?? new NAudioPlayerFactory();
    }

    #endregion

    #region パブリックメソッド

    /// <summary>
    /// 音声ファイルのプレビューを開始。
    /// </summary>
    /// <param name="filePath">再生する音声ファイルパス。</param>
    /// <remarks>
    /// <para>【処理フロー】</para>
    /// <list type="number">
    /// <item>前回の再生をキャンセル</item>
    /// <item>300msのデバウンス待機</item>
    /// <item>キャンセルされていなければ読み込み開始</item>
    /// <item>UIスレッドで再生開始</item>
    /// <item>再生状態をイベントで通知</item>
    /// </list>
    /// 
    /// <para>【Why デバウンス】</para>
    /// ユーザーがリストを素早くスクロールしたり、連続でファイルを選択した場合、
    /// 最後に選択されたファイルのみを再生することで、無駄なリソース消費を抑えます。
    /// 
    /// <para>【エラーハンドリング】</para>
    /// ファイル読み込みエラーや再生エラーは、
    /// <see cref="PlaybackStateChanged"/>イベントで通知されます。
    /// </remarks>
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
    /// 現在の再生を停止。
    /// </summary>
    /// <remarks>
    /// <para>【処理内容】</para>
    /// <list type="bullet">
    /// <item>キャンセルトークンをキャンセル</item>
    /// <item>WaveOutを停止してDispose</item>
    /// <item>リソースを解放</item>
    /// </list>
    /// </remarks>
    public void StopCurrentPlayback()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _currentPlayer?.Stop();
        _currentPlayer?.Dispose();
        _currentPlayer = null;
    }

    /// <summary>
    /// リソースの解放。
    /// </summary>
    public void Dispose()
    {
        StopCurrentPlayback();
    }

    #endregion

    #region プライベートメソッド

    /// <summary>
    /// 状態変更を通知。
    /// </summary>
    /// <param name="fileName">ファイル名（任意）。</param>
    /// <param name="isLoading">読み込み中フラグ。</param>
    /// <param name="isPlaying">再生中フラグ。</param>
    /// <param name="errorMessage">エラーメッセージ（任意）。</param>
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

    #endregion
}
