using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Audio;

public class FftAlignmentEngineTests
{
    private static float[] GenerateSineWave(int length, float frequency, float sampleRate)
    {
        float[] wave = new float[length];
        for (int i = 0; i < length; i++)
        {
            wave[i] = (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate);
        }
        return wave;
    }

    [Fact]
    public void CalculateAlignmentOffset_IdenticalSignals_ReturnsZero()
    {
        // Arrange
        var shorter = GenerateSineWave(2048, 440f, 44100f);
        var longer = GenerateSineWave(2048, 440f, 44100f);

        // Act
        int offset = FftAlignmentEngine.CalculateAlignmentOffset(shorter, longer);

        // Assert
        Assert.Equal(0, offset);
    }

    [Fact]
    public void CalculateAlignmentOffset_DelayedSignal_ReturnsNegativeOffset()
    {
        // Arrange
        // shorter: 元の波形
        // longer: 10サンプル遅れた波形（longer[10] から shorter[0] が始まる）
        var shorter = GenerateSineWave(2048, 440f, 44100f);
        var longer = new float[2048];
        Array.Copy(shorter, 0, longer, 10, 2048 - 10);

        // Act
        int offset = FftAlignmentEngine.CalculateAlignmentOffset(shorter, longer);

        // Assert
        // shorter[i] が longer[i + 10] と一致するため、ズレは -10 となるはず
        Assert.Equal(-10, offset);
    }

    [Fact]
    public void CalculateAlignmentOffset_AdvancedSignal_ReturnsPositiveOffset()
    {
        // Arrange
        // shorter: 元の波形
        // longer: 15サンプル進んだ波形（longer[0] から shorter[15] が始まる）
        var shorter = GenerateSineWave(2048, 440f, 44100f);
        var longer = new float[2048];
        Array.Copy(shorter, 15, longer, 0, 2048 - 15);

        // Act
        int offset = FftAlignmentEngine.CalculateAlignmentOffset(shorter, longer);

        // Assert
        // shorter[i] が longer[i - 15] と一致するため、ズレは 15 となるはず
        Assert.Equal(15, offset);
    }
}
