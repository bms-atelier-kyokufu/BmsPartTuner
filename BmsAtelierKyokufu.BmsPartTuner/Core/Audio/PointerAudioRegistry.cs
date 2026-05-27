using System.Collections.Concurrent;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

/// <summary>
/// PointerSoundData をファイル名からルックアップするための中央レジストリ。
/// AudioSliceManager がポインタデータを登録し、AudioCacheManager がここから取得します。
/// </summary>
public static class PointerAudioRegistry
{
    private static readonly ConcurrentDictionary<string, PointerSoundData> _cache = new(StringComparer.OrdinalIgnoreCase);

    public static void Register(string fileName, PointerSoundData data)
    {
        _cache[Path.GetFileName(fileName)] = data;
    }

    public static bool TryGet(string fileName, out PointerSoundData data)
    {
        return _cache.TryGetValue(Path.GetFileName(fileName), out data!);
    }

    public static void Clear()
    {
        _cache.Clear();
    }
}

/// <summary>
/// 定義削減セッション中に静的キャッシュレジストリを自動管理するための IDisposable セッション。
/// 例外発生時にも確実に Clear が呼び出されるようにします。
/// </summary>
public sealed class AudioRegistrySession : IDisposable
{
    public void Dispose()
    {
        PointerAudioRegistry.Clear();
        VirtualAudioRegistry.Clear();
    }
}
