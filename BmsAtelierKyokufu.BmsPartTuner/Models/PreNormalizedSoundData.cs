using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using NAudio.Wave;

namespace BmsAtelierKyokufu.BmsPartTuner.Models
{
    /// <summary>
    /// 有音区間のメタデータと波形データを保持する構造体
    /// </summary>
    public readonly struct ActiveRegion(int offset, int length, float[] data)
    {
        public readonly int Offset = offset;
        public readonly int Length = length;
        public readonly float[] Data = data;
    }

    /// <summary>
    /// <para>波形正規化モード</para>
    /// <para>
    /// 【概要】
    /// ロード時に波形を正規化し、音量差に強い比較を実現
    /// </para>
    /// </summary>
    public enum NormalizationMode
    {
        /// <summary>無正規化（現在の動作）</summary>
        None,

        /// <summary>ピークノーマライズ（最大値を1.0に）</summary>
        PeakNormalize,

        /// <summary>RMSノーマライズ（エネルギーを統一）</summary>
        RmsNormalize
    }

    /// <summary>
    /// <para>オンメモリでキャッシュされた音声データ（SIMD最適化版）</para>
    /// <para>
    /// 【目的】
    /// - ディスクI/Oを最小化し、高速比較を実現
    /// - 音声データを事前にデインターリーブ（チャンネル分離）
    /// - SIMD演算に最適なデータ構造を提供
    /// </para>
    /// <para>
    /// 【メモリ最適化戦略】
    /// 1. ロード時にインターリーブデータを取得
    /// 2. チャンネルごとにデインターリーブ
    /// 3. 元のインターリーブデータを破棄（メモリ削減）
    /// 4. RMSを事前計算（高速フィルタ用）
    /// 5. Phase 2: 正規化波形を事前計算（ドット積への帰着）
    /// </para>
    /// <para>
    /// 【効果】
    /// - メモリ使用量: 30〜40%削減
    /// - 比較時のデインターリーブ処理: 完全削除
    /// - GC負荷: 大幅軽減
    /// - Phase 2: 比較時の演算を5倍削減
    /// </para>
    /// <para>
    /// 【メモリリーク対策】
    /// - IDisposableを実装し、明示的なメモリ解放をサポート
    /// - 処理完了後にDisposeを呼び出すことで、大量のメモリを即座に解放
    /// </para>
    /// </summary>
    public class PreNormalizedSoundData : ICachedSoundData, IDisposable
    {
        #region プロパティ

        /// <summary>
        /// 音声ファイルのフルパス
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// サンプルレート（例: 44100 Hz）
        /// </summary>
        public int SampleRate { get; }

        /// <summary>
        /// チャンネル数（1: モノラル、2: ステレオ）
        /// </summary>
        public int Channels { get; }

        /// <summary>
        /// ビット深度（例: 16, 24, 32）
        /// </summary>
        public int BitsPerSample { get; }

        /// <summary>
        /// <para>インターリーブされた元データ（デインターリーブ後はnull）</para>
        /// <para>
        /// 【メモリ最適化】
        /// デインターリーブ完了後にnullを設定してメモリを解放
        /// これにより2196ファイル分のメモリ使用量を30〜40%削減
        /// </para>
        /// </summary>
        public float[]? Samples { get; private set; }

        /// <summary>
        /// <para>チャンネルごとに分離されたデータ（高速比較用）</para>
        /// <para>
        /// 【データ構造】
        /// [チャンネル番号][サンプル番号]
        /// </para>
        /// <para>
        /// 例: ステレオ音声（44.1kHz, 1秒）
        /// SamplesPerChannel[0] = 左チャンネル 44,100サンプル
        /// SamplesPerChannel[1] = 右チャンネル 44,100サンプル
        /// </para>
        /// <para>
        /// 【利点】
        /// - 比較時のデインターリーブ不要
        /// - 連続メモリアクセス（キャッシュ効率向上）
        /// - SIMD演算に最適
        /// </para>
        /// </summary>
        public float[][] SamplesPerChannel { get; private set; }

        /// <summary>
        /// <para>Phase 2: 正規化された波形の有音区間リスト</para>
        /// <para>
        /// 【メモリ最適化】
        /// 長大な無音区間をスキップし、有音区間のみのデータとオフセットを保持します。
        /// これによりメモリを劇的に削減します。
        /// </para>
        /// </summary>
        public List<ActiveRegion>[]? NormalizedRegions { get; private set; }

        /// <summary>
        /// 全サンプルの総数（全チャンネル合計）
        /// </summary>
        public int TotalSamples { get; }

        /// <summary>
        /// <para>RMS（Root Mean Square: 二乗平均平方根）</para>
        /// <para>
        /// 【計算式】
        /// RMS = sqrt(Σ(sample²) / N)
        /// </para>
        /// <para>
        /// 【意味】
        /// 音声の全体的な音圧レベルを表す
        /// </para>
        /// <para>
        /// 【用途】
        /// - 高速フィルタ（Phase 3）
        /// - Sort & Sweep のソートキー
        /// - 20%以上の差があれば即座に不一致判定
        /// </para>
        /// </summary>
        public float TotalRms { get; }

        /// <summary>
        /// ファイルサイズ（バイト単位）
        /// </summary>
        public long FileSize { get; }

        /// <summary>
        /// <para>先頭の無音サンプル数（チャンネルごと）</para>
        /// <para>
        /// 【目的】
        /// 書き出しタイミングのズレを高速補正するため、ロード時に一度だけ検出。
        /// これにより、比較時の総当りループ（FindBestTimeAlignment）を不要化。
        /// </para>
        /// <para>
        /// 【検出方法】
        /// 振幅が閾値（0.001f）を超える最初のサンプル位置を特定。
        /// </para>
        /// </summary>
        public int StartSilenceSamples { get; }

        /// <summary>
        /// <para>有効な音声長（先頭無音を除いたサンプル数）</para>
        /// <para>
        /// 【用途】
        /// - Phase 2の長さチェックで使用
        /// - 末尾の無音長が違うだけの同一ファイルを救済
        /// </para>
        /// </summary>
        public int EffectiveLength => TotalSamples > StartSilenceSamples * Channels
            ? TotalSamples - (StartSilenceSamples * Channels)
            : 0;

        /// <summary>
        /// <para>メモリ使用量の推定値（MB単位）</para>
        /// <para>
        /// 【計算方法】
        /// - Samples配列（null化済みなら0）
        /// - SamplesPerChannel配列の合計
        /// - NormalizedRegions配列内のDataサイズの合計
        /// </para>
        /// </summary>
        public double EstimatedMemoryMB
        {
            get
            {
                long totalBytes = 0;

                // インターリーブ配列が残っていれば加算
                if (Samples != null)
                    totalBytes += Samples.Length * sizeof(float);

                // チャンネル分離データのメモリを加算
                if (SamplesPerChannel != null)
                {
                    foreach (var channelData in SamplesPerChannel)
                    {
                        if (channelData != null)
                            totalBytes += channelData.Length * sizeof(float);
                    }
                }

                // 正規化波形（有音区間）のメモリを加算
                if (NormalizedRegions != null)
                {
                    foreach (var channelRegions in NormalizedRegions)
                    {
                        if (channelRegions != null)
                        {
                            foreach (var region in channelRegions)
                            {
                                if (region.Data != null)
                                    totalBytes += region.Data.Length * sizeof(float);
                            }
                        }
                    }
                }

                return totalBytes / 1024.0 / 1024.0;
            }
        }

        public bool IsPreNormalized => true;

        public IReadOnlyList<ActiveRegion>[] GetActiveRegions()
        {
            return NormalizedRegions ?? [[], []];
        }

        public ReadOnlySpan<float> GetRawSpan(int channel, int offset, int length)
        {
            if (channel < 0 || channel >= Channels) throw new ArgumentOutOfRangeException(nameof(channel));

            float[] buffer = new float[length];
            var regions = NormalizedRegions?[channel];
            if (regions == null) return buffer;

            int endOffset = offset + length;
            foreach (var region in regions)
            {
                int rStart = region.Offset;
                int rEnd = region.Offset + region.Length;

                if (rEnd <= offset || rStart >= endOffset) continue;

                int overlapStart = Math.Max(rStart, offset);
                int overlapEnd = Math.Min(rEnd, endOffset);

                int srcOffset = overlapStart - rStart;
                int destOffset = overlapStart - offset;
                int copyLength = overlapEnd - overlapStart;

                if (copyLength > 0 && region.Data != null)
                {
                    Array.Copy(region.Data, srcOffset, buffer, destOffset, copyLength);
                }
            }

            return buffer;
        }

        public double GetChannelSum(int channel)
        {
            throw new NotSupportedException("PreNormalizedSoundData does not support raw sum access.");
        }

        public double GetChannelSumSq(int channel)
        {
            throw new NotSupportedException("PreNormalizedSoundData does not support raw sum access.");
        }

        public double GetRangeSum(int channel, int offset, int length)
        {
            throw new NotSupportedException("PreNormalizedSoundData does not support raw range sum access.");
        }

        public double GetRangeSumSq(int channel, int offset, int length)
        {
            throw new NotSupportedException("PreNormalizedSoundData does not support raw range sum access.");
        }

        public ReadOnlySpan<ulong> GetLsh(int channel)
        {
            if (channel < 0 || channel >= Channels) throw new ArgumentOutOfRangeException(nameof(channel));
            if (_signLsh == null) return [];
            return _signLsh[channel];
        }

        public ReadOnlySpan<ulong> GetLshMask(int channel)
        {
            if (channel < 0 || channel >= Channels) throw new ArgumentOutOfRangeException(nameof(channel));
            if (_signLshMask == null) return [];
            return _signLshMask[channel];
        }

        private ulong[][]? _signLsh;
        private ulong[][]? _signLshMask;

        private void GenerateLsh(float[][] samplesPerChannel, int lengthSamples)
        {
            // FFT Parameter Constants (Phase 2 Measure B)
            int extractLen = Math.Min(lengthSamples, 2048);
            int fftLen = 4096;

            // Frequency domain magnitude produces fftLen/2 positive frequencies = 2048 bins
            int lshLength = 2048 / 64; // exactly 32 ulongs per channel
            _signLsh = [new ulong[lshLength], new ulong[lshLength]];
            _signLshMask = [new ulong[lshLength], new ulong[lshLength]];

            if (extractLen <= 0) return;

            double[] hannWindow = MathNet.Numerics.Window.Hann(extractLen);

            for (int ch = 0; ch < Channels; ch++)
            {
                var complexData = new Complex32[fftLen];
                var span = new ReadOnlySpan<float>(samplesPerChannel[ch], 0, extractLen);

                // 1. Extract, apply Hann Window, and zero-pad
                for (int i = 0; i < extractLen; i++)
                {
                    complexData[i] = new Complex32((float)(span[i] * hannWindow[i]), 0);
                }

                // 2. Perform FFT
                Fourier.Forward(complexData, FourierOptions.Default);

                // 3. Generate LSH from Magnitude Spectrum (Shift-invariant)
                float[] magnitudes = new float[2048];
                for (int i = 0; i < 2048; i++)
                {
                    magnitudes[i] = complexData[i].Magnitude;
                }

                // Create LSH bit array: bit is 1 if magnitude[i] >= magnitude[i+1] (spectral shape)
                for (int i = 0; i < 2048 - 1; i++)
                {
                    int lshIdx = i / 64;
                    int bitShift = i % 64;

                    if (magnitudes[i] >= magnitudes[i + 1])
                    {
                        _signLsh[ch][lshIdx] |= (1UL << bitShift);
                    }
                    if (magnitudes[i] > 1e-4f) // Simple threshold for mask
                    {
                        _signLshMask[ch][lshIdx] |= (1UL << bitShift);
                    }
                }
            }
        }

        #endregion

        #region コンストラクタ

        /// <summary>
        /// テスト用コンストラクタ（内部利用のみ）
        /// </summary>
        /// <param name="samplesPerChannel">チャンネル分離済みのサンプルデータ</param>
        /// <param name="sampleRate">サンプルレート（例: 44100）</param>
        /// <param name="bitsPerSample">ビット深度（例: 16）</param>
        /// <param name="filePath">ファイルパス（任意、テスト用）</param>
        /// <remarks>
        /// <para>【目的】</para>
        /// テストコードでファイルI/Oなしでモックデータを注入可能にするため。
        ///
        /// <para>【制約】</para>
        /// - NormalizedRegionsは自動計算されます
        /// - TotalRmsとStartSilenceSamplesも自動計算されます
        /// </remarks>
        internal PreNormalizedSoundData(float[][] samplesPerChannel, int sampleRate, int bitsPerSample, string filePath = "test.wav")
        {
            if (samplesPerChannel == null || samplesPerChannel.Length == 0)
                throw new ArgumentNullException(nameof(samplesPerChannel));

            if (samplesPerChannel[0] == null || samplesPerChannel[0].Length == 0)
                throw new ArgumentException("Samples per channel cannot be empty", nameof(samplesPerChannel));

            FilePath = filePath;
            SampleRate = sampleRate;
            Channels = samplesPerChannel.Length;
            BitsPerSample = bitsPerSample;
            SamplesPerChannel = samplesPerChannel;
            Samples = null; // テストではインターリーブデータは不要
            FileSize = 0; // テストでは無視

            int samplesPerChannel_count = samplesPerChannel[0].Length;
            TotalSamples = samplesPerChannel_count * Channels;

            // 有音区間と正規化波形を抽出・計算
            NormalizedRegions = ExtractActiveRegions(samplesPerChannel, Channels);

            // RMSを計算
            TotalRms = CalculateTotalRms(samplesPerChannel, samplesPerChannel_count, Channels);

            // 先頭無音を検出
            StartSilenceSamples = DetectStartSilence(samplesPerChannel, samplesPerChannel_count, Channels);

            // LSH（Locality-Sensitive Hashing）の符号ビット配列を生成（高速比較用）
            GenerateLsh(samplesPerChannel, samplesPerChannel_count);

            // メモリ最適化：比較処理には不要なため解放
            SamplesPerChannel = null!;
        }

        /// <summary>
        /// 音声ファイルからデータをロードしてキャッシュします。
        /// ファイル読み込みエラー時に InvalidOperationException をスロー
        /// </summary>
        /// <param name="path">音声ファイルのフルパス</param>
        /// <param name="normalizationMode">波形正規化モード</param>
        /// <exception cref="InvalidOperationException">ファイル読み込み失敗時</exception>
        public PreNormalizedSoundData(string path, NormalizationMode normalizationMode = NormalizationMode.None)
        {
            FilePath = path;

            try
            {
                var fileName = Path.GetFileName(path);
                Stream? memoryStreamToDispose = null;
                WaveStream stream;
                ISampleProvider sampleProvider;

                if (VirtualAudioRegistry.TryGetStream(fileName, out var vStream))
                {
                    VirtualAudioRegistry.TryGetFileSize(fileName, out var size);
                    FileSize = size;
                    memoryStreamToDispose = vStream;
                    var waveReader = new WaveFileReader(memoryStreamToDispose);
                    stream = waveReader;
                    sampleProvider = waveReader.ToSampleProvider();
                }
                else
                {
                    var fi = new FileInfo(path);
                    if (!fi.Exists)
                    {
                        throw new FileNotFoundException($"File not found: {path}");
                    }
                    FileSize = fi.Length;
                    var audioReader = new AudioFileReader(path);
                    stream = audioReader;
                    sampleProvider = audioReader;
                }

                using (memoryStreamToDispose)
                using (stream)
                {
                    SampleRate = stream.WaveFormat.SampleRate;
                    Channels = stream.WaveFormat.Channels;
                    BitsPerSample = stream.WaveFormat.BitsPerSample;

                    // 全データをメモリに読み込む
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
                    int bufferSize = Math.Min(stream.WaveFormat.SampleRate * stream.WaveFormat.Channels, (int)totalSamples);

                    while (totalRead < totalSamples)
                    {
                        int toRead = (int)Math.Min(bufferSize, totalSamples - totalRead);
                        int read = sampleProvider.Read(samplesArray, totalRead, toRead);

                        if (read == 0)
                        {
                            PerformanceDebugLogger.WriteLine($"[CachedSoundData] WARNING: Read returned 0 at {totalRead}/{totalSamples} for {Path.GetFileName(path)}");
                            break;
                        }

                        totalRead += read;
                    }

                    if (totalRead == 0)
                    {
                        throw new InvalidOperationException($"Failed to read any samples from file: {path}");
                    }

                    // 実際に読み込んだサンプル数が想定より少ない場合は配列をリサイズ
                    if (totalRead < totalSamples)
                    {
                        Array.Resize(ref samplesArray, totalRead);
                    }

                    Samples = samplesArray;

                    // チャンネル分離（デインターリーブ）を事前実行
                    int samplesPerChannel = Samples.Length / Channels;
                    TotalSamples = samplesPerChannel * Channels;
                    SamplesPerChannel = DeinterleaveChannels(Samples, Channels, samplesPerChannel);

                    // メモリ最適化: デインターリーブ後はインターリーブ配列を解放
                    Samples = null;

                    // 波形を正規化（指定された場合）
                    if (normalizationMode != NormalizationMode.None)
                    {
                        ApplyNormalization(normalizationMode);
                    }

                    // Phase 2: 有音区間の抽出と正規化（無音区間のメモリを削減し、ドット積をサボる）
                    NormalizedRegions = ExtractActiveRegions(SamplesPerChannel, Channels);

                    // 先頭無音を検出（高速比較用）
                    StartSilenceSamples = DetectStartSilence(SamplesPerChannel, samplesPerChannel, Channels);

                    // RMS（音圧）を計算（高速フィルタ用）
                    TotalRms = CalculateTotalRms(SamplesPerChannel, samplesPerChannel, Channels);

                    // LSH（Locality-Sensitive Hashing）の符号ビット配列を生成（高速比較用）
                    GenerateLsh(SamplesPerChannel, samplesPerChannel);

                    // メモリ最適化：比較処理には不要なためチャンネル分離データを解放
                    SamplesPerChannel = null!;
                }
            }
            catch (Exception ex)
            {
                PerformanceDebugLogger.WriteLine($"[CachedSoundData] ERROR loading {Path.GetFileName(path)}: {ex.Message}");
                throw new InvalidOperationException($"音声ファイルの読み込みに失敗: {path}", ex);
            }
        }

        #endregion

        #region プライベートメソッド

        /// <summary>
        /// <para>インターリーブされたデータをチャンネルごとに分離</para>
        /// <para>
        /// 【入力】
        /// インターリーブ: [L0, R0, L1, R1, L2, R2, ...]
        /// </para>
        /// <para>
        /// 【出力】
        /// SamplesPerChannel[0]: [L0, L1, L2, ...] (左チャンネル)
        /// SamplesPerChannel[1]: [R0, R1, R2, ...] (右チャンネル)
        /// </para>
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
        /// 波形を指定されたモードで正規化
        /// </summary>
        private void ApplyNormalization(NormalizationMode mode)
        {
            switch (mode)
            {
                case NormalizationMode.PeakNormalize:
                    NormalizePeak();
                    break;
                case NormalizationMode.RmsNormalize:
                    NormalizeRms();
                    break;
            }
        }

        /// <summary>
        /// <para>ピークノーマライズ: 最大値を1.0に統一</para>
        /// <para>
        /// 【処理】
        /// 1. 全チャンネルの最大値（絶対値）を見つける
        /// 2. 全サンプルをその値で除算
        /// </para>
        /// <para>
        /// 【効果】
        /// - 波形の形状を100%保持
        /// - 音量差のある音声を統一
        /// - 最も単純で高速
        /// </para>
        /// </summary>
        private void NormalizePeak()
        {
            // 最大値を見つける
            float maxAbsValue = 0.0f;
            for (int ch = 0; ch < Channels; ch++)
            {
                foreach (float sample in SamplesPerChannel[ch])
                {
                    float absValue = Math.Abs(sample);
                    if (absValue > maxAbsValue)
                        maxAbsValue = absValue;
                }
            }

            // ゼロ除算回避（無音ファイル対応）
            if (maxAbsValue < 1e-10f)
                return;

            // 全チャンネルを正規化
            for (int ch = 0; ch < Channels; ch++)
            {
                for (int i = 0; i < SamplesPerChannel[ch].Length; i++)
                {
                    SamplesPerChannel[ch][i] /= maxAbsValue;
                }
            }
        }

        /// <summary>
        /// <para>RMSノーマライズ: エネルギー（音圧）を統一</para>
        /// <para>
        /// 【処理】
        /// 1. 現在のRMSを計算
        /// 2. 目標RMS（デフォルト: 0.5）に正規化
        /// </para>
        /// <para>
        /// 【効果】
        /// - 知覚的な音量を統一
        /// - 無音部分の影響を受けにくい
        /// - 音声圧縮への対応が優れている
        /// </para>
        /// <para>
        /// 【計算式】
        /// normalized[i] = sample[i] * (targetRMS / currentRMS)
        /// </para>
        /// </summary>
        /// <param name="targetRms">目標RMS値（デフォルト: 0.5）</param>
        private void NormalizeRms(float targetRms = 0.5f)
        {
            // 現在のRMSを計算
            float currentRms = CalculateTotalRms(SamplesPerChannel, SamplesPerChannel[0].Length, Channels);

            // ゼロ除算回避（無音ファイル対応）
            if (currentRms < 1e-10f)
                return;

            // スケーリング係数を計算
            float scaleFactor = targetRms / currentRms;

            // 全チャンネルを正規化
            for (int ch = 0; ch < Channels; ch++)
            {
                for (int i = 0; i < SamplesPerChannel[ch].Length; i++)
                {
                    SamplesPerChannel[ch][i] *= scaleFactor;
                }
            }

            // RMSは正規化後は自動的に targetRms になる
            // （TotalRmsは後で再計算されるため明示的な更新は不要）
        }

        /// <summary>
        /// <para>Phase 2: 正規化波形を計算</para>
        /// <para>
        /// 【数学的背景】
        /// ピアソン相関係数の定義:
        /// $r = \frac{\sum(x_i - \bar{x})(y_i - \bar{y})}{\sqrt{\sum(x_i - \bar{x})^2} \sqrt{\sum(y_i - \bar{y})^2}}$
        /// </para>
        /// <para>
        /// 正規化波形の定義:
        /// $\hat{x}_i = \frac{x_i - \bar{x}}{\sqrt{\sum_{j=1}^{n}(x_j - \bar{x})^2}}$
        /// </para>
        /// <para>
        /// この変換により:
        /// $r = \sum_{i=1}^{n} \hat{x}_i \cdot \hat{y}_i$
        /// </para>
        /// <para>
        /// 【処理】
        /// 1. 平均値を計算
        /// 2. 分散を計算
        /// 3. 標準偏差で正規化（ゼロ除算対策付き）
        /// </para>
        /// <para>
        /// <summary>
        /// Phase 2: 有音区間の抽出と正規化
        ///
        /// 【数学的背景】
        /// ピアソン相関係数の定義:
        /// $r = \frac{\sum(x_i - \bar{x})(y_i - \bar{y})}{\sqrt{\sum(x_i - \bar{x})^2} \sqrt{\sum(y_i - \bar{y})^2}}$
        ///
        /// 正規化波形の定義:
        /// $\hat{x}_i = \frac{x_i - \bar{x}}{\sqrt{\sum_{j=1}^{n}(x_j - \bar{x})^2}}$
        ///
        /// この変換により:
        /// $r = \sum_{i=1}^{n} \hat{x}_i \cdot \hat{y}_i$
        ///
        /// 【処理】
        /// 1. 平均値を計算
        /// 2. 分散を計算
        /// 3. O(1)スライディングウィンドウで有音区間（ActiveRegion）を抽出
        /// 4. 抽出された区間のみ標準偏差で正規化（ゼロ除算対策付き）
        ///
        /// 【効果】
        /// - 無音区間のメモリを削減
        /// - 比較時のドット積計算を有音区間の交差のみに限定し、計算をサボる（高速化）
        /// </summary>
        /// </para>
        private static List<ActiveRegion>[] ExtractActiveRegions(float[][] samplesPerChannel, int channels)
        {
            var regionsPerChannel = new List<ActiveRegion>[channels];

            const double dbThreshold = -90.0;
            const int windowFrames = 256; // 約5.8ms (44.1kHz時)

            // E_threshold = N * A_ref^2 * 10^(T_threshold/10) (A_ref = 1.0)
            double eThreshold = windowFrames * Math.Pow(10, dbThreshold / 10.0);
            const int maxSilenceFrames = AppConstants.Audio.StandardSampleRate / 4; // 250ms

            for (int ch = 0; ch < channels; ch++)
            {
                var samples = samplesPerChannel[ch];
                regionsPerChannel[ch] = [];

                int totalFrames = samples.Length;

                // ステップ1: 平均を計算
                double sum = 0;
                for (int i = 0; i < totalFrames; i++) sum += samples[i];
                double mean = sum / totalFrames;

                // ステップ2: 分散（偏差平方和）を計算
                double varianceSum = 0;
                for (int i = 0; i < totalFrames; i++)
                {
                    double centered = samples[i] - mean;
                    varianceSum += centered * centered;
                }
                double norm = Math.Sqrt(varianceSum);

                // 無音またはほぼ無音のチャンネル
                if (norm < 1e-10) continue;

                // 長さが短すぎる場合はスライディングウィンドウを適用せず全体を1つのRegionにする
                if (totalFrames <= windowFrames)
                {
                    float[] data = new float[totalFrames];
                    for (int i = 0; i < totalFrames; i++) data[i] = (float)((samples[i] - mean) / norm);
                    regionsPerChannel[ch].Add(new ActiveRegion(0, totalFrames, data));
                    continue;
                }

                // ステップ3: スライディングウィンドウで有音区間を抽出
                double currentEnergy = 0;
                for (int i = 0; i < windowFrames; i++)
                {
                    currentEnergy += (double)samples[i] * samples[i];
                }

                bool inSound = false;
                int regionStart = 0;
                int continuousSilence = 0;

                for (int i = 0; i < totalFrames - windowFrames; i++)
                {
                    bool windowHasSound = currentEnergy >= eThreshold;

                    if (!inSound)
                    {
                        if (windowHasSound)
                        {
                            inSound = true;
                            regionStart = i;
                            continuousSilence = 0;
                        }
                    }
                    else
                    {
                        if (!windowHasSound)
                        {
                            continuousSilence++;
                            if (continuousSilence >= maxSilenceFrames)
                            {
                                int regionEnd = i - continuousSilence + windowFrames;
                                regionEnd = Math.Max(regionStart, Math.Min(totalFrames, regionEnd));

                                int regionLength = regionEnd - regionStart;
                                if (regionLength > 0)
                                {
                                    float[] regionData = new float[regionLength];
                                    for (int j = 0; j < regionLength; j++)
                                    {
                                        regionData[j] = (float)((samples[regionStart + j] - mean) / norm);
                                    }
                                    regionsPerChannel[ch].Add(new ActiveRegion(regionStart, regionLength, regionData));
                                }

                                inSound = false;
                                continuousSilence = 0;
                            }
                        }
                        else
                        {
                            continuousSilence = 0;
                        }
                    }

                    // Slide window (O(1))
                    double outSample = samples[i];
                    double inSample = samples[i + windowFrames];
                    currentEnergy = currentEnergy - (outSample * outSample) + (inSample * inSample);
                    if (currentEnergy < 0) currentEnergy = 0; // 数値誤差対策
                }

                // ファイル終端時にまだSound状態なら閉じる
                if (inSound)
                {
                    int regionEnd = totalFrames;
                    int regionLength = regionEnd - regionStart;
                    if (regionLength > 0)
                    {
                        float[] regionData = new float[regionLength];
                        for (int j = 0; j < regionLength; j++)
                        {
                            regionData[j] = (float)((samples[regionStart + j] - mean) / norm);
                        }
                        regionsPerChannel[ch].Add(new ActiveRegion(regionStart, regionLength, regionData));
                    }
                }
            }

            return regionsPerChannel;
        }

        /// <summary>
        /// <para>全体のRMS（二乗平均平方根）を計算</para>
        /// <para>
        /// 【計算式】
        /// RMS = sqrt(Σ(sample²) / N)
        /// </para>
        /// <para>ここでは全チャンネルの全サンプルを対象に計算</para>
        /// </summary>
        private static float CalculateTotalRms(float[][] channelData, int samplesPerChannel, int channels)
        {
            double sum = 0;

            for (int ch = 0; ch < channels; ch++)
            {
                var data = channelData[ch];
                for (int i = 0; i < data.Length; i++)
                {
                    sum += data[i] * data[i];
                }
            }

            return (float)Math.Sqrt(sum / (samplesPerChannel * channels));
        }

        /// <summary>
        /// <para>先頭の無音サンプル数を検出</para>
        /// <para>
        /// 【アルゴリズム】
        /// 1. 全チャンネルの最初のサンプルから順に走査
        /// 2. いずれかのチャンネルで振幅が閾値を超えたら、その位置を返す
        /// 3. 全サンプルが閾値未満なら0を返す（完全無音ファイル）
        /// </para>
        /// <para>
        /// 【閾値】
        /// 0.001f（RMS無音判定と同じ値）
        /// </para>
        /// </summary>
        /// <param name="channelData">チャンネル分離された波形データ</param>
        /// <param name="samplesPerChannel">チャンネルごとのサンプル数</param>
        /// <param name="channels">チャンネル数</param>
        /// <returns>先頭の無音サンプル数</returns>
        private static int DetectStartSilence(float[][] channelData, int samplesPerChannel, int channels)
        {
            const float SilenceThreshold = AppConstants.AudioComparison.SilenceRmsThreshold;

            for (int i = 0; i < samplesPerChannel; i++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    if (Math.Abs(channelData[ch][i]) > SilenceThreshold)
                    {
                        return i;
                    }
                }
            }

            // 完全無音ファイル
            return 0;
        }

        #endregion

        #region IDisposable実装

        private bool _disposed = false;

        /// <summary>
        /// キャッシュされた音声データを解放します。
        /// </summary>
        /// <remarks>
        /// <para>【解放対象】</para>
        /// <list type="bullet">
        /// <item>SamplesPerChannel: チャンネル分離データ</item>
        /// <item>NormalizedWaveform: 正規化波形データ</item>
        /// <item>Samples: インターリーブデータ（通常は既にnull）</item>
        /// </list>
        ///
        /// <para>【効果】</para>
        /// 数百MBのメモリを即座に解放し、GC待ちを回避します。
        ///
        /// <para>【LOH対策】</para>
        /// 大きな配列を個別にnullに設定し、LOH内のメモリも確実に解放します。
        /// </remarks>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // マネージドリソースの解放
                // 大きな配列を個別にnullに設定（LOH対策）
                Samples = null;

                // SamplesPerChannelの各チャンネルを個別に解放
                if (SamplesPerChannel != null)
                {
                    for (int i = 0; i < SamplesPerChannel.Length; i++)
                    {
                        SamplesPerChannel[i] = null!;
                    }
                    SamplesPerChannel = null!;
                }

                // NormalizedRegionsの各チャンネルを個別に解放
                if (NormalizedRegions != null)
                {
                    for (int i = 0; i < NormalizedRegions.Length; i++)
                    {
                        if (NormalizedRegions[i] != null)
                        {
                            NormalizedRegions[i].Clear();
                            NormalizedRegions[i] = null!;
                        }
                    }
                    NormalizedRegions = null;
                }

                _signLsh = null;
                _signLshMask = null;
            }

            _disposed = true;
        }

        #endregion
    }
}
