using BmsAtelierKyokufu.BmsPartTuner.Services.Bms;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms;

/// <summary>
/// BMSファイルに関連付けられたオーディオファイルリストの管理および解析を行います。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="number">
/// <item>BMSファイルから#WAV定義を解析</item>
/// <item>参照されているオーディオファイルの存在確認</item>
/// <item>ファイルメタデータ（サイズ、定義番号）の取得</item>
/// <item>楽器名の統計的推定（<see cref="InstrumentNameDetectionService"/>連携）</item>
/// </list>
/// 
/// <para>【Why ObservableCollection】</para>
/// WPFのListBoxやDataGridにバインドされるため、コレクション変更を自動的にUIに反映させる必要があります。
/// </remarks>
/// <remarks>
/// BmsDefinitionManagerを初期化します。
/// </remarks>
/// <remarks>
/// <para>【Why InstrumentNameDetectionServiceを内部生成】</para>
/// BmsDefinitionManagerは単一のBMSファイルを管理するため、
/// 楽器検出サービスのライフサイクルもこれに合わせます。
/// </remarks>
public partial class BmsDefinitionManager(string bmsFilePath, string? bmsContent = null)
{
    [System.Text.RegularExpressions.GeneratedRegex("[a-z]")]
    private static partial System.Text.RegularExpressions.Regex LowerCaseRegex();
    private readonly string _bmsFilePath = bmsFilePath ?? throw new ArgumentNullException(nameof(bmsFilePath));
    private readonly string? _bmsContent = bmsContent;
    private readonly string _bmsDirectory = Path.GetDirectoryName(bmsFilePath) ?? string.Empty;
    private readonly ObservableCollection<BmsAudioFile> _fileList = [];
    private readonly InstrumentNameDetectionService _instrumentDetectionService = new();

    /// <summary>
    /// 見つからなかったファイルパスを記録するリスト。
    /// </summary>
    /// <remarks>
    /// <para>【用途】</para>
    /// ユーザーに欠落ファイルを通知し、BMSファイルの修正を促します。
    /// 例: "kick_01.wav が見つかりません"
    /// </remarks>
    public List<string> MissingFiles { get; private set; } = [];

    public string GetBmsDirectory() => _bmsDirectory;

    public ObservableCollection<BmsAudioFile> GetFileList() => _fileList;


    /// <summary>
    /// BMSファイルからファイルリストを作成します。
    /// </summary>
    /// <remarks>
    /// <para>【処理フロー】</para>
    /// <list type="number">
    /// <item>BMSファイルから#WAV定義を解析</item>
    /// <item>基数（36進 or 62進）を自動判定</item>
    /// <item>ファイル存在確認とメタデータ取得</item>
    /// <item>楽器名を統計的に推定</item>
    /// <item>ObservableCollectionに追加（UI反映）</item>
    /// </list>
    /// 
    /// <para>【Why 一時リストを使用】</para>
    /// <see cref="ObservableCollection{T}"/>への頻繁なAddは、毎回CollectionChangedイベントを
    /// 発火させUIを更新するため、パフォーマンスが低下します。
    /// 一時リストで処理してから一括追加することで、UI更新回数を削減します。
    /// 
    /// <para>【Why 基数を自動判定】</para>
    /// BMSフォーマットは36進数（0-9,A-Z）が標準ですが、
    /// 拡張仕様で62進数（0-9,A-Z,a-z）も使用されます。
    /// 定義に小文字が含まれていれば62進数と判定します。
    /// </remarks>
    public ObservableCollection<BmsAudioFile> CreateFileList()
    {
        PerformanceDebugLogger.WriteLine($"=== BmsDefinitionManager.CreateFileList Started for {Path.GetFileName(_bmsFilePath)} ===");
        var timerTotal = PerformanceDebugLogger.StartTimer();
        var timer = PerformanceDebugLogger.StartTimer();
        MissingFiles.Clear();

        var manager = new BmsManager(_bmsFilePath, _bmsContent);
        var definitions = manager.ParseWavDefinitions();
        PerformanceDebugLogger.WriteLine($"  [CreateFileList] ParseWavDefinitions (count={definitions.Count}): {timer.Lap("ParseWavDefinitions")} ms");

        bool isBase62 = definitions.Any(d => LowerCaseRegex().IsMatch(d.def));
        int inputRadix = isBase62 ? AppConstants.Definition.RadixBase62 : AppConstants.Definition.RadixBase36;

        var tempList = new List<BmsAudioFile>();

        foreach (var (def, path) in definitions)
        {
            var fullPath = Path.IsPathRooted(path)
                ? path
                : Path.Combine(_bmsDirectory, path);

            if (!File.Exists(fullPath) && !Core.Audio.VirtualAudioRegistry.TryGetFile(path, out _))
            {
                PerformanceDebugLogger.WriteLine($"[BmsDefinitionManager] Missing file: {path}");
                MissingFiles.Add(path);
                continue;
            }

            long fileSize = 0;
            if (Core.Audio.VirtualAudioRegistry.TryGetFile(path, out var memoryData))
            {
                fileSize = memoryData.Length;
            }
            else
            {
                var fileInfo = new FileInfo(fullPath);
                fileSize = fileInfo.Length;
            }

            tempList.Add(new BmsAudioFile
            {
                Num = def,
                NumInteger = RadixConvert.ZZToInt(def, inputRadix),
                Name = fullPath,
                FileSize = fileSize,
                AudioFingerprint = string.Empty,
                InstrumentName = string.Empty
            });
        }
        PerformanceDebugLogger.WriteLine($"  [CreateFileList] File resolution and existence checks: {timer.Lap("File resolution and existence checks")} ms");

        AssignInstrumentNames(tempList);
        PerformanceDebugLogger.WriteLine($"  [CreateFileList] AssignInstrumentNames: {timer.Lap("AssignInstrumentNames")} ms");

        foreach (var file in tempList)
        {
            _fileList.Add(file);
        }
        PerformanceDebugLogger.WriteLine($"  [CreateFileList] ObservableCollection.Add total: {timer.Lap("ObservableCollection.Add total")} ms");

        PerformanceDebugLogger.WriteLine($"=== BmsDefinitionManager.CreateFileList Finished: {timerTotal.Lap("Total")} ms ===");
        return _fileList;
    }

    /// <summary>
    /// ファイル名の統計分析に基づいて楽器名を推定・設定します。
    /// </summary>
    /// <remarks>
    /// <para>【Why try-catch】</para>
    /// 楽器名推定はオプショナルな機能であり、失敗してもファイルリスト作成は
    /// 継続すべきです。エラーが発生しても、InstrumentNameを空文字列のままにして処理を続行します。
    /// 
    /// <para>【処理タイミング】</para>
    /// ObservableCollectionに追加する前に実行することで、
    /// UIへの通知回数を削減（InstrumentName設定による追加通知を避ける）します。
    /// </remarks>
    private void AssignInstrumentNames(List<BmsAudioFile> files)
    {
        try
        {
            var detectionResult = _instrumentDetectionService.DetectInstruments(files);

            foreach (var file in files)
            {
                if (detectionResult.FileInstrumentMap.TryGetValue(file.Name, out string? instrumentName))
                {
                    file.InstrumentName = instrumentName;
                }
            }
        }
        catch (Exception ex)
        {
            PerformanceDebugLogger.WriteLine($"[BmsDefinitionManager.AssignInstrumentNames] ERROR: {ex.Message}");
        }
    }


}

