using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Validation;

/// <summary>
/// 定義範囲の検証データ。
/// </summary>
/// <param name="Start">開始定義（2桁の62進数文字列）。</param>
/// <param name="End">終了定義（2桁の62進数文字列）。</param>
public record DefinitionRange(string Start, string End);

/// <summary>
/// 定義範囲のValidator実装（Strategy Pattern）。
/// </summary>
/// <remarks>
/// <para>【検証項目】</para>
/// <list type="number">
/// <item>長さチェック: 開始・終了ともに2桁であること</item>
/// <item>範囲チェック: 開始≧01、終了≦zz（62進数の最大値）</item>
/// <item>順序チェック: 終了 &gt; 開始</item>
/// <item>形式チェック: 62進数として有効な文字列であること</item>
/// </list>
/// 
/// <para>【Why 62進数】</para>
/// BMSフォーマットでは、定義番号を62進数（0-9, a-z, A-Z）で表現します。
/// 2桁の62進数で01～zzの範囲（1～3843）を表現できます。
/// 
/// <para>【例】</para>
/// <code>
/// 01 → 1
/// 0z → 35
/// 10 → 36
/// zz → 3843
/// </code>
/// </remarks>
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
            var maxValue = RadixConvert.ZZToInt("zz", AppConstants.Definition.RadixBase62);

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
/// </summary>
/// <remarks>
/// <para>【検証項目】</para>
/// <list type="number">
/// <item>空白チェック: 値が入力されていること</item>
/// <item>数値チェック: float型に変換可能であること</item>
/// <item>範囲チェック: 0.0～1.0の範囲内であること</item>
/// </list>
/// 
/// <para>【Why 0.0～1.0】</para>
/// ピアソン相関係数は定義上-1.0～1.0の範囲ですが、
/// 音声比較では負の相関（逆相）を統合することはないため、
/// 0.0～1.0の範囲に限定しています。
/// 
/// <para>【推奨値】</para>
/// <list type="bullet">
/// <item>0.95～0.98: 標準（推奨）</item>
/// <item>0.90～0.95: やや緩い</item>
/// <item>0.98～1.00: 厳密</item>
/// </list>
/// </remarks>
public class R2ThresholdValidator : IValidator<string>
{
    /// <summary>
    /// 相関係数しきい値を検証し、値を返す。
    /// </summary>
    /// <param name="r2Text">しきい値文字列。</param>
    /// <returns>検証結果（値付き）。</returns>
    /// <remarks>
    /// <para>【Why 値付き検証】</para>
    /// 検証とパースを同時に行うことで、呼び出し側での再パースを不要にします。
    /// これにより、エラーが発生しやすい重複したパース処理を排除できます。
    /// </remarks>
    public ValidationResult<float> ValidateWithValue(string r2Text)
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
