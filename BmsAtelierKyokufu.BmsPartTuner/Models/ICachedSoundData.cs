namespace BmsAtelierKyokufu.BmsPartTuner.Models;

/// <summary>
/// 髻ｳ螢ｰ繧ｭ繝｣繝・す繝･繝・・繧ｿ縺ｮ蜈ｱ騾壹う繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ縲・/// BMS・井ｺ句燕豁｣隕丞喧譁ｹ蠑擾ｼ峨→bmson・医・繧､繝ｳ繧ｿ譁ｹ蠑擾ｼ峨・荳｡譁ｹ縺ｫ蟇ｾ蠢懊☆繧九◆繧√・謚ｽ雎｡蛹悶・/// </summary>
public interface ICachedSoundData : IDisposable
{
    /// <summary>繝輔ぃ繧､繝ｫ縺ｮ繝代せ縲√∪縺溘・隴伜挨蟄舌・/summary>
    string FilePath { get; }

    /// <summary>繧ｵ繝ｳ繝励Μ繝ｳ繧ｰ繝ｬ繝ｼ繝医・/summary>
    int SampleRate { get; }

    /// <summary>繝√Ε繝ｳ繝阪Ν謨ｰ・井ｾ・ 2=繧ｹ繝・Ξ繧ｪ・峨・/summary>
    int Channels { get; }

    int BitsPerSample { get; }

    long FileSize { get; }

    /// <summary>蜈ｨ菴薙・邱上し繝ｳ繝励Ν謨ｰ縲・/summary>
    int TotalSamples { get; }

    /// <summary>蜈ｨ菴薙・RMS・磯浹蝨ｧ・峨よｯ碑ｼ・燕縺ｮ鬮倬溘ヵ繧｣繝ｫ繧ｿ繝ｪ繝ｳ繧ｰ縺ｫ菴ｿ逕ｨ縲・/summary>
    float TotalRms { get; }

    /// <summary>蜈磯�ｭ縺ｮ辟｡髻ｳ繧ｵ繝ｳ繝励Ν謨ｰ縲・/summary>
    int StartSilenceSamples { get; }

    /// <summary>譛牙柑縺ｪ髟ｷ縺包ｼ育ｷ上し繝ｳ繝励Ν謨ｰ - 蜈磯�ｭ辟｡髻ｳ・峨・/summary>
    int EffectiveLength { get; }

    /// <summary>繝｡繝｢繝ｪ菴ｿ逕ｨ驥上・謗ｨ螳壼､・・B・峨・/summary>
    double EstimatedMemoryMB { get; }

    /// <summary>莠句燕豁｣隕丞喧貂医∩縺ｮ繝・・繧ｿ繧呈戟縺｣縺ｦ縺・ｋ縺具ｼ・rue=BMS逕ｨ, false=bmson逕ｨ・峨・/summary>
    bool IsPreNormalized { get; }

    /// <summary>
    /// 譛蛾浹蛹ｺ髢難ｼ・ctiveRegion・峨・繝ｪ繧ｹ繝医ｒ蜿門ｾ励＠縺ｾ縺吶・    /// 莠句燕豁｣隕丞喧譁ｹ蠑上・蝣ｴ蜷医・ Data (float[]) 縺瑚ｨｭ螳壹＆繧後※縺・∪縺吶・    /// </summary>
    System.Collections.Generic.IReadOnlyList<ActiveRegion>[] GetActiveRegions();

    /// <summary>
    /// 謖・ｮ壹＆繧後◆繝√Ε繝ｳ繝阪Ν縺ｮ縲∫函縺ｮ豕｢蠖｢繝・・繧ｿ・・pan・峨ｒ蜿門ｾ励＠縺ｾ縺吶・    /// 繝昴う繝ｳ繧ｿ譁ｹ蠑擾ｼ・mson・峨・蝣ｴ蜷医↓菴ｿ逕ｨ縺励∪縺吶・    /// </summary>
    /// <param name="channel">繝√Ε繝ｳ繝阪Ν逡ｪ蜿ｷ・・ or 1・・/param>
    /// <param name="offset">繧ｪ繝輔そ繝・ヨ・医し繝ｳ繝励Ν蜊倅ｽ搾ｼ・/param>
    /// <param name="length">髟ｷ縺包ｼ医し繝ｳ繝励Ν蜊倅ｽ搾ｼ・/param>
    /// <returns>豕｢蠖｢繝・・繧ｿ</returns>
    /// <summary>
    /// 指定されたチャンネルの、生の波形データ（Span）を取得します。
    /// ポインタ方式（bmson）の場合に使用します。
    /// </summary>
    /// <param name="channel">チャンネル番号（0 or 1）</param>
    /// <param name="offset">オフセット（サンプル単位）</param>
    /// <param name="length">長さ（サンプル単位）</param>
    /// <returns>波形データ</returns>
    System.ReadOnlySpan<float> GetRawSpan(int channel, int offset, int length);

    /// <summary>
    /// 指定されたチャンネルの生データの総和（Σx）を取得します（1パスSIMD用）。
    /// 事前正規化方式の場合は使用されないため 0 または NotSupportedException を返します。
    /// </summary>
    /// <param name="channel">チャンネル番号（0 or 1）</param>
    double GetChannelSum(int channel);

    /// <summary>
    /// 指定されたチャンネルの生データの二乗和（Σx²）を取得します（1パスSIMD用）。
    /// 事前正規化方式の場合は使用されないため 0 または NotSupportedException を返します。
    /// </summary>
    /// <param name="channel">チャンネル番号（0 or 1）</param>
    double GetChannelSumSq(int channel);
}
