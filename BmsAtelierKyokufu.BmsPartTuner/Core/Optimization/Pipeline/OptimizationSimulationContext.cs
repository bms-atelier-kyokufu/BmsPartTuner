namespace BmsAtelierKyokufu.BmsPartTuner.Core.Optimization.Pipeline;

/// <summary>
/// しきい値最適化シミュレーション用のパイプライン実行コンテキスト。
/// </summary>
[ADRAnchor("ARCH-01", nameof(OptimizationSimulationContext))]
internal sealed class OptimizationSimulationContext(
    List<string> filePaths,
    int startDefinition,
    int endDefinition,
    IProgress<int>? progress)
{
    // 入力
    public List<string> FilePaths { get; } = filePaths ?? throw new ArgumentNullException(nameof(filePaths));
    public int StartDefinition { get; } = startDefinition;
    public int EndDefinition { get; set; } = endDefinition;
    public IProgress<int>? Progress { get; } = progress;

    // 中間状態
    public List<BmsAudioFile> FileListItems { get; } = [];
    public ConcurrentDictionary<string, ICachedSoundData>? AudioCache { get; set; }
    public List<string> FailedFiles { get; set; } = [];

    // シミュレーション結果
    public IReadOnlyList<SimulationPoint>? SimulationResults { get; set; }
    public List<(double Threshold, int FileCount)> SimulationData { get; set; } = [];

    // 出力
    public OptimizationResult? Result { get; set; }
}
