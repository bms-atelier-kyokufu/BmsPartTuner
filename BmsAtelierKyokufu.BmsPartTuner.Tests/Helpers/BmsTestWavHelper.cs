using System.IO;
using System.Text;
using NAudio.Wave;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers
{
    /// <summary>
    /// テスト用のWAVファイルや音声データの生成を行うヘルパークラス。
    /// </summary>
    public static class BmsTestWavHelper
    {
        /// <summary>
        /// 最小限のWAVヘッダーのみを持つダミーWAVファイルを作成します。
        /// </summary>
        /// <param name="filePath">作成するファイルの保存先パス。</param>
        /// <param name="writeToDisk">ディスクへ書き出す場合は <c>true</c>、仮想メモリ上に登録する場合は <c>false</c>。</param>
        public static void CreateDummyWavFile(string filePath, bool writeToDisk = true)
        {
            byte[] wavHeader = new byte[44];

            // RIFF
            Encoding.ASCII.GetBytes("RIFF").CopyTo(wavHeader, 0);
            BitConverter.GetBytes(36).CopyTo(wavHeader, 4); // ChunkSize (36 + data size 0)
            Encoding.ASCII.GetBytes("WAVE").CopyTo(wavHeader, 8);

            // fmt
            Encoding.ASCII.GetBytes("fmt ").CopyTo(wavHeader, 12);
            BitConverter.GetBytes(16).CopyTo(wavHeader, 16); // Subchunk1Size
            BitConverter.GetBytes((short)1).CopyTo(wavHeader, 20); // AudioFormat (PCM)
            BitConverter.GetBytes((short)1).CopyTo(wavHeader, 22); // NumChannels
            BitConverter.GetBytes(44100).CopyTo(wavHeader, 24); // SampleRate
            BitConverter.GetBytes(44100 * 2).CopyTo(wavHeader, 28); // ByteRate
            BitConverter.GetBytes((short)2).CopyTo(wavHeader, 32); // BlockAlign
            BitConverter.GetBytes((short)16).CopyTo(wavHeader, 34); // BitsPerSample

            // data
            Encoding.ASCII.GetBytes("data").CopyTo(wavHeader, 36);
            BitConverter.GetBytes(0).CopyTo(wavHeader, 40); // Subchunk2Size

            if (writeToDisk)
            {
                File.WriteAllBytes(filePath, wavHeader);
            }
            else
            {
                VirtualAudioRegistry.AddFile(Path.GetFileName(filePath), wavHeader);
            }
        }

        /// <summary>
        /// 指定された条件の正弦波を含むWAVフォーマットのバイナリデータを生成します。
        /// </summary>
        /// <param name="sampleCount">生成するサンプルの個数。</param>
        /// <param name="frequency">正弦波の周波数。</param>
        /// <param name="amplitude">波形の振幅。</param>
        /// <param name="channels">オーディオのチャンネル数。</param>
        /// <returns>WAVフォーマットに準拠したバイト配列。</returns>
        public static byte[] CreateSineWavBytes(int sampleCount = 1000, double frequency = 440.0, double amplitude = 0.5, int channels = 1)
        {
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = (float)(amplitude * Math.Sin(2 * Math.PI * frequency * i / sampleCount));
            }

            using var ms = new MemoryStream();
            using (var writer = new WaveFileWriter(ms, new WaveFormat(44100, 16, channels)))
            {
                foreach (var sample in samples)
                {
                    writer.WriteSample(sample);
                }
            }
            return ms.ToArray();
        }

        /// <summary>
        /// 正弦波を含むWAVファイルを生成します。
        /// </summary>
        /// <param name="filePath">ファイルの保存先パス。</param>
        /// <param name="sampleCount">生成するサンプルの個数。</param>
        /// <param name="frequency">正弦波の周波数。</param>
        /// <param name="amplitude">波形の振幅。</param>
        /// <param name="channels">オーディオのチャンネル数。</param>
        /// <param name="writeToDisk">ディスクへ書き出す場合は <c>true</c>、仮想メモリ上に登録する場合は <c>false</c>。</param>
        /// <returns>生成された（または登録された）ファイルのパス。</returns>
        public static string CreateSineWavFile(string filePath, int sampleCount = 1000, double frequency = 440.0, double amplitude = 0.5, int channels = 1, bool writeToDisk = true)
        {
            var bytes = CreateSineWavBytes(sampleCount, frequency, amplitude, channels);

            if (writeToDisk)
            {
                File.WriteAllBytes(filePath, bytes);
            }
            else
            {
                VirtualAudioRegistry.AddFile(Path.GetFileName(filePath), bytes);
            }

            return filePath;
        }

        /// <summary>
        /// 無音（サンプル値 0）のWAVファイルを生成します。
        /// </summary>
        /// <param name="filePath">ファイルの保存先パス。</param>
        /// <param name="durationSeconds">再生時間（秒）。</param>
        /// <param name="channels">オーディオのチャンネル数。</param>
        /// <param name="writeToDisk">ディスクへ書き出す場合は <c>true</c>、仮想メモリ上に登録する場合は <c>false</c>。</param>
        /// <returns>生成された（または登録された）ファイルのパス。</returns>
        public static string CreateSilenceWavFile(string filePath, double durationSeconds = 0.1, int channels = 1, bool writeToDisk = true)
        {
            const int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * durationSeconds * channels);

            using (var ms = new MemoryStream())
            {
                using (var writer = new WaveFileWriter(ms, new WaveFormat(sampleRate, 16, channels)))
                {
                    var silence = new byte[totalSamples * 2]; // 16bit = 2 bytes per sample
                    writer.Write(silence, 0, silence.Length);
                }

                var bytes = ms.ToArray();

                if (writeToDisk)
                {
                    File.WriteAllBytes(filePath, bytes);
                }
                else
                {
                    VirtualAudioRegistry.AddFile(Path.GetFileName(filePath), bytes);
                }
            }

            return filePath;
        }

        /// <summary>
        /// テスト用途で有効な音声を持つWAVファイルを生成します。オプションで異なる周波数の波形を生成可能です。
        /// </summary>
        /// <param name="filePath">ファイルの保存先パス。</param>
        /// <param name="isDifferent">異なる周波数（880Hz）を生成する場合は <c>true</c>、通常（440Hz）は <c>false</c>。</param>
        /// <param name="writeToDisk">ディスクへ書き出す場合は <c>true</c>、仮想メモリ上に登録する場合は <c>false</c>。</param>
        /// <returns>生成された（または登録された）ファイルのパス。</returns>
        public static string CreateValidWavFile(string filePath, bool isDifferent = false, bool writeToDisk = true)
        {
            double frequency = isDifferent ? 880.0 : 440.0;
            const int sampleRate = 44100;
            const int sampleCount = 4410; // 0.1 seconds

            using (var ms = new MemoryStream())
            {
                using (var writer = new WaveFileWriter(ms, new WaveFormat(sampleRate, 16, 1)))
                {
                    for (int i = 0; i < sampleCount; i++)
                    {
                        double t = (double)i / sampleRate;
                        float sample = (float)(0.5 * Math.Sin(2 * Math.PI * frequency * t));
                        writer.WriteSample(sample);
                    }
                }

                var bytes = ms.ToArray();

                if (writeToDisk)
                {
                    File.WriteAllBytes(filePath, bytes);
                }
                else
                {
                    VirtualAudioRegistry.AddFile(Path.GetFileName(filePath), bytes);
                }
            }

            return filePath;
        }
    }
}
