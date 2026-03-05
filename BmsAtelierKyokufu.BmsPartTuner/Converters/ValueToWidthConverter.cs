using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace BmsAtelierKyokufu.BmsPartTuner.Converters;

/// <summary>
/// 0.0～1.0の値を幅パーセンテージに変換するコンバーター。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>相関係数（0.0～1.0）をプログレスバーの幅に変換</item>
/// <item>App.xamlのグローバルリソースを優先して使用（インスタンス重複回避）</item>
/// </list>
/// 
/// <para>【用途】</para>
/// 相関係数入力TextBoxに、現在値を視覚的に表示するプログレスバー背景として使用。
/// 
/// <para>【変換式】</para>
/// 幅 = totalWidth × clamp(value, 0.0, 1.0)
/// 
/// <para>【Why MarkupExtension】</para>
/// <see cref="ProvideValue"/>をオーバーライドすることで、
/// XAMLで{local:ValueToWidthConverter}と記述した際に、
/// App.xamlのグローバルリソースを再利用できます。
/// 
/// <para>【メリット】</para>
/// <list type="bullet">
/// <item>Converterインスタンスの重複定義を防止</item>
/// <item>メモリ効率の向上</item>
/// <item>全参照を単一インスタンスに統一</item>
/// </list>
/// </remarks>
public class ValueToWidthConverter : MarkupExtension, IMultiValueConverter
{
    /// <summary>
    /// XAMLからの参照時にApp.xamlのグローバルリソースを優先して返す。
    /// </summary>
    /// <param name="serviceProvider">XAMLサービスプロバイダー。</param>
    /// <returns>Converterインスタンス。</returns>
    /// <remarks>
    /// App.xaml に "ValueToWidthConverter" キーでリソースが登録されている場合、
    /// それを返します。存在しない場合は自インスタンスを返します。
    /// </remarks>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        try
        {
            if (Application.Current != null && Application.Current.Resources.Contains("ValueToWidthConverter"))
            {
                var res = Application.Current.Resources["ValueToWidthConverter"];
                if (res is ValueToWidthConverter)
                    return res;
            }
        }
        catch
        {
        }

        return this;
    }

    /// <summary>
    /// 値と幅を受け取り、パーセンテージ幅に変換。
    /// </summary>
    /// <param name="values">values[0]: 値（0.0～1.0）、values[1]: 総幅。</param>
    /// <param name="targetType">ターゲット型（未使用）。</param>
    /// <param name="parameter">パラメータ（未使用）。</param>
    /// <param name="culture">カルチャ情報（未使用）。</param>
    /// <returns>計算された幅。</returns>
    /// <remarks>
    /// <para>【計算】</para>
    /// 1. 値を0.0～1.0の範囲にクランプ
    /// 2. 総幅を乗算
    /// 
    /// <para>【例】</para>
    /// values[0] = 0.75, values[1] = 200.0 → 150.0
    /// </remarks>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2 ||
            values[0] is not double value ||
            values[1] is not double totalWidth)
        {
            return 0.0;
        }

        var clampedValue = Math.Max(0.0, Math.Min(1.0, value));
        return totalWidth * clampedValue;
    }

    /// <summary>
    /// 逆変換（サポート対象外）。
    /// </summary>
    /// <remarks>
    /// 逆変換には総幅（totalWidth）の情報が必要ですが、
    /// ConvertBackの引数からは取得できないため、何も行いません（Binding.DoNothingを返します）。
    /// これにより、TwoWayバインディングで使用された場合でも例外が発生せず、
    /// ソースプロパティの更新が行われない安全な挙動となります。
    /// </remarks>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        var result = new object[targetTypes.Length];
        for (int i = 0; i < targetTypes.Length; i++)
        {
            result[i] = Binding.DoNothing;
        }
        return result;
    }
}
