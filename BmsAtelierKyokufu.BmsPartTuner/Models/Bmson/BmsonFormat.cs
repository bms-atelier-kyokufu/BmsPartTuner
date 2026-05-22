using System.Text.Json.Serialization;

namespace BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;

/// <summary>
/// bmsonフォーマットのルート要素。
/// </summary>
public class BmsonFormat
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("info")]
    public BmsonInfo Info { get; set; } = new();

    [JsonPropertyName("lines")]
    public List<BmsonLineEvent> Lines { get; set; } = [];

    [JsonPropertyName("bpm_events")]
    public List<BmsonBpmEvent> BpmEvents { get; set; } = [];

    [JsonPropertyName("stop_events")]
    public List<BmsonStopEvent> StopEvents { get; set; } = [];

    [JsonPropertyName("sound_channels")]
    public List<BmsonSoundChannel> SoundChannels { get; set; } = [];

    [JsonPropertyName("bga")]
    public BmsonBga? Bga { get; set; }
}

/// <summary>
/// 楽曲のメタデータ情報。
/// </summary>
public class BmsonInfo
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("subtitle")]
    public string Subtitle { get; set; } = string.Empty;

    [JsonPropertyName("artist")]
    public string Artist { get; set; } = string.Empty;

    [JsonPropertyName("subartists")]
    public List<string> Subartists { get; set; } = [];

    [JsonPropertyName("genre")]
    public string Genre { get; set; } = string.Empty;

    [JsonPropertyName("mode_hint")]
    public string ModeHint { get; set; } = "beat-7k";

    [JsonPropertyName("chart_name")]
    public string ChartName { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("init_bpm")]
    public double InitBpm { get; set; } = 120.0;

    [JsonPropertyName("judge_rank")]
    public double JudgeRank { get; set; } = 100.0;

    [JsonPropertyName("total")]
    public double Total { get; set; } = 100.0;

    [JsonPropertyName("back_image")]
    public string BackImage { get; set; } = string.Empty;

    [JsonPropertyName("eyecatch_image")]
    public string EyecatchImage { get; set; } = string.Empty;

    [JsonPropertyName("banner_image")]
    public string BannerImage { get; set; } = string.Empty;

    [JsonPropertyName("resolution")]
    public int Resolution { get; set; } = 240;
}

/// <summary>
/// 小節線を定義するイベント。
/// </summary>
public class BmsonLineEvent
{
    [JsonPropertyName("y")]
    public long Y { get; set; }
}

/// <summary>
/// BPM変更イベント。
/// </summary>
public class BmsonBpmEvent
{
    [JsonPropertyName("y")]
    public long Y { get; set; }

    [JsonPropertyName("bpm")]
    public double Bpm { get; set; }
}

/// <summary>
/// ストップ（一時停止）イベント。
/// </summary>
public class BmsonStopEvent
{
    [JsonPropertyName("y")]
    public long Y { get; set; }

    [JsonPropertyName("duration")]
    public long Duration { get; set; }
}

/// <summary>
/// サウンドチャンネル（ステム/キー音）。
/// </summary>
public class BmsonSoundChannel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public List<BmsonNote> Notes { get; set; } = [];
}

/// <summary>
/// サウンドチャンネル内に配置されるノーツ。
/// </summary>
public class BmsonNote
{
    /// <summary>
    /// 配置されるレーン。0=BGM、1~7=鍵盤、8=スクラッチ など。
    /// </summary>
    [JsonPropertyName("x")]
    public int X { get; set; }

    /// <summary>
    /// ノーツが配置される絶対パルス値。
    /// </summary>
    [JsonPropertyName("y")]
    public long Y { get; set; }

    /// <summary>
    /// ロングノーツの長さ。0の場合は単ノート。
    /// </summary>
    [JsonPropertyName("l")]
    public long L { get; set; } = 0;

    /// <summary>
    /// 音声を継続させるか（true=継続、false=カット）。
    /// </summary>
    [JsonPropertyName("c")]
    public bool C { get; set; } = true;
}

/// <summary>
/// BGA定義とイベント。
/// </summary>
public class BmsonBga
{
    [JsonPropertyName("bga_header")]
    public List<BmsonBgaHeader> BgaHeader { get; set; } = [];

    [JsonPropertyName("bga_events")]
    public List<BmsonBgaEvent> BgaEvents { get; set; } = [];

    [JsonPropertyName("layer_events")]
    public List<BmsonBgaEvent> LayerEvents { get; set; } = [];

    [JsonPropertyName("poor_events")]
    public List<BmsonBgaEvent> PoorEvents { get; set; } = [];
}

/// <summary>
/// BGA/BMPファイルのヘッダー定義。
/// </summary>
public class BmsonBgaHeader
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// BGA画像を切り替えるイベント。
/// </summary>
public class BmsonBgaEvent
{
    [JsonPropertyName("y")]
    public long Y { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }
}
