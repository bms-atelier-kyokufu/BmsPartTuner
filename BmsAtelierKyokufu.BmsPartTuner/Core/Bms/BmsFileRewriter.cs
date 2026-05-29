using System.Text.RegularExpressions;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms;

/// <summary>
/// BMSファイルの書き換えと定義の整列を担当するクラスです。
/// 削減後の定義リストを抽出してファイル名順に整列し、新しいIDを割り当てた上で
/// BMSファイル内の#WAV定義と譜面データの置換を行います。
/// </summary>
[ADRAnchor("OPT-06", nameof(BmsFileRewriter))]
internal partial class BmsFileRewriter(
    IReadOnlyList<BmsAudioFile> fileList,
    int[] replaces,
    int startPoint,
    int endPoint,
    string? inputBmsContent = null)
{
    private readonly IReadOnlyList<BmsAudioFile> _fileList = fileList ?? throw new ArgumentNullException(nameof(fileList));
    private readonly int[] _replaces = replaces ?? throw new ArgumentNullException(nameof(replaces));
    private readonly int _startPoint = startPoint;
    private readonly int _endPoint = endPoint;
    private readonly string? _inputBmsContent = inputBmsContent;

    /// <summary>
    /// Shift_JISエンコーディングのプロバイダを登録する静的コンストラクタ。
    /// .NET 10では System.Text.Encoding.CodePages が必要。
    /// </summary>
    /// </summary>
    static BmsFileRewriter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [GeneratedRegex(@"^#(\d{3})([0-9A-Z]{2}):(.+)$")]
    private static partial Regex BmsChannelDataRegex();

    /// <summary>
    /// 削減後に保持されるファイルのリスト。
    /// ReplaceAndAlignBmsFile呼び出し後に設定されます。
    /// </summary>
    public List<BmsAudioFile> KeptFiles { get; private set; } = [];

    /// <summary>
    /// 削減後の定義ファイルをファイル名順に整列し、新しいIDを割り当ててBMSファイル内の定義とデータを置換します。
    /// </summary>
    /// <param name="bmsFileName">入力BMSファイルのパス。</param>
    /// <returns>書き換え後のBMS内容（文字列）。</returns>
    public string ReplaceAndAlignBmsFile(string bmsFileName)
    {
        var (reductionMap, filesToKeep) = BuildReductionMap();

        filesToKeep.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        // 保持ファイルをプロパティに保存（物理削除で使用）
        KeptFiles = filesToKeep;

        var finalMap = BuildFinalMap(filesToKeep, bmsFileName, out var newDefinitions);

        return RewriteBmsContent(bmsFileName, finalMap, newDefinitions);
    }

    /// <summary>
    /// 削減マップと保持ファイルリストを構築します。
    /// 各ファイルの元のIDと削減後のIDをマップ化し、重複排除を行いながら保持すべきファイルのリストを作成します。
    /// </summary>
    /// <returns>削減マップと保持ファイルリストのタプル。</returns>
    private (Dictionary<int, int> reductionMap, List<BmsAudioFile> filesToKeep) BuildReductionMap()
    {
        var reductionMap = new Dictionary<int, int>();
        var filesToKeep = new List<BmsAudioFile>();
        var keptIndices = new HashSet<int>();

        foreach (var file in _fileList)
        {
            int original = file.NumInteger;
            int reduced = original;

            // Within processing range, check replace table
            if (original >= _startPoint && original <= _endPoint)
            {
                // If replaceTable has a replacement (non-zero), use it
                // Otherwise, the file stands on its own (reduced = original)
                if (_replaces[original] > 0 && _replaces[original] != original)
                {
                    reduced = _replaces[original];
                }
            }

            reductionMap[original] = reduced;

            // Add to kept files if this is the first time we see this reduced index
            if (keptIndices.Add(reduced))
            {
                var repFile = _fileList.FirstOrDefault(f => f.NumInteger == reduced);
                if (repFile != null)
                {
                    filesToKeep.Add(repFile);
                }
            }
        }

        // プロパティに保存（外部参照用）
        KeptFiles = filesToKeep;

        return (reductionMap, filesToKeep);
    }

    /// <summary>
    /// 保持ファイル数に応じて36進数または62進数を自動判定し、最終的なIDマップを構築します。
    /// 新しい定義リストはBMSディレクトリからの相対パスで作成されます。
    /// </summary>
    /// <param name="filesToKeep">保持するファイルリスト。</param>
    /// <param name="bmsFileName">BMSファイルのパス。</param>
    /// <param name="newDefinitions">新しい定義リスト（出力）。</param>
    /// <returns>元のID → 新しいIDのマップ。</returns>
    private Dictionary<string, string> BuildFinalMap(
        List<BmsAudioFile> filesToKeep,
        string bmsFileName,
        out List<(string Index, string Path)> newDefinitions)
    {
        var finalMap = new Dictionary<string, string>();
        newDefinitions = [];

        int maxCount = filesToKeep.Count;
        int radix = (maxCount > AppConstants.Definition.MaxNumberBase36)
            ? AppConstants.Definition.RadixBase62
            : AppConstants.Definition.RadixBase36;

        string bmsDirectory = Path.GetDirectoryName(bmsFileName) ?? string.Empty;
        int counter = 1;
        var reducedToNewMap = new Dictionary<int, string>();

        foreach (var file in filesToKeep)
        {
            string newIdxStr = RadixConvert.IntToZZ(counter++, radix);
            if (newIdxStr.Length == 1) newIdxStr = "0" + newIdxStr;

            reducedToNewMap[file.NumInteger] = newIdxStr;

            string relativePath = Path.GetRelativePath(bmsDirectory, file.Name);
            newDefinitions.Add((newIdxStr, relativePath));
        }

        foreach (var file in _fileList)
        {
            int original = file.NumInteger;
            int reduced = original;

            if (original >= _startPoint && original <= _endPoint)
            {
                if (_replaces[original] > 0)
                {
                    reduced = _replaces[original];
                }
            }

            if (reducedToNewMap.TryGetValue(reduced, out string? newIdStr))
            {
                finalMap[file.Num] = newIdStr;
            }
        }

        return finalMap;
    }

    /// <summary>
    /// BMSファイルを行単位で読み込み、内容の書き換えを行います。
    /// 散在している#WAV定義を先頭に一括出力して整理し、譜面データ内のIDを置換マップに従って更新します。
    /// 定義リストに存在しないWAV ID参照は、データ非破壊の原則に基づき変更されずに維持されます。
    /// </summary>
    /// <param name="bmsFileName">入力BMSファイルのパス。</param>
    /// <param name="finalMap">IDマップ。</param>
    /// <param name="newDefinitions">新しい定義リスト。</param>
    /// <returns>書き換え後の内容。</returns>
    private string RewriteBmsContent(
        string bmsFileName,
        Dictionary<string, string> finalMap,
        List<(string Index, string Path)> newDefinitions)
    {
        var sb = new StringBuilder();
        bool definitionsWritten = false;
        var undefinedReferences = new HashSet<string>();

        if (_inputBmsContent == null && !File.Exists(bmsFileName))
            return sb.ToString();

        using TextReader sr = _inputBmsContent != null
            ? new StringReader(_inputBmsContent)
            : new StreamReader(bmsFileName, Encoding.GetEncoding("shift_jis"));

        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            var command = BmsManager.GetLineCommand(line);

            if (command == BmsManager.BmsCommand.HEADER)
            {
                if (IsWavDefinition(line))
                {
                    if (!definitionsWritten)
                    {
                        foreach (var def in newDefinitions)
                        {
                            sb.AppendLine($"{AppConstants.Definition.WavPrefix}{def.Index} {def.Path}");
                        }
                        definitionsWritten = true;
                    }
                    continue;
                }
                sb.AppendLine(line);
            }
            else if (command == BmsManager.BmsCommand.MAIN)
            {
                if (!definitionsWritten)
                {
                    foreach (var def in newDefinitions)
                    {
                        sb.AppendLine($"{AppConstants.Definition.WavPrefix}{def.Index} {def.Path}");
                    }
                    definitionsWritten = true;
                }

                if (finalMap.Count > 0)
                {
                    // 未定義参照を検出してログ出力
                    DetectUndefinedReferences(line, finalMap, undefinedReferences);
                    line = BmsManager.ChangeDefinition(line, finalMap);
                }
                sb.AppendLine(line);
            }
            else
            {
                sb.AppendLine(line);
            }
        }

        if (!definitionsWritten && newDefinitions.Count > 0)
        {
            foreach (var def in newDefinitions)
            {
                sb.AppendLine($"{AppConstants.Definition.WavPrefix}{def.Index} {def.Path}");
            }
        }

        // 未定義参照があればワーニングログを出力
        if (undefinedReferences.Count > 0)
        {
            PerformanceDebugLogger.WriteDebug(nameof(BmsFileRewriter), $"[BmsFileRewriter] WARNING: Found undefined WAV references in {Path.GetFileName(bmsFileName)}: {string.Join(", ", undefinedReferences)}");
            PerformanceDebugLogger.WriteDebug(nameof(BmsFileRewriter), "[BmsFileRewriter] These references were preserved as-is (non-destructive policy)");
        }
        return sb.ToString();
    }

    /// <summary>
    /// 行内の未定義WAV参照を検出します。
    /// </summary>
    /// <param name="line">検査対象の行</param>
    /// <param name="finalMap">定義マップ</param>
    /// <param name="undefinedReferences">未定義参照の集合（出力）</param>
    private static void DetectUndefinedReferences(string line, Dictionary<string, string> finalMap, HashSet<string> undefinedReferences)
    {
        // WAVチャンネルのデータ行を解析
        var match = BmsChannelDataRegex().Match(line);
        if (!match.Success)
            return;

        var channel = match.Groups[2].Value;
        var data = match.Groups[3].Value;

        // WAVチャンネルかどうか確認
        if (!BmsManager.IsWavChannel(channel))
            return;

        // 2文字ずつIDを抽出
        for (int i = 0; i < data.Length; i += 2)
        {
            if (i + 1 < data.Length)
            {
                var id = data.Substring(i, 2);
                if (id != AppConstants.Definition.Rest && !finalMap.ContainsKey(id))
                {
                    undefinedReferences.Add(id);
                }
            }
        }
    }

    /// <summary>
    /// 行が#WAV定義かどうかを判定します。
    /// </summary>
    /// <param name="line">検査対象の行。</param>
    /// <returns>#WAV定義の場合true。</returns>
    private static bool IsWavDefinition(string line)
    {
        return WavDefinitionRegex().IsMatch(line);
    }

    [GeneratedRegex(@"^#WAV[0-9A-Za-z]{2}", RegexOptions.IgnoreCase, "ja-JP")]
    private static partial Regex WavDefinitionRegex();

}
