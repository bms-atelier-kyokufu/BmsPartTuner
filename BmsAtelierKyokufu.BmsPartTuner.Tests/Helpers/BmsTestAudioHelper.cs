using System.Collections.Concurrent;
using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;
using BmsAtelierKyokufu.BmsPartTuner.Models;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers
{
    /// <summary>
    /// テスト用のオーディオデータおよびキャッシュの生成を支援するヘルパークラス。
    /// </summary>
    public static class BmsTestAudioHelper
    {
        /// <summary>
        /// サンプルデータから、テスト用のノーマライズ済み音源データを生成します。
        /// </summary>
        /// <param name="samples">オーディオサンプルの配列。</param>
        /// <param name="channels">チャンネル数（デフォルトは 1）。</param>
        /// <returns>生成された <see cref="MockCachedSoundData"/>。</returns>
        public static MockCachedSoundData CreatePreNormalizedSoundData(
            float[] samples,
            int channels = 1,
            bool disableCascadeClassifiers = false,
            string filePath = "test.wav",
            int sampleRate = 44100,
            int bitDepth = 16)
        {
            float[][] samplesPerChannel = new float[channels][];
            int samplesPerCh = samples.Length / channels;

            for (int i = 0; i < channels; i++)
            {
                samplesPerChannel[i] = new float[samplesPerCh];
                for (int j = 0; j < samplesPerCh; j++)
                {
                    samplesPerChannel[i][j] = samples[(j * channels) + i];
                }
            }

            return new MockCachedSoundData(samplesPerChannel, sampleRate, bitDepth, filePath)
            {
                DisableCascadeClassifiers = disableCascadeClassifiers
            };
        }

        /// <summary>
        /// 疑似キャッシュを割り当てた <see cref="BmsAudioFile"/> インスタンスを生成します。
        /// </summary>
        /// <param name="num">BMS内でのインデックス番号。</param>
        /// <param name="samples">オーディオサンプルの配列。</param>
        /// <param name="audioCache">キャッシュを格納するスレッドセーフな辞書。</param>
        /// <param name="filenamePattern">ファイル名の命名パターン。</param>
        /// <returns>初期化された <see cref="BmsAudioFile"/>。</returns>
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

        /// <summary>
        /// シンプルなダミー用のキャッシュデータを生成します。
        /// </summary>
        /// <returns>生成された <see cref="MockCachedSoundData"/>。</returns>
        public static MockCachedSoundData CreateDummyCache()
        {
            float[][] samples = [[0.0f, 0.1f, 0.2f]];
            return new MockCachedSoundData(samples, 44100, 16);
        }

        /// <summary>
        /// 指定された周波数の正弦波からなるテスト用キャッシュデータを生成します。
        /// </summary>
        /// <param name="frequency">生成する正弦波の周波数。</param>
        /// <returns>生成された <see cref="MockCachedSoundData"/>。</returns>
        public static MockCachedSoundData CreateDistinctCache(double frequency = 440.0)
        {
            float[][] samples = [new float[100]];
            for (int i = 0; i < 100; i++)
            {
                samples[0][i] = (float)Math.Sin(2.0 * Math.PI * frequency * i / 100.0);
            }
            return new MockCachedSoundData(samples, 44100, 16);
        }
    }
}
