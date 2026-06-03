using BmsAtelierKyokufu.BmsPartTuner.Core.Validation;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Validation;

/// <summary>
/// <see cref="DefinitionRangeValidator"/> のテストクラス。
/// </summary>
public class DefinitionRangeValidatorTests
{
    private readonly DefinitionRangeValidator _validator = new();

    #region Valid Range Tests - 正常系

    /// <summary>
    /// 有効な定義範囲が指定された場合、検証が成功することを確認します。
    /// </summary>
    /// <param name="start">開始定義。</param>
    /// <param name="end">終了定義。</param>
    /// <summary>
    /// テスト を検証します。
    /// </summary>
    [Theory]
    [InlineData("01", "ZZ")]     // 36進数の全範囲
    [InlineData("01", "zz")]     // 62進数の全範囲
    [InlineData("01", "02")]     // 最小有効範囲
    [InlineData("10", "20")]     // 中間範囲
    [InlineData("0A", "0Z")]     // アルファベット範囲
    [InlineData("0a", "0z")]     // 小文字範囲
    public void Validate_ValidRange_ReturnsSuccess(string start, string end)
    {
        DefinitionRange range = new(start, end);

        ValidationResult result = _validator.Validate(range);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    #endregion

    #region Null/Empty Tests - Null・空値

    /// <summary>
    /// nullが指定された場合、検証が失敗することを確認します。
    /// </summary>
    [Fact]
    public void Validate_NullRange_ReturnsFailure()
    {
        ValidationResult result = _validator.Validate(null!);

        Assert.False(result.IsValid);
        Assert.Contains("定義範囲が指定されていません", result.GetFirstError());
    }

    #endregion

    #region Length Validation Tests - 長さチェック

    /// <summary>
    /// 開始定義の桁数が不正な場合、検証が失敗することを確認します。
    /// </summary>
    /// <param name="start">開始定義。</param>
    /// <param name="end">終了定義。</param>
    /// <summary>
    /// Validate において、条件 InvalidStartLength の場合に ReturnsFailure されることを検証します。
    /// </summary>
    [Theory]
    [InlineData("1", "ZZ")]       // 開始が1桁
    [InlineData("001", "ZZ")]    // 開始が3桁
    [InlineData("", "ZZ")]       // 開始が空
    public void Validate_InvalidStartLength_ReturnsFailure(string start, string end)
    {
        DefinitionRange range = new(start, end);

        ValidationResult result = _validator.Validate(range);

        Assert.False(result.IsValid);
        Assert.Contains("開始定義は2桁で入力してください", result.GetFirstError());
    }

    /// <summary>
    /// 終了定義の桁数が不正な場合、検証が失敗することを確認します。
    /// </summary>
    /// <param name="start">開始定義。</param>
    /// <param name="end">終了定義。</param>
    /// <summary>
    /// Validate において、条件 InvalidEndLength の場合に ReturnsFailure されることを検証します。
    /// </summary>
    [Theory]
    [InlineData("01", "Z")]       // 終了が1桁
    [InlineData("01", "ZZZ")]    // 終了が3桁
    [InlineData("01", "")]       // 終了が空
    public void Validate_InvalidEndLength_ReturnsFailure(string start, string end)
    {
        DefinitionRange range = new(start, end);

        ValidationResult result = _validator.Validate(range);

        Assert.False(result.IsValid);
        Assert.Contains("終了定義は2桁で入力してください", result.GetFirstError());
    }

    #endregion

    #region Range Validation Tests - 範囲チェック

    /// <summary>
    /// 開始定義が最小値未満の場合、検証が失敗することを確認します。
    /// </summary>
    [Fact]
    public void Validate_StartBelowMinimum_ReturnsFailure()
    {
        // "00" = 0 < 最小値1
        DefinitionRange range = new("00", "ZZ");

        ValidationResult result = _validator.Validate(range);

        Assert.False(result.IsValid);
        Assert.Contains("開始定義は01以上にしてください", result.GetFirstError());
    }

    /// <summary>
    /// 終了定義が開始定義より小さい場合、検証が失敗することを確認します。
    /// </summary>
    [Fact]
    public void Validate_EndGreaterThanStart_RequiredForSuccess()
    {
        // 終了 < 開始
        DefinitionRange range = new("20", "10");

        ValidationResult result = _validator.Validate(range);

        Assert.False(result.IsValid);
        Assert.Contains("終了定義は開始定義より大きい値にしてください", result.GetFirstError());
    }

    /// <summary>
    /// 終了定義が開始定義と等しい場合、検証が失敗することを確認します。
    /// </summary>
    [Fact]
    public void Validate_EndEqualsStart_ReturnsFailure()
    {
        // 終了 == 開始
        DefinitionRange range = new("10", "10");

        ValidationResult result = _validator.Validate(range);

        Assert.False(result.IsValid);
        Assert.Contains("終了定義は開始定義より大きい値にしてください", result.GetFirstError());
    }

    #endregion

    #region Format Validation Tests - 形式チェック

    /// <summary>
    /// 不正な文字が含まれている場合、検証が失敗することを確認します。
    /// </summary>
    /// <param name="start">開始定義。</param>
    /// <param name="end">終了定義。</param>
    /// <summary>
    /// Validate において、条件 InvalidCharacters の場合に ReturnsFailure されることを検証します。
    /// </summary>
    [Theory]
    [InlineData("!!", "ZZ")]     // 記号
    [InlineData("##", "ZZ")]     // 特殊文字
    [InlineData("  ", "ZZ")]     // 空白
    public void Validate_InvalidCharacters_ReturnsFailure(string start, string end)
    {
        DefinitionRange range = new(start, end);

        ValidationResult result = _validator.Validate(range);

        Assert.False(result.IsValid);
    }

    #endregion

    #region Case Sensitivity Tests - 大文字小文字

    /// <summary>
    /// 大文字と小文字が混在していても許容されることを確認します。
    /// </summary>
    [Fact]
    public void Validate_MixedCase_AcceptsBoth()
    {
        // 大文字と小文字の混在
        DefinitionRange range = new("0A", "0z");

        ValidationResult result = _validator.Validate(range);

        Assert.True(result.IsValid);
    }

    #endregion
}

/// <summary>
/// <see cref="R2ThresholdValidator"/> のテストクラス。
/// </summary>
public class R2ThresholdValidatorTests
{
    private readonly R2ThresholdValidator _validator = new();

    #region ValidateWithValue Tests - 正常系（整数 = 表示値）

    /// <summary>
    /// 整数の表示値が指定された場合、対応する内部値に変換されることを確認します。
    /// </summary>
    /// <param name="input">入力文字列。</param>
    /// <param name="expectedInternal">期待される内部値。</param>
    /// <summary>
    /// テスト を検証します。
    /// </summary>
    [Theory]
    [InlineData("0", 0.0f)]
    [InlineData("50", 0.5f)]
    [InlineData("95", 0.95f)]
    [InlineData("100", 1.0f)]
    public void ValidateWithValue_IntegerDisplayValue_ReturnsConvertedInternalValue(
        string input, float expectedInternal)
    {
        ValidationResult<float> result = R2ThresholdValidator.ValidateWithValue(input);

        Assert.True(result.IsValid);
        Assert.Equal(expectedInternal, result.Value, 0.001f);
    }

    #endregion

    #region ValidateWithValue Tests - 正常系（小数 = 内部値、後方互換）

    /// <summary>
    /// 小数の内部値が指定された場合、そのままの値が保持されることを確認します。
    /// </summary>
    /// <param name="input">入力文字列。</param>
    /// <param name="expectedInternal">期待される内部値。</param>
    /// <summary>
    /// テスト を検証します。
    /// </summary>
    [Theory]
    [InlineData("0.0", 0.0f)]
    [InlineData("0.5", 0.5f)]
    [InlineData("0.95", 0.95f)]
    [InlineData("1.0", 1.0f)]
    public void ValidateWithValue_DecimalInternalValue_PreservesValue(
        string input, float expectedInternal)
    {
        ValidationResult<float> result = R2ThresholdValidator.ValidateWithValue(input);

        Assert.True(result.IsValid);
        Assert.Equal(expectedInternal, result.Value, 0.001f);
    }

    #endregion

    #region ValidateWithValue Tests - 正常系（小数表示値）

    /// <summary>
    /// 小数の表示値が指定された場合、対応する内部値に正しく変換されることを確認します。
    /// </summary>
    /// <param name="input">入力文字列。</param>
    /// <param name="expectedInternal">期待される内部値。</param>
    /// <summary>
    /// ValidateWithValue において、条件 DecimalDisplayValue の場合に ConvertsCorrectly されることを検証します。
    /// </summary>
    [Theory]
    [InlineData("50.5", 0.505f)]   // 1-100スケールの小数
    [InlineData("75.25", 0.7525f)]
    public void ValidateWithValue_DecimalDisplayValue_ConvertsCorrectly(
        string input, float expectedInternal)
    {
        ValidationResult<float> result = R2ThresholdValidator.ValidateWithValue(input);

        Assert.True(result.IsValid);
        Assert.Equal(expectedInternal, result.Value, 0.001f);
    }

    #endregion

    #region ValidateWithValue Tests - 異常系

    /// <summary>
    /// 空または空白の文字列が指定された場合、検証が失敗することを確認します。
    /// </summary>
    /// <param name="input">入力文字列。</param>
    /// <summary>
    /// ValidateWithValue において、条件 EmptyOrWhitespace の場合に ReturnsFailure されることを検証します。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidateWithValue_EmptyOrWhitespace_ReturnsFailure(string? input)
    {
        ValidationResult<float> result = R2ThresholdValidator.ValidateWithValue(input!);

        Assert.False(result.IsValid);
        Assert.Contains("マッチ許容度を入力してください", result.GetFirstError());
    }

    /// <summary>
    /// 範囲外の値が指定された場合、検証が失敗することを確認します。
    /// </summary>
    /// <param name="input">入力文字列。</param>
    /// <summary>
    /// テスト を検証します。
    /// </summary>
    [Theory]
    [InlineData("-1")]
    [InlineData("-50")]
    [InlineData("101")]
    [InlineData("200")]
    public void ValidateWithValue_OutOfRange_ReturnsFailure(string input)
    {
        ValidationResult<float> result = R2ThresholdValidator.ValidateWithValue(input);

        Assert.False(result.IsValid);
        Assert.Contains("0～100の範囲で入力してください", result.GetFirstError());
    }

    /// <summary>
    /// 不正な形式の文字列が指定された場合、検証が失敗することを確認します。
    /// </summary>
    /// <param name="input">入力文字列。</param>
    /// <summary>
    /// ValidateWithValue において、条件 InvalidFormat の場合に ReturnsFailure されることを検証します。
    /// </summary>
    [Theory]
    [InlineData("abc")]
    [InlineData("!@#")]
    [InlineData("1.2.3")]
    public void ValidateWithValue_InvalidFormat_ReturnsFailure(string input)
    {
        ValidationResult<float> result = R2ThresholdValidator.ValidateWithValue(input);

        Assert.False(result.IsValid);
        Assert.Contains("形式が正しくありません", result.GetFirstError());
    }

    #endregion

    #region Validate Tests - 値なし版

    /// <summary>
    /// 有効な入力が指定された場合、検証が成功することを確認します。
    /// </summary>
    [Fact]
    public void Validate_ValidInput_ReturnsSuccess()
    {
        ValidationResult result = _validator.Validate("95");

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// 無効な入力が指定された場合、検証が失敗することを確認します。
    /// </summary>
    [Fact]
    public void Validate_InvalidInput_ReturnsFailure()
    {
        ValidationResult result = _validator.Validate("invalid");

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.GetFirstError());
    }

    #endregion

    #region Edge Cases - エッジケース

    /// <summary>
    /// 境界値が指定された場合、検証が成功することを確認します。
    /// </summary>
    /// <param name="input">入力文字列。</param>
    /// <summary>
    /// ValidateWithValue において、条件 BoundaryValues の場合に ReturnsSuccess されることを検証します。
    /// </summary>
    [Theory]
    [InlineData("0")]      // 最小境界
    [InlineData("100")]    // 最大境界
    public void ValidateWithValue_BoundaryValues_ReturnsSuccess(string input)
    {
        ValidationResult<float> result = R2ThresholdValidator.ValidateWithValue(input);

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// 先頭にゼロが含まれる値が指定された場合、正しく解析されることを確認します。
    /// </summary>
    [Fact]
    public void ValidateWithValue_LeadingZeros_ParsesCorrectly()
    {
        ValidationResult<float> result = R2ThresholdValidator.ValidateWithValue("095");

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

    /// <summary>
    /// 成功を表す有効な結果が作成されることを確認します。
    /// </summary>
    [Fact]
    public void Success_CreatesValidResult()
    {
        ValidationResult result = ValidationResult.Success();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// 単一のエラーを持つ失敗結果が作成されることを確認します。
    /// </summary>
    [Fact]
    public void Failure_SingleError_CreatesInvalidResult()
    {
        ValidationResult result = ValidationResult.Failure("エラーメッセージ");

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("エラーメッセージ", result.GetFirstError());
    }

    /// <summary>
    /// 複数のエラーを持つ失敗結果が作成されることを確認します。
    /// </summary>
    [Fact]
    public void Failure_MultipleErrors_CreatesInvalidResult()
    {
        var errors = new[] { "エラー1", "エラー2", "エラー3" };

        ValidationResult result = ValidationResult.Failure(errors);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }

    /// <summary>
    /// 全てのエラーメッセージが指定した区切り文字で結合されることを確認します。
    /// </summary>
    [Fact]
    public void GetAllErrors_JoinsWithSeparator()
    {
        var errors = new[] { "エラー1", "エラー2" };
        ValidationResult result = ValidationResult.Failure(errors);

        var allErrors = result.GetAllErrors(", ");

        Assert.Equal("エラー1, エラー2", allErrors);
    }

    /// <summary>
    /// エラーがない場合、最初のエラーとして空文字列が返されることを確認します。
    /// </summary>
    [Fact]
    public void GetFirstError_NoErrors_ReturnsEmptyString()
    {
        ValidationResult result = ValidationResult.Success();

        var firstError = result.GetFirstError();

        Assert.Equal(string.Empty, firstError);
    }

    #endregion

    #region ValidationResult<T> Tests

    /// <summary>
    /// 値を持つ成功結果が作成されることを確認します。
    /// </summary>
    [Fact]
    public void Success_WithValue_CreatesValidResultWithValue()
    {
        ValidationResult<float> result = ValidationResult<float>.Success(0.95f);

        Assert.True(result.IsValid);
        Assert.Equal(0.95f, result.Value);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// 値を持たない失敗結果が作成されることを確認します。
    /// </summary>
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
