using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Behaviors;

/// <summary>
/// テキストボックスでEscapeキーを押すとテキストをクリアするBehavior。
/// </summary>
public class EscapeToClearBehavior : Behavior<TextBox>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.KeyDown += OnKeyDown;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.KeyDown -= OnKeyDown;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            AssociatedObject.Clear();
            e.Handled = true;
        }
    }
}
