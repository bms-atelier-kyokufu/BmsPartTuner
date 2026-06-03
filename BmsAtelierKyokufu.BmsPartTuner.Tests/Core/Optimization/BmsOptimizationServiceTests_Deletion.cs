using System.IO;
using System.Text;
using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Optimization
{
    /// <summary>
    /// BmsOptimizationService の動作検証テスト。
    /// 特にファイルの削除ロジックと定義削減の整合性を確認します。
    /// </summary>
    public class BmsOptimizationServiceTests_Deletion : BmsOptimizationServiceTestBase
    {
        public BmsOptimizationServiceTests_Deletion()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// ExecuteDefinitionReductionAsync において、条件 DeletionEnabled の場合に DeletesUnusedFiles されることを検証します。
        /// </summary>
        [Fact]
        public Task ExecuteDefinitionReductionAsync_DeletionEnabled_DeletesUnusedFiles() =>
            RunDefinitionReductionTestAsync(new()
            {
                InputBmsName = "test.bms",
                BuildBms = b => b.WithHeader("HEADER", "").WithWav(1, "used.wav", false).AddMainData(11, "01"),
                CreateFiles = dir =>
                {
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "used.wav"));
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "unused.wav"));
                    return [
                        new() { Name = Path.Combine(dir, "used.wav"), NumInteger = 1, Num = "01", FileSize = new FileInfo(Path.Combine(dir, "used.wav")).Length },
                        new() { Name = Path.Combine(dir, "unused.wav"), NumInteger = 2, Num = "02", FileSize = new FileInfo(Path.Combine(dir, "unused.wav")).Length }
                    ];
                },
                Threshold = 0.99f,
                StartDef = 1,
                EndDef = 2,
                PhysicalDeletion = true,
                AssertResult = res =>
                {
                    Assert.True(res.IsSuccess);
                    Assert.Equal(1, res.OptimizedCount);
                    Assert.Equal(1, res.DeletedFilesCount);
                    Assert.True(File.Exists(Path.Combine(Context.TempDirectory, "used.wav")));
                    Assert.False(File.Exists(Path.Combine(Context.TempDirectory, "unused.wav")));
                }
            });

        /// <summary>
        /// ExecuteDefinitionReductionAsync において、条件 DeletionDisabled の場合に KeepsUnusedFiles されることを検証します。
        /// </summary>
        [Fact]
        public Task ExecuteDefinitionReductionAsync_DeletionDisabled_KeepsUnusedFiles() =>
            RunDefinitionReductionTestAsync(new()
            {
                InputBmsName = "test.bms",
                BuildBms = b => b.WithHeader("HEADER", "").WithWav(1, "used.wav", false).AddMainData(11, "01"),
                CreateFiles = dir =>
                {
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "used.wav"));
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "unused.wav"));
                    return [
                        new() { Name = Path.Combine(dir, "used.wav"), NumInteger = 1, Num = "01", FileSize = new FileInfo(Path.Combine(dir, "used.wav")).Length },
                        new() { Name = Path.Combine(dir, "unused.wav"), NumInteger = 2, Num = "02", FileSize = new FileInfo(Path.Combine(dir, "unused.wav")).Length }
                    ];
                },
                Threshold = 0.99f,
                StartDef = 1,
                EndDef = 2,
                PhysicalDeletion = false,
                AssertResult = res =>
                {
                    Assert.True(res.IsSuccess);
                    Assert.Equal(1, res.OptimizedCount);
                    Assert.Equal(0, res.DeletedFilesCount);
                    Assert.True(File.Exists(Path.Combine(Context.TempDirectory, "used.wav")));
                    Assert.True(File.Exists(Path.Combine(Context.TempDirectory, "unused.wav")));
                }
            });

        /// <summary>
        /// ExecuteDefinitionReductionAsync において、条件 MultipleDuplicates の場合に DeletesAllUnused されることを検証します。
        /// </summary>
        [Fact]
        public Task ExecuteDefinitionReductionAsync_MultipleDuplicates_DeletesAllUnused() =>
            RunDefinitionReductionTestAsync(new()
            {
                InputBmsName = "test.bms",
                BuildBms = b => b.WithHeader("GENRE", "Test").WithWav(1, "original.wav", false).AddMainData(11, "01"),
                CreateFiles = dir =>
                {
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "original.wav"));
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "dup1.wav"));
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "dup2.wav"));
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "dup3.wav"));
                    return [
                        new() { Name = Path.Combine(dir, "original.wav"), NumInteger = 1, Num = "01", FileSize = new FileInfo(Path.Combine(dir, "original.wav")).Length },
                        new() { Name = Path.Combine(dir, "dup1.wav"), NumInteger = 2, Num = "02", FileSize = new FileInfo(Path.Combine(dir, "dup1.wav")).Length },
                        new() { Name = Path.Combine(dir, "dup2.wav"), NumInteger = 3, Num = "03", FileSize = new FileInfo(Path.Combine(dir, "dup2.wav")).Length },
                        new() { Name = Path.Combine(dir, "dup3.wav"), NumInteger = 4, Num = "04", FileSize = new FileInfo(Path.Combine(dir, "dup3.wav")).Length }
                    ];
                },
                Threshold = 0.99f,
                StartDef = 1,
                EndDef = 4,
                PhysicalDeletion = true,
                AssertResult = res =>
                {
                    Assert.True(res.IsSuccess);
                    Assert.Equal(1, res.OptimizedCount);
                    Assert.True(File.Exists(Path.Combine(Context.TempDirectory, "original.wav")));
                    Assert.True(res.DeletedFilesCount >= 1);
                }
            });

        /// <summary>
        /// ExecuteDefinitionReductionAsync において、条件 DifferentFrequency の場合に NotMerged されることを検証します。
        /// </summary>
        [Fact]
        public Task ExecuteDefinitionReductionAsync_DifferentFrequency_NotMerged() =>
            RunDefinitionReductionTestAsync(new()
            {
                InputBmsName = "test.bms",
                BuildBms = b => b.WithHeader("GENRE", "Test").WithWav(1, "low_freq.wav", false).WithWav(2, "high_freq.wav", false).AddMainData(11, "0102"),
                CreateFiles = dir =>
                {
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "low_freq.wav"), isDifferent: false);
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "high_freq.wav"), isDifferent: true);
                    return [
                        new() { Name = Path.Combine(dir, "low_freq.wav"), NumInteger = 1, Num = "01", FileSize = new FileInfo(Path.Combine(dir, "low_freq.wav")).Length },
                        new() { Name = Path.Combine(dir, "high_freq.wav"), NumInteger = 2, Num = "02", FileSize = new FileInfo(Path.Combine(dir, "high_freq.wav")).Length }
                    ];
                },
                Threshold = 0.99f,
                StartDef = 1,
                EndDef = 2,
                PhysicalDeletion = true,
                AssertResult = res =>
                {
                    Assert.True(res.IsSuccess);
                    Assert.Equal(2, res.OptimizedCount);
                    Assert.Equal(0, res.DeletedFilesCount);
                    Assert.True(File.Exists(Path.Combine(Context.TempDirectory, "low_freq.wav")));
                    Assert.True(File.Exists(Path.Combine(Context.TempDirectory, "high_freq.wav")));
                }
            });

        /// <summary>
        /// ExecuteDefinitionReductionAsync において、条件 LowThreshold の場合に MergesSimilarFiles されることを検証します。
        /// </summary>
        [Fact]
        public Task ExecuteDefinitionReductionAsync_LowThreshold_MergesSimilarFiles() =>
            RunDefinitionReductionTestAsync(new()
            {
                InputBmsName = "test.bms",
                BuildBms = b => b.WithHeader("GENRE", "Test").WithWav(1, "base.wav", false).AddMainData(11, "01"),
                CreateFiles = dir =>
                {
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "base.wav"), isDifferent: false);
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "similar.wav"), isDifferent: false);
                    return [
                        new() { Name = Path.Combine(dir, "base.wav"), NumInteger = 1, Num = "01", FileSize = new FileInfo(Path.Combine(dir, "base.wav")).Length },
                        new() { Name = Path.Combine(dir, "similar.wav"), NumInteger = 2, Num = "02", FileSize = new FileInfo(Path.Combine(dir, "similar.wav")).Length }
                    ];
                },
                Threshold = 0.5f,
                StartDef = 1,
                EndDef = 2,
                PhysicalDeletion = true,
                AssertResult = res =>
                {
                    Assert.True(res.IsSuccess);
                    Assert.Equal(1, res.OptimizedCount);
                    Assert.True(res.DeletedFilesCount >= 1);
                }
            });
    }
}
