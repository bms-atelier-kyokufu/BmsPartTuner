namespace BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;

/// <summary>
/// 音声ファイルの効率的なグループ化戦略を提供するクラスです。
/// 全ファイル総当たり比較（$O(N^2)$）を避け、類似ファイルのみを比較（$O(\sum m^2)$）することで計算量を大幅に削減します。
/// キーワードフィルタ（楽器種別など）や、ファイルサイズとRMS値による分類を利用してグループ化を行い、巨大なグループは自動的に分割します。
/// </summary>
[ADRAnchor("OPT-09", nameof(AudioFileGroupingStrategy))]
public sealed class AudioFileGroupingStrategy
{
    private AudioFileGroupingStrategy() { }

    private static readonly Logger<AudioFileGroupingStrategy> s_logger = new();

    /// <summary>
    /// ファイルリストをグループ化します。
    /// 指定されたキーワードフィルタがある場合はキーワードベースのパート分離を、
    /// 指定がない場合はファイルサイズとRMSを利用した従来の全体グループ化を行います。
    /// </summary>
    /// <param name="audioCache">音声キャッシュデータ。</param>
    /// <param name="fileList">音声ファイルリスト。</param>
    /// <param name="startPoint">開始位置。</param>
    /// <param name="endPoint">終了位置。</param>
    /// <param name="selectedKeywords">選択されたキーワード（nullまたは空の場合は全て処理）。</param>
    /// <returns>グループ化されたインデックスリスト。</returns>
    public static IReadOnlyList<IReadOnlyList<int>> GroupFiles(
        IReadOnlyDictionary<string, ICachedSoundData> audioCache,
        IReadOnlyList<BmsAudioFile> fileList,
        int startPoint,
        int endPoint,
        IEnumerable<string>? selectedKeywords = null)
    {
        var keywordList = selectedKeywords?.ToList();
        bool hasKeywordFilter = keywordList?.Count > 0;

        if (hasKeywordFilter)
        {
            s_logger.WriteDebug("=== GroupFiles with Keyword Filter ===");
            s_logger.WriteDebug($"Selected Keywords: {string.Join(", ", keywordList!)}");

            return GroupFilesByKeywords(audioCache, fileList, startPoint, endPoint, keywordList!);
        }
        else
        {
            s_logger.WriteDebug("=== GroupFiles without Keyword Filter ===");

            return GroupFilesTraditional(audioCache, fileList, startPoint, endPoint);
        }
    }

    /// <summary>
    /// キーワードベースのパート分離グループ化を行います。
    /// ファイル名（拡張子なし）に対して大文字小文字を区別せず部分一致でキーワードを判定し、
    /// 該当するキーワードのグループに追加します。巨大なグループは分割されます。
    /// </summary>
    private static List<IReadOnlyList<int>> GroupFilesByKeywords(
        IReadOnlyDictionary<string, ICachedSoundData> audioCache,
        IReadOnlyList<BmsAudioFile> fileList,
        int startPoint,
        int endPoint,
        List<string> selectedKeywords)
    {
        var timer = s_logger.StartTimer();
        var keywordGroups = new Dictionary<string, Dictionary<string, List<int>>>();

        int totalFiles = 0;
        int outOfRange = 0;
        int noCache = 0;
        int notMatchingKeywords = 0;

        foreach (var keyword in selectedKeywords)
        {
            keywordGroups[keyword] = [];
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

            if (string.IsNullOrEmpty(fileList[i].Name))
            {
                noCache++;
                continue;
            }

            audioCache.TryGetValue(fileList[i].Name, out var cachedData);
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

            // 比較エンジン(ParallelAudioComparisonEngine)がSort & Sweepアルゴリズムを使用して
            // RMS近傍検索を効率的に行うため、ここではRMSによるバケツ分けを行わず同一キーワードで1つのグループにします。
            const string groupKey = "ALL";

            if (!keywordGroups[matchedKeyword].TryGetValue(groupKey, out List<int>? value))
            {
                value = [];
                keywordGroups[matchedKeyword][groupKey] = value;
            }

            value.Add(i);
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

        s_logger.WriteDebug("=== GroupFilesByKeywords Complete ===");
        s_logger.WriteDebug($"Total in range: {totalFiles}");
        s_logger.WriteDebug($"Out of range: {outOfRange}");
        s_logger.WriteDebug($"No cache: {noCache}");
        s_logger.WriteDebug($"Not matching keywords: {notMatchingKeywords}");
        s_logger.WriteDebug($"Grouped files: {totalFiles - noCache - notMatchingKeywords}");

        foreach (var (keyword, count) in keywordStats)
        {
            s_logger.WriteDebug($"  Keyword '{keyword}': {count} files");
        }

        s_logger.WriteDebug($"Final groups: {finalGroups.Count}");
        s_logger.WriteDebug($"Time: {timer.Lap("GroupFilesByKeywords")}ms");

        return finalGroups;
    }

    /// <summary>
    /// 従来の全体グループ化（キーワードフィルタなし）を行います。
    /// 全ファイルをファイルサイズとRMS値（浮動小数点の誤差を吸収するために量子化）を組み合わせたキーで分類し、
    /// 巨大なグループは分割します。
    /// </summary>
    private static List<IReadOnlyList<int>> GroupFilesTraditional(
        IReadOnlyDictionary<string, ICachedSoundData> audioCache,
        IReadOnlyList<BmsAudioFile> fileList,
        int startPoint,
        int endPoint)
    {
        var timer = s_logger.StartTimer();
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

            if (string.IsNullOrEmpty(fileList[i].Name))
            {
                noCache++;
                continue;
            }

            audioCache.TryGetValue(fileList[i].Name, out var cachedData);
            if (cachedData == null)
            {
                noCache++;
                continue;
            }

            long fileSize = cachedData.FileSize;
            float rms = cachedData.TotalRms;

            int rmsQuantized = (int)(rms * AppConstants.Grouping.RmsQuantizationFactor);
            // 50ms (44100Hz 16bit Stereo = 8820 bytes) のブレを許容するため、サイズを量子化してグループ分けします。
            long sizeQuantized = fileSize / 8820;

            string groupKey = $"{sizeQuantized}_{rmsQuantized}";

            if (!groups.TryGetValue(groupKey, out var groupList))
            {
                groupList = [];
                groups[groupKey] = groupList;
            }

            groupList.Add(i);
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

        s_logger.WriteDebug("=== GroupFilesTraditional Complete ===");
        s_logger.WriteDebug($"Total in range: {totalFiles}");
        s_logger.WriteDebug($"Out of range: {outOfRange}");
        s_logger.WriteDebug($"No cache: {noCache}");
        s_logger.WriteDebug($"Grouped files: {totalFiles - noCache}");
        s_logger.WriteDebug($"Initial groups: {groups.Count}");
        s_logger.WriteDebug($"Final groups (after split): {finalGroups.Count}");
        s_logger.WriteDebug($"Time: {timer.Lap("GroupFilesTraditional")}ms");

        return finalGroups;
    }
}


