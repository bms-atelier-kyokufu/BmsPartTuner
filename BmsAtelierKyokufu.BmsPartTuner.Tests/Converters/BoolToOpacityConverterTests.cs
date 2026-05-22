using System.Globalization;
using System.Windows.Data;
using BmsAtelierKyokufu.BmsPartTuner.Converters;
using BmsAtelierKyokufu.BmsPartTuner.Models;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Converters;

/// <summary>
/// <see cref="BoolToOpacityConverter"/> のテストクラス。
/// </summary>
public class BoolToOpacityConverterTests
{
    private readonly BoolToOpacityConverter _converter = new();
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

    [Fact]
    public void Convert_True_Returns0_5()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        // Act
        var result = _converter.Convert(true, typeof(double), null!, _culture);

        // Assert
        Assert.Equal(0.5, result);
    }

    [Fact]
    public void Convert_False_Returns1_0()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        // Act
        var result = _converter.Convert(false, typeof(double), null!, _culture);

        // Assert
        Assert.Equal(1.0, result);
    }

    [Fact]
    public void Convert_NonBool_Returns1_0()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        // Act
        var result = _converter.Convert("invalid", typeof(double), null!, _culture);

        // Assert
        Assert.Equal(1.0, result);
    }

    [Fact]
    public void ConvertBack_0_5_ReturnsTrue()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        // Act
        var result = _converter.ConvertBack(0.5, typeof(bool), null!, _culture);

        // Assert
        Assert.IsType<bool>(result);
        Assert.True((bool)result);
    }

    [Fact]
    public void ConvertBack_CloseTo0_5_ReturnsTrue()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        // Arrange
        // 0.5との差が0.01未満ならtrueとする想定
        var input = 0.5000001;

        // Act
        var result = _converter.ConvertBack(input, typeof(bool), null!, _culture);

        // Assert
        Assert.True((bool)result);
    }

    [Fact]
    public void ConvertBack_1_0_ReturnsFalse()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        // Act
        var result = _converter.ConvertBack(1.0, typeof(bool), null!, _culture);

        // Assert
        Assert.False((bool)result);
    }

    [Fact]
    public void ConvertBack_OtherValue_ReturnsFalse()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        // Arrange
        var input = 0.0; // 0.5以外はfalse (通常表示 = 1.0 = false)

        // Act
        var result = _converter.ConvertBack(input, typeof(bool), null!, _culture);

        // Assert
        Assert.False((bool)result);
    }

    [Fact]
    public void ConvertBack_NonDouble_ReturnsBindingDoNothing()
    {
        _ = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
        // Act
        var result = _converter.ConvertBack("invalid", typeof(bool), null!, _culture);

        // Assert
        Assert.Equal(Binding.DoNothing, result);
    }
}
