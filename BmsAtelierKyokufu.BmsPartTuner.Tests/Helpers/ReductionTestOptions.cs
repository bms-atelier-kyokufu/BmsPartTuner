using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;
using BmsAtelierKyokufu.BmsPartTuner.Models;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers
{
    /// <summary>
    /// 定義削減テストの動作設定を格納するオプションクラス。
    /// </summary>
    public class ReductionTestOptions
    {
        /// <summary>
        /// テスト用BMSファイルを構築するためのコールバック処理。
        /// </summary>
        public Action<BmsFileBuilder>? BuildBms { get; set; }

        /// <summary>
        /// テスト用のオーディオファイル定義リストを作成するためのコールバック処理。
        /// </summary>
        public Func<string, List<BmsAudioFile>>? CreateFiles { get; set; }

        /// <summary>
        /// 削減結果をアサート（検証）するためのコールバック処理。
        /// </summary>
        public Action<BmsOptimizationService.ReductionResult>? AssertResult { get; set; }

        /// <summary>
        /// テスト実行の直前に呼ばれるコールバック処理。
        /// </summary>
        public Action<string>? BeforeExecute { get; set; }

        /// <summary>
        /// テスト実行の直後に呼ばれるコールバック処理。
        /// </summary>
        public Action<string>? AfterExecute { get; set; }

        /// <summary>
        /// 波形比較のR2しきい値。
        /// </summary>
        public float? Threshold { get; set; }

        /// <summary>
        /// 削減対象の開始定義番号（整数値）。
        /// </summary>
        public int StartDef { get; set; } = 1;

        /// <summary>
        /// 削減対象の終了定義番号（整数値）。
        /// </summary>
        public int EndDef { get; set; } = 1;

        /// <summary>
        /// 物理ファイルの削除（ディスク削除）を行うかどうか。
        /// </summary>
        public bool PhysicalDeletion { get; set; }

        /// <summary>
        /// フィルタリング等に使用するキーワードのリスト。
        /// </summary>
        public IEnumerable<string>? Keywords { get; set; }

        /// <summary>
        /// 入力BMSファイルのファイル名。
        /// </summary>
        public string? InputBmsName { get; set; }

        /// <summary>
        /// 出力BMSファイルのファイル名。
        /// </summary>
        public string? OutputBmsName { get; set; }
    }
}
