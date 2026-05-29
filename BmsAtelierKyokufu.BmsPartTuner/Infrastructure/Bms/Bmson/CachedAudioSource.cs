using NAudio.Wave;
using NAudio.Wave.SampleProviders;

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

    private BaseAudioOptimizationData? _decodedData;
    private readonly Lock _lock = new();

    public BaseAudioOptimizationData DecodedData
    {
        get
        {
            if (_decodedData != null) return _decodedData;
            lock (_lock)
            {
                if (_decodedData != null) return _decodedData;
                _decodedData = DecodeAllData();
                return _decodedData;
            }
        }
    }

    private BaseAudioOptimizationData DecodeAllData()
    {
        int frames = PcmLength / 4; // 16bit stereo = 4 bytes per frame
        float[][] samples = [new float[frames], new float[frames]];

        // Prefix sums need L + 1 length
        double[][] prefixSum = [new double[frames + 1], new double[frames + 1]];
        double[][] prefixSumSq = [new double[frames + 1], new double[frames + 1]];

        // LSH arrays (1 ulong per 64 frames)
        int lshLength = (frames + 63) / 64;
        ulong[][] signLsh = [new ulong[lshLength], new ulong[lshLength]];
        ulong[][] signLshMask = [new ulong[lshLength], new ulong[lshLength]];

        ReadOnlySpan<byte> data = new(RawBytes, PcmOffset, PcmLength);

        // RMS threshold for LSH mask (dbThreshold = -45.0)
        const float silenceThreshold = 0.0056234f; // 10^(-45/20)

        for (int i = 0; i < frames; i++)
        {
            short l = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(data.Slice(i * 4, 2));
            short r = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(data.Slice((i * 4) + 2, 2));

            float fl = l / 32768f;
            float fr = r / 32768f;

            samples[0][i] = fl;
            samples[1][i] = fr;

            // 1. Prefix Sums (offset by 1)
            prefixSum[0][i + 1] = prefixSum[0][i] + fl;
            prefixSum[1][i + 1] = prefixSum[1][i] + fr;

            prefixSumSq[0][i + 1] = prefixSumSq[0][i] + (fl * fl);
            prefixSumSq[1][i + 1] = prefixSumSq[1][i] + (fr * fr);

            // 2. LSH (every 64 frames = 1 ulong)
            int lshIdx = i / 64;
            int bitShift = i % 64;

            // Left channel
            if (fl >= 0) signLsh[0][lshIdx] |= 1UL << bitShift;
            if (Math.Abs(fl) >= silenceThreshold) signLshMask[0][lshIdx] |= 1UL << bitShift;

            // Right channel
            if (fr >= 0) signLsh[1][lshIdx] |= 1UL << bitShift;
            if (Math.Abs(fr) >= silenceThreshold) signLshMask[1][lshIdx] |= 1UL << bitShift;
        }

        return new BaseAudioOptimizationData(samples, prefixSum, prefixSumSq, signLsh, signLshMask);
    }

    public WaveFormat WaveFormat { get; } = new WaveFormat(AppConstants.Audio.StandardSampleRate, 16, 2);

    public CachedAudioSource(string path)
    {
        var timer = PerformanceDebugLogger.StartTimer();

        if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                byte[] fileBytes = File.ReadAllBytes(path);
                if (fileBytes.Length >= 44 &&
                    fileBytes[0] == 'R' && fileBytes[1] == 'I' && fileBytes[2] == 'F' && fileBytes[3] == 'F' &&
                    fileBytes[8] == 'W' && fileBytes[9] == 'A' && fileBytes[10] == 'V' && fileBytes[11] == 'E')
                {
                    int fmtOffset = -1;
                    for (int i = 12; i < fileBytes.Length - 8; i++)
                    {
                        if (fileBytes[i] == 'f' && fileBytes[i + 1] == 'm' && fileBytes[i + 2] == 't' && fileBytes[i + 3] == ' ')
                        {
                            fmtOffset = i;
                            break;
                        }
                    }

                    if (fmtOffset != -1)
                    {
                        short audioFormat = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(fileBytes.AsSpan(fmtOffset + 8, 2));
                        short numChannels = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(fileBytes.AsSpan(fmtOffset + 10, 2));
                        int sampleRateVal = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(fileBytes.AsSpan(fmtOffset + 12, 4));
                        short bitsPerSample = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(fileBytes.AsSpan(fmtOffset + 22, 2));

                        if (audioFormat == 1 && // PCM
                            numChannels == 2 && // Stereo
                            sampleRateVal == AppConstants.Audio.StandardSampleRate && // 44100
                            bitsPerSample == 16) // 16bit
                        {
                            int dataOffset = -1;
                            for (int i = fmtOffset + 8; i < fileBytes.Length - 8; i++)
                            {
                                if (fileBytes[i] == 'd' && fileBytes[i + 1] == 'a' && fileBytes[i + 2] == 't' && fileBytes[i + 3] == 'a')
                                {
                                    dataOffset = i;
                                    break;
                                }
                            }

                            if (dataOffset != -1)
                            {
                                int dataLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(fileBytes.AsSpan(dataOffset + 4, 4));
                                int actualDataStart = dataOffset + 8;
                                if (actualDataStart + dataLength > fileBytes.Length)
                                {
                                    dataLength = fileBytes.Length - actualDataStart;
                                }

                                RawBytes = fileBytes;
                                PcmOffset = actualDataStart;
                                PcmLength = dataLength;

                                long fastElapsed = timer.Lap($"{Path.GetFileName(path)}|CachedAudioSource Load FastPath");
                                PerformanceDebugLogger<CachedAudioSource>.WriteDebug($"[CachedAudioSource Load] {Path.GetFileName(path)} (FastPath Direct Load) loaded in {fastElapsed} ms");
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PerformanceDebugLogger<CachedAudioSource>.WriteError($"[CachedAudioSource] Custom WAV parser failed, falling back to NAudio: {path}", ex);
            }
        }

        WaveStream baseReader;
        if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            baseReader = new NAudio.Vorbis.VorbisWaveReader(path);
        }
        else
        {
            baseReader = new AudioFileReader(path);
        }

        using (baseReader)
        {
            ISampleProvider sampleProvider = baseReader.ToSampleProvider();

            // 1. モノラルの場合はステレオへ変換
            if (sampleProvider.WaveFormat.Channels == 1)
            {
                sampleProvider = new MonoToStereoSampleProvider(sampleProvider);
            }

            // 2. サンプルレートが異なる場合は 44.1kHz へリサンプリング
            if (sampleProvider.WaveFormat.SampleRate != AppConstants.Audio.StandardSampleRate)
            {
                sampleProvider = new WdlResamplingSampleProvider(sampleProvider, AppConstants.Audio.StandardSampleRate);
            }

            timer.Lap($"{Path.GetFileName(path)}|Load_SetupProviders");

            // LOHフラグメンテーションや、壊れたTotalTimeによる数十GBの異常アロケーションを完全に防ぐため、
            // 小さなチャンク（LOH閾値 85,000 bytes 未満）のリストとして読み込み、最後に一度だけ結合する
            const int ChunkSizeBytes = 65536; // 64KB
            const int ChunkSizeElements = ChunkSizeBytes / sizeof(float);

            var pcmChunks = new List<byte[]>();
            int totalBytesWritten = 0;

            float[] chunkBuffer = new float[ChunkSizeElements];

            // NAudioのリサンプラーがストリーム終了後も無音のサンプルを無限に返し続けるバグを防ぐため、
            // 元のオーディオファイルの実際の長さ（TotalTime）を基準にして最大デコード量を制限する。
            // （リサンプリング時の余白として +2.0 秒を加算）
            double maxSeconds = baseReader.TotalTime.TotalSeconds + 2.0;
            const int bytesPerSec = AppConstants.Audio.StandardSampleRate * 2 * 2; // 44100Hz * 2ch * 16bit(2bytes)
            long dynamicMaxBytes = (long)(maxSeconds * bytesPerSec);

            // 万が一 TotalTime が壊れているファイルに備えて、絶対上限も250MBとする
            const int AbsoluteMaxBytes = 250 * 1024 * 1024;
            long maxAllowedBytes = Math.Min(dynamicMaxBytes, AbsoluteMaxBytes);

            while (true)
            {
                int read = sampleProvider.Read(chunkBuffer, 0, chunkBuffer.Length);
                if (read <= 0) break;

                int neededBytes = read * 2;

                if (totalBytesWritten + neededBytes > maxAllowedBytes)
                {
                    PerformanceDebugLogger<CachedAudioSource>.WriteDebug($"[AudioSliceManager] Reached length limit ({maxAllowedBytes} bytes) for {Path.GetFileName(path)}. Stopping decode.");
                    break;
                }

                byte[] pcmChunk = new byte[neededBytes]; // LOHに乗らないサイズ

                ConvertFloatTo16BitPcm(new ReadOnlySpan<float>(chunkBuffer, 0, read), pcmChunk);

                pcmChunks.Add(pcmChunk);
                totalBytesWritten += neededBytes;
            }

            timer.Lap($"{Path.GetFileName(path)}|Load_ReadFloat_And_ConvertPCM");

            // 全て読み終わった後、必要な正確なサイズの配列を1回だけアロケートして結合する
            byte[] finalPcmData = new byte[totalBytesWritten];
            int offset = 0;
            foreach (var chunk in pcmChunks)
            {
                Buffer.BlockCopy(chunk, 0, finalPcmData, offset, chunk.Length);
                offset += chunk.Length;
            }

            timer.Lap($"{Path.GetFileName(path)}|Combine_Chunks");

            RawBytes = finalPcmData;
            PcmOffset = 0;
            PcmLength = totalBytesWritten;
        }

        long elapsed = timer.Lap($"{Path.GetFileName(path)}|CachedAudioSource Load");
        PerformanceDebugLogger<CachedAudioSource>.WriteTrace($"[{path}] WaveFormat: {WaveFormat.SampleRate}Hz, {WaveFormat.Channels}ch, {WaveFormat.BitsPerSample}bit");
        PerformanceDebugLogger<CachedAudioSource>.WriteDebug($"[CachedAudioSource Load] {Path.GetFileName(path)} (Format: {WaveFormat.SampleRate}Hz, {WaveFormat.Channels}ch, Size: {PcmLength} bytes) loaded in {elapsed} ms");
    }

    /// <summary>
    /// floatサンプルから16-bit PCM（リトルエンディアン）へ一括でスケール＆クランプ変換します。
    /// Math.Clamp を用いることで、.NET JITコンパイルにより自動的にSIMDのMax/Min命令に最適化されます。
    /// </summary>
    private static void ConvertFloatTo16BitPcm(ReadOnlySpan<float> source, Span<byte> destination)
    {
        int sampleCount = source.Length;
        for (int i = 0; i < sampleCount; i++)
        {
            float sample = source[i] * 32767f;
            short val = (short)Math.Clamp(sample, -32768f, 32767f);

            int destIdx = i * 2;
            destination[destIdx] = (byte)val;
            destination[destIdx + 1] = (byte)(val >> 8);
        }
    }
}
