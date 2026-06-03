using System.Collections.Concurrent;
using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Optimization
{
    /// <summary>
    /// SimulationEngine の動作検証テスト。
    /// 並列シミュレーション全体の統合仕様、早期終了などを検証します。
    /// </summary>
    /// <summary>
    /// <see cref="SimulationEngineTests"/> の動作を検証するテストクラス。
    /// </summary>
    public class SimulationEngineTests
    {
        #region 統合テスト

        /// <summary>
        /// RunParallelSimulation において、条件 EmptyList の場合に ReturnsEmptyResults されることを検証します。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_EmptyList_ReturnsEmptyResults()
        {
            var audioCache = new ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            // 準備
            var fileList = new List<BmsAudioFile>();
            var engine = new SimulationEngine(fileList, audioCache, 1, 10);

            // 実行
            var results = engine.RunParallelSimulation(0.5f, 0.9f, 0.1f, null);

            // 検証
            Assert.NotNull(results);
            // 空リストでもシミュレーションは実行されるが、ファイル数は0
            Assert.All(results, r => Assert.Equal(0, r.FileCount));
        }

        /// <summary>
        /// RunParallelSimulation において、条件 SingleFile の場合に ReturnsCountOne されることを検証します。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_SingleFile_ReturnsCountOne()
        {
            var engine = new TestEngineBuilder()
                .AddFile("a.wav", 1, BmsTestAudioHelper.CreateDummyCache())
                .Build(1, 1);

            // 実行
            var results = engine.RunParallelSimulation(0.5f, 0.5f, 0.1f, null);

            // 検証
            Assert.Single(results);
            Assert.Equal(1, results[0].FileCount);
        }

        /// <summary>
        /// RunParallelSimulation において、条件 TwoDifferentFiles の場合に ReturnsCountTwo されることを検証します。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_TwoDifferentFiles_ReturnsCountTwo()
        {
            var engine = new TestEngineBuilder()
                .AddFile("a.wav", 1, BmsTestAudioHelper.CreateDistinctCache(440.0))
                .AddFile("b.wav", 2, BmsTestAudioHelper.CreateDistinctCache(880.0))
                .Build(1, 2);

            // 実行: 厳密なししきい値（0.99）で異なるファイルは統合されないことを確認
            var results = engine.RunParallelSimulation(0.99f, 0.99f, 0.1f, null);

            // 検証
            Assert.Single(results);
            Assert.Equal(2, results[0].FileCount); // 異なる名前・異なる波形なので統合されない
        }

        /// <summary>
        /// RunParallelSimulation において、条件 TwoIdenticalNames の場合に MergesCorrectly されることを検証します。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_TwoIdenticalNames_MergesCorrectly()
        {
            var engine = new TestEngineBuilder()
                .AddFile("a.wav", 1, BmsTestAudioHelper.CreateDummyCache())
                .AddFile("a.wav", 2, BmsTestAudioHelper.CreateDummyCache())
                .Build(1, 2);

            // 実行
            var results = engine.RunParallelSimulation(0.5f, 0.5f, 0.1f, null);

            // 検証: 決定論的なアサーション
            Assert.Single(results);
            // CachedDataがある場合、名前ベースのグループ化が行われるため、
            // 同じ名前のファイルは1つにマージされる
            Assert.Equal(1, results[0].FileCount);
        }

        #endregion

        #region 早期終了テスト

        /// <summary>
        /// RunParallelSimulation において、条件 Base36ConditionMet の場合に TerminatesEarly されることを検証します。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_Base36ConditionMet_TerminatesEarly()
        {
            var engine = new TestEngineBuilder()
                .AddFile("a.wav", 1)
                .AddFile("a.wav", 2)
                .AddFile("a.wav", 3)
                .Build(1, 3);

            // 実行
            var results = engine.RunParallelSimulation(0.1f, 0.5f, 0.1f, null);

            // 検証
            // 早期終了するため、1つの閾値のみ実行される
            Assert.Single(results);
            Assert.Equal(0.5f, results[0].Threshold);
        }

        #endregion

        private class TestEngineBuilder
        {
            public ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData> Cache { get; } = new();
            public List<BmsAudioFile> Files { get; } = [];

            public TestEngineBuilder AddFile(string name, int num, ICachedSoundData? data = null)
            {
                Files.Add(new BmsAudioFile { Name = name, NumInteger = num });
                if (data != null)
                {
                    Cache[name] = data;
                }
                return this;
            }

            public TestEngineBuilder AddDummyFile(string name, int num)
            {
                return AddFile(name, num, BmsTestAudioHelper.CreateDummyCache());
            }

            public SimulationEngine Build(int start, int end)
            {
                return new SimulationEngine(Files, Cache, start, end);
            }
        }
    }
}
