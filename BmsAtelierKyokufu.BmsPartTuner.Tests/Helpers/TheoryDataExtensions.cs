namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers
{
    /// <summary>
    /// xUnit の <see cref="TheoryData"/> に対する拡張メソッド群。
    /// 型安全かつ名前付き引数による自己記述的なテストデータ追加（AddCase）を実現します。
    /// </summary>
    public static class TheoryDataExtensions
    {
        /// <summary>
        /// DefinitionStatisticsTests 用のテストケースを追加。
        /// </summary>
        public static void AddCase(
            this TheoryData<int[], int[][], int, int, int> data,
            int[] fileListNumbers,
            int[][] replacesMap,
            int start,
            int end,
            int expectedCount)
        {
            data.Add(fileListNumbers, replacesMap, start, end, expectedCount);
        }

        /// <summary>
        /// DefinitionRangeManagerTests 用のテストケースを追加。
        /// </summary>
        public static void AddCase(
            this TheoryData<int[], int, int, int, int> data,
            int[] fileListNumbers,
            int defStart,
            int defEnd,
            int expectedStart,
            int expectedEnd)
        {
            data.Add(fileListNumbers, defStart, defEnd, expectedStart, expectedEnd);
        }

        /// <summary>
        /// WaveValidationTests 用のテストケースを追加。
        /// </summary>
        public static void AddCase(
            this TheoryData<float[], float[], float, float> data,
            float[] wav1,
            float[] wav2,
            float minExpected,
            float maxExpected)
        {
            data.Add(wav1, wav2, minExpected, maxExpected);
        }
    }
}
