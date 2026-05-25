using System.Buffers.Binary;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

/// <summary>
/// メモリ上のPCMデータの一部を指し示し、アロケーションフリーでWAVバイナリとして読み出し可能なストリーム。
/// </summary>
public class VirtualWavStream : Stream
{
    private readonly byte[] _header;
    private readonly byte[] _pcmData;
    private readonly int _startByte;
    private readonly int _lengthBytes;
    private long _position;

    public VirtualWavStream(byte[] pcmData, int startByte, int lengthBytes, byte[] headerTemplate)
    {
        _pcmData = pcmData ?? throw new ArgumentNullException(nameof(pcmData));
        _startByte = startByte;
        _lengthBytes = lengthBytes;
        _position = 0;

        // 44バイトのヘッダをインスタンスごとにローカルで生成
        _header = new byte[44];
        Buffer.BlockCopy(headerTemplate, 0, _header, 0, 44);

        // 可変なサイズ情報をリトルエンディアンで直接上書き
        BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(_header, 4, 4), 36 + lengthBytes);
        BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(_header, 40, 4), lengthBytes);
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => 44 + _lengthBytes;

    public override long Position
    {
        get => _position;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "位置を負の値に設定することはできません。");
            _position = value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || count < 0) throw new ArgumentOutOfRangeException($"{(offset < 0 ? nameof(offset) : "")} {(count < 0 ? " | " + nameof(count) : "")}");
        if (buffer.Length - offset < count) throw new ArgumentException("バッファの長さが不足しています。");

        if (_position >= Length) return 0;

        int bytesRead = 0;

        // 1. WAVヘッダ部（0～43バイト）の読み込み
        if (_position < 44)
        {
            int headerAvailable = 44 - (int)_position;
            int toCopy = Math.Min(headerAvailable, count);
            Buffer.BlockCopy(_header, (int)_position, buffer, offset, toCopy);
            _position += toCopy;
            offset += toCopy;
            count -= toCopy;
            bytesRead += toCopy;
        }

        // 2. PCMデータ部（44バイト以降）の読み込み
        if (count > 0 && _position >= 44)
        {
            long pcmOffset = _position - 44;
            int pcmAvailable = _lengthBytes - (int)pcmOffset;
            if (pcmAvailable > 0)
            {
                int toCopy = Math.Min(pcmAvailable, count);
                Buffer.BlockCopy(_pcmData, _startByte + (int)pcmOffset, buffer, offset, toCopy);
                _position += toCopy;
                bytesRead += toCopy;
            }
        }

        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentException("無効なSeekOriginです。", nameof(origin))
        };

        if (newPosition < 0)
        {
            throw new IOException("ファイルポインタを開始位置より前に移動することはできません。");
        }
        _position = newPosition;
        return _position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() { }
}

/// <summary>
/// スライスされた仮想WAVファイルを表現するクラス。
/// メモリコピーを伴わず、元のPCMバッファの該当範囲への参照のみを保持します。
/// </summary>
public class SlicedVirtualFile(byte[] pcmData, int startByte, int lengthBytes, byte[] headerTemplate) : IVirtualFile
{
    private readonly byte[] _pcmData = pcmData ?? throw new ArgumentNullException(nameof(pcmData));
    private readonly int _startByte = startByte;
    private readonly int _lengthBytes = lengthBytes;
    private readonly byte[] _headerTemplate = headerTemplate ?? throw new ArgumentNullException(nameof(headerTemplate));

    public long Length => 44 + _lengthBytes;

    public Stream Open()
    {
        return new VirtualWavStream(_pcmData, _startByte, _lengthBytes, _headerTemplate);
    }
}
