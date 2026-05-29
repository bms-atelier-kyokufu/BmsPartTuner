using System.Windows.Data;
using System.Windows.Markup;

namespace BmsAtelierKyokufu.BmsPartTuner.UI.Converters;

/// <summary>
/// 0.0～1.0の値を指定された総幅に基づくプログレスバー等の幅に変換するコンバーターです。
/// 相関係数（0.0～1.0）をUI表示用の幅に変換するために使用します。
/// {local:ValueToWidthConverter}としてXAMLで記述した際にApp.xamlのグローバルリソースを
/// 優先利用することで、インスタンスの重複定義を防ぎ、メモリ効率を向上させています。
/// </summary>
public class ValueToWidthConverter : MarkupExtension, IMultiValueConverter
{
    /// <summary>
    /// XAMLからの参照時にApp.xamlのグローバルリソース（キー "ValueToWidthConverter"）を優先して返します。
    /// 存在しない場合は自身のインスタンスを返します。
    /// </summary>
    /// <param name="serviceProvider">XAMLサービスプロバイダー。</param>
    /// <returns>Converterインスタンス。</returns>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        try
        {
            if (Application.Current?.Resources.Contains("ValueToWidthConverter") is true)
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
    /// 値と総幅を受け取り、パーセンテージ幅に変換します。
    /// 値は自動的に 0.0 ～ 1.0 の範囲にクランプされます。
    /// </summary>
    /// <param name="values">values[0]: 値（0.0～1.0）、values[1]: 総幅。</param>
    /// <param name="targetType">ターゲット型（未使用）。</param>
    /// <param name="parameter">パラメータ（未使用）。</param>
    /// <param name="culture">カルチャ情報（未使用）。</param>
    /// <returns>計算された幅。</returns>
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
    /// 逆変換はサポート対象外です。
    /// ConvertBackの引数から総幅（totalWidth）が取得できないため、<see cref="Binding.DoNothing"/>を返します。
    /// これにより、TwoWayバインディングで使用された場合でも安全にソースの更新をキャンセルします。
    /// </summary>
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
