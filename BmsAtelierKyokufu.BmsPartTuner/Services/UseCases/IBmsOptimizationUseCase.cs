using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BmsAtelierKyokufu.BmsPartTuner.Models;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.UseCases;

public class ThresholdOptimizationRequest
{
    public string? InputPath { get; set; }
    public List<string>? BmsFileList { get; set; }
    public int StartDefinition { get; set; }
    public int EndDefinition { get; set; }
    public IProgress<int>? Progress { get; set; }
}

public class OptimizationUseCaseResult<T>
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public T? Data { get; set; }

    public static OptimizationUseCaseResult<T> Success(T data) => new() { IsSuccess = true, Data = data };
    public static OptimizationUseCaseResult<T> Failure(string errorMessage) => new() { IsSuccess = false, ErrorMessage = errorMessage };
}

public class DefinitionReductionRequest
{
    public Core.Bms.BmsDefinitionManager? BmsFileList { get; set; }
    public string? InputPath { get; set; }
    public string? OutputPath { get; set; }
    public string? InputBmsContent { get; set; }
    public IEnumerable<string>? SelectedKeywords { get; set; }
    public float R2Threshold { get; set; }
    public int StartDefinition { get; set; }
    public int EndDefinition { get; set; }
    public bool IsPhysicalDeletionEnabled { get; set; }
    public IProgress<int>? Progress { get; set; }
}

public interface IBmsOptimizationUseCase
{
    Task<OptimizationUseCaseResult<OptimizationResult>> ExecuteThresholdOptimizationAsync(ThresholdOptimizationRequest request);
    Task<OptimizationUseCaseResult<BmsAtelierKyokufu.BmsPartTuner.Services.Bms.BmsOptimizationService.ReductionResult>> ExecuteDefinitionReductionAsync(DefinitionReductionRequest request);
}
