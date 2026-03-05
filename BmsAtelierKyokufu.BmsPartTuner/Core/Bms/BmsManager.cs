using System.Text;
using System.Text.RegularExpressions;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms;

/// <summary>
/// BMSファイルの解析・操作を行うマネージャークラス。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>#WAV定義の解析と抽出</item>
/// <item>BMSコマンドの種別判定（ヘッダー/譜面/その他）</item>
/// <item>譜面データ内の定義番号置換</item>
/// <item>WAVチャンネルの識別（BGM、キー音、不可視、ロングノート、地雷）</item>
/// </list>
/// 
/// <para>【Why Shift_JIS】</para>
/// BMSフォーマットはShift_JISエンコーディングが標準です。
/// 互換性維持のため、ファイル読み書きにはShift_JISを使用します。
/// 
/// <para>【サポートするチャンネル】</para>
/// <list type="bullet">
/// <item>01: BGM</item>
/// <item>11-19, 21-29: キー音（1P/2P）</item>
/// <item>31-39, 41-49: 不可視オブジェ（1P/2P）</item>
/// <item>51-59, 61-69: ロングノート（1P/2P）</item>
/// <item>D1-D9, E1-E9: 地雷（1P/2P）</item>
/// </list>
/// </remarks>
internal partial class BmsManager
{
    /// <summary>
    /// BMSコマンドの種別。
    /// </summary>
    public enum BmsCommand
    {
        /// <summary>ヘッダー定義（#WAV, #BMP, #BPM, #STOP等）。</summary>
        HEADER,
        /// <summary>譜面データ（#xxxxx:形式）。</summary>
        MAIN,
        /// <summary>その他のコマンドまたはコメント。</summary>
        OTHER
    }

    private readonly string _bmsFilePath;
    private readonly string? _bmsDirectory;

    [GeneratedRegex(@"^#(WAV|BMP|BPM|STOP)[0-9A-Za-z]{2}")]
    private static partial Regex BmsHeaderRegex();
    [GeneratedRegex(@"^#\d{5}:")]
    private static partial Regex BmsMainCommandRegex();
    [GeneratedRegex(@"^(#)(\d{3})([0-9A-Fa-f]{2})(:)(.+)$")]
    private static partial Regex BmsChannelDataRegex();
    [GeneratedRegex(@"^#WAV([0-9A-Za-z]{2})\s+(.+)$")]
    private static partial Regex WavDefinitionRegex();

    /// <summary>
    /// BmsManagerを初期化します。
    /// </summary>
    /// <param name="bmsFilePath">BMSファイルのフルパス。</param>
    /// <exception cref="ArgumentNullException">bmsFilePathがnullの場合。</exception>
    public BmsManager(string bmsFilePath)
    {
        _bmsFilePath = bmsFilePath ?? throw new ArgumentNullException(nameof(bmsFilePath));
        _bmsDirectory = Path.GetDirectoryName(bmsFilePath);
    }

    /// <summary>
    /// BMSファイルが配置されているディレクトリパスを取得します。
    /// </summary>
    public string GetBmsDirectory() => _bmsDirectory ?? string.Empty;

    /// <summary>
    /// BMSファイルから#WAV定義を解析します。
    /// </summary>
    /// <returns>定義番号とファイルパスのタプルリスト。</returns>
    /// <remarks>
    /// <para>【処理内容】</para>
    /// <list type="number">
    /// <item>Shift_JISエンコーディングでファイルを読み込み</item>
    /// <item>正規表現で#WAVxx行を抽出</item>
    /// <item>定義番号とファイルパスをタプルで返す</item>
    /// </list>
    /// 
    /// <para>【エラーハンドリング】</para>
    /// <list type="bullet">
    /// <item>エンコーディングエラー: 例外を再スロー（UI側でハンドリング）</item>
    /// <item>パースエラー: ログに記録し、部分的な成功を許容</item>
    /// </list>
    /// 
    /// <para>【例】</para>
    /// <code>
    /// #WAV01 kick.wav
    /// #WAV02 snare.wav
    /// → [("01", "kick.wav"), ("02", "snare.wav")]
    /// </code>
    /// </remarks>
    public List<(string def, string path)> ParseWavDefinitions()
    {
        var definitions = new List<(string def, string path)>();

        if (!File.Exists(_bmsFilePath))
            return definitions;

        try
        {
            using var sr = new StreamReader(_bmsFilePath, Encoding.GetEncoding("shift_jis"));
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                var match = WavDefinitionRegex().Match(line);
                if (match.Success && match.Groups.Count >= 3)
                {
                    var def = match.Groups[1].Value;
                    var path = match.Groups[2].Value.Trim();
                    definitions.Add((def, path));
                }
            }
        }
        catch (ArgumentException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BmsManager] Encoding Error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BmsManager] Parse Error in file '{Path.GetFileName(_bmsFilePath)}': {ex.Message}");
        }

        return definitions;
    }

    /// <summary>
    /// 行のコマンドタイプを判定します。
    /// </summary>
    /// <param name="line">判定対象の行。</param>
    /// <returns>コマンドの種別（HEADER/MAIN/OTHER）。</returns>
    /// <remarks>
    /// <para>【判定ルール】</para>
    /// <list type="bullet">
    /// <item>HEADER: #WAV, #BMP, #BPM, #STOPで始まる行</item>
    /// <item>MAIN: #xxxxx:形式（5桁の数字 + コロン）</item>
    /// <item>OTHER: 上記以外（コメント、メタデータ等）</item>
    /// </list>
    /// </remarks>
    public static BmsCommand GetLineCommand(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return BmsCommand.OTHER;

        line = line.Trim();

        if (!line.StartsWith('#'))
            return BmsCommand.OTHER;

        if (BmsHeaderRegex().IsMatch(line))
            return BmsCommand.HEADER;

        if (BmsMainCommandRegex().IsMatch(line))
            return BmsCommand.MAIN;

        return BmsCommand.OTHER;
    }

    /// <summary>
    /// メインコマンド行の定義番号を置換します。
    /// </summary>
    /// <param name="line">置換対象の行。</param>
    /// <param name="replaceMap">置換マップ（元の定義番号 → 新しい定義番号）。</param>
    /// <returns>置換後の行。</returns>
    /// <remarks>
    /// <para>【処理内容】</para>
    /// <list type="number">
    /// <item>行を正規表現で解析（小節番号、チャンネル、データ部分を分離）</item>
    /// <item>WAVチャンネルかどうかを判定</item>
    /// <item>データ部分を2文字ずつ処理し、置換マップを適用</item>
    /// <item>"00"（休符）は置換しない</item>
    /// </list>
    /// 
    /// <para>【Why 2文字ずつ処理】</para>
    /// BMSの定義番号は2桁の36進数または62進数で表現されます（例: "01", "0Z", "zz"）。
    /// 
    /// <para>【例】</para>
    /// <code>
    /// 入力: "#00111:010203"
    /// replaceMap: {"01" → "0A", "02" → "0B"}
    /// 出力: "#00111:0A0B03"
    /// </code>
    /// </remarks>
    public static string ChangeDefinition(string line, Dictionary<string, string> replaceMap)
    {
        if (string.IsNullOrEmpty(line) || replaceMap.Count == 0)
            return line;

        var match = BmsChannelDataRegex().Match(line);
        if (!match.Success)
            return line;

        var prefix = match.Groups[1].Value + match.Groups[2].Value + match.Groups[3].Value + match.Groups[4].Value;
        var channel = match.Groups[3].Value;
        var data = match.Groups[5].Value;

        if (!IsWavChannel(channel))
        {
            return line;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < data.Length; i += 2)
        {
            if (i + 1 < data.Length)
            {
                var def = data.Substring(i, 2);
                if (def != "00" && replaceMap.TryGetValue(def, out string? replacement))
                {
                    sb.Append(replacement);
                }
                else
                {
                    sb.Append(def);
                }
            }
            else
            {
                sb.Append(data[i]);
            }
        }

        return prefix + sb.ToString();
    }

    /// <summary>
    /// チャンネル番号がWAV音声チャンネルかどうかを判定します。
    /// </summary>
    /// <param name="channelHex">16進数形式のチャンネル番号（2桁、例: "11", "1A"）</param>
    /// <returns>WAV音声チャンネルの場合true</returns>
    /// <remarks>
    /// <para>【WAV音声チャンネル】</para>
    /// <list type="bullet">
    /// <item>01: BGM</item>
    /// <item>11-19, 21-29, 31-39, 41-49, 51-59, 61-69: キー音</item>
    /// </list>
    /// 
    /// <para>【非WAVチャンネル】</para>
    /// <list type="bullet">
    /// <item>02: 小節長変更</item>
    /// <item>03, 08, 09: BPM/STOP定義を参照</item>
    /// </list>
    /// </remarks>
    internal static bool IsWavChannel(string channelHex)
    {
        if (channelHex == "01") return true;

        try
        {
            int ch = Convert.ToInt32(channelHex, 16);

            if ((ch >= 0x11 && ch <= 0x19) || (ch >= 0x21 && ch <= 0x29)) return true;
            if ((ch >= 0x31 && ch <= 0x39) || (ch >= 0x41 && ch <= 0x49)) return true;
            if ((ch >= 0x51 && ch <= 0x59) || (ch >= 0x61 && ch <= 0x69)) return true;
            if ((ch >= 0xD1 && ch <= 0xD9) || (ch >= 0xE1 && ch <= 0xE9)) return true;
        }
        catch
        {
            return false;
        }

        return false;
    }
}
