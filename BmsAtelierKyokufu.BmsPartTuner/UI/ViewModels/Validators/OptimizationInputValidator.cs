using System;
using System.Linq;
using BmsAtelierKyokufu.BmsPartTuner.Core;
using BmsAtelierKyokufu.BmsPartTuner.Core.Attributes;
using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Diagnostics;

namespace BmsAtelierKyokufu.BmsPartTuner.UI.ViewModels.Validators;

/// <summary>
/// OptimizationViewModel の入力バリデーションを担当する静的クラス。
/// ViewModel から検証ロジックを抽出し、テストの容易性と保守性を向上させます。
/// </summary>
[ADRAnchor("OPT-07", nameof(OptimizationInputValidator))]
public static class OptimizationInputValidator
{
    /// <summary>
    /// マッチ許容度（しきい値）の入力値を検証します。
    /// </summary>
    public static string ValidateR2Threshold(string threshold)
    {
        if (string.IsNullOrWhiteSpace(threshold))
        {
            return "マッチ許容度を入力してください";
        }

        if (threshold.Any(c => c > 0x7F))
        {
            return "半角数字のみを入力してください";
        }

        var valueText = threshold.TrimEnd('%').Trim();
        if (!int.TryParse(valueText, out var displayValue))
        {
            return "有効な数値を入力してください";
        }
        
        if (displayValue < AppConstants.Threshold.MinDisplay || displayValue > AppConstants.Threshold.MaxDisplay)
        {
            return $"マッチ許容度は{AppConstants.Threshold.MinDisplay}～{AppConstants.Threshold.MaxDisplay}の範囲で入力してください";
        }

        return string.Empty;
    }

    /// <summary>
    /// 定義開始インデックスの入力値を検証します。
    /// </summary>
    public static string ValidateDefinitionStart(string start, string end)
    {
        if (string.IsNullOrWhiteSpace(start) || start.Length != 2)
        {
            return "2桁で入力してください";
        }

        if (start.Any(c => c > 0x7F))
        {
            return "英数字のみを入力してください";
        }

        try
        {
            var startVal = RadixConvert.ZZToInt(start);
            if (startVal < 1)
            {
                return "01以上を入力してください";
            }
            else if (!string.IsNullOrWhiteSpace(end))
            {
                var endVal = RadixConvert.ZZToInt(end);
                if (startVal >= endVal)
                {
                    return "終了より小さい値にしてください";
                }
            }
        }
        catch
        {
            return "有効な値を入力してください";
        }

        return string.Empty;
    }

    /// <summary>
    /// 定義終了インデックスの入力値を検証します。
    /// </summary>
    public static string ValidateDefinitionEnd(string end, string start)
    {
        if (string.IsNullOrWhiteSpace(end) || end.Length != 2)
        {
            return "2桁で入力してください";
        }

        if (end.Any(c => c > 0x7F))
        {
            return "英数字のみを入力してください";
        }

        if (end.Equals(AppConstants.Definition.End, StringComparison.OrdinalIgnoreCase))
        {
            // 00は許可
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(start))
        {
            try
            {
                var startVal = RadixConvert.ZZToInt(start);
                var endVal = RadixConvert.ZZToInt(end);
                if (endVal != 0 && endVal <= startVal)
                {
                    return "開始より大きい値または00を入力してください";
                }
            }
            catch
            {
                return "有効な値を入力してください";
            }
        }

        return string.Empty;
    }
}
