namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms;

/// <summary>
/// BMSファイルリストに対するフィルタリング（テキスト検索、楽器種別、キーワードチップ）機能を提供するサービス。
/// WPFのICollectionViewを利用してUIとデータの同期を管理します。
/// </summary>
public partial class FileListFilterService
{
    private readonly InstrumentNameDetectionService _instrumentDetectionService;

    /// <summary>
    /// FilterChip データモデル（読み取り専用、UI表示用の楽器候補情報を保持）。
    /// </summary>
    public class FilterChip
    {
        /// <summary>楽器名キーワード（例: "kick", "snare"）。</summary>
        public string Keyword { get; set; } = string.Empty;

        /// <summary>このキーワードに該当するファイル数。</summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// UIでの選択状態を双方向にバインディング可能なフィルターチップのデータモデル。
    /// </summary>
    public partial class SelectableFilterChip : ObservableObject
    {
        [ObservableProperty]
        public partial bool IsSelected { get; set; }
        public string Keyword { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>
    /// FileListFilterServiceを初期化します。
    /// </summary>
    public FileListFilterService()
    {
        _instrumentDetectionService = new InstrumentNameDetectionService(minimumOccurrences: 2);
    }

    /// <summary>
    /// 指定されたテキスト条件および楽器種別条件に基づいてフィルター用Predicateを生成します。
    /// </summary>
    public static Predicate<object> CreateFilterPredicate(string textFilter, HashSet<string>? selectedInstruments)
    {
        bool hasTextFilter = !string.IsNullOrWhiteSpace(textFilter);
        bool hasInstrumentFilter = selectedInstruments?.Count > 0;

        if (!hasTextFilter && !hasInstrumentFilter)
        {
            return static _ => true;
        }

        // スレッドセーフのためローカル変数にキャプチャ
        var selectedSet = hasInstrumentFilter ? new HashSet<string>(selectedInstruments!, StringComparer.OrdinalIgnoreCase) : null;
        var text = textFilter ?? string.Empty;

        return (obj) =>
        {
            if (obj is BmsAudioFile item)
            {
                // AND条件1: テキストフィルター
                if (hasTextFilter && !item.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // AND条件2: 楽器フィルター
                if (hasInstrumentFilter && selectedSet != null)
                {
                    var instrument = item.InstrumentName ?? string.Empty;
                    if (!selectedSet.Contains(instrument))
                    {
                        return false;
                    }
                }

                return true;
            }
            return false;
        };
    }

    /// <summary>
    /// 選択されたキーワードのリストに基づいて、いずれかに一致するファイルを抽出するPredicateを生成します（OR条件）。
    /// </summary>
    public static Predicate<object> CreateChipFilterPredicate(IEnumerable<string>? selectedKeywords)
    {
        var keywordList = selectedKeywords?.ToList();

        if (keywordList == null || keywordList.Count == 0)
        {
            return static _ => true;
        }

        return (obj) =>
        {
            if (obj is BmsAudioFile item)
            {
                // 優先戦略: InstrumentName（統計的に信頼性が高い）
                if (!string.IsNullOrEmpty(item.InstrumentName))
                {
                    return keywordList.Any(keyword =>
                        item.InstrumentName.Equals(keyword, StringComparison.OrdinalIgnoreCase))
                        || keywordList.Any(keyword =>
                        Path.GetFileNameWithoutExtension(item.Name)
                            .Contains(keyword, StringComparison.OrdinalIgnoreCase));
                }

                // フォールバック: ファイル名での部分一致
                var fileName = Path.GetFileNameWithoutExtension(item.Name);
                return keywordList.Any(keyword =>
                    fileName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }
            return false;
        };
    }

    /// <summary>
    /// ファイルリストから頻出するプレフィックスを抽出し、選択可能なフィルターチップのリストを生成します。
    /// </summary>
    /// <param name="files">対象のファイルリスト。</param>
    /// <param name="minOccurrences">チップとして抽出されるための最小出現回数。</param>
    /// <param name="maxChips">生成するチップの最大数。</param>
    /// <param name="minKeywordLength">キーワードとして抽出される最小の文字列長。</param>
    /// <returns>選択可能なフィルターチップのコレクション。</returns>
    private static readonly System.Buffers.SearchValues<char> SeparatorSearchValues = System.Buffers.SearchValues.Create("_ -");

    public static ObservableCollection<SelectableFilterChip> GenerateSelectableFilterChips(
        ObservableCollection<BmsAudioFile> files,
        int minOccurrences = 2,
        int maxChips = 8,
        int minKeywordLength = 3)
    {
        var keywordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file.Name);
            var span = fileName.AsSpan().TrimStart("_ -");
            int index = span.IndexOfAny(SeparatorSearchValues);

            var prefixSpan = index >= 0 ? span[..index] : span;

            if (prefixSpan.Length >= minKeywordLength)
            {
                var prefix = prefixSpan.ToString();

                if (keywordCounts.TryGetValue(prefix, out int value))
                    keywordCounts[prefix] = value + 1;
                else
                    keywordCounts[prefix] = 1;
            }
        }

        var chips = keywordCounts
            .Where(kvp => kvp.Value >= minOccurrences)
            .OrderByDescending(kvp => kvp.Value)
            .Take(maxChips)
            .Select(kvp => new SelectableFilterChip
            {
                Keyword = kvp.Key,
                Count = kvp.Value,
                IsSelected = true // Default to selected
            })
            .ToList();

        return new ObservableCollection<SelectableFilterChip>(chips);
    }

    /// <summary>
    /// 統計的推定とファイル名解析を組み合わせて、効果的なフィルターチップ（読み取り専用）を生成します。
    /// </summary>
    public IList<FilterChip> GenerateFilterChips(
        ObservableCollection<BmsAudioFile> files,
        int minOccurrences = 2,
        int maxChips = 8,
        int minKeywordLength = 3)
    {
        var keywordCounts = new Dictionary<string, int>();

        // フェーズ1: InstrumentName（統計的推定）からチップ生成
        foreach (var file in files)
        {
            if (!string.IsNullOrEmpty(file.InstrumentName) && file.InstrumentName.Length >= minKeywordLength)
            {
                keywordCounts[file.InstrumentName] = keywordCounts.GetValueOrDefault(file.InstrumentName, 0) + 1;
            }
        }

        // フェーズ2: 不足している場合のみファイル名統計で補完
        if (keywordCounts.Count < maxChips / 2)
        {
            foreach (var file in files)
            {
                // InstrumentNameが設定されていないファイルのみ対象
                if (string.IsNullOrEmpty(file.InstrumentName))
                {
                    var fileName = Path.GetFileNameWithoutExtension(file.Name);
                    var candidates = _instrumentDetectionService.ExtractWordsFromFileName(fileName);

                    foreach (var candidate in candidates)
                    {
                        // 重複チェック＆妥当性チェック
                        if (!keywordCounts.ContainsKey(candidate) &&
                            _instrumentDetectionService.IsValidInstrumentCandidate(candidate))
                        {
                            keywordCounts[candidate] = keywordCounts.GetValueOrDefault(candidate, 0) + 1;
                        }
                    }
                }
            }
        }

        var chips = keywordCounts
            .Where(kvp => kvp.Value >= minOccurrences)
            .OrderByDescending(kvp => kvp.Value)
            .Take(maxChips)
            .Select(kvp => new FilterChip
            {
                Keyword = kvp.Key,
                Count = kvp.Value,
            })
            .ToList();

        return chips ?? [];
    }
}
