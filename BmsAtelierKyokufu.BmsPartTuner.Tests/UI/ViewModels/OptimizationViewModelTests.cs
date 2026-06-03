using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;
using BmsAtelierKyokufu.BmsPartTuner.Core.Validation;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;
using BmsAtelierKyokufu.BmsPartTuner.UI.ViewModels;
using BmsAtelierKyokufu.BmsPartTuner.UseCases.Dto;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.UI.ViewModels
{
    /// <summary>
    /// テスト用のフェイク最適化サービス。
    /// </summary>
    internal class FakeOptimizationService : IBmsOptimizationService
    {
        /// <inheritdoc />
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

        /// <inheritdoc />
        public ValidationResult<float> ValidateR2Threshold(string r2Text)
        {
            if (int.TryParse(r2Text, out var v) && v >= 0 && v <= 100)
            {
                return ValidationResult<float>.Success(v / 100f);
            }
            return ValidationResult<float>.Failure("invalid");
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public ValidationResult ValidateDefinitionRange(string startVal, string endVal)
        {
            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// テスト用のフェイク最適化ユースケース。
    /// </summary>
    internal class FakeOptimizationUseCase : BmsAtelierKyokufu.BmsPartTuner.UseCases.IBmsOptimizationUseCase
    {
        /// <inheritdoc />
        public Task<OptimizationUseCaseResult<OptimizationResult>> ExecuteThresholdOptimizationAsync(ThresholdOptimizationRequest request)
        {
            if (request.BmsFileList == null || request.BmsFileList.Count == 0)
                return Task.FromResult(OptimizationUseCaseResult<OptimizationResult>.Failure("ファイルリストが空です"));

            return Task.FromResult(OptimizationUseCaseResult<OptimizationResult>.Success(new OptimizationResult
            {
                Base36Result = (0.85f, 100),
                Base62Result = (0.90f, 200),
                ExecutionTime = TimeSpan.FromSeconds(0.5),
                MemoryUsedBytes = 10 * 1024 * 1024
            }));
        }

        /// <inheritdoc />
        public Task<OptimizationUseCaseResult<BmsOptimizationService.ReductionResult>> ExecuteDefinitionReductionAsync(DefinitionReductionRequest request)
        {
            if (request.R2Threshold < 0)
                return Task.FromResult(OptimizationUseCaseResult<BmsOptimizationService.ReductionResult>.Failure("invalid"));

            return Task.FromResult(OptimizationUseCaseResult<BmsOptimizationService.ReductionResult>.Success(new BmsOptimizationService.ReductionResult
            {
                IsSuccess = true,
                OriginalCount = 10,
                OptimizedCount = 7,
                ErrorMessage = null,
                DeletedFilesCount = 0
            }));
        }
    }

    /// <summary>
    /// OptimizationViewModel の動作検証テスト。
    /// 閾値最適化・定義削減の実行フロー、状態管理、エラーハンドリングを確認します。
    /// </summary>
    /// <summary>
    /// <see cref="OptimizationViewModelTests"/> の動作を検証するテストクラス。
    /// </summary>
    public class OptimizationViewModelTests
    {
        /// <summary>
        /// 指定された設定に基づいてViewModelのテストを実行します。
        /// </summary>
        private static Task RunViewModelTestAsync(
            IBmsOptimizationService service,
            BmsAtelierKyokufu.BmsPartTuner.UseCases.IBmsOptimizationUseCase useCase,
            Action<OptimizationViewModel>? setup,
            Func<OptimizationViewModel, Task> act,
            Action<OptimizationViewModel> assert)
        {
            return WpfTestHelper.RunStaAsync(async () =>
            {
                var vm = new OptimizationViewModel(service, useCase);
                setup?.Invoke(vm);
                await act(vm);
                assert?.Invoke(vm);
            });
        }

        /// <summary>
        /// 閾値最適化が実行されたときに、ビジー状態と進捗状況が適切に更新されることを検証します。
        /// </summary>
        [Fact]
        public Task ExecuteThresholdOptimizationAsync_UpdatesBusyStateAndProgress() =>
            RunViewModelTestAsync(
                new FakeOptimizationService(),
                new FakeOptimizationUseCase(),
                null,
                vm => vm.ExecuteThresholdOptimizationAsync("in.bms", ["a.wav", "b.wav"], 0, 10),
                vm =>
                {
                    Assert.False(vm.IsBusy);
                    Assert.False(vm.IsProgressIndeterminate);
                    Assert.Equal(100, vm.ProgressValue);
                    Assert.NotNull(vm.LastOptimizationResult);
                    Assert.Contains("完了", vm.StatusMessage);
                }
            );

        /// <summary>
        /// ファイルリストが空の状態で閾値最適化を実行したときに、エラーイベントが発生し結果がnullになることを検証します。
        /// </summary>
        [Fact]
        public Task ExecuteThresholdOptimizationAsync_EmptyFiles_RaisesErrorAndReturnsNull()
        {
            string? error = null;
            return RunViewModelTestAsync(
                new FakeOptimizationService(),
                new FakeOptimizationUseCase(),
                vm => vm.ErrorOccurred += (_, msg) => error = msg,
                vm => vm.ExecuteThresholdOptimizationAsync("in.bms", [], 0, 10),
                vm =>
                {
                    Assert.Equal("ファイルリストが空です", error);
                    Assert.False(vm.IsBusy);
                }
            );
        }

        /// <summary>
        /// 有効な閾値で定義削減を実行したときに、進捗が報告され結果が出力されることを検証します。
        /// </summary>
        [Fact]
        public Task ExecuteDefinitionReductionAsync_ValidatesThresholdAndReportsResult()
        {
            string? completedOutput = null;
            return RunViewModelTestAsync(
                new FakeOptimizationService(),
                new FakeOptimizationUseCase(),
                vm => { vm.R2Threshold = "80"; vm.DefinitionReductionCompleted += (_, e) => completedOutput = e.OutputPath; },
                vm => vm.ExecuteDefinitionReductionAsync(new BmsDefinitionManager("dummy.bms"), "in.bms", "out.bms"),
                vm => { Assert.False(vm.IsBusy); Assert.Equal("out.bms", completedOutput); }
            );
        }

        /// <summary>
        /// 無効な閾値で定義削減を実行したときに、エラーイベントが発生することを検証します。
        /// </summary>
        [Fact]
        public Task ExecuteDefinitionReductionAsync_InvalidThreshold_RaisesError()
        {
            string? error = null;
            return RunViewModelTestAsync(
                new FakeOptimizationService(),
                new FakeOptimizationUseCase(),
                vm => { vm.R2Threshold = "-1"; vm.ErrorOccurred += (_, msg) => error = msg; },
                vm => vm.ExecuteDefinitionReductionAsync(new BmsDefinitionManager("dummy.bms"), "in.bms", "out.bms"),
                vm => { Assert.Equal("invalid", error); Assert.False(vm.IsBusy); }
            );
        }

        #region Priority A: State Transition Tests (UIフリーズ防止)

        /// <summary>
        /// サービス実行中に例外が発生したときに、エラー状態へ適切に遷移することを検証します。
        /// </summary>
        [Fact]
        public Task ExecuteThresholdOptimizationAsync_ServiceThrows_TransitionsToErrorState()
        {
            string? error = null;
            return RunViewModelTestAsync(
                new ThrowingOptimizationService(),
                new ThrowingOptimizationUseCase(),
                vm => vm.ErrorOccurred += (_, msg) => error = msg,
                vm => vm.ExecuteThresholdOptimizationAsync("in.bms", ["a.wav"], 0, 10),
                vm => { Assert.False(vm.IsBusy); Assert.NotNull(error); }
            );
        }

        /// <summary>
        /// 閾値最適化の処理中において、ビジー状態（IsBusy）の遷移順序が正しいことを検証します。
        /// </summary>
        [Fact]
        public Task ExecuteThresholdOptimizationAsync_IsBusyTransition_CorrectOrder()
        {
            var busyStates = new List<bool>();
            return RunViewModelTestAsync(
                new FakeOptimizationService(),
                new FakeOptimizationUseCase(),
                vm => vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(vm.IsBusy)) busyStates.Add(vm.IsBusy); },
                vm => vm.ExecuteThresholdOptimizationAsync("in.bms", ["a.wav"], 0, 10),
                vm => { Assert.Contains(true, busyStates); Assert.False(vm.IsBusy); }
            );
        }

        /// <summary>
        /// 閾値最適化の処理中において、進捗状況の値が正しく報告されることを検証します。
        /// </summary>
        [Fact]
        public Task ExecuteThresholdOptimizationAsync_ProgressUpdates_ReportedCorrectly()
        {
            var progressValues = new List<int>();
            return RunViewModelTestAsync(
                new FakeOptimizationService(),
                new FakeOptimizationUseCase(),
                vm => vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(vm.ProgressValue)) progressValues.Add(vm.ProgressValue); },
                vm => vm.ExecuteThresholdOptimizationAsync("in.bms", ["a.wav"], 0, 10),
                vm => { Assert.NotEmpty(progressValues); Assert.Equal(100, vm.ProgressValue); }
            );
        }

        /// <summary>
        /// 定義削減の処理中において、ビジー状態（IsBusy）の遷移順序が正しいことを検証します。
        /// </summary>
        [Fact]
        public Task ExecuteDefinitionReductionAsync_StateTransition_CorrectOrder()
        {
            var busyStates = new List<bool>();
            return RunViewModelTestAsync(
                new FakeOptimizationService(),
                new FakeOptimizationUseCase(),
                vm => { vm.R2Threshold = "80"; vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(vm.IsBusy)) busyStates.Add(vm.IsBusy); }; },
                vm => vm.ExecuteDefinitionReductionAsync(new BmsDefinitionManager("dummy.bms"), "in.bms", "out.bms"),
                vm => Assert.False(vm.IsBusy)
            );
        }

        /// <summary>
        /// サービスがnullを返却したときに、プログラムがフリーズせず適切に処理を終了することを検証します。
        /// </summary>
        [Fact]
        public Task ExecuteThresholdOptimizationAsync_ServiceReturnsNull_HandlesGracefully() =>
            RunViewModelTestAsync(
                new NullReturningOptimizationService(),
                new NullReturningOptimizationUseCase(),
                null,
                vm => vm.ExecuteThresholdOptimizationAsync("in.bms", ["a.wav"], 0, 10),
                vm => Assert.False(vm.IsBusy)
            );

        #endregion
    }

    #region Test Doubles for State Transition Tests

    /// <summary>
    /// 例外を意図的にスローするテスト用のフェイク最適化サービス。
    /// </summary>
    internal class ThrowingOptimizationService : IBmsOptimizationService
    {
        /// <inheritdoc />
        public Task<OptimizationResult?> FindOptimalThresholdsAsync(
            List<string> files, int startDefinition, int endDefinition, IProgress<int>? progress = null)
        {
            throw new InvalidOperationException("Test exception");
        }

        /// <inheritdoc />
        public ValidationResult<float> ValidateR2Threshold(string r2Text)
        {
            return ValidationResult<float>.Success(0.8f);
        }

        /// <inheritdoc />
        public Task<BmsOptimizationService.ReductionResult> ExecuteDefinitionReductionAsync(
            IReadOnlyList<BmsAudioFile> fileList,
            string inputPath,
            string outputPath,
            DefinitionReductionOptions options)
        {
            throw new InvalidOperationException("Test exception");
        }

        /// <inheritdoc />
        public ValidationResult ValidateDefinitionRange(string startVal, string endVal)
        {
            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// 意図的にnullを返却するテスト用のフェイク最適化サービス。
    /// </summary>
    internal class NullReturningOptimizationService : IBmsOptimizationService
    {
        /// <inheritdoc />
        public Task<OptimizationResult?> FindOptimalThresholdsAsync(
            List<string> files, int startDefinition, int endDefinition, IProgress<int>? progress = null)
        {
            return Task.FromResult<OptimizationResult?>(null);
        }

        /// <inheritdoc />
        public ValidationResult<float> ValidateR2Threshold(string r2Text)
        {
            return ValidationResult<float>.Success(0.8f);
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public ValidationResult ValidateDefinitionRange(string startVal, string endVal)
        {
            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// 例外を意図的にスローするテスト用のフェイク最適化ユースケース。
    /// </summary>
    internal class ThrowingOptimizationUseCase : BmsAtelierKyokufu.BmsPartTuner.UseCases.IBmsOptimizationUseCase
    {
        /// <inheritdoc />
        public Task<OptimizationUseCaseResult<OptimizationResult>> ExecuteThresholdOptimizationAsync(ThresholdOptimizationRequest request)
            => Task.FromResult(OptimizationUseCaseResult<OptimizationResult>.Failure("Test exception"));

        public Task<OptimizationUseCaseResult<BmsOptimizationService.ReductionResult>> ExecuteDefinitionReductionAsync(DefinitionReductionRequest request)
            => Task.FromResult(OptimizationUseCaseResult<BmsOptimizationService.ReductionResult>.Failure("Test exception"));
    }

    /// <summary>
    /// 意図的にnullに相当するエラー結果を返却するテスト用のフェイク最適化ユースケース。
    /// </summary>
    internal class NullReturningOptimizationUseCase : BmsAtelierKyokufu.BmsPartTuner.UseCases.IBmsOptimizationUseCase
    {
        /// <inheritdoc />
        public Task<OptimizationUseCaseResult<OptimizationResult>> ExecuteThresholdOptimizationAsync(ThresholdOptimizationRequest request)
            => Task.FromResult(OptimizationUseCaseResult<OptimizationResult>.Failure("Service returned null"));

        public Task<OptimizationUseCaseResult<BmsOptimizationService.ReductionResult>> ExecuteDefinitionReductionAsync(DefinitionReductionRequest request)
            => Task.FromResult(OptimizationUseCaseResult<BmsOptimizationService.ReductionResult>.Failure("Service returned null"));
    }

    #endregion
}
