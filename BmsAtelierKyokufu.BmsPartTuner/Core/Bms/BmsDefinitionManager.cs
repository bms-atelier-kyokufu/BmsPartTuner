using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms;

/// <summary>
/// BMSファイルに関連付けられたオーディオファイルリストの管理および解析を行います。
/// BMSファイルからの#WAV定義の解析、参照ファイルの存在確認とメタデータ取得、
/// および楽器名の統計的推定（<see cref="InstrumentNameDetectionService"/>）を統合して実行します。
/// ObservableCollectionを利用してUIへ変更を通知します。
/// </summary>
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
    /// 見つからなかったファイルパスを記録するリストです。
    /// ユーザーに欠落ファイルを通知し、BMSファイルの修正を促すために使用されます。
    /// </summary>
    public List<string> MissingFiles { get; private set; } = [];

    public string GetBmsDirectory() => _bmsDirectory;

    public ObservableCollection<BmsAudioFile> GetFileList() => _fileList;


    /// <summary>
    /// BMSファイルからファイルリストを作成します。
    /// #WAV定義の解析、基数の自動判定、ファイル存在確認、およびメタデータの取得を行います。
    /// その後、楽器名を推定してObservableCollectionに一括で追加し、UI更新頻度を最適化します。
    /// </summary>
    public ObservableCollection<BmsAudioFile> CreateFileList()
    {
        PerformanceDebugLogger.WriteDebug(nameof(BmsDefinitionManager), $"=== BmsDefinitionManager.CreateFileList Started for {Path.GetFileName(_bmsFilePath)} ===");
        var timerTotal = PerformanceDebugLogger.StartTimer();
        var timer = PerformanceDebugLogger.StartTimer();
        MissingFiles.Clear();

        var lines = new List<string>();
        if (_bmsContent != null)
        {
            using var sr = new StringReader(_bmsContent);
            string? line;
            while ((line = sr.ReadLine()) != null) lines.Add(line);
        }
        else if (File.Exists(_bmsFilePath))
        {
            try
            {
                using var sr = new StreamReader(_bmsFilePath, System.Text.Encoding.GetEncoding("shift_jis"));
                string? line;
                while ((line = sr.ReadLine()) != null) lines.Add(line);
            }
            catch (Exception ex)
            {
                PerformanceDebugLogger.WriteDebug(nameof(BmsDefinitionManager), $"Encoding/IO Error: {ex.Message}");
            }
        }

        var definitions = BmsManager.ParseWavDefinitions(lines);
        PerformanceDebugLogger.WriteDebug(nameof(BmsDefinitionManager), $"  [CreateFileList] ParseWavDefinitions (count={definitions.Count}): {timer.Lap("ParseWavDefinitions")} ms");

        bool isBase62 = definitions.Any(static d => LowerCaseRegex().IsMatch(d.def));
        int inputRadix = isBase62 ? AppConstants.Definition.RadixBase62 : AppConstants.Definition.RadixBase36;

        var tempList = new List<BmsAudioFile>();

        foreach (var (def, path) in definitions)
        {
            var fullPath = Path.IsPathRooted(path)
                ? path
                : Path.Combine(_bmsDirectory, path);

            if (!VirtualAudioRegistry.TryGetFileSize(path, out _) && !File.Exists(fullPath))
            {
                PerformanceDebugLogger.WriteDebug(nameof(BmsDefinitionManager), $"Missing file: {path}");
                MissingFiles.Add(path);
                continue;
            }

            long fileSize = 0;
            if (VirtualAudioRegistry.TryGetFileSize(path, out var memorySize))
            {
                fileSize = memorySize;
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
        PerformanceDebugLogger.WriteDebug(nameof(BmsDefinitionManager), $"File resolution and existence checks: {timer.Lap("File resolution and existence checks")} ms");

        AssignInstrumentNames(tempList);
        PerformanceDebugLogger.WriteDebug(nameof(BmsDefinitionManager), $"AssignInstrumentNames: {timer.Lap("AssignInstrumentNames")} ms");

        foreach (var file in tempList)
        {
            _fileList.Add(file);
        }
        PerformanceDebugLogger.WriteDebug(nameof(BmsDefinitionManager), $"ObservableCollection.Add total: {timer.Lap("ObservableCollection.Add total")} ms");

        PerformanceDebugLogger.WriteDebug(nameof(BmsDefinitionManager), $"=== BmsDefinitionManager.CreateFileList Finished: {timerTotal.Lap("Total")} ms ===");
        return _fileList;
    }

    /// <summary>
    /// ファイル名の統計分析に基づいて楽器名を推定・設定します。
    /// 処理に失敗した場合は例外を捕捉し、InstrumentNameが空のまま安全に続行されます。
    /// </summary>
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
            PerformanceDebugLogger.WriteDebug(nameof(BmsDefinitionManager), $"ERROR: {ex.Message}");
        }
    }


}

