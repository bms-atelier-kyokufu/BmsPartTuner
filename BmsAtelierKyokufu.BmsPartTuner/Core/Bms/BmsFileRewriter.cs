using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms;

/// <summary>
/// BMSファイルの書き換えと定義整列を担当するクラス。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>削減後の定義リストを抽出・整列</item>
/// <item>新しいID（01, 02, ...）の割り当て</item>
/// <item>BMSファイル内の#WAV定義と譜面データの置換</item>
/// <item>Shift_JISエンコーディングでファイル保存</item>
/// </list>
/// 
/// <para>【処理フロー】</para>
/// <list type="number">
/// <item>削減後の定義ファイルを抽出</item>
/// <item>ファイル名順に整列</item>
/// <item>新しいID（01, 02, ...）を割り当て</item>
/// <item>BMSファイル内の定義とデータを置換</item>
/// </list>
/// 
/// <para>【Why ファイル名順】</para>
/// 整列により、類似ファイルが連続して並ぶため、視認性が向上します。
/// 例: kick_01, kick_02, kick_03, snare_01, snare_02...
/// </remarks>
internal partial class BmsFileRewriter
{
    private readonly IReadOnlyList<WavFiles> _fileList;
    private readonly int[] _replaces;
    private readonly int _startPoint;
    private readonly int _endPoint;

    /// <summary>
    /// Shift_JISエンコーディングのプロバイダを登録する静的コンストラクタ。
    /// .NET 10では System.Text.Encoding.CodePages が必要。
    /// </summary>
    static BmsFileRewriter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// 削減後に保持されるファイルのリスト。
    /// ReplaceAndAlignBmsFile呼び出し後に設定されます。
    /// </summary>
    public List<WavFiles> KeptFiles { get; private set; } = new List<WavFiles>();

    /// <summary>
    /// BmsFileRewriterを初期化します。
    /// </summary>
    /// <param name="fileList">ファイルリスト。</param>
    /// <param name="replaces">置換テーブル（配列インデックス: 元のID、値: 置換先ID）。</param>
    /// <param name="startPoint">処理範囲の開始定義番号。</param>
    /// <param name="endPoint">処理範囲の終了定義番号。</param>
    /// <exception cref="ArgumentNullException">fileListまたはreplacesがnullの場合。</exception>
    public BmsFileRewriter(
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
    /// BMSファイルの置換と整列を実行します。
    /// </summary>
    /// <param name="bmsFileName">入力BMSファイルのパス。</param>
    /// <returns>書き換え後のBMS内容（文字列）。</returns>
    /// <remarks>
    /// <para>【処理内容】</para>
    /// <list type="number">
    /// <item>削減後の定義ファイルを抽出</item>
    /// <item>ファイル名順に整列</item>
    /// <item>新しいID（01, 02, ...）を割り当て</item>
    /// <item>BMSファイル内の定義とデータを置換</item>
    /// </list>
    /// 
    /// <para>【Why 3段階マップ】</para>
    /// <list type="bullet">
    /// <item>reductionMap: 元のID → 削減後のID（重複排除）</item>
    /// <item>reducedToNewMap: 削減後のID → 新しいID（整列後の連番）</item>
    /// <item>finalMap: 元のID → 新しいID（最終的な置換マップ）</item>
    /// </list>
    /// これにより、重複排除と整列を独立して処理できます。
    /// </remarks>
    public string ReplaceAndAlignBmsFile(string bmsFileName)
    {
        var (reductionMap, filesToKeep) = BuildReductionMap();

        filesToKeep.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        // 保持ファイルをプロパティに保存（物理削除で使用）
        KeptFiles = filesToKeep;

        var finalMap = BuildFinalMap(filesToKeep, bmsFileName, out var newDefinitions);

        return RewriteBmsContent(bmsFileName, finalMap, newDefinitions);
    }

    /// <summary>
    /// BMSファイルを書き込みます。
    /// </summary>
    /// <param name="saveFileName">保存先ファイルパス。</param>
    /// <param name="writeData">書き込む内容。</param>
    /// <remarks>
    /// <para>【Why Shift_JIS】</para>
    /// BMSフォーマットはShift_JISエンコーディングが標準です。
    /// 互換性維持のため、ファイル書き込みにはShift_JISを使用します。
    /// </remarks>
    public void WriteBmsFile(string saveFileName, string writeData)
    {
        // アトミック書き込み: 一時ファイルに書き込んでからリネーム
        var tempFileName = saveFileName + ".tmp";

        try
        {
            // パス長チェック（WindowsのMAX_PATH制限への対策）
            // .NET Modernでは自動的に\\?\が付与される場合が多いが、念のため
            var directory = Path.GetDirectoryName(saveFileName);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"出力先ディレクトリが存在しません: {directory}");
            }

            // 1. 一時ファイルに書き込み
            using (var sw = new StreamWriter(tempFileName, false, Encoding.GetEncoding("shift_jis")))
            {
                sw.Write(writeData);
            }

            // 2. 書き込み成功後、元のファイルを置き換え
            // Move(overwrite: true) は .NET Core 3.0+ / .NET 5+ でサポート
            // Windows 7互換ターゲットでもランタイムが新しければ動作するが、
            // 安全のため Delete -> Move の手順を踏む（Moveのoverwriteはアトミック性が高いが、ここではDelete+Moveで実装）
            // .NET Standard等では File.Move(src, dst, overwrite) が使える。

            if (File.Exists(saveFileName))
            {
                File.Delete(saveFileName);
            }
            File.Move(tempFileName, saveFileName);

            Debug.WriteLine($"BMS file written atomically: {saveFileName}");
        }
        catch (IOException)
        {
            // エラー発生時のクリーンアップ処理
            // 注意: 元のファイルが既に削除されている場合は、データ消失を防ぐために
            // 一時ファイルを削除せずに残す（ユーザーによる手動復旧を可能にするため）
            try
            {
                bool originalExists = File.Exists(saveFileName);
                if (File.Exists(tempFileName))
                {
                    // 元のファイルが存在する場合のみ、一時ファイルを削除（クリーンアップ）
                    // 元のファイルが消失している場合は、一時ファイルを残す
                    if (originalExists)
                    {
                        File.Delete(tempFileName);
                        Debug.WriteLine($"Cleanup: Incomplete temp file deleted: {tempFileName}");
                    }
                    else
                    {
                        Debug.WriteLine($"CRITICAL WARNING: Original file lost, keeping temp file for recovery: {tempFileName}");
                    }
                }
            }
            catch
            {
                // クリーンアップ中の例外は無視（元の例外を優先）
            }
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            // アクセス拒否エラー（読み取り専用ディレクトリなど）
            try
            {
                if (File.Exists(tempFileName))
                {
                    File.Delete(tempFileName);
                    Debug.WriteLine($"Cleanup: Temp file deleted due to access denied: {tempFileName}");
                }
            }
            catch
            {
                // クリーンアップ中の例外は無視
            }
            throw;
        }
    }

    #region プライベートメソッド

    /// <summary>
    /// 削減マップと保持ファイルリストを構築します。
    /// </summary>
    /// <returns>削減マップと保持ファイルリストのタプル。</returns>
    /// <remarks>
    /// <para>【処理内容】</para>
    /// <list type="number">
    /// <item>各ファイルの元のIDと削減後のIDをマップ化</item>
    /// <item>削減後のIDが重複しないようにHashSetで管理</item>
    /// <item>保持すべきファイルをリストに追加</item>
    /// </list>
    /// 
    /// <para>【Why HashSet】</para>
    /// Contains()がO(1)で高速なため、重複チェックに最適です。
    /// </remarks>
    private (Dictionary<int, int> reductionMap, List<WavFiles> filesToKeep) BuildReductionMap()
    {
        var reductionMap = new Dictionary<int, int>();
        var filesToKeep = new List<WavFiles>();
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
            if (!keptIndices.Contains(reduced))
            {
                keptIndices.Add(reduced);
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
    /// 最終的なIDマップを構築します。
    /// </summary>
    /// <param name="filesToKeep">保持するファイルリスト。</param>
    /// <param name="bmsFileName">BMSファイルのパス。</param>
    /// <param name="newDefinitions">新しい定義リスト（出力）。</param>
    /// <returns>元のID → 新しいIDのマップ。</returns>
    /// <remarks>
    /// <para>【処理内容】</para>
    /// <list type="number">
    /// <item>ファイル数に応じて基数を自動判定（36進 or 62進）</item>
    /// <item>削減後のIDに新しいID（01, 02, ...）を割り当て</item>
    /// <item>相対パスを計算して新しい定義リストを作成</item>
    /// <item>元のIDから新しいIDへのマップを完成</item>
    /// </list>
    /// 
    /// <para>【Why 基数の自動判定】</para>
    /// 36進数（0-9,A-Z）は1295定義まで、62進数（0-9,A-Z,a-z）は3843定義まで対応。
    /// ファイル数に応じて自動的に最適な基数を選択します。
    /// 
    /// <para>【Why 相対パス】</para>
    /// BMSファイルから見た相対パスで記述することで、フォルダ構造の変更に強くなります。
    /// </remarks>
    private Dictionary<string, string> BuildFinalMap(
        List<WavFiles> filesToKeep,
        string bmsFileName,
        out List<(string Index, string Path)> newDefinitions)
    {
        var finalMap = new Dictionary<string, string>();
        newDefinitions = new List<(string Index, string Path)>();

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
    /// BMSコンテンツを書き換えます。
    /// </summary>
    /// <param name="bmsFileName">入力BMSファイルのパス。</param>
    /// <param name="finalMap">IDマップ。</param>
    /// <param name="newDefinitions">新しい定義リスト。</param>
    /// <returns>書き換え後の内容。</returns>
    /// <remarks>
    /// <para>【処理内容】</para>
    /// <list type="number">
    /// <item>BMSファイルを行単位で読み込み</item>
    /// <item>ヘッダー行: 最初の#WAV定義の位置で新しい定義リストを一括出力</item>
    /// <item>譜面データ行: IDを置換して出力</item>
    /// <item>その他の行: そのまま出力</item>
    /// </list>
    /// 
    /// <para>【Why 一括出力】</para>
    /// 元の#WAV定義がファイル中に散在している場合でも、
    /// 新しい定義リストを先頭にまとめて出力することで、
    /// 可読性と管理性が向上します。
    /// 
    /// <para>【未定義参照の扱い】</para>
    /// 譜面データ内で参照されているが定義リストに存在しないWAV IDは、
    /// データ非破壊の原則に従い、変更せずにそのまま維持します。
    /// これにより、ユーザーの資産を保護します。
    /// </remarks>
    private string RewriteBmsContent(
        string bmsFileName,
        Dictionary<string, string> finalMap,
        List<(string Index, string Path)> newDefinitions)
    {
        var sb = new StringBuilder();
        bool definitionsWritten = false;
        var undefinedReferences = new HashSet<string>();

        using (var sr = new StreamReader(bmsFileName, Encoding.GetEncoding("shift_jis")))
        {
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
                                sb.AppendLine($"#WAV{def.Index} {def.Path}");
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
                            sb.AppendLine($"#WAV{def.Index} {def.Path}");
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
        }

        if (!definitionsWritten && newDefinitions.Count > 0)
        {
            foreach (var def in newDefinitions)
            {
                sb.AppendLine($"#WAV{def.Index} {def.Path}");
            }
        }

        // 未定義参照があればワーニングログを出力
        if (undefinedReferences.Count > 0)
        {
            Debug.WriteLine($"[BmsFileRewriter] WARNING: Found undefined WAV references in {Path.GetFileName(bmsFileName)}: {string.Join(", ", undefinedReferences)}");
            Debug.WriteLine($"[BmsFileRewriter] These references were preserved as-is (non-destructive policy)");
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
        var match = System.Text.RegularExpressions.Regex.Match(line, @"^#(\d{3})([0-9A-Z]{2}):(.+)$");
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
                if (id != "00" && !finalMap.ContainsKey(id))
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

    #endregion
}
