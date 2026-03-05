using System.Diagnostics;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

namespace BmsAtelierKyokufu.BmsPartTuner.Services;

/// <summary>
/// ファイル名の統計分析に基づき、音声ファイル群から楽器種別を推定するサービス。
/// </summary>
/// <remarks>
/// <para>【目的】</para>
/// 同じ楽器の音声ファイルをグループ化し、ユーザーが視覚的に管理しやすいフィルター機能を提供します。
/// 
/// <para>【アルゴリズム】</para>
/// <list type="number">
/// <item>ファイル名を区切り文字で分割</item>
/// <item>英字部分を抽出（数値やバージョン番号を除外）</item>
/// <item>複数ファイルに共通して出現する単語を楽器候補として採用</item>
/// <item>出現頻度の高い順に各ファイルへマッチング</item>
/// </list>
/// 
/// <para>【例】</para>
/// <code>
/// kick_01.wav, kick_02.wav, snare_01.wav
/// → 楽器候補: "kick" (2回), "snare" (1回)
/// → kick_01.wav → "kick", kick_02.wav → "kick", snare_01.wav → "snare"
/// </code>
/// </remarks>
public partial class InstrumentNameDetectionService
{
    /// <summary>
    /// 楽器名検出の結果データ。
    /// </summary>
    public class InstrumentDetectionResult
    {
        /// <summary>検出された楽器候補とその出現回数。</summary>
        public Dictionary<string, int> InstrumentCandidates { get; init; } = new();

        /// <summary>ファイルごとの楽器名マッピング（キー: ファイルフルパス、値: 推定された楽器名）。</summary>
        public Dictionary<string, string> FileInstrumentMap { get; init; } = new();
    }

    /// <summary>
    /// フィルター用の楽器グループ（UI表示用）。
    /// </summary>
    /// <remarks>
    /// <para>【用途】</para>
    /// ViewModelで使用し、チェックボックス付きの楽器フィルターUIを実現します。
    /// <see cref="ObservableObject"/>を継承することで、IsSelectedの変更が自動的にUIに反映されます。
    /// </remarks>
    public partial class InstrumentGroup : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        /// <summary>楽器名（例: "kick", "snare"）。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>この楽器に分類されたファイル数。</summary>
        public int Count { get; set; }
    }

    private readonly int _minimumOccurrences;
    private readonly int _minimumWordLength;
    private readonly int _maximumWordLength;

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex IsDigitsOnlyRegex();
    [GeneratedRegex(@"^[a-zA-Z]+")]
    private static partial Regex ExtractAlphabetPrefixRegex();
    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9]*$")]
    private static partial Regex AlphanumericWordRegex();

    /// <summary>
    /// 楽器名検出サービスを初期化します。
    /// </summary>
    /// <param name="minimumOccurrences">楽器名として認定する最小出現回数（デフォルト: 3）。</param>
    /// <param name="minimumWordLength">単語の最小長（デフォルト: 3）。</param>
    /// <param name="maximumWordLength">単語の最大長（デフォルト: 20）。</param>
    /// <remarks>
    /// <para>【パラメータ設定理由】</para>
    /// <list type="bullet">
    /// <item><c>minimumOccurrences=3</c>: 1-2回の出現は偶然の可能性が高く、ノイズを避けるため</item>
    /// <item><c>minimumWordLength=3</c>: "ab"等の短すぎる単語は楽器名として意味を持たない</item>
    /// <item><c>maximumWordLength=20</c>: 異常に長い文字列を除外してメモリを節約</item>
    /// </list>
    /// </remarks>
    public InstrumentNameDetectionService(
        int minimumOccurrences = 3,
        int minimumWordLength = 3,
        int maximumWordLength = 20)
    {
        _minimumOccurrences = minimumOccurrences;
        _minimumWordLength = minimumWordLength;
        _maximumWordLength = maximumWordLength;
    }

    /// <summary>
    /// ファイルリストから楽器名を統計的に検出・推定します。
    /// </summary>
    /// <param name="files">音声ファイルリスト。</param>
    /// <returns>検出結果（楽器候補と各ファイルのマッピング）。</returns>
    /// <remarks>
    /// <para>【処理フロー】</para>
    /// <list type="number">
    /// <item>ファイル名から単語候補を抽出</item>
    /// <item>出現頻度が閾値以上の単語を楽器候補として採用</item>
    /// <item>各ファイルに最適な楽器名を割り当て</item>
    /// </list>
    /// 
    /// <para>【スレッドセーフ】</para>
    /// <see cref="IEnumerable{T}"/>をToList()で即座にコピーすることで、
    /// 元のコレクションが別スレッドで変更されても影響を受けません。
    /// </remarks>
    public InstrumentDetectionResult DetectInstruments(IEnumerable<WavFiles> files)
    {
        if (files == null)
        {
            return new InstrumentDetectionResult();
        }

        try
        {
            var fileList = files.ToList();
            var instrumentCandidates = ExtractInstrumentCandidates(fileList);
            var fileInstrumentMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in fileList)
            {
                if (file?.Name == null)
                    continue;

                var fileName = Path.GetFileNameWithoutExtension(file.Name);
                var instrumentName = FindBestInstrumentMatch(fileName, instrumentCandidates);
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
            Debug.WriteLine($"[InstrumentNameDetectionService.DetectInstruments] ERROR: {ex.Message}");
            return new InstrumentDetectionResult();
        }
    }

    /// <summary>
    /// ファイル名から単語を抽出します。
    /// </summary>
    /// <param name="fileName">ファイル名（拡張子なし）。</param>
    /// <returns>抽出された単語リスト。</returns>
    /// <remarks>
    /// <para>【抽出戦略】</para>
    /// <list type="number">
    /// <item>区切り文字で分割: "kick_01_final.wav" → ["kick", "01", "final"]</item>
    /// <item>数値のみを除外: "01" → スキップ</item>
    /// <item>英字プレフィックスを抽出: "kick01" → "kick"</item>
    /// <item>有効な英数字単語を採用: "kick" → 採用</item>
    /// </list>
    /// 
    /// <para>【Why 区切り文字を使用】</para>
    /// BMSの命名規則では、'_'や'-'が楽器種別と番号を区切るため。
    /// 例: "bd_01.wav", "snare-soft.wav"
    /// </remarks>
    public List<string> ExtractWordsFromFileName(string fileName)
    {
        var words = new List<string>();

        if (string.IsNullOrWhiteSpace(fileName))
            return words;

        char[] separators = ['_', '-', ' ', '.', '(', ')', '[', ']', '{', '}'];
        var parts = fileName.Split(separators, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
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
    /// 単語が楽器候補として有効かチェックします。
    /// </summary>
    /// <param name="word">チェック対象の単語。</param>
    /// <returns>有効な場合true。</returns>
    /// <remarks>
    /// <para>【除外理由】</para>
    /// <list type="bullet">
    /// <item>ファイル形式名（"wav", "mp3"）: 楽器ではなく技術情報</item>
    /// <item>音響パラメータ（"khz", "bit"）: 楽器ではなく仕様</item>
    /// <item>汎用ワード（"sample", "loop"）: 意味が広すぎて識別に寄与しない</item>
    /// <item>バージョン管理（"v01", "final"）: 楽器ではなく管理情報</item>
    /// </list>
    /// 
    /// <para>【Why HashSetを使用】</para>
    /// Contains()がO(1)で高速。大量のファイル処理で効果的。
    /// </remarks>
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
    /// ファイル名に最も適合する楽器名を検索します。
    /// </summary>
    /// <param name="fileName">ファイル名（拡張子なし）。</param>
    /// <param name="instrumentCandidates">楽器候補辞書（キー: 楽器名、値: 出現回数）。</param>
    /// <returns>最適な楽器名、見つからない場合は空文字。</returns>
    /// <remarks>
    /// <para>【マッチング戦略】</para>
    /// <list type="number">
    /// <item>完全一致を優先（"kick" vs "kick"）</item>
    /// <item>部分一致で補完（"kickdrum" contains "kick"）</item>
    /// <item>出現頻度の高い候補から順に検索（よくある楽器を優先）</item>
    /// </list>
    /// 
    /// <para>【Why 出現頻度順】</para>
    /// "kick"が100回、"tom"が3回の場合、"kick"から先に検索することで
    /// 誤検出（"tom"を"cymbal"内で誤検出）を減らします。
    /// </remarks>
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
            Debug.WriteLine($"[InstrumentNameDetectionService.FindBestInstrumentMatch] ERROR: {ex.Message}");
        }

        return string.Empty;
    }

    /// <summary>
    /// ファイルリストから楽器候補を統計的に抽出します。
    /// </summary>
    /// <param name="files">ファイルリスト。</param>
    /// <returns>楽器候補辞書（キー: 楽器名、値: 出現回数）。</returns>
    /// <remarks>
    /// <para>【Why 統計的手法】</para>
    /// 単一ファイルの命名規則は不安定（typo、略語の揺れ）ですが、
    /// 複数ファイルで共通して出現する単語は楽器種別である可能性が高くなります。
    /// 
    /// <para>【閾値の意味】</para>
    /// minimumOccurrences=3の場合:
    /// <list type="bullet">
    /// <item>1-2回: 偶然の一致、typo、特殊なファイル名</item>
    /// <item>3回以上: 意図的に使用されている楽器名の可能性</item>
    /// </list>
    /// 
    /// <para>【例】</para>
    /// <code>
    /// kick_01.wav, kick_02.wav, kick_03.wav, snare_01.wav, cymbal.wav
    /// → "kick": 3回（採用）、"snare": 1回（除外）、"cymbal": 1回（除外）
    /// </code>
    /// </remarks>
    private Dictionary<string, int> ExtractInstrumentCandidates(List<WavFiles> files)
    {
        var candidates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (files == null || files.Count == 0)
            return candidates;

        try
        {
            foreach (var file in files)
            {
                if (file?.Name == null)
                    continue;

                var fileName = Path.GetFileNameWithoutExtension(file.Name);
                var words = ExtractWordsFromFileName(fileName);

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
            Debug.WriteLine($"[InstrumentNameDetectionService.ExtractInstrumentCandidates] ERROR: {ex.Message}");
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
