using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Optimization;

/// <summary>
/// BmsOptimizationService のバリデーション（定義範囲、R2閾値）に関するテスト。
/// </summary>
public class BmsOptimizationServiceValidationTests
{
    private readonly BmsOptimizationService _service;

    public BmsOptimizationServiceValidationTests()
    {
        _service = new BmsOptimizationService();
    }

    /// <summary>
    /// ValidateDefinitionRange において、条件 ValidInputs の場合に ReturnsSuccess されることを検証します。
    /// </summary>
    [Theory]
    [InlineData("01", "02", true)]  // Valid: 1 to 2
    [InlineData("01", "10", true)]  // Valid: 1 to 16
    [InlineData("01", "ZZ", true)]  // Valid: 1 to 1295 (Base36 max)
    public void ValidateDefinitionRange_ValidInputs_ReturnsSuccess(string start, string end, bool expectedValid)
    {
        var result = _service.ValidateDefinitionRange(start, end);

        Assert.Equal(expectedValid, result.IsValid);
        if (!result.IsValid)
        {
            Assert.NotEmpty(result.Errors);
        }
    }

    /// <summary>
    /// テスト を検証します。
    /// </summary>
    [Theory]
    [InlineData("", "10")]          // Empty start
    [InlineData("01", "")]          // Empty end
    [InlineData("10", "01")]        // Start > End
    [InlineData("00", "10")]        // Start < 1
    [InlineData("01", "ZZZ")]       // End > MaxNumberBase62
    [InlineData("ABC", "10")]       // Invalid characters
    public void ValidateDefinitionRange_InvalidInputs_ReturnsError(string start, string end)
    {
        var result = _service.ValidateDefinitionRange(start, end);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    /// <summary>
    /// テスト を検証します。
    /// </summary>
    [Theory]
    [InlineData("80", 0.80f)]       // Percentage format
    [InlineData("0.8", 0.80f)]      // Decimal format
    [InlineData("100", 1.0f)]       // Max percentage
    [InlineData("1.0", 1.0f)]       // Max decimal
    [InlineData("0", 0.0f)]         // Min value
    public void ValidateR2Threshold_ValidInputs_ReturnsSuccess(string input, float expectedValue)
    {
        var result = _service.ValidateR2Threshold(input);

        Assert.True(result.IsValid);
        Assert.Equal(expectedValue, result.Value, precision: 2);
    }

    /// <summary>
    /// テスト を検証します。
    /// </summary>
    [Theory]
    [InlineData("")]                // Empty
    [InlineData("abc")]             // Non-numeric
    [InlineData("-10")]             // Negative
    [InlineData("150")]             // > 100 (max percentage)
    public void ValidateR2Threshold_InvalidInputs_ReturnsError(string input)
    {
        var result = _service.ValidateR2Threshold(input);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    /// <summary>
    /// Base36の境界値テスト（ZZ = 1295）。
    /// </summary>
    [Theory]
    [InlineData("01", "ZZ", true)]   // 1 to 1295 (Base36 max)
    [InlineData("ZY", "ZZ", true)]   // Near max range
    [InlineData("01", "100", false)] // "100" exceeds Base36 limit (1296 > 1295)
    public void ValidateDefinitionRange_Base36Boundary_ValidatesCorrectly(string start, string end, bool expectedValid)
    {
        var result = _service.ValidateDefinitionRange(start, end);

        Assert.Equal(expectedValid, result.IsValid);
    }

    /// <summary>
    /// R2しきい値の境界値テスト。
    /// </summary>
    [Theory]
    [InlineData("0", 0.0f)]          // 最小値
    [InlineData("0.01", 0.01f)]      // 最小有効値付近
    [InlineData("0.99", 0.99f)]      // 最大有効値付近
    [InlineData("1", 1.0f)]          // 最大値（パーセンテージ形式）
    [InlineData("99", 0.99f)]        // パーセンテージ形式の境界
    public void ValidateR2Threshold_BoundaryValues_ReturnsCorrectValue(string input, float expectedValue)
    {
        var result = _service.ValidateR2Threshold(input);

        Assert.True(result.IsValid, $"Expected valid for input '{input}' but got invalid: {string.Join(", ", result.Errors)}");
        Assert.Equal(expectedValue, result.Value, precision: 2);
    }

    /// <summary>
    /// 極端な入力値に対するR2しきい値検証。
    /// </summary>
    [Theory]
    [InlineData("101")]              // > 100%
    [InlineData("-0.01")]            // 負の値
    [InlineData("NaN")]              // 非数
    [InlineData("Infinity")]         // 無限大
    public void ValidateR2Threshold_ExtremeValues_ReturnsError(string input)
    {
        var result = _service.ValidateR2Threshold(input);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }
}
