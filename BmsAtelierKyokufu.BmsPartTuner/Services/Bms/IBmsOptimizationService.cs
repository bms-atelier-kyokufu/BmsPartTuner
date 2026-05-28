using BmsAtelierKyokufu.BmsPartTuner.Services.Common;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.Bms
{
    /// <summary>
    /// BMSファイルの定義最適化やしきい値シミュレーションなどの機能を提供するサービスのインターフェース。
    /// 入力値の検証機能も提供します。
    /// </summary>
    public interface IBmsOptimizationService : IInputValidationService
    {
        /// <summary>
        /// 最適なしきい値を見つけるため、指定された範囲でシミュレーションを実行します。
        /// </summary>
        Task<OptimizationResult?> FindOptimalThresholdsAsync(
            List<string> files,
            int startDefinition,
            int endDefinition,
            IProgress<int>? progress = null);

        /// <summary>
        /// 提供された音声ファイルリストとオプションに基づき、BMSの定義削減処理を実行します。
        /// </summary>
        Task<BmsOptimizationService.ReductionResult> ExecuteDefinitionReductionAsync(
            IReadOnlyList<BmsAudioFile> fileList,
            string inputPath,
            string outputPath,
            DefinitionReductionOptions options);
    }
}
