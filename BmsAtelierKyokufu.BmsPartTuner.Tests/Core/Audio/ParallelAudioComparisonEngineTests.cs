using System.Collections.Concurrent;
using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Audio
{
    /// <summary>
    /// ParallelAudioComparisonEngine の動作検証テスト。
    /// 並列処理による音声ファイルの比較・置換テーブル更新の仕様を確認します。
    /// </summary>
    public class ParallelAudioComparisonEngineTests
    {
        [Fact]
        public void CompareGroups_IdenticalFiles_UpdatesReplaceTable()
        {
            var audioCache = new ConcurrentDictionary<string, CachedSoundData>();

            var samples = new float[] { 0.1f, 0.2f, 0.3f };
            var fileList = new List<BmsAudioFile>
            {
                BmsTestAudioHelper.CreateAudioFileWithMockCache(0, [0], audioCache), // ダミー
                BmsTestAudioHelper.CreateAudioFileWithMockCache(1, samples, audioCache),
                BmsTestAudioHelper.CreateAudioFileWithMockCache(2, samples, audioCache),
                BmsTestAudioHelper.CreateAudioFileWithMockCache(3, [0.5f, 0.6f, 0.7f], audioCache) // 異なるデータ
            };

            var replaceTable = new int[fileList.Count];
            var groups = new List<List<int>>
            {
                new() { 1, 2, 3 }
            };

            var parameters = new AudioComparisonParameters(fileList, audioCache, replaceTable, 1, fileList.Count - 1);
            var engine = new ParallelAudioComparisonEngine(parameters);

            // 並列処理エンジンの仕様確認
            // 置換が発生しない（ユニークな）ファイルは、処理済みマークとして
            // 置換テーブルに「自分自身のID」が設定されます（0=未処理 ではありません）。
            engine.CompareGroups(groups, 0.99f, new Progress<int>());

            // 2は1と同一なので1に置換される
            Assert.Equal(1, replaceTable[2]);
            // 3はユニークなので自身のIDでマークされる
            Assert.Equal(3, replaceTable[3]);
        }

        [Fact]
        public void CompareGroups_SimilarFiles_UpdatesReplaceTable()
        {
            var audioCache = new ConcurrentDictionary<string, CachedSoundData>();

            var samples1 = new float[] { 0.1f, 0.2f, 0.3f };
            var samples2 = new float[] { 0.11f, 0.21f, 0.31f }; // 非常に近いデータ

            var fileList = new List<BmsAudioFile>
            {
                BmsTestAudioHelper.CreateAudioFileWithMockCache(0, [0], audioCache),
                BmsTestAudioHelper.CreateAudioFileWithMockCache(1, samples1, audioCache),
                BmsTestAudioHelper.CreateAudioFileWithMockCache(2, samples2, audioCache)
            };

            var replaceTable = new int[fileList.Count];
            var groups = new List<List<int>> { new() { 1, 2 } };

            var parameters = new AudioComparisonParameters(fileList, audioCache, replaceTable, 1, fileList.Count - 1);
            var engine = new ParallelAudioComparisonEngine(parameters);

            // 類似度が高い場合、2は1に置換される
            engine.CompareGroups(groups, 0.90f, new Progress<int>());

            Assert.Equal(1, replaceTable[2]);
        }

        [Fact]
        public void CompareGroups_DifferentFiles_NoReplacement()
        {
            var audioCache = new ConcurrentDictionary<string, CachedSoundData>();

            var samples1 = new float[] { 0.1f, 0.2f, 0.3f };
            var samples2 = new float[] { -0.1f, -0.2f, -0.3f }; // 反転（相関係数 -1）

            var fileList = new List<BmsAudioFile>
            {
                BmsTestAudioHelper.CreateAudioFileWithMockCache(0, [0], audioCache),
                BmsTestAudioHelper.CreateAudioFileWithMockCache(1, samples1, audioCache),
                BmsTestAudioHelper.CreateAudioFileWithMockCache(2, samples2, audioCache)
            };

            var replaceTable = new int[fileList.Count];
            var groups = new List<List<int>> { new() { 1, 2 } };

            var engine = new ParallelAudioComparisonEngine(new AudioComparisonParameters(
                fileList, audioCache, replaceTable, 1, fileList.Count - 1));

            // 類似度が低い場合、置換は発生せず各自のIDでマークされる
            engine.CompareGroups(groups, 0.99f, new Progress<int>());

            Assert.Equal(1, replaceTable[1]);
            Assert.Equal(2, replaceTable[2]);
        }
    }
}
