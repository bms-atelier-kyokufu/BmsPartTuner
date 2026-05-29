using BmsAtelierKyokufu.BmsPartTuner.Core.Bms.Pipeline;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms;

/// <summary>
/// BMS定義の重複削減を統括するメインオーケストレータ（Facade）です。
/// パイプラインパターンを利用して、処理範囲の決定、音声データのプリロード、置換テーブルの作成、BMSファイルの書き換えなどの全体のフローを制御します。
/// </summary>
/// <remarks>
/// DefinitionReuseのインスタンスを作成します。
/// <para>引数:</para>
/// <list type="bullet">
/// <item><description><c>fileList</c>: 処理対象の音声ファイルリスト。</description></item>
/// <item><description><c>audioCache</c>: 初期の音声データキャッシュ。</description></item>
/// <item><description><c>inputBmsContent</c>: BMSファイル内容（省略時はパスから読み込み）。</description></item>
/// </list>
/// </remarks>
public class DefinitionReuse(ObservableCollection<BmsAudioFile> fileList, IReadOnlyDictionary<string, ICachedSoundData> audioCache, string? inputBmsContent = null)
{
    private readonly IReadOnlyList<BmsAudioFile> _fileList = fileList?.ToList() ?? throw new ArgumentNullException(nameof(fileList));
    private readonly IReadOnlyDictionary<string, ICachedSoundData> _initialAudioCache = audioCache ?? throw new ArgumentNullException(nameof(audioCache));
    private readonly string? _inputBmsContent = inputBmsContent;

    // パイプライン実行結果を保持して後続処理で利用するためのコンテキスト
    private DefinitionReductionContext? _context;

    /// <summary>
    /// BMS定義の重複削減処理を実行します。
    /// パイプラインパターンにより、各ステップを順次実行します。
    /// </summary>
    /// <param name="bmsFileName">BMSファイル名。</param>
    /// <param name="saveFileName">保存ファイル名。</param>
    /// <param name="options">定義削減オプション。</param>
    /// <param name="normalizationMode">正規化モード。</param>
    /// <exception cref="ArgumentNullException">optionsがnullの場合。</exception>
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

