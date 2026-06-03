using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Optimization
{
    /// <summary>
    /// BmsOptimizationService の正常系動作検証テスト。
    /// </summary>
    public class BmsOptimizationServiceTests : IDisposable
    {
        private readonly BmsTestContext _context;
        private readonly BmsOptimizationService _service;
        private bool _disposed;

        public BmsOptimizationServiceTests()
        {
            _context = new BmsTestContext();
            _service = new BmsOptimizationService();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _context?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private async Task RunOptimalThresholdsTestAsync(Func<string, List<string>> setupFiles, Action<OptimizationResult?> assertResult, int startDef = 1, int endDef = 1, IProgress<int>? progress = null)
        {
            var files = setupFiles?.Invoke(_context.TempDirectory) ?? [];
            var result = await _service.FindOptimalThresholdsAsync(files, startDef, endDef, progress);
            assertResult?.Invoke(result);
        }

        private async Task RunDefinitionReductionTestAsync(ReductionTestOptions options)
        {
            var builder = _context.CreateBuilder();
            options.BuildBms?.Invoke(builder);
            string inputBmsPath = builder.Build("input.bms");
            string outputBmsPath = Path.Combine(_context.TempDirectory, "output.bms");
            var files = options.CreateFiles?.Invoke(_context.TempDirectory) ?? [];

            var result = await _service.ExecuteDefinitionReductionAsync(
                files,
                inputBmsPath,
                outputBmsPath,
                new DefinitionReductionOptions
                {
                    R2Threshold = options.Threshold ?? 0.5f,
                    StartDefinition = options.StartDef,
                    EndDefinition = options.EndDef,
                    IsPhysicalDeletionEnabled = options.PhysicalDeletion,
                    SelectedKeywords = options.Keywords
                });
            options.AssertResult?.Invoke(result);
        }

        /// <summary>
        /// FindOptimalThresholdsAsync において、条件 ValidFiles の場合に ReturnsResult されることを検証します。
        /// </summary>
        [Fact]
        public Task FindOptimalThresholdsAsync_ValidFiles_ReturnsResult() =>
            RunOptimalThresholdsTestAsync(
                dir => { _context.CreateBuilder().WithWav(1, "test1.wav"); return [Path.Combine(dir, "test1.wav")]; },
                res => { Assert.NotNull(res); Assert.NotEmpty(res.SimulationData); Assert.True(res.Base36Result.Count > 0); }
            );

        /// <summary>
        /// FindOptimalThresholdsAsync において、条件 EmptyList の場合に ReturnsNull されることを検証します。
        /// </summary>
        [Fact]
        public async Task FindOptimalThresholdsAsync_EmptyList_ReturnsNull() =>
            _ = await Assert.ThrowsAsync<ArgumentException>(() => _service.FindOptimalThresholdsAsync([], 1, 1));

        /// <summary>
        /// FindOptimalThresholdsAsync において、条件 NoValidFiles の場合に ReturnsNull されることを検証します。
        /// </summary>
        [Fact]
        public Task FindOptimalThresholdsAsync_NoValidFiles_ReturnsNull() =>
            RunOptimalThresholdsTestAsync(_ => ["nonexistent.wav"], Assert.Null);

        /// <summary>
        /// ExecuteDefinitionReductionAsync において、条件 WithPhysicalDeletion の場合に OnlyDeletesUnusedFiles されることを検証します。
        /// </summary>
        [Fact]
        public Task ExecuteDefinitionReductionAsync_WithPhysicalDeletion_OnlyDeletesUnusedFiles() =>
            RunDefinitionReductionTestAsync(new()
            {
                BuildBms = b => b.WithHeader("GENRE", "Test").WithWav(1, "used1.wav").WithWav(2, "used2.wav").WithWav(3, "unused1.wav").WithWav(4, "unused2.wav").AddMainData(0, 11, "0102"),
                CreateFiles = dir => [
                    new() { Name = Path.Combine(dir, "used1.wav"), Num = "01", NumInteger = 1 },
                    new() { Name = Path.Combine(dir, "used2.wav"), Num = "02", NumInteger = 2 },
                    new() { Name = Path.Combine(dir, "unused1.wav"), Num = "03", NumInteger = 3 },
                    new() { Name = Path.Combine(dir, "unused2.wav"), Num = "04", NumInteger = 4 }
                ],
                AssertResult = res =>
                {
                    Assert.True(res.IsSuccess);
                    Assert.True(File.Exists(Path.Combine(_context.TempDirectory, "used1.wav")));
                    Assert.True(File.Exists(Path.Combine(_context.TempDirectory, "used2.wav")));
                },
                EndDef = 4,
                PhysicalDeletion = true
            });

        /// <summary>
        /// ExecuteDefinitionReductionAsync において、条件 WithValidInput の場合に CalculatesReductionRate されることを検証します。
        /// </summary>
        [Fact]
        public Task ExecuteDefinitionReductionAsync_WithValidInput_CalculatesReductionRate() =>
            RunDefinitionReductionTestAsync(new()
            {
                BuildBms = b => b.WithHeader("GENRE", "Test").WithWav(1, "test1.wav").AddMainData(0, 11, "01"),
                CreateFiles = dir => [new() { Name = Path.Combine(dir, "test1.wav"), Num = "01", NumInteger = 1 }],
                AssertResult = res =>
                {
                    Assert.NotNull(res);
                    Assert.True(res.OriginalCount > 0);
                    Assert.True(res.ReductionRate >= 0 && res.ReductionRate <= 1.0);
                    Assert.True(res.ProcessingTime.TotalMilliseconds >= 0);
                }
            });

        /// <summary>
        /// FindOptimalThresholdsAsync において、条件 PartialFileNotFound の場合に ProcessesValidFiles されることを検証します。
        /// </summary>
        [Fact]
        public Task FindOptimalThresholdsAsync_PartialFileNotFound_ProcessesValidFiles() =>
            RunOptimalThresholdsTestAsync(
                dir => { _context.CreateBuilder().WithWav(1, "valid.wav"); return [Path.Combine(dir, "valid.wav"), "nonexistent1.wav", "nonexistent2.wav"]; },
                res => { Assert.NotNull(res); Assert.NotEmpty(res.SimulationData); },
                endDef: 3
            );

        /// <summary>
        /// FindOptimalThresholdsAsync において、条件 EndDefinitionZero の場合に AutoDetectsEndDefinition されることを検証します。
        /// </summary>
        [Fact]
        public Task FindOptimalThresholdsAsync_EndDefinitionZero_AutoDetectsEndDefinition() =>
            RunOptimalThresholdsTestAsync(
                dir => { _context.CreateBuilder().WithWav(1, "test1.wav").WithWav(2, "test2.wav"); return [Path.Combine(dir, "test1.wav"), Path.Combine(dir, "test2.wav")]; },
                res => { Assert.NotNull(res); Assert.NotEmpty(res.SimulationData); },
                endDef: 0
            );

        /// <summary>
        /// FindOptimalThresholdsAsync において、条件 WithProgress の場合に ReportsProgress されることを検証します。
        /// </summary>
        [Fact]
        public Task FindOptimalThresholdsAsync_WithProgress_ReportsProgress()
        {
            var progressValues = new List<int>();
            return RunOptimalThresholdsTestAsync(
                dir => { _context.CreateBuilder().WithWav(1, "test1.wav"); return [Path.Combine(dir, "test1.wav")]; },
                _ => Assert.NotEmpty(progressValues),
                progress: new Progress<int>(p => progressValues.Add(p))
            );
        }

        /// <summary>
        /// FindOptimalThresholdsAsync において、条件 VariousRanges の場合に ProcessesCorrectly されることを検証します。
        /// </summary>
        [Theory]
        [InlineData(1, 10)]
        [InlineData(1, 100)]
        [InlineData(10, 50)]
        public Task FindOptimalThresholdsAsync_VariousRanges_ProcessesCorrectly(int start, int end) =>
            RunOptimalThresholdsTestAsync(
                dir => { _context.CreateBuilder().WithWav(1, $"test_{start}_{end}.wav"); return [Path.Combine(dir, $"test_{start}_{end}.wav")]; },
                Assert.NotNull,
                startDef: start, endDef: end
            );

        /// <summary>
        /// ExecuteDefinitionReductionAsync において、条件 WithSelectedKeywords の場合に ProcessesFilteredFiles されることを検証します。
        /// </summary>
        [Fact]
        public Task ExecuteDefinitionReductionAsync_WithSelectedKeywords_ProcessesFilteredFiles() =>
            RunDefinitionReductionTestAsync(new()
            {
                BuildBms = b => b.WithHeader("GENRE", "Test").WithWav(1, "kick.wav").WithWav(2, "snare.wav").AddMainData(0, 11, "0102"),
                CreateFiles = dir => [
                    new() { Name = Path.Combine(dir, "kick.wav"), Num = "01", NumInteger = 1 },
                    new() { Name = Path.Combine(dir, "snare.wav"), Num = "02", NumInteger = 2 }
                ],
                AssertResult = Assert.NotNull,
                EndDef = 2,
                Keywords = ["kick"]
            });
    }
}
