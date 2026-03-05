using BmsAtelierKyokufu.BmsPartTuner.Models;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

namespace BmsAtelierKyokufu.BmsPartTuner.Services
{
    /// <summary>
    /// BMS最適化サービスのインターフェース。
    /// </summary>
    /// <remarks>
    /// <para>【責務】</para>
    /// <list type="bullet">
    /// <item>しきい値最適化シミュレーション</item>
    /// <item>定義削減処理の実行</item>
    /// <item>入力値検証（<see cref="IInputValidationService"/>継承）</item>
    /// </list>
    /// </remarks>
    public interface IBmsOptimizationService : IInputValidationService
    {
        /// <summary>
        /// 最適なしきい値を見つけるため、100回のシミュレーションを実行します
        /// </summary>
        Task<OptimizationResult?> FindOptimalThresholdsAsync(
            List<string> files,
            int startDefinition,
            int endDefinition,
            IProgress<int>? progress = null);

        /// <summary>
        /// 定義削減処理を実行
        /// </summary>
        Task<BmsOptimizationService.ReductionResult> ExecuteDefinitionReductionAsync(
            IReadOnlyList<WavFiles> fileList,
            string inputPath,
            string outputPath,
            float r2Threshold,
            int startDefinition,
            int endDefinition,
            bool isPhysicalDeletionEnabled,
            IProgress<int>? progress = null,
        IEnumerable<string>? selectedKeywords = null);
    }
}
