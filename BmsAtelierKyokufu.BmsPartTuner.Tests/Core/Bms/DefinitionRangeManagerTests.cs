using BmsAtelierKyokufu.BmsPartTuner.Core;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Bms
{
    /// <summary>
    /// <see cref="DefinitionRangeManager"/> のテストクラス。
    /// 
    /// 【テスト対象】
    /// - 処理範囲の決定（自動検出、明示指定）
    /// - 範囲の妥当性検証
    /// - 境界値処理
    /// </summary>
    public class DefinitionRangeManagerTests
    {
        #region Helper Methods

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidBmsDefinitionManager_InitializesCorrectly()
        {
            // Arrange
            var fileList = BmsTestDefinitionHelper.CreateBmsDefinitionManager(1, 10, 50);

            // Act
            var manager = new DefinitionRangeManager(fileList);

            // Assert
            Assert.Equal(AppConstants.Definition.MinNumber, manager.StartPoint);
            Assert.Equal(AppConstants.Definition.MaxNumberBase62, manager.EndPoint);
        }

        [Fact]
        public void Constructor_WithEmptyBmsDefinitionManager_InitializesWithDefaults()
        {
            // Arrange
            var fileList = new List<BmsAudioFile>();

            // Act
            var manager = new DefinitionRangeManager(fileList);

            // Assert
            Assert.Equal(AppConstants.Definition.MinNumber, manager.StartPoint);
            Assert.Equal(AppConstants.Definition.MaxNumberBase62, manager.EndPoint);
        }

        [Fact]
        public void Constructor_WithNullBmsDefinitionManager_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DefinitionRangeManager(null!));
        }

        #endregion

        #region DetermineProcessingRange Tests

        public static TheoryData<int[], int, int, int, int> GetDetermineProcessingRangeTestData()
        {
            var data = new TheoryData<int[], int, int, int, int>();
            data.AddCase(
                fileListNumbers: [1, 10, 50, 100],
                defStart: 1,
                defEnd: 0,
                expectedStart: 1,
                expectedEnd: 100
            );
            data.AddCase(
                fileListNumbers: [5, 25, 75],
                defStart: 1,
                defEnd: -1,
                expectedStart: 5,
                expectedEnd: 75
            );
            data.AddCase(
                fileListNumbers: [1, 10, 50, 100],
                defStart: 10,
                defEnd: 50,
                expectedStart: 10,
                expectedEnd: 50
            );
            data.AddCase(
                fileListNumbers: [5, 10, 20],
                defStart: 10,
                defEnd: 0,
                expectedStart: 10,
                expectedEnd: 20
            );
            data.AddCase(
                fileListNumbers: [1, 50, 100, 200],
                defStart: 1,
                defEnd: 150,
                expectedStart: 1,
                expectedEnd: 150
            );
            data.AddCase(
                fileListNumbers: [1, 10, 50],
                defStart: -5,
                defEnd: 50,
                expectedStart: 1,
                expectedEnd: 50
            );
            data.AddCase(
                fileListNumbers: [1, 10, 3800],
                defStart: 1,
                defEnd: 5000,
                expectedStart: 1,
                expectedEnd: 3800
            );
            data.AddCase(
                fileListNumbers: [1, 10],
                defStart: 0,
                defEnd: 10,
                expectedStart: 1,
                expectedEnd: 10
            );
            data.AddCase(
                fileListNumbers: [],
                defStart: 1,
                defEnd: 0,
                expectedStart: 1,
                expectedEnd: 1
            );
            data.AddCase(
                fileListNumbers: [42],
                defStart: 1,
                defEnd: 0,
                expectedStart: 42,
                expectedEnd: 42
            );
            data.AddCase(
                fileListNumbers: [5, 100, 50, 1000, 200],
                defStart: 1,
                defEnd: 0,
                expectedStart: 5,
                expectedEnd: 1000
            );
            data.AddCase(
                fileListNumbers: [1, 1295, 3843],
                defStart: 1,
                defEnd: 0,
                expectedStart: 1,
                expectedEnd: 3842
            );
            return data;
        }

        [Theory]
        [MemberData(nameof(GetDetermineProcessingRangeTestData))]
        public void DetermineProcessingRange_BehaviorTests(int[] fileListNumbers, int defStart, int defEnd, int expectedStart, int expectedEnd)
        {
            // Arrange
            var fileList = fileListNumbers.Length == 0
                ? []
                : BmsTestDefinitionHelper.CreateBmsDefinitionManager(fileListNumbers);
            var manager = new DefinitionRangeManager(fileList);

            // Act
            manager.DetermineProcessingRange(defStart, defEnd);

            // Assert
            Assert.Equal(expectedStart, manager.StartPoint);
            Assert.Equal(expectedEnd, manager.EndPoint);
        }

        #endregion

        #region DetermineProcessingRange Tests - 複数回呼び出し

        [Fact]
        public void DetermineProcessingRange_CalledMultipleTimes_UpdatesCorrectly()
        {
            // Arrange
            var fileList = BmsTestDefinitionHelper.CreateBmsDefinitionManager(1, 50, 100);
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
}
