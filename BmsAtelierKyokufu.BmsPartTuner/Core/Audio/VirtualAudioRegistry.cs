namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

/// <summary>
/// BMSON変換時に生成された音声スライスなどをメモリ上に保持するための仮想オーディオレジストリ。
/// 物理ディスクI/Oを抑え、ユーザーが保存を明示的に指示するまでオンメモリで管理します。
/// </summary>
public static class VirtualAudioRegistry
{
    // キー: ファイル名 (例: Slice_0001.wav), 値: 仮想ファイルオブジェクト
    private static readonly ConcurrentDictionary<string, IVirtualFile> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 音声データをレジストリに追加します。
    /// </summary>
    public static void AddFile(string fileName, byte[] data)
    {
        _files[fileName] = new MemoryVirtualFile(data);
    }

    /// <summary>
    /// 仮想音声ファイルをレジストリに追加します。
    /// </summary>
    public static void AddFile(string fileName, IVirtualFile virtualFile)
    {
        _files[fileName] = virtualFile;
    }

    /// <summary>
    /// 指定したファイル名の音声データを取得します（後方互換性用）。
    /// 仮想ファイルの場合は一時的にバイト配列をアロケートしてコピーします。
    /// </summary>
    public static bool TryGetFile(string fileName, out byte[] data)
    {
        if (fileName != null && _files.TryGetValue(fileName, out var vf))
        {
            if (vf is MemoryVirtualFile mvf)
            {
                data = mvf.Data;
                return true;
            }

            using var stream = vf.Open();
            data = new byte[vf.Length];
            int read = 0;
            while (read < data.Length)
            {
                int r = stream.Read(data, read, data.Length - read);
                if (r <= 0) break;
                read += r;
            }
            return true;
        }
        data = [];
        return false;
    }

    /// <summary>
    /// 指定したファイル名の音声データをストリームとして取得します。
    /// </summary>
    public static bool TryGetStream(string fileName, out Stream stream)
    {
        if (fileName != null && _files.TryGetValue(fileName, out var vf))
        {
            stream = vf.Open();
            return true;
        }
        stream = Stream.Null;
        return false;
    }

    /// <summary>
    /// 指定したファイル名の音声データのサイズを取得します。
    /// </summary>
    public static bool TryGetFileSize(string fileName, out long size)
    {
        if (fileName != null && _files.TryGetValue(fileName, out var vf))
        {
            size = vf.Length;
            return true;
        }
        size = 0;
        return false;
    }

    /// <summary>
    /// 登録されているすべてのファイルをクリアします。
    /// </summary>
    public static void Clear()
    {
        _files.Clear();
    }
}
