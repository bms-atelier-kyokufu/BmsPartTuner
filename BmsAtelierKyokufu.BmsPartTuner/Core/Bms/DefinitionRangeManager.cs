namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms;

/// <summary>
/// BMSファイルの定義番号の処理範囲を管理するクラスです。
/// 実際に使用されている範囲のみに限定することで不要な比較を避け、
/// 指定された終了番号が0または負の場合は、ファイルリストから自動で最大定義番号を検出します。
/// デフォルトの範囲は1（最小）から3843（62進数最大 "zz"）です。
/// </summary>
internal class DefinitionRangeManager(IReadOnlyList<BmsAudioFile> fileList)
{
    private readonly IReadOnlyList<BmsAudioFile> _fileList = fileList ?? throw new ArgumentNullException(nameof(fileList));

    /// <summary>処理範囲の開始定義番号。</summary>
    public int StartPoint { get; private set; } = AppConstants.Definition.MinNumber;

    /// <summary>処理範囲の終了定義番号。</summary>
    public int EndPoint { get; private set; } = AppConstants.Definition.MaxNumberBase62;

    /// <summary>
    /// 指定された開始・終了定義番号から処理範囲を決定します。
    /// 終了番号が0以下の場合はファイルリストの最大定義番号を自動検出し、
    /// 全体の範囲がBMS定義の許容範囲内（1～3843）に収まるように補正します。
    /// </summary>
    /// <param name="defStart">開始定義番号。</param>
    /// <param name="defEnd">終了定義番号（0または負の値の場合、自動検出）。</param>
    public void DetermineProcessingRange(int defStart, int defEnd)
    {
        int maxDefined = AppConstants.Definition.MinNumber;
        if (_fileList?.Count > 0)
        {
            for (int i = 0; i < _fileList.Count; i++)
            {
                if (_fileList[i].NumInteger > maxDefined)
                    maxDefined = _fileList[i].NumInteger;
            }
        }

        if (defStart < AppConstants.Definition.MinNumber)
            defStart = AppConstants.Definition.MinNumber;

        if (defEnd <= 0)
            defEnd = maxDefined;

        if (defEnd > AppConstants.Definition.MaxNumberBase62 - 1)
            defEnd = AppConstants.Definition.MaxNumberBase62 - 1;

        int firstNum = AppConstants.Definition.MinNumber;
        var firstItem = (_fileList ?? Enumerable.Empty<BmsAudioFile>()).FirstOrDefault();
        if (firstItem != null)
        {
            firstNum = firstItem.NumInteger;
        }

        StartPoint = Math.Max(firstNum, defStart);
        EndPoint = Math.Min(maxDefined, defEnd);

        PerformanceDebugLogger.WriteDebug(nameof(DefinitionRangeManager), $"Processing range: {StartPoint} - {EndPoint} ({EndPoint - StartPoint + 1} definitions)");
    }
}
