using BmsAtelierKyokufu.BmsPartTuner.Core.Validation;

namespace BmsAtelierKyokufu.BmsPartTuner.Services;

/// <summary>
/// 入力値検証サービスのインターフェース。
/// 責務: ユーザー入力の検証（定義範囲、相関係数しきい値）
/// ISP (Interface Segregation Principle) に基づき、検証機能を分離
/// </summary>
public interface IInputValidationService
{
    /// <summary>
    /// 定義範囲を検証します。
    /// </summary>
    /// <param name="startVal">開始定義（文字列、16進数）。</param>
    /// <param name="endVal">終了定義（文字列、16進数）。</param>
    /// <returns>検証結果。</returns>
    ValidationResult ValidateDefinitionRange(string startVal, string endVal);

    /// <summary>
    /// 相関係数しきい値を検証します。
    /// </summary>
    /// <param name="r2Text">相関係数の入力値（文字列）。例: "80", "0.8"</param>
    /// <returns>検証結果と変換された値。成功時は 0.0～1.0 の float 値。</returns>
    ValidationResult<float> ValidateR2Threshold(string r2Text);
}
