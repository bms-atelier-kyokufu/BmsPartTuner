using System.Collections.Concurrent;
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

    // key: "fileName|offsetSec|durationSec", value: "outputFileName.wav"
    private readonly ConcurrentDictionary<string, Lazy<string>> _sliceCache = new();
    private readonly ConcurrentDictionary<string, CachedAudioSource?> _sourceCache = new(StringComparer.OrdinalIgnoreCase);
    private int _sliceCounter = 1;

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

        var lazyVal = _sliceCache.GetOrAdd(cacheKey, key => new Lazy<string>(() =>
        {
            var source = _sourceCache.GetOrAdd(sourceFileName, name =>
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

            if (source == null) return string.Empty;

            // BmsPartTunerの命名規則に合わせたスライス名（スライス元のファイル名先頭大文字_0001.wav 等）
            string nameWithoutExt = Path.GetFileNameWithoutExtension(sourceFileName);
            string prefix = string.IsNullOrEmpty(nameWithoutExt)
                ? "Slice"
                : char.ToUpper(nameWithoutExt[0]) + nameWithoutExt[1..];

            int currentCount = Interlocked.Increment(ref _sliceCounter) - 1;
            string outputFileName = $"{prefix}_{currentCount:D4}.wav";

            try
            {
                double totalSeconds = (double)source.Samples.Length / source.WaveFormat.SampleRate / source.WaveFormat.Channels;

                // オフセットがファイル長を超えている場合は無音扱いとして出力しない
                if (offsetSec >= totalSeconds)
                {
                    return string.Empty;
                }

                // 長さの補正（ファイル終端を超えないようにする）
                double actualDuration = durationSec;
                if (offsetSec + actualDuration > totalSeconds)
                {
                    actualDuration = totalSeconds - offsetSec;
                }

                if (actualDuration <= 0)
                {
                    return string.Empty;
                }

                // メモリ上のキャッシュデコードデータからカスタムサンプルプロバイダを構築
                var arrayProvider = new ArraySampleProvider(source.Samples, source.WaveFormat);
                arrayProvider.Seek(offsetSec);

                ISampleProvider sampleProvider = arrayProvider;

                if (sampleProvider.WaveFormat.Channels == 1)
                {
                    sampleProvider = new MonoToStereoSampleProvider(sampleProvider);
                }

                if (sampleProvider.WaveFormat.SampleRate != AppConstants.Audio.StandardSampleRate)
                {
                    sampleProvider = new WdlResamplingSampleProvider(sampleProvider, AppConstants.Audio.StandardSampleRate);
                }

                // Durationでカットする
                var cutProvider = new OffsetSampleProvider(sampleProvider)
                {
                    Take = TimeSpan.FromSeconds(actualDuration)
                };

                // 16bit PCMとしてメモリに書き出し
                var provider16 = new SampleToWaveProvider16(cutProvider);
                using var ms = new MemoryStream();
                WaveFileWriter.WriteWavFileToStream(ms, provider16);
                Core.Audio.VirtualAudioRegistry.AddFile(outputFileName, ms.ToArray());

                return outputFileName;
            }
            catch (Exception ex)
            {
                PerfDebugLogger.WriteLine($"[AudioSliceManager] スライス失敗: {sourceFileName} ({ex.Message})");
                return string.Empty;
            }
        }));

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
    /// 音声ソースファイルをメモリに一括ロードして保持するキャッシュクラス。
    /// </summary>
    private class CachedAudioSource
    {
        public float[] Samples { get; }
        public WaveFormat WaveFormat { get; }

        public CachedAudioSource(string path)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            WaveStream reader;
            if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                reader = new NAudio.Vorbis.VorbisWaveReader(path);
            }
            else
            {
                reader = new AudioFileReader(path);
            }

            using (reader)
            {
                WaveFormat = reader.WaveFormat;
                var sampleProvider = reader.ToSampleProvider();
                long totalSamples;
                if (reader is AudioFileReader)
                {
                    totalSamples = reader.Length / sizeof(float);
                }
                else
                {
                    totalSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);
                }

                var samplesList = new float[totalSamples];
                int read = sampleProvider.Read(samplesList, 0, (int)totalSamples);
                if (read < totalSamples)
                {
                    Array.Resize(ref samplesList, read);
                }
                Samples = samplesList;
            }
            PerfDebugLogger.WriteLine($"    [CachedAudioSource] Loaded {Path.GetFileName(path)}: {sw.ElapsedMilliseconds} ms");
        }
    }

    /// <summary>
    /// メモリ上の float 配列からシーク・読み込みを行うカスタムサンプルプロバイダ。
    /// </summary>
    private class ArraySampleProvider(float[] samples, WaveFormat waveFormat) : ISampleProvider
    {
        private int _position;

        public WaveFormat WaveFormat { get; } = waveFormat;

        public void Seek(double seconds)
        {
            long sampleOffset = (long)(seconds * WaveFormat.SampleRate * WaveFormat.Channels);
            // チャンネル境界にアライメント
            sampleOffset -= sampleOffset % WaveFormat.Channels;
            _position = (int)Math.Max(0, Math.Min(samples.Length, sampleOffset));
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int available = samples.Length - _position;
            int toCopy = Math.Min(available, count);
            if (toCopy <= 0) return 0;

            Array.Copy(samples, _position, buffer, offset, toCopy);
            _position += toCopy;
            return toCopy;
        }
    }
}
