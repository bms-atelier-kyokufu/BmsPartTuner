using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson;

/// <summary>
/// 高速パースが不可能な WAV ファイルや、.ogg ファイルなどを
/// NAudio / VorbisWaveReader を用いて 16-bit 44.1kHz ステレオの PCM に変換しロードするヘルパークラス。
/// </summary>
internal static class NAudioFallbackLoader
{
    public static (byte[] rawBytes, int pcmLength) Load(string path, Logger<CachedAudioSource> logger, Infrastructure.Diagnostics.Logger.PerformanceTimer timer)
    {
        string fileName = Path.GetFileName(path);

        using WaveStream baseReader = CreateWaveReader(path);
        ISampleProvider sampleProvider = CreateSampleProvider(baseReader);
        timer.Lap($"{fileName}|Load_SetupProviders");

        (List<byte[]> chunks, int totalBytesWritten) = ReadAndConvertChunks(sampleProvider, baseReader, fileName, logger);
        timer.Lap($"{fileName}|Load_ReadFloat_And_ConvertPCM");

        byte[] finalPcmData = CombineChunks(chunks, totalBytesWritten);
        timer.Lap($"{fileName}|Combine_Chunks");

        return (finalPcmData, totalBytesWritten);
    }

    private static WaveStream CreateWaveReader(string path)
    {
        if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            return new NAudio.Vorbis.VorbisWaveReader(path);
        }
        else
        {
            return new AudioFileReader(path);
        }
    }

    private static ISampleProvider CreateSampleProvider(WaveStream baseReader)
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

        return sampleProvider;
    }

    private static (List<byte[]> chunks, int totalBytesWritten) ReadAndConvertChunks(
        ISampleProvider sampleProvider,
        WaveStream baseReader,
        string fileName,
        Logger<CachedAudioSource> logger)
    {
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
                logger.WriteDebug($"[AudioSliceManager] Reached length limit ({maxAllowedBytes} bytes) for {fileName}. Stopping decode.");
                break;
            }

            byte[] pcmChunk = new byte[neededBytes]; // LOHに乗らないサイズ

            ConvertFloatTo16BitPcm(new ReadOnlySpan<float>(chunkBuffer, 0, read), pcmChunk);

            pcmChunks.Add(pcmChunk);
            totalBytesWritten += neededBytes;
        }

        return (pcmChunks, totalBytesWritten);
    }

    private static byte[] CombineChunks(List<byte[]> chunks, int totalBytes)
    {
        // 全て読み終わった後、必要な正確なサイズの配列を1回だけアロケートして結合する
        byte[] finalPcmData = new byte[totalBytes];
        int offset = 0;
        foreach (var chunk in chunks)
        {
            Buffer.BlockCopy(chunk, 0, finalPcmData, offset, chunk.Length);
            offset += chunk.Length;
        }
        return finalPcmData;
    }

    /// <summary>
    /// floatサンプルから16-bit PCM（リトルエンディアン）へ一括でスケール＆クランプ変換します。
    /// Math.Clamp を用いることで、.NET JITコンパイルにより自動的にSIMD of Max/Min命令に最適化されます。
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

