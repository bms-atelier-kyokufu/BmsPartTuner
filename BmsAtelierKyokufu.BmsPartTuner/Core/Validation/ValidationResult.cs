namespace BmsAtelierKyokufu.BmsPartTuner.Core.Validation;

/// <summary>
/// 検証結果を表すResult Pattern。
/// </summary>
/// <remarks>
/// <para>【Result Pattern】</para>
/// 例外を投げる代わりに、検証結果を値として返すことで、
/// エラーハンドリングを明示的かつ型安全に行えます。
/// 
/// <para>【Why Result Pattern】</para>
/// <list type="bullet">
/// <item>例外よりもパフォーマンスが良い（スタック巻き戻し不要）</item>
/// <item>エラーハンドリングが強制される（コンパイラが保証）</item>
/// <item>複数のエラーを集約できる</item>
/// </list>
/// </remarks>
public class ValidationResult
{
    /// <summary>検証が成功したかどうか。</summary>
    public bool IsValid { get; }

    /// <summary>エラーメッセージのリスト。</summary>
    public IReadOnlyList<string> Errors { get; }

    private ValidationResult(bool isValid, IEnumerable<string> errors)
    {
        IsValid = isValid;
        Errors = errors.ToList();
    }

    /// <summary>
    /// 成功結果を作成。
    /// </summary>
    /// <returns>成功を表すValidationResult。</returns>
    public static ValidationResult Success()
        => new ValidationResult(true, Enumerable.Empty<string>());

    /// <summary>
    /// 失敗結果を作成。
    /// </summary>
    /// <param name="error">エラーメッセージ。</param>
    /// <returns>失敗を表すValidationResult。</returns>
    public static ValidationResult Failure(string error)
        => new ValidationResult(false, new[] { error });

    /// <summary>
    /// 複数エラーの失敗結果を作成。
    /// </summary>
    /// <param name="errors">エラーメッセージのコレクション。</param>
    /// <returns>失敗を表すValidationResult。</returns>
    public static ValidationResult Failure(IEnumerable<string> errors)
        => new ValidationResult(false, errors);

    /// <summary>
    /// 最初のエラーメッセージを取得。
    /// </summary>
    /// <returns>最初のエラーメッセージ、エラーがない場合は空文字列。</returns>
    public string GetFirstError()
        => Errors.FirstOrDefault() ?? string.Empty;

    /// <summary>
    /// すべてのエラーを連結。
    /// </summary>
    /// <param name="separator">セパレータ（デフォルト: 改行）。</param>
    /// <returns>連結されたエラーメッセージ。</returns>
    public string GetAllErrors(string separator = "\n")
        => string.Join(separator, Errors);
}

/// <summary>
/// 値を含む検証結果。
/// </summary>
/// <typeparam name="T">検証値の型。</typeparam>
/// <remarks>
/// <para>【用途】</para>
/// 検証成功時に、パースされた値を一緒に返すことで、
/// 呼び出し側でのパース処理を省略できます。
/// 
/// <para>【例】</para>
/// <code>
/// var result = ValidateR2Threshold("0.95");
/// if (result.IsValid) {
///     float threshold = result.Value; // 再パース不要
/// }
/// </code>
/// </remarks>
public class ValidationResult<T>
{
    /// <summary>検証が成功したかどうか。</summary>
    public bool IsValid { get; }

    /// <summary>検証値（成功時）。</summary>
    public T? Value { get; }

    /// <summary>エラーメッセージのリスト。</summary>
    public IReadOnlyList<string> Errors { get; }

    private ValidationResult(bool isValid, T? value, IEnumerable<string> errors)
    {
        IsValid = isValid;
        Value = value;
        Errors = errors.ToList();
    }

    /// <summary>
    /// 成功結果を作成。
    /// </summary>
    /// <param name="value">検証された値。</param>
    /// <returns>成功を表すValidationResult。</returns>
    public static ValidationResult<T> Success(T value)
        => new ValidationResult<T>(true, value, Enumerable.Empty<string>());

    /// <summary>
    /// 失敗結果を作成。
    /// </summary>
    /// <param name="error">エラーメッセージ。</param>
    /// <returns>失敗を表すValidationResult。</returns>
    public static ValidationResult<T> Failure(string error)
        => new ValidationResult<T>(false, default, new[] { error });

    /// <summary>
    /// 最初のエラーメッセージを取得。
    /// </summary>
    /// <returns>最初のエラーメッセージ、エラーがない場合は空文字列。</returns>
    public string GetFirstError()
        => Errors.FirstOrDefault() ?? string.Empty;
}
