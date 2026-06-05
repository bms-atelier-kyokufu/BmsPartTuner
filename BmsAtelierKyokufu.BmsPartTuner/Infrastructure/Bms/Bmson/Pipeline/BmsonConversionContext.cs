namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson.Pipeline;


/// <summary>
/// BMSON変換パイプラインの実行コンテキスト。
/// 入力値、中間生成オブジェクト、最終結果を保持し、使い終わったリソースの破棄（IDisposable）を管理します。
/// </summary>
/// <remarks>
/// コンテキストを初期化します。
/// </remarks>
[ADRAnchor("ARCH-01", nameof(BmsonConversionContext))]
public sealed class BmsonConversionContext(string bmsonFilePath, bool keyNotesOnly, IOperationContext? opContext = null) : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// 非同期処理キャンセルのためのトークン、進捗報告などを束ねたコンテキスト。
    /// </summary>
    public IOperationContext? OperationContext { get; } = opContext;

    /// <summary>
    /// 入力bmsonファイルのフルパス。
    /// </summary>
    public string BmsonFilePath { get; } = bmsonFilePath;

    /// <summary>
    /// trueの場合、BGMレーンを無視して演奏ノーツのみを抽出する。
    /// </summary>
    public bool KeyNotesOnly { get; } = keyNotesOnly;

    /// <summary>
    /// パースされたBMSONデータ。
    /// </summary>
    public BmsonFormat? Bmson { get; set; }

    /// <summary>
    /// パルス数からBMS小節時間への変換電卓。
    /// </summary>
    public PulseToBmsTimeCalculator? BmsTimeCalculator { get; set; }

    /// <summary>
    /// パルス数から実時間(秒)への変換電卓。
    /// </summary>
    public PulseToRealTimeCalculator? RealTimeCalculator { get; set; }

    /// <summary>
    /// 音声スライスマネージャ。
    /// </summary>
    public AudioSliceManager? AudioSlicer { get; set; }

    /// <summary>
    /// 生成されたBMSテキスト（出力）。
    /// </summary>
    public string? ResultBmsText { get; set; }

    /// <summary>
    /// IDisposableの実装。音声スライスマネージャなどのリソースを安全に解放します。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        AudioSlicer?.Dispose();
        _disposed = true;
    }
}