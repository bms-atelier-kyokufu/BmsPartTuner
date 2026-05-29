using BmsAtelierKyokufu.BmsPartTuner.Core.Attributes;
using System.Collections.Concurrent;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

/// <summary>
/// FFT畳み込み定理を用いたサブミリ秒アライメントのズレ量推定エンジン。
/// 呼び出しホットパスにおけるメモリアロケーションを完全に排除するよう設計されています。
/// </summary>
[ADRAnchor("M-03", nameof(FftAlignmentEngine))]
[ADRAnchor("OPT-11", nameof(FftAlignmentEngine))]
public static class FftAlignmentEngine
{
    private const int FftLength = 4096; // 2048 + 2048 - 1 <= 4096 (Radix-2)

    // スレッドごとに使い回すFFT計算バッファ
    private static readonly ThreadLocal<Complex32[]> ThreadLocalComplexX = new(static () => new Complex32[FftLength]);
    private static readonly ThreadLocal<Complex32[]> ThreadLocalComplexY = new(static () => new Complex32[FftLength]);
    private static readonly ThreadLocal<Complex32[]> ThreadLocalComplexResult = new(static () => new Complex32[FftLength]);

    // ハニング窓のキャッシュ（配列アロケーション回避用）
    private static readonly ConcurrentDictionary<int, float[]> HannWindowCache = new();

    /// <summary>
    /// キャッシュから指定の長さのハニング窓係数を取得または生成します。
    /// </summary>
    private static float[] GetHannWindow(int len)
    {
        return HannWindowCache.GetOrAdd(len, static l =>
        {
            double[] win = MathNet.Numerics.Window.Hann(l);
            float[] floatWin = new float[l];
            for (int i = 0; i < l; i++)
            {
                floatWin[i] = (float)win[i];
            }
            return floatWin;
        });
    }

    /// <summary>
    /// 短い波形と長い波形を比較し、最も相関が高くなるズレ量（サンプル数）を算出します。
    /// </summary>
    public static int CalculateAlignmentOffset(ReadOnlySpan<float> shorter, ReadOnlySpan<float> longer)
    {
        int extractLen = Math.Min(Math.Min(shorter.Length, longer.Length), 2048);
        if (extractLen <= 0) return 0;

        var complexX = ThreadLocalComplexX.Value!;
        var complexY = ThreadLocalComplexY.Value!;
        var complexResult = ThreadLocalComplexResult.Value!;

        float[] hannWindow = GetHannWindow(extractLen);

        // ハニング窓を適用してバッファへコピー
        for (int i = 0; i < extractLen; i++)
        {
            complexX[i] = new Complex32(shorter[i] * hannWindow[i], 0);
            complexY[i] = new Complex32(longer[i] * hannWindow[i], 0);
        }

        // 残りのバッファ領域をゼロパディング（前回のデータを残さないため必須）
        for (int i = extractLen; i < FftLength; i++)
        {
            complexX[i] = Complex32.Zero;
            complexY[i] = Complex32.Zero;
        }

        // フーリエ順変換を実行（高速化のためバッファを直接書き換える）
        Fourier.Forward(complexX, FourierOptions.Default);
        Fourier.Forward(complexY, FourierOptions.Default);

        // クロススペクトルの計算
        for (int i = 0; i < FftLength; i++)
        {
            complexResult[i] = complexX[i] * complexY[i].Conjugate();
        }

        // 逆フーリエ変換を実行
        Fourier.Inverse(complexResult, FourierOptions.Default);

        float maxVal = float.MinValue;
        int maxIndex = 0;

        int searchRange = Math.Min(1000, FftLength / 2); // Limit search to ~22ms to avoid false positives

        // 正方向のズレを探索
        for (int i = 0; i < searchRange; i++)
        {
            if (complexResult[i].Real > maxVal)
            {
                maxVal = complexResult[i].Real;
                maxIndex = i;
            }
        }

        // 負方向のズレを探索
        for (int i = FftLength - searchRange; i < FftLength; i++)
        {
            if (complexResult[i].Real > maxVal)
            {
                maxVal = complexResult[i].Real;
                maxIndex = i - FftLength;
            }
        }

        return maxIndex;
    }



    /// <summary>
    /// 事前計算されたFFTスペクトルを使用して、最も相関が高くなるズレ量（サンプル数）を算出します
    /// </summary>
    public static int CalculateAlignmentOffsetFromCache(Complex32[] fftX, Complex32[] fftY)
    {
        if (fftX.Length != FftLength || fftY.Length != FftLength)
            return 0;

        var complexResult = ThreadLocalComplexResult.Value!;

        // クロススペクトルの計算
        for (int i = 0; i < FftLength; i++)
        {
            complexResult[i] = fftX[i] * fftY[i].Conjugate();
        }

        // 逆フーリエ変換を実行
        Fourier.Inverse(complexResult, FourierOptions.Default);

        float maxVal = float.MinValue;
        int maxIndex = 0;

        int searchRange = Math.Min(1000, FftLength / 2); // Limit search to ~22ms to avoid false positives

        // 正方向のズレを探索
        for (int i = 0; i < searchRange; i++)
        {
            if (complexResult[i].Real > maxVal)
            {
                maxVal = complexResult[i].Real;
                maxIndex = i;
            }
        }

        // 負方向のズレを探索
        for (int i = FftLength - searchRange; i < FftLength; i++)
        {
            if (complexResult[i].Real > maxVal)
            {
                maxVal = complexResult[i].Real;
                maxIndex = i - FftLength;
            }
        }

        return maxIndex;
    }
}
