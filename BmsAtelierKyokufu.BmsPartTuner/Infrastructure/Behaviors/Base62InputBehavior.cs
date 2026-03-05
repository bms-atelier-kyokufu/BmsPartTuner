using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Behaviors;

/// <summary>
/// 62進数入力（0-9, A-Z, a-z）を許可するBehavior。
/// BMSの定義番号は62進数（0-9, A-Z, a-z）で表現されます。
/// </summary>
public partial class Base62InputBehavior : Behavior<TextBox>
{
    [GeneratedRegex("^[0-9A-Za-z]*$")]
    private static partial Regex Base62Regex();

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewTextInput += OnPreviewTextInput;
        AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
        DataObject.AddPastingHandler(AssociatedObject, OnPasting);
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.PreviewTextInput -= OnPreviewTextInput;
        AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
        DataObject.RemovePastingHandler(AssociatedObject, OnPasting);
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // 入力が62進数文字（0-9, A-Z, a-z）でない場合は拒否
        if (!Base62Regex().IsMatch(e.Text))
        {
            e.Handled = true;
            return;
        }

        // MaxLength チェック（2桁制限）
        var textBox = (TextBox)sender;
        var currentText = textBox.Text;
        var selectionLength = textBox.SelectionLength;
        var newLength = currentText.Length - selectionLength + e.Text.Length;

        if (newLength > 2)
        {
            e.Handled = true;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // スペースキーを無効化
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    private void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)e.DataObject.GetData(typeof(string));

            // ペーストされたテキストが62進数文字のみでない場合は拒否
            if (!Base62Regex().IsMatch(text))
            {
                e.CancelCommand();
                return;
            }

            // MaxLength チェック
            var textBox = (TextBox)sender;
            var currentText = textBox.Text;
            var selectionLength = textBox.SelectionLength;
            var newLength = currentText.Length - selectionLength + text.Length;

            if (newLength > 2)
            {
                e.CancelCommand();
            }
        }
        else
        {
            e.CancelCommand();
        }
    }
}
