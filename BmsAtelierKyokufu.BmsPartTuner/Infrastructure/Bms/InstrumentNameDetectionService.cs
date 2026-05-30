using System.Text.RegularExpressions;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms;

/// <summary>
/// BMS音声ファイル群のファイル名を統計的に分析し、楽器種別を自動的に推定するサービス。
/// これにより、ユーザーが楽器の種類ごとにフィルタリングを行う機能を提供します。
/// </summary>
public partial class InstrumentNameDetectionService(
    int minimumOccurrences = 3,
    int minimumWordLength = 3,
    int maximumWordLength = 20)
{
    private static readonly Logger<InstrumentNameDetectionService> s_logger = new();
    /// <summary>
    /// 楽器名検出の結果データ。
    /// </summary>
    public class InstrumentDetectionResult
    {
        /// <summary>検出された楽器候補とその出現回数。</summary>
        public Dictionary<string, int> InstrumentCandidates { get; init; } = [];

        /// <summary>ファイルごとの楽器名マッピング（キー: ファイルフルパス、値: 推定された楽器名）。</summary>
        public Dictionary<string, string> FileInstrumentMap { get; init; } = [];
    }

    /// <summary>
    /// UI表示用のフィルター楽器グループを表すデータモデル。
    /// ObservableObjectを継承し、チェック状態などをUIに自動反映します。
    /// </summary>
    public partial class InstrumentGroup : ObservableObject
    {
        [ObservableProperty]
        public partial bool IsSelected { get; set; }

        /// <summary>楽器名（例: "kick", "snare"）。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>この楽器に分類されたファイル数。</summary>
        public int Count { get; set; }
    }

    private readonly int _minimumOccurrences = minimumOccurrences;
    private readonly int _minimumWordLength = minimumWordLength;
    private readonly int _maximumWordLength = maximumWordLength;

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex IsDigitsOnlyRegex();
    [GeneratedRegex(@"^[a-zA-Z]+")]
    private static partial Regex ExtractAlphabetPrefixRegex();
    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9]*$")]
    private static partial Regex AlphanumericWordRegex();

    /// <summary>
    /// 提供されたファイルリスト全体から統計的解析を行い、楽器候補と各ファイルへのマッピングを推定します。
    /// </summary>
    /// <param name="files">対象の音声ファイルリスト。</param>
    /// <returns>検出された楽器候補と、各ファイルごとの楽器名マッピングを含む結果オブジェクト。</returns>
    public InstrumentDetectionResult DetectInstruments(IEnumerable<BmsAudioFile> files)
    {
        if (files == null)
        {
            return new InstrumentDetectionResult();
        }

        try
        {
            var fileList = files.ToList();
            var fileWordsMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            // 1. 各ファイルの単語を1回だけ抽出
            foreach (var file in fileList)
            {
                if (file?.Name == null) continue;
                var fileName = Path.GetFileNameWithoutExtension(file.Name);
                fileWordsMap[file.Name] = ExtractWordsFromFileName(fileName);
            }

            // 2. 楽器候補の抽出（事前抽出した単語リストを使用）
            var instrumentCandidates = ExtractInstrumentCandidates(fileWordsMap);
            var sortedCandidates = instrumentCandidates.OrderByDescending(kvp => kvp.Value).ToList();

            var fileInstrumentMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 3. マッチング処理
            foreach (var file in fileList)
            {
                if (file?.Name == null) continue;

                var fileName = Path.GetFileNameWithoutExtension(file.Name);
                var words = fileWordsMap[file.Name];

                var instrumentName = string.Empty;
                foreach (var candidate in sortedCandidates)
                {
                    if (words.Any(w => string.Equals(w, candidate.Key, StringComparison.OrdinalIgnoreCase)))
                    {
                        instrumentName = candidate.Key;
                        break;
                    }
                    if (fileName.Contains(candidate.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        instrumentName = candidate.Key;
                        break;
                    }
                }

                fileInstrumentMap[file.Name] = instrumentName;
            }

            return new InstrumentDetectionResult
            {
                InstrumentCandidates = instrumentCandidates,
                FileInstrumentMap = fileInstrumentMap
            };
        }
        catch (Exception ex)
        {
            s_logger.WriteDebug( $"ERROR: {ex.Message}");
            return new InstrumentDetectionResult();
        }
    }

    private static readonly SearchValues<char> WordSeparatorSearchValues = SearchValues.Create("_- .()[]{}");

    /// <summary>
    /// ファイル名から区切り文字や数字を除外し、楽器名の候補となる英数字単語を抽出します。
    /// </summary>
    /// <param name="fileName">対象のファイル名（拡張子なし）。</param>
    /// <returns>ファイル名から抽出された有効な単語のリスト。</returns>
    public List<string> ExtractWordsFromFileName(string fileName)
    {
        var words = new List<string>();

        if (string.IsNullOrWhiteSpace(fileName))
            return words;

        var span = fileName.AsSpan();

        while (span.Length > 0)
        {
            // 区切り文字をスキップ
            int nextNonSeparator = span.IndexOfAnyExcept(WordSeparatorSearchValues);
            if (nextNonSeparator < 0) break;

            span = span[nextNonSeparator..];

            // 次の区切り文字までの単語を取得
            int nextSeparator = span.IndexOfAny(WordSeparatorSearchValues);
            var partSpan = nextSeparator >= 0 ? span[..nextSeparator] : span;

            // スパンを進める
            span = nextSeparator >= 0 ? span[(nextSeparator + 1)..] : default;

            // 正規表現でチェック（ReadOnlySpan対応の IsMatch等を使用、今回は.ToString()で対応可能か確認）
            // IsDigitsOnlyRegex / ExtractAlphabetPrefixRegex は string用なので文字列化する
            // 将来的にはRegexをSpan対応に変更することで更なる最適化が可能
            var part = partSpan.ToString();

            if (IsDigitsOnlyRegex().IsMatch(part))
                continue;

            var alphabetPart = ExtractAlphabetPrefixRegex().Match(part).Value;
            if (!string.IsNullOrEmpty(alphabetPart) && alphabetPart.Length >= _minimumWordLength)
            {
                words.Add(alphabetPart);
            }

            if (part.Length >= _minimumWordLength &&
                part.Length <= _maximumWordLength &&
                AlphanumericWordRegex().IsMatch(part))
            {
                words.Add(part);
            }
        }

        return words;
    }

    /// <summary>
    /// 抽出された単語が、技術用語や汎用キーワードなどのノイズでない有効な楽器名候補であるかを判定します。
    /// </summary>
    /// <param name="word">チェック対象の単語。</param>
    /// <returns>有効な楽器名候補と判断された場合はtrue、それ以外はfalse。</returns>
    public bool IsValidInstrumentCandidate(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return false;

        if (word.Length < _minimumWordLength || word.Length > _maximumWordLength)
            return false;

        var excludeWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "wav", "mp3", "ogg", "flac", "aiff", "aif", "au", "raw", "pcm",
            "bit", "khz", "mono", "stereo", "left", "right", "mid", "side",
            "loop", "oneshot", "shot", "sample", "smp", "sfx", "fx",
            "edit", "mix", "master", "final", "ver", "version", "v01", "v02", "v03",
            "test", "temp", "tmp", "backup", "bak", "copy", "new", "old",
            "untitled", "noname", "default", "misc", "other", "unknown",
            "file", "audio", "sound", "track", "song", "music", "bms", "bme", "bml", "bmg"
        };

        return !excludeWords.Contains(word);
    }

    /// <summary>
    /// 抽出済みの楽器候補辞書と照らし合わせ、指定されたファイル名に最も適した楽器名（完全一致優先、次いで部分一致）を決定します。
    /// </summary>
    /// <param name="fileName">対象のファイル名（拡張子なし）。</param>
    /// <param name="instrumentCandidates">統計的に抽出された楽器候補と出現回数の辞書。</param>
    /// <returns>最適な楽器名。見つからない場合は空文字を返します。</returns>
    public string FindBestInstrumentMatch(string fileName, Dictionary<string, int> instrumentCandidates)
    {
        if (string.IsNullOrWhiteSpace(fileName) || instrumentCandidates == null || instrumentCandidates.Count == 0)
            return string.Empty;

        try
        {
            var words = ExtractWordsFromFileName(fileName);
            var sortedCandidates = instrumentCandidates.OrderByDescending(kvp => kvp.Value);

            foreach (var candidate in sortedCandidates)
            {
                if (words.Any(w => string.Equals(w, candidate.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidate.Key;
                }

                if (fileName.Contains(candidate.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate.Key;
                }
            }
        }
        catch (Exception ex)
        {
            s_logger.WriteDebug( $"ERROR: {ex.Message}");
        }

        return string.Empty;
    }

    /// <summary>
    /// ファイル名から抽出された全単語の中から、一定の出現回数（最小出現回数）を超える単語を楽器候補として抽出します。
    /// </summary>
    /// <param name="fileWordsMap">ファイル名とその抽出単語のリストの辞書。</param>
    /// <returns>楽器名候補と出現回数の辞書。</returns>
    private Dictionary<string, int> ExtractInstrumentCandidates(Dictionary<string, List<string>> fileWordsMap)
    {
        var candidates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (fileWordsMap == null || fileWordsMap.Count == 0)
            return candidates;

        try
        {
            foreach (var kvp in fileWordsMap)
            {
                var words = kvp.Value;

                foreach (var word in words)
                {
                    if (IsValidInstrumentCandidate(word))
                    {
                        if (!candidates.ContainsKey(word))
                            candidates[word] = 0;

                        candidates[word]++;
                    }
                }
            }

            return candidates
                .Where(kvp => kvp.Value >= _minimumOccurrences)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            s_logger.WriteDebug( $"ERROR: {ex.Message}");
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }
}

