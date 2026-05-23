using System.Collections.Concurrent;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

/// <summary>
/// BMSON変換時に生成された音声スライスなどをメモリ上に保持するための仮想オーディオレジストリ。
/// 物理ディスクI/Oを抑え、ユーザーが保存を明示的に指示するまでオンメモリで管理します。
/// </summary>
public static class VirtualAudioRegistry
{
    // キー: ファイル名 (例: Slice_0001.wav), 値: WAVデータのバイト配列
    private static readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 音声データをレジストリに追加します。
    /// </summary>
    public static void AddFile(string fileName, byte[] data)
    {
        _files[fileName] = data;
    }

    /// <summary>
    /// 指定したファイル名の音声データを取得します。
    /// </summary>
    public static bool TryGetFile(string fileName, out byte[] data)
    {
        if (fileName == null)
        {
            data = [];
            return false;
        }
        return _files.TryGetValue(fileName, out data!);
    }

    /// <summary>
    /// 登録されているすべてのファイルをクリアします。
    /// </summary>
    public static void Clear()
    {
        _files.Clear();
    }
}
