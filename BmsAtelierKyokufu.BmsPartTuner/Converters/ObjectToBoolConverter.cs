using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace BmsAtelierKyokufu.BmsPartTuner.Converters;

/// <summary>
/// オブジェクトがnullでないかどうかをboolに変換するコンバーター。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// オブジェクトの存在チェックをbool値に変換し、XAMLバインディングで使用可能にします。
/// 
/// <para>【用途】</para>
/// Material Design 3 TextBoxのLeading/Trailing Iconの表示判定に使用。
/// アイコンが設定されている（null以外）場合のみ表示します。
/// 
/// <para>【変換ロジック】</para>
/// value != null → true/false
/// 
/// <para>【Singletonパターン + MarkupExtension】</para>
/// <list type="bullet">
/// <item><see cref="Instance"/>により単一インスタンスを共有</item>
/// <item><see cref="ProvideValue"/>により、XAMLで{local:ObjectToBoolConverter}と記述可能</item>
/// </list>
/// 
/// <para>【Why Singleton】</para>
/// Converterはステートレスなため、インスタンスを共有することで
/// メモリ効率を向上させます。
/// </remarks>
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
    /// 逆変換（一部実装）。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>falseの場合：元のオブジェクトが存在しない状態とみなし、nullを返します。</item>
    /// <item>trueの場合：どのようなオブジェクトを生成すべきか不明なため、<see cref="Binding.DoNothing"/>を返します。</item>
    /// </list>
    /// </remarks>
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
