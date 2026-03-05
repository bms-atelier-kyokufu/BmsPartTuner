namespace BmsAtelierKyokufu.BmsPartTuner.Core.Validation;

/// <summary>
/// 検証を行うStrategy Patternのインターフェース。
/// </summary>
/// <typeparam name="T">検証対象の型。</typeparam>
/// <remarks>
/// <para>【Strategy Pattern】</para>
/// 検証ロジックをインターフェースとして抽象化することで、
/// 異なる検証ルールを柔軟に切り替えられます。
///
/// <para>【利点】</para>
/// <list type="bullet">
/// <item>検証ロジックを独立したクラスに分離（SRP: 単一責任の原則）</item>
/// <item>新しい検証ルールを既存コードに影響なく追加可能（OCP: 開放閉鎖の原則）</item>
/// <item>ユニットテストが容易（モック化しやすい）</item>
/// </list>
///
/// <para>【実装例】</para>
/// <list type="bullet">
/// <item><see cref="DefinitionRangeValidator"/>: BMS定義範囲の検証</item>
/// <item><see cref="R2ThresholdValidator"/>: 相関係数しきい値の検証</item>
/// </list>
/// </remarks>
public interface IValidator<T>
{
    /// <summary>
    /// 値を検証します。
    /// </summary>
    /// <param name="value">検証対象の値。</param>
    /// <returns>検証結果。</returns>
    ValidationResult Validate(T value);
}
