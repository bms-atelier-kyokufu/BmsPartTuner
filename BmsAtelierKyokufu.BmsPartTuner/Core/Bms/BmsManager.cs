using System.Text.RegularExpressions;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms;

/// <summary>
/// BMSファイルの解析・操作を行うマネージャークラスです。
/// #WAV定義の解析と抽出、BMSコマンドの種別判定、譜面データ内の定義番号置換、
/// およびBGM・キー音・不可視・ロングノート・地雷などのWAVチャンネルの識別を行います。
/// Shift_JISエンコーディングによる標準的なファイル読み書きをサポートします。
/// </summary>
/// <param name="bmsFilePath">BMSファイルのフルパス。</param>
/// <param name="bmsContent">BMSファイル内容（省略時はパスから読み込み）。</param>
/// <exception cref="ArgumentNullException">bmsFilePathがnullの場合。</exception>
internal partial class BmsManager(string bmsFilePath, string? bmsContent = null)
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

    private readonly string _bmsFilePath = bmsFilePath ?? throw new ArgumentNullException(nameof(bmsFilePath));
    private readonly string? _bmsContent = bmsContent;
    private readonly string? _bmsDirectory = Path.GetDirectoryName(bmsFilePath);

    [GeneratedRegex(@"^#(WAV|BMP|BPM|STOP)[0-9A-Za-z]{2}")]
    private static partial Regex BmsHeaderRegex();
    [GeneratedRegex(@"^#\d{5}:")]
    private static partial Regex BmsMainCommandRegex();
    [GeneratedRegex(@"^(#)(\d{3})([0-9A-Fa-f]{2})(:)(.+)$")]
    private static partial Regex BmsChannelDataRegex();
    [GeneratedRegex(@"^#WAV([0-9A-Za-z]{2})\s+(.+)$")]
    private static partial Regex WavDefinitionRegex();

    /// <summary>
    /// BMSファイルが配置されているディレクトリパスを取得します。
    /// </summary>
    public string GetBmsDirectory() => _bmsDirectory ?? string.Empty;

    /// <summary>
    /// BMSファイルから#WAV定義をShift_JISエンコーディングで解析し、抽出します。
    /// 解析エラーが発生した場合はログに記録し、部分的な抽出結果を返します。
    /// </summary>
    /// <returns>定義番号とファイルパスのタプルリスト。</returns>
    public List<(string def, string path)> ParseWavDefinitions()
    {
        var definitions = new List<(string def, string path)>();

        if (_bmsContent == null && !File.Exists(_bmsFilePath))
            return definitions;

        try
        {
            using TextReader sr = _bmsContent != null
                ? new StringReader(_bmsContent)
                : new StreamReader(_bmsFilePath, Encoding.GetEncoding("shift_jis"));
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
            PerformanceDebugLogger.WriteDebug(nameof(BmsManager), $"Encoding Error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            PerformanceDebugLogger.WriteDebug(nameof(BmsManager), $"[BmsManager] Parse Error in file '{Path.GetFileName(_bmsFilePath)}': {ex.Message}");
        }

        return definitions;
    }

    /// <summary>
    /// 行のコマンドタイプ（HEADER: #WAV/#BMPなど, MAIN: #xxxxx:, OTHER: その他）を判定します。
    /// </summary>
    /// <param name="line">判定対象の行。</param>
    /// <returns>コマンドの種別（HEADER/MAIN/OTHER）。</returns>
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
    /// メインコマンド行のWAVチャンネルを判定し、データ部分を2文字ずつ区切って置換マップを適用します。
    /// "00"（休符）は置換の対象外となります。
    /// </summary>
    /// <param name="line">置換対象の行。</param>
    /// <param name="replaceMap">置換マップ（元の定義番号 → 新しい定義番号）。</param>
    /// <returns>置換後の行。</returns>
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
                if (def != AppConstants.Definition.Rest && replaceMap.TryGetValue(def, out string? replacement))
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
    /// チャンネル番号がWAV音声チャンネル（BGM、キー音など）かどうかを判定します。
    /// 小節長変更（02）やBPM/STOP定義（03, 08, 09）などはfalseを返します。
    /// </summary>
    /// <param name="channelHex">16進数形式のチャンネル番号（2桁、例: "11", "1A"）</param>
    /// <returns>WAV音声チャンネルの場合true</returns>
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
