namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson;

using System.Buffers.Binary;

/// <summary>
/// キャッシュされた PCM データから、波形比較等の最適化に必要な
/// プレフィックス和や LSH (Locality Sensitive Hashing) シグネチャを計算するヘルパークラス。
/// </summary>
internal static class AudioOptimizationDecoder
{
    public static BaseAudioOptimizationData DecodeAllData(byte[] rawBytes, int pcmOffset, int pcmLength)
    {
        int frames = pcmLength / 4; // 16bit stereo = 4 bytes per frame
        float[][] samples = [new float[frames], new float[frames]];

        // Prefix sums need L + 1 length
        double[][] prefixSum = [new double[frames + 1], new double[frames + 1]];
        double[][] prefixSumSq = [new double[frames + 1], new double[frames + 1]];

        // LSH arrays (1 ulong per 64 frames)
        int lshLength = (frames + 63) / 64;
        ulong[][] signLsh = [new ulong[lshLength], new ulong[lshLength]];
        ulong[][] signLshMask = [new ulong[lshLength], new ulong[lshLength]];

        ReadOnlySpan<byte> data = new(rawBytes, pcmOffset, pcmLength);

        // RMS threshold for LSH mask (dbThreshold = -45.0)
        const float silenceThreshold = 0.0056234f; // 10^(-45/20)

        for (int i = 0; i < frames; i++)
        {
            short l = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(i * 4, 2));
            short r = BinaryPrimitives.ReadInt16LittleEndian(data.Slice((i * 4) + 2, 2));

            float fl = l / 32768f;
            float fr = r / 32768f;

            samples[0][i] = fl;
            samples[1][i] = fr;

            // 1. Prefix Sums (offset by 1)
            prefixSum[0][i + 1] = prefixSum[0][i] + fl;
            prefixSum[1][i + 1] = prefixSum[1][i] + fr;

            prefixSumSq[0][i + 1] = prefixSumSq[0][i] + (fl * fl);
            prefixSumSq[1][i + 1] = prefixSumSq[1][i] + (fr * fr);

            // 2. LSH (every 64 frames = 1 ulong)
            int lshIdx = i / 64;
            int bitShift = i % 64;

            // Left channel
            if (fl >= 0) signLsh[0][lshIdx] |= 1UL << bitShift;
            if (Math.Abs(fl) >= silenceThreshold) signLshMask[0][lshIdx] |= 1UL << bitShift;

            // Right channel
            if (fr >= 0) signLsh[1][lshIdx] |= 1UL << bitShift;
            if (Math.Abs(fr) >= silenceThreshold) signLshMask[1][lshIdx] |= 1UL << bitShift;
        }

        return new BaseAudioOptimizationData(samples, prefixSum, prefixSumSq, signLsh, signLshMask);
    }
}
