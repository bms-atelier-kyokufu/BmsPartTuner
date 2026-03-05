using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.UI
{
    /// <summary>
    /// TextBox用の添付プロパティヘルパー
    /// WPFのMVVMパターンに従い、継承せずに機能を拡張
    /// </summary>
    public static class TextBoxHelper
    {
        /// <summary>
        /// 角の丸みを設定する添付プロパティ
        /// </summary>
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.RegisterAttached(
                "CornerRadius",
                typeof(CornerRadius),
                typeof(TextBoxHelper),
                new PropertyMetadata(new CornerRadius(0)));

        public static void SetCornerRadius(UIElement element, CornerRadius value)
            => element.SetValue(CornerRadiusProperty, value);

        public static CornerRadius GetCornerRadius(UIElement element)
            => (CornerRadius)element.GetValue(CornerRadiusProperty);

        /// <summary>
        /// プレースホルダーテキストを設定する添付プロパティ
        /// </summary>
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.RegisterAttached(
                "Placeholder",
                typeof(string),
                typeof(TextBoxHelper),
                new PropertyMetadata(string.Empty));

        public static void SetPlaceholder(UIElement element, string value)
            => element.SetValue(PlaceholderProperty, value);

        public static string GetPlaceholder(UIElement element)
            => (string)element.GetValue(PlaceholderProperty);

        #region M3 Floating Label Properties

        /// <summary>
        /// フローティングラベルとして表示するテキスト
        /// M3 Outlined Text Field仕様: 未入力時は中央、入力時/フォーカス時は上部に移動
        /// </summary>
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.RegisterAttached(
                "Label",
                typeof(string),
                typeof(TextBoxHelper),
                new PropertyMetadata(string.Empty));

        public static void SetLabel(UIElement element, string value)
            => element.SetValue(LabelProperty, value);

        public static string GetLabel(UIElement element)
            => (string)element.GetValue(LabelProperty);

        /// <summary>
        /// テキストボックス左側に表示するアイコン（Leading Icon）
        /// </summary>
        public static readonly DependencyProperty LeadingIconProperty =
            DependencyProperty.RegisterAttached(
                "LeadingIcon",
                typeof(object),
                typeof(TextBoxHelper),
                new PropertyMetadata(null));

        public static void SetLeadingIcon(UIElement element, object value)
            => element.SetValue(LeadingIconProperty, value);

        public static object GetLeadingIcon(UIElement element)
            => element.GetValue(LeadingIconProperty);

        /// <summary>
        /// テキストボックス右側に表示するアイコン（Trailing Icon）
        /// </summary>
        public static readonly DependencyProperty TrailingIconProperty =
            DependencyProperty.RegisterAttached(
                "TrailingIcon",
                typeof(object),
                typeof(TextBoxHelper),
                new PropertyMetadata(null));

        public static void SetTrailingIcon(UIElement element, object value)
            => element.SetValue(TrailingIconProperty, value);

        public static object GetTrailingIcon(UIElement element)
            => element.GetValue(TrailingIconProperty);

        /// <summary>
        /// テキストボックス下部に表示する補助テキスト（Supporting Text）
        /// エラー時はエラーメッセージが優先される
        /// </summary>
        public static readonly DependencyProperty SupportingTextProperty =
            DependencyProperty.RegisterAttached(
                "SupportingText",
                typeof(string),
                typeof(TextBoxHelper),
                new PropertyMetadata(string.Empty));

        public static void SetSupportingText(UIElement element, string value)
            => element.SetValue(SupportingTextProperty, value);

        public static string GetSupportingText(UIElement element)
            => (string)element.GetValue(SupportingTextProperty);

        #endregion

        /// <summary>
        /// アイコンを設定する添付プロパティ
        /// </summary>
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.RegisterAttached(
                "Icon",
                typeof(string),
                typeof(TextBoxHelper),
                new PropertyMetadata(string.Empty));

        public static void SetIcon(UIElement element, string value)
            => element.SetValue(IconProperty, value);

        public static string GetIcon(UIElement element)
            => (string)element.GetValue(IconProperty);

        /// <summary>
        /// ブラウズコマンドを設定する添付プロパティ（ファイル選択ダイアログ用）
        /// </summary>
        public static readonly DependencyProperty BrowseCommandProperty =
            DependencyProperty.RegisterAttached(
                "BrowseCommand",
                typeof(ICommand),
                typeof(TextBoxHelper),
                new PropertyMetadata(null));

        public static void SetBrowseCommand(UIElement element, ICommand value)
            => element.SetValue(BrowseCommandProperty, value);

        public static ICommand GetBrowseCommand(UIElement element)
            => (ICommand)element.GetValue(BrowseCommandProperty);

        /// <summary>
        /// クリアボタンの有効化を設定する添付プロパティ
        /// </summary>
        public static readonly DependencyProperty EnableClearButtonProperty =
            DependencyProperty.RegisterAttached(
                "EnableClearButton",
                typeof(bool),
                typeof(TextBoxHelper),
                new PropertyMetadata(false, OnEnableClearButtonChanged));

        public static void SetEnableClearButton(TextBox element, bool value)
            => element.SetValue(EnableClearButtonProperty, value);

        public static bool GetEnableClearButton(TextBox element)
            => (bool)element.GetValue(EnableClearButtonProperty);

        private static void OnEnableClearButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox textBox) return;

            if ((bool)e.NewValue)
            {
                textBox.Loaded -= TextBox_Loaded;
                textBox.Loaded += TextBox_Loaded;
            }
            else
            {
                textBox.Loaded -= TextBox_Loaded;
            }
        }

        private static void TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            // テンプレートが適用されるまで待機
            textBox.ApplyTemplate();

            if (textBox.Template?.FindName("PART_ClearButton", textBox) is Button clearButton)
            {
                // 既存のハンドラーを削除してから追加（重複防止）
                clearButton.Click -= ClearButton_Click;
                clearButton.Click += ClearButton_Click;
            }
        }

        private static void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // ボタンの親要素からTextBoxを探す
                var textBox = FindParent<TextBox>(button);
                if (textBox != null)
                {
                    textBox.Clear();
                    textBox.Focus();
                }
            }
        }

        /// <summary>
        /// 指定した型の親要素を検索する
        /// </summary>
        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null) return null;

            if (parentObject is T parent)
                return parent;

            return FindParent<T>(parentObject);
        }

        #region Speed Badge Properties

        /// <summary>
        /// スピードバッジのテキストを設定する添付プロパティ
        /// </summary>
        public static readonly DependencyProperty BadgeTextProperty =
            DependencyProperty.RegisterAttached(
                "BadgeText",
                typeof(string),
                typeof(TextBoxHelper),
                new PropertyMetadata(string.Empty));

        public static void SetBadgeText(UIElement element, string value)
            => element.SetValue(BadgeTextProperty, value);

        public static string GetBadgeText(UIElement element)
            => (string)element.GetValue(BadgeTextProperty);

        /// <summary>
        /// スピードバッジの背景色を設定する添付プロパティ
        /// </summary>
        public static readonly DependencyProperty BadgeBrushProperty =
            DependencyProperty.RegisterAttached(
                "BadgeBrush",
                typeof(Brush),
                typeof(TextBoxHelper),
                new PropertyMetadata(null));

        public static void SetBadgeBrush(UIElement element, Brush value)
            => element.SetValue(BadgeBrushProperty, value);

        public static Brush GetBadgeBrush(UIElement element)
            => (Brush)element.GetValue(BadgeBrushProperty);

        #endregion

        #region AI Optimization Properties

        /// <summary>
        /// AI最適化ボタンを表示するかどうかを設定する添付プロパティ
        /// </summary>
        public static readonly DependencyProperty HasAiOptimizationProperty =
            DependencyProperty.RegisterAttached(
                "HasAiOptimization",
                typeof(bool),
                typeof(TextBoxHelper),
                new PropertyMetadata(false));

        public static void SetHasAiOptimization(UIElement element, bool value)
            => element.SetValue(HasAiOptimizationProperty, value);

        public static bool GetHasAiOptimization(UIElement element)
            => (bool)element.GetValue(HasAiOptimizationProperty);

        /// <summary>
        /// AI最適化実行コマンドを設定する添付プロパティ
        /// </summary>
        public static readonly DependencyProperty AiOptimizationCommandProperty =
            DependencyProperty.RegisterAttached(
                "AiOptimizationCommand",
                typeof(ICommand),
                typeof(TextBoxHelper),
                new PropertyMetadata(null));

        public static void SetAiOptimizationCommand(UIElement element, ICommand value)
            => element.SetValue(AiOptimizationCommandProperty, value);

        public static ICommand GetAiOptimizationCommand(UIElement element)
            => (ICommand)element.GetValue(AiOptimizationCommandProperty);

        #endregion

        #region Floating Label Pinned Property

        /// <summary>
        /// フローティングラベルを上部に固定するかどうかを設定する添付プロパティ
        /// True: ラベルを常に上部に固定表示（SearchTextBox、R2TextBox等で使用）
        /// False: 通常のフローティング動作（未入力時は中央、入力時/フォーカス時は上部）
        /// </summary>
        public static readonly DependencyProperty IsLabelPinnedProperty =
            DependencyProperty.RegisterAttached(
                "IsLabelPinned",
                typeof(bool),
                typeof(TextBoxHelper),
                new PropertyMetadata(false));

        public static void SetIsLabelPinned(UIElement element, bool value)
            => element.SetValue(IsLabelPinnedProperty, value);

        public static bool GetIsLabelPinned(UIElement element)
            => (bool)element.GetValue(IsLabelPinnedProperty);

        #endregion
    }
}
