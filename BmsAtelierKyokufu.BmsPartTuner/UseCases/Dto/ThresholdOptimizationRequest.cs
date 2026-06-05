using BmsAtelierKyokufu.BmsPartTuner.Core.Context;

namespace BmsAtelierKyokufu.BmsPartTuner.UseCases.Dto;

public class ThresholdOptimizationRequest
{
    public string? InputPath { get; set; }
    public List<string>? BmsFileList { get; set; }
    public int StartDefinition { get; set; }
    public int EndDefinition { get; set; }
    public IOperationContext? OperationContext { get; set; }
}
