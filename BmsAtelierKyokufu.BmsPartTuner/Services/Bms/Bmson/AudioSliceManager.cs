using System.Collections.Concurrent;
using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.Bms.Bmson;

/// <summary>
/// bmsonのノート情報に基づき、元の音声ファイル（ステムなど）を指定時間で切り出し、
/// BMS用の短いWAVスライスを生成するマネージャー。
/// </summary>
public class AudioSliceManager(string bmsonDir, bool throwOnMissingFile = true) : IDisposable
{
    private readonly string _bmsonDir = bmsonDir;

    private static readonly byte[] WavHeaderTemplate = CreateWavHeaderTemplate();

    /// <summary>
    /// WAVヘッダの不変フィールドを事前設定した44バイトのテンプレート配列を生成します。
    /// </summary>
    private static byte[] CreateWavHeaderTemplate()
    {
        byte[] template = new byte[44];
        using (var ms = new MemoryStream(template))
        using (var writer = new BinaryWriter(ms))
        {
            writer.Write("RIFF"u8);
            writer.Write(0); // fileSize (36 + dataLengthBytes) のプレースホルダー
            writer.Write("WAVE"u8);

            writer.Write("fmt "u8);
            writer.Write(16); // Subchunk1Size = 16 (PCM)
            writer.Write((short)1); // AudioFormat = 1 (PCM)
            writer.Write((short)2); // NumChannels = 2 (Stereo)
            writer.Write(AppConstants.Audio.StandardSampleRate);
            writer.Write(AppConstants.Audio.StandardSampleRate * 2 * 2); // ByteRate
            writer.Write((short)4); // BlockAlign = 4
            writer.Write((short)16); // BitsPerSample = 16

            writer.Write("data"u8);
            writer.Write(0); // dataLengthBytes のプレースホルダー
        }
        return template;
    }

    // key: "fileName|offsetSec|durationSec", value: "outputFileName.wav"
    private readonly ConcurrentDictionary<string, Lazy<string>> _sliceCache = new();
    private readonly ConcurrentDictionary<string, CachedAudioSource?> _sourceCache = new(StringComparer.OrdinalIgnoreCase);
    private int _sliceCounter = 1;
    private int _cacheHitCount = 0;
    private int _cacheMissCount = 0;

    public int GetCacheHitCount() => _cacheHitCount;
    public int GetCacheMissCount() => _cacheMissCount;

    /// <summary>
    /// 指定された音声ファイルの特定区間を切り出し、ステレオ・44.1kHz・16bitのWAVとして保存します。
    /// 同一区間が要求された場合は、キャッシュされたファイル名を返します。
    /// </summary>
    /// <param name="sourceFileName">元の音声ファイル名</param>
    /// <param name="offsetSec">切り出し開始時間（秒）</param>
    /// <param name="durationSec">切り出し長さ（秒）</param>
    /// <returns>生成されたWAVファイル名。失敗時や無効な範囲の場合は空文字列。</returns>
    public string SliceAudio(string sourceFileName, double offsetSec, double durationSec)
    {
        if (string.IsNullOrWhiteSpace(sourceFileName)) return string.Empty;

        // 小数点第6位までの精度でキャッシュキーを作成 (約1マイクロ秒の精度)
        string cacheKey = $"{sourceFileName}|{offsetSec:F6}|{durationSec:F6}";

        bool isNew = false;
        var lazyVal = _sliceCache.GetOrAdd(cacheKey, key =>
        {
            isNew = true;
            return new Lazy<string>(() =>
            {
                Interlocked.Increment(ref _cacheMissCount);
                var timer = PerformanceDebugLogger.StartTimer();

                var source = GetOrLoadAudioSource(sourceFileName);
                timer.Lap($"{sourceFileName}|SourceGet");

                if (source == null) return string.Empty;

                string outputFileName = GenerateSliceFileName(sourceFileName);

                try
                {
                    var (startByte, lengthBytes) = CalculateByteRange(source.PcmLength, offsetSec, durationSec);
                    if (lengthBytes <= 0)
                    {
                        return string.Empty;
                    }

                    timer.Lap($"{sourceFileName}|ProviderSetup");

                    // 実バイト配列を作成せず、仮想ファイルを登録（遅延生成）
                    var virtualFile = new SlicedVirtualFile(source.RawBytes, source.PcmOffset + startByte, lengthBytes, WavHeaderTemplate);
                    timer.Lap($"{sourceFileName}|WriteWav");

                    VirtualAudioRegistry.AddFile(outputFileName, virtualFile);
                    timer.Lap($"{sourceFileName}|Registry");

                    return outputFileName;
                }
                catch (Exception ex)
                {
                    PerformanceDebugLogger.WriteError($"[AudioSliceManager] スライス失敗: {sourceFileName}", ex);
                    return string.Empty;
                }
            });
        });

        if (!isNew)
        {
            Interlocked.Increment(ref _cacheHitCount);
        }

        return lazyVal.Value;
    }

    /// <summary>
    /// 生成されたスライスの総数を取得します。
    /// </summary>
    public int GetGeneratedSliceCount() => _sliceCache.Values.Count(v => !string.IsNullOrEmpty(v.Value));

    public void Dispose()
    {
        _sourceCache.Clear();
        _sliceCache.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 音源キャッシュから取得するか、存在しない場合は新しくロードしてキャッシュに登録します。
    /// </summary>
    private CachedAudioSource? GetOrLoadAudioSource(string sourceFileName)
    {
        return _sourceCache.GetOrAdd(sourceFileName, name =>
        {
            string sourcePath = Path.Combine(_bmsonDir, name);
            if (!File.Exists(sourcePath))
            {
                if (throwOnMissingFile)
                {
                    throw new FileNotFoundException($"音源ファイルが見つかりません: {name} (Path: {sourcePath})");
                }
                return null;
            }
            return new CachedAudioSource(sourcePath);
        });
    }

    /// <summary>
    /// BmsPartTunerの命名規則に合わせたスライスファイル名を生成します。
    /// </summary>
    private string GenerateSliceFileName(string sourceFileName)
    {
        string nameWithoutExt = Path.GetFileNameWithoutExtension(sourceFileName);
        string prefix = string.IsNullOrEmpty(nameWithoutExt)
            ? "Slice"
            : char.ToUpper(nameWithoutExt[0]) + nameWithoutExt[1..];

        int currentCount = Interlocked.Increment(ref _sliceCounter) - 1;
        return $"{prefix}_{currentCount:D4}.wav";
    }

    /// <summary>
    /// 音声切り出し範囲から、キャッシュデータの開始バイト位置と長さを計算します（4バイト境界にアライメント）。
    /// </summary>
    private static (int startByte, int lengthBytes) CalculateByteRange(int pcmLength, double offsetSec, double durationSec)
    {
        const int bytesPerSec = AppConstants.Audio.StandardSampleRate * 2 * 2; // 44100Hz * 2ch * 2bytes (16bit)
        double totalSeconds = (double)pcmLength / bytesPerSec;

        if (offsetSec >= totalSeconds)
        {
            return (0, 0);
        }

        double actualDuration = durationSec;
        if (offsetSec + actualDuration > totalSeconds)
        {
            actualDuration = totalSeconds - offsetSec;
        }

        if (actualDuration <= 0)
        {
            return (0, 0);
        }

        long startByte = (long)(offsetSec * bytesPerSec);
        long lengthBytes = (long)(actualDuration * bytesPerSec);

        startByte = Math.Max(0, Math.Min(pcmLength, startByte));
        startByte -= startByte % 4;

        long endByte = startByte + lengthBytes;
        endByte = Math.Max(startByte, Math.Min(pcmLength, endByte));
        endByte -= endByte % 4;

        return ((int)startByte, (int)(endByte - startByte));
    }

    /// <summary>
    /// 指定された音声ファイルを先行してロード・デコードしキャッシュします（投機的プリロード）。
    /// </summary>
    public void PreloadAudioSource(string sourceFileName)
    {
        if (string.IsNullOrWhiteSpace(sourceFileName)) return;
        GetOrLoadAudioSource(sourceFileName);
    }

    /// <summary>
    /// 音声ソースファイルをロードし、事前にステレオ・44.1kHz・16bit PCMへ変換してキャッシュするクラス。
    /// </summary>
    private class CachedAudioSource
    {
        public byte[] RawBytes { get; }
        public int PcmOffset { get; }
        public int PcmLength { get; }
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
                                    PerformanceDebugLogger.WriteLine($"[CachedAudioSource Load] {Path.GetFileName(path)} (FastPath Direct Load) loaded in {fastElapsed} ms", LogLevel.Debug);
                                    return;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    PerformanceDebugLogger.WriteError($"[CachedAudioSource] Custom WAV parser failed, falling back to NAudio: {path}", ex);
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

                double totalSeconds = baseReader.TotalTime.TotalSeconds;
                int estimatedSamples = (int)(totalSeconds * AppConstants.Audio.StandardSampleRate * 2) + 65536;
                float[] floatBuffer;
                int totalSamplesRead = 0;

                // もし元の形式が 44.1kHz 16bit stereo ならば、一括で読み込める
                if (sampleProvider == baseReader.ToSampleProvider() || sampleProvider.GetType().Name == "SampleChannel")
                {
                    // For performance, read in large chunks instead of small ones
                    floatBuffer = new float[estimatedSamples];
                    int chunkSize = 1048576; // 1MB chunks
                    while (true)
                    {
                        if (totalSamplesRead + chunkSize > floatBuffer.Length)
                        {
                            Array.Resize(ref floatBuffer, floatBuffer.Length + chunkSize * 2);
                        }
                        int read = sampleProvider.Read(floatBuffer, totalSamplesRead, chunkSize);
                        if (read <= 0) break;
                        totalSamplesRead += read;
                    }
                }
                else
                {
                    floatBuffer = new float[estimatedSamples];
                    int chunkSize = 16384;
                    while (true)
                    {
                        if (totalSamplesRead + chunkSize > floatBuffer.Length)
                        {
                            Array.Resize(ref floatBuffer, floatBuffer.Length * 2);
                        }
                        int read = sampleProvider.Read(floatBuffer, totalSamplesRead, chunkSize);
                        if (read <= 0) break;
                        totalSamplesRead += read;
                    }
                }

                timer.Lap($"{Path.GetFileName(path)}|Load_ReadFloat");

                byte[] pcmData = new byte[totalSamplesRead * 2];
                ConvertFloatTo16BitPcm(new ReadOnlySpan<float>(floatBuffer, 0, totalSamplesRead), pcmData);
                timer.Lap($"{Path.GetFileName(path)}|Load_ConvertPCM");

                RawBytes = pcmData;
                PcmOffset = 0;
                PcmLength = pcmData.Length;
            }

            long elapsed = timer.Lap($"{Path.GetFileName(path)}|CachedAudioSource Load");
            PerformanceDebugLogger.WriteLine($"[CachedAudioSource Load] {Path.GetFileName(path)} (Format: {WaveFormat.SampleRate}Hz, {WaveFormat.Channels}ch, Size: {PcmLength} bytes) loaded in {elapsed} ms", LogLevel.Debug);
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
}

