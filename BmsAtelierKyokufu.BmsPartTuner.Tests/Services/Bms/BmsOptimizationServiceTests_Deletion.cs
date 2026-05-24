using System.IO;
using System.Text;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Services.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Services.Bms
{
    /// <summary>
    /// BmsOptimizationService の動作検証テスト。
    /// 特にファイルの削除ロジックと定義削減の整合性を確認します。
    /// </summary>
    public class BmsOptimizationServiceTests_Deletion
    {
        public BmsOptimizationServiceTests_Deletion()
        {
            // .NET 10ではShift_JISエンコーディングを使用するために登録が必要
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Fact]
        public async Task ExecuteDefinitionReductionAsync_DeletionEnabled_DeletesUnusedFiles()
        {
            using var context = new BmsTestContext();

            var file1Path = Path.Combine(context.TempDirectory, "used.wav");
            var file2Path = Path.Combine(context.TempDirectory, "unused.wav");
            BmsTestWavHelper.CreateValidWavFile(file1Path);
            BmsTestWavHelper.CreateValidWavFile(file2Path); // 同じ内容

            var bmsPath = context.CreateBuilder()
                .WithHeader("HEADER", "")
                .WithWav(1, "used.wav")
                .WithHeader("MAIN", "")
                .AddMainData(11, "01")
                .Build("test.bms");

            var outputPath = Path.Combine(context.TempDirectory, "output.bms");

            var file1 = new BmsAudioFile { Name = file1Path, NumInteger = 1, Num = "01" };
            var file2 = new BmsAudioFile { Name = file2Path, NumInteger = 2, Num = "02" };
            var fileList = new List<BmsAudioFile> { file1, file2 };

            var service = new BmsOptimizationService();

            // ファイル2がファイル1と重複している場合、削減処理で物理削除されることを検証
            BmsTestWavHelper.CreateValidWavFile(file1Path);
            BmsTestWavHelper.CreateValidWavFile(file2Path); // 完全な重複

            var file1_dup = new BmsAudioFile { Name = file1Path, NumInteger = 1, Num = "01", FileSize = new FileInfo(file1Path).Length };
            var file2_dup = new BmsAudioFile { Name = file2Path, NumInteger = 2, Num = "02", FileSize = new FileInfo(file2Path).Length };
            var fileList_dup = new List<BmsAudioFile> { file1_dup, file2_dup };

            // 100%一致（R2=1.0）で削減判定
            var result = await service.ExecuteDefinitionReductionAsync(
                fileList_dup,
                bmsPath,
                outputPath,
                new DefinitionReductionOptions
                {
                    R2Threshold = 0.99f,
                    StartDefinition = 1,
                    EndDefinition = 2,
                    IsPhysicalDeletionEnabled = true
                });

            // 削減後、重複ファイルが削除されることを確認
            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.OptimizedCount); // 1つに削減される
            Assert.Equal(1, result.DeletedFilesCount);

            Assert.True(File.Exists(file1Path), "残すべきファイルが存在すること");
            Assert.False(File.Exists(file2Path), "重複（未使用）ファイルが削除されていること");
        }

        [Fact]
        public async Task ExecuteDefinitionReductionAsync_DeletionDisabled_KeepsUnusedFiles()
        {
            using var context = new BmsTestContext();

            var file1Path = Path.Combine(context.TempDirectory, "used.wav");
            var file2Path = Path.Combine(context.TempDirectory, "unused.wav");
            BmsTestWavHelper.CreateValidWavFile(file1Path);
            BmsTestWavHelper.CreateValidWavFile(file2Path); // 同じ内容

            var bmsPath = context.CreateBuilder()
                .WithHeader("HEADER", "")
                .WithWav(1, "used.wav")
                .WithHeader("MAIN", "")
                .AddMainData(11, "01")
                .Build("test.bms");
            var outputPath = Path.Combine(context.TempDirectory, "output.bms");

            var file1 = new BmsAudioFile { Name = file1Path, NumInteger = 1, Num = "01", FileSize = new FileInfo(file1Path).Length };
            var file2 = new BmsAudioFile { Name = file2Path, NumInteger = 2, Num = "02", FileSize = new FileInfo(file2Path).Length };
            var fileList = new List<BmsAudioFile> { file1, file2 };

            var service = new BmsOptimizationService();

            // 削除無効時は未使用ファイルが残ることを検証
            var result = await service.ExecuteDefinitionReductionAsync(
                fileList,
                bmsPath,
                outputPath,
                new DefinitionReductionOptions
                {
                    R2Threshold = 0.99f,
                    StartDefinition = 1,
                    EndDefinition = 2,
                    IsPhysicalDeletionEnabled = false
                });

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.OptimizedCount);
            Assert.Equal(0, result.DeletedFilesCount);

            Assert.True(File.Exists(file1Path));
            Assert.True(File.Exists(file2Path), "削除無効時は未使用ファイルが残ること");
        }

        #region Priority S: Extended Deletion Logic Tests

        /// <summary>
        /// 複数の重複ファイルが存在する場合、正しく削除されることを検証。
        /// </summary>
        [Fact]
        public async Task ExecuteDefinitionReductionAsync_MultipleDuplicates_DeletesAllUnused()
        {
            using var context = new BmsTestContext();

            var file1Path = Path.Combine(context.TempDirectory, "original.wav");
            var file2Path = Path.Combine(context.TempDirectory, "dup1.wav");
            var file3Path = Path.Combine(context.TempDirectory, "dup2.wav");
            var file4Path = Path.Combine(context.TempDirectory, "dup3.wav");

            BmsTestWavHelper.CreateValidWavFile(file1Path);
            BmsTestWavHelper.CreateValidWavFile(file2Path); // 重複
            BmsTestWavHelper.CreateValidWavFile(file3Path); // 重複
            BmsTestWavHelper.CreateValidWavFile(file4Path); // 重複

            var bmsPath = context.CreateBuilder()
                .WithHeader("GENRE", "Test")
                .WithWav(1, "original.wav")
                .AddMainData(11, "01")
                .Build("test_multi_dup.bms");

            var outputPath = Path.Combine(context.TempDirectory, "output_multi_dup.bms");

            var fileList = new List<BmsAudioFile>
            {
                new() { Name = file1Path, NumInteger = 1, Num = "01", FileSize = new FileInfo(file1Path).Length },
                new() { Name = file2Path, NumInteger = 2, Num = "02", FileSize = new FileInfo(file2Path).Length },
                new() { Name = file3Path, NumInteger = 3, Num = "03", FileSize = new FileInfo(file3Path).Length },
                new() { Name = file4Path, NumInteger = 4, Num = "04", FileSize = new FileInfo(file4Path).Length }
            };

            var service = new BmsOptimizationService();

            var result = await service.ExecuteDefinitionReductionAsync(
                fileList,
                bmsPath,
                outputPath,
                new DefinitionReductionOptions
                {
                    R2Threshold = 0.99f,
                    StartDefinition = 1,
                    EndDefinition = 4,
                    IsPhysicalDeletionEnabled = true
                });

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.OptimizedCount);
            Assert.True(File.Exists(file1Path), "オリジナルファイルは残る");
            Assert.True(result.DeletedFilesCount >= 1, "少なくとも1つの重複ファイルが削除される");
        }

        /// <summary>
        /// 異なる周波数のWAVファイルが重複判定されないことを検証（データ破壊防止）。
        /// </summary>
        [Fact]
        public async Task ExecuteDefinitionReductionAsync_DifferentFrequency_NotMerged()
        {
            using var context = new BmsTestContext();

            var file1Path = Path.Combine(context.TempDirectory, "low_freq.wav");
            var file2Path = Path.Combine(context.TempDirectory, "high_freq.wav");

            BmsTestWavHelper.CreateValidWavFile(file1Path, isDifferent: false);  // 440Hz
            BmsTestWavHelper.CreateValidWavFile(file2Path, isDifferent: true);   // 880Hz

            var bmsPath = context.CreateBuilder()
                .WithHeader("GENRE", "Test")
                .WithWav(1, "low_freq.wav", false)  // Use existing file
                .WithWav(2, "high_freq.wav", false) // Use existing file
                .AddMainData(11, "0102")
                .Build("test_diff_freq.bms");

            var outputPath = Path.Combine(context.TempDirectory, "output_diff_freq.bms");

            var fileList = new List<BmsAudioFile>
            {
                new() { Name = file1Path, NumInteger = 1, Num = "01", FileSize = new FileInfo(file1Path).Length },
                new() { Name = file2Path, NumInteger = 2, Num = "02", FileSize = new FileInfo(file2Path).Length }
            };

            var service = new BmsOptimizationService();

            // 高いしきい値（0.99）では異なる周波数のファイルはマージされない
            var result = await service.ExecuteDefinitionReductionAsync(
                fileList,
                bmsPath,
                outputPath,
                new DefinitionReductionOptions
                {
                    R2Threshold = 0.99f, // 厳密なしきい値
                    StartDefinition = 1,
                    EndDefinition = 2,
                    IsPhysicalDeletionEnabled = true
                });

            Assert.True(result.IsSuccess, $"処理失敗: {result.ErrorMessage}");
            // 異なる音源なのでマージされない（2ファイルのまま）
            Assert.Equal(2, result.OptimizedCount);
            Assert.Equal(0, result.DeletedFilesCount);
            Assert.True(File.Exists(file1Path));
            Assert.True(File.Exists(file2Path));
        }

        /// <summary>
        /// しきい値が低い場合、似た音源もマージされることを検証。
        /// </summary>
        [Fact]
        public async Task ExecuteDefinitionReductionAsync_LowThreshold_MergesSimilarFiles()
        {
            using var context = new BmsTestContext();

            var file1Path = Path.Combine(context.TempDirectory, "base.wav");
            var file2Path = Path.Combine(context.TempDirectory, "similar.wav");

            BmsTestWavHelper.CreateValidWavFile(file1Path, isDifferent: false);
            BmsTestWavHelper.CreateValidWavFile(file2Path, isDifferent: false); // 同一周波数

            var bmsPath = context.CreateBuilder()
                .WithHeader("GENRE", "Test")
                .WithWav(1, "base.wav", false) // Use existing file
                .AddMainData(11, "01")
                .Build("test_low_threshold.bms");

            var outputPath = Path.Combine(context.TempDirectory, "output_low_threshold.bms");

            var fileList = new List<BmsAudioFile>
            {
                new() { Name = file1Path, NumInteger = 1, Num = "01", FileSize = new FileInfo(file1Path).Length },
                new() { Name = file2Path, NumInteger = 2, Num = "02", FileSize = new FileInfo(file2Path).Length }
            };

            var service = new BmsOptimizationService();

            // 低いしきい値（0.5）では似た音源はマージされる
            var result = await service.ExecuteDefinitionReductionAsync(
                fileList,
                bmsPath,
                outputPath,
                new DefinitionReductionOptions
                {
                    R2Threshold = 0.5f, // 緩いしきい値
                    StartDefinition = 1,
                    EndDefinition = 2,
                    IsPhysicalDeletionEnabled = true
                });

            Assert.True(result.IsSuccess, $"処理失敗: {result.ErrorMessage}");
            // 同一周波数のファイルはマージされる
            Assert.Equal(1, result.OptimizedCount);
            Assert.True(result.DeletedFilesCount >= 1);
        }

        #endregion
    }
}
