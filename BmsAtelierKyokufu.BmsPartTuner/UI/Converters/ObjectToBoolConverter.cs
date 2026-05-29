using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace BmsAtelierKyokufu.BmsPartTuner.UI.Converters;

/// <summary>
/// オブジェクトがnullでないかどうかをboolに変換するコンバーターです。
/// オブジェクトの存在チェックをbool値に変換し、XAMLバインディングで使用可能にします。
/// Material Design 3 TextBoxのLeading/Trailing Iconの表示判定等に使用します。
/// Converterインスタンスはシングルトンパターンにより共有され、メモリ効率を向上させます。
/// </summary>
public class ObjectToBoolConverter : MarkupExtension, IValueConverter
{
    private static ObjectToBoolConverter? _instance;

    /// <summary>シングルトンインスタンス。</summary>
    public static ObjectToBoolConverter Instance => _instance ??= new ObjectToBoolConverter();

    /// <summary>
    /// XAMLからの参照時にシングルトンインスタンスを返す。
    /// </summary>
    /// <param name="serviceProvider">XAMLサービスプロバイダー。</param>
    /// <returns>Converterインスタンス。</returns>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return Instance;
    }

    /// <summary>
    /// オブジェクトがnullでないかを判定。
    /// </summary>
    /// <param name="value">検証対象の値。</param>
    /// <param name="targetType">ターゲット型（未使用）。</param>
    /// <param name="parameter">パラメータ（未使用）。</param>
    /// <param name="culture">カルチャ情報（未使用）。</param>
    /// <returns>nullでない場合true。</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null;
    }

    /// <summary>
    /// 逆変換を一部サポートします。
    /// falseが渡された場合、元のオブジェクトが存在しない状態とみなし、nullを返します。
    /// trueが渡された場合は生成すべきオブジェクトが不明なため、<see cref="Binding.DoNothing"/>を返します。
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && !b)
        {
#pragma warning disable CS8603 // Possible null reference return.
            return null;
#pragma warning restore CS8603 // Possible null reference return.
        }

        return Binding.DoNothing;
    }
}
