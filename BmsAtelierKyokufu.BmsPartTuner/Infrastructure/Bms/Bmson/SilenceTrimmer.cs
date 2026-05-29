using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using BmsAtelierKyokufu.BmsPartTuner.Core.Attributes;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson;

public static class SilenceTrimmer
{
    private const int WindowFrames = 1024; // 約23msのウィンドウサイズ

    /// <summary>
    /// 無音判定のエネルギー閾値（実行前に1度だけ計算）。
    /// </summary>
    public static readonly long E_threshold = CalculateEnergyThreshold();

    private static long CalculateEnergyThreshold()
    {
        const double dbThreshold = -45.0; // -45dBを無音とみなす（フロアノイズ対応）
        const double maxAmp = 32768.0;    // 16bit PCMの最大振幅
        const int totalSamples = WindowFrames * 2; // ステレオなのでフレーム数の2倍
        return (long)(totalSamples * maxAmp * maxAmp * Math.Pow(10, dbThreshold / 10.0));
    }

    /// <summary>
    /// O(1) の差分更新スライディングウィンドウを用いて、末尾の無音部分をトリミングします。
    /// ゼロアロケーション（Span）と Native SIMD (AVX2) により極限最適化されています。
    /// </summary>
    [ADRAnchor("M-06", nameof(SilenceTrimmer))]
    public static int TrimSilenceFromEnd(byte[] data, int startOffset, int length)
    {
        const int frameSize = 4; // 16bit Stereo = 4 bytes per frame
        int totalFrames = length / frameSize;

        if (totalFrames <= WindowFrames) return length;

        // Zero-Allocation: byte[] を境界チェックなしの short スパンにキャスト
        var pcm = MemoryMarshal.Cast<byte, short>(data.AsSpan(startOffset, length));

        long currentEnergy = 0;
        int windowStartFrame = totalFrames - WindowFrames;

        // 初期ウィンドウのエネルギー計算
        if (Avx2.IsSupported && WindowFrames >= 16)
        {
            currentEnergy = CalculateEnergyAvx2(pcm.Slice(windowStartFrame * 2, WindowFrames * 2));
        }
        else
        {
            for (int i = 0; i < WindowFrames; i++)
            {
                currentEnergy += GetFrameEnergy(pcm, windowStartFrame + i);
            }
        }

        int trimFrames = 0;

        // 後方探索 (O(1) のスライディングウィンドウ)
        while (windowStartFrame > 0)
        {
            if (currentEnergy >= E_threshold)
            {
                break;
            }

            trimFrames++;
            windowStartFrame--;

            // インライン展開される配列直アクセス
            long outEnergy = GetFrameEnergy(pcm, windowStartFrame + WindowFrames);
            long inEnergy = GetFrameEnergy(pcm, windowStartFrame);

            currentEnergy = currentEnergy - outEnergy + inEnergy;
        }

        return (totalFrames - trimFrames) * frameSize;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static long GetFrameEnergy(ReadOnlySpan<short> pcm, int frameIndex)
    {
        // 1 frame = 2 samples (Stereo: L and R)
        int idx = frameIndex * 2;
        long l = pcm[idx];
        long r = pcm[idx + 1];
        return (l * l) + (r * r);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static long CalculateEnergyAvx2(ReadOnlySpan<short> pcm)
    {
        int length = pcm.Length;
        long totalEnergy = 0;

        unsafe
        {
            fixed (short* p = pcm)
            {
                Vector256<long> acc = Vector256<long>.Zero;
                int i = 0;

                // Process 16 samples (8 frames) per loop iteration
                for (; i <= length - 16; i += 16)
                {
                    // Load 16 short samples: [L0, R0, L1, R1, L2, R2, L3, R3, L4, R4, L5, R5, L6, R6, L7, R7]
                    Vector256<short> v = Avx2.LoadVector256(p + i);

                    // MultiplyAddAdjacent (_mm256_madd_epi16)
                    // Multiplies adjacent pairs and adds them:
                    // Res[0] = L0*L0 + R0*R0 (32-bit int)
                    // Res[1] = L1*L1 + R1*R1 (32-bit int)
                    Vector256<int> mad = Avx2.MultiplyAddAdjacent(v, v);

                    // Convert 32-bit int results to 64-bit long
                    Vector256<long> madLow = Avx2.ConvertToVector256Int64(mad.GetLower());
                    Vector256<long> madHigh = Avx2.ConvertToVector256Int64(mad.GetUpper());

                    acc = Avx2.Add(acc, madLow);
                    acc = Avx2.Add(acc, madHigh);
                }

                totalEnergy += Vector256.Sum(acc);

                // Handle remaining samples (if any)
                for (; i < length; i += 2)
                {
                    long l = p[i];
                    long r = p[i + 1];
                    totalEnergy += (l * l) + (r * r);
                }
            }
        }

        return totalEnergy;
    }
}
