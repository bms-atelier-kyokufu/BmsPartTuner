using BmsAtelierKyokufu.BmsPartTuner.Core;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Bms
{
    /// <summary>
    /// <see cref="DefinitionStatistics"/> のテストクラス。
    /// <para>
    /// 【テスト対象】
    /// - ユニークファイル数の計算
    /// - 統計情報の正確性
    /// </para>
    /// </summary>
    public class DefinitionStatisticsTests
    {
        #region Helper Methods

        private static int[] CreateReplaceTable()
        {
            return new int[AppConstants.Definition.ReplaceTableSize];
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            var fileList = BmsTestDefinitionHelper.CreateBmsDefinitionManager(1, 2, 3);
            var replaces = CreateReplaceTable();

            var stats = new DefinitionStatistics(fileList, replaces, 1, 3);

            // 例外が発生しなければ成功
            Assert.NotNull(stats);
        }

        [Fact]
        public void Constructor_WithNullBmsDefinitionManager_ThrowsArgumentNullException()
        {
            var replaces = CreateReplaceTable();

            Assert.Throws<ArgumentNullException>(() =>
                new DefinitionStatistics(null!, replaces, 1, 10));
        }

        [Fact]
        public void Constructor_WithNullReplaces_ThrowsArgumentNullException()
        {
            var fileList = BmsTestDefinitionHelper.CreateBmsDefinitionManager(1, 2, 3);

            Assert.Throws<ArgumentNullException>(() =>
                new DefinitionStatistics(fileList, null!, 1, 10));
        }

        #endregion

        #region GetUniqueFileCount Tests

        public static TheoryData<int[], int[][], int, int, int> GetUniqueFileCountTestData()
        {
            var data = new TheoryData<int[], int[][], int, int, int>();
            data.AddCase(
                fileListNumbers: [1, 2, 3, 4, 5],
                replacesMap: [[1, 1], [2, 2], [3, 3], [4, 4], [5, 5]],
                start: 1,
                end: 5,
                expectedCount: 5
            );
            data.AddCase(
                fileListNumbers: [1, 2, 3, 4, 5],
                replacesMap: [[1, 1], [2, 1], [3, 1], [4, 1], [5, 1]],
                start: 1,
                end: 5,
                expectedCount: 1
            );
            data.AddCase(
                fileListNumbers: [1, 2, 3, 4, 5],
                replacesMap: [[1, 1], [2, 1], [3, 3], [4, 3], [5, 5]],
                start: 1,
                end: 5,
                expectedCount: 3
            );
            data.AddCase(
                fileListNumbers: [1, 2, 3, 4, 5],
                replacesMap: [[1, 1], [2, 1], [3, 0], [4, 4], [5, 0]],
                start: 1,
                end: 5,
                expectedCount: 2
            );
            data.AddCase(
                fileListNumbers: [1, 5, 10, 15, 20],
                replacesMap: [[1, 1], [5, 5], [10, 10], [15, 15], [20, 20]],
                start: 5,
                end: 15,
                expectedCount: 3
            );
            data.AddCase(
                fileListNumbers: [1, 100, 200],
                replacesMap: [[1, 1], [100, 100], [200, 200]],
                start: 50,
                end: 150,
                expectedCount: 1
            );
            data.AddCase(
                fileListNumbers: [],
                replacesMap: [],
                start: 1,
                end: 100,
                expectedCount: 0
            );
            data.AddCase(
                fileListNumbers: [42],
                replacesMap: [[42, 42]],
                start: 1,
                end: 100,
                expectedCount: 1
            );
            data.AddCase(
                fileListNumbers: [1, 2, 3],
                replacesMap: [],
                start: 1,
                end: 3,
                expectedCount: 0
            );
            data.AddCase(
                fileListNumbers: [1, 2, 3, 4, 5],
                replacesMap: [[1, 1], [2, 1], [3, 1], [4, 4], [5, 4]],
                start: 1,
                end: 5,
                expectedCount: 2
            );
            data.AddCase(
                fileListNumbers: [1, 100, 500, 1000, 3000],
                replacesMap: [[1, 1], [100, 100], [500, 1], [1000, 100], [3000, 3000]],
                start: 1,
                end: 3843,
                expectedCount: 3
            );
            return data;
        }

        [Theory]
        [MemberData(nameof(GetUniqueFileCountTestData))]
        public void GetUniqueFileCount_BehaviorTests(int[] fileListNumbers, int[][] replacesMap, int start, int end, int expectedCount)
        {
            var fileList = fileListNumbers.Length == 0
                ? []
                : BmsTestDefinitionHelper.CreateBmsDefinitionManager(fileListNumbers);
            var replaces = CreateReplaceTable();
            foreach (var pair in replacesMap)
            {
                if (pair.Length == 2)
                {
                    replaces[pair[0]] = pair[1];
                }
            }

            var stats = new DefinitionStatistics(fileList, replaces, start, end);
            var uniqueCount = stats.GetUniqueFileCount();

            Assert.Equal(expectedCount, uniqueCount);
        }

        #endregion

        #region LogStatistics Tests

        [Fact]
        public void LogStatistics_DoesNotThrow()
        {
            var fileList = BmsTestDefinitionHelper.CreateBmsDefinitionManager(1, 2, 3);
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
    }
}
