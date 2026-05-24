using System.IO;
using System.Text;
using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;
using NAudio.Wave;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers
{
    public static class BmsTestWavHelper
    {
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

        public static string CreateSilenceWavFile(string filePath, double durationSeconds = 0.1, int channels = 1, bool writeToDisk = true)
        {
            int sampleRate = 44100;
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

        public static string CreateValidWavFile(string filePath, bool isDifferent = false, bool writeToDisk = true)
        {
            double frequency = isDifferent ? 880.0 : 440.0;
            int sampleRate = 44100;
            int sampleCount = 4410; // 0.1 seconds

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
