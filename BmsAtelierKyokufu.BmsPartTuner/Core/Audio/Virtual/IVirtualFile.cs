namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio.Virtual;

/// <summary>
/// メモリ上に実体を持つ仮想ファイルを表現するインターフェース。
/// </summary>
public interface IVirtualFile
{
    /// <summary>
    /// ファイルの総バイト数。
    /// </summary>
    long Length { get; }

    /// <summary>
    /// ファイルデータを読み取るためのストリームを開きます。呼び出し元はストリームを破棄する責任があります。
    /// </summary>
    Stream Open();
}

/// <summary>
/// バイト配列をメモリ上で保持する単純な仮想ファイル実装。
/// </summary>
public class MemoryVirtualFile(byte[] data) : IVirtualFile
{
    public byte[] Data { get; } = data ?? throw new ArgumentNullException(nameof(data));

    public long Length => Data.Length;

    public Stream Open()
    {
        return new MemoryStream(Data);
    }
}
