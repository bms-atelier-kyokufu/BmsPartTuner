using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Core.Validation;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Services.Bms;
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
            IReadOnlyList<BmsAudioFile> fileList,
            string inputPath,
            string outputPath,
            DefinitionReductionOptions options)
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
        private static Task RunViewModelTestAsync(
            IBmsOptimizationService service,
            Action<OptimizationViewModel>? setup,
            Func<OptimizationViewModel, Task> act,
            Action<OptimizationViewModel> assert)
        {
            return WpfTestHelper.RunStaAsync(async () =>
            {
                var vm = new OptimizationViewModel(service);
                setup?.Invoke(vm);
                await act(vm);
                assert?.Invoke(vm);
            });
        }

        [Fact]
        public Task ExecuteThresholdOptimizationAsync_UpdatesBusyStateAndProgress() =>
            RunViewModelTestAsync(
                new FakeOptimizationService(),
                null,
                vm => vm.ExecuteThresholdOptimizationAsync(["a.wav", "b.wav"], 0, 10),
                vm =>
                {
                    Assert.False(vm.IsBusy);
                    Assert.False(vm.IsProgressIndeterminate);
                    Assert.Equal(100, vm.ProgressValue);
                    Assert.NotNull(vm.LastOptimizationResult);
                    Assert.Contains("完了", vm.StatusMessage);
                }
            );

        [Fact]
        public Task ExecuteThresholdOptimizationAsync_EmptyFiles_RaisesErrorAndReturnsNull()
        {
            string? error = null;
            return RunViewModelTestAsync(
                new FakeOptimizationService(),
                vm => vm.ErrorOccurred += (_, msg) => error = msg,
                vm => vm.ExecuteThresholdOptimizationAsync([], 0, 10),
                vm =>
                {
                    Assert.Equal("ファイルリストが空です", error);
                    Assert.False(vm.IsBusy);
                }
            );
        }

        [Fact]
        public Task ExecuteDefinitionReductionAsync_ValidatesThresholdAndReportsResult()
        {
            string? completedOutput = null;
            return RunViewModelTestAsync(
                new FakeOptimizationService(),
                vm => { vm.R2Threshold = "80"; vm.DefinitionReductionCompleted += (_, e) => completedOutput = e.OutputPath; },
                vm => vm.ExecuteDefinitionReductionAsync(new BmsDefinitionManager("dummy.bms"), "in.bms", "out.bms"),
                vm => { Assert.False(vm.IsBusy); Assert.Equal("out.bms", completedOutput); }
            );
        }

        [Fact]
        public Task ExecuteDefinitionReductionAsync_InvalidThreshold_RaisesError()
        {
            string? error = null;
            return RunViewModelTestAsync(
                new FakeOptimizationService(),
                vm => { vm.R2Threshold = "-1"; vm.ErrorOccurred += (_, msg) => error = msg; },
                vm => vm.ExecuteDefinitionReductionAsync(new BmsDefinitionManager("dummy.bms"), "in.bms", "out.bms"),
                vm => { Assert.Equal("invalid", error); Assert.False(vm.IsBusy); }
            );
        }

        #region Priority A: State Transition Tests (UIフリーズ防止)

        [Fact]
        public Task ExecuteThresholdOptimizationAsync_ServiceThrows_TransitionsToErrorState()
        {
            string? error = null;
            return RunViewModelTestAsync(
                new ThrowingOptimizationService(),
                vm => vm.ErrorOccurred += (_, msg) => error = msg,
                vm => vm.ExecuteThresholdOptimizationAsync(["a.wav"], 0, 10),
                vm => { Assert.False(vm.IsBusy); Assert.NotNull(error); }
            );
        }

        [Fact]
        public Task ExecuteThresholdOptimizationAsync_IsBusyTransition_CorrectOrder()
        {
            var busyStates = new List<bool>();
            return RunViewModelTestAsync(
                new FakeOptimizationService(),
                vm => vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(vm.IsBusy)) busyStates.Add(vm.IsBusy); },
                vm => vm.ExecuteThresholdOptimizationAsync(["a.wav"], 0, 10),
                vm => { Assert.Contains(true, busyStates); Assert.False(vm.IsBusy); }
            );
        }

        [Fact]
        public Task ExecuteThresholdOptimizationAsync_ProgressUpdates_ReportedCorrectly()
        {
            var progressValues = new List<int>();
            return RunViewModelTestAsync(
                new FakeOptimizationService(),
                vm => vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(vm.ProgressValue)) progressValues.Add(vm.ProgressValue); },
                vm => vm.ExecuteThresholdOptimizationAsync(["a.wav"], 0, 10),
                vm => { Assert.NotEmpty(progressValues); Assert.Equal(100, vm.ProgressValue); }
            );
        }

        [Fact]
        public Task ExecuteDefinitionReductionAsync_StateTransition_CorrectOrder()
        {
            var busyStates = new List<bool>();
            return RunViewModelTestAsync(
                new FakeOptimizationService(),
                vm => { vm.R2Threshold = "80"; vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(vm.IsBusy)) busyStates.Add(vm.IsBusy); }; },
                vm => vm.ExecuteDefinitionReductionAsync(new BmsDefinitionManager("dummy.bms"), "in.bms", "out.bms"),
                vm => Assert.False(vm.IsBusy)
            );
        }

        [Fact]
        public Task ExecuteThresholdOptimizationAsync_ServiceReturnsNull_HandlesGracefully() =>
            RunViewModelTestAsync(
                new NullReturningOptimizationService(),
                null,
                vm => vm.ExecuteThresholdOptimizationAsync(["a.wav"], 0, 10),
                vm => Assert.False(vm.IsBusy)
            );

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
            IReadOnlyList<BmsAudioFile> fileList,
            string inputPath,
            string outputPath,
            DefinitionReductionOptions options)
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
            IReadOnlyList<BmsAudioFile> fileList,
            string inputPath,
            string outputPath,
            DefinitionReductionOptions options)
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
