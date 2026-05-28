#nullable enable

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Attributes;

[System.AttributeUsage(System.AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class GenerateSimdBatchUnrollAttribute : System.Attribute
{
    public int BatchSize { get; set; } = 4;
    public int UnrollFactor { get; set; } = 4;
    public System.Type? ElementType { get; set; }
    public string LogicType { get; set; } = "";
}
