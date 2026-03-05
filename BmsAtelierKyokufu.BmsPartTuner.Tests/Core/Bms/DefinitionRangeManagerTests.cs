using BmsAtelierKyokufu.BmsPartTuner.Core;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Bms;

/// <summary>
/// <see cref="DefinitionRangeManager"/> のテストクラス。
/// 
/// 【テスト対象】
/// - 処理範囲の決定（自動検出、明示指定）
/// - 範囲の妥当性検証
/// - 境界値処理
/// 
/// 【テスト設計方針】
/// - 境界値分析: 0, 1, 最大値
/// - 自動検出: defEnd=0の動作
/// - エッジケース: 空リスト、逆順指定
/// </summary>
public class DefinitionRangeManagerTests
{
    #region Helper Methods

    private static WavFiles CreateWavFile(int numInteger, string num = "")
    {
        return new WavFiles
        {
            NumInteger = numInteger,
            Num = string.IsNullOrEmpty(num) ? numInteger.ToString("D2") : num,
            Name = $"test_{numInteger}.wav",
            FileSize = 1000
        };
    }

    private static List<WavFiles> CreateFileList(params int[] numbers)
    {
        return numbers.Select(n => CreateWavFile(n)).ToList();
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidFileList_InitializesCorrectly()
    {
        // Arrange
        var fileList = CreateFileList(1, 10, 50);

        // Act
        var manager = new DefinitionRangeManager(fileList);

        // Assert
        Assert.Equal(AppConstants.Definition.MinNumber, manager.StartPoint);
        Assert.Equal(AppConstants.Definition.MaxNumberBase62, manager.EndPoint);
    }

    [Fact]
    public void Constructor_WithEmptyFileList_InitializesWithDefaults()
    {
        // Arrange
        var fileList = new List<WavFiles>();

        // Act
        var manager = new DefinitionRangeManager(fileList);

        // Assert
        Assert.Equal(AppConstants.Definition.MinNumber, manager.StartPoint);
        Assert.Equal(AppConstants.Definition.MaxNumberBase62, manager.EndPoint);
    }

    [Fact]
    public void Constructor_WithNullFileList_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DefinitionRangeManager(null!));
    }

    #endregion

    #region DetermineProcessingRange Tests - 自動検出

    [Fact]
    public void DetermineProcessingRange_DefEndZero_AutoDetectsMaximum()
    {
        // Arrange
        var fileList = CreateFileList(1, 10, 50, 100);
        var manager = new DefinitionRangeManager(fileList);

        // Act
        manager.DetermineProcessingRange(1, 0);

        // Assert
        Assert.Equal(1, manager.StartPoint);
        Assert.Equal(100, manager.EndPoint);  // 自動検出で最大値100
    }

    [Fact]
    public void DetermineProcessingRange_DefEndNegative_AutoDetectsMaximum()
    {
        // Arrange
        var fileList = CreateFileList(5, 25, 75);
        var manager = new DefinitionRangeManager(fileList);

        // Act
        manager.DetermineProcessingRange(1, -1);

        // Assert
        Assert.Equal(5, manager.StartPoint);  // ファイルリストの最初
        Assert.Equal(75, manager.EndPoint);   // 自動検出で最大値75
    }

    #endregion

    #region DetermineProcessingRange Tests - 明示指定

    [Fact]
    public void DetermineProcessingRange_ExplicitRange_UsesSpecifiedValues()
    {
        // Arrange
        var fileList = CreateFileList(1, 10, 50, 100);
        var manager = new DefinitionRangeManager(fileList);

        // Act
        manager.DetermineProcessingRange(10, 50);

        // Assert
        Assert.Equal(10, manager.StartPoint);
        Assert.Equal(50, manager.EndPoint);
    }

    [Fact]
    public void DetermineProcessingRange_StartGreaterThanFileListStart_UsesLarger()
    {
        // Arrange
        var fileList = CreateFileList(5, 10, 20);
        var manager = new DefinitionRangeManager(fileList);

        // Act
        manager.DetermineProcessingRange(10, 0);

        // Assert
        Assert.Equal(10, manager.StartPoint);  // defStart=10がファイルリストの5より大きい
    }

    [Fact]
    public void DetermineProcessingRange_EndSmallerThanMax_UsesSmaller()
    {
        // Arrange
        var fileList = CreateFileList(1, 50, 100, 200);
        var manager = new DefinitionRangeManager(fileList);

        // Act
        manager.DetermineProcessingRange(1, 150);

        // Assert
        Assert.Equal(150, manager.EndPoint);  // defEnd=150がmaxDefined=200より小さい
    }

    #endregion

    #region DetermineProcessingRange Tests - 境界値補正

    [Fact]
    public void DetermineProcessingRange_StartLessThanMin_CorrectsToMin()
    {
        // Arrange
        var fileList = CreateFileList(1, 10, 50);
        var manager = new DefinitionRangeManager(fileList);

        // Act
        manager.DetermineProcessingRange(-5, 50);

        // Assert
        Assert.Equal(1, manager.StartPoint);  // -5 → 1に補正
    }

    [Fact]
    public void DetermineProcessingRange_EndExceedsMax_CorrectsToMax()
    {
        // Arrange
        var fileList = CreateFileList(1, 10, 3800);
        var manager = new DefinitionRangeManager(fileList);

        // Act
        manager.DetermineProcessingRange(1, 5000);

        // Assert
        // 5000 → MaxDefinitionNumberBase62-1 = 3842に補正、
        // さらにmaxDefined=3800との最小値で3800
        Assert.Equal(3800, manager.EndPoint);
    }

    [Fact]
    public void DetermineProcessingRange_ZeroStart_CorrectsToMin()
    {
        // Arrange
        var fileList = CreateFileList(1, 10);
        var manager = new DefinitionRangeManager(fileList);

        // Act
        manager.DetermineProcessingRange(0, 10);

        // Assert
        Assert.Equal(1, manager.StartPoint);  // 0 → 1に補正
    }

    #endregion

    #region DetermineProcessingRange Tests - エッジケース

    [Fact]
    public void DetermineProcessingRange_EmptyFileList_UsesMinDefaults()
    {
        // Arrange
        var fileList = new List<WavFiles>();
        var manager = new DefinitionRangeManager(fileList);

        // Act
        manager.DetermineProcessingRange(1, 0);

        // Assert
        // 空リストの場合、maxDefined=1(最小値)となる
        Assert.Equal(1, manager.StartPoint);
        Assert.Equal(1, manager.EndPoint);
    }

    [Fact]
    public void DetermineProcessingRange_SingleFile_RangeIsSinglePoint()
    {
        // Arrange
        var fileList = CreateFileList(42);
        var manager = new DefinitionRangeManager(fileList);

        // Act
        manager.DetermineProcessingRange(1, 0);

        // Assert
        Assert.Equal(42, manager.StartPoint);  // ファイルリストの最初かつ唯一
        Assert.Equal(42, manager.EndPoint);    // 自動検出で42
    }

    [Fact]
    public void DetermineProcessingRange_NonSequentialNumbers_FindsCorrectMax()
    {
        // Arrange - 連番でないファイルリスト
        var fileList = CreateFileList(5, 100, 50, 1000, 200);
        var manager = new DefinitionRangeManager(fileList);

        // Act
        manager.DetermineProcessingRange(1, 0);

        // Assert
        Assert.Equal(5, manager.StartPoint);   // ファイルリストの最初
        Assert.Equal(1000, manager.EndPoint);  // 最大値1000を自動検出
    }

    [Fact]
    public void DetermineProcessingRange_LargeRange_HandlesCorrectly()
    {
        // Arrange - 大きな定義番号
        var fileList = CreateFileList(1, 1295, 3843);  // ZZ(36進), zz(62進)
        var manager = new DefinitionRangeManager(fileList);

        // Act
        manager.DetermineProcessingRange(1, 0);

        // Assert
        Assert.Equal(1, manager.StartPoint);
        // 3843はMaxDefinitionNumberBase62と同じだが、EndPointはMax-1=3842で制限される
        // さらにmaxDefinedとの最小値なので3842
        Assert.Equal(3842, manager.EndPoint);
    }

    #endregion

    #region DetermineProcessingRange Tests - 複数回呼び出し

    [Fact]
    public void DetermineProcessingRange_CalledMultipleTimes_UpdatesCorrectly()
    {
        // Arrange
        var fileList = CreateFileList(1, 50, 100);
        var manager = new DefinitionRangeManager(fileList);

        // Act - 1回目
        manager.DetermineProcessingRange(1, 50);
        Assert.Equal(1, manager.StartPoint);
        Assert.Equal(50, manager.EndPoint);

        // Act - 2回目（範囲変更）
        manager.DetermineProcessingRange(20, 0);
        Assert.Equal(20, manager.StartPoint);
        Assert.Equal(100, manager.EndPoint);
    }

    #endregion
}
