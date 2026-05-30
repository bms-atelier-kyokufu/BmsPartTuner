namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms;

/// <summary>
/// 定義削減の統計情報を管理するクラスです。
/// 処理前後のファイル数を集計し削減率を計算、ユニークファイル数を取得することで、
/// 自動最適化時のエルボーポイント検出などの評価指標として機能します。
/// </summary>
internal class DefinitionStatistics(
    IReadOnlyList<BmsAudioFile> fileList,
    int[] replaces,
    int startPoint,
    int endPoint)
{
    private static readonly Logger<DefinitionStatistics> s_logger = new();
    private readonly IReadOnlyList<BmsAudioFile> _fileList = fileList ?? throw new ArgumentNullException(nameof(fileList));
    private readonly int[] _replaces = replaces ?? throw new ArgumentNullException(nameof(replaces));
    private readonly int _startPoint = startPoint;
    private readonly int _endPoint = endPoint;

    /// <summary>
    /// 処理統計情報（処理範囲、総定義数、ユニークファイル数、置換されたファイル数、削減率）をログに出力します。
    /// </summary>
    public void LogStatistics()
    {
        var stats = CalculateStatistics();

        s_logger.WriteDebug( $$"""
            === Statistics ===
            Processing range: {{_startPoint}} - {{_endPoint}}
            Total definitions: {{stats.TotalDefinitions}}
            Unique files: {{stats.UniqueFiles}}
            Replaced: {{stats.ReplacedFiles}}
            Reduction rate: {{stats.ReductionRate:F1}}%
            """);
    }

    /// <summary>
    /// 削減後のユニークファイル数（置換テーブルで自分自身を指しているファイルの数）を取得します。
    /// この値は自動最適化のエルボーポイント検出において評価指標として利用されます。
    /// </summary>
    /// <returns>ユニークファイル数。</returns>
    public int GetUniqueFileCount()
    {
        var stats = CalculateStatistics();

        s_logger.WriteDebug( $$"""
            === GetUniqueFileCount Detail ===
              Total in range: {{stats.TotalInRange}}
              Unique (self-ref): {{stats.UniqueFiles}}
              Not processed (==0): {{stats.NotProcessed}}
              Processed (>0): {{stats.Processed}}
            """);

        return stats.UniqueFiles;
    }

    #region プライベートメソッド

    /// <summary>
    /// 処理範囲内の総定義数、置換されたファイル数、ユニークファイル数、未処理ファイル数、削減率などの統計情報を計算します。
    /// </summary>
    /// <returns>統計データ構造体。</returns>
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
    private readonly struct StatisticsData
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

