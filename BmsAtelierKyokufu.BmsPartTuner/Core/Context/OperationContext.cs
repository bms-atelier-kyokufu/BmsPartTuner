namespace BmsAtelierKyokufu.BmsPartTuner.Core.Context;

/// <summary>
/// 長時間実行される操作（非同期処理）のコンテキスト情報を提供するインターフェース。
/// CancellationTokenやIProgressなどを束ねるパラメータオブジェクトとして機能し、メソッドシグネチャの肥大化を防ぎます。
/// </summary>
public interface IOperationContext
{
    /// <summary>
    /// キャンセルトークン。
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// キャンセルが要求された場合に例外をスローします。
    /// </summary>
    void ThrowIfCancellationRequested();

    /// <summary>
    /// 進捗を報告します。
    /// </summary>
    /// <param name="percent">0から100までの進捗率。</param>
    void ReportProgress(int percent);
}

/// <summary>
/// <see cref="IOperationContext"/> の標準的な実装。
/// </summary>
/// <remarks>
/// OperationContext の新しいインスタンスを初期化します。
/// </remarks>
[ADRAnchor("ARCH-05", nameof(OperationContext))]
public sealed class OperationContext(CancellationToken cancellationToken = default, IProgress<int>? progress = null) : IOperationContext
{
    private readonly IProgress<int>? _progress = progress;

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public void ThrowIfCancellationRequested()
    {
        CancellationToken.ThrowIfCancellationRequested();
    }

    public void ReportProgress(int percent)
    {
        _progress?.Report(percent);
    }
}
