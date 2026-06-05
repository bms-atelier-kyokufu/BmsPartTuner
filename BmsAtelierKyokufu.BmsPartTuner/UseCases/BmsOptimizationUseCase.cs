using BmsAtelierKyokufu.BmsPartTuner.UseCases.Dto;

namespace BmsAtelierKyokufu.BmsPartTuner.UseCases;


public class BmsOptimizationUseCase(IBmsOptimizationService optimizationService) : IBmsOptimizationUseCase
{
    private readonly IBmsOptimizationService _optimizationService = optimizationService ?? throw new ArgumentNullException(nameof(optimizationService));

    public async Task<OptimizationUseCaseResult<OptimizationResult>> ExecuteThresholdOptimizationAsync(ThresholdOptimizationRequest request)
    {
        var inputPath = request.InputPath?.Trim('"') ?? string.Empty;

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return OptimizationUseCaseResult<OptimizationResult>.Failure("入力BMS/BMSONファイルを先に読み込んでください");
        }

        if (!File.Exists(inputPath))
        {
            return OptimizationUseCaseResult<OptimizationResult>.Failure($"入力ファイルが見つかりません: {Path.GetFileName(inputPath)}");
        }

        if (request.BmsFileList == null || request.BmsFileList.Count == 0)
        {
            return OptimizationUseCaseResult<OptimizationResult>.Failure("ファイルリストが空です。BMS/BMSONファイルに定義が含まれているか確認してください");
        }

        var files = new List<string>();
        foreach (var wavFile in request.BmsFileList)
        {
            if (!string.IsNullOrEmpty(wavFile))
            {
                files.Add(wavFile);
            }
        }

        if (files.Count == 0)
        {
            return OptimizationUseCaseResult<OptimizationResult>.Failure("有効なファイルパスが見つかりません");
        }

        var result = await _optimizationService.FindOptimalThresholdsAsync(
            files,
            request.StartDefinition,
            request.EndDefinition,
            request.OperationContext);

        if (result != null)
        {
            return OptimizationUseCaseResult<OptimizationResult>.Success(result);
        }

        return OptimizationUseCaseResult<OptimizationResult>.Failure("最適化に失敗しました");
    }

    public async Task<OptimizationUseCaseResult<BmsOptimizationService.ReductionResult>> ExecuteDefinitionReductionAsync(DefinitionReductionRequest request)
    {
        if (request.BmsFileList == null)
        {
            return OptimizationUseCaseResult<BmsOptimizationService.ReductionResult>.Failure("BMS/BMSONファイルが読み込まれていません");
        }

        if (string.IsNullOrWhiteSpace(request.InputPath))
        {
            return OptimizationUseCaseResult<BmsOptimizationService.ReductionResult>.Failure("入力BMS/BMSONファイルを指定してください");
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return OptimizationUseCaseResult<BmsOptimizationService.ReductionResult>.Failure("出力先を指定してください");
        }

        var result = await _optimizationService.ExecuteDefinitionReductionAsync(
            request.BmsFileList.GetFileList(),
            request.InputPath.Trim('"'),
            request.OutputPath.Trim('"'),
            new DefinitionReductionOptions
            {
                R2Threshold = request.R2Threshold,
                StartDefinition = request.StartDefinition,
                EndDefinition = request.EndDefinition,
                IsPhysicalDeletionEnabled = request.IsPhysicalDeletionEnabled,
                InputBmsContent = request.InputBmsContent,
                SelectedKeywords = request.SelectedKeywords,
                OperationContext = request.OperationContext
            });

        if (result.IsSuccess)
        {
            return OptimizationUseCaseResult<BmsOptimizationService.ReductionResult>.Success(result);
        }

        return OptimizationUseCaseResult<BmsOptimizationService.ReductionResult>.Failure($"処理エラー: {result.ErrorMessage}");
    }
}
