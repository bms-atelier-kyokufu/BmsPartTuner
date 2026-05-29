using System;
using System.Collections.Generic;

namespace BmsAtelierKyokufu.BmsPartTuner.UseCases.Dto;

public class ThresholdOptimizationRequest
{
    public string? InputPath { get; set; }
    public List<string>? BmsFileList { get; set; }
    public int StartDefinition { get; set; }
    public int EndDefinition { get; set; }
    public IProgress<int>? Progress { get; set; }
}
