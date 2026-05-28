using System;
using System.Collections.Generic;
using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using NAudio.Wave;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio
{
    /// <summary>
    /// 音声ファイルの読み込み、チャンネル分離、正規化、各種特徴量（RMS、有音区間、LSH）の抽出を行うサービス。
    /// PreNormalizedSoundData の生成を担います。
    /// </summary>
    internal static class AudioProcessingService
    {
        public static PreNormalizedSoundData LoadAndProcess(string path, NormalizationMode normalizationMode)
        {
            var (memoryStreamToDispose, stream, sampleProvider, fileSize) = OpenAudioFile(path);

            using (memoryStreamToDispose)
            using (stream)
            {
                int sampleRate = stream.WaveFormat.SampleRate;
                int channels = stream.WaveFormat.Channels;
                int bitsPerSample = stream.WaveFormat.BitsPerSample;

                long totalSamples;
                if (stream is AudioFileReader)
                {
                    totalSamples = stream.Length / sizeof(float);
                }
                else
                {
                    totalSamples = stream.Length / (stream.WaveFormat.BitsPerSample / 8);
                }

                if (totalSamples == 0)
                {
                    throw new InvalidOperationException($"File has zero samples: {path}");
                }

                float[] samplesArray = new float[totalSamples];
                int totalRead = 0;
                int bufferSize = Math.Min(sampleRate * channels, (int)totalSamples);

                while (totalRead < totalSamples)
                {
                    int toRead = (int)Math.Min(bufferSize, totalSamples - totalRead);
                    int read = sampleProvider.Read(samplesArray, totalRead, toRead);

                    if (read == 0)
                    {
                        PerformanceDebugLogger.WriteDebug(nameof(AudioProcessingService), $"[CachedSoundData] WARNING: Read returned 0 at {totalRead}/{totalSamples} for {Path.GetFileName(path)}");
                        break;
                    }

                    totalRead += read;
                }

                if (totalRead == 0)
                {
                    throw new InvalidOperationException($"Failed to read any samples from file: {path}");
                }

                if (totalRead < totalSamples)
                {
                    Array.Resize(ref samplesArray, totalRead);
                }

                int samplesPerChannelLen = samplesArray.Length / channels;
                float[][] samplesPerChannel = DeinterleaveChannels(samplesArray, channels, samplesPerChannelLen);

                if (normalizationMode != NormalizationMode.None)
                {
                    ApplyNormalization(samplesPerChannel, channels, normalizationMode);
                }

                var normalizedRegions = ExtractActiveRegions(samplesPerChannel, channels);
                int startSilenceSamples = DetectStartSilence(samplesPerChannel, samplesPerChannelLen, channels);
                float totalRms = CalculateTotalRms(samplesPerChannel, samplesPerChannelLen, channels);
                var (signLsh, signLshMask) = GenerateLsh(samplesPerChannel, samplesPerChannelLen, channels);
                var fftSpectrum = GenerateFftSpectrum(samplesPerChannel, normalizedRegions, channels);
                float[]? spectralFeatures = GenerateSpectralFeatures(fftSpectrum);
                ulong[]? simHash256 = GenerateSimHash256(fftSpectrum);

                var p = new PreNormalizedSoundDataParameters(
                    path,
                    sampleRate,
                    channels,
                    bitsPerSample,
                    samplesPerChannelLen * channels,
                    fileSize,
                    normalizedRegions,
                    totalRms,
                    startSilenceSamples,
                    signLsh,
                    signLshMask,
                    fftSpectrum,
                    spectralFeatures,
                    simHash256
                );
                return new PreNormalizedSoundData(p);
            }
        }

        /// <summary>
        /// 音声ファイルを開き、ストリームとサンプルプロバイダーを取得します。
        /// 仮想ファイルレジストリ（メモリキャッシュ）が存在する場合はそちらを優先して利用します。
        /// </summary>
        private static (Stream? memoryStreamToDispose, WaveStream stream, ISampleProvider sampleProvider, long fileSize) OpenAudioFile(string path)
        {
            var fileName = Path.GetFileName(path);
            if (VirtualAudioRegistry.TryGetStream(fileName, out var vStream))
            {
                VirtualAudioRegistry.TryGetFileSize(fileName, out var size);
                var waveReader = new WaveFileReader(vStream);
                return (vStream, waveReader, waveReader.ToSampleProvider(), size);
            }
            else
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) throw new FileNotFoundException($"File not found: {path}");
                var audioReader = new AudioFileReader(path);
                return (null, audioReader, audioReader, fi.Length);
            }
        }

        /// <summary>
        /// インターリーブされたオーディオデータ（LRLRLR...）を、チャンネルごとの独立した配列（LLLL..., RRRR...）に分離します。
        /// キャッシュ効率とSIMD処理の前提となる連続メモリ配置を確保するための重要な前処理です。
        /// </summary>
        private static float[][] DeinterleaveChannels(float[] interleavedData, int channels, int samplesPerChannel)
        {
            var result = new float[channels][];
            for (int ch = 0; ch < channels; ch++)
            {
                result[ch] = new float[samplesPerChannel];
                int srcIdx = ch;
                for (int i = 0; i < samplesPerChannel; i++)
                {
                    result[ch][i] = interleavedData[srcIdx];
                    srcIdx += channels;
                }
            }
            return result;
        }

        /// <summary>
        /// 指定された正規化モード（Peak または RMS）に従って、波形全体を正規化します。
        /// </summary>
        private static void ApplyNormalization(float[][] samplesPerChannel, int channels, NormalizationMode mode)
        {
            switch (mode)
            {
                case NormalizationMode.PeakNormalize:
                    NormalizePeak(samplesPerChannel, channels);
                    break;
                case NormalizationMode.RmsNormalize:
                    NormalizeRms(samplesPerChannel, channels);
                    break;
            }
        }

        /// <summary>
        /// 波形の最大振幅が 1.0 になるようにスケーリングする Peak 正規化を行います。
        /// クリッピングを防ぎつつ、全体の音量を最大化します。
        /// </summary>
        private static void NormalizePeak(float[][] samplesPerChannel, int channels)
        {
            float maxAbsValue = 0.0f;
            for (int ch = 0; ch < channels; ch++)
            {
                foreach (float sample in samplesPerChannel[ch])
                {
                    float absValue = Math.Abs(sample);
                    if (absValue > maxAbsValue)
                        maxAbsValue = absValue;
                }
            }

            if (maxAbsValue < 1e-10f) return;

            for (int ch = 0; ch < channels; ch++)
            {
                for (int i = 0; i < samplesPerChannel[ch].Length; i++)
                {
                    samplesPerChannel[ch][i] /= maxAbsValue;
                }
            }
        }

        /// <summary>
        /// 波形の平均的なエネルギー（RMS）が目標値になるようにスケーリングする RMS 正規化を行います。
        /// 人間の聴覚上の音量感を揃えるのに適しています。
        /// </summary>
        private static void NormalizeRms(float[][] samplesPerChannel, int channels, float targetRms = 0.5f)
        {
            float currentRms = CalculateTotalRms(samplesPerChannel, samplesPerChannel[0].Length, channels);
            if (currentRms < 1e-10f) return;

            float scaleFactor = targetRms / currentRms;
            for (int ch = 0; ch < channels; ch++)
            {
                for (int i = 0; i < samplesPerChannel[ch].Length; i++)
                {
                    samplesPerChannel[ch][i] *= scaleFactor;
                }
            }
        }

        /// <summary>
        /// 各チャンネルから無音区間を除外した「有音区間（Active Regions）」のリストを抽出します。
        /// 計算量を下げるため、O(1) のスライディングウィンドウ法を用いてエネルギー（2乗和）を評価します。
        /// </summary>
        private static List<ActiveRegion>[] ExtractActiveRegions(float[][] samplesPerChannel, int channels)
        {
            var regionsPerChannel = new List<ActiveRegion>[channels];
            const double dbThreshold = -90.0;
            const int windowFrames = 256;
            double eThreshold = windowFrames * Math.Pow(10, dbThreshold / 10.0);
            const int maxSilenceFrames = AppConstants.Audio.StandardSampleRate / 4;

            for (int ch = 0; ch < channels; ch++)
            {
                regionsPerChannel[ch] = ExtractChannelActiveRegions(samplesPerChannel[ch], windowFrames, eThreshold, maxSilenceFrames);
            }

            return regionsPerChannel;
        }

        /// <summary>
        /// 単一チャンネルの波形から、O(1) スライディングウィンドウを用いて有音区間を抽出します。
        /// 浮動小数点の加減算のみで次ウィンドウのエネルギーを算出するため、全フレームを舐めても高速に動作します。
        /// </summary>
        private static List<ActiveRegion> ExtractChannelActiveRegions(float[] samples, int windowFrames, double eThreshold, int maxSilenceFrames)
        {
            var regions = new List<ActiveRegion>();
            int totalFrames = samples.Length;

            if (totalFrames == 0) return regions;

            double sumSq = 0;
            int currentWindowFrames = Math.Min(windowFrames, totalFrames);
            for (int i = 0; i < currentWindowFrames; i++)
            {
                sumSq += samples[i] * (double)samples[i];
            }

            int silenceFramesCount = 0;
            bool inActiveRegion = false;
            int currentRegionStart = -1;
            int lastActiveFrame = -1;

            if (sumSq > eThreshold)
            {
                inActiveRegion = true;
                currentRegionStart = 0;
                lastActiveFrame = 0;
            }

            for (int i = 1; i <= totalFrames - windowFrames; i++)
            {
                double prevSample = samples[i - 1];
                double nextSample = samples[i + windowFrames - 1];
                sumSq += (nextSample * nextSample) - (prevSample * prevSample);

                if (sumSq < 0) sumSq = 0;

                if (sumSq > eThreshold)
                {
                    if (!inActiveRegion)
                    {
                        inActiveRegion = true;
                        currentRegionStart = i;
                    }
                    lastActiveFrame = i;
                    silenceFramesCount = 0;
                }
                else if (inActiveRegion)
                {
                    silenceFramesCount++;
                    if (silenceFramesCount > maxSilenceFrames)
                    {
                        int regionEnd = lastActiveFrame + windowFrames;
                        int regionLength = regionEnd - currentRegionStart;
                        AddNormalizedRegion(regions, samples, currentRegionStart, regionLength);
                        inActiveRegion = false;
                    }
                }
            }

            if (inActiveRegion)
            {
                int regionEnd = Math.Min(lastActiveFrame + windowFrames, totalFrames);
                int regionLength = regionEnd - currentRegionStart;
                AddNormalizedRegion(regions, samples, currentRegionStart, regionLength);
            }

            return regions;
        }

        /// <summary>
        /// 抽出された有音区間の波形に対し、平均0・分散1となるような標準化（Z-score正規化）を適用して追加します。
        /// これにより、音量や直流オフセットの違いを無視した純粋な波形形状の比較（ピアソン相関）が可能になります。
        /// </summary>
        private static void AddNormalizedRegion(List<ActiveRegion> regions, float[] originalSamples, int offset, int length)
        {
            if (length <= 0) return;

            float[] normData = new float[length];
            double sum = 0;
            for (int i = 0; i < length; i++)
            {
                sum += originalSamples[offset + i];
            }
            double mean = sum / length;

            double varianceSum = 0;
            for (int i = 0; i < length; i++)
            {
                double diff = originalSamples[offset + i] - mean;
                varianceSum += diff * diff;
            }

            double stdDev = Math.Sqrt(varianceSum);
            if (stdDev < 1e-10)
            {
                for (int i = 0; i < length; i++) normData[i] = 0;
            }
            else
            {
                for (int i = 0; i < length; i++)
                {
                    normData[i] = (float)((originalSamples[offset + i] - mean) / stdDev);
                }
            }

            regions.Add(new ActiveRegion(offset, length, normData));
        }

        /// <summary>
        /// チャンネルを跨いだ波形全体のRMS（二乗平均平方根）を計算します。
        /// ファイル全体のエネルギー量を示す指標として使用されます。
        /// </summary>
        private static float CalculateTotalRms(float[][] samplesPerChannel, int length, int channels)
        {
            double sumSq = 0;
            long totalCount = (long)length * channels;

            if (totalCount == 0) return 0f;

            for (int ch = 0; ch < channels; ch++)
            {
                for (int i = 0; i < length; i++)
                {
                    sumSq += samplesPerChannel[ch][i] * (double)samplesPerChannel[ch][i];
                }
            }

            return (float)Math.Sqrt(sumSq / totalCount);
        }

        /// <summary>
        /// 曲の先頭から、最初に音が鳴り始めるまでの無音サンプル数を検出します。
        /// 閾値は極めて小さな値（0.001f）に設定されています。
        /// </summary>
        private static int DetectStartSilence(float[][] samplesPerChannel, int length, int channels)
        {
            const float silenceThreshold = 0.001f;
            int silenceSamples = 0;
            for (int i = 0; i < length; i++)
            {
                bool isSilent = true;
                for (int ch = 0; ch < channels; ch++)
                {
                    if (Math.Abs(samplesPerChannel[ch][i]) > silenceThreshold)
                    {
                        isSilent = false;
                        break;
                    }
                }
                if (!isSilent) break;
                silenceSamples++;
            }
            return silenceSamples;
        }

        /// <summary>
        /// 波形の微小な時間変化を表現する LSH (Locality-Sensitive Hashing) を生成します。
        /// 隣接する周波数ビンの大小関係をビット化することで、音量変動に強いロバストなシグネチャを作ります。
        /// </summary>
        private static (ulong[][] signLsh, ulong[][] signLshMask) GenerateLsh(float[][] samplesPerChannel, int lengthSamples, int channels)
        {
            int extractLen = Math.Min(lengthSamples, 2048);
            const int fftLen = 4096;
            const int lshLength = 2048 / 64;


            var signLsh = new ulong[channels][];
            var signLshMask = new ulong[channels][];

            for (int ch = 0; ch < channels; ch++)
            {
                signLsh[ch] = new ulong[lshLength];
                signLshMask[ch] = new ulong[lshLength];
            }

            if (extractLen <= 0) return (signLsh, signLshMask);

            double[] hannWindow = MathNet.Numerics.Window.Hann(extractLen);

            for (int ch = 0; ch < channels; ch++)
            {
                var complexData = new Complex32[fftLen];
                var span = new ReadOnlySpan<float>(samplesPerChannel[ch], 0, extractLen);

                for (int i = 0; i < extractLen; i++)
                {
                    complexData[i] = new Complex32((float)(span[i] * hannWindow[i]), 0);
                }

                Fourier.Forward(complexData, FourierOptions.Default);

                float[] magnitudes = new float[2048];
                for (int i = 0; i < 2048; i++)
                {
                    magnitudes[i] = complexData[i].Magnitude;
                }

                for (int i = 0; i < 2048 - 1; i++)
                {
                    int lshIdx = i / 64;
                    int bitShift = i % 64;

                    if (magnitudes[i] >= magnitudes[i + 1])
                    {
                        signLsh[ch][lshIdx] |= 1UL << bitShift;
                    }
                    if (magnitudes[i] > 1e-4f)
                    {
                        signLshMask[ch][lshIdx] |= 1UL << bitShift;
                    }
                }
            }


            return (signLsh, signLshMask);
        }

        /// <summary>
        /// 各チャンネルの有音区間の先頭部分（アタック）を抽出し、ハニング窓をかけてFFT（高速フーリエ変換）を実行します。
        /// 音色が最も特徴的に表れる先頭の周波数スペクトルを、以降の特徴量抽出のベースとします。
        /// </summary>
        private static Complex32[][]? GenerateFftSpectrum(float[][] samplesPerChannel, List<ActiveRegion>[] regionsPerChannel, int channels)
        {
            // FFT用の配列長（Radix-2要件）
            const int fftLen = 4096;
            // 抽出する波形の長さ
            const int extractLen = 2048;

            var fftSpectrum = new Complex32[channels][];
            double[] hannWindow = MathNet.Numerics.Window.Hann(extractLen);

            for (int ch = 0; ch < channels; ch++)
            {
                var complexData = new Complex32[fftLen];
                var regions = regionsPerChannel[ch];

                // 有音区間がない場合はゼロ配列のまま

                if (regions?.Count > 0)
                {
                    int startOffset = regions[0].Offset;
                    var channelSamples = samplesPerChannel[ch];
                    int availableLength = channelSamples.Length - startOffset;
                    int copyLength = Math.Min(extractLen, availableLength);

                    if (copyLength > 0)
                    {
                        var span = new ReadOnlySpan<float>(channelSamples, startOffset, copyLength);
                        for (int i = 0; i < copyLength; i++)
                        {
                            complexData[i] = new Complex32((float)(span[i] * hannWindow[i]), 0);
                        }
                    }
                }

                Fourier.Forward(complexData, FourierOptions.Default);
                fftSpectrum[ch] = complexData;
            }

            return fftSpectrum;
        }

        /// <summary>
        /// FFTスペクトルの振幅（低〜中域）を用いたシフト不変なLSH（SimHash）を生成します (256bit)。
        /// </summary>
        private static ulong[]? GenerateSimHash256(Complex32[][]? fftSpectrum)
        {
            if (fftSpectrum == null || fftSpectrum.Length == 0 || fftSpectrum[0] == null) return null;

            var spectrum = fftSpectrum[0];
            ulong[] hash = new ulong[4];

            // 擬似乱数で256個のランダムベクトルを生成し、内積を取る
            // シードを固定することで、起動ごとに一意な射影空間を保証する
            var random = new Random(42);
            // 人間の聴覚や特徴が集中しやすい低〜中域（256ビン）を対象とする
            const int features = 256;

            for (int i = 0; i < 4; i++)
            {
                ulong currentHash = 0;
                for (int bit = 0; bit < 64; bit++)
                {
                    double dotProduct = 0;
                    for (int f = 0; f < features; f++)
                    {
                        double val = spectrum[f].Magnitude;
                        double weight = (random.NextDouble() * 2.0) - 1.0;
                        dotProduct += val * weight;
                    }
                    if (dotProduct > 0)
                    {
                        currentHash |= (1UL << bit);
                    }
                }
                hash[i] = currentHash;
            }
            return hash;
        }
        /// <summary>
        /// FFTスペクトルの低周波ビン（0〜15）から、次元圧縮分類用の16次元特徴量ベクトルを抽出します。
        /// エネルギーをL2正規化することで、O(1)のユークリッド距離比較による超高速な事前足切り（カスケード分類）を実現します。
        /// </summary>
        private static float[]? GenerateSpectralFeatures(Complex32[][]? fftSpectrum)
        {
            if (fftSpectrum == null || fftSpectrum.Length == 0 || fftSpectrum[0] == null) return null;
            var spec = fftSpectrum[0];
            if (spec.Length < 17) return null;

            var vec = new float[16];
            double sumSq = 0;
            for (int i = 1; i <= 16; i++) // bins 1 to 16 (exclude DC offset at bin 0)
            {
                float mag = spec[i].Magnitude;
                vec[i - 1] = mag;
                sumSq += mag * mag;
            }
            if (sumSq > 0)
            {
                float norm = (float)Math.Sqrt(sumSq);
                for (int i = 0; i < 16; i++)
                {
                    vec[i] /= norm;
                }
            }
            return vec;
        }
    }
}
