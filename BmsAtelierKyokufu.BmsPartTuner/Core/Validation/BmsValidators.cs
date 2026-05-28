namespace BmsAtelierKyokufu.BmsPartTuner.Core.Validation;

/// <summary>
/// 定義範囲の検証データ。
/// </summary>
/// <param name="Start">開始定義（2桁の62進数文字列）。</param>
/// <param name="End">終了定義（2桁の62進数文字列）。</param>
public record DefinitionRange(string Start, string End);

/// <summary>
/// 定義範囲のValidator実装（Strategy Pattern）。
/// 開始・終了がともに2桁の62進数であり、開始≧01、終了≦zzの範囲内で、終了 &gt; 開始の順序を満たしているかを検証します。
/// BMSフォーマットの仕様に基づき、62進数として有効な文字列であるかを判定します。
/// </summary>
public class DefinitionRangeValidator : IValidator<DefinitionRange>
{
    /// <summary>
    /// 定義範囲を検証。
    /// </summary>
    /// <param name="range">検証対象の定義範囲。</param>
    /// <returns>検証結果。</returns>
    public ValidationResult Validate(DefinitionRange range)
    {
        if (range == null)
            return ValidationResult.Failure("定義範囲が指定されていません");

        if (range.Start.Length != AppConstants.Definition.StringLength)
            return ValidationResult.Failure("開始定義は2桁で入力してください");

        if (range.End.Length != AppConstants.Definition.StringLength)
            return ValidationResult.Failure("終了定義は2桁で入力してください");

        try
        {
            // Why: BMSフォーマットは62進数（0-9, A-Z, a-z）をサポートするため、Base62で検証する
            var startValue = RadixConvert.ZZToInt(range.Start, AppConstants.Definition.RadixBase62);
            var endValue = RadixConvert.ZZToInt(range.End, AppConstants.Definition.RadixBase62);
            const int maxValue = AppConstants.Definition.MaxNumberBase62;

            if (startValue < AppConstants.Definition.MinNumber)
                return ValidationResult.Failure("開始定義は01以上にしてください");

            if (endValue > maxValue)
                return ValidationResult.Failure("終了定義はZZ以下にしてください");

            if (endValue <= startValue)
                return ValidationResult.Failure("終了定義は開始定義より大きい値にしてください");

            return ValidationResult.Success();
        }
        catch
        {
            return ValidationResult.Failure("定義の形式が正しくありません");
        }
    }
}

/// <summary>
/// 相関係数しきい値のValidator実装（Strategy Pattern）。
/// 入力値が空白でなく、0.0～1.0（または0～100）の範囲の数値に変換可能であるかを検証します。
/// </summary>
public class R2ThresholdValidator : IValidator<string>
{
    /// <summary>
    /// 相関係数しきい値を検証し、パースされた値を同時に返します。
    /// 検証とパースを同時に行うことで、重複した処理を排除します。
    /// </summary>
    /// <param name="r2Text">しきい値文字列。</param>
    /// <returns>検証結果（値付き）。</returns>
    public static ValidationResult<float> ValidateWithValue(string r2Text)
    {
        if (string.IsNullOrWhiteSpace(r2Text))
            return ValidationResult<float>.Failure("マッチ許容度を入力してください");

        // %記号を削除（PercentageSuffixBehaviorとの互換性）
        var valueText = r2Text.TrimEnd('%').Trim();

        // 整数として解析を試みる（表示値 0-100）
        if (int.TryParse(valueText, out var displayValue))
        {
            // Special case: "1" should be treated as 1.0 (100%) for correlation context
            // In correlation coefficient context, 1 means perfect correlation (1.0), not 1%
            if (displayValue == 1)
            {
                return ValidationResult<float>.Success(1.0f);
            }

            if (displayValue < AppConstants.Threshold.MinDisplay || displayValue > AppConstants.Threshold.MaxDisplay)
                return ValidationResult<float>.Failure($"マッチ許容度は{AppConstants.Threshold.MinDisplay}～{AppConstants.Threshold.MaxDisplay}の範囲で入力してください");

            // 表示値から内部値へ変換 (95 → 0.95)
            float internalValue = displayValue / 100f;
            return ValidationResult<float>.Success(internalValue);
        }

        // 小数として解析を試みる（内部値 0.0-1.00、後方互換性）
        if (float.TryParse(valueText, out var floatValue))
        {
            // 既に0-1の範囲なら内部値として受け入れ
            if (floatValue >= AppConstants.Threshold.MinValueForValidation && floatValue <= AppConstants.Threshold.Max)
                return ValidationResult<float>.Success(floatValue);

            // 1より大きければ表示値として扱う
            if (floatValue > 1f && floatValue <= AppConstants.Threshold.MaxDisplay)
            {
                float internalValue = floatValue / 100f;
                return ValidationResult<float>.Success(internalValue);
            }

            return ValidationResult<float>.Failure($"マッチ許容度は{AppConstants.Threshold.MinDisplay}～{AppConstants.Threshold.MaxDisplay}の範囲で入力してください");
        }

        return ValidationResult<float>.Failure("マッチ許容度の形式が正しくありません");
    }

    /// <summary>
    /// 相関係数しきい値を検証（値なし版）。
    /// </summary>
    /// <param name="r2Text">しきい値文字列。</param>
    /// <returns>検証結果。</returns>
    public ValidationResult Validate(string r2Text)
    {
        var result = ValidateWithValue(r2Text);
        return result.IsValid
            ? ValidationResult.Success()
            : ValidationResult.Failure(result.GetFirstError());
    }
}
