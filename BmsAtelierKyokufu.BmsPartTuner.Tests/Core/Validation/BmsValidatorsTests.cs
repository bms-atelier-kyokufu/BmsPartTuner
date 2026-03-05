using BmsAtelierKyokufu.BmsPartTuner.Core.Validation;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Validation;

/// <summary>
/// <see cref="DefinitionRangeValidator"/> のテストクラス。
/// 
/// 【テスト対象】
/// - 定義範囲の妥当性検証
/// - 境界値チェック
/// - 形式チェック
/// 
/// 【テスト設計方針】
/// - 境界値: 01, zz, 大文字小文字
/// - 異常系: null, 空文字, 不正形式
/// </summary>
public class DefinitionRangeValidatorTests
{
    private readonly DefinitionRangeValidator _validator = new();

    #region Valid Range Tests - 正常系

    [Theory]
    [InlineData("01", "ZZ")]     // 36進数の全範囲
    [InlineData("01", "zz")]     // 62進数の全範囲
    [InlineData("01", "02")]     // 最小有効範囲
    [InlineData("10", "20")]     // 中間範囲
    [InlineData("0A", "0Z")]     // アルファベット範囲
    [InlineData("0a", "0z")]     // 小文字範囲
    public void Validate_ValidRange_ReturnsSuccess(string start, string end)
    {
        DefinitionRange range = new DefinitionRange(start, end);

        ValidationResult result = _validator.Validate(range);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    #endregion

    #region Null/Empty Tests - Null・空値

    [Fact]
    public void Validate_NullRange_ReturnsFailure()
    {
        ValidationResult result = _validator.Validate(null!);

        Assert.False(result.IsValid);
        Assert.Contains("定義範囲が指定されていません", result.GetFirstError());
    }

    #endregion

    #region Length Validation Tests - 長さチェック

    [Theory]
    [InlineData("1", "ZZ")]       // 開始が1桁
    [InlineData("001", "ZZ")]    // 開始が3桁
    [InlineData("", "ZZ")]       // 開始が空
    public void Validate_InvalidStartLength_ReturnsFailure(string start, string end)
    {
        DefinitionRange range = new DefinitionRange(start, end);

        ValidationResult result = _validator.Validate(range);

        Assert.False(result.IsValid);
        Assert.Contains("開始定義は2桁で入力してください", result.GetFirstError());
    }

    [Theory]
    [InlineData("01", "Z")]       // 終了が1桁
    [InlineData("01", "ZZZ")]    // 終了が3桁
    [InlineData("01", "")]       // 終了が空
    public void Validate_InvalidEndLength_ReturnsFailure(string start, string end)
    {
        DefinitionRange range = new DefinitionRange(start, end);

        ValidationResult result = _validator.Validate(range);

        Assert.False(result.IsValid);
        Assert.Contains("終了定義は2桁で入力してください", result.GetFirstError());
    }

    #endregion

    #region Range Validation Tests - 範囲チェック

    [Fact]
    public void Validate_StartBelowMinimum_ReturnsFailure()
    {
        // "00" = 0 < 最小値1
        DefinitionRange range = new DefinitionRange("00", "ZZ");

        ValidationResult result = _validator.Validate(range);

        Assert.False(result.IsValid);
        Assert.Contains("開始定義は01以上にしてください", result.GetFirstError());
    }

    [Fact]
    public void Validate_EndGreaterThanStart_RequiredForSuccess()
    {
        // 終了 < 開始
        DefinitionRange range = new DefinitionRange("20", "10");

        ValidationResult result = _validator.Validate(range);

        Assert.False(result.IsValid);
        Assert.Contains("終了定義は開始定義より大きい値にしてください", result.GetFirstError());
    }

    [Fact]
    public void Validate_EndEqualsStart_ReturnsFailure()
    {
        // 終了 == 開始
        DefinitionRange range = new DefinitionRange("10", "10");

        ValidationResult result = _validator.Validate(range);

        Assert.False(result.IsValid);
        Assert.Contains("終了定義は開始定義より大きい値にしてください", result.GetFirstError());
    }

    #endregion

    #region Format Validation Tests - 形式チェック

    [Theory]
    [InlineData("!!", "ZZ")]     // 記号
    [InlineData("##", "ZZ")]     // 特殊文字
    [InlineData("  ", "ZZ")]     // 空白
    public void Validate_InvalidCharacters_ReturnsFailure(string start, string end)
    {
        DefinitionRange range = new DefinitionRange(start, end);

        ValidationResult result = _validator.Validate(range);

        Assert.False(result.IsValid);
    }

    #endregion

    #region Case Sensitivity Tests - 大文字小文字

    [Fact]
    public void Validate_MixedCase_AcceptsBoth()
    {
        // 大文字と小文字の混在
        DefinitionRange range = new DefinitionRange("0A", "0z");

        ValidationResult result = _validator.Validate(range);

        Assert.True(result.IsValid);
    }

    #endregion
}

/// <summary>
/// <see cref="R2ThresholdValidator"/> のテストクラス。
/// 
/// 【テスト対象】
/// - 相関係数しきい値の検証
/// - 表示値（0-100）と内部値（0.0-1.0）の両対応
/// 
/// 【テスト設計方針】
/// - 境界値: 0, 100, 0.0, 1.0
/// - 異常系: null, 空文字, 範囲外
/// </summary>
public class R2ThresholdValidatorTests
{
    private readonly R2ThresholdValidator _validator = new();

    #region ValidateWithValue Tests - 正常系（整数 = 表示値）

    [Theory]
    [InlineData("0", 0.0f)]
    [InlineData("50", 0.5f)]
    [InlineData("95", 0.95f)]
    [InlineData("100", 1.0f)]
    public void ValidateWithValue_IntegerDisplayValue_ReturnsConvertedInternalValue(
        string input, float expectedInternal)
    {
        ValidationResult<float> result = _validator.ValidateWithValue(input);

        Assert.True(result.IsValid);
        Assert.Equal(expectedInternal, result.Value, 0.001f);
    }

    #endregion

    #region ValidateWithValue Tests - 正常系（小数 = 内部値、後方互換）

    [Theory]
    [InlineData("0.0", 0.0f)]
    [InlineData("0.5", 0.5f)]
    [InlineData("0.95", 0.95f)]
    [InlineData("1.0", 1.0f)]
    public void ValidateWithValue_DecimalInternalValue_PreservesValue(
        string input, float expectedInternal)
    {
        ValidationResult<float> result = _validator.ValidateWithValue(input);

        Assert.True(result.IsValid);
        Assert.Equal(expectedInternal, result.Value, 0.001f);
    }

    #endregion

    #region ValidateWithValue Tests - 正常系（小数表示値）

    [Theory]
    [InlineData("50.5", 0.505f)]   // 1-100スケールの小数
    [InlineData("75.25", 0.7525f)]
    public void ValidateWithValue_DecimalDisplayValue_ConvertsCorrectly(
        string input, float expectedInternal)
    {
        ValidationResult<float> result = _validator.ValidateWithValue(input);

        Assert.True(result.IsValid);
        Assert.Equal(expectedInternal, result.Value, 0.001f);
    }

    #endregion

    #region ValidateWithValue Tests - 異常系

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidateWithValue_EmptyOrWhitespace_ReturnsFailure(string? input)
    {
        ValidationResult<float> result = _validator.ValidateWithValue(input!);

        // UI上の用語が「相関係数」から「マッチ許容度」へ変更されました。
        // エラーメッセージにも新しい用語が反映されていることを検証します。
        Assert.False(result.IsValid);
        Assert.Contains("マッチ許容度を入力してください", result.GetFirstError());
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("-50")]
    [InlineData("101")]
    [InlineData("200")]
    public void ValidateWithValue_OutOfRange_ReturnsFailure(string input)
    {
        ValidationResult<float> result = _validator.ValidateWithValue(input);

        Assert.False(result.IsValid);
        Assert.Contains("0～100の範囲で入力してください", result.GetFirstError());
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("!@#")]
    [InlineData("1.2.3")]
    public void ValidateWithValue_InvalidFormat_ReturnsFailure(string input)
    {
        ValidationResult<float> result = _validator.ValidateWithValue(input);

        Assert.False(result.IsValid);
        Assert.Contains("形式が正しくありません", result.GetFirstError());
    }

    #endregion

    #region Validate Tests - 値なし版

    [Fact]
    public void Validate_ValidInput_ReturnsSuccess()
    {
        ValidationResult result = _validator.Validate("95");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_InvalidInput_ReturnsFailure()
    {
        ValidationResult result = _validator.Validate("invalid");

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.GetFirstError());
    }

    #endregion

    #region Edge Cases - エッジケース

    [Theory]
    [InlineData("0")]      // 最小境界
    [InlineData("100")]    // 最大境界
    public void ValidateWithValue_BoundaryValues_ReturnsSuccess(string input)
    {
        ValidationResult<float> result = _validator.ValidateWithValue(input);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateWithValue_LeadingZeros_ParsesCorrectly()
    {
        ValidationResult<float> result = _validator.ValidateWithValue("095");

        Assert.True(result.IsValid);
        Assert.Equal(0.95f, result.Value, 0.001f);
    }

    #endregion
}

/// <summary>
/// <see cref="ValidationResult"/> および <see cref="ValidationResult{T}"/> のテストクラス。
/// </summary>
public class ValidationResultTests
{
    #region ValidationResult Tests

    [Fact]
    public void Success_CreatesValidResult()
    {
        ValidationResult result = ValidationResult.Success();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Failure_SingleError_CreatesInvalidResult()
    {
        ValidationResult result = ValidationResult.Failure("エラーメッセージ");

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("エラーメッセージ", result.GetFirstError());
    }

    [Fact]
    public void Failure_MultipleErrors_CreatesInvalidResult()
    {
        var errors = new[] { "エラー1", "エラー2", "エラー3" };

        ValidationResult result = ValidationResult.Failure(errors);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void GetAllErrors_JoinsWithSeparator()
    {
        var errors = new[] { "エラー1", "エラー2" };
        ValidationResult result = ValidationResult.Failure(errors);

        var allErrors = result.GetAllErrors(", ");

        Assert.Equal("エラー1, エラー2", allErrors);
    }

    [Fact]
    public void GetFirstError_NoErrors_ReturnsEmptyString()
    {
        ValidationResult result = ValidationResult.Success();

        var firstError = result.GetFirstError();

        Assert.Equal(string.Empty, firstError);
    }

    #endregion

    #region ValidationResult<T> Tests

    [Fact]
    public void Success_WithValue_CreatesValidResultWithValue()
    {
        ValidationResult<float> result = ValidationResult<float>.Success(0.95f);

        Assert.True(result.IsValid);
        Assert.Equal(0.95f, result.Value);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Failure_WithValue_CreatesInvalidResult()
    {
        ValidationResult<float> result = ValidationResult<float>.Failure("エラー");

        Assert.False(result.IsValid);
        Assert.Equal(default, result.Value);
        Assert.Single(result.Errors);
    }

    #endregion
}
