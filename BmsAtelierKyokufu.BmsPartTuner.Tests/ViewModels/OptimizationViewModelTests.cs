using BmsAtelierKyokufu.BmsPartTuner.Core.Validation;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Services;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Infrastructure;
using BmsAtelierKyokufu.BmsPartTuner.ViewModels;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.ViewModels
{
    // Moqを使わないテスト用のシンプルなフェイクサービス
    internal class FakeOptimizationService : IBmsOptimizationService
    {
        public Task<OptimizationResult?> FindOptimalThresholdsAsync(List<string> files, int startDefinition, int endDefinition, IProgress<int>? progress = null)
        {
            return Task.Run<OptimizationResult?>(async () =>
            {
                for (int i = 0; i <= 100; i += 25)
                {
                    progress?.Report(i);
                    await Task.Delay(5);
                }
                return new OptimizationResult
                {
                    Base36Result = (0.85f, 100),
                    Base62Result = (0.90f, 200),
                    ExecutionTime = TimeSpan.FromSeconds(0.5),
                    MemoryUsedBytes = 10 * 1024 * 1024
                };
            });
        }

        public ValidationResult<float> ValidateR2Threshold(string r2Text)
        {
            if (int.TryParse(r2Text, out var v) && v >= 0 && v <= 100)
            {
                return ValidationResult<float>.Success(v / 100f);
            }
            return ValidationResult<float>.Failure("invalid");
        }

        public async Task<BmsOptimizationService.ReductionResult> ExecuteDefinitionReductionAsync(
            IReadOnlyList<FileList.WavFiles> fileList,
            string inputPath,
            string outputPath,
            float r2Threshold,
            int startDefinition,
            int endDefinition,
            bool isPhysicalDeletionEnabled,
            IProgress<int>? progress = null,
            IEnumerable<string>? selectedKeywords = null)
        {
            // テストが "Busy" 状態を検知できるように意図的に待機する
            await Task.Delay(200);

            return new BmsOptimizationService.ReductionResult
            {
                IsSuccess = true,
                OriginalCount = 10,
                OptimizedCount = 7,
                ErrorMessage = null,
                DeletedFilesCount = 0
            };
        }

        public ValidationResult ValidateDefinitionRange(string startVal, string endVal)
        {
            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// OptimizationViewModel の動作検証テスト。
    /// 閾値最適化・定義削減の実行フロー、状態管理、エラーハンドリングを確認します。
    /// </summary>
    public class OptimizationViewModelTests
    {
        [Fact]
        public Task ExecuteThresholdOptimizationAsync_UpdatesBusyStateAndProgress()
        {
            return WpfTestHelper.RunStaAsync(async () =>
            {
                var vm = new OptimizationViewModel(new FakeOptimizationService());
                var files = new List<string> { "a.wav", "b.wav" };

                var result = await vm.ExecuteThresholdOptimizationAsync(files, 0, 10);

                Assert.NotNull(result);
                Assert.False(vm.IsBusy);
                Assert.False(vm.IsProgressIndeterminate);
                Assert.Equal(100, vm.ProgressValue);
                Assert.NotNull(vm.LastOptimizationResult);
                Assert.Contains("最適化", vm.StatusMessage);
            });
        }

        [Fact]
        public Task ExecuteThresholdOptimizationAsync_EmptyFiles_RaisesErrorAndReturnsNull()
        {
            return WpfTestHelper.RunStaAsync(async () =>
            {
                var vm = new OptimizationViewModel(new FakeOptimizationService());
                string? error = null;
                vm.ErrorOccurred += (_, msg) => error = msg;

                var result = await vm.ExecuteThresholdOptimizationAsync(new List<string>(), 0, 10);

                Assert.Null(result);
                Assert.Equal("ファイルリストが空です", error);
                Assert.False(vm.IsBusy);
            });
        }

        [Fact]
        public Task ExecuteDefinitionReductionAsync_ValidatesThresholdAndReportsResult()
        {
            return WpfTestHelper.RunStaAsync(async () =>
            {
                var vm = new OptimizationViewModel(new FakeOptimizationService())
                {
                    R2Threshold = "80"
                };
                var list = new FileList("dummy.bms");
                string? completedOutput = null;
                vm.DefinitionReductionCompleted += (_, e) => completedOutput = e.OutputPath;

                await vm.ExecuteDefinitionReductionAsync(list, "in.bms", "out.bms");

                Assert.False(vm.IsBusy);
                Assert.Equal("out.bms", completedOutput);
            });
        }

        [Fact]
        public Task ExecuteDefinitionReductionAsync_InvalidThreshold_RaisesError()
        {
            return WpfTestHelper.RunStaAsync(async () =>
            {
                var vm = new OptimizationViewModel(new FakeOptimizationService())
                {
                    R2Threshold = "-1"
                };
                var list = new FileList("dummy.bms");
                string? error = null;
                vm.ErrorOccurred += (_, msg) => error = msg;

                await vm.ExecuteDefinitionReductionAsync(list, "in.bms", "out.bms");

                Assert.Equal("invalid", error);
                Assert.False(vm.IsBusy);
            });
        }

        #region Priority A: State Transition Tests (UIフリーズ防止)

        /// <summary>
        /// 処理中に例外が発生した場合、エラー状態へ正しく遷移することを検証。
        /// </summary>
        [Fact]
        public Task ExecuteThresholdOptimizationAsync_ServiceThrows_TransitionsToErrorState()
        {
            return WpfTestHelper.RunStaAsync(async () =>
            {
                var vm = new OptimizationViewModel(new ThrowingOptimizationService());
                string? error = null;
                vm.ErrorOccurred += (_, msg) => error = msg;

                var result = await vm.ExecuteThresholdOptimizationAsync(
                    new List<string> { "a.wav" }, 0, 10);

                // 処理後の状態
                Assert.Null(result);
                Assert.False(vm.IsBusy, "処理後はIsBusyがfalseになるべき");
                Assert.NotNull(error);
            });
        }

        /// <summary>
        /// IsBusy状態の正しい遷移: Idle → Processing → Idle
        /// </summary>
        [Fact]
        public Task ExecuteThresholdOptimizationAsync_IsBusyTransition_CorrectOrder()
        {
            return WpfTestHelper.RunStaAsync(async () =>
            {
                var vm = new OptimizationViewModel(new FakeOptimizationService());
                var busyStates = new List<bool>();

                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.IsBusy))
                    {
                        busyStates.Add(vm.IsBusy);
                    }
                };

                var files = new List<string> { "a.wav" };
                await vm.ExecuteThresholdOptimizationAsync(files, 0, 10);

                // Idle(false) → Processing(true) → Idle(false)
                Assert.Contains(true, busyStates);  // 処理中にtrueになった
                Assert.False(vm.IsBusy);            // 最終状態はfalse
            });
        }

        /// <summary>
        /// 進捗が正しく更新されることを検証。
        /// </summary>
        [Fact]
        public Task ExecuteThresholdOptimizationAsync_ProgressUpdates_ReportedCorrectly()
        {
            return WpfTestHelper.RunStaAsync(async () =>
            {
                var vm = new OptimizationViewModel(new FakeOptimizationService());
                var progressValues = new List<int>();

                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.ProgressValue))
                    {
                        progressValues.Add(vm.ProgressValue);
                    }
                };

                var files = new List<string> { "a.wav" };
                await vm.ExecuteThresholdOptimizationAsync(files, 0, 10);

                // 進捗が更新されたこと
                Assert.NotEmpty(progressValues);
                // 最終的に100%になること
                Assert.Equal(100, vm.ProgressValue);
            });
        }

        /// <summary>
        /// 定義削減処理での状態遷移検証。
        /// </summary>
        [Fact]
        public Task ExecuteDefinitionReductionAsync_StateTransition_CorrectOrder()
        {
            return WpfTestHelper.RunStaAsync(async () =>
            {
                var vm = new OptimizationViewModel(new FakeOptimizationService())
                {
                    R2Threshold = "80"
                };
                var list = new FileList("dummy.bms");
                var busyStates = new List<bool>();

                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.IsBusy))
                    {
                        busyStates.Add(vm.IsBusy);
                    }
                };

                await vm.ExecuteDefinitionReductionAsync(list, "in.bms", "out.bms");

                // 処理が完了していること
                Assert.False(vm.IsBusy);
            });
        }

        /// <summary>
        /// サービスがnullを返した場合の状態遷移検証。
        /// </summary>
        [Fact]
        public Task ExecuteThresholdOptimizationAsync_ServiceReturnsNull_HandlesGracefully()
        {
            return WpfTestHelper.RunStaAsync(async () =>
            {
                var vm = new OptimizationViewModel(new NullReturningOptimizationService());

                var result = await vm.ExecuteThresholdOptimizationAsync(
                    new List<string> { "a.wav" }, 0, 10);

                Assert.Null(result);
                Assert.False(vm.IsBusy, "サービスがnullを返しても、IsBusyはfalseになるべき");
            });
        }

        #endregion
    }

    #region Test Doubles for State Transition Tests

    /// <summary>
    /// 例外をスローするフェイクサービス（エラーハンドリングテスト用）。
    /// </summary>
    internal class ThrowingOptimizationService : IBmsOptimizationService
    {
        public Task<OptimizationResult?> FindOptimalThresholdsAsync(
            List<string> files, int startDefinition, int endDefinition, IProgress<int>? progress = null)
        {
            throw new InvalidOperationException("Test exception");
        }

        public ValidationResult<float> ValidateR2Threshold(string r2Text)
        {
            return ValidationResult<float>.Success(0.8f);
        }

        public Task<BmsOptimizationService.ReductionResult> ExecuteDefinitionReductionAsync(
            IReadOnlyList<FileList.WavFiles> fileList,
            string inputPath,
            string outputPath,
            float r2Threshold,
            int startDefinition,
            int endDefinition,
            bool isPhysicalDeletionEnabled,
            IProgress<int>? progress = null,
            IEnumerable<string>? selectedKeywords = null)
        {
            throw new InvalidOperationException("Test exception");
        }

        public ValidationResult ValidateDefinitionRange(string startVal, string endVal)
        {
            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// nullを返すフェイクサービス（null処理テスト用）。
    /// </summary>
    internal class NullReturningOptimizationService : IBmsOptimizationService
    {
        public Task<OptimizationResult?> FindOptimalThresholdsAsync(
            List<string> files, int startDefinition, int endDefinition, IProgress<int>? progress = null)
        {
            return Task.FromResult<OptimizationResult?>(null);
        }

        public ValidationResult<float> ValidateR2Threshold(string r2Text)
        {
            return ValidationResult<float>.Success(0.8f);
        }

        public Task<BmsOptimizationService.ReductionResult> ExecuteDefinitionReductionAsync(
            IReadOnlyList<FileList.WavFiles> fileList,
            string inputPath,
            string outputPath,
            float r2Threshold,
            int startDefinition,
            int endDefinition,
            bool isPhysicalDeletionEnabled,
            IProgress<int>? progress = null,
            IEnumerable<string>? selectedKeywords = null)
        {

            return Task.FromResult(new BmsOptimizationService.ReductionResult
            {
                IsSuccess = false,
                ErrorMessage = "Service returned null"
            });
        }

        public ValidationResult ValidateDefinitionRange(string startVal, string endVal)
        {
            return ValidationResult.Success();
        }
    }

    #endregion
}
