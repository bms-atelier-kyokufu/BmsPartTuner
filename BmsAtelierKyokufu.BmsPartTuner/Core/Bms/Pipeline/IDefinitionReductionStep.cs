namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms.Pipeline;

/// <summary>
/// BMS定義削減パイプラインの各ステップが実装するインターフェース。
/// </summary>
internal interface IDefinitionReductionStep
{
    /// <summary>
    /// ステップの表示名（パフォーマンス計測・ログ用）。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// ステップの処理を実行します。
    /// </summary>
    /// <param name="context">パイプラインの実行コンテキスト</param>
    void Execute(DefinitionReductionContext context);
}
