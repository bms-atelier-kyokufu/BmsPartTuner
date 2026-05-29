namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

/// <summary>
/// Audio file properties extracted during parsing.
/// </summary>
public record AudioFileInfo(string FilePath, int SampleRate, int Channels, int BitsPerSample, int TotalSamples, long FileSize);
