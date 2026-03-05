using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Behaviors;

/// <summary>
/// IMEを無効化して非ASCII入力を抑止するBehavior。
/// 数字/英字のみを想定するテキストボックスに適用します。
/// </summary>
public class ImeOffBehavior : Behavior<TextBox>
{
    private bool? _originalImeEnabled;

    protected override void OnAttached()
    {
        base.OnAttached();
        _originalImeEnabled = InputMethod.GetIsInputMethodEnabled(AssociatedObject);
        InputMethod.SetIsInputMethodEnabled(AssociatedObject, false);
        // 念のため半角入力を優先
        InputMethod.SetPreferredImeState(AssociatedObject, InputMethodState.Off);
        InputMethod.SetPreferredImeConversionMode(AssociatedObject, ImeConversionModeValues.Alphanumeric);

        AssociatedObject.PreviewTextInput += OnPreviewTextInput;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        if (_originalImeEnabled.HasValue)
        {
            InputMethod.SetIsInputMethodEnabled(AssociatedObject, _originalImeEnabled.Value);
        }
        AssociatedObject.PreviewTextInput -= OnPreviewTextInput;
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // 1バイトASCII以外は遮断
        foreach (var ch in e.Text)
        {
            if (ch > 0x7F)
            {
                e.Handled = true;
                return;
            }
        }
    }
}
