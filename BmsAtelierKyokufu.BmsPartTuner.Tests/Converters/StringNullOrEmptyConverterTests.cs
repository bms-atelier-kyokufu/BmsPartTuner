using System.Globalization;
using System.Windows;
using BmsAtelierKyokufu.BmsPartTuner.Converters;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Converters;

public class StringNullOrEmptyConverterTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(" ", false)] // " " is not empty
    [InlineData("test", false)]
    public void Convert_ReturnsExpectedResult(string? value, bool expected)
    {
        // Arrange
        var converter = StringNullOrEmptyConverter.Instance;

        // Act
        var result = converter.Convert(value!, typeof(bool), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertBack_ReturnsUnsetValue()
    {
        // Arrange
        var converter = StringNullOrEmptyConverter.Instance;

        // Act
        var result = converter.ConvertBack(true, typeof(string), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(DependencyProperty.UnsetValue, result);
    }
}
