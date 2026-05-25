using System.Collections.Concurrent;
using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;
using BmsAtelierKyokufu.BmsPartTuner.Models;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers
{
    public static class BmsTestAudioHelper
    {
        public static PreNormalizedSoundData CreatePreNormalizedSoundData(float[] samples, int channels = 1)
        {
            float[][] samplesPerChannel = new float[channels][];
            int samplesPerCh = samples.Length / channels;

            for (int i = 0; i < channels; i++)
            {
                samplesPerChannel[i] = new float[samplesPerCh];
                for (int j = 0; j < samplesPerCh; j++)
                {
                    samplesPerChannel[i][j] = samples[j * channels + i];
                }
            }

            return new PreNormalizedSoundData(samplesPerChannel, 44100, 16);
        }

        public static BmsAudioFile CreateAudioFileWithMockCache(
            int num,
            float[] samples,
            ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData> audioCache,
            string filenamePattern = "file_{0}.wav")
        {
            string filename = string.Format(filenamePattern, num);
            var file = new BmsAudioFile
            {
                Num = RadixConvert.IntToZZ(num),
                NumInteger = num,
                Name = filename,
                FileSize = 1024
            };

            var cachedData = CreatePreNormalizedSoundData(samples);
            audioCache[file.Name] = cachedData;

            // Set private backing field _cachedData via reflection
            var field = typeof(BmsAudioFile).GetField("_cachedData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(file, cachedData);

            return file;
        }

        public static PreNormalizedSoundData CreateDummyCache()
        {
            float[][] samples = [[0.0f, 0.1f, 0.2f]];
            return new PreNormalizedSoundData(samples, 44100, 16);
        }

        public static PreNormalizedSoundData CreateDistinctCache(double frequency = 440.0)
        {
            float[][] samples = [new float[100]];
            for (int i = 0; i < 100; i++)
            {
                samples[0][i] = (float)Math.Sin(2.0 * Math.PI * frequency * i / 100.0);
            }
            return new PreNormalizedSoundData(samples, 44100, 16);
        }
    }
}
