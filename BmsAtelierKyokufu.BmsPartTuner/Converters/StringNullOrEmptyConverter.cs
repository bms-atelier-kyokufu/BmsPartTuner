using System.Globalization;
using System.Windows.Data;

namespace BmsAtelierKyokufu.BmsPartTuner.Converters;

/// <summary>
/// 文字列がnullまたは空かどうかを判定するコンバーターです。
/// TextBoxの入力検証表示、ボタンの有効/無効制御、エラーメッセージ表示などに使用されます。
/// インスタンスはシングルトンパターンにより共有され、メモリ効率を向上させます。
/// </summary>
public class StringNullOrEmptyConverter : IValueConverter
{
    /// <summary>シングルトンインスタンス。</summary>
    public static readonly StringNullOrEmptyConverter Instance = new();

    /// <summary>
    /// 文字列がnullまたは空かを判定。
    /// </summary>
    /// <param name="value">検証対象の値。</param>
    /// <param name="targetType">ターゲット型（未使用）。</param>
    /// <param name="parameter">パラメータ（未使用）。</param>
    /// <param name="culture">カルチャ情報（未使用）。</param>
    /// <returns>nullまたは空の場合true。</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string);
    }

    /// <summary>
    /// 逆変換はサポート対象外です。
    /// 常に <see cref="DependencyProperty.UnsetValue"/> を返し、更新をキャンセルします。
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}
