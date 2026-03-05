namespace BmsAtelierKyokufu.BmsPartTuner.Tests.MutationFramework;

/// <summary>
/// JSON出力用のレポートクラス。
/// </summary>
public class MutationTestReport
{
    public DateTime Timestamp { get; set; }
    public string SourceDirectory { get; set; } = "";
    public int TotalMutations { get; set; }
    public int Killed { get; set; }
    public int Survived { get; set; }
    public int CompileErrors { get; set; }
    public double MutationScore { get; set; }
    public TimeSpan Duration { get; set; }
    public List<MutationResultDto> Mutations { get; set; } = [];
}

/// <summary>
/// JSON出力用の変異結果DTO。
/// </summary>
public class MutationResultDto
{
    public string FilePath { get; set; } = "";
    public string MutationType { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public string OriginalCode { get; set; } = "";
    public string MutatedCode { get; set; } = "";
    public bool IsKilled { get; set; }
    public string? ErrorMessage { get; set; }
}
