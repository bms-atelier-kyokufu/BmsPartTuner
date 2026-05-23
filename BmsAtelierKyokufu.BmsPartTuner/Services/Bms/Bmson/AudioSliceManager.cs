using System.Collections.Concurrent;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.Bms.Bmson;

/// <summary>
/// bmsonのノート情報に基づき、元の音声ファイル（ステムなど）を指定時間で切り出し、
/// BMS用の短いWAVスライスを生成するマネージャー。
/// </summary>
public class AudioSliceManager(string bmsonDir)
{
    private readonly string _bmsonDir = bmsonDir;

    // key: "fileName|offsetSec|durationSec", value: "outputFileName.wav"
    private readonly ConcurrentDictionary<string, Lazy<string>> _sliceCache = new();
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
            string sourcePath = Path.Combine(_bmsonDir, sourceFileName);
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"音源ファイルが見つかりません: {sourceFileName} (Path: {sourcePath})");
            }

            // BmsPartTunerの命名規則に合わせたスライス名（スライス元のファイル名先頭大文字_0001.wav 等）
            string nameWithoutExt = Path.GetFileNameWithoutExtension(sourceFileName);
            string prefix = string.IsNullOrEmpty(nameWithoutExt)
                ? "Slice"
                : char.ToUpper(nameWithoutExt[0]) + nameWithoutExt[1..];

            int currentCount = Interlocked.Increment(ref _sliceCounter) - 1;
            string outputFileName = $"{prefix}_{currentCount:D4}.wav";

            try
            {
                WaveStream reader;
                if (sourcePath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                {
                    reader = new NAudio.Vorbis.VorbisWaveReader(sourcePath);
                }
                else
                {
                    reader = new AudioFileReader(sourcePath);
                }

                using (reader)
                {
                    // オフセットがファイル長を超えている場合は無音扱いとして出力しない
                    if (offsetSec >= reader.TotalTime.TotalSeconds)
                    {
                        return string.Empty;
                    }

                    // 長さの補正（ファイル終端を超えないようにする）
                    double actualDuration = durationSec;
                    if (offsetSec + actualDuration > reader.TotalTime.TotalSeconds)
                    {
                        actualDuration = reader.TotalTime.TotalSeconds - offsetSec;
                    }

                    if (actualDuration <= 0)
                    {
                        return string.Empty;
                    }

                    // 指定位置へシーク
                    reader.CurrentTime = TimeSpan.FromSeconds(offsetSec);

                    // ステレオ・44.1kHzに揃えるプロバイダチェーンの構築
                    ISampleProvider sampleProvider = reader.ToSampleProvider();

                    if (sampleProvider.WaveFormat.Channels == 1)
                    {
                        sampleProvider = new MonoToStereoSampleProvider(sampleProvider);
                    }

                    if (sampleProvider.WaveFormat.SampleRate != 44100)
                    {
                        sampleProvider = new WdlResamplingSampleProvider(sampleProvider, 44100);
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
                    BmsAtelierKyokufu.BmsPartTuner.Core.Audio.VirtualAudioRegistry.AddFile(outputFileName, ms.ToArray());

                    return outputFileName;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioSliceManager] スライス失敗: {sourceFileName} ({ex.Message})");
                return string.Empty;
            }
        }));

        return lazyVal.Value;
    }

    /// <summary>
    /// 生成されたスライスの総数を取得します。
    /// </summary>
    public int GetGeneratedSliceCount() => _sliceCache.Values.Count(v => !string.IsNullOrEmpty(v.Value));
}
