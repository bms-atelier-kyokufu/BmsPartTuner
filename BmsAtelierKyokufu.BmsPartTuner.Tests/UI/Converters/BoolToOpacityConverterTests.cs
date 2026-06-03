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
    /// Convert の動作を検証します。
    /// </summary>
    [Theory]
    [InlineData(true, 0.5)]
    [InlineData(false, 1.0)]
    [InlineData("invalid", 1.0)]
    public void Convert_ReturnsExpectedDouble(object input, double expected)
    {
        var result = _converter.Convert(input, typeof(double), null!, _culture);
        Assert.Equal(expected, (double)result);
    }

    /// <summary>
    /// ConvertBack の動作を検証します。
    /// </summary>
    [Theory]
    [InlineData(0.5, true)]
    [InlineData(0.5000001, true)]
    [InlineData(1.0, false)]
    [InlineData(0.0, false)]
    public void ConvertBack_ReturnsExpectedBool(double input, bool expected)
    {
        var result = _converter.ConvertBack(input, typeof(bool), null!, _culture);
        Assert.IsType<bool>(result);
        Assert.Equal(expected, (bool)result);
    }

    /// <summary>
    /// ConvertBack において、条件 NonDouble の場合に ReturnsBindingDoNothing されることを検証します。
    /// </summary>
    [Fact]
    public void ConvertBack_NonDouble_ReturnsBindingDoNothing()
    {
        var result = _converter.ConvertBack("invalid", typeof(bool), null!, _culture);
        Assert.Equal(Binding.DoNothing, result);
    }
}
