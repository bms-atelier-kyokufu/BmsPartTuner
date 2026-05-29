#nullable enable

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class GenerateSimdBatchUnrollAttribute : Attribute
{
    public int BatchSize { get; set; } = 4;
    public int UnrollFactor { get; set; } = 4;
    public Type? ElementType { get; set; }
    public string LogicType { get; set; } = "";
}
