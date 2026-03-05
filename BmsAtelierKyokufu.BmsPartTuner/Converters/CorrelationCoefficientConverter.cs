using System.Globalization;
using System.Windows.Data;

namespace BmsAtelierKyokufu.BmsPartTuner.Converters
{
    /// <summary>
    /// 相関係数の表示値(1-100)と内部値(0.00-1.00)を変換するコンバーター
    /// 
    /// 【変換規則】
    /// - 表示: 1-100の整数（ユーザーフレンドリー）
    /// - 内部: 0.00-1.00の小数（計算用）
    /// 
    /// 【例】
    /// 表示値 95 → 内部値 0.95
    /// 内部値 0.98 → 表示値 98
    /// </summary>
    public class CorrelationCoefficientConverter : IValueConverter
    {
        /// <summary>
        /// 内部値(0.00-1.00)から表示値(1-100)への変換
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string strValue && float.TryParse(strValue, out float floatValue))
            {
                // 0.95 → 95
                int displayValue = (int)Math.Round(floatValue * 100);
                return displayValue.ToString();
            }

            if (value is float f)
            {
                int displayValue = (int)Math.Round(f * 100);
                return displayValue.ToString();
            }

            if (value is double d)
            {
                int displayValue = (int)Math.Round(d * 100);
                return displayValue.ToString();
            }

            return value?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// 表示値(1-100)から内部値(0.00-1.00)への変換
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string strValue)
            {
                // 空文字の場合はデフォルト値を返す
                if (string.IsNullOrWhiteSpace(strValue))
                {
                    return Core.AppConstants.Threshold.Default.ToString("F2");
                }

                // 整数として解析
                if (int.TryParse(strValue, out int displayValue))
                {
                    // 範囲チェック (0-100)
                    if (displayValue < 0)
                        displayValue = 0;
                    if (displayValue > 100)
                        displayValue = 100;

                    // 95 → 0.95
                    float internalValue = displayValue / 100f;
                    return internalValue.ToString("F2");
                }

                // 小数として解析を試みる（既に0-1の形式の場合の互換性）
                if (float.TryParse(strValue, out float floatValue))
                {
                    // 既に0-1の範囲なら、そのまま使用
                    if (floatValue >= 0f && floatValue <= 1f)
                    {
                        return floatValue.ToString("F2");
                    }
                    // 1より大きい場合は100で割る
                    else if (floatValue > 1f && floatValue <= 100f)
                    {
                        return (floatValue / 100f).ToString("F2");
                    }
                }

                // パース失敗時はデフォルト値
                return Core.AppConstants.Threshold.Default.ToString("F2");
            }

            return Core.AppConstants.Threshold.Default.ToString("F2");
        }
    }
}
