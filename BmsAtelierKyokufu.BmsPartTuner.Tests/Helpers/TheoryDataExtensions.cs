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

        /// <summary>
        /// タプル形式（2要素）でのコレクション初期化子による追加をサポートします。
        /// </summary>
        public static void Add<T1, T2>(
            this TheoryData<T1, T2> data,
            (T1, T2) item)
        {
            data.Add(item.Item1, item.Item2);
        }

        /// <summary>
        /// タプル形式（3要素）でのコレクション初期化子による追加をサポートします。
        /// </summary>
        public static void Add<T1, T2, T3>(
            this TheoryData<T1, T2, T3> data,
            (T1, T2, T3) item)
        {
            data.Add(item.Item1, item.Item2, item.Item3);
        }

        /// <summary>
        /// タプル形式（4要素）でのコレクション初期化子による追加をサポートします。
        /// </summary>
        public static void Add<T1, T2, T3, T4>(
            this TheoryData<T1, T2, T3, T4> data,
            (T1, T2, T3, T4) item)
        {
            data.Add(item.Item1, item.Item2, item.Item3, item.Item4);
        }

        /// <summary>
        /// タプル形式（5要素）でのコレクション初期化子による追加をサポートします。
        /// </summary>
        public static void Add<T1, T2, T3, T4, T5>(
            this TheoryData<T1, T2, T3, T4, T5> data,
            (T1, T2, T3, T4, T5) item)
        {
            data.Add(item.Item1, item.Item2, item.Item3, item.Item4, item.Item5);
        }
    }
}
