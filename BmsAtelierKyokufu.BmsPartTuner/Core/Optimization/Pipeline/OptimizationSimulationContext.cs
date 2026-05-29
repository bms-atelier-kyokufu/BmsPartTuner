namespace BmsAtelierKyokufu.BmsPartTuner.Core.Optimization.Pipeline;

/// <summary>
/// しきい値最適化シミュレーション用のパイプライン実行コンテキスト。
/// </summary>
internal sealed class OptimizationSimulationContext
{
    // 入力
    public List<string> FilePaths { get; }
    public int StartDefinition { get; }
    public int EndDefinition { get; set; }
    public IProgress<int>? Progress { get; }

    // 中間状態
    public List<BmsAudioFile> FileListItems { get; } = new();
    public System.Collections.Concurrent.ConcurrentDictionary<string, ICachedSoundData>? AudioCache { get; set; }
    public List<string> FailedFiles { get; set; } = new();
    
    // シミュレーション結果
    public IReadOnlyList<SimulationPoint>? SimulationResults { get; set; }
    public List<(double Threshold, int FileCount)> SimulationData { get; set; } = new();

    // 出力
    public OptimizationResult? Result { get; set; }

    public OptimizationSimulationContext(
        List<string> filePaths,
        int startDefinition,
        int endDefinition,
        IProgress<int>? progress)
    {
        FilePaths = filePaths ?? throw new ArgumentNullException(nameof(filePaths));
        StartDefinition = startDefinition;
        EndDefinition = endDefinition;
        Progress = progress;
    }
}
