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
    private readonly ConcurrentDictionary<string, Lazy<CachedAudioSource?>> _sourceCache = new(StringComparer.OrdinalIgnoreCase);
    private int _sliceCounter = 1;
    private int _cacheHitCount = 0;
    private int _cacheMissCount = 0;

    public int GetCacheHitCount() => _cacheHitCount;
    public int GetCacheMissCount() => _cacheMissCount;

    /// <summary>
    /// 無音判定のエネルギー閾値（実行前に1度だけ計算）。
    /// </summary>
    private static readonly long E_threshold = CalculateEnergyThreshold();
    private const int WindowFrames = 1024; // 約23msのウィンドウサイズ

    private static long CalculateEnergyThreshold()
    {
        const double dbThreshold = -45.0; // -45dBを無音とみなす（フロアノイズ対応）
        const double maxAmp = 32768.0;    // 16bit PCMの最大振幅
        const int totalSamples = WindowFrames * 2; // ステレオなのでフレーム数の2倍
        return (long)(totalSamples * maxAmp * maxAmp * Math.Pow(10, dbThreshold / 10.0));
    }

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

        // 1. まず音源のキャッシュを取得（スレッドセーフにロードされる）
        var source = GetOrLoadAudioSource(sourceFileName);
        if (source == null) return string.Empty;

        // 2. 要求された開始位置と長さを計算
        var (startByte, lengthBytes) = CalculateByteRange(source.PcmLength, offsetSec, durationSec);
        if (lengthBytes <= 0) return string.Empty;

        // 3. 末尾の無音部分をトリミングして、本当に必要な長さに切り詰める
        int trimmedLengthBytes = TrimSilenceFromEnd(source.RawBytes, source.PcmOffset + startByte, lengthBytes);
        if (trimmedLengthBytes <= 0) return string.Empty;

        if (trimmedLengthBytes < lengthBytes)
        {
            PerformanceDebugLogger.WriteTrace($"[AudioSliceManager] Trimmed silence: {sourceFileName} (offset={offsetSec:F2}s, duration={durationSec:F2}s) from {lengthBytes / 1024.0:F1}KB to {trimmedLengthBytes / 1024.0:F1}KB");
        }

        // 4. トリミング後の真の長さを用いてキャッシュキーを作成
        // これにより、要求長(durationSec)が異なっていても、実体が同じなら100%キャッシュヒットする
        string cacheKey = $"{sourceFileName}|{startByte}|{trimmedLengthBytes}";

        bool isNew = false;
        var lazyVal = _sliceCache.GetOrAdd(cacheKey, _ =>
        {
            isNew = true;
            return new Lazy<string>(() =>
            {
                Interlocked.Increment(ref _cacheMissCount);
                string outputFileName = GenerateSliceFileName(sourceFileName);

                try
                {
                    // 実バイト配列を作成せず、仮想ファイルを登録（遅延生成）
                    var virtualFile = new SlicedVirtualFile(source.RawBytes, source.PcmOffset + startByte, trimmedLengthBytes, WavHeaderTemplate);
                    VirtualAudioRegistry.AddFile(outputFileName, virtualFile);

                    // ポインタを生成し、レジストリに登録 (16bit stereo = 4 bytes per frame)
                    var pointerData = new PointerSoundData(
                        outputFileName,
                        source.DecodedData,
                        startByte / 4,
                        trimmedLengthBytes / 4
                    );
                    PointerAudioRegistry.Register(outputFileName, pointerData);

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
        var lazySource = _sourceCache.GetOrAdd(sourceFileName, name => new Lazy<CachedAudioSource?>(() =>
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
        }, LazyThreadSafetyMode.ExecutionAndPublication));

        return lazySource.Value;
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
    /// O(1) の差分更新スライディングウィンドウを用いて、末尾の無音部分をトリミングします。
    /// </summary>
    private static int TrimSilenceFromEnd(byte[] data, int startOffset, int length)
    {
        const int frameSize = 4; // 16bit Stereo = 4 bytes per frame
        int totalFrames = length / frameSize;

        // ウィンドウサイズより短い場合はトリミングしない（安全策）
        if (totalFrames <= WindowFrames) return length;

        // 初期ウィンドウ (末尾の WindowFrames フレーム) のエネルギーを計算 (O(N))
        long currentEnergy = 0;
        int windowStartFrame = totalFrames - WindowFrames;

        for (int i = 0; i < WindowFrames; i++)
        {
            currentEnergy += GetFrameEnergy(data, startOffset + ((windowStartFrame + i) * frameSize));
        }

        int trimFrames = 0;

        // 後方探索 (O(1) のスライディングウィンドウ)
        while (windowStartFrame > 0)
        {
            if (currentEnergy >= E_threshold)
            {
                // 音が見つかった。この時点での末尾は windowStartFrame + WindowFrames となる。
                break;
            }

            trimFrames++;
            windowStartFrame--;

            // ウィンドウから外れるフレームのエネルギーを引き、新しく入るフレームのエネルギーを足す
            long outEnergy = GetFrameEnergy(data, startOffset + ((windowStartFrame + WindowFrames) * frameSize));
            long inEnergy = GetFrameEnergy(data, startOffset + (windowStartFrame * frameSize));

            currentEnergy = currentEnergy - outEnergy + inEnergy;
        }

        int newTotalFrames = totalFrames - trimFrames;
        return newTotalFrames * frameSize;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static long GetFrameEnergy(byte[] data, int offset)
    {
        short l = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, 2));
        short r = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 2, 2));
        return ((long)l * l) + ((long)r * r);
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

        private BmsAtelierKyokufu.BmsPartTuner.Models.BaseAudioOptimizationData? _decodedData;

        public BmsAtelierKyokufu.BmsPartTuner.Models.BaseAudioOptimizationData DecodedData
        {
            get
            {
                if (_decodedData != null) return _decodedData;
                lock (this)
                {
                    if (_decodedData != null) return _decodedData;
                    _decodedData = DecodeAllData();
                    return _decodedData;
                }
            }
        }

        private BmsAtelierKyokufu.BmsPartTuner.Models.BaseAudioOptimizationData DecodeAllData()
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
                short r = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(data.Slice(i * 4 + 2, 2));

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
                if (fl >= 0) signLsh[0][lshIdx] |= (1UL << bitShift);
                if (Math.Abs(fl) >= silenceThreshold) signLshMask[0][lshIdx] |= (1UL << bitShift);

                // Right channel
                if (fr >= 0) signLsh[1][lshIdx] |= (1UL << bitShift);
                if (Math.Abs(fr) >= silenceThreshold) signLshMask[1][lshIdx] |= (1UL << bitShift);
            }

            return new BmsAtelierKyokufu.BmsPartTuner.Models.BaseAudioOptimizationData(samples, prefixSum, prefixSumSq, signLsh, signLshMask);
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
                        PerformanceDebugLogger.WriteLine($"[AudioSliceManager] Reached length limit ({maxAllowedBytes} bytes) for {Path.GetFileName(path)}. Stopping decode.", LogLevel.Debug);
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

