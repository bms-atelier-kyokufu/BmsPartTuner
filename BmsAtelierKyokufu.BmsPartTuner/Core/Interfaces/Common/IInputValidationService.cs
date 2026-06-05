using ValidationResult = BmsAtelierKyokufu.BmsPartTuner.Core.Validation.ValidationResult;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Common;

/// <summary>
/// ユーザー入力（定義範囲や相関係数しきい値など）の検証を行うサービスのインターフェース。
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