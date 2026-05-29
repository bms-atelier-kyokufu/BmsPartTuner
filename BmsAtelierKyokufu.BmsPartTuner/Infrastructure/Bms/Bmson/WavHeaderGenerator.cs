using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson;

public static class WavHeaderGenerator
{
    /// <summary>
    /// WAVヘッダの不変フィールドを事前設定した44バイトのテンプレート配列を生成します。
    /// </summary>
    public static byte[] CreateWavHeaderTemplate()
    {
        byte[] template = new byte[44];
        using (var ms = new MemoryStream(template))
        using (var writer = new BinaryWriter(ms))
        {
            writer.Write("RIFF"u8);
            writer.Write(0); // fileSize (36 + dataLengthBytes) のプレースホルダー
            writer.Write("WAVE"u8);

            writer.Write("fmt "u8);
            writer.Write(16); // Subchunk1Size = 16 (PCM)
            writer.Write((short)1); // AudioFormat = 1 (PCM)
            writer.Write((short)2); // NumChannels = 2 (Stereo)
            writer.Write(AppConstants.Audio.StandardSampleRate);
            writer.Write(AppConstants.Audio.StandardSampleRate * 2 * 2); // ByteRate
            writer.Write((short)4); // BlockAlign = 4
            writer.Write((short)16); // BitsPerSample = 16

            writer.Write("data"u8);
            writer.Write(0); // dataLengthBytes のプレースホルダー
        }
        return template;
    }
}
