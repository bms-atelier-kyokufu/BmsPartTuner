using System.Threading.Tasks;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Optimization.Pipeline;

/// <summary>
/// 非同期最適化シミュレーションパイプラインの各ステップが実装するインターフェース。
/// </summary>
internal interface IAsyncOptimizationStep
{
    /// <summary>
    /// ステップの表示名（パフォーマンス計測・ログ用）。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// ステップの処理を非同期で実行します。
    /// </summary>
    /// <param name="context">パイプラインの実行コンテキスト</param>
    Task ExecuteAsync(OptimizationSimulationContext context);
}
