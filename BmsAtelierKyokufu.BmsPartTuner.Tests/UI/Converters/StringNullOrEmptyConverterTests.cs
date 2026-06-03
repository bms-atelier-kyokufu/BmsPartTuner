using System.Globalization;
using System.Windows;
using BmsAtelierKyokufu.BmsPartTuner.UI.Converters;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.UI.Converters;

/// <summary>
/// <see cref="StringNullOrEmptyConverterTests"/> の動作を検証するテストクラス。
/// </summary>
public class StringNullOrEmptyConverterTests
{
    /// <summary>
    /// テスト を検証します。
    /// </summary>
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

    /// <summary>
    /// ConvertBack において ReturnsUnsetValue の場合の挙動を検証します。
    /// </summary>
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
