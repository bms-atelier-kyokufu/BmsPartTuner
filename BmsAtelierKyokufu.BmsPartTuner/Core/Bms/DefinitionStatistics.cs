using System.Diagnostics;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms;

/// <summary>
/// 定義削減の統計情報を管理するクラス。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>処理前後のファイル数を集計</item>
/// <item>削減率の計算</item>
/// <item>ユニークファイル数の取得</item>
/// <item>デバッグログへの統計情報出力</item>
/// </list>
/// 
/// <para>【用途】</para>
/// 自動最適化（<see cref="Core.Optimization.CorrelationThresholdOptimizer"/>）において、
/// エルボーポイント検出のための評価指標として使用されます。
/// </remarks>
internal class DefinitionStatistics
{
    private readonly IReadOnlyList<WavFiles> _fileList;
    private readonly int[] _replaces;
    private readonly int _startPoint;
    private readonly int _endPoint;

    /// <summary>
    /// DefinitionStatisticsを初期化します。
    /// </summary>
    /// <param name="fileList">ファイルリスト。</param>
    /// <param name="replaces">置換テーブル。</param>
    /// <param name="startPoint">処理範囲の開始定義番号。</param>
    /// <param name="endPoint">処理範囲の終了定義番号。</param>
    /// <exception cref="ArgumentNullException">fileListまたはreplacesがnullの場合。</exception>
    public DefinitionStatistics(
        IReadOnlyList<WavFiles> fileList,
        int[] replaces,
        int startPoint,
        int endPoint)
    {
        _fileList = fileList ?? throw new ArgumentNullException(nameof(fileList));
        _replaces = replaces ?? throw new ArgumentNullException(nameof(replaces));
        _startPoint = startPoint;
        _endPoint = endPoint;
    }

    /// <summary>
    /// 処理統計情報のログ出力。
    /// </summary>
    /// <remarks>
    /// <para>【出力項目】</para>
    /// <list type="bullet">
    /// <item>処理範囲（開始-終了）</item>
    /// <item>総定義数</item>
    /// <item>ユニークファイル数</item>
    /// <item>置換されたファイル数</item>
    /// <item>削減率（%）</item>
    /// </list>
    /// </remarks>
    public void LogStatistics()
    {
        var stats = CalculateStatistics();

        Debug.WriteLine($"=== Statistics ===");
        Debug.WriteLine($"Processing range: {_startPoint} - {_endPoint}");
        Debug.WriteLine($"Total definitions: {stats.TotalDefinitions}");
        Debug.WriteLine($"Unique files: {stats.UniqueFiles}");
        Debug.WriteLine($"Replaced: {stats.ReplacedFiles}");
        Debug.WriteLine($"Reduction rate: {stats.ReductionRate:F1}%");
    }

    /// <summary>
    /// 削減後のユニークファイル数を取得。
    /// </summary>
    /// <returns>ユニークファイル数。</returns>
    /// <remarks>
    /// <para>【計算方法】</para>
    /// 置換テーブルで自分自身を指している（置換されていない）ファイルの数を集計します。
    /// 
    /// <para>【用途】</para>
    /// 自動最適化のエルボーポイント検出において、
    /// 相関係数のしきい値を変化させた際のファイル数を評価します。
    /// 
    /// <para>【デバッグ情報】</para>
    /// 処理範囲内の総ファイル数、ユニークファイル数、未処理ファイル数を
    /// デバッグログに出力します。
    /// </remarks>
    public int GetUniqueFileCount()
    {
        var stats = CalculateStatistics();

        Debug.WriteLine($"=== GetUniqueFileCount Detail ===");
        Debug.WriteLine($"  Total in range: {stats.TotalInRange}");
        Debug.WriteLine($"  Unique (self-ref): {stats.UniqueFiles}");
        Debug.WriteLine($"  Not processed (==0): {stats.NotProcessed}");
        Debug.WriteLine($"  Processed (>0): {stats.Processed}");

        return stats.UniqueFiles;
    }

    #region プライベートメソッド

    /// <summary>
    /// 統計情報を計算。
    /// </summary>
    /// <returns>統計データ構造体。</returns>
    /// <remarks>
    /// <para>【集計項目】</para>
    /// <list type="bullet">
    /// <item>総定義数: 処理範囲内のファイル数</item>
    /// <item>置換されたファイル数: 別のファイルに置換されたファイル数</item>
    /// <item>ユニークファイル数: 自分自身を指している（残された）ファイル数</item>
    /// <item>削減率: 置換されたファイル数 / 総定義数 × 100</item>
    /// </list>
    /// 
    /// <para>【判定ロジック】</para>
    /// <list type="bullet">
    /// <item>_replaces[i] == i: 自分自身を指している（ユニーク）</item>
    /// <item>_replaces[i] > 0 かつ _replaces[i] != i: 別のファイルに置換された</item>
    /// <item>_replaces[i] == 0: 未処理（範囲外またはスキップ）</item>
    /// </list>
    /// </remarks>
    private StatisticsData CalculateStatistics()
    {
        int totalDefs = 0;
        int replaced = 0;
        int unique = 0;
        int totalInRange = 0;
        int notProcessed = 0;
        int processed = 0;

        foreach (var file in _fileList)
        {
            int fileNum = file.NumInteger;
            if (fileNum >= _startPoint && fileNum <= _endPoint)
            {
                totalDefs++;
                totalInRange++;

                if (_replaces[fileNum] == fileNum)
                {
                    unique++;
                    processed++;
                }
                else if (_replaces[fileNum] > 0 && _replaces[fileNum] != fileNum)
                {
                    replaced++;
                    processed++;
                }
                else
                {
                    notProcessed++;
                }
            }
        }

        double reductionRate = totalDefs > 0 ? (double)replaced / totalDefs * 100 : 0;

        return new StatisticsData
        {
            TotalDefinitions = totalDefs,
            ReplacedFiles = replaced,
            UniqueFiles = unique,
            TotalInRange = totalInRange,
            NotProcessed = notProcessed,
            Processed = processed,
            ReductionRate = reductionRate
        };
    }

    #endregion

    #region 内部データ構造

    /// <summary>
    /// 統計データを保持する構造体。
    /// </summary>
    private struct StatisticsData
    {
        /// <summary>総定義数（処理範囲内）。</summary>
        public int TotalDefinitions { get; init; }

        /// <summary>置換されたファイル数。</summary>
        public int ReplacedFiles { get; init; }

        /// <summary>ユニークファイル数（削減後に残るファイル数）。</summary>
        public int UniqueFiles { get; init; }

        /// <summary>処理範囲内の総ファイル数。</summary>
        public int TotalInRange { get; init; }

        /// <summary>未処理ファイル数（_replaces[i]==0）。</summary>
        public int NotProcessed { get; init; }

        /// <summary>処理済みファイル数（_replaces[i]>0）。</summary>
        public int Processed { get; init; }

        /// <summary>削減率（%）。</summary>
        public double ReductionRate { get; init; }
    }

    #endregion
}
