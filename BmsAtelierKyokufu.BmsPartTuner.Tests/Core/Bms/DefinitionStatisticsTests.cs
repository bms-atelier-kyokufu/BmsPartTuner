using BmsAtelierKyokufu.BmsPartTuner.Core;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Models;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Bms;

/// <summary>
/// <see cref="DefinitionStatistics"/> のテストクラス。
/// 
/// 【テスト対象】
/// - ユニークファイル数の計算
/// - 統計情報の正確性
/// 
/// 【テスト設計方針】
/// - 置換テーブルの状態パターン
/// - 境界条件（空リスト、全置換、置換なし）
/// </summary>
public class DefinitionStatisticsTests
{
    #region Helper Methods

    private static BmsAudioFile CreateWavFile(int numInteger)
    {
        return new BmsAudioFile
        {
            NumInteger = numInteger,
            Num = numInteger.ToString("D2"),
            Name = $"test_{numInteger}.wav",
            FileSize = 1000
        };
    }

    private static List<BmsAudioFile> CreateBmsDefinitionManager(params int[] numbers)
    {
        return [.. numbers.Select(CreateWavFile)];
    }

    private static int[] CreateReplaceTable()
    {
        return new int[AppConstants.Definition.ReplaceTableSize];
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        var fileList = CreateBmsDefinitionManager(1, 2, 3);
        var replaces = CreateReplaceTable();

        var stats = new DefinitionStatistics(fileList, replaces, 1, 3);

        // 例外が発生しなければ成功
        Assert.NotNull(stats);
    }

    [Fact]
    public void Constructor_WithNullBmsDefinitionManager_ThrowsArgumentNullException()
    {
        var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        var replaces = CreateReplaceTable();

        Assert.Throws<ArgumentNullException>(() =>
            new DefinitionStatistics(null!, replaces, 1, 10));
    }

    [Fact]
    public void Constructor_WithNullReplaces_ThrowsArgumentNullException()
    {
        var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        var fileList = CreateBmsDefinitionManager(1, 2, 3);

        Assert.Throws<ArgumentNullException>(() =>
            new DefinitionStatistics(fileList, null!, 1, 10));
    }

    #endregion

    #region GetUniqueFileCount Tests - 基本パターン

    [Fact]
    public void GetUniqueFileCount_AllSelfReferencing_ReturnsAllCount()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        // すべてのファイルが自分自身を指している（置換なし）
        var fileList = CreateBmsDefinitionManager(1, 2, 3, 4, 5);
        var replaces = CreateReplaceTable();

        // 各ファイルが自分自身を指す
        replaces[1] = 1;
        replaces[2] = 2;
        replaces[3] = 3;
        replaces[4] = 4;
        replaces[5] = 5;

        var stats = new DefinitionStatistics(fileList, replaces, 1, 5);

        var uniqueCount = stats.GetUniqueFileCount();

        Assert.Equal(5, uniqueCount);
    }

    [Fact]
    public void GetUniqueFileCount_AllReplaced_ReturnsOne()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        // すべてのファイルが1つのファイルに置換されている
        var fileList = CreateBmsDefinitionManager(1, 2, 3, 4, 5);
        var replaces = CreateReplaceTable();

        // すべてが1を指す
        replaces[1] = 1;  // 自分自身（ユニーク）
        replaces[2] = 1;  // 1に置換
        replaces[3] = 1;  // 1に置換
        replaces[4] = 1;  // 1に置換
        replaces[5] = 1;  // 1に置換

        var stats = new DefinitionStatistics(fileList, replaces, 1, 5);

        var uniqueCount = stats.GetUniqueFileCount();

        Assert.Equal(1, uniqueCount);  // 1のみがユニーク
    }

    [Fact]
    public void GetUniqueFileCount_PartiallyReplaced_ReturnsCorrectCount()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        // 一部が置換されている
        var fileList = CreateBmsDefinitionManager(1, 2, 3, 4, 5);
        var replaces = CreateReplaceTable();

        replaces[1] = 1;  // ユニーク
        replaces[2] = 1;  // 1に置換
        replaces[3] = 3;  // ユニーク
        replaces[4] = 3;  // 3に置換
        replaces[5] = 5;  // ユニーク

        var stats = new DefinitionStatistics(fileList, replaces, 1, 5);

        var uniqueCount = stats.GetUniqueFileCount();

        Assert.Equal(3, uniqueCount);  // 1, 3, 5がユニーク
    }

    [Fact]
    public void GetUniqueFileCount_NotProcessed_ExcludesFromCount()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        // 一部が未処理（replaces[i] == 0）
        var fileList = CreateBmsDefinitionManager(1, 2, 3, 4, 5);
        var replaces = CreateReplaceTable();

        replaces[1] = 1;  // ユニーク
        replaces[2] = 1;  // 置換
        replaces[3] = 0;  // 未処理
        replaces[4] = 4;  // ユニーク
        replaces[5] = 0;  // 未処理

        var stats = new DefinitionStatistics(fileList, replaces, 1, 5);

        var uniqueCount = stats.GetUniqueFileCount();

        Assert.Equal(2, uniqueCount);  // 1, 4のみがユニーク
    }

    #endregion

    #region GetUniqueFileCount Tests - 範囲指定

    [Fact]
    public void GetUniqueFileCount_RangeExcludesSomeFiles_CountsOnlyInRange()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        var fileList = CreateBmsDefinitionManager(1, 5, 10, 15, 20);
        var replaces = CreateReplaceTable();

        replaces[1] = 1;
        replaces[5] = 5;
        replaces[10] = 10;
        replaces[15] = 15;
        replaces[20] = 20;

        // 範囲: 5-15（1と20は範囲外）
        var stats = new DefinitionStatistics(fileList, replaces, 5, 15);

        var uniqueCount = stats.GetUniqueFileCount();

        Assert.Equal(3, uniqueCount);  // 5, 10, 15のみ
    }

    [Fact]
    public void GetUniqueFileCount_FileOutsideRange_NotCounted()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        var fileList = CreateBmsDefinitionManager(1, 100, 200);
        var replaces = CreateReplaceTable();

        replaces[1] = 1;
        replaces[100] = 100;
        replaces[200] = 200;

        // 範囲: 50-150（1と200は範囲外）
        var stats = new DefinitionStatistics(fileList, replaces, 50, 150);

        var uniqueCount = stats.GetUniqueFileCount();

        Assert.Equal(1, uniqueCount);  // 100のみ
    }

    #endregion

    #region GetUniqueFileCount Tests - エッジケース

    [Fact]
    public void GetUniqueFileCount_EmptyBmsDefinitionManager_ReturnsZero()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        var fileList = new List<BmsAudioFile>();
        var replaces = CreateReplaceTable();

        var stats = new DefinitionStatistics(fileList, replaces, 1, 100);

        var uniqueCount = stats.GetUniqueFileCount();

        Assert.Equal(0, uniqueCount);
    }

    [Fact]
    public void GetUniqueFileCount_SingleFile_ReturnsOne()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        var fileList = CreateBmsDefinitionManager(42);
        var replaces = CreateReplaceTable();
        replaces[42] = 42;

        var stats = new DefinitionStatistics(fileList, replaces, 1, 100);

        var uniqueCount = stats.GetUniqueFileCount();

        Assert.Equal(1, uniqueCount);
    }

    [Fact]
    public void GetUniqueFileCount_AllZeroReplaces_ReturnsZero()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        // すべて未処理
        var fileList = CreateBmsDefinitionManager(1, 2, 3);
        var replaces = CreateReplaceTable();
        // replaces配列は初期値0のまま

        var stats = new DefinitionStatistics(fileList, replaces, 1, 3);

        var uniqueCount = stats.GetUniqueFileCount();

        Assert.Equal(0, uniqueCount);  // すべて未処理なのでユニークなし
    }

    #endregion

    #region LogStatistics Tests

    [Fact]
    public void LogStatistics_DoesNotThrow()
    {
        var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        var fileList = CreateBmsDefinitionManager(1, 2, 3);
        var replaces = CreateReplaceTable();
        replaces[1] = 1;
        replaces[2] = 1;
        replaces[3] = 3;

        var stats = new DefinitionStatistics(fileList, replaces, 1, 3);

        // 例外が発生しなければ成功
        var exception = Record.Exception(() => stats.LogStatistics());
        Assert.Null(exception);
    }

    #endregion

    #region Complex Scenarios - 複雑なシナリオ

    [Fact]
    public void GetUniqueFileCount_ChainReplacement_CountsCorrectly()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        // 連鎖的な置換（2→1, 3→1）
        var fileList = CreateBmsDefinitionManager(1, 2, 3, 4, 5);
        var replaces = CreateReplaceTable();

        replaces[1] = 1;  // ユニーク（グループ1の代表）
        replaces[2] = 1;  // グループ1
        replaces[3] = 1;  // グループ1
        replaces[4] = 4;  // ユニーク（グループ2の代表）
        replaces[5] = 4;  // グループ2

        var stats = new DefinitionStatistics(fileList, replaces, 1, 5);

        var uniqueCount = stats.GetUniqueFileCount();

        Assert.Equal(2, uniqueCount);  // グループ1と2の代表のみ
    }

    [Fact]
    public void GetUniqueFileCount_SparseNumbers_CountsCorrectly()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        // まばらな定義番号
        var fileList = CreateBmsDefinitionManager(1, 100, 500, 1000, 3000);
        var replaces = CreateReplaceTable();

        replaces[1] = 1;
        replaces[100] = 100;
        replaces[500] = 1;      // 1に置換
        replaces[1000] = 100;   // 100に置換
        replaces[3000] = 3000;

        var stats = new DefinitionStatistics(fileList, replaces, 1, 3843);

        var uniqueCount = stats.GetUniqueFileCount();

        Assert.Equal(3, uniqueCount);  // 1, 100, 3000
    }

    #endregion
}
