#nullable disable
using System.Globalization;
using System.Windows.Data;
using BmsAtelierKyokufu.BmsPartTuner.UI.Converters;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.UI.Converters;

/// <summary>
/// <see cref="ObjectToBoolConverterTests"/> の動作を検証するテストクラス。
/// </summary>
public class ObjectToBoolConverterTests
{
    private readonly ObjectToBoolConverter _converter = new();

    /// <summary>
    /// Convert の動作を検証します。
    /// </summary>
    [Theory]
    [InlineData(null, false)]
    [InlineData("valid_string", true)]
    [InlineData(42, true)]
    public void Convert_ReturnsExpectedBool(object input, bool expected)
    {
        var result = _converter.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);
        Assert.Equal(expected, (bool)result);
    }

    /// <summary>
    /// ConvertBack の動作を検証します。
    /// </summary>
    [Fact]
    public void ConvertBack_WithFalse_ReturnsNull()
    {
        var result = _converter.ConvertBack(false, typeof(object), null, CultureInfo.InvariantCulture);
        Assert.Null(result);
    }

    /// <summary>
    /// ConvertBack において、条件 WithTrueOrNonBool の場合に ReturnsBindingDoNothing されることを検証します。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData("invalid")]
    public void ConvertBack_WithTrueOrNonBool_ReturnsBindingDoNothing(object input)
    {
        var result = _converter.ConvertBack(input, typeof(object), null, CultureInfo.InvariantCulture);
        Assert.Equal(Binding.DoNothing, result);
    }
}
