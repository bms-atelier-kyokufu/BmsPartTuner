using BmsAtelierKyokufu.BmsPartTuner.Core.Validation;

namespace BmsAtelierKyokufu.BmsPartTuner.Services;

/// <summary>
/// 入力値検証サービスの実装。
/// 責務: ユーザー入力の検証（定義範囲、相関係数しきい値）
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
        return _r2ThresholdValidator.ValidateWithValue(r2Text);
    }
}

