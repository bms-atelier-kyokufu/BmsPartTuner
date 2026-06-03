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
    /// Convert において、条件 WithValidInputs の場合に ReturnsCalculatedWidth されることを検証します。
    /// </summary>
    [Fact]
    public void Convert_WithValidInputs_ReturnsCalculatedWidth()
    {
        // Arrange
        object[] values = [0.75, 200.0];
        const double expected = 150.0;

        // Act
        object result = _converter.Convert(values, null!, null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<double>(result);
        Assert.Equal(expected, (double)result, 2);
    }

    /// <summary>
    /// Convert において、条件 WithClampedValue の場合に ReturnsClampedWidth されることを検証します。
    /// </summary>
    [Fact]
    public void Convert_WithClampedValue_ReturnsClampedWidth()
    {
        // Arrange
        object[] values = [1.5, 100.0]; // Value > 1.0, should be clamped to 1.0
        double expected = 100.0;

        // Act
        object result = _converter.Convert(values, null!, null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(expected, (double)result, 2);

        // Arrange
        values = [-0.5, 100.0]; // Value < 0.0, should be clamped to 0.0
        expected = 0.0;

        // Act
        result = _converter.Convert(values, null!, null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(expected, (double)result, 2);
    }

    /// <summary>
    /// Convert において、条件 WithInvalidInputs の場合に ReturnsZero されることを検証します。
    /// </summary>
    [Fact]
    public void Convert_WithInvalidInputs_ReturnsZero()
    {
        // Arrange
        object[] values = ["invalid", 100.0];

        // Act
        object result = _converter.Convert(values, null!, null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(0.0, result);
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
