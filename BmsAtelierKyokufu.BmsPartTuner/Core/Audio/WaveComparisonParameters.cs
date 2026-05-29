using System.Runtime.CompilerServices;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

/// <summary>
/// FastWaveCompare.CalculateMaxCorrelation の引数をカプセル化する readonly ref struct。
/// </summary>
internal readonly ref struct WaveComparisonParameters
{
    public readonly ICachedSoundData Shorter;
    public readonly ICachedSoundData Longer;
    public readonly int TargetChannel;
    public readonly int ShorterFrames;
    public readonly int LongerFrames;
    public readonly ReadOnlySpan<float> ShorterSpan;
    public readonly ReadOnlySpan<float> LongerFullSpan;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WaveComparisonParameters(
        ICachedSoundData shorter,
        ICachedSoundData longer,
        int targetChannel,
        int shorterFrames,
        int longerFrames,
        ReadOnlySpan<float> shorterSpan,
        ReadOnlySpan<float> longerFullSpan)
    {
        Shorter = shorter;
        Longer = longer;
        TargetChannel = targetChannel;
        ShorterFrames = shorterFrames;
        LongerFrames = longerFrames;
        ShorterSpan = shorterSpan;
        LongerFullSpan = longerFullSpan;
    }
}
