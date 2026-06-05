using System.Collections.Concurrent;
using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Optimization
{
    /// <summary>
    /// <see cref="SimulationEngineEdgeCasesTests"/> の動作を検証するテストクラス。
    /// </summary>
    public class SimulationEngineEdgeCasesTests
    {
        private static IReadOnlyList<SimulationPoint> RunSimulation(
            IEnumerable<(string name, int num, ICachedSoundData? cache)> files,
            int startDef,
            int endDef,
            float threshold)
        {
            var audioCache = new ConcurrentDictionary<string, ICachedSoundData>();
            var fileList = new List<BmsAudioFile>();

            foreach (var (name, num, cache) in files)
            {
                fileList.Add(new BmsAudioFile { Name = name, NumInteger = num });
                if (cache != null)
                {
                    audioCache[name] = cache;
                }
            }

            var engine = new SimulationEngine(fileList, audioCache, startDef, endDef);
            return engine.RunParallelSimulation(threshold, threshold, 0.1f, null);
        }

        #region エッジケーステスト

        /// <summary>
        /// RunParallelSimulation において、条件 NullCache の場合に HandledGracefully されることを検証します。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_NullCache_HandledGracefully()
        {
            var files = new (string, int, ICachedSoundData?)[]
            {
                ("a.wav", 1, null)
            };

            var results = RunSimulation(files, 1, 1, 0.5f);

            Assert.Single(results);
            Assert.Equal(1, results[0].FileCount);
        }

        #endregion

        #region Priority S: Boundary Value Tests (データ破壊防止)

        /// <summary>
        /// しきい値の変更によって削減される数（マージ結果）を検証します。
        /// </summary>
        [Theory]
        [InlineData(0.0f, 1)]    // しきい値0.0：異なる波形でもすべて結合される
        [InlineData(0.99f, 3)]   // しきい値0.99：異なる波形は結合されない
        [InlineData(1.0f, 3)]    // しきい値1.0：異なる波形は結合されない（完全一致のみ結合）
        public void RunParallelSimulation_DifferentThresholds_MergesCorrectly(float threshold, int expectedCount)
        {
            var files = new (string, int, ICachedSoundData?)[]
            {
                ("unique1.wav", 1, BmsTestAudioHelper.CreateDistinctCache(440.0)),
                ("unique2.wav", 2, BmsTestAudioHelper.CreateDistinctCache(880.0)),
                ("unique3.wav", 3, BmsTestAudioHelper.CreateDistinctCache(1320.0))
            };

            var results = RunSimulation(files, 1, 3, threshold);

            Assert.Single(results);
            Assert.Equal(expectedCount, results[0].FileCount);
        }

        /// <summary>
        /// 同一ファイル名や完全一致によるマージ、および単一ファイルケースを検証します。
        /// </summary>
        [Theory]
        [InlineData(new[] { "same.wav", "same.wav", "same.wav" }, 1, 3, 0.5f, 1)] // 同一ファイル名はマージされる
        [InlineData(new[] { "diff1.wav", "diff2.wav" }, 1, 2, 1.0f, 1)]           // 音声データが完全に同じなら異なる名前でも結合される
        [InlineData(new[] { "single.wav" }, 5, 5, 0.5f, 1)]                       // 範囲が単一ファイルの場合
        public void RunParallelSimulation_NameAndExactMatchMerging(string[] fileNames, int start, int end, float threshold, int expectedCount)
        {
            var dummy = BmsTestAudioHelper.CreateDummyCache();
            var files = fileNames.Select((name, i) => (name, start + i, (ICachedSoundData?)dummy));

            var results = RunSimulation(files, start, end, threshold);

            Assert.Single(results);
            Assert.Equal(expectedCount, results[0].FileCount);
        }

        #endregion

        #region Priority S: Edge Case Tests (極端なケース)

        /// <summary>
        /// 極端な音声データ（極小または無音）でも例外なくシミュレーションが完了することを検証します。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_EdgeCaseAudioData_HandlesGracefully()
        {
            var cases = new ICachedSoundData[]
            {
                new MockCachedSoundData([[0.5f]], 44100, 16), // 極端に短い音声データ（1サンプル）
                new MockCachedSoundData([new float[100]], 44100, 16) // 無音データ（すべてゼロ）
            };

            foreach (var audioCacheData in cases)
            {
                var files = new (string, int, ICachedSoundData?)[]
                {
                    ("file1.wav", 1, audioCacheData),
                    ("file2.wav", 2, audioCacheData)
                };

                var results = RunSimulation(files, 1, 2, 0.5f);

                Assert.NotNull(results);
                Assert.NotEmpty(results);
            }
        }

        /// <summary>
        /// 負のインデックスや範囲外の定義番号を持つファイルが適切にフィルタリングまたは無視されることを検証します。
        /// </summary>
        [Theory]
        [InlineData(5, 100, 1, 10, 1)] // 100は範囲(1-10)外のため無視される
        [InlineData(1, -1, 1, 10, 1)]  // -1は負のインデックスのため無視される
        public void RunParallelSimulation_FilterInvalidOrOutOfRangeFiles(
            int num1, int num2, int start, int end, int expectedCount)
        {
            var dummy = BmsTestAudioHelper.CreateDummyCache();
            var files = new (string, int, ICachedSoundData?)[]
            {
                ("file1.wav", num1, dummy),
                ("file2.wav", num2, dummy)
            };

            var results = RunSimulation(files, start, end, 0.5f);

            Assert.Single(results);
            Assert.Equal(expectedCount, results[0].FileCount);
        }

        /// <summary>
        /// ファイル数が極端に多い場合のBase36/Base62境界テスト。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_LargeFileCount_EarlyTerminationWorks()
        {
            var audioCache = new ConcurrentDictionary<string, ICachedSoundData>();
            var fileList = new List<BmsAudioFile>();
            for (int i = 1; i <= 100; i++)
            {
                fileList.Add(new BmsAudioFile
                {
                    Name = $"file{i}.wav",
                    NumInteger = i
                });
            }

            var engine = new SimulationEngine(fileList, audioCache, 1, 100);
            var results = engine.RunParallelSimulation(0.1f, 0.9f, 0.1f, null);

            Assert.NotEmpty(results);
            Assert.True(results.Count < 9); // 早期終了により全シミュレーションを実行しない
        }

        /// <summary>
        /// 空白のファイル名を持つケースのハンドリング。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_EmptyFileName_HandlesGracefully()
        {
            var files = new (string, int, ICachedSoundData?)[]
            {
                ("", 1, null),
                ("", 2, null)
            };

            var exception = Record.Exception(() => RunSimulation(files, 1, 2, 0.5f));

            Assert.Null(exception);
        }

        /// <summary>
        /// 極端に多いファイル数（Base62制限超）でのシミュレーション。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_MoreThanBase62Limit_HandlesCorrectly()
        {
            var audioCache = new ConcurrentDictionary<string, ICachedSoundData>();
            var fileList = new List<BmsAudioFile>();
            for (int i = 1; i <= 3843; i++)
            {
                fileList.Add(new BmsAudioFile
                {
                    Name = $"file{i}.wav",
                    NumInteger = i
                });
            }

            var engine = new SimulationEngine(fileList, audioCache, 1, 3843);
            var results = engine.RunParallelSimulation(0.1f, 0.9f, 0.2f, null);

            Assert.NotEmpty(results);
            Assert.True(results.Count >= 1);
        }

        /// <summary>
        /// 逆順の定義範囲（startPoint > endPoint）が正しくハンドルされるかテスト。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_ReversedRange_HandlesGracefully()
        {
            var files = new (string, int, ICachedSoundData?)[]
            {
                ("a.wav", 5, BmsTestAudioHelper.CreateDummyCache())
            };

            var exception = Record.Exception(() => RunSimulation(files, 10, 1, 0.5f));

            Assert.Null(exception);
        }

        #endregion

        #region Priority S: Progress Reporting Tests

        /// <summary>
        /// 進捗報告が正しく行われることを検証。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_WithProgress_ReportsCorrectly()
        {
            var audioCache = new ConcurrentDictionary<string, ICachedSoundData>();
            var progressValues = new List<int>();
            var progress = new SyncProgress<int>(progressValues.Add);
            var opContext = new BmsAtelierKyokufu.BmsPartTuner.Core.Context.OperationContext(progress);

            var fileList = new List<BmsAudioFile>
            {
                new() { Name = "a.wav", NumInteger = 1}
            };
            var engine = new SimulationEngine(fileList, audioCache, 1, 1);

            engine.RunParallelSimulation(0.1f, 0.5f, 0.1f, opContext);

            Assert.Contains(70, progressValues);
        }

        #endregion

        private class SyncProgress<T>(Action<T> handler) : IProgress<T>
        {
            private readonly Lock _lock = new();
            public void Report(T value)
            {
                lock (_lock)
                {
                    handler(value);
                }
            }
        }
    }
}
