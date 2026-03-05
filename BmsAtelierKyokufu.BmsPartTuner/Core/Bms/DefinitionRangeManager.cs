using System.Diagnostics;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms;

/// <summary>
/// 定義番号の処理範囲を管理するクラス。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>処理範囲の開始・終了位置の決定</item>
/// <item>自動範囲検出（defEnd=0の場合）</item>
/// <item>範囲の妥当性検証</item>
/// </list>
/// 
/// <para>【Why 範囲指定】</para>
/// BMSファイルには数百～数千の定義番号が存在しますが、
/// 実際に使用されているのは一部のみです。処理範囲を限定することで、
/// 不要な比較を避け、処理時間を短縮できます。
/// 
/// <para>【自動範囲検出】</para>
/// defEnd=0または負の値を指定すると、ファイルリストから
/// 実際に使用されている最大定義番号を自動検出します。
/// </remarks>
internal class DefinitionRangeManager
{
    private readonly IReadOnlyList<WavFiles> _fileList;

    /// <summary>処理範囲の開始定義番号。</summary>
    public int StartPoint { get; private set; }

    /// <summary>処理範囲の終了定義番号。</summary>
    public int EndPoint { get; private set; }

    /// <summary>
    /// DefinitionRangeManagerを初期化します。
    /// </summary>
    /// <param name="fileList">ファイルリスト。</param>
    /// <exception cref="ArgumentNullException">fileListがnullの場合。</exception>
    /// <remarks>
    /// <para>【デフォルト範囲】</para>
    /// StartPoint: 1（BMSの最小定義番号）
    /// EndPoint: 3843（62進数の最大定義番号"zz"）
    /// </remarks>
    public DefinitionRangeManager(IReadOnlyList<WavFiles> fileList)
    {
        _fileList = fileList ?? throw new ArgumentNullException(nameof(fileList));
        StartPoint = AppConstants.Definition.MinNumber;
        EndPoint = AppConstants.Definition.MaxNumberBase62;
    }

    /// <summary>
    /// 処理範囲を決定します。
    /// </summary>
    /// <param name="defStart">開始定義番号。</param>
    /// <param name="defEnd">終了定義番号（0または負の値の場合、自動検出）。</param>
    /// <remarks>
    /// <para>【処理内容】</para>
    /// <list type="number">
    /// <item>ファイルリストから最大定義番号を取得</item>
    /// <item>開始・終了位置の妥当性を検証</item>
    /// <item>実際のファイルリストの開始位置を考慮</item>
    /// <item>デバッグログに範囲情報を出力</item>
    /// </list>
    /// 
    /// <para>【自動検出（defEnd≤0）】</para>
    /// ファイルリスト内の最大定義番号を自動的に検出します。
    /// これにより、不要な範囲を処理対象から除外できます。
    /// 
    /// <para>【範囲補正】</para>
    /// <list type="bullet">
    /// <item>defStart &lt; 1: 1に補正</item>
    /// <item>defEnd &gt; 3843: 3843に補正</item>
    /// <item>defEnd ≤ 0: ファイルリストから自動検出</item>
    /// </list>
    /// 
    /// <para>【例】</para>
    /// <code>
    /// ファイルリスト: [01, 02, 03, ..., 50]
    /// defStart=1, defEnd=0 → StartPoint=1, EndPoint=50（自動検出）
    /// defStart=10, defEnd=30 → StartPoint=10, EndPoint=30（明示指定）
    /// </code>
    /// </remarks>
    public void DetermineProcessingRange(int defStart, int defEnd)
    {
        int maxDefined = AppConstants.Definition.MinNumber;
        if (_fileList != null && _fileList.Count > 0)
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
        var firstItem = (_fileList ?? Enumerable.Empty<WavFiles>()).FirstOrDefault();
        if (firstItem != null)
        {
            firstNum = firstItem.NumInteger;
        }

        StartPoint = Math.Max(firstNum, defStart);
        EndPoint = Math.Min(maxDefined, defEnd);

        Debug.WriteLine($"Processing range: {StartPoint} - {EndPoint} ({EndPoint - StartPoint + 1} definitions)");
    }
}
