using System.Globalization;
using BmsAtelierKyokufu.BmsPartTuner.Converters;
using BmsAtelierKyokufu.BmsPartTuner.Core;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Converters;

/// <summary>
/// <see cref="CorrelationCoefficientConverter"/> のテストクラス。
/// 
/// 【テスト対象】
/// - Convert: 内部値(0.00-1.00) → 表示値(0-100)
/// - ConvertBack: 表示値(0-100) → 内部値(0.00-1.00)
/// 
/// 【テスト設計方針】
/// - 境界値分析: 0, 1, 100 などの境界値
/// - 同値分割: 正常値、異常値、エッジケース
/// - 型の多様性: string, float, double の入力対応
/// </summary>
public class CorrelationCoefficientConverterTests
{
    private readonly CorrelationCoefficientConverter _converter = new();
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

    #region Convert Tests - 内部値から表示値への変換

    [Theory]
    [InlineData("0.95", "95")]
    [InlineData("0.00", "0")]
    [InlineData("1.00", "100")]
    [InlineData("0.50", "50")]
    [InlineData("0.01", "1")]
    [InlineData("0.99", "99")]
    public void Convert_StringInternalValue_ReturnsCorrectDisplayValue(string input, string expected)
    {
        // Act
        var result = _converter.Convert(input, typeof(string), null!, _culture);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0.95f, "95")]
    [InlineData(0.00f, "0")]
    [InlineData(1.00f, "100")]
    [InlineData(0.50f, "50")]
    [InlineData(0.01f, "1")]
    [InlineData(0.99f, "99")]
    [InlineData(0.005f, "0")]   // 浮動小数点精度により0.00499...となり、四捨五入で0
    [InlineData(0.004f, "0")]   // 四捨五入で0になる
    [InlineData(0.015f, "2")]   // 四捨五入で2になる（1.5に近い）
    public void Convert_FloatInternalValue_ReturnsCorrectDisplayValue(float input, string expected)
    {
        // Act
        var result = _converter.Convert(input, typeof(string), null!, _culture);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0.95, "95")]
    [InlineData(0.00, "0")]
    [InlineData(1.00, "100")]
    [InlineData(0.50, "50")]
    [InlineData(0.956, "96")]  // 四捨五入
    [InlineData(0.954, "95")]  // 四捨五入
    public void Convert_DoubleInternalValue_ReturnsCorrectDisplayValue(double input, string expected)
    {
        // Act
        var result = _converter.Convert(input, typeof(string), null!, _culture);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0.955", "96")]  // 0.955 → 95.5 → 96 (四捨五入)
    [InlineData("0.944", "94")]  // 0.944 → 94.4 → 94 (四捨五入)
    public void Convert_StringWithRounding_RoundsCorrectly(string input, string expected)
    {
        // Act
        var result = _converter.Convert(input, typeof(string), null!, _culture);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_NullValue_ReturnsEmptyString()
    {
        // Act
        var result = _converter.Convert(null!, typeof(string), null!, _culture);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Convert_InvalidString_ReturnsOriginalString()
    {
        // Arrange
        var invalidInput = "not-a-number";

        // Act
        var result = _converter.Convert(invalidInput, typeof(string), null!, _culture);

        // Assert
        Assert.Equal(invalidInput, result);
    }

    [Fact]
    public void Convert_IntegerValue_ReturnsToString()
    {
        // Arrange
        int intValue = 42;

        // Act
        var result = _converter.Convert(intValue, typeof(string), null!, _culture);

        // Assert
        Assert.Equal("42", result);
    }

    #endregion

    #region ConvertBack Tests - 表示値から内部値への変換

    [Theory]
    [InlineData("95", "0.95")]
    [InlineData("0", "0.00")]
    [InlineData("100", "1.00")]
    [InlineData("50", "0.50")]
    [InlineData("1", "0.01")]
    [InlineData("99", "0.99")]
    public void ConvertBack_ValidDisplayValue_ReturnsCorrectInternalValue(string input, string expected)
    {
        // Act
        var result = _converter.ConvertBack(input, typeof(string), null!, _culture);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ConvertBack_EmptyOrWhitespace_ReturnsDefaultValue(string? input)
    {
        // Arrange
        var expectedDefault = AppConstants.Threshold.Default.ToString("F2");

        // Act
        var result = _converter.ConvertBack(input!, typeof(string), null!, _culture);

        // Assert
        Assert.Equal(expectedDefault, result);
    }

    [Theory]
    [InlineData("-10", "0.00")]   // 負の値は0にクランプ
    [InlineData("-1", "0.00")]    // 負の値は0にクランプ
    [InlineData("150", "1.00")]   // 100超は100にクランプ
    [InlineData("999", "1.00")]   // 100超は100にクランプ
    public void ConvertBack_OutOfRangeValue_ClampsToValidRange(string input, string expected)
    {
        // Act
        var result = _converter.ConvertBack(input, typeof(string), null!, _culture);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0.95", "0.95")]  // 既に0-1形式
    [InlineData("0.50", "0.50")]  // 既に0-1形式
    [InlineData("0.00", "0.00")]  // 既に0-1形式
    [InlineData("1.00", "1.00")]  // 既に0-1形式
    public void ConvertBack_AlreadyInternalFormat_PreservesValue(string input, string expected)
    {
        // Act
        var result = _converter.ConvertBack(input, typeof(string), null!, _culture);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("50.5", "0.50")]   // 小数の表示値（1-100スケール）→ 100で割って切り捨て
    [InlineData("75.25", "0.75")]  // 小数の表示値（1-100スケール）→ 100で割る
    public void ConvertBack_DecimalDisplayValue_DividesBy100(string input, string expected)
    {
        // Act
        var result = _converter.ConvertBack(input, typeof(string), null!, _culture);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertBack_InvalidString_ReturnsDefaultValue()
    {
        // Arrange
        var invalidInput = "not-a-number";
        var expectedDefault = AppConstants.Threshold.Default.ToString("F2");

        // Act
        var result = _converter.ConvertBack(invalidInput, typeof(string), null!, _culture);

        // Assert
        Assert.Equal(expectedDefault, result);
    }

    [Fact]
    public void ConvertBack_NonStringValue_ReturnsDefaultValue()
    {
        // Arrange
        int nonStringValue = 42;
        var expectedDefault = AppConstants.Threshold.Default.ToString("F2");

        // Act
        var result = _converter.ConvertBack(nonStringValue, typeof(string), null!, _culture);

        // Assert
        Assert.Equal(expectedDefault, result);
    }

    #endregion

    #region Roundtrip Tests - 往復変換の一貫性

    [Theory]
    [InlineData("0.95")]
    [InlineData("0.50")]
    [InlineData("0.00")]
    [InlineData("1.00")]
    [InlineData("0.75")]
    public void Roundtrip_ConvertThenConvertBack_PreservesValue(string originalInternal)
    {
        // Act
        var displayValue = _converter.Convert(originalInternal, typeof(string), null!, _culture);
        var roundtripInternal = _converter.ConvertBack(displayValue, typeof(string), null!, _culture);

        // Assert
        Assert.Equal(originalInternal, roundtripInternal);
    }

    [Theory]
    [InlineData("95")]
    [InlineData("50")]
    [InlineData("0")]
    [InlineData("100")]
    [InlineData("75")]
    public void Roundtrip_ConvertBackThenConvert_PreservesValue(string originalDisplay)
    {
        // Act
        var internalValue = _converter.ConvertBack(originalDisplay, typeof(string), null!, _culture);
        var roundtripDisplay = _converter.Convert(internalValue, typeof(string), null!, _culture);

        // Assert
        Assert.Equal(originalDisplay, roundtripDisplay);
    }

    #endregion

    #region Culture Invariance Tests - カルチャ独立性

    [Theory]
    [InlineData("de-DE")]  // ドイツ（小数点にカンマを使用）
    [InlineData("fr-FR")]  // フランス
    [InlineData("ja-JP")]  // 日本
    [InlineData("en-US")]  // アメリカ
    public void Convert_DifferentCultures_ProducesSameResult(string cultureName)
    {
        // Arrange
        var culture = new CultureInfo(cultureName);
        var input = 0.95f;

        // Act
        var result = _converter.Convert(input, typeof(string), null!, culture);

        // Assert
        Assert.Equal("95", result);
    }

    #endregion

    #region Edge Cases - エッジケース

    [Theory]
    [InlineData("0.001", "0")]     // 非常に小さい値
    [InlineData("0.999", "100")]   // 非常に大きい値（1に近い）
    public void Convert_ExtremeValues_HandlesCorrectly(string input, string expected)
    {
        // Act
        var result = _converter.Convert(input, typeof(string), null!, _culture);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertBack_VeryLongDecimal_HandlesCorrectly()
    {
        // Arrange
        var input = "0.123456789";  // 非常に長い小数

        // Act
        var result = _converter.ConvertBack(input, typeof(string), null!, _culture);

        // Assert
        Assert.Equal("0.12", result);  // 0-1の範囲なのでそのまま使用、F2でフォーマット
    }

    [Theory]
    [InlineData("  95  ", "0.95")]  // 前後の空白
    public void ConvertBack_StringWithWhitespace_TrimsAndConverts(string input, string expected)
    {
        // Act
        var result = _converter.ConvertBack(input, typeof(string), null!, _culture);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion
}
