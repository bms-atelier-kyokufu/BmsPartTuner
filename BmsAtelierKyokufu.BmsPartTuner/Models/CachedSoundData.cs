using System.Diagnostics;
using NAudio.Wave;

namespace BmsAtelierKyokufu.BmsPartTuner.Models
{
    /// <summary>
    /// 波形正規化モード
    /// 
    /// 【概要】
    /// ロード時に波形を正規化し、音量差に強い比較を実現
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
    /// オンメモリでキャッシュされた音声データ（SIMD最適化版）
    /// 
    /// 【目的】
    /// - ディスクI/Oを最小化し、高速比較を実現
    /// - 音声データを事前にデインターリーブ（チャンネル分離）
    /// - SIMD演算に最適なデータ構造を提供
    /// 
    /// 【メモリ最適化戦略】
    /// 1. ロード時にインターリーブデータを取得
    /// 2. チャンネルごとにデインターリーブ
    /// 3. 元のインターリーブデータを破棄（メモリ削減）
    /// 4. RMSを事前計算（高速フィルタ用）
    /// 5. Phase 2: 正規化波形を事前計算（ドット積への帰着）
    /// 
    /// 【効果】
    /// - メモリ使用量: 30〜40%削減
    /// - 比較時のデインターリーブ処理: 完全削除
    /// - GC負荷: 大幅軽減
    /// - Phase 2: 比較時の演算を5倍削減
    /// 
    /// 【メモリリーク対策】
    /// - IDisposableを実装し、明示的なメモリ解放をサポート
    /// - 処理完了後にDisposeを呼び出すことで、大量のメモリを即座に解放
    /// </summary>
    public class CachedSoundData : IDisposable
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
        /// インターリーブされた元データ（デインターリーブ後はnull）
        /// 
        /// 【メモリ最適化】
        /// デインターリーブ完了後にnullを設定してメモリを解放
        /// これにより2196ファイル分のメモリ使用量を30〜40%削減
        /// </summary>
        public float[]? Samples { get; private set; }

        /// <summary>
        /// チャンネルごとに分離されたデータ（高速比較用）
        /// 
        /// 【データ構造】
        /// [チャンネル番号][サンプル番号]
        /// 
        /// 例: ステレオ音声（44.1kHz, 1秒）
        /// SamplesPerChannel[0] = 左チャンネル 44,100サンプル
        /// SamplesPerChannel[1] = 右チャンネル 44,100サンプル
        /// 
        /// 【利点】
        /// - 比較時のデインターリーブ不要
        /// - 連続メモリアクセス（キャッシュ効率向上）
        /// - SIMD演算に最適
        /// </summary>
        public float[][] SamplesPerChannel { get; private set; }

        /// <summary>
        /// Phase 2: 正規化された波形（ドット積でピアソン相関係数を計算可能）
        /// 
        /// 【数学的定義】
        /// $\hat{x}_i = \frac{x_i - \bar{x}}{\sqrt{\sum_{j=1}^{n}(x_j - \bar{x})^2}}$
        /// 
        /// 【特性】
        /// - 平均: 0
        /// - ノルム: 1
        /// 
        /// 【用途】
        /// ピアソン相関係数 $r$ を単なるドット積に帰着:
        /// $r = \sum_{i=1}^{n} \hat{x}_i \cdot \hat{y}_i$
        /// 
        /// 【効果】
        /// - 比較時の計算を5変数の累積から1変数のFMA積算へ簡略化
        /// - 演算密度を80%削減
        /// 
        /// 【無音対応】
        /// 分散がほぼ0の場合、全てのサンプルを0で初期化
        /// </summary>
        public float[][]? NormalizedWaveform { get; private set; }

        /// <summary>
        /// 全サンプルの総数（全チャンネル合計）
        /// </summary>
        public int TotalSamples
        {
            get
            {
                if (SamplesPerChannel == null || SamplesPerChannel.Length == 0) return 0;
                return SamplesPerChannel[0].Length * Channels;
            }
        }

        /// <summary>
        /// RMS（Root Mean Square: 二乗平均平方根）
        /// 
        /// 【計算式】
        /// RMS = sqrt(Σ(sample²) / N)
        /// 
        /// 【意味】
        /// 音声の全体的な音圧レベルを表す
        /// 
        /// 【用途】
        /// - 高速フィルタ（Phase 3）
        /// - Sort & Sweep のソートキー
        /// - 20%以上の差があれば即座に不一致判定
        /// </summary>
        public float TotalRms { get; private set; }

        /// <summary>
        /// ファイルサイズ（バイト単位）
        /// </summary>
        public long FileSize { get; }

        /// <summary>
        /// 先頭の無音サンプル数（チャンネルごと）
        /// 
        /// 【目的】
        /// 書き出しタイミングのズレを高速補正するため、ロード時に一度だけ検出。
        /// これにより、比較時の総当りループ（FindBestTimeAlignment）を不要化。
        /// 
        /// 【検出方法】
        /// 振幅が閾値（0.001f）を超える最初のサンプル位置を特定。
        /// </summary>
        public int StartSilenceSamples { get; private set; }

        /// <summary>
        /// 有効な音声長（先頭無音を除いたサンプル数）
        /// 
        /// 【用途】
        /// - Phase 2の長さチェックで使用
        /// - 末尾の無音長が違うだけの同一ファイルを救済
        /// </summary>
        public int EffectiveLength => TotalSamples > StartSilenceSamples * Channels
            ? TotalSamples - (StartSilenceSamples * Channels)
            : 0;

        /// <summary>
        /// メモリ使用量の推定値（MB単位）
        /// 
        /// 【計算方法】
        /// - Samples配列（null化済みなら0）
        /// - SamplesPerChannel配列の合計
        /// - NormalizedWaveform配列の合計
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

                // 正規化波形のメモリを加算
                if (NormalizedWaveform != null)
                {
                    foreach (var channelData in NormalizedWaveform)
                    {
                        if (channelData != null)
                            totalBytes += channelData.Length * sizeof(float);
                    }
                }

                return totalBytes / 1024.0 / 1024.0;
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
        /// - NormalizedWaveformは自動計算されます
        /// - TotalRmsとStartSilenceSamplesも自動計算されます
        /// </remarks>
        internal CachedSoundData(float[][] samplesPerChannel, int sampleRate, int bitsPerSample, string filePath = "test.wav")
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

            // 正規化波形を計算
            NormalizedWaveform = ComputeNormalizedWaveform(samplesPerChannel, samplesPerChannel_count, Channels);

            // RMSを計算
            TotalRms = CalculateTotalRms(samplesPerChannel, samplesPerChannel_count, Channels);

            // 先頭無音を検出
            StartSilenceSamples = DetectStartSilence(samplesPerChannel, samplesPerChannel_count, Channels);
        }

        /// <summary>
        /// 音声ファイルからデータをロードしてキャッシュします。
        /// ファイル読み込みエラー時に InvalidOperationException をスロー
        /// </summary>
        /// <param name="path">音声ファイルのフルパス</param>
        /// <param name="normalizationMode">波形正規化モード</param>
        /// <exception cref="InvalidOperationException">ファイル読み込み失敗時</exception>
        public CachedSoundData(string path, NormalizationMode normalizationMode = NormalizationMode.None)
        {
            FilePath = path;

            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists)
                {
                    throw new FileNotFoundException($"File not found: {path}");
                }
                FileSize = fi.Length;

                using (var reader = new AudioFileReader(path))
                {
                    SampleRate = reader.WaveFormat.SampleRate;
                    Channels = reader.WaveFormat.Channels;
                    BitsPerSample = reader.WaveFormat.BitsPerSample;

                    // 全データをメモリに読み込む
                    // NAudio 2.x: reader.Length はバイト数、AudioFileReaderは常にfloat[]を返す
                    long totalSamples = reader.Length / sizeof(float);

                    if (totalSamples == 0)
                    {
                        throw new InvalidOperationException($"File has zero samples: {path}");
                    }

                    float[] samplesArray = new float[totalSamples];

                    int totalRead = 0;
                    int bufferSize = Math.Min(reader.WaveFormat.SampleRate * reader.WaveFormat.Channels, (int)totalSamples);

                    while (totalRead < totalSamples)
                    {
                        int toRead = (int)Math.Min(bufferSize, totalSamples - totalRead);
                        int read = reader.Read(samplesArray, totalRead, toRead);

                        if (read == 0)
                        {
                            Debug.WriteLine($"[CachedSoundData] WARNING: Read returned 0 at {totalRead}/{totalSamples} for {Path.GetFileName(path)}");
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
                    SamplesPerChannel = DeinterleaveChannels(Samples, Channels, samplesPerChannel);

                    // メモリ最適化: デインターリーブ後はインターリーブ配列を解放
                    Samples = null;

                    // 波形を正規化（指定された場合）
                    if (normalizationMode != NormalizationMode.None)
                    {
                        ApplyNormalization(normalizationMode);
                    }

                    // Phase 2: 正規化波形を事前計算（ドット積でピアソン相関係数を計算可能に）
                    NormalizedWaveform = ComputeNormalizedWaveform(SamplesPerChannel, samplesPerChannel, Channels);

                    // 先頭無音を検出（高速比較用）
                    StartSilenceSamples = DetectStartSilence(SamplesPerChannel, samplesPerChannel, Channels);

                    // RMS（音圧）を計算（高速フィルタ用）
                    TotalRms = CalculateTotalRms(SamplesPerChannel, samplesPerChannel, Channels);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CachedSoundData] ERROR loading {Path.GetFileName(path)}: {ex.Message}");
                throw new InvalidOperationException($"音声ファイルの読み込みに失敗: {path}", ex);
            }
        }

        #endregion

        #region プライベートメソッド

        /// <summary>
        /// インターリーブされたデータをチャンネルごとに分離
        /// 
        /// 【入力】
        /// インターリーブ: [L0, R0, L1, R1, L2, R2, ...]
        /// 
        /// 【出力】
        /// SamplesPerChannel[0]: [L0, L1, L2, ...] (左チャンネル)
        /// SamplesPerChannel[1]: [R0, R1, R2, ...] (右チャンネル)
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
        /// ピークノーマライズ: 最大値を1.0に統一
        /// 
        /// 【処理】
        /// 1. 全チャンネルの最大値（絶対値）を見つける
        /// 2. 全サンプルをその値で除算
        /// 
        /// 【効果】
        /// - 波形の形状を100%保持
        /// - 音量差のある音声を統一
        /// - 最も単純で高速
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
        /// RMSノーマライズ: エネルギー（音圧）を統一
        /// 
        /// 【処理】
        /// 1. 現在のRMSを計算
        /// 2. 目標RMS（デフォルト: 0.5）に正規化
        /// 
        /// 【効果】
        /// - 知覚的な音量を統一
        /// - 無音部分の影響を受けにくい
        /// - 音声圧縮への対応が優れている
        /// 
        /// 【計算式】
        /// normalized[i] = sample[i] * (targetRMS / currentRMS)
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
        /// Phase 2: 正規化波形を計算
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
        /// 3. 標準偏差で正規化（ゼロ除算対策付き）
        /// 
        /// 【効果】
        /// - 比較時の5変数の累積計算を1変数のドット積に削減
        /// - SIMD演算との相性向上
        /// </summary>
        private static float[][] ComputeNormalizedWaveform(float[][] samplesPerChannel, int samplesPerChannelCount, int channels)
        {
            var normalized = new float[channels][];

            for (int ch = 0; ch < channels; ch++)
            {
                var samples = samplesPerChannel[ch];
                normalized[ch] = new float[samples.Length];

                // ステップ1: 平均を計算
                double sum = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    sum += samples[i];
                }
                double mean = sum / samples.Length;

                // ステップ2: 分散（偏差平方和）を計算
                double varianceSum = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    double centered = samples[i] - mean;
                    varianceSum += centered * centered;
                }

                // ステップ3: 標準偏差（ノルム）を計算してゼロ除算チェック
                double norm = Math.Sqrt(varianceSum);
                if (norm < 1e-10)
                {
                    // 無音またはほぼ無音のチャンネル：ゼロで初期化
                    // この場合、ドット積は0になり、相関は0と判定される
                    Array.Clear(normalized[ch], 0, normalized[ch].Length);
                    continue;
                }

                // ステップ4: 正規化（平均0、ノルム1）
                for (int i = 0; i < samples.Length; i++)
                {
                    normalized[ch][i] = (float)((samples[i] - mean) / norm);
                }
            }

            return normalized;
        }

        /// <summary>
        /// 全体のRMS（二乗平均平方根）を計算
        /// 
        /// 【計算式】
        /// RMS = sqrt(Σ(sample²) / N)
        /// 
        /// ここでは全チャンネルの全サンプルを対象に計算
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
        /// 先頭の無音サンプル数を検出
        /// 
        /// 【アルゴリズム】
        /// 1. 全チャンネルの最初のサンプルから順に走査
        /// 2. いずれかのチャンネルで振幅が閾値を超えたら、その位置を返す
        /// 3. 全サンプルが閾値未満なら0を返す（完全無音ファイル）
        /// 
        /// 【閾値】
        /// 0.001f（RMS無音判定と同じ値）
        /// </summary>
        /// <param name="channelData">チャンネル分離された波形データ</param>
        /// <param name="samplesPerChannel">チャンネルごとのサンプル数</param>
        /// <param name="channels">チャンネル数</param>
        /// <returns>先頭の無音サンプル数</returns>
        private static int DetectStartSilence(float[][] channelData, int samplesPerChannel, int channels)
        {
            const float SilenceThreshold = 0.001f;

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

                // NormalizedWaveformの各チャンネルを個別に解放
                if (NormalizedWaveform != null)
                {
                    for (int i = 0; i < NormalizedWaveform.Length; i++)
                    {
                        NormalizedWaveform[i] = null!;
                    }
                    NormalizedWaveform = null;
                }
            }

            _disposed = true;
        }

        #endregion
    }
}
