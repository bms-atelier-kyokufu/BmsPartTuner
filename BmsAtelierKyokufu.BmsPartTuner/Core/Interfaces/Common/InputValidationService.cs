using BmsAtelierKyokufu.BmsPartTuner.Core.Validation;
using ValidationResult = BmsAtelierKyokufu.BmsPartTuner.Core.Validation.ValidationResult;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Common;

/// <summary>
/// ユーザー入力（定義範囲や相関係数しきい値など）の検証を行うサービスの実装。
/// </summary>
public class InputValidationService : IInputValidationService
{
    private readonly DefinitionRangeValidator _definitionRangeValidator;
    private readonly R2ThresholdValidator _r2ThresholdValidator;

    /// <summary>
    /// InputValidationServiceを初期化します。
    /// </summary>
    public InputValidationService()
    {
        _definitionRangeValidator = new DefinitionRangeValidator();
        _r2ThresholdValidator = new R2ThresholdValidator();
    }

    /// <summary>
    /// 定義範囲を検証します。
    /// </summary>
    public ValidationResult ValidateDefinitionRange(string startVal, string endVal)
    {
        var range = new DefinitionRange(startVal, endVal);
        return _definitionRangeValidator.Validate(range);
    }

    /// <summary>
    /// 相関係数しきい値を検証します。
    /// </summary>
    public ValidationResult<float> ValidateR2Threshold(string r2Text)
    {
        return R2ThresholdValidator.ValidateWithValue(r2Text);
    }
}

