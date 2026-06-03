using BmsAtelierKyokufu.BmsPartTuner.Core;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Bms
{
    /// <summary>
    /// <see cref="DefinitionRangeManager"/> のテストクラス。
    /// <para>
    /// 【テスト対象】
    /// - 処理範囲の決定（自動検出、明示指定）
    /// - 範囲の妥当性検証
    /// - 境界値処理
    /// </para>
    /// </summary>
    /// <summary>
    /// <see cref="DefinitionRangeManagerTests"/> の動作を検証するテストクラス。
    /// </summary>
    public class DefinitionRangeManagerTests
    {
        #region Constructor Tests

        /// <summary>
        /// Constructor において、条件 WithValidBmsDefinitionManager の場合に InitializesCorrectly されることを検証します。
        /// </summary>
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

        /// <summary>
        /// Constructor において、条件 WithEmptyBmsDefinitionManager の場合に InitializesWithDefaults されることを検証します。
        /// </summary>
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

        /// <summary>
        /// Constructor において、条件 WithNullBmsDefinitionManager の場合に ThrowsArgumentNullException されることを検証します。
        /// </summary>
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

            // Case 1: 終了定義が0（自動検出）の場合、リストの最大値（100）が終了地点となる。
            data.AddCase(
                fileListNumbers: [1, 10, 50, 100],
                defStart: 1,
                defEnd: 0,
                expectedStart: 1,
                expectedEnd: 100
            );

            // Case 2: 終了定義が負の値（自動検出）かつ、開始定義(1)がリストの先頭(5)未満の場合、開始地点はリスト先頭(5)、終了地点は最大値(75)となる。
            data.AddCase(
                fileListNumbers: [5, 25, 75],
                defStart: 1,
                defEnd: -1,
                expectedStart: 5,
                expectedEnd: 75
            );

            // Case 3: 開始・終了定義が明示的に指定され、リストの範囲内である場合、指定通りの範囲となる。
            data.AddCase(
                fileListNumbers: [1, 10, 50, 100],
                defStart: 10,
                defEnd: 50,
                expectedStart: 10,
                expectedEnd: 50
            );

            // Case 4: 開始定義がリストの先頭より大きく、終了定義が0（自動検出）の場合、開始地点は指定値(10)、終了地点はリストの最大値(20)となる。
            data.AddCase(
                fileListNumbers: [5, 10, 20],
                defStart: 10,
                defEnd: 0,
                expectedStart: 10,
                expectedEnd: 20
            );

            // Case 5: 終了定義が明示指定され、リストの最大値(200)未満である場合、指定通りの150が終了地点となる。
            data.AddCase(
                fileListNumbers: [1, 50, 100, 200],
                defStart: 1,
                defEnd: 150,
                expectedStart: 1,
                expectedEnd: 150
            );

            // Case 6: 開始定義が下限（1未満）に設定されている場合、開始地点は最小値である1に補正される。
            data.AddCase(
                fileListNumbers: [1, 10, 50],
                defStart: -5,
                defEnd: 50,
                expectedStart: 1,
                expectedEnd: 50
            );

            // Case 7: 終了定義が上限(3842)を超える場合、一旦3842に補正されるが、リストの最大値(3800)との最小値をとるため、最終的な終了地点は最大値(3800)となる。
            data.AddCase(
                fileListNumbers: [1, 10, 3800],
                defStart: 1,
                defEnd: 5000,
                expectedStart: 1,
                expectedEnd: 3800
            );

            // Case 8: 開始定義が0の場合、1に補正され、開始地点は1となる。
            data.AddCase(
                fileListNumbers: [1, 10],
                defStart: 0,
                defEnd: 10,
                expectedStart: 1,
                expectedEnd: 10
            );

            // Case 9: ファイルリストが空の場合、開始・終了地点ともにデフォルトの1となる。
            data.AddCase(
                fileListNumbers: [],
                defStart: 1,
                defEnd: 0,
                expectedStart: 1,
                expectedEnd: 1
            );

            // Case 10: ファイルリストが単一要素(42)かつ終了定義が0の場合、開始・終了地点ともにその要素の値(42)となる。
            data.AddCase(
                fileListNumbers: [42],
                defStart: 1,
                defEnd: 0,
                expectedStart: 42,
                expectedEnd: 42
            );

            // Case 11: リストが未ソートの場合、開始地点は最初の要素(5)、終了地点はリスト内の最大値(1000)となる。
            data.AddCase(
                fileListNumbers: [5, 100, 50, 1000, 200],
                defStart: 1,
                defEnd: 0,
                expectedStart: 5,
                expectedEnd: 1000
            );

            // Case 12: リストの最大値が3843（Base62上限）の場合、補正ロジックにより終了地点は上限値である3842となる。
            data.AddCase(
                fileListNumbers: [1, 1295, 3843],
                defStart: 1,
                defEnd: 0,
                expectedStart: 1,
                expectedEnd: 3842
            );

            return data;
        }

        /// <summary>
        /// DetermineProcessingRange において BehaviorTests の場合の挙動を検証します。
        /// </summary>
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

        /// <summary>
        /// DetermineProcessingRange において、条件 CalledMultipleTimes の場合に UpdatesCorrectly されることを検証します。
        /// </summary>
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
