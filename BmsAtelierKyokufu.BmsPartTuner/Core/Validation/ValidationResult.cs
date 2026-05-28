namespace BmsAtelierKyokufu.BmsPartTuner.Core.Validation;

/// <summary>
/// 検証結果を表すResult Patternです。
/// 例外を投げる代わりに検証結果を値として返すことで、パフォーマンスの向上と明示的で型安全なエラーハンドリングを実現します。
/// </summary>
public sealed class ValidationResult
{
    /// <summary>検証が成功したかどうか。</summary>
    public bool IsValid { get; }

    /// <summary>エラーメッセージのリスト。</summary>
    public IReadOnlyList<string> Errors { get; }

    private ValidationResult(bool isValid, IEnumerable<string> errors)
    {
        IsValid = isValid;
        Errors = [.. errors];
    }

    /// <summary>
    /// 成功結果を作成。
    /// </summary>
    /// <returns>成功を表すValidationResult。</returns>
    public static ValidationResult Success()
        => new(true, []);

    /// <summary>
    /// 失敗結果を作成。
    /// </summary>
    /// <param name="error">エラーメッセージ。</param>
    /// <returns>失敗を表すValidationResult。</returns>
    public static ValidationResult Failure(string error)
        => new(false, [error]);

    /// <summary>
    /// 複数エラーの失敗結果を作成。
    /// </summary>
    /// <param name="errors">エラーメッセージのコレクション。</param>
    /// <returns>失敗を表すValidationResult。</returns>
    public static ValidationResult Failure(IEnumerable<string> errors)
        => new(false, errors);

    /// <summary>
    /// 最初のエラーメッセージを取得。
    /// </summary>
    /// <returns>最初のエラーメッセージ、エラーがない場合は空文字列。</returns>
    public string GetFirstError()
        => Errors.Count > 0 ? Errors[0] : string.Empty;

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
/// 検証成功時にパースされた値を一緒に返すことで、呼び出し側での再パース処理を省略できます。
/// </summary>
/// <typeparam name="T">検証値の型。</typeparam>
public sealed class ValidationResult<T>
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
        Errors = [.. errors];
    }

    /// <summary>
    /// 成功結果を作成。
    /// </summary>
    /// <param name="value">検証された値。</param>
    /// <returns>成功を表すValidationResult。</returns>
    public static ValidationResult<T> Success(T value)
        => new(true, value, []);

    /// <summary>
    /// 失敗結果を作成。
    /// </summary>
    /// <param name="error">エラーメッセージ。</param>
    /// <returns>失敗を表すValidationResult。</returns>
    public static ValidationResult<T> Failure(string error)
        => new(false, default, [error]);

    /// <summary>
    /// 最初のエラーメッセージを取得。
    /// </summary>
    /// <returns>最初のエラーメッセージ、エラーがない場合は空文字列。</returns>
    public string GetFirstError()
        => Errors.Count > 0 ? Errors[0] : string.Empty;
}
