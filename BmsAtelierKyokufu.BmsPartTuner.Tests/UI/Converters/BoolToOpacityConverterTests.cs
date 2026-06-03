using System.Globalization;
using System.Windows.Data;
using BmsAtelierKyokufu.BmsPartTuner.UI.Converters;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.UI.Converters;

/// <summary>
/// <see cref="BoolToOpacityConverter"/> のテストクラス。
/// </summary>
public class BoolToOpacityConverterTests
{
    private readonly BoolToOpacityConverter _converter = new();
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

    /// <summary>
    /// Convert において、条件 True の場合に Returns0 されることを検証します。
    /// </summary>
    [Fact]
    public void Convert_True_Returns0_5()
    {
        // Act
        var result = _converter.Convert(true, typeof(double), null!, _culture);

        // Assert
        Assert.Equal(0.5, result);
    }

    /// <summary>
    /// Convert において、条件 False の場合に Returns1 されることを検証します。
    /// </summary>
    [Fact]
    public void Convert_False_Returns1_0()
    {
        // Act
        var result = _converter.Convert(false, typeof(double), null!, _culture);

        // Assert
        Assert.Equal(1.0, result);
    }

    /// <summary>
    /// Convert において、条件 NonBool の場合に Returns1 されることを検証します。
    /// </summary>
    [Fact]
    public void Convert_NonBool_Returns1_0()
    {
        // Act
        var result = _converter.Convert("invalid", typeof(double), null!, _culture);

        // Assert
        Assert.Equal(1.0, result);
    }

    /// <summary>
    /// ConvertBack において、条件 0 の場合に 5 されることを検証します。
    /// </summary>
    [Fact]
    public void ConvertBack_0_5_ReturnsTrue()
    {
        // Act
        var result = _converter.ConvertBack(0.5, typeof(bool), null!, _culture);

        // Assert
        Assert.IsType<bool>(result);
        Assert.True((bool)result);
    }

    /// <summary>
    /// ConvertBack において、条件 CloseTo0 の場合に 5 されることを検証します。
    /// </summary>
    [Fact]
    public void ConvertBack_CloseTo0_5_ReturnsTrue()
    {
        // Arrange
        // 0.5との差が0.01未満ならtrueとする想定
        const double input = 0.5000001;

        // Act
        var result = _converter.ConvertBack(input, typeof(bool), null!, _culture);

        // Assert
        Assert.True((bool)result);
    }

    /// <summary>
    /// ConvertBack において、条件 1 の場合に 0 されることを検証します。
    /// </summary>
    [Fact]
    public void ConvertBack_1_0_ReturnsFalse()
    {
        // Act
        var result = _converter.ConvertBack(1.0, typeof(bool), null!, _culture);

        // Assert
        Assert.False((bool)result);
    }

    /// <summary>
    /// ConvertBack において、条件 OtherValue の場合に ReturnsFalse されることを検証します。
    /// </summary>
    [Fact]
    public void ConvertBack_OtherValue_ReturnsFalse()
    {
        // Arrange
        const double input = 0.0; // 0.5以外はfalse (通常表示 = 1.0 = false)

        // Act
        var result = _converter.ConvertBack(input, typeof(bool), null!, _culture);

        // Assert
        Assert.False((bool)result);
    }

    /// <summary>
    /// ConvertBack において、条件 NonDouble の場合に ReturnsBindingDoNothing されることを検証します。
    /// </summary>
    [Fact]
    public void ConvertBack_NonDouble_ReturnsBindingDoNothing()
    {
        // Act
        var result = _converter.ConvertBack("invalid", typeof(bool), null!, _culture);

        // Assert
        Assert.Equal(Binding.DoNothing, result);
    }
}
