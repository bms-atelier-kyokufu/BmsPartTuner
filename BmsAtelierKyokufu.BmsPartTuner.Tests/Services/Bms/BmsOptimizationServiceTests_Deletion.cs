using System.IO;
using System.Text;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;
using BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;
using static BmsAtelierKyokufu.BmsPartTuner.Core.Optimization.BmsOptimizationService;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Services.Bms
{
    /// <summary>
    /// BmsOptimizationService の動作検証テスト。
    /// 特にファイルの削除ロジックと定義削減の整合性を確認します。
    /// </summary>
    public class BmsOptimizationServiceTests_Deletion : IDisposable
    {
        private readonly BmsTestContext _context;
        private readonly BmsOptimizationService _service;

        public BmsOptimizationServiceTests_Deletion()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _context = new BmsTestContext();
            _service = new BmsOptimizationService();
        }

        public void Dispose()
        {
            _context?.Dispose();
            GC.SuppressFinalize(this);
        }

        private async Task RunDeletionTestAsync(
            Action<string> createFiles,
            Action<BmsFileBuilder> buildBms,
            Func<string, List<BmsAudioFile>> createFileList,
            DefinitionReductionOptions options,
            Action<ReductionResult, string> assertResult)
        {
            createFiles(_context.TempDirectory);

            var builder = _context.CreateBuilder();
            buildBms(builder);
            string bmsPath = builder.Build("test.bms");
            string outputPath = Path.Combine(_context.TempDirectory, "output.bms");

            var fileList = createFileList(_context.TempDirectory);

            var result = await _service.ExecuteDefinitionReductionAsync(
                fileList,
                bmsPath,
                outputPath,
                options);

            assertResult(result, _context.TempDirectory);
        }

        [Fact]
        public Task ExecuteDefinitionReductionAsync_DeletionEnabled_DeletesUnusedFiles() =>
            RunDeletionTestAsync(
                createFiles: dir =>
                {
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "used.wav"));
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "unused.wav")); // Duplicate
                },
                buildBms: b => b.WithHeader("HEADER", "").WithWav(1, "used.wav", false).AddMainData(11, "01"),
                createFileList: dir => [
                    new() { Name = Path.Combine(dir, "used.wav"), NumInteger = 1, Num = "01", FileSize = new FileInfo(Path.Combine(dir, "used.wav")).Length },
                    new() { Name = Path.Combine(dir, "unused.wav"), NumInteger = 2, Num = "02", FileSize = new FileInfo(Path.Combine(dir, "unused.wav")).Length }
                ],
                options: new DefinitionReductionOptions { R2Threshold = 0.99f, StartDefinition = 1, EndDefinition = 2, IsPhysicalDeletionEnabled = true },
                assertResult: (result, dir) =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(1, result.OptimizedCount);
                    Assert.Equal(1, result.DeletedFilesCount);
                    Assert.True(File.Exists(Path.Combine(dir, "used.wav")));
                    Assert.False(File.Exists(Path.Combine(dir, "unused.wav")));
                });

        [Fact]
        public Task ExecuteDefinitionReductionAsync_DeletionDisabled_KeepsUnusedFiles() =>
            RunDeletionTestAsync(
                createFiles: dir =>
                {
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "used.wav"));
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "unused.wav")); // Duplicate
                },
                buildBms: b => b.WithHeader("HEADER", "").WithWav(1, "used.wav", false).AddMainData(11, "01"),
                createFileList: dir => [
                    new() { Name = Path.Combine(dir, "used.wav"), NumInteger = 1, Num = "01", FileSize = new FileInfo(Path.Combine(dir, "used.wav")).Length },
                    new() { Name = Path.Combine(dir, "unused.wav"), NumInteger = 2, Num = "02", FileSize = new FileInfo(Path.Combine(dir, "unused.wav")).Length }
                ],
                options: new DefinitionReductionOptions { R2Threshold = 0.99f, StartDefinition = 1, EndDefinition = 2, IsPhysicalDeletionEnabled = false },
                assertResult: (result, dir) =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(1, result.OptimizedCount);
                    Assert.Equal(0, result.DeletedFilesCount);
                    Assert.True(File.Exists(Path.Combine(dir, "used.wav")));
                    Assert.True(File.Exists(Path.Combine(dir, "unused.wav")));
                });

        [Fact]
        public Task ExecuteDefinitionReductionAsync_MultipleDuplicates_DeletesAllUnused() =>
            RunDeletionTestAsync(
                createFiles: dir =>
                {
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "original.wav"));
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "dup1.wav"));
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "dup2.wav"));
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "dup3.wav"));
                },
                buildBms: b => b.WithHeader("GENRE", "Test").WithWav(1, "original.wav", false).AddMainData(11, "01"),
                createFileList: dir => [
                    new() { Name = Path.Combine(dir, "original.wav"), NumInteger = 1, Num = "01", FileSize = new FileInfo(Path.Combine(dir, "original.wav")).Length },
                    new() { Name = Path.Combine(dir, "dup1.wav"), NumInteger = 2, Num = "02", FileSize = new FileInfo(Path.Combine(dir, "dup1.wav")).Length },
                    new() { Name = Path.Combine(dir, "dup2.wav"), NumInteger = 3, Num = "03", FileSize = new FileInfo(Path.Combine(dir, "dup2.wav")).Length },
                    new() { Name = Path.Combine(dir, "dup3.wav"), NumInteger = 4, Num = "04", FileSize = new FileInfo(Path.Combine(dir, "dup3.wav")).Length }
                ],
                options: new DefinitionReductionOptions { R2Threshold = 0.99f, StartDefinition = 1, EndDefinition = 4, IsPhysicalDeletionEnabled = true },
                assertResult: (result, dir) =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(1, result.OptimizedCount);
                    Assert.True(File.Exists(Path.Combine(dir, "original.wav")));
                    Assert.True(result.DeletedFilesCount >= 1);
                });

        [Fact]
        public Task ExecuteDefinitionReductionAsync_DifferentFrequency_NotMerged() =>
            RunDeletionTestAsync(
                createFiles: dir =>
                {
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "low_freq.wav"), isDifferent: false);
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "high_freq.wav"), isDifferent: true);
                },
                buildBms: b => b.WithHeader("GENRE", "Test").WithWav(1, "low_freq.wav", false).WithWav(2, "high_freq.wav", false).AddMainData(11, "0102"),
                createFileList: dir => [
                    new() { Name = Path.Combine(dir, "low_freq.wav"), NumInteger = 1, Num = "01", FileSize = new FileInfo(Path.Combine(dir, "low_freq.wav")).Length },
                    new() { Name = Path.Combine(dir, "high_freq.wav"), NumInteger = 2, Num = "02", FileSize = new FileInfo(Path.Combine(dir, "high_freq.wav")).Length }
                ],
                options: new DefinitionReductionOptions { R2Threshold = 0.99f, StartDefinition = 1, EndDefinition = 2, IsPhysicalDeletionEnabled = true },
                assertResult: (result, dir) =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(2, result.OptimizedCount);
                    Assert.Equal(0, result.DeletedFilesCount);
                    Assert.True(File.Exists(Path.Combine(dir, "low_freq.wav")));
                    Assert.True(File.Exists(Path.Combine(dir, "high_freq.wav")));
                });

        [Fact]
        public Task ExecuteDefinitionReductionAsync_LowThreshold_MergesSimilarFiles() =>
            RunDeletionTestAsync(
                createFiles: dir =>
                {
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "base.wav"), isDifferent: false);
                    BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "similar.wav"), isDifferent: false); // Same frequency
                },
                buildBms: b => b.WithHeader("GENRE", "Test").WithWav(1, "base.wav", false).AddMainData(11, "01"),
                createFileList: dir => [
                    new() { Name = Path.Combine(dir, "base.wav"), NumInteger = 1, Num = "01", FileSize = new FileInfo(Path.Combine(dir, "base.wav")).Length },
                    new() { Name = Path.Combine(dir, "similar.wav"), NumInteger = 2, Num = "02", FileSize = new FileInfo(Path.Combine(dir, "similar.wav")).Length }
                ],
                options: new DefinitionReductionOptions { R2Threshold = 0.5f, StartDefinition = 1, EndDefinition = 2, IsPhysicalDeletionEnabled = true },
                assertResult: (result, dir) =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(1, result.OptimizedCount);
                    Assert.True(result.DeletedFilesCount >= 1);
                });
    }
}

