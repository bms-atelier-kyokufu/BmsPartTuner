using System.Text.Json.Serialization;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Scenarios;

/// <summary>
/// <see cref="OracleData"/> の動作を検証するテストクラス。
/// </summary>
public class OracleData
{
    [JsonPropertyName("test_scenario")]
    public string TestScenario { get; set; } = string.Empty;

    [JsonPropertyName("total_wav_count")]
    public int TotalWavCount { get; set; }

    [JsonPropertyName("expected_clusters")]
    public List<OracleCluster> ExpectedClusters { get; set; } = [];

    [JsonPropertyName("expected_isolated")]
    public List<OracleIsolated> ExpectedIsolated { get; set; } = [];
}

/// <summary>
/// <see cref="OracleCluster"/> の動作を検証するテストクラス。
/// </summary>
public class OracleCluster
{
    [JsonPropertyName("logical_group_id")]
    public string LogicalGroupId { get; set; } = string.Empty;

    [JsonPropertyName("expected_merged_to")]
    public string ExpectedMergedTo { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("source_wav_ids")]
    public List<string> SourceWavIds { get; set; } = [];
}

/// <summary>
/// <see cref="OracleIsolated"/> の動作を検証するテストクラス。
/// </summary>
public class OracleIsolated
{
    [JsonPropertyName("logical_group_id")]
    public string LogicalGroupId { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("source_wav_ids")]
    public List<string> SourceWavIds { get; set; } = [];
}
