using BmsAtelierKyokufu.BmsPartTuner.UseCases.Dto;

namespace BmsAtelierKyokufu.BmsPartTuner.UseCases;

public interface IBmsOptimizationUseCase
{
    Task<OptimizationUseCaseResult<OptimizationResult>> ExecuteThresholdOptimizationAsync(ThresholdOptimizationRequest request);
    Task<OptimizationUseCaseResult<BmsOptimizationService.ReductionResult>> ExecuteDefinitionReductionAsync(DefinitionReductionRequest request);
}