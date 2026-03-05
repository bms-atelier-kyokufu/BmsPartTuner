using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace BmsAtelierKyokufu.BmsPartTuner.Converters;

/// <summary>
/// bool値を反転させるコンバーター。
/// </summary>
public class InverseBooleanConverter : MarkupExtension, IValueConverter
{
    private static InverseBooleanConverter? _instance;

    public static InverseBooleanConverter Instance => _instance ??= new InverseBooleanConverter();

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return Instance;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool booleanValue)
        {
            return !booleanValue;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool booleanValue)
        {
            return !booleanValue;
        }
        return false;
    }
}
