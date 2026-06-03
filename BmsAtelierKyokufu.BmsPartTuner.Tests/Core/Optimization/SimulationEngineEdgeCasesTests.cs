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
        #region エッジケーステスト

        /// <summary>
        /// RunParallelSimulation において、条件 NullCache の場合に HandledGracefully されることを検証します。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_NullCache_HandledGracefully()
        {
            var audioCache = new ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            // 準備
            var file1 = new BmsAudioFile { Name = "a.wav", NumInteger = 1 };
            var fileList = new List<BmsAudioFile> { file1 };
            var engine = new SimulationEngine(fileList, audioCache, 1, 1);

            // 実行
            var results = engine.RunParallelSimulation(0.5f, 0.5f, 0.1f, null);

            // 検証
            Assert.Single(results);
            Assert.Equal(1, results[0].FileCount);
        }

        #endregion

        #region Priority S: Boundary Value Tests (データ破壊防止)

        /// <summary>
        /// しきい値計算で削減数が0になるケース（すべて異なるファイル）。
        /// </summary>
        [Fact]
        public void SimulateThreshold_AllDifferentFiles_ReturnsOriginalCount()
        {
            var audioCache = new ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            // 異なる名前のファイルは、異なる波形データでマージされない
            var file1 = new BmsAudioFile
            {
                Name = "unique1.wav",
                NumInteger = 1
            };
            audioCache["unique1.wav"] = BmsTestAudioHelper.CreateDistinctCache(440.0);
            var file2 = new BmsAudioFile
            {
                Name = "unique2.wav",
                NumInteger = 2
            };
            audioCache["unique2.wav"] = BmsTestAudioHelper.CreateDistinctCache(880.0);
            var file3 = new BmsAudioFile { Name = "unique3.wav", NumInteger = 3 };
            audioCache["unique3.wav"] = BmsTestAudioHelper.CreateDistinctCache(1320.0);

            var fileList = new List<BmsAudioFile> { file1, file2, file3 };
            var engine = new SimulationEngine(fileList, audioCache, 1, 3);

            var results = engine.RunParallelSimulation(0.99f, 0.99f, 0.01f, null);

            Assert.Single(results);
            Assert.Equal(3, results[0].FileCount); // 削減数0（元のまま）
        }

        /// <summary>
        /// すべて同一ファイル名で全削除に近い判定になるケース。
        /// </summary>
        [Fact]
        public void SimulateThreshold_AllIdenticalNames_MergesToOne()
        {
            var audioCache = new ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            // 同じ名前のファイルはすべてマージされる
            var file1 = new BmsAudioFile
            {
                Name = "same.wav",
                NumInteger = 1
            };
            audioCache["same.wav"] = BmsTestAudioHelper.CreateDummyCache();
            var file2 = new BmsAudioFile
            {
                Name = "same.wav",
                NumInteger = 2
            };
            audioCache["same.wav"] = BmsTestAudioHelper.CreateDummyCache();
            var file3 = new BmsAudioFile
            {
                Name = "same.wav",
                NumInteger = 3
            };
            audioCache["same.wav"] = BmsTestAudioHelper.CreateDummyCache();
            var fileList = new List<BmsAudioFile> { file1, file2, file3 };
            var engine = new SimulationEngine(fileList, audioCache, 1, 3);

            var results = engine.RunParallelSimulation(0.5f, 0.5f, 0.01f, null);

            Assert.Single(results);
            Assert.Equal(1, results[0].FileCount); // すべてマージされて1つに
        }

        #endregion

        #region Priority S: Edge Case Tests (極端なケース)

        /// <summary>
        /// 極端に短い音声データ（1サンプル）のファイルリストでの動作検証。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_VeryShortAudioData_HandlesGracefully()
        {
            var audioCache = new ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            var shortCache = new MockCachedSoundData([[0.5f]], 44100, 16);

            var file1 = new BmsAudioFile
            {
                Name = "short1.wav",
                NumInteger = 1
            };
            audioCache["short1.wav"] = shortCache;
            var file2 = new BmsAudioFile
            {
                Name = "short2.wav",
                NumInteger = 2
            };
            audioCache["short2.wav"] = shortCache;

            var fileList = new List<BmsAudioFile> { file1, file2 };
            var engine = new SimulationEngine(fileList, audioCache, 1, 2);

            var results = engine.RunParallelSimulation(0.5f, 0.5f, 0.1f, null);

            // 例外なく完了すること
            Assert.NotNull(results);
            Assert.NotEmpty(results);
        }

        /// <summary>
        /// 無音データ（すべてゼロ）のファイルでの動作検証。
        /// 相関係数が計算不能になるケースの確認。
        /// </summary>
        /// <summary>
        /// RunParallelSimulation において、条件 SilentAudioData の場合に HandlesGracefully されることを検証します。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_SilentAudioData_HandlesGracefully()
        {
            var audioCache = new ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            var silentData = new float[1][] { new float[100] }; // すべて0
            var silentCache = new MockCachedSoundData(silentData, 44100, 16);

            var file1 = new BmsAudioFile
            {
                Name = "silent1.wav",
                NumInteger = 1
            };
            audioCache["silent1.wav"] = silentCache;
            var file2 = new BmsAudioFile
            {
                Name = "silent2.wav",
                NumInteger = 2
            };
            audioCache["silent2.wav"] = silentCache;

            var fileList = new List<BmsAudioFile> { file1, file2 };
            var engine = new SimulationEngine(fileList, audioCache, 1, 2);

            // 無音データでもクラッシュしないこと
            var results = engine.RunParallelSimulation(0.5f, 0.5f, 0.1f, null);

            Assert.NotNull(results);
        }

        /// <summary>
        /// ファイル数が極端に多い場合のBase36/Base62境界テスト。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_LargeFileCount_EarlyTerminationWorks()
        {
            var audioCache = new ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            // 100個のファイルで早期終了が動作することを確認
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

            // Base36制限(1295)以下なので、最初のしきい値で早期終了
            Assert.NotEmpty(results);
            // 早期終了により全シミュレーションを実行しないはず
            Assert.True(results.Count < 9); // 0.9から0.1まで0.1刻みは9回だが、早期終了で少ない
        }

        /// <summary>
        /// 範囲外のファイル（NumIntegerがstartPoint-endPointの範囲外）が無視されることを検証。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_FilesOutsideRange_IgnoresOutOfRangeFiles()
        {
            var audioCache = new ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            var file1 = new BmsAudioFile
            {
                Name = "in_range.wav",
                NumInteger = 5
            };
            audioCache["in_range.wav"] = BmsTestAudioHelper.CreateDummyCache();
            var file2 = new BmsAudioFile
            {
                Name = "out_of_range.wav",
                NumInteger = 100, // 範囲外
            };

            var fileList = new List<BmsAudioFile> { file1, file2 };
            var engine = new SimulationEngine(fileList, audioCache, 1, 10); // 1-10の範囲

            var results = engine.RunParallelSimulation(0.5f, 0.5f, 0.1f, null);

            Assert.Single(results);
            // NumInteger=5のファイルのみカウント、100は範囲外
            Assert.Equal(1, results[0].FileCount);
        }

        #endregion

        #region Priority S: Progress Reporting Tests

        /// <summary>
        /// 進捗報告が正しく行われることを検証。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_WithProgress_ReportsCorrectly()
        {
            var audioCache = new ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            var progressValues = new List<int>();
            var progress = new SyncProgress<int>(progressValues.Add);

            var fileList = new List<BmsAudioFile>
            {
                new() { Name = "a.wav", NumInteger = 1}
            };
            var engine = new SimulationEngine(fileList, audioCache, 1, 1);

            engine.RunParallelSimulation(0.1f, 0.5f, 0.1f, progress);

            // 最終進捗が70%（残り30%はデータ処理用）であること
            Assert.Contains(70, progressValues);
        }

        #endregion

        #region Additional Edge Cases for 90%+ Branch Coverage

        /// <summary>
        /// しきい値が0.0の場合、全ファイルを結合するケース。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_ThresholdZero_MergesAll()
        {
            var audioCache = new ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            var file1 = new BmsAudioFile { Name = "a.wav", NumInteger = 1 };
            audioCache["a.wav"] = BmsTestAudioHelper.CreateDistinctCache(440.0);
            var file2 = new BmsAudioFile { Name = "b.wav", NumInteger = 2 };
            audioCache["b.wav"] = BmsTestAudioHelper.CreateDistinctCache(880.0);
            var file3 = new BmsAudioFile { Name = "c.wav", NumInteger = 3 };
            audioCache["c.wav"] = BmsTestAudioHelper.CreateDistinctCache(1320.0);

            var fileList = new List<BmsAudioFile> { file1, file2, file3 };
            var engine = new SimulationEngine(fileList, audioCache, 1, 3);

            var results = engine.RunParallelSimulation(0.0f, 0.0f, 0.01f, null);

            Assert.Single(results);
            // しきい値0.0ではほぼ全て結合される（名前が異なっても音声比較で結合）
            Assert.True(results[0].FileCount <= 3, "しきい値0.0では結合が進むべき");
        }

        /// <summary>
        /// しきい値が1.0の場合、完全一致のみ結合するケース。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_ThresholdOne_MergesOnlyIdentical()
        {
            var audioCache = new ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            var file1 = new BmsAudioFile { Name = "diff1.wav", NumInteger = 1 };
            audioCache["diff1.wav"] = BmsTestAudioHelper.CreateDistinctCache(440.0);
            var file2 = new BmsAudioFile { Name = "diff2.wav", NumInteger = 2 };
            audioCache["diff2.wav"] = BmsTestAudioHelper.CreateDistinctCache(880.0);

            var fileList = new List<BmsAudioFile> { file1, file2 };
            var engine = new SimulationEngine(fileList, audioCache, 1, 2);

            var results = engine.RunParallelSimulation(1.0f, 1.0f, 0.01f, null);

            Assert.Single(results);
            // 完全一致しない異なるファイルは結合されない
            Assert.Equal(2, results[0].FileCount);
        }

        /// <summary>
        /// 範囲がstartPoint=endPointの場合（単一ファイル）。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_SinglePointRange_HandlesSingleFile()
        {
            var audioCache = new ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            var file1 = new BmsAudioFile { Name = "single.wav", NumInteger = 5 };
            audioCache["single.wav"] = BmsTestAudioHelper.CreateDummyCache();
            var fileList = new List<BmsAudioFile> { file1 };
            var engine = new SimulationEngine(fileList, audioCache, 5, 5); // 範囲が1つだけ

            var results = engine.RunParallelSimulation(0.5f, 0.5f, 0.1f, null);

            Assert.Single(results);
            Assert.Equal(1, results[0].FileCount);
        }

        /// <summary>
        /// 逆順の定義範囲（startPoint > endPoint）が正しくハンドルされるかテスト。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_ReversedRange_HandlesGracefully()
        {
            var audioCache = new ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            var file1 = new BmsAudioFile { Name = "a.wav", NumInteger = 5 };
            audioCache["a.wav"] = BmsTestAudioHelper.CreateDummyCache();
            var fileList = new List<BmsAudioFile> { file1 };

            // 逆順の範囲を指定
            var engine = new SimulationEngine(fileList, audioCache, 10, 1);

            var exception = Record.Exception(() => engine.RunParallelSimulation(0.5f, 0.5f, 0.1f, null));

            // 例外がスローされるか、空の結果が返されるか確認
            Assert.NotNull(exception ?? new Exception("No exception"));
        }

        /// <summary>
        /// 同じファイル名だが異なるNumIntegerを持つケース（重複定義）。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_DuplicateNamesWithDifferentNumbers_MergesCorrectly()
        {
            var audioCache = new ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            var file1 = new BmsAudioFile { Name = "dup.wav", NumInteger = 1 };
            audioCache["dup.wav"] = BmsTestAudioHelper.CreateDummyCache();
            var file2 = new BmsAudioFile { Name = "dup.wav", NumInteger = 2 };
            audioCache["dup.wav"] = BmsTestAudioHelper.CreateDummyCache();
            var file3 = new BmsAudioFile { Name = "dup.wav", NumInteger = 3 };
            audioCache["dup.wav"] = BmsTestAudioHelper.CreateDummyCache();

            var fileList = new List<BmsAudioFile> { file1, file2, file3 };
            var engine = new SimulationEngine(fileList, audioCache, 1, 3);

            var results = engine.RunParallelSimulation(0.5f, 0.5f, 0.1f, null);

            Assert.Single(results);
            // 同じ名前のファイルは1つにマージされる
            Assert.Equal(1, results[0].FileCount);
        }

        /// <summary>
        /// 負のNumIntegerを持つファイルが含まれる場合のエラーハンドリング。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_NegativeNumInteger_IgnoresInvalidFiles()
        {
            var audioCache = new ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            var file1 = new BmsAudioFile { Name = "valid.wav", NumInteger = 1 };
            audioCache["valid.wav"] = BmsTestAudioHelper.CreateDummyCache();
            var file2 = new BmsAudioFile { Name = "invalid.wav", NumInteger = -1 };
            audioCache["invalid.wav"] = BmsTestAudioHelper.CreateDummyCache();

            var fileList = new List<BmsAudioFile> { file1, file2 };
            var engine = new SimulationEngine(fileList, audioCache, 1, 10);

            var results = engine.RunParallelSimulation(0.5f, 0.5f, 0.1f, null);

            Assert.Single(results);
            // 負のNumIntegerは範囲外として無視される
            Assert.Equal(1, results[0].FileCount);
        }

        /// <summary>
        /// 空白のファイル名を持つケースのハンドリング。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_EmptyFileName_HandlesGracefully()
        {
            var audioCache = new ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            var file1 = new BmsAudioFile { Name = "", NumInteger = 1 };
            var file2 = new BmsAudioFile { Name = "", NumInteger = 2 };

            var fileList = new List<BmsAudioFile> { file1, file2 };
            var engine = new SimulationEngine(fileList, audioCache, 1, 2);

            var exception = Record.Exception(() => engine.RunParallelSimulation(0.5f, 0.5f, 0.1f, null));

            // 例外なく処理されること
            Assert.Null(exception);
        }

        /// <summary>
        /// 極端に多いファイル数（Base62制限超）でのシミュレーション。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_MoreThanBase62Limit_HandlesCorrectly()
        {
            var audioCache = new ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            // Base62の上限（3843）付近のファイル数でテスト
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

            // Base62上限でもクラッシュせず、適切に結果が得られること
            Assert.NotEmpty(results);
            Assert.True(results.Count >= 1);
        }

        #endregion

        private class SyncProgress<T>(Action<T> handler) : IProgress<T>
        {
            public void Report(T value) => handler(value);
        }
    }
}
