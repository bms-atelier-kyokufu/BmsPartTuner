namespace BmsAtelierKyokufu.BmsPartTuner.Services.Bms;

/// <summary>
/// BMSファイルリストに対するフィルタリング（テキスト検索、楽器種別、キーワードチップ）機能を提供するサービス。
/// WPFのICollectionViewを利用してUIとデータの同期を管理します。
/// </summary>
public partial class FileListFilterService
{
    private ICollectionView? _collectionView;
    private readonly InstrumentNameDetectionService _instrumentDetectionService;
    private string _textFilter = string.Empty;
    private HashSet<string> _selectedInstruments = new(StringComparer.OrdinalIgnoreCase);

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
    /// 楽器名の検出には緩めの基準（出現回数2回以上）を使用します。
    /// </summary>
    public FileListFilterService()
    {
        _instrumentDetectionService = new InstrumentNameDetectionService(minimumOccurrences: 2);
    }

    /// <summary>
    /// フィルタリングの対象となるCollectionViewを設定します。
    /// </summary>
    public void SetCollectionView(ICollectionView collectionView)
    {
        _collectionView = collectionView;
    }

    /// <summary>
    /// テキストベースのフィルターを適用します（ファイル名に指定文字列が含まれるかを判定、大文字小文字を区別しない）。
    /// </summary>
    public void ApplyFilter(string filterText)
    {
        _textFilter = filterText ?? string.Empty;
        UpdateFilter();
    }

    /// <summary>
    /// 指定された楽器種別のセットに基づいてファイルリストをフィルタリングします（AND条件）。
    /// </summary>
    public void ApplyInstrumentFilter(HashSet<string> selectedInstruments)
    {
        _selectedInstruments = selectedInstruments != null
            ? new HashSet<string>(selectedInstruments, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        UpdateFilter();
    }

    /// <summary>
    /// 現在設定されているテキストフィルターと楽器フィルターの条件を結合し、CollectionViewに適用します。
    /// スレッドセーフ性を確保するため、フィルター実行時には条件のローカルコピーを使用します。
    /// </summary>
    private void UpdateFilter()
    {
        if (_collectionView == null) return;

        bool hasTextFilter = !string.IsNullOrWhiteSpace(_textFilter);
        bool hasInstrumentFilter = _selectedInstruments.Count > 0;

        if (!hasTextFilter && !hasInstrumentFilter)
        {
            // フィルターなし: 全項目を表示
            _collectionView.Filter = null;
        }
        else
        {
            // スレッドセーフのためローカル変数にキャプチャ
            var selectedSet = _selectedInstruments;

            _collectionView.Filter = (obj) =>
            {
                if (obj is BmsAudioFile item)
                {
                    // AND条件1: テキストフィルター
                    if (hasTextFilter && !item.Name.Contains(_textFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    // AND条件2: 楽器フィルター
                    if (hasInstrumentFilter)
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

        _collectionView.Refresh();
    }

    /// <summary>
    /// 選択されたキーワードのリストに基づいて、いずれかに一致するファイルを抽出します（OR条件）。
    /// 統計的推定による楽器名との完全一致を優先し、フォールバックとしてファイル名の部分一致を使用します。
    /// </summary>
    public void ApplyChipFilter(IEnumerable<string> selectedKeywords)
    {
        if (_collectionView == null) return;

        var keywordList = selectedKeywords?.ToList();

        if (keywordList == null || keywordList.Count == 0)
        {
            _collectionView.Filter = null;
        }
        else
        {
            _collectionView.Filter = (obj) =>
            {
                if (obj is BmsAudioFile item)
                {
                    // 優先戦略: InstrumentName（統計的に信頼性が高い）
                    if (!string.IsNullOrEmpty(item.InstrumentName))
                    {
                        // InstrumentNameが割り当てられている場合は、それを優先して完全一致で評価します。
                        if (!string.IsNullOrEmpty(item.InstrumentName))
                        {
                            return keywordList.Any(keyword =>
                                item.InstrumentName.Equals(keyword, StringComparison.OrdinalIgnoreCase));
                        }

                        // InstrumentNameがない場合のみ、フォールバックとしてファイル名で部分一致を評価します。
                        return keywordList.Any(keyword =>
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

        _collectionView.Refresh();
    }

    /// <summary>
    /// ファイルリストから頻出するプレフィックスを抽出し、選択可能なフィルターチップのリストを生成します。
    /// </summary>
    /// <param name="files">対象のファイルリスト。</param>
    /// <param name="minOccurrences">チップとして抽出されるための最小出現回数。</param>
    /// <param name="maxChips">生成するチップの最大数。</param>
    /// <param name="minKeywordLength">キーワードとして抽出される最小の文字列長。</param>
    /// <returns>選択可能なフィルターチップのコレクション。</returns>
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
            var parts = fileName.Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length > 0)
            {
                var prefix = parts[0];

                if (prefix.Length >= minKeywordLength)
                {
                    if (keywordCounts.TryGetValue(prefix, out int value))
                        keywordCounts[prefix] = ++value;
                    else
                        keywordCounts[prefix] = 1;
                }
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
