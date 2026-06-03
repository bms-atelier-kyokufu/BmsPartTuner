using NAudio.Wave;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson;

/// <summary>
/// 音声ソースファイルをロードし、事前にステレオ・44.1kHz・16bit PCMへ変換してキャッシュするクラス。
/// </summary>
[ADRAnchor("M-05", nameof(CachedAudioSource))]
public class CachedAudioSource
{
    public byte[] RawBytes { get; }
    public int PcmOffset { get; }
    public int PcmLength { get; }

    /// <summary>
    /// 無音トリミング用の固定長ブロックごとのエネルギーマップ
    /// </summary>
    public long[] EnergyMap { get; private set; } = [];
    public const int EnergyMapWindowFrames = 256;

    private BaseAudioOptimizationData? _decodedData;
    private readonly Lock _lock = new();
    private static readonly Logger<CachedAudioSource> s_logger = new();

    public BaseAudioOptimizationData DecodedData
    {
        get
        {
            if (_decodedData != null) return _decodedData;
            lock (_lock)
            {
                if (_decodedData != null) return _decodedData;
                _decodedData = AudioOptimizationDecoder.DecodeAllData(RawBytes, PcmOffset, PcmLength);
                return _decodedData;
            }
        }
    }

    public WaveFormat WaveFormat { get; } = new WaveFormat(AppConstants.Audio.StandardSampleRate, 16, 2);

    public CachedAudioSource(string path)
    {
        var timer = s_logger.StartTimer();

        if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            if (FastWavLoader.TryLoad(path, out byte[] fastRawBytes, out int fastPcmOffset, out int fastPcmLength))
            {
                RawBytes = fastRawBytes;
                PcmOffset = fastPcmOffset;
                PcmLength = fastPcmLength;

                CalculateEnergyMap();

                long fastElapsed = timer.Lap($"{Path.GetFileName(path)}|CachedAudioSource Load FastPath");
                s_logger.WriteDebug($"[CachedAudioSource Load] {Path.GetFileName(path)} (FastPath Direct Load) loaded in {fastElapsed} ms");
                return;
            }
            else
            {
                s_logger.WriteError($"[CachedAudioSource] Custom WAV parser failed, falling back to NAudio: {path}");
            }
        }

        // Fast Path が失敗したか .ogg の場合は NAudio でフォールバック
        var (rawBytes, pcmLength) = NAudioFallbackLoader.Load(path, s_logger, timer);
        RawBytes = rawBytes;
        PcmOffset = 0;
        PcmLength = pcmLength;

        CalculateEnergyMap();

        long elapsed = timer.Lap($"{Path.GetFileName(path)}|CachedAudioSource Load");
        s_logger.WriteTrace($"[{path}] WaveFormat: {WaveFormat.SampleRate}Hz, {WaveFormat.Channels}ch, {WaveFormat.BitsPerSample}bit");
        s_logger.WriteDebug($"[CachedAudioSource Load] {Path.GetFileName(path)} (Format: {WaveFormat.SampleRate}Hz, {WaveFormat.Channels}ch, Size: {PcmLength} bytes) loaded in {elapsed} ms");
    }

    private void CalculateEnergyMap()
    {
        int frames = PcmLength / 4; // 16bit Stereo = 4 bytes per frame
        int mapSize = (frames + EnergyMapWindowFrames - 1) / EnergyMapWindowFrames;
        long[] map = new long[mapSize];

        ReadOnlySpan<short> pcm = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, short>(
            new ReadOnlySpan<byte>(RawBytes, PcmOffset, PcmLength));

        for (int i = 0; i < mapSize; i++)
        {
            int startFrame = i * EnergyMapWindowFrames;
            int framesToProcess = Math.Min(EnergyMapWindowFrames, frames - startFrame);

            if (framesToProcess == EnergyMapWindowFrames && System.Runtime.Intrinsics.X86.Avx2.IsSupported)
            {
                map[i] = SilenceTrimmer.CalculateEnergyAvx2(pcm.Slice(startFrame * 2, framesToProcess * 2));
            }
            else
            {
                long energy = 0;
                int startSample = startFrame * 2;
                int endSample = startSample + (framesToProcess * 2);
                for (int s = startSample; s < endSample; s += 2)
                {
                    long l = pcm[s];
                    long r = pcm[s + 1];
                    energy += (l * l) + (r * r);
                }
                map[i] = energy;
            }
        }
        EnergyMap = map;
    }
}
