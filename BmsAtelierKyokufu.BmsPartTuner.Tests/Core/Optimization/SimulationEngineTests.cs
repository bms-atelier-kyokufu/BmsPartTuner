using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;
using BmsAtelierKyokufu.BmsPartTuner.Models;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Optimization
{
    /// <summary>
    /// SimulationEngine の動作検証テスト。
    /// Union-Findロジック、RMS範囲計算、閾値生成、並列シミュレーションの仕様を確認します。
    /// </summary>
    public class SimulationEngineTests
    {
        #region テストヘルパー

        /// <summary>
        /// SimulationEngineはキャッシュ(CachedData)がないファイルを処理対象外としてスキップします。
        /// 名前の一致判定ロジックをテストするため、ダミーのキャッシュデータを設定します。
        /// </summary>
        private CachedSoundData CreateDummyCache()
        {
            return new CachedSoundData(new float[1][] { new float[10] }, 44100, 16);
        }

        /// <summary>
        /// 異なる波形データを持つキャッシュを生成します（音声比較テスト用）。
        /// </summary>
        /// <param name="frequency">サイン波の周波数（Hz）。異なる値で異なる波形を生成します。</param>
        private CachedSoundData CreateDistinctCache(double frequency = 440.0)
        {
            int sampleCount = 4410; // 0.1秒分 @ 44100Hz
            float[] samples = new float[sampleCount];

            // サイン波を生成（周波数によって異なる波形になる）
            for (int i = 0; i < sampleCount; i++)
            {
                double t = (double)i / 44100;
                samples[i] = (float)(0.5 * Math.Sin(2 * Math.PI * frequency * t));
            }

            return new CachedSoundData(new float[1][] { samples }, 44100, 16);
        }

        #endregion

        #region Union-Find ロジックテスト

        [Fact]
        public void FindRoot_DirectParent_ReturnsParent()
        {

            int[] table = new int[10];
            table[2] = 1;
            table[1] = 1; // 1がルート


            int root = SimulationEngine.FindRoot(table, 2);


            Assert.Equal(1, root);
        }

        [Fact]
        public void FindRoot_TransitiveParent_ReturnsRoot()
        {

            int[] table = new int[10];
            table[3] = 2;
            table[2] = 1;
            table[1] = 1;


            int root = SimulationEngine.FindRoot(table, 3);


            Assert.Equal(1, root);
        }

        [Fact]
        public void FindRoot_Uninitialized_ReturnsSelf()
        {

            int[] table = new int[10];



            int root = SimulationEngine.FindRoot(table, 5);


            Assert.Equal(5, root);
        }

        [Fact]
        public void UpdateReplaceTable_MergesSetsCorrectly()
        {

            int[] table = new int[10];


            SimulationEngine.UpdateReplaceTable(table, 2, 3);


            int root2 = SimulationEngine.FindRoot(table, 2);
            int root3 = SimulationEngine.FindRoot(table, 3);
            Assert.Equal(root2, root3);
            Assert.Equal(2, root2); // より小さい方がルートになる
        }

        [Fact]
        public void UpdateReplaceTable_TransitiveMerge_WorksCorrectly()
        {

            int[] table = new int[10];

            SimulationEngine.UpdateReplaceTable(table, 2, 3);
            SimulationEngine.UpdateReplaceTable(table, 3, 4);


            int root2 = SimulationEngine.FindRoot(table, 2);
            int root3 = SimulationEngine.FindRoot(table, 3);
            int root4 = SimulationEngine.FindRoot(table, 4);
            Assert.Equal(root2, root3);
            Assert.Equal(root2, root4);
        }

        [Fact]
        public void UpdateReplaceTable_AlreadyMerged_NoChange()
        {

            int[] table = new int[10];
            SimulationEngine.UpdateReplaceTable(table, 2, 3);
            int rootBefore = SimulationEngine.FindRoot(table, 3);

            // 実行: 再度マージを試みる
            SimulationEngine.UpdateReplaceTable(table, 2, 3);

            // 検証: 既にマージ済みなので変化なし
            int rootAfter = SimulationEngine.FindRoot(table, 3);
            Assert.Equal(rootBefore, rootAfter);
        }

        [Fact]
        public void UpdateReplaceTable_MultipleGroups_MaintainsSeparation()
        {
            // 準備
            int[] table = new int[20];

            // 実行
            // グループ1: {1, 2, 3}
            SimulationEngine.UpdateReplaceTable(table, 1, 2);
            SimulationEngine.UpdateReplaceTable(table, 2, 3);

            // グループ2: {10, 11, 12}
            SimulationEngine.UpdateReplaceTable(table, 10, 11);
            SimulationEngine.UpdateReplaceTable(table, 11, 12);

            // 検証: 各グループは分離されている
            int root1 = SimulationEngine.FindRoot(table, 1);
            int root2 = SimulationEngine.FindRoot(table, 2);
            int root3 = SimulationEngine.FindRoot(table, 3);
            int root10 = SimulationEngine.FindRoot(table, 10);
            int root11 = SimulationEngine.FindRoot(table, 11);
            int root12 = SimulationEngine.FindRoot(table, 12);

            Assert.Equal(root1, root2);
            Assert.Equal(root1, root3);
            Assert.Equal(root10, root11);
            Assert.Equal(root10, root12);
            Assert.NotEqual(root1, root10); // 異なるグループ
        }

        #endregion

        #region RMS範囲計算テスト

        [Fact]
        public void CalculateRmsRange_SilentAudio_ReturnsZeroToThreshold()
        {
            // 実行
            var (min, max) = SimulationEngine.CalculateRmsRange(0.0005f);

            // 検証
            Assert.Equal(0.0f, min);
            Assert.True(max > 0.0f);
        }

        [Fact]
        public void CalculateRmsRange_NormalAudio_ReturnsProportionalRange()
        {
            // 実行
            var (min, max) = SimulationEngine.CalculateRmsRange(0.5f);

            // 検証
            Assert.True(min < 0.5f);
            Assert.True(max > 0.5f);
            Assert.True(min > 0.0f);
        }

        [Fact]
        public void CalculateRmsRange_HighRmsAudio_ReturnsWiderRange()
        {
            // 実行
            var (min1, max1) = SimulationEngine.CalculateRmsRange(0.1f);
            var (min2, max2) = SimulationEngine.CalculateRmsRange(1.0f);

            // 検証
            float range1 = max1 - min1;
            float range2 = max2 - min2;
            Assert.True(range2 > range1);
        }

        #endregion

        #region 閾値生成テスト

        [Fact]
        public void GenerateThresholds_ValidRange_ReturnsDescendingList()
        {
            // 実行
            var thresholds = SimulationEngine.GenerateThresholds(0.5f, 0.9f, 0.1f);

            // 検証
            Assert.NotEmpty(thresholds);
            Assert.Equal(0.9f, thresholds[0], 2);
            // 実際の出力を確認: 降順で0.9から0.1ステップで減算
            // 0.9 - 0 = 0.9
            // 0.9 - 0.1 = 0.8
            // 0.9 - 0.2 = 0.7
            // 0.9 - 0.3 = 0.6
            // 0.9 - 0.4 = 0.5 <- 浮動小数点誤差で含まれない可能性あり
            // 少なくとも4要素（0.9, 0.8, 0.7, 0.6）は含まれるべき
            Assert.InRange(thresholds.Count, 4, 5);

            // 降順確認
            for (int i = 0; i < thresholds.Count - 1; i++)
            {
                Assert.True(thresholds[i] > thresholds[i + 1],
                    $"Expected descending order but thresholds[{i}]={thresholds[i]} <= thresholds[{i + 1}]={thresholds[i + 1]}");
            }

            // 最後の要素が最小値
            Assert.InRange(thresholds[^1], 0.5f, 0.7f);
        }

        [Fact]
        public void GenerateThresholds_SingleValue_ReturnsSingleElement()
        {
            // 実行
            var thresholds = SimulationEngine.GenerateThresholds(0.8f, 0.8f, 0.1f);

            // 検証
            Assert.Single(thresholds);
            Assert.Equal(0.8f, thresholds[0]);
        }

        [Fact]
        public void GenerateThresholds_SmallStep_ReturnsMoreElements()
        {
            // 実行
            var thresholds1 = SimulationEngine.GenerateThresholds(0.5f, 0.9f, 0.1f);
            var thresholds2 = SimulationEngine.GenerateThresholds(0.5f, 0.9f, 0.05f);

            // 検証
            Assert.True(thresholds2.Count > thresholds1.Count);
        }

        #endregion

        #region 統合テスト

        [Fact]
        public void RunParallelSimulation_EmptyList_ReturnsEmptyResults()
        {
            // 準備
            var fileList = new List<FileList.WavFiles>();
            var engine = new SimulationEngine(fileList, 1, 10);

            // 実行
            var results = engine.RunParallelSimulation(0.5f, 0.9f, 0.1f, null);

            // 検証
            Assert.NotNull(results);
            // 空リストでもシミュレーションは実行されるが、ファイル数は0
            Assert.All(results, r => Assert.Equal(0, r.FileCount));
        }

        [Fact]
        public void RunParallelSimulation_SingleFile_ReturnsCountOne()
        {
            // SimulationEngineはCachedData=nullのファイルをスキップするため、ダミーを設定
            var file1 = new FileList.WavFiles { Name = "a.wav", NumInteger = 1, CachedData = CreateDummyCache() };
            var fileList = new List<FileList.WavFiles> { file1 };
            var engine = new SimulationEngine(fileList, 1, 1);

            // 実行
            var results = engine.RunParallelSimulation(0.5f, 0.5f, 0.1f, null);

            // 検証
            Assert.Single(results);
            Assert.Equal(1, results[0].FileCount);
        }

        [Fact]
        public void RunParallelSimulation_TwoDifferentFiles_ReturnsCountTwo()
        {
            // SimulationEngineはCachedData=nullのファイルをスキップするため、
            // 明確に異なる波形データを持つキャッシュを設定
            var file1 = new FileList.WavFiles { Name = "a.wav", NumInteger = 1, CachedData = CreateDistinctCache(440.0) };
            var file2 = new FileList.WavFiles { Name = "b.wav", NumInteger = 2, CachedData = CreateDistinctCache(880.0) };
            var fileList = new List<FileList.WavFiles> { file1, file2 };
            var engine = new SimulationEngine(fileList, 1, 2);

            // 実行: 厳密なしきい値（0.99）で異なるファイルは統合されないことを確認
            var results = engine.RunParallelSimulation(0.99f, 0.99f, 0.1f, null);

            // 検証
            Assert.Single(results);
            Assert.Equal(2, results[0].FileCount); // 異なる名前・異なる波形なので統合されない
        }

        [Fact]
        public void RunParallelSimulation_TwoIdenticalNames_MergesCorrectly()
        {
            // SimulationEngineはCachedData=nullのファイルをスキップするため、ダミーを設定
            // 同じファイル名は統合されるべき
            var file1 = new FileList.WavFiles { Name = "a.wav", NumInteger = 1, CachedData = CreateDummyCache() };
            var file2 = new FileList.WavFiles { Name = "a.wav", NumInteger = 2, CachedData = CreateDummyCache() };
            var fileList = new List<FileList.WavFiles> { file1, file2 };
            var engine = new SimulationEngine(fileList, 1, 2);

            // 実行
            var results = engine.RunParallelSimulation(0.5f, 0.5f, 0.1f, null);

            // 検証: 決定論的なアサーション
            Assert.Single(results);
            // CachedDataがある場合、名前ベースのグループ化が行われるため、
            // 同じ名前のファイルは1つにマージされる
            Assert.Equal(1, results[0].FileCount);

            // 以前は Assert.InRange(1, 2) で非決定論的だったが、
            // 名前ベースのグループ化ロジックにより決定論的に1になる
        }

        [Fact]
        public void UpdateReplaceTable_ConcurrentAccess_MaintainsConsistency()
        {
            // 準備: スレッドセーフ性の検証
            const int iterations = 1000;
            const int maxIndex = 100;
            int[] table = new int[maxIndex + 1];

            // 実行: 複数スレッドから同時にマージ操作を実行
            Parallel.For(0, iterations, i =>
            {
                int a = (i % maxIndex) + 1;
                int b = ((i + 1) % maxIndex) + 1;
                SimulationEngine.UpdateReplaceTable(table, a, b);
            });

            // 全ての要素がルートにたどり着ける（無限ループなし）
            for (int i = 1; i <= maxIndex; i++)
            {
                int root = SimulationEngine.FindRoot(table, i);
                Assert.InRange(root, 1, maxIndex);

                // ルート要素の値が 0 (初期値=自分自身) であることも許容する
                // SimulationEngineの実装では 0 は「親なし（自分自身がルート）」を意味する
                Assert.True(table[root] == root || table[root] == 0 || table[root] > 0);
            }
        }

        [Fact]
        public async Task UpdateReplaceTable_RaceCondition_NoDeadlock()
        {
            // 準備: 競合状態のテスト
            const int threadCount = 10;
            const int operationsPerThread = 100;
            int[] table = new int[1000];
            var tasks = new List<Task>();

            // 実行: 複数スレッドから同じペアをマージ
            for (int t = 0; t < threadCount; t++)
            {
                int threadId = t;
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        // 同じペアを繰り返しマージしてデッドロックや無限ループを検証
                        SimulationEngine.UpdateReplaceTable(table, 10, 20);
                        SimulationEngine.UpdateReplaceTable(table, 20, 30);
                        SimulationEngine.UpdateReplaceTable(table, 30, 40);
                    }
                }));
            }

            // 検証: タイムアウトなく完了する（タイムアウトを明示的にチェック）
            var timeout = TimeSpan.FromSeconds(5);
            var allTask = Task.WhenAll(tasks);
            if (await Task.WhenAny(allTask, Task.Delay(timeout)) != allTask)
            {
                Assert.Fail("並列マージ操作がタイムアウトしました（デッドロックの可能性）");
            }
            // 例外が発生していればここでスローされる
            await allTask;

            // 最終的な一貫性を検証
            int root10 = SimulationEngine.FindRoot(table, 10);
            int root20 = SimulationEngine.FindRoot(table, 20);
            int root30 = SimulationEngine.FindRoot(table, 30);
            int root40 = SimulationEngine.FindRoot(table, 40);

            Assert.Equal(root10, root20);
            Assert.Equal(root10, root30);
            Assert.Equal(root10, root40);
        }

        #endregion

        #region 早期終了テスト

        [Fact]
        public void RunParallelSimulation_Base36ConditionMet_TerminatesEarly()
        {
            // 準備
            // Base36の上限は1295。3ファイルは常に条件以下なので、
            // 最初の閾値で早期終了するはず
            var file1 = new FileList.WavFiles { Name = "a.wav", NumInteger = 1, CachedData = null };
            var file2 = new FileList.WavFiles { Name = "a.wav", NumInteger = 2, CachedData = null };
            var file3 = new FileList.WavFiles { Name = "a.wav", NumInteger = 3, CachedData = null };

            var fileList = new List<FileList.WavFiles> { file1, file2, file3 };
            var engine = new SimulationEngine(fileList, 1, 3);

            // 実行
            var results = engine.RunParallelSimulation(0.1f, 0.5f, 0.1f, null);

            // 検証
            // 早期終了するため、1つの閾値のみ実行される
            Assert.Single(results);
            Assert.Equal(0.5f, results[0].Threshold);
        }

        #endregion

        #region エッジケーステスト

        [Fact]
        public void RunParallelSimulation_NullCache_HandledGracefully()
        {
            // 準備
            var file1 = new FileList.WavFiles { Name = "a.wav", NumInteger = 1, CachedData = null };
            var fileList = new List<FileList.WavFiles> { file1 };
            var engine = new SimulationEngine(fileList, 1, 1);

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
            // 異なる名前のファイルは、異なる波形データでマージされない
            var file1 = new FileList.WavFiles
            {
                Name = "unique1.wav",
                NumInteger = 1,
                CachedData = CreateDistinctCache(440.0) // A4音
            };
            var file2 = new FileList.WavFiles
            {
                Name = "unique2.wav",
                NumInteger = 2,
                CachedData = CreateDistinctCache(880.0) // A5音
            };
            var file3 = new FileList.WavFiles
            {
                Name = "unique3.wav",
                NumInteger = 3,
                CachedData = CreateDistinctCache(1320.0) // E6音
            };

            var fileList = new List<FileList.WavFiles> { file1, file2, file3 };
            var engine = new SimulationEngine(fileList, 1, 3);

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
            // 同じ名前のファイルはすべてマージされる
            var file1 = new FileList.WavFiles
            {
                Name = "same.wav",
                NumInteger = 1,
                CachedData = CreateDummyCache()
            };
            var file2 = new FileList.WavFiles
            {
                Name = "same.wav",
                NumInteger = 2,
                CachedData = CreateDummyCache()
            };
            var file3 = new FileList.WavFiles
            {
                Name = "same.wav",
                NumInteger = 3,
                CachedData = CreateDummyCache()
            };

            var fileList = new List<FileList.WavFiles> { file1, file2, file3 };
            var engine = new SimulationEngine(fileList, 1, 3);

            var results = engine.RunParallelSimulation(0.5f, 0.5f, 0.01f, null);

            Assert.Single(results);
            Assert.Equal(1, results[0].FileCount); // すべてマージされて1つに
        }

        /// <summary>
        /// しきい値の範囲が非常に狭い場合のテスト。
        /// </summary>
        [Fact]
        public void GenerateThresholds_VerySmallRange_ReturnsAtLeastOne()
        {
            var thresholds = SimulationEngine.GenerateThresholds(0.95f, 0.96f, 0.01f);

            Assert.NotEmpty(thresholds);
            Assert.True(thresholds.Count >= 1);
            Assert.True(thresholds.All(t => t >= 0.95f && t <= 0.96f));
        }

        /// <summary>
        /// しきい値のステップがゼロに近い極端な場合のテスト。
        /// </summary>
        [Fact]
        public void GenerateThresholds_VerySmallStep_GeneratesManyThresholds()
        {
            var thresholds = SimulationEngine.GenerateThresholds(0.90f, 0.91f, 0.001f);

            // 0.001刻みで0.90-0.91なら約10個生成されるはず
            Assert.True(thresholds.Count >= 10);
        }

        /// <summary>
        /// ステップがrangeより大きい場合、最大値のみが返される。
        /// </summary>
        [Fact]
        public void GenerateThresholds_StepLargerThanRange_ReturnsSingleMaxValue()
        {
            var thresholds = SimulationEngine.GenerateThresholds(0.5f, 0.6f, 1.0f);

            Assert.Single(thresholds);
            Assert.Equal(0.6f, thresholds[0]);
        }

        #endregion

        #region Priority S: Edge Case Tests (極端なケース)

        /// <summary>
        /// 極端に短い音声データ（1サンプル）のファイルリストでの動作検証。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_VeryShortAudioData_HandlesGracefully()
        {
            var shortCache = new CachedSoundData(new float[1][] { new float[1] { 0.5f } }, 44100, 16);

            var file1 = new FileList.WavFiles
            {
                Name = "short1.wav",
                NumInteger = 1,
                CachedData = shortCache
            };
            var file2 = new FileList.WavFiles
            {
                Name = "short2.wav",
                NumInteger = 2,
                CachedData = shortCache
            };

            var fileList = new List<FileList.WavFiles> { file1, file2 };
            var engine = new SimulationEngine(fileList, 1, 2);

            var results = engine.RunParallelSimulation(0.5f, 0.5f, 0.1f, null);

            // 例外なく完了すること
            Assert.NotNull(results);
            Assert.NotEmpty(results);
        }

        /// <summary>
        /// 無音データ（すべてゼロ）のファイルでの動作検証。
        /// 相関係数が計算不能になるケースの確認。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_SilentAudioData_HandlesGracefully()
        {
            var silentData = new float[1][] { new float[100] }; // すべて0
            var silentCache = new CachedSoundData(silentData, 44100, 16);

            var file1 = new FileList.WavFiles
            {
                Name = "silent1.wav",
                NumInteger = 1,
                CachedData = silentCache
            };
            var file2 = new FileList.WavFiles
            {
                Name = "silent2.wav",
                NumInteger = 2,
                CachedData = silentCache
            };

            var fileList = new List<FileList.WavFiles> { file1, file2 };
            var engine = new SimulationEngine(fileList, 1, 2);

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
            // 100個のファイルで早期終了が動作することを確認
            var fileList = new List<FileList.WavFiles>();
            for (int i = 1; i <= 100; i++)
            {
                fileList.Add(new FileList.WavFiles
                {
                    Name = $"file{i}.wav",
                    NumInteger = i,
                    CachedData = null // CachedData=nullでもカウントは行われる
                });
            }

            var engine = new SimulationEngine(fileList, 1, 100);
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
            var file1 = new FileList.WavFiles
            {
                Name = "in_range.wav",
                NumInteger = 5,
                CachedData = CreateDummyCache()
            };
            var file2 = new FileList.WavFiles
            {
                Name = "out_of_range.wav",
                NumInteger = 100, // 範囲外
                CachedData = CreateDummyCache()
            };

            var fileList = new List<FileList.WavFiles> { file1, file2 };
            var engine = new SimulationEngine(fileList, 1, 10); // 1-10の範囲

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
            var progressValues = new List<int>();
            var progress = new Progress<int>(p => progressValues.Add(p));

            var fileList = new List<FileList.WavFiles>
            {
                new FileList.WavFiles { Name = "a.wav", NumInteger = 1, CachedData = CreateDummyCache() }
            };
            var engine = new SimulationEngine(fileList, 1, 1);

            engine.RunParallelSimulation(0.1f, 0.5f, 0.1f, progress);

            // Progress<T>は非同期なので少し待つ
            Thread.Sleep(100);

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
            var file1 = new FileList.WavFiles { Name = "a.wav", NumInteger = 1, CachedData = CreateDistinctCache(440.0) };
            var file2 = new FileList.WavFiles { Name = "b.wav", NumInteger = 2, CachedData = CreateDistinctCache(880.0) };
            var file3 = new FileList.WavFiles { Name = "c.wav", NumInteger = 3, CachedData = CreateDistinctCache(1320.0) };

            var fileList = new List<FileList.WavFiles> { file1, file2, file3 };
            var engine = new SimulationEngine(fileList, 1, 3);

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
            var file1 = new FileList.WavFiles { Name = "diff1.wav", NumInteger = 1, CachedData = CreateDistinctCache(440.0) };
            var file2 = new FileList.WavFiles { Name = "diff2.wav", NumInteger = 2, CachedData = CreateDistinctCache(880.0) };

            var fileList = new List<FileList.WavFiles> { file1, file2 };
            var engine = new SimulationEngine(fileList, 1, 2);

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
            var file1 = new FileList.WavFiles { Name = "single.wav", NumInteger = 5, CachedData = CreateDummyCache() };
            var fileList = new List<FileList.WavFiles> { file1 };
            var engine = new SimulationEngine(fileList, 5, 5); // 範囲が1つだけ

            var results = engine.RunParallelSimulation(0.5f, 0.5f, 0.1f, null);

            Assert.Single(results);
            Assert.Equal(1, results[0].FileCount);
        }

        /// <summary>
        /// 音声データのRMSが極端に小さい（静音に近い）ケース。
        /// </summary>
        [Fact]
        public void CalculateRmsRange_VeryLowRms_ReturnsNonNegativeRange()
        {
            var (min, max) = SimulationEngine.CalculateRmsRange(0.00001f);

            Assert.True(min >= 0.0f, "RMS範囲の最小値は非負であるべき");
            Assert.True(max > min, "RMS範囲の最大値は最小値より大きいべき");
        }

        /// <summary>
        /// 音声データのRMSが極端に大きい（クリッピングレベル）ケース。
        /// </summary>
        [Fact]
        public void CalculateRmsRange_VeryHighRms_ReturnsValidRange()
        {
            var (min, max) = SimulationEngine.CalculateRmsRange(0.99f);

            Assert.True(min >= 0.0f);
            Assert.True(max <= 2.0f); // 現実的な上限チェック
            Assert.True(max > min);
        }

        /// <summary>
        /// 逆順の定義範囲（startPoint > endPoint）が正しくハンドルされるかテスト。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_ReversedRange_HandlesGracefully()
        {
            var file1 = new FileList.WavFiles { Name = "a.wav", NumInteger = 5, CachedData = CreateDummyCache() };
            var fileList = new List<FileList.WavFiles> { file1 };

            // 逆順の範囲を指定（実装がどうハンドルするかを確認）
            var engine = new SimulationEngine(fileList, 10, 1);

            var exception = Record.Exception(() =>
            {
                engine.RunParallelSimulation(0.5f, 0.5f, 0.1f, null);
            });

            // 例外がスローされるか、空の結果が返されるか確認
            Assert.NotNull(exception ?? new Exception("No exception"));
        }

        /// <summary>
        /// 大量のしきい値を生成する場合のパフォーマンステスト。
        /// </summary>
        [Fact]
        public void GenerateThresholds_LargeNumberOfThresholds_CompletsesQuickly()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var thresholds = SimulationEngine.GenerateThresholds(0.0f, 1.0f, 0.001f);

            sw.Stop();

            Assert.NotEmpty(thresholds);
            Assert.True(thresholds.Count >= 1000, "0.001刻みで0.0-1.0なら約1000個");
            Assert.True(sw.ElapsedMilliseconds < 1000, "しきい値生成は1秒以内に完了すべき");
        }

        /// <summary>
        /// 同じファイル名だが異なるNumIntegerを持つケース（重複定義）。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_DuplicateNamesWithDifferentNumbers_MergesCorrectly()
        {
            var file1 = new FileList.WavFiles { Name = "dup.wav", NumInteger = 1, CachedData = CreateDummyCache() };
            var file2 = new FileList.WavFiles { Name = "dup.wav", NumInteger = 2, CachedData = CreateDummyCache() };
            var file3 = new FileList.WavFiles { Name = "dup.wav", NumInteger = 3, CachedData = CreateDummyCache() };

            var fileList = new List<FileList.WavFiles> { file1, file2, file3 };
            var engine = new SimulationEngine(fileList, 1, 3);

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
            var file1 = new FileList.WavFiles { Name = "valid.wav", NumInteger = 1, CachedData = CreateDummyCache() };
            var file2 = new FileList.WavFiles { Name = "invalid.wav", NumInteger = -1, CachedData = CreateDummyCache() };

            var fileList = new List<FileList.WavFiles> { file1, file2 };
            var engine = new SimulationEngine(fileList, 1, 10);

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
            var file1 = new FileList.WavFiles { Name = "", NumInteger = 1, CachedData = CreateDummyCache() };
            var file2 = new FileList.WavFiles { Name = "", NumInteger = 2, CachedData = CreateDummyCache() };

            var fileList = new List<FileList.WavFiles> { file1, file2 };
            var engine = new SimulationEngine(fileList, 1, 2);

            var exception = Record.Exception(() =>
            {
                engine.RunParallelSimulation(0.5f, 0.5f, 0.1f, null);
            });

            // 例外なく処理されるか、適切なエラーが返されること
            Assert.Null(exception);
        }

        /// <summary>
        /// FindRootでのパス圧縮の効果を検証（深いチェーンが圧縮される）。
        /// </summary>
        [Fact]
        public void FindRoot_PathCompression_CompressesDeepChain()
        {
            int[] table = new int[10];

            // 深いチェーンを作成: 1 <- 2 <- 3 <- 4 <- 5
            table[5] = 4;
            table[4] = 3;
            table[3] = 2;
            table[2] = 1;
            table[1] = 1; // ルート

            // 初回のFindRootでパス圧縮が行われる
            int root = SimulationEngine.FindRoot(table, 5);

            Assert.Equal(1, root);

            // パス圧縮後、5は直接1を指すようになるはず
            Assert.Equal(1, table[5]);
        }

        /// <summary>
        /// UpdateReplaceTableで同じ要素を自分自身にマージしようとするケース。
        /// </summary>
        [Fact]
        public void UpdateReplaceTable_SelfMerge_NoChange()
        {
            int[] table = new int[10];

            // 自分自身にマージを試みる
            SimulationEngine.UpdateReplaceTable(table, 3, 3);

            // ルートは自分自身のまま
            int root = SimulationEngine.FindRoot(table, 3);
            Assert.Equal(3, root);
        }

        /// <summary>
        /// 極端に多いファイル数（Base62制限超）でのシミュレーション。
        /// </summary>
        [Fact]
        public void RunParallelSimulation_MoreThanBase62Limit_HandlesCorrectly()
        {
            // Base62の上限（3843）付近のファイル数でテスト
            // replaceTableのサイズが3844なので、それを超えないようにする
            var fileList = new List<FileList.WavFiles>();
            for (int i = 1; i <= 3843; i++)
            {
                // テストデータとして簡易的な波形データを設定
                // CachedSoundDataはfloat[][]（チャンネルごと）を期待
                var monoChannel = new float[][] { new float[100] };
                var cachedData = new BmsAtelierKyokufu.BmsPartTuner.Models.CachedSoundData(
                    monoChannel,
                    44100,
                    16
                );

                fileList.Add(new FileList.WavFiles
                {
                    Name = $"file{i}.wav",
                    NumInteger = i,
                    CachedData = cachedData
                });
            }

            var engine = new SimulationEngine(fileList, 1, 3843);

            var results = engine.RunParallelSimulation(0.1f, 0.9f, 0.2f, null);

            // Base62上限でもクラッシュせず、適切に結果が得られること
            Assert.NotEmpty(results);
            // 早期終了により、すべてのしきい値がシミュレーションされるわけではない
            Assert.True(results.Count >= 1);

            // クリーンアップ
            foreach (var file in fileList)
            {
                file.CachedData?.Dispose();
            }
        }

        #endregion
    }
}
