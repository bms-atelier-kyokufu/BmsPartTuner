using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace BmsAtelierKyokufu.BmsPartTuner.Converters;

/// <summary>
/// bool値を透明度に変換するコンバーター。
/// true: 0.5（半透明）, false: 1.0（完全に表示）
/// </summary>
/// <remarks>
/// 設定画面で、無効化された項目を視覚的に示すために使用します。
/// </remarks>
public class BoolToOpacityConverter : MarkupExtension, IValueConverter
{
    private static BoolToOpacityConverter? _instance;

    public static BoolToOpacityConverter Instance => _instance ??= new BoolToOpacityConverter();

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return Instance;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? 0.5 : 1.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double opacity)
        {
            return Math.Abs(opacity - 0.5) < 0.01;
        }
        return Binding.DoNothing;
    }
}
