using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Xaml.Behaviors;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Behaviors
{
    /// <summary>
    /// TextBoxにFloating Label機能を追加するBehavior
    /// M3 Outlined Text Field仕様に準拠
    /// 初期位置Y=18（中央）、Floating時Y=8（上部に適度な距離）
    /// </summary>
    public class FloatingLabelBehavior : Behavior<TextBox>
    {
        private Border? _labelContainer;
        private TextBlock? _labelTextBlock;
        private TextBlock? _watermarkHost;
        private TranslateTransform? _labelTranslate;
        private ScaleTransform? _labelScale;
        private bool _isFloated = false;

        #region Dependency Properties

        public static readonly DependencyProperty LabelTextProperty =
            DependencyProperty.Register(
                nameof(LabelText),
                typeof(string),
                typeof(FloatingLabelBehavior),
                new PropertyMetadata(string.Empty, OnLabelTextChanged));

        public string LabelText
        {
            get => (string)GetValue(LabelTextProperty);
            set => SetValue(LabelTextProperty, value);
        }

        private static void OnLabelTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FloatingLabelBehavior behavior && behavior._labelTextBlock != null)
            {
                behavior._labelTextBlock.Text = e.NewValue as string ?? string.Empty;
            }
        }

        #endregion

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.Loaded += OnTextBoxLoaded;
            AssociatedObject.GotFocus += OnTextBoxGotFocus;
            AssociatedObject.LostFocus += OnTextBoxLostFocus;
            AssociatedObject.TextChanged += OnTextBoxTextChanged;
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.Loaded -= OnTextBoxLoaded;
                AssociatedObject.GotFocus -= OnTextBoxGotFocus;
                AssociatedObject.LostFocus -= OnTextBoxLostFocus;
                AssociatedObject.TextChanged -= OnTextBoxTextChanged;
            }

            base.OnDetaching();
        }

        private void OnTextBoxLoaded(object sender, RoutedEventArgs e)
        {
            InitializeFloatingLabel();

            // IsLabelPinnedがTrueの場合は初期状態でfloat
            bool isPinned = UI.TextBoxHelper.GetIsLabelPinned(AssociatedObject);
            bool shouldFloat = isPinned || AssociatedObject.IsFocused || !string.IsNullOrEmpty(AssociatedObject.Text);
            AnimateLabel(shouldFloat, animated: false);
        }

        private void InitializeFloatingLabel()
        {
            if (AssociatedObject.Template == null) return;

            _labelContainer = AssociatedObject.Template.FindName("PART_FloatingLabelContainer", AssociatedObject) as Border;
            _watermarkHost = AssociatedObject.Template.FindName("PART_WatermarkHost", AssociatedObject) as TextBlock;

            if (_labelContainer != null)
            {
                _labelTextBlock = _labelContainer.Child as TextBlock;
                if (_labelTextBlock != null && _labelContainer.RenderTransform is TransformGroup transformGroup)
                {
                    _labelScale = transformGroup.Children[0] as ScaleTransform;
                    _labelTranslate = transformGroup.Children[1] as TranslateTransform;
                }
            }
        }

        private void OnTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            AnimateLabel(toFloated: true);
        }

        private void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            // IsLabelPinnedがTrueの場合は常にfloat状態を維持
            bool isPinned = UI.TextBoxHelper.GetIsLabelPinned(AssociatedObject);

            // テキストが空でもpinnedならfloat状態を維持、そうでなければ中央に戻す
            if (!isPinned && string.IsNullOrEmpty(AssociatedObject.Text))
            {
                AnimateLabel(toFloated: false);
            }
        }

        private void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateLabelState(animated: true);
        }

        private void UpdateLabelState(bool animated)
        {
            // IsLabelPinnedがTrueの場合は常にfloat状態を維持
            bool isPinned = UI.TextBoxHelper.GetIsLabelPinned(AssociatedObject);
            bool shouldFloat = isPinned || AssociatedObject.IsFocused || !string.IsNullOrEmpty(AssociatedObject.Text);

            if (shouldFloat != _isFloated)
            {
                AnimateLabel(shouldFloat, animated);
            }
        }

        private void AnimateLabel(bool toFloated, bool animated = true)
        {
            if (_labelTranslate == null || _labelScale == null) return;

            _isFloated = toFloated;

            // Floating時Y=0（上部、ボーダーに重なる）
            // 非Floating時：テキストボックスの実際の高さから縦方向中央を動的に計算
            double targetY;
            if (toFloated)
            {
                targetY = 0;
            }
            else
            {
                // AssociatedObjectの実際の高さから中央位置を計算
                // テンプレート内のBorderを取得して、その高さから計算
                var border = AssociatedObject.Template?.FindName("PART_Border", AssociatedObject) as Border
                          ?? AssociatedObject.Template?.FindName("PART_MainBorder", AssociatedObject) as Border;

                if (border != null && border.ActualHeight > 0)
                {
                    targetY = border.ActualHeight / 2.0;
                }
                else
                {
                    // フォールバック値：標準的な56pxの高さを想定
                    targetY = 28;
                }
            }

            double targetScale = toFloated ? 0.75 : 1.0;
            double targetWatermarkOpacity = toFloated && AssociatedObject.IsFocused ? 0.5 : 0.0;

            var duration = animated ? TimeSpan.FromMilliseconds(200) : TimeSpan.Zero;
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

            // Animate Y position
            var translateAnimation = new DoubleAnimation
            {
                To = targetY,
                Duration = duration,
                EasingFunction = easing
            };
            _labelTranslate.BeginAnimation(TranslateTransform.YProperty, translateAnimation);

            // Animate scale
            var scaleAnimation = new DoubleAnimation
            {
                To = targetScale,
                Duration = duration,
                EasingFunction = easing
            };
            _labelScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            _labelScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);

            // Animate watermark opacity (show only when focused)
            if (_watermarkHost != null)
            {
                var watermarkAnimation = new DoubleAnimation
                {
                    To = targetWatermarkOpacity,
                    Duration = duration,
                    EasingFunction = easing
                };
                _watermarkHost.BeginAnimation(TextBlock.OpacityProperty, watermarkAnimation);
            }

            // Update label color based on focus state
            if (_labelTextBlock != null)
            {
                var targetBrush = AssociatedObject.IsFocused
                    ? Application.Current.FindResource("M3PrimaryBrush") as Brush
                    : Application.Current.FindResource("M3OnSurfaceVariantBrush") as Brush;

                if (targetBrush != null)
                {
                    _labelTextBlock.Foreground = targetBrush;
                }
            }
        }
    }
}
