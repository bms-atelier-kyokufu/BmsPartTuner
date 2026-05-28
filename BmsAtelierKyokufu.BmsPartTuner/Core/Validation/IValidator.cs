namespace BmsAtelierKyokufu.BmsPartTuner.Core.Validation;

/// <summary>
/// 検証を行うStrategy Patternのインターフェース。
/// 検証ロジックを独立したクラスに分離し、柔軟な検証ルールの切り替えと追加を可能にします。
/// </summary>
/// <typeparam name="T">検証対象の型。</typeparam>
public interface IValidator<T>
{
    /// <summary>
    /// 値を検証します。
    /// </summary>
    /// <param name="value">検証対象の値。</param>
    /// <returns>検証結果。</returns>
    ValidationResult Validate(T value);
}
