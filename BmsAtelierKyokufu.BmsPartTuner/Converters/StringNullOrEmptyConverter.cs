using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BmsAtelierKyokufu.BmsPartTuner.Converters;

/// <summary>
/// 文字列がnullまたは空かどうかを判定するコンバーター。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// 文字列の存在チェックをbool値に変換し、XAMLバインディングで使用可能にします。
/// 
/// <para>【用途】</para>
/// <list type="bullet">
/// <item>TextBoxの入力検証表示</item>
/// <item>ボタンの有効/無効制御</item>
/// <item>エラーメッセージの表示/非表示</item>
/// </list>
/// 
/// <para>【変換ロジック】</para>
/// string.IsNullOrEmpty(value) → true/false
/// 
/// <para>【Singletonパターン】</para>
/// <see cref="Instance"/>により、アプリケーション全体で
/// 単一のインスタンスを共有します。
/// </remarks>
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
    /// 逆変換（未サポート）。
    /// </summary>
    /// <remarks>
    /// 文字列がnullまたは空かどうかの判定結果から、元の文字列を復元することは不可能なため、
    /// <see cref="DependencyProperty.UnsetValue"/>を返します。
    /// これにより、TwoWayバインディングで使用された場合でも例外が発生せず、何も更新されません。
    /// </remarks>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}
