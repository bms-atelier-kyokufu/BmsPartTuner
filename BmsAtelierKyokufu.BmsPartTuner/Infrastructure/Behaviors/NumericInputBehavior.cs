using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Behaviors
{
    /// <summary>
    /// 数値入力のみを許可するビヘイビア
    /// TextBoxに添付して使用し、継承なしで機能を拡張
    /// </summary>
    public partial class NumericInputBehavior : Behavior<TextBox>
    {
        private static readonly Regex _numericRegex = NumericRegex();

        /// <summary>
        /// 小数点を許可するかどうか
        /// </summary>
        public static readonly DependencyProperty AllowDecimalProperty =
            DependencyProperty.Register(
                nameof(AllowDecimal),
                typeof(bool),
                typeof(NumericInputBehavior),
                new PropertyMetadata(true));

        public bool AllowDecimal
        {
            get => (bool)GetValue(AllowDecimalProperty);
            set => SetValue(AllowDecimalProperty, value);
        }

        /// <summary>
        /// マイナス記号を許可するかどうか
        /// </summary>
        public static readonly DependencyProperty AllowNegativeProperty =
            DependencyProperty.Register(
                nameof(AllowNegative),
                typeof(bool),
                typeof(NumericInputBehavior),
                new PropertyMetadata(true));

        public bool AllowNegative
        {
            get => (bool)GetValue(AllowNegativeProperty);
            set => SetValue(AllowNegativeProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewTextInput += OnPreviewTextInput;
            DataObject.AddPastingHandler(AssociatedObject, OnPaste);
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.PreviewTextInput -= OnPreviewTextInput;
            DataObject.RemovePastingHandler(AssociatedObject, OnPaste);
        }

        private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsValidInput(e.Text);
        }

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                var text = (string)e.DataObject.GetData(typeof(string));
                if (!IsValidInput(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        internal bool IsValidInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return true;

            // 基本の数値チェック（0-9のみ）
            if (!_numericRegex.IsMatch(input))
                return false;

            // 小数点のチェック
            if (!AllowDecimal && input.Contains('.'))
                return false;

            // マイナス記号のチェック
            if (!AllowNegative && input.Contains('-'))
                return false;

            return true;
        }

        [GeneratedRegex(@"^[0-9.\-]+$")]
        private static partial Regex NumericRegex();
    }
}
