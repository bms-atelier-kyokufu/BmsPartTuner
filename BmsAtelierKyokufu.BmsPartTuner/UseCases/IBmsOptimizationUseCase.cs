using System.Threading.Tasks;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.UseCases.Dto;

namespace BmsAtelierKyokufu.BmsPartTuner.UseCases;

public interface IBmsOptimizationUseCase
{
    Task<OptimizationUseCaseResult<OptimizationResult>> ExecuteThresholdOptimizationAsync(ThresholdOptimizationRequest request);
    Task<OptimizationUseCaseResult<BmsAtelierKyokufu.BmsPartTuner.Core.Optimization.BmsOptimizationService.ReductionResult>> ExecuteDefinitionReductionAsync(DefinitionReductionRequest request);
}
