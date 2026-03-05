#nullable disable
using System.Globalization;
using System.Windows.Data;
using BmsAtelierKyokufu.BmsPartTuner.Converters;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Converters;

public class ObjectToBoolConverterTests
{
    private readonly ObjectToBoolConverter _converter = new();

    [Fact]
    public void Convert_WithNull_ReturnsFalse()
    {
        // Arrange
        object value = null;

        // Act
        var result = _converter.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.False((bool)result);
    }

    [Fact]
    public void Convert_WithObject_ReturnsTrue()
    {
        // Arrange
        var value = new object();

        // Act
        var result = _converter.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.True((bool)result);
    }

    [Fact]
    public void ConvertBack_WithFalse_ReturnsNull()
    {
        // Arrange
        var value = false;

        // Act
        var result = _converter.ConvertBack(value, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ConvertBack_WithTrue_ReturnsBindingDoNothing()
    {
        // Arrange
        var value = true;

        // Act
        var result = _converter.ConvertBack(value, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(Binding.DoNothing, result);
    }

    [Fact]
    public void ConvertBack_WithNonBool_ReturnsBindingDoNothing()
    {
        // Arrange
        var value = "invalid";

        // Act
        var result = _converter.ConvertBack(value, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(Binding.DoNothing, result);
    }
}
