using BmsAtelierKyokufu.BmsPartTuner.Core.Bms.Pipeline;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms;

/// <summary>
/// BMS定義の重複削減を統括するメインオーケストレータ（Facade）です。
/// パイプラインパターンを利用して、処理範囲の決定、音声データのプリロード、置換テーブルの作成、BMSファイルの書き換えなどの全体のフローを制御します。
/// </summary>
public class DefinitionReuse
{
    private readonly IReadOnlyList<BmsAudioFile> _fileList;
    private readonly IReadOnlyDictionary<string, ICachedSoundData> _initialAudioCache;
    private readonly string? _inputBmsContent;

    // パイプライン実行結果を保持して後続処理で利用するためのコンテキスト
    private DefinitionReductionContext? _context;

    /// <summary>
    /// DefinitionReuseのインスタンスを作成します。
    /// </summary>
    /// <param name="fileList">処理対象の音声ファイルリスト。</param>
    /// <param name="audioCache">初期の音声データキャッシュ。</param>
    /// <param name="inputBmsContent">BMSファイル内容（省略時はパスから読み込み）。</param>
    /// <exception cref="ArgumentNullException">fileListまたはaudioCacheがnullの場合。</exception>
    public DefinitionReuse(ObservableCollection<BmsAudioFile> fileList, IReadOnlyDictionary<string, ICachedSoundData> audioCache, string? inputBmsContent = null)
    {
        _fileList = fileList?.ToList() ?? throw new ArgumentNullException(nameof(fileList));
        _initialAudioCache = audioCache ?? throw new ArgumentNullException(nameof(audioCache));
        _inputBmsContent = inputBmsContent;
    }

    /// <summary>
    /// BMS定義の重複削減処理を実行します。
    /// パイプラインパターンにより、各ステップを順次実行します。
    /// </summary>
    public void ReductDefinition(
        string bmsFileName,
        string saveFileName,
        DefinitionReductionOptions options,
        NormalizationMode normalizationMode = NormalizationMode.None)
    {
        ArgumentNullException.ThrowIfNull(options);

        // 実行コンテキストの初期化
        _context = new DefinitionReductionContext(
            bmsFileName,
            saveFileName,
            options,
            normalizationMode,
            _fileList,
            _initialAudioCache,
            _inputBmsContent);

        // 削減処理パイプラインの構築と実行
        var pipeline = new DefinitionReductionPipeline()
            .AddStep(new DetermineProcessingRangeStep())
            .AddStep(new PreloadAudioDataStep())
            .AddStep(new CreateReplaceTableStep())
            .AddStep(new RewriteBmsFileStep())
            .AddStep(new WriteAndFlushToDiskStep())
            .AddStep(new PhysicalDeletionStep())
            .AddStep(new LogStatisticsStep());

        pipeline.Execute(_context);
    }

    /// <summary>
    /// 削減後のユニークファイル数を取得します。自動最適化におけるエルボーポイント検出のための評価指標として使用されます。
    /// </summary>
    /// <returns>ユニークファイル数。</returns>
    public int GetUniqueFileCount()
    {
        return _context?.Statistics?.GetUniqueFileCount() ?? 0;
    }

    /// <summary>
    /// 削減対象となった（未使用の）ファイルパスのリストを取得します。
    /// </summary>
    /// <returns>未使用ファイルのパスリスト。</returns>
    public List<string> GetUnusedFilePaths()
    {
        if (_context?.Rewriter?.KeptFiles == null)
        {
            return [];
        }

        var keptFilePaths = new HashSet<string>(_context.Rewriter.KeptFiles.Select(static f => f.Name), StringComparer.OrdinalIgnoreCase);
        var unusedFiles = new List<string>();

        foreach (var file in _fileList)
        {
            if (!keptFilePaths.Contains(file.Name))
            {
                unusedFiles.Add(file.Name);
            }
        }

        return unusedFiles;
    }
}

