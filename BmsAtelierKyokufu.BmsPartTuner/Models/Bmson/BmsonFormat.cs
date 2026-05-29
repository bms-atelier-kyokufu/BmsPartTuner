using System.Text.Json.Serialization;

namespace BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;

/// <summary>
/// bmsonフォーマットのルート要素。
/// </summary>
public record BmsonFormat
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";

    [JsonPropertyName("info")]
    public BmsonInfo Info { get; init; } = new();

    [JsonPropertyName("lines")]
    public List<BmsonLineEvent> Lines { get; init; } = [];

    [JsonPropertyName("bpm_events")]
    public List<BmsonBpmEvent> BpmEvents { get; init; } = [];

    [JsonPropertyName("stop_events")]
    public List<BmsonStopEvent> StopEvents { get; init; } = [];

    [JsonPropertyName("sound_channels")]
    public List<BmsonSoundChannel> SoundChannels { get; init; } = [];

    [JsonPropertyName("bga")]
    public BmsonBga? Bga { get; init; }
}

/// <summary>
/// 楽曲のメタデータ情報。
/// </summary>
public record BmsonInfo
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("subtitle")]
    public string Subtitle { get; init; } = string.Empty;

    [JsonPropertyName("artist")]
    public string Artist { get; init; } = string.Empty;

    [JsonPropertyName("subartists")]
    public List<string> Subartists { get; init; } = [];

    [JsonPropertyName("genre")]
    public string Genre { get; init; } = string.Empty;

    [JsonPropertyName("mode_hint")]
    public string ModeHint { get; init; } = "beat-7k";

    [JsonPropertyName("chart_name")]
    public string ChartName { get; init; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; init; } = 1;

    [JsonPropertyName("init_bpm")]
    public double InitBpm { get; init; } = 120.0;

    [JsonPropertyName("judge_rank")]
    public double JudgeRank { get; init; } = 100.0;

    [JsonPropertyName("total")]
    public double Total { get; init; } = 100.0;

    [JsonPropertyName("back_image")]
    public string BackImage { get; init; } = string.Empty;

    [JsonPropertyName("eyecatch_image")]
    public string EyecatchImage { get; init; } = string.Empty;

    [JsonPropertyName("banner_image")]
    public string BannerImage { get; init; } = string.Empty;

    [JsonPropertyName("resolution")]
    public int Resolution { get; init; } = 240;
}

/// <summary>
/// 小節線を定義するイベント。
/// </summary>
public record BmsonLineEvent
{
    [JsonPropertyName("y")]
    public long Y { get; init; }
}

/// <summary>
/// BPM変更イベント。
/// </summary>
public record BmsonBpmEvent
{
    [JsonPropertyName("y")]
    public long Y { get; init; }

    [JsonPropertyName("bpm")]
    public double Bpm { get; init; }
}

/// <summary>
/// ストップ (一時停止) イベント。
/// </summary>
public record BmsonStopEvent
{
    [JsonPropertyName("y")]
    public long Y { get; init; }

    [JsonPropertyName("duration")]
    public long Duration { get; init; }
}

/// <summary>
/// サウンドチャンネル (ステム/キー音)。
/// </summary>
public record BmsonSoundChannel
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("notes")]
    public List<BmsonNote> Notes { get; init; } = [];
}

/// <summary>
/// サウンドチャンネル内に配置されるノーツ。
/// </summary>
public record BmsonNote
{
    /// <summary>
    /// 配置されるレーン (0 = BGM, 1~7 = 鍵盤, 8 = スクラッチ など)。
    /// </summary>
    [JsonPropertyName("x")]
    public int X { get; init; }

    /// <summary>
    /// ノーツが配置される絶対パルス値。
    /// </summary>
    [JsonPropertyName("y")]
    public long Y { get; init; }

    /// <summary>
    /// ロングノーツの長さ (0の場合は単ノート)。
    /// </summary>
    [JsonPropertyName("l")]
    public long L { get; init; } = 0;

    /// <summary>
    /// 音声を継続させるかどうか (<c>true</c> = 継続、<c>false</c> = カット)。
    /// </summary>
    [JsonPropertyName("c")]
    public bool C { get; init; } = true;
}

/// <summary>
/// BGA定義とイベント。
/// </summary>
public record BmsonBga
{
    [JsonPropertyName("bga_header")]
    public List<BmsonBgaHeader> BgaHeader { get; init; } = [];

    [JsonPropertyName("bga_events")]
    public List<BmsonBgaEvent> BgaEvents { get; init; } = [];

    [JsonPropertyName("layer_events")]
    public List<BmsonBgaEvent> LayerEvents { get; init; } = [];

    [JsonPropertyName("poor_events")]
    public List<BmsonBgaEvent> PoorEvents { get; init; } = [];
}

/// <summary>
/// BGA/BMPファイルのヘッダー定義。
/// </summary>
public record BmsonBgaHeader
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// BGA画像を切り替えるイベント。
/// </summary>
public record BmsonBgaEvent
{
    [JsonPropertyName("y")]
    public long Y { get; init; }

    [JsonPropertyName("id")]
    public int Id { get; init; }
}
