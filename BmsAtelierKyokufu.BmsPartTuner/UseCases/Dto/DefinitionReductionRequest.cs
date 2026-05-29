using System;
using System.Collections.Generic;

namespace BmsAtelierKyokufu.BmsPartTuner.UseCases.Dto;

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
