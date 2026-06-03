using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Optimization
{
    /// <summary>
    /// <see cref="BmsOptimizationService"/> のテストクラスで共通して使用するベースクラス。
    /// 一時ディレクトリ環境の管理と、テスト実行フローの共通化を提供します。
    /// </summary>
    public abstract class BmsOptimizationServiceTestBase : IDisposable
    {
        protected readonly BmsFamilyTestContext Context;
        protected readonly BmsOptimizationService Service;
        private bool _disposed;

        protected BmsOptimizationServiceTestBase()
        {
            Context = new BmsFamilyTestContext();
            Service = new BmsOptimizationService();
        }

        public void Dispose()
        {
            if (_disposed) return;
            Context?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 最適なしきい値検索の検証を実行する共通メソッド。
        /// </summary>
        protected async Task RunOptimalThresholdsTestAsync(Func<string, List<string>> setupFiles, Action<OptimizationResult?> assertResult, int startDef = 1, int endDef = 1, IProgress<int>? progress = null)
        {
            var files = setupFiles?.Invoke(Context.TempDirectory) ?? [];
            var result = await Service.FindOptimalThresholdsAsync(files, startDef, endDef, progress);
            assertResult?.Invoke(result);
        }

        /// <summary>
        /// 定義削減の検証を実行する共通メソッド。
        /// </summary>
        protected async Task RunDefinitionReductionTestAsync(ReductionTestOptions options)
        {
            var builder = Context.CreateBuilder();
            options.BuildBms?.Invoke(builder);
            string inputBmsName = options.InputBmsName ?? "test.bms";
            string outputBmsName = options.OutputBmsName ?? "output.bms";
            string inputBmsPath = builder.Build(inputBmsName);
            string outputBmsPath = Path.Combine(Context.TempDirectory, outputBmsName);
            var files = options.CreateFiles?.Invoke(Context.TempDirectory) ?? [];

            options.BeforeExecute?.Invoke(outputBmsPath);

            try
            {
                var result = await Service.ExecuteDefinitionReductionAsync(
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
            finally
            {
                options.AfterExecute?.Invoke(outputBmsPath);
            }
        }
    }
}
