using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Behaviors;

/// <summary>
/// テキストボックスに任意の単位サフィックスを自動付加するBehavior。
/// - フォーカス時: 単位を一時的に除去
/// - フォーカスアウト/初期表示: 単位を付加
/// </summary>
public class UnitSuffixBehavior : Behavior<TextBox>
{
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(UnitSuffixBehavior), new PropertyMetadata("%"));

    /// <summary>付与する単位（例: "%", "MB", "px"）</summary>
    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.GotFocus += OnGotFocus;
        AssociatedObject.LostFocus += OnLostFocus;
        AssociatedObject.Loaded += OnLoaded;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.GotFocus -= OnGotFocus;
        AssociatedObject.LostFocus -= OnLostFocus;
        AssociatedObject.Loaded -= OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyUnitIfNeeded();
    }

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        var unit = Unit ?? string.Empty;
        if (string.IsNullOrEmpty(unit)) return;

        var text = AssociatedObject.Text?.Trim() ?? string.Empty;
        if (text.EndsWith(unit))
        {
            AssociatedObject.Text = text.Substring(0, text.Length - unit.Length).TrimEnd();
            AssociatedObject.SelectAll();
        }
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        ApplyUnitIfNeeded();
    }

    private void ApplyUnitIfNeeded()
    {
        var unit = Unit ?? string.Empty;
        if (string.IsNullOrEmpty(unit)) return;

        var text = AssociatedObject.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(text) && !text.EndsWith(unit))
        {
            AssociatedObject.Text = $"{text}{unit}";
        }
    }
}
