using System.Globalization;
using System.Windows.Data;
using BmsAtelierKyokufu.BmsPartTuner.Converters;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Converters;

public class ValueToWidthConverterTests
{
    private readonly ValueToWidthConverter _converter = new();

    [Fact]
    public void Convert_WithValidInputs_ReturnsCalculatedWidth()
    {
        // Arrange
        object[] values = new object[] { 0.75, 200.0 };
        double expected = 150.0;

        // Act
        object result = _converter.Convert(values, null!, null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<double>(result);
        Assert.Equal(expected, (double)result, 2);
    }

    [Fact]
    public void Convert_WithClampedValue_ReturnsClampedWidth()
    {
        // Arrange
        object[] values = new object[] { 1.5, 100.0 }; // Value > 1.0, should be clamped to 1.0
        double expected = 100.0;

        // Act
        object result = _converter.Convert(values, null!, null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(expected, (double)result, 2);

        // Arrange
        values = new object[] { -0.5, 100.0 }; // Value < 0.0, should be clamped to 0.0
        expected = 0.0;

        // Act
        result = _converter.Convert(values, null!, null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(expected, (double)result, 2);
    }

    [Fact]
    public void Convert_WithInvalidInputs_ReturnsZero()
    {
        // Arrange
        object[] values = new object[] { "invalid", 100.0 };

        // Act
        object result = _converter.Convert(values, null!, null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void ConvertBack_ReturnsDoNothing()
    {
        // Arrange
        Type[] targetTypes = new Type[] { typeof(double), typeof(double) };
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
