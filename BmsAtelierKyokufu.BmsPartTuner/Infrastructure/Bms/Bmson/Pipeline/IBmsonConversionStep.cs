namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson.Pipeline;

/// <summary>
/// BMSON変換パイプラインの各処理ステップが実装するインターフェース。
/// </summary>
public interface IBmsonConversionStep
{
    /// <summary>
    /// ステップの表示名（デバッグログ・パフォーマンス計測用）。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// ステップの処理を実行します。
    /// </summary>
    /// <param name="context">パイプラインの実行コンテキスト</param>
    void Execute(BmsonConversionContext context);
}
