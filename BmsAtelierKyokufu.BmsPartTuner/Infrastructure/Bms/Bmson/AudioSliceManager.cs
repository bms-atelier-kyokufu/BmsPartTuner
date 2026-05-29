using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson;

/// <summary>
/// bmsonのノート情報に基づき、元の音声ファイル（ステムなど）を指定時間で切り出し、
/// BMS用の短いWAVスライスを生成するマネージャー。
/// </summary>
[ADRAnchor("OPT-10", nameof(AudioSliceManager))]
public class AudioSliceManager(string bmsonDir, bool throwOnMissingFile = true) : IDisposable
{
    private readonly string _bmsonDir = bmsonDir;

    private static readonly byte[] WavHeaderTemplate = WavHeaderGenerator.CreateWavHeaderTemplate();

    // key: "fileName|offsetSec|durationSec", value: "outputFileName.wav"
    private readonly ConcurrentDictionary<string, string> _requestCache = new();

    // key: "fileName|offsetByte|trimmedLengthBytes", value: "outputFileName.wav"
    private readonly ConcurrentDictionary<string, Lazy<string>> _sliceCache = new();

    // 楽器種別ごとの連番を管理する辞書
    private readonly ConcurrentDictionary<string, int> _instrumentCounters = new();

    private readonly ConcurrentDictionary<string, Lazy<CachedAudioSource?>> _sourceCache = new(StringComparer.OrdinalIgnoreCase);
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

        // 1. まず音源のキャッシュを取得（スレッドセーフにロードされる）
        var source = GetOrLoadAudioSource(sourceFileName);
        if (source == null) return string.Empty;

        // 2. 要求された開始位置と長さを計算
        var (startByte, lengthBytes) = CalculateByteRange(source.PcmLength, offsetSec, durationSec);
        if (lengthBytes <= 0) return string.Empty;

        // L1キャッシュ（要求ベース）の確認: ヒットすればTrim計算も不要
        string requestCacheKey = $"{sourceFileName}|{startByte}|{lengthBytes}";
        if (_requestCache.TryGetValue(requestCacheKey, out string? cachedFileName))
        {
            Interlocked.Increment(ref _cacheHitCount);
            return cachedFileName;
        }

        // 3. 末尾の無音部分をトリミングして、本当に必要な長さに切り詰める
        int trimmedLengthBytes = SilenceTrimmer.TrimSilenceFromEnd(source.RawBytes, source.PcmOffset + startByte, lengthBytes);
        if (trimmedLengthBytes <= 0) return string.Empty;

        if (trimmedLengthBytes < lengthBytes)
        {
            PerformanceDebugLogger<AudioSliceManager>.WriteTrace( $"Trimmed silence: {sourceFileName} (offset={offsetSec:F2}s, duration={durationSec:F2}s) from {lengthBytes / 1024.0:F1}KB to {trimmedLengthBytes / 1024.0:F1}KB");
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
                    PerformanceDebugLogger<AudioSliceManager>.WriteError( $"スライス失敗: {sourceFileName}", ex);
                    return string.Empty;
                }
            });
        });

        if (!isNew)
        {
            Interlocked.Increment(ref _cacheHitCount);
        }

        string finalFileName = lazyVal.Value;

        // 次回以降のためにL1キャッシュにも覚えさせておく
        if (!string.IsNullOrEmpty(finalFileName))
        {
            _requestCache.TryAdd(requestCacheKey, finalFileName);
        }

        return finalFileName;
    }

    /// <summary>
    /// 生成されたスライスの総数を取得します。
    /// </summary>
    public int GetGeneratedSliceCount() => _sliceCache.Values.Count(static v => !string.IsNullOrEmpty(v.Value));

    public void Dispose()
    {
        _sourceCache.Clear();
        _sliceCache.Clear();
        _requestCache.Clear();
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

        int currentCount = _instrumentCounters.AddOrUpdate(prefix, 1, static (_, count) => count + 1);
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

}

