using System.Collections.Concurrent;
using BmsAtelierKyokufu.BmsPartTuner.Models;

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
        _cache[fileName] = data;
    }

    public static bool TryGet(string fileName, out PointerSoundData data)
    {
        return _cache.TryGetValue(fileName, out data!);
    }

    public static void Clear()
    {
        _cache.Clear();
    }
}
