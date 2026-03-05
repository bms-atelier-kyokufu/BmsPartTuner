using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

namespace BmsAtelierKyokufu.BmsPartTuner.Services;

/// <summary>
/// WPF CollectionViewのフィルタリング機能を抽象化し、テキスト/楽器種別/Smart Filter Chipsによるフィルターを提供します。
/// </summary>
/// <remarks>
/// <para>【目的】</para>
/// <list type="number">
/// <item>テキストベースフィルタリング（ファイル名検索）</item>
/// <item>楽器種別フィルタリング（チェックボックス選択）</item>
/// <item>Smart Filter Chips生成（統計ベースの候補提示）</item>
/// </list>
/// 
/// <para>【Why CollectionViewを使用】</para>
/// WPFの<see cref="ICollectionView"/>は、元のコレクションを変更せずに
/// 表示内容をフィルター・ソートできるため、UIとデータを分離できます。
/// また、フィルター変更時にUIが自動的に更新されます。
/// </remarks>
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
    /// 選択可能なFilterChip データモデル（双方向バインディング用）。
    /// </summary>
    /// <remarks>
    /// <para>【Why ObservableObject】</para>
    /// IsSelectedプロパティの変更をUIに自動反映するため。
    /// チェックボックスの状態とプロパティを双方向バインディングします。
    /// </remarks>
    public partial class SelectableFilterChip : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        public string Keyword { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>
    /// FileListFilterServiceを初期化します。
    /// </summary>
    /// <remarks>
    /// <para>【Why minimumOccurrences=2】</para>
    /// FilterChips生成では、<see cref="InstrumentNameDetectionService"/>（minimumOccurrences=3）より
    /// 緩い基準を使用します。理由: フィルターUIでは候補を多く提示する方がユーザビリティが高いため。
    /// </remarks>
    public FileListFilterService()
    {
        _instrumentDetectionService = new InstrumentNameDetectionService(minimumOccurrences: 2);
    }

    /// <summary>
    /// CollectionViewを設定します。
    /// </summary>
    /// <remarks>
    /// <para>【Why 外部から注入】</para>
    /// ViewModelが管理するCollectionViewを受け取ることで、
    /// Serviceは状態を持たず、複数のViewModelで再利用可能です。
    /// </para>
    /// </remarks>
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
    /// 楽器種別フィルターを適用します（AND条件）。
    /// </summary>
    /// <remarks>
    /// <para>【Why HashSetをコピー】</para>
    /// 元のコレクションが変更されても、フィルター条件が変わらないよう防御的コピーを行います。
    /// また、Contains()がO(1)で高速なため、大量のファイル処理に適しています。
    /// </para>
    /// </remarks>
    public void ApplyInstrumentFilter(HashSet<string> selectedInstruments)
    {
        _selectedInstruments = selectedInstruments != null
            ? new HashSet<string>(selectedInstruments, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        UpdateFilter();
    }

    /// <summary>
    /// フィルター条件を結合してCollectionViewに適用します。
    /// </summary>
    /// <remarks>
    /// <para>【フィルターロジック】</para>
    /// テキストフィルター AND 楽器フィルター の両方を満たす項目のみ表示。
    /// フィルターなしの場合、全項目を表示（Filter=null）。
    /// 
    /// <para>【Why ローカル変数selectedSetにキャプチャ】</para>
    /// Predicateデリゲート内で_selectedInstrumentsを直接参照すると、
    /// フィルター実行中に別スレッドで_selectedInstrumentsが変更される可能性があります。
    /// ローカル変数にコピーすることでスレッドセーフを確保します。
    /// </para>
    /// </remarks>
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
                if (obj is WavFiles item)
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
    /// Filter Chipベースのフィルターを適用します（OR条件）。
    /// </summary>
    /// <remarks>
    /// <para>【Why OR条件】</para>
    /// ユーザーが複数のチップを選択した場合、いずれかに該当すれば表示する方が直感的です。
    /// 例: "kick" OR "snare" → キックかスネアのいずれかを表示。
    /// 
    /// <para>【マッチング戦略】</para>
    /// <list type="number">
    /// <item>InstrumentName（統計的推定）での一致を優先</item>
    /// <item>ファイル名での部分一致で補完</item>
    /// </list>
    /// </para>
    /// </remarks>
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
                if (obj is WavFiles item)
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
    /// Smart Filter Chipsを生成します（選択可能版）。
    /// </summary>
    /// <param name="files">ファイルリスト。</param>
    /// <param name="minOccurrences">最小出現回数（デフォルト: 2）。</param>
    /// <param name="maxChips">最大チップ数（デフォルト: 8、UI表示の限界）。</param>
    /// <param name="minKeywordLength">最小キーワード長（デフォルト: 3）。</param>
    /// <returns>SelectableFilterChipのコレクション。</returns>
    /// <remarks>
    /// <para>【アルゴリズム】</para>
    /// <list type="number">
    /// <item>ファイル名の先頭単語（プレフィックス）を抽出</item>
    /// <item>出現回数をカウント</item>
    /// <item>頻度降順でソートし、上位N件を採用</item>
    /// </list>
    /// 
    /// <para>【Why 先頭単語のみ】</para>
    /// BMSの命名規則では、楽器名がプレフィックスに来ることが多いです。
    /// 例: "kick_01.wav", "snare_loud.wav"
    /// 先頭のみに絞ることで、ノイズ（"01", "loud"等）を削減します。
    /// </para>
    /// </remarks>
    public ObservableCollection<SelectableFilterChip> GenerateSelectableFilterChips(
        ObservableCollection<WavFiles> files,
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
                    if (keywordCounts.ContainsKey(prefix))
                        keywordCounts[prefix]++;
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
    /// Smart Filter Chipsを生成します（読み取り専用版）。
    /// </summary>
    /// <remarks>
    /// <para>【生成戦略】</para>
    /// <list type="number">
    /// <item>InstrumentName（統計的推定）を優先使用</item>
    /// <item>InstrumentNameが不足している場合、ファイル名統計で補完</item>
    /// </list>
    /// 
    /// <para>【Why 2段階アプローチ】</para>
    /// <see cref="InstrumentNameDetectionService"/>が高精度な楽器名を推定しているため、
    /// これを最優先で使用します。不足分のみファイル名統計で補うことで、
    /// ノイズを最小限に抑えながら十分な候補を提供します。
    /// 
    /// <para>【補完条件】</para>
    /// maxChips / 2 未満の場合のみ補完。
    /// 理由: 楽器名が十分に推定されている場合、ファイル名統計は不要です。
    /// </para>
    /// </remarks>
    public IList<FilterChip> GenerateFilterChips(
        ObservableCollection<WavFiles> files,
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
