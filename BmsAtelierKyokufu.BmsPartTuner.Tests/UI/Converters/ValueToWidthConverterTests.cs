using System.Globalization;
using System.Windows.Data;
using BmsAtelierKyokufu.BmsPartTuner.UI.Converters;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.UI.Converters;

/// <summary>
/// <see cref="ValueToWidthConverterTests"/> の動作を検証するテストクラス。
/// </summary>
public class ValueToWidthConverterTests
{
    private readonly ValueToWidthConverter _converter = new();

    /// <summary>
    /// Convert の動作を検証します。
    /// </summary>
    [Theory]
    [InlineData(0.75, 200.0, 150.0)]
    [InlineData(1.5, 100.0, 100.0)] // Value > 1.0, should be clamped to 1.0
    [InlineData(-0.5, 100.0, 0.0)] // Value < 0.0, should be clamped to 0.0
    [InlineData("invalid", 100.0, 0.0)] // Invalid input should return 0.0
    public void Convert_ReturnsExpectedWidth(object value, object maxWidth, double expected)
    {
        object[] values = [value, maxWidth];
        object result = _converter.Convert(values, null!, null!, CultureInfo.InvariantCulture);
        
        Assert.IsType<double>(result);
        Assert.Equal(expected, (double)result, 2);
    }

    /// <summary>
    /// ConvertBack において ReturnsDoNothing の場合の挙動を検証します。
    /// </summary>
    [Fact]
    public void ConvertBack_ReturnsDoNothing()
    {
        // Arrange
        Type[] targetTypes = [typeof(double), typeof(double)];
        object value = 100.0;

        // Act
        object[] result = _converter.ConvertBack(value, targetTypes, null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(targetTypes.Length, result.Length);
        foreach (var item in result)
        {
            Assert.Equal(Binding.DoNothing, item);
        }
    }
}
