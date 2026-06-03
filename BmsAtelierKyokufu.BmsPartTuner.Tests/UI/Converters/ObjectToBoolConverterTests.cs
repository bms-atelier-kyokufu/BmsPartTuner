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
    /// Convert において、条件 WithNull の場合に ReturnsFalse されることを検証します。
    /// </summary>
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

    /// <summary>
    /// Convert において、条件 WithObject の場合に ReturnsTrue されることを検証します。
    /// </summary>
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

    /// <summary>
    /// ConvertBack において、条件 WithFalse の場合に ReturnsNull されることを検証します。
    /// </summary>
    [Fact]
    public void ConvertBack_WithFalse_ReturnsNull()
    {
        // Arrange
        const bool value = false;

        // Act
        var result = _converter.ConvertBack(value, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// ConvertBack において、条件 WithTrue の場合に ReturnsBindingDoNothing されることを検証します。
    /// </summary>
    [Fact]
    public void ConvertBack_WithTrue_ReturnsBindingDoNothing()
    {
        // Arrange
        const bool value = true;

        // Act
        var result = _converter.ConvertBack(value, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(Binding.DoNothing, result);
    }

    /// <summary>
    /// ConvertBack において、条件 WithNonBool の場合に ReturnsBindingDoNothing されることを検証します。
    /// </summary>
    [Fact]
    public void ConvertBack_WithNonBool_ReturnsBindingDoNothing()
    {
        // Arrange
        const string value = "invalid";

        // Act
        var result = _converter.ConvertBack(value, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(Binding.DoNothing, result);
    }
}
