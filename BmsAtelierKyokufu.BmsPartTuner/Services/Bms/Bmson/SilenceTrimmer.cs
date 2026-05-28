using BmsAtelierKyokufu.BmsPartTuner.Core.Attributes;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.Bms.Bmson;

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
    /// </summary>
    [ADRAnchor("M-06", nameof(SilenceTrimmer))]
    public static int TrimSilenceFromEnd(byte[] data, int startOffset, int length)
    {
        const int frameSize = 4; // 16bit Stereo = 4 bytes per frame
        int totalFrames = length / frameSize;

        // ウィンドウサイズより短い場合はトリミングしない（安全策）
        if (totalFrames <= WindowFrames) return length;

        // 初期ウィンドウ (末尾の WindowFrames フレーム) のエネルギーを計算 (O(N))
        long currentEnergy = 0;
        int windowStartFrame = totalFrames - WindowFrames;

        for (int i = 0; i < WindowFrames; i++)
        {
            currentEnergy += GetFrameEnergy(data, startOffset + ((windowStartFrame + i) * frameSize));
        }

        int trimFrames = 0;

        // 後方探索 (O(1) のスライディングウィンドウ)
        while (windowStartFrame > 0)
        {
            if (currentEnergy >= E_threshold)
            {
                // 音が見つかった。この時点での末尾は windowStartFrame + WindowFrames となる。
                break;
            }

            trimFrames++;
            windowStartFrame--;

            // ウィンドウから外れるフレームのエネルギーを引き、新しく入るフレームのエネルギーを足す
            long outEnergy = GetFrameEnergy(data, startOffset + ((windowStartFrame + WindowFrames) * frameSize));
            long inEnergy = GetFrameEnergy(data, startOffset + (windowStartFrame * frameSize));

            currentEnergy = currentEnergy - outEnergy + inEnergy;
        }

        int newTotalFrames = totalFrames - trimFrames;
        return newTotalFrames * frameSize;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static long GetFrameEnergy(byte[] data, int offset)
    {
        short l = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, 2));
        short r = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 2, 2));
        return ((long)l * l) + ((long)r * r);
    }
}
