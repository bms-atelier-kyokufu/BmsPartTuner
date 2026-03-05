using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Behaviors;

/// <summary>
/// 指定桁数の0埋めを行うBehavior（例: PadLength=2の場合、"1" → "01"）。
/// フォーカスアウト時に適用されます。
/// </summary>
public class ZeroPadBehavior : Behavior<TextBox>
{
    /// <summary>
    /// 0埋めする桁数（デフォルト: 2）
    /// </summary>
    public static readonly DependencyProperty PadLengthProperty =
        DependencyProperty.Register(
            nameof(PadLength),
            typeof(int),
            typeof(ZeroPadBehavior),
            new PropertyMetadata(2));

    public int PadLength
    {
        get => (int)GetValue(PadLengthProperty);
        set => SetValue(PadLengthProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.LostFocus += OnLostFocus;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.LostFocus -= OnLostFocus;
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        var text = AssociatedObject.Text ?? string.Empty;
        var padLength = Math.Max(1, PadLength);

        if (text.Length > 0 && text.Length < padLength)
        {
            AssociatedObject.Text = text.PadLeft(padLength, '0');
        }
        else if (text.Length > padLength)
        {
            AssociatedObject.Text = text.Substring(0, padLength);
        }
    }
}
