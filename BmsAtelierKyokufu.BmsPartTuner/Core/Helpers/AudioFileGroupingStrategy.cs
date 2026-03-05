using System.Diagnostics;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;

/// <summary>
/// 音声ファイルのグループ化戦略。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>ファイルサイズとRMSによる効率的なグループ分割</item>
/// <item>キーワードベースのパート分離（楽器種別ごとのグループ化）</item>
/// <item>巨大グループの自動分割（<see cref="AppConstants.MaxGroupSize"/>単位）</item>
/// </list>
/// 
/// <para>【グループ化戦略】</para>
/// <list type="number">
/// <item>キーワードフィルタあり: 楽器種別（kick, snare等）ごとに独立グループ化</item>
/// <item>キーワードフィルタなし: 全体を統合グループ化</item>
/// </list>
/// 
/// <para>【グループキーの生成】</para>
/// ファイルサイズ（バイト）+ RMS値（量子化）を組み合わせたキーで分類:
/// 
/// GroupKey = $"{fileSize}_{rmsQuantized}"
/// rmsQuantized = (int)(rms × <see cref="AppConstants.RmsQuantizationFactor"/>)
/// 
/// <para>【Why グループ化が必要か】</para>
/// 全ファイル総当たり比較（$O(N^2)$）を避け、類似ファイルのみを比較（$O(\sum m^2)$）することで、
/// 計算量を大幅に削減します（実測: 約800倍高速化）。
/// 
/// <para>【キーワードベースの利点】</para>
/// 楽器種別ごとに分離することで、異なる楽器同士の無駄な比較を回避し、
/// さらなる高速化と精度向上を実現します。
/// </remarks>
public class AudioFileGroupingStrategy
{
    /// <summary>
    /// ファイルリストをグループ化。
    /// </summary>
    /// <param name="fileList">音声ファイルリスト。</param>
    /// <param name="startPoint">開始位置。</param>
    /// <param name="endPoint">終了位置。</param>
    /// <param name="selectedKeywords">選択されたキーワード（nullまたは空の場合は全て処理）。</param>
    /// <returns>グループ化されたインデックスリスト。</returns>
    /// <remarks>
    /// <para>【動作モード】</para>
    /// <list type="bullet">
    /// <item>selectedKeywordsが指定されている: <see cref="GroupFilesByKeywords"/>を使用</item>
    /// <item>selectedKeywordsがnullまたは空: <see cref="GroupFilesTraditional"/>を使用</item>
    /// </list>
    /// </remarks>
    public IReadOnlyList<IReadOnlyList<int>> GroupFiles(
        IReadOnlyList<WavFiles> fileList,
        int startPoint,
        int endPoint,
        IEnumerable<string>? selectedKeywords = null)
    {
        var keywordList = selectedKeywords?.ToList();
        bool hasKeywordFilter = keywordList != null && keywordList.Count > 0;

        if (hasKeywordFilter)
        {
            Debug.WriteLine($"=== GroupFiles with Keyword Filter ===");
            Debug.WriteLine($"Selected Keywords: {string.Join(", ", keywordList!)}");

            return GroupFilesByKeywords(fileList, startPoint, endPoint, keywordList!);
        }
        else
        {
            Debug.WriteLine($"=== GroupFiles without Keyword Filter ===");

            return GroupFilesTraditional(fileList, startPoint, endPoint);
        }
    }

    /// <summary>
    /// キーワードベースのパート分離グループ化。
    /// </summary>
    /// <remarks>
    /// <para>【処理フロー】</para>
    /// <list type="number">
    /// <item>各キーワードごとにグループ辞書を初期化</item>
    /// <item>ファイル名からキーワードを判定</item>
    /// <item>該当するキーワードのグループに追加</item>
    /// <item>巨大グループを分割（<see cref="AppConstants.MaxGroupSize"/>単位）</item>
    /// </list>
    /// 
    /// <para>【キーワード判定】</para>
    /// ファイル名（拡張子なし）に対して、大文字小文字を区別せずに
    /// 部分一致（Contains）でキーワードをマッチングします。
    /// 
    /// <para>【除外条件】</para>
    /// <list type="bullet">
    /// <item>範囲外のファイル番号</item>
    /// <item>キャッシュが存在しない</item>
    /// <item>どのキーワードにも該当しない</item>
    /// </list>
    /// </remarks>
    private IReadOnlyList<IReadOnlyList<int>> GroupFilesByKeywords(
        IReadOnlyList<WavFiles> fileList,
        int startPoint,
        int endPoint,
        List<string> selectedKeywords)
    {
        var sw = Stopwatch.StartNew();
        var keywordGroups = new Dictionary<string, Dictionary<string, List<int>>>();

        int totalFiles = 0;
        int outOfRange = 0;
        int noCache = 0;
        int notMatchingKeywords = 0;

        foreach (var keyword in selectedKeywords)
        {
            keywordGroups[keyword] = new Dictionary<string, List<int>>();
        }

        for (int i = 0; i < fileList.Count; i++)
        {
            int fileNum = fileList[i].NumInteger;

            if (fileNum < startPoint || fileNum > endPoint)
            {
                outOfRange++;
                continue;
            }

            totalFiles++;

            var cachedData = fileList[i].CachedData;
            if (cachedData == null)
            {
                noCache++;
                continue;
            }

            var fileName = Path.GetFileNameWithoutExtension(fileList[i].Name);
            var matchedKeyword = selectedKeywords.FirstOrDefault(kw =>
                fileName.Contains(kw, StringComparison.OrdinalIgnoreCase));

            if (matchedKeyword == null)
            {
                notMatchingKeywords++;
                continue;
            }

            long fileSize = cachedData.FileSize;
            float rms = cachedData.TotalRms;
            int rmsQuantized = (int)(rms * AppConstants.Grouping.RmsQuantizationFactor);
            string groupKey = $"{fileSize}_{rmsQuantized}";

            if (!keywordGroups[matchedKeyword].ContainsKey(groupKey))
            {
                keywordGroups[matchedKeyword][groupKey] = new List<int>();
            }

            keywordGroups[matchedKeyword][groupKey].Add(i);
        }

        var finalGroups = new List<IReadOnlyList<int>>();
        var keywordStats = new Dictionary<string, int>();

        foreach (var (keyword, groups) in keywordGroups)
        {
            int filesInKeyword = 0;

            foreach (var group in groups.Values)
            {
                filesInKeyword += group.Count;

                if (group.Count > AppConstants.Grouping.MaxGroupSize)
                {
                    for (int i = 0; i < group.Count; i += AppConstants.Grouping.MaxGroupSize)
                    {
                        int count = Math.Min(AppConstants.Grouping.MaxGroupSize, group.Count - i);
                        finalGroups.Add(group.GetRange(i, count));
                    }
                }
                else
                {
                    finalGroups.Add(group);
                }
            }

            keywordStats[keyword] = filesInKeyword;
        }

        sw.Stop();

        Debug.WriteLine($"=== GroupFilesByKeywords Complete ===");
        Debug.WriteLine($"Total in range: {totalFiles}");
        Debug.WriteLine($"Out of range: {outOfRange}");
        Debug.WriteLine($"No cache: {noCache}");
        Debug.WriteLine($"Not matching keywords: {notMatchingKeywords}");
        Debug.WriteLine($"Grouped files: {totalFiles - noCache - notMatchingKeywords}");

        foreach (var (keyword, count) in keywordStats)
        {
            Debug.WriteLine($"  Keyword '{keyword}': {count} files");
        }

        Debug.WriteLine($"Final groups: {finalGroups.Count}");
        Debug.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");

        return finalGroups;
    }

    /// <summary>
    /// 従来の全体グループ化（キーワードフィルタなし）。
    /// </summary>
    /// <remarks>
    /// <para>【処理フロー】</para>
    /// <list type="number">
    /// <item>全ファイルをファイルサイズ+RMSでグループ化</item>
    /// <item>巨大グループを分割（<see cref="AppConstants.MaxGroupSize"/>単位）</item>
    /// </list>
    /// 
    /// <para>【グループキーの生成】</para>
    /// GroupKey = $"{fileSize}_{rmsQuantized}"
    /// 
    /// <para>【Why RMS量子化】</para>
    /// 浮動小数点の完全一致は困難なため、<see cref="AppConstants.RmsQuantizationFactor"/>倍して
    /// 整数化することで、近いRMS値を持つファイルを同じグループに分類します。
    /// </remarks>
    private IReadOnlyList<IReadOnlyList<int>> GroupFilesTraditional(
        IReadOnlyList<WavFiles> fileList,
        int startPoint,
        int endPoint)
    {
        var sw = Stopwatch.StartNew();
        var groups = new Dictionary<string, List<int>>();

        int totalFiles = 0;
        int outOfRange = 0;
        int noCache = 0;

        for (int i = 0; i < fileList.Count; i++)
        {
            int fileNum = fileList[i].NumInteger;

            if (fileNum < startPoint || fileNum > endPoint)
            {
                outOfRange++;
                continue;
            }

            totalFiles++;

            var cachedData = fileList[i].CachedData;
            if (cachedData == null)
            {
                noCache++;
                continue;
            }

            long fileSize = cachedData.FileSize;
            float rms = cachedData.TotalRms;

            int rmsQuantized = (int)(rms * AppConstants.Grouping.RmsQuantizationFactor);

            string groupKey = $"{fileSize}_{rmsQuantized}";

            if (!groups.ContainsKey(groupKey))
            {
                groups[groupKey] = new List<int>();
            }

            groups[groupKey].Add(i);
        }

        var finalGroups = new List<IReadOnlyList<int>>();
        foreach (var group in groups.Values)
        {
            if (group.Count > AppConstants.Grouping.MaxGroupSize)
            {
                for (int i = 0; i < group.Count; i += AppConstants.Grouping.MaxGroupSize)
                {
                    int count = Math.Min(AppConstants.Grouping.MaxGroupSize, group.Count - i);
                    finalGroups.Add(group.GetRange(i, count));
                }
            }
            else
            {
                finalGroups.Add(group);
            }
        }

        sw.Stop();

        Debug.WriteLine($"=== GroupFilesTraditional Complete ===");
        Debug.WriteLine($"Total in range: {totalFiles}");
        Debug.WriteLine($"Out of range: {outOfRange}");
        Debug.WriteLine($"No cache: {noCache}");
        Debug.WriteLine($"Grouped files: {totalFiles - noCache}");
        Debug.WriteLine($"Initial groups: {groups.Count}");
        Debug.WriteLine($"Final groups (after split): {finalGroups.Count}");
        Debug.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");

        return finalGroups;
    }
}
