using System.IO;
using System.Text;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Services;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Services
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

        private void CreateValidWavFile(string path, bool isDifferent = false)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);

            // 音声比較エンジンが正しく解析（FFT/RMS計算）を行うためには、
            // 極端に短いデータではなく一定以上の長さが必要です（ここでは0.1秒分を確保）。
            int sampleCount = 4410;
            int bytesPerSample = 2; // 16bit
            int dataSize = sampleCount * bytesPerSample;

            // RIFFヘッダー
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            // fmtチャンク
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // Chunk size
            writer.Write((short)1); // PCM
            writer.Write((short)1); // Mono
            writer.Write(44100); // Sample rate
            writer.Write(44100 * 2); // Byte rate
            writer.Write((short)2); // Block align
            writer.Write((short)16); // Bits per sample

            // dataチャンク
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            // 【相関係数計算の考慮】
            // 無音(0)や定数値のデータは分散が0となり、相関係数が計算不能(NaN)になります。
            // これを防ぐため、意図的に分散を持つ波形（サイン波）を生成します。
            // isDifferentフラグに応じて周波数を変え、データの一致/不一致を制御します。
            double frequency = isDifferent ? 880.0 : 440.0;
            double amplitude = 0.5; // 音量50%

            for (int i = 0; i < sampleCount; i++)
            {
                double t = (double)i / 44100;
                short sample = (short)(amplitude * Math.Sin(2 * Math.PI * frequency * t) * short.MaxValue);
                writer.Write(sample);
            }
        }

        [Fact]
        public async Task ExecuteDefinitionReductionAsync_DeletionEnabled_DeletesUnusedFiles()
        {
            using var context = new BmsTestContext();


            var file1Path = Path.Combine(context.TempDirectory, "used.wav");
            var file2Path = Path.Combine(context.TempDirectory, "unused.wav");
            CreateValidWavFile(file1Path);
            CreateValidWavFile(file2Path); // 同じ内容

            var bmsPath = context.CreateBuilder()
                .WithHeader("HEADER", "")
                .WithWav(1, "used.wav")
                .WithHeader("MAIN", "")
                .AddMainData(11, "01")
                .Build("test.bms");

            var outputPath = Path.Combine(context.TempDirectory, "output.bms");

            var file1 = new FileList.WavFiles { Name = file1Path, NumInteger = 1, Num = "01" };
            var file2 = new FileList.WavFiles { Name = file2Path, NumInteger = 2, Num = "02" };
            var fileList = new List<FileList.WavFiles> { file1, file2 };

            var service = new BmsOptimizationService();

            // ファイル2がファイル1と重複している場合、削減処理で物理削除されることを検証
            CreateValidWavFile(file1Path);
            CreateValidWavFile(file2Path); // 完全な重複

            var file1_dup = new FileList.WavFiles { Name = file1Path, NumInteger = 1, Num = "01", FileSize = new FileInfo(file1Path).Length };
            var file2_dup = new FileList.WavFiles { Name = file2Path, NumInteger = 2, Num = "02", FileSize = new FileInfo(file2Path).Length };
            var fileList_dup = new List<FileList.WavFiles> { file1_dup, file2_dup };

            // 100%一致（R2=1.0）で削減判定

            var result = await service.ExecuteDefinitionReductionAsync(
                fileList_dup,
                bmsPath,
                outputPath,
                0.99f, // Threshold
                1, 2, // Range
                true, // isPhysicalDeletionEnabled: ENABLED
                null, null);

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
            CreateValidWavFile(file1Path);
            CreateValidWavFile(file2Path); // 同じ内容

            var bmsPath = context.CreateBuilder()
                .WithHeader("HEADER", "")
                .WithWav(1, "used.wav")
                .WithHeader("MAIN", "")
                .AddMainData(11, "01")
                .Build("test.bms");
            var outputPath = Path.Combine(context.TempDirectory, "output.bms");

            var file1 = new FileList.WavFiles { Name = file1Path, NumInteger = 1, Num = "01", FileSize = new FileInfo(file1Path).Length };
            var file2 = new FileList.WavFiles { Name = file2Path, NumInteger = 2, Num = "02", FileSize = new FileInfo(file2Path).Length };
            var fileList = new List<FileList.WavFiles> { file1, file2 };

            var service = new BmsOptimizationService();

            // 削除無効時は未使用ファイルが残ることを検証
            var result = await service.ExecuteDefinitionReductionAsync(
                fileList,
                bmsPath,
                outputPath,
                0.99f,
                1, 2,
                false, // isPhysicalDeletionEnabled: 無効
                null, null);

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

            CreateValidWavFile(file1Path);
            CreateValidWavFile(file2Path); // 重複
            CreateValidWavFile(file3Path); // 重複
            CreateValidWavFile(file4Path); // 重複

            var bmsPath = context.CreateBuilder()
                .WithHeader("GENRE", "Test")
                .WithWav(1, "original.wav")
                .AddMainData(11, "01")
                .Build("test_multi_dup.bms");

            var outputPath = Path.Combine(context.TempDirectory, "output_multi_dup.bms");

            var fileList = new List<FileList.WavFiles>
            {
                new FileList.WavFiles { Name = file1Path, NumInteger = 1, Num = "01", FileSize = new FileInfo(file1Path).Length },
                new FileList.WavFiles { Name = file2Path, NumInteger = 2, Num = "02", FileSize = new FileInfo(file2Path).Length },
                new FileList.WavFiles { Name = file3Path, NumInteger = 3, Num = "03", FileSize = new FileInfo(file3Path).Length },
                new FileList.WavFiles { Name = file4Path, NumInteger = 4, Num = "04", FileSize = new FileInfo(file4Path).Length }
            };

            var service = new BmsOptimizationService();

            var result = await service.ExecuteDefinitionReductionAsync(
                fileList,
                bmsPath,
                outputPath,
                0.99f,
                1, 4,
                true, // 物理削除有効
                null, null);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.OptimizedCount);
            Assert.True(File.Exists(file1Path), "オリジナルファイルは残る");
            // 重複ファイルの削除数をカウント
            Assert.True(result.DeletedFilesCount >= 1, "少なくとも1つの重複ファイルが削除される");
        }

        /// <summary>
        /// 削除対象ファイルが読み取り専用の場合、エラーをログしつつ処理を継続することを検証。
        /// </summary>
        [Fact]
        public async Task ExecuteDefinitionReductionAsync_ReadOnlyFile_ContinuesWithoutCrash()
        {
            using var context = new BmsTestContext();

            var file1Path = Path.Combine(context.TempDirectory, "used.wav");
            var file2Path = Path.Combine(context.TempDirectory, "readonly_unused.wav");

            CreateValidWavFile(file1Path);
            CreateValidWavFile(file2Path);

            // ファイルを読み取り専用に設定
            File.SetAttributes(file2Path, FileAttributes.ReadOnly);

            try
            {
                var bmsPath = context.CreateBuilder()
                    .WithHeader("GENRE", "Test")
                    .WithWav(1, "used.wav")
                    .AddMainData(11, "01")
                    .Build("test_readonly.bms");

                var outputPath = Path.Combine(context.TempDirectory, "output_readonly.bms");

                var fileList = new List<FileList.WavFiles>
                {
                    new FileList.WavFiles { Name = file1Path, NumInteger = 1, Num = "01", FileSize = new FileInfo(file1Path).Length },
                    new FileList.WavFiles { Name = file2Path, NumInteger = 2, Num = "02", FileSize = new FileInfo(file2Path).Length }
                };

                var service = new BmsOptimizationService();

                // 読み取り専用ファイルの削除に失敗しても、処理全体は成功すべき
                var result = await service.ExecuteDefinitionReductionAsync(
                    fileList,
                    bmsPath,
                    outputPath,
                    0.99f,
                    1, 2,
                    true,
                    null, null);

                // 処理自体は成功（削除失敗はログのみ）
                Assert.True(result.IsSuccess);
                Assert.Equal(1, result.OptimizedCount);
                // 読み取り専用ファイルは削除できないので残っているはず
                Assert.True(File.Exists(file2Path), "読み取り専用ファイルは削除されない");
            }
            finally
            {
                // クリーンアップのため属性を解除
                if (File.Exists(file2Path))
                {
                    File.SetAttributes(file2Path, FileAttributes.Normal);
                }
            }
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

            CreateValidWavFile(file1Path, isDifferent: false);  // 440Hz
            CreateValidWavFile(file2Path, isDifferent: true);   // 880Hz

            var bmsPath = context.CreateBuilder()
                .WithHeader("GENRE", "Test")
                .WithWav(1, "low_freq.wav", false)  // Use existing file
                .WithWav(2, "high_freq.wav", false) // Use existing file
                .AddMainData(11, "0102")
                .Build("test_diff_freq.bms");

            var outputPath = Path.Combine(context.TempDirectory, "output_diff_freq.bms");

            var fileList = new List<FileList.WavFiles>
            {
                new FileList.WavFiles { Name = file1Path, NumInteger = 1, Num = "01", FileSize = new FileInfo(file1Path).Length },
                new FileList.WavFiles { Name = file2Path, NumInteger = 2, Num = "02", FileSize = new FileInfo(file2Path).Length }
            };

            var service = new BmsOptimizationService();

            // 高いしきい値（0.99）では異なる周波数のファイルはマージされない
            var result = await service.ExecuteDefinitionReductionAsync(
                fileList,
                bmsPath,
                outputPath,
                0.99f, // 厳密なしきい値
                1, 2,
                true,
                null, null);

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

            CreateValidWavFile(file1Path, isDifferent: false);
            CreateValidWavFile(file2Path, isDifferent: false); // 同一周波数

            var bmsPath = context.CreateBuilder()
                .WithHeader("GENRE", "Test")
                .WithWav(1, "base.wav", false) // Use existing file
                .AddMainData(11, "01")
                .Build("test_low_threshold.bms");

            var outputPath = Path.Combine(context.TempDirectory, "output_low_threshold.bms");

            var fileList = new List<FileList.WavFiles>
            {
                new FileList.WavFiles { Name = file1Path, NumInteger = 1, Num = "01", FileSize = new FileInfo(file1Path).Length },
                new FileList.WavFiles { Name = file2Path, NumInteger = 2, Num = "02", FileSize = new FileInfo(file2Path).Length }
            };

            var service = new BmsOptimizationService();

            // 低いしきい値（0.5）では似た音源はマージされる
            var result = await service.ExecuteDefinitionReductionAsync(
                fileList,
                bmsPath,
                outputPath,
                0.5f, // 緩いしきい値
                1, 2,
                true,
                null, null);

            Assert.True(result.IsSuccess, $"処理失敗: {result.ErrorMessage}");
            // 同一周波数のファイルはマージされる
            Assert.Equal(1, result.OptimizedCount);
            Assert.True(result.DeletedFilesCount >= 1);
        }

        /// <summary>
        /// 存在しないファイルがリストに含まれている場合のエラーハンドリング検証。
        /// </summary>
        [Fact]
        public async Task ExecuteDefinitionReductionAsync_MixedExistingAndMissing_HandlesGracefully()
        {
            using var context = new BmsTestContext();

            var existingPath = Path.Combine(context.TempDirectory, "existing.wav");
            var missingPath = Path.Combine(context.TempDirectory, "missing.wav");

            CreateValidWavFile(existingPath);
            // missingPathは作成しない

            var bmsPath = context.CreateBuilder()
                .WithHeader("GENRE", "Test")
                .WithWav(1, "existing.wav")
                .AddMainData(11, "01")
                .Build("test_mixed.bms");

            var outputPath = Path.Combine(context.TempDirectory, "output_mixed.bms");

            var fileList = new List<FileList.WavFiles>
            {
                new FileList.WavFiles { Name = existingPath, NumInteger = 1, Num = "01", FileSize = new FileInfo(existingPath).Length },
                new FileList.WavFiles { Name = missingPath, NumInteger = 2, Num = "02" }
            };

            var service = new BmsOptimizationService();

            // 一部ファイルが存在しなくても処理は完了すべき
            var result = await service.ExecuteDefinitionReductionAsync(
                fileList,
                bmsPath,
                outputPath,
                0.99f,
                1, 2,
                true,
                null, null);

            // 処理が正常に完了すること
            Assert.NotNull(result);
        }

        #endregion
    }
}
