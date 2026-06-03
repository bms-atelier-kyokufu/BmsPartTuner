namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson;

using System;
using System.Buffers.Binary;
using System.IO;

/// <summary>
/// 標準的な 16-bit, 44.1kHz, ステレオ の WAV ファイルを
/// NAudio を介さず直接高速にパースするためのヘルパークラス。
/// </summary>
internal static class FastWavLoader
{
    // WAV Header Constants
    private const int MinWavHeaderSize = 44;
    private const int ChunkIdLength = 4;
    private const int ChunkSizeLength = 4;
    private const int ChunkHeaderLength = ChunkIdLength + ChunkSizeLength; // 8 bytes

    // RIFF/WAVE Offset Constants
    private const int WaveOffset = 8;
    private const int FirstChunkSearchOffset = 12; // Start searching after "RIFFxxxxWAVE"

    // fmt Chunk Field Offsets (relative to fmt chunk start offset)
    private const int FmtAudioFormatOffset = ChunkHeaderLength; // 8
    private const int FmtChannelsOffset = FmtAudioFormatOffset + 2; // 10
    private const int FmtSampleRateOffset = FmtChannelsOffset + 2; // 12
    private const int FmtBitsPerSampleOffset = FmtSampleRateOffset + 10; // 22
    private const int FmtChunkMinSize = FmtBitsPerSampleOffset + 2; // 24

    // Expected Format Constants
    private const short FormatPcm = 1;
    private const short ChannelsStereo = 2;
    private const short BitsPerSample16 = 16;

    public static bool TryLoad(string path, out byte[] rawBytes, out int pcmOffset, out int pcmLength)
    {
        rawBytes = [];
        pcmOffset = 0;
        pcmLength = 0;

        try
        {
            byte[] fileBytes = File.ReadAllBytes(path);
            if (!IsRiffWaveHeader(fileBytes)) return false;

            int fmtOffset = FindChunkOffset(fileBytes, "fmt "u8, FirstChunkSearchOffset);
            if (fmtOffset == -1) return false;

            if (!IsStandardPcmFormat(fileBytes, fmtOffset)) return false;

            int dataOffset = FindChunkOffset(fileBytes, "data"u8, fmtOffset + ChunkHeaderLength);
            if (dataOffset == -1) return false;

            int dataLength = BinaryPrimitives.ReadInt32LittleEndian(fileBytes.AsSpan(dataOffset + ChunkIdLength));
            int actualDataStart = dataOffset + ChunkHeaderLength;
            if (actualDataStart + dataLength > fileBytes.Length)
            {
                dataLength = fileBytes.Length - actualDataStart;
            }

            rawBytes = fileBytes;
            pcmOffset = actualDataStart;
            pcmLength = dataLength;
            return true;
        }
        catch
        {
            // パースエラー時は false を返し、フォールバックに任せる
        }

        return false;
    }

    private static bool IsRiffWaveHeader(ReadOnlySpan<byte> fileBytes)
    {
        return fileBytes.Length >= MinWavHeaderSize &&
               fileBytes[..ChunkIdLength].SequenceEqual("RIFF"u8) &&
               fileBytes.Slice(WaveOffset, ChunkIdLength).SequenceEqual("WAVE"u8);
    }

    private static int FindChunkOffset(ReadOnlySpan<byte> fileBytes, ReadOnlySpan<byte> chunkName, int startOffset)
    {
        if (chunkName.Length != ChunkIdLength) throw new ArgumentException($"Chunk name must be {ChunkIdLength} bytes.", nameof(chunkName));

        int searchLength = fileBytes.Length - startOffset - ChunkSizeLength;
        if (searchLength < ChunkIdLength) return -1;

        int index = fileBytes.Slice(startOffset, searchLength).IndexOf(chunkName);
        return index == -1 ? -1 : startOffset + index;
    }

    private static bool IsStandardPcmFormat(ReadOnlySpan<byte> fileBytes, int fmtOffset)
    {
        if (fmtOffset + FmtChunkMinSize > fileBytes.Length) return false;

        short audioFormat = BinaryPrimitives.ReadInt16LittleEndian(fileBytes[(fmtOffset + FmtAudioFormatOffset)..]);
        short numChannels = BinaryPrimitives.ReadInt16LittleEndian(fileBytes[(fmtOffset + FmtChannelsOffset)..]);
        int sampleRateVal = BinaryPrimitives.ReadInt32LittleEndian(fileBytes[(fmtOffset + FmtSampleRateOffset)..]);
        short bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(fileBytes[(fmtOffset + FmtBitsPerSampleOffset)..]);

        return audioFormat == FormatPcm &&
               numChannels == ChannelsStereo &&
               sampleRateVal == AppConstants.Audio.StandardSampleRate &&
               bitsPerSample == BitsPerSample16;
    }
}

