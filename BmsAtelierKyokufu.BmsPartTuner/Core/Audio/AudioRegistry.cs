namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

/// <summary>
/// 音声キャッシュデータ（PreNormalizedSoundData および PointerSoundData）を管理する共通レジストリ。
/// IDisposable を実装し、キャッシュのクリーンアップ時に内部のデータを確実に Dispose します。
/// </summary>
public sealed class AudioRegistry : IDisposable
{
    /// <summary>
    /// シングルトンインスタンス。
    /// </summary>
    public static AudioRegistry Instance { get; } = new();

    private bool _disposed;
    private readonly ConcurrentDictionary<string, ICachedSoundData> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// キャッシュされた音声データ間の類似性比較結果（ピアソン相関係数など）のキャッシュ。
    /// （キー: ファイル名1, ファイル名2）
    /// </summary>
    public ConcurrentDictionary<(string, string), float> CorrelationCache { get; } = new();

    private AudioRegistry() { }

    /// <summary>
    /// 音声データを登録します。
    /// </summary>
    public void Register(string filePath, ICachedSoundData data)
    {
        _cache[Path.GetFileName(filePath)] = data;
    }

    /// <summary>
    /// キャッシュされた音声データを取得します。
    /// </summary>
    public bool TryGet(string filePath, out ICachedSoundData? data)
    {
        return _cache.TryGetValue(Path.GetFileName(filePath), out data);
    }

    /// <summary>
    /// 特定の型の音声データを取得します。
    /// </summary>
    public bool TryGet<T>(string filePath, out T? data) where T : class, ICachedSoundData
    {
        if (_cache.TryGetValue(Path.GetFileName(filePath), out var cachedData) && cachedData is T typedData)
        {
            data = typedData;
            return true;
        }
        data = null;
        return false;
    }

    /// <summary>
    /// すべてのキャッシュを破棄し、格納されているデータを Dispose します。
    /// </summary>
    public void Clear()
    {
        foreach (var entry in _cache.Values)
        {
            entry.Dispose();
        }
        _cache.Clear();
        CorrelationCache.Clear();
    }

    /// <summary>
    /// PointerSoundData（bmsonのスライスデータ一時参照）のみを破棄します。
    /// </summary>
    public void ClearPointerDataOnly()
    {
        foreach (var pair in _cache)
        {
            if (pair.Value is PointerSoundData)
            {
                if (_cache.TryRemove(pair.Key, out var data))
                {
                    data.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// キャッシュされているアイテム数。
    /// </summary>
    public int Count => _cache.Count;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        Clear();
        _disposed = true;
    }
}

/// <summary>
/// 定義削減セッション中に静的キャッシュレジストリを自動管理するための IDisposable セッション。
/// セッション終了時または例外発生時に、一時的な変換参照データ（PointerSoundData や仮想ファイル）を確実に解放します。
/// </summary>
public sealed class AudioRegistrySession : IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        AudioRegistry.Instance.ClearPointerDataOnly();
        VirtualAudioRegistry.Clear();
        _disposed = true;
    }
}
