using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Behaviors
{
    /// <summary>
    /// テキストボックスにバーチャルスライダー機能を追加するBehavior
    /// マウスドラッグで数値を増減可能にする（Shift: 高速、Ctrl: 微調整）
    /// </summary>
    public class VirtualSliderBehavior : Behavior<TextBox>
    {
        #region Dependency Properties

        /// <summary>
        /// ドラッグ操作の感度（デフォルト: 0.01）
        /// </summary>
        public static readonly DependencyProperty SensitivityProperty =
            DependencyProperty.Register(
                nameof(Sensitivity),
                typeof(double),
                typeof(VirtualSliderBehavior),
                new PropertyMetadata(0.01));

        public double Sensitivity
        {
            get => (double)GetValue(SensitivityProperty);
            set => SetValue(SensitivityProperty, value);
        }

        /// <summary>
        /// 最小値（デフォルト: 0.0）
        /// </summary>
        public static readonly DependencyProperty MinValueProperty =
            DependencyProperty.Register(
                nameof(MinValue),
                typeof(double),
                typeof(VirtualSliderBehavior),
                new PropertyMetadata(0.0));

        public double MinValue
        {
            get => (double)GetValue(MinValueProperty);
            set => SetValue(MinValueProperty, value);
        }

        /// <summary>
        /// 最大値（デフォルト: 1.0）
        /// </summary>
        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register(
                nameof(MaxValue),
                typeof(double),
                typeof(VirtualSliderBehavior),
                new PropertyMetadata(1.0));

        public double MaxValue
        {
            get => (double)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        /// <summary>
        /// 整数モード（デフォルト: false）
        /// trueの場合、小数点なしの整数として扱う
        /// </summary>
        public static readonly DependencyProperty IsIntegerModeProperty =
            DependencyProperty.Register(
                nameof(IsIntegerMode),
                typeof(bool),
                typeof(VirtualSliderBehavior),
                new PropertyMetadata(false));

        public bool IsIntegerMode
        {
            get => (bool)GetValue(IsIntegerModeProperty);
            set => SetValue(IsIntegerModeProperty, value);
        }

        #endregion

        #region Struct

        /// <summary>
        /// バーチャルスライダーの設定を格納する構造体
        /// </summary>
        private static class SliderSettings
        {
            /// <summary>
            /// ドラッグ速度に応じた整数モード用のピクセル数/ステップを取得します
            /// </summary>
            /// <param name="speed">ドラッグ速度</param>
            /// <returns>1ステップに必要なピクセル数</returns>
            public static double GetPixelsPerStep(DragSpeed speed) => speed switch
            {
                DragSpeed.Fast => Core.AppConstants.UI.VirtualSlider.IntegerPixelsPerStepFast,
                DragSpeed.Fine => Core.AppConstants.UI.VirtualSlider.IntegerPixelsPerStepFine,
                _ => Core.AppConstants.UI.VirtualSlider.IntegerPixelsPerStepNormal
            };

            /// <summary>
            /// ドラッグ速度に応じた小数モード用の乗数を取得します
            /// </summary>
            /// <param name="speed">ドラッグ速度</param>
            /// <returns>感度に掛け合わせる乗数</returns>
            public static double GetMultiplier(DragSpeed speed) => speed switch
            {
                DragSpeed.Fast => Core.AppConstants.UI.VirtualSlider.DecimalMultiplierFast,
                DragSpeed.Fine => Core.AppConstants.UI.VirtualSlider.DecimalMultiplierFine,
                DragSpeed.Normal => Core.AppConstants.UI.VirtualSlider.DecimalMultiplierNormal,
                _ => Core.AppConstants.UI.VirtualSlider.DecimalMultiplierNormal
            };
        }

        #endregion

        #region Fields

        /// <summary>
        /// ドラッグ操作中かどうかを示すフラグ
        /// </summary>
        private bool _isDragging;

        /// <summary>
        /// ドラッグ開始時のマウス位置
        /// </summary>
        private Point _dragStartPos;

        /// <summary>
        /// ドラッグ開始時の値
        /// </summary>
        private double _dragStartValue;

        /// <summary>
        /// 最後に値を適用したX座標（累積計算用）
        /// </summary>
        private double _lastAppliedX;

        /// <summary>
        /// ドラッグ前の元のカーソル
        /// </summary>
        private Cursor? _originalCursor;

        /// <summary>
        /// ドラッグ前の元のボーダーブラシ
        /// </summary>
        private Brush? _originalBorderBrush;

        /// <summary>
        /// ドラッグ前の元のボーダー太さ
        /// </summary>
        private Thickness _originalBorderThickness;

        /// <summary>
        /// ドラッグ速度の種類
        /// </summary>
        internal enum DragSpeed
        {
            /// <summary>
            /// 通常速度
            /// </summary>
            Normal,
            /// <summary>
            /// 高速（Shiftキー）
            /// </summary>
            Fast,
            /// <summary>
            /// 微調整（Ctrlキー）
            /// </summary>
            Fine
        }

        /// <summary>
        /// 修飾キーからドラッグ速度を決定します
        /// </summary>
        /// <param name="modifiers">現在の修飾キー状態</param>
        /// <returns>対応するドラッグ速度</returns>
        private DragSpeed GetDetermineSpeed(ModifierKeys modifiers) => modifiers switch
        {
            _ when modifiers.HasFlag(ModifierKeys.Shift) => DragSpeed.Fast,
            _ when modifiers.HasFlag(ModifierKeys.Control) => DragSpeed.Fine,
            _ => DragSpeed.Normal
        };

        #endregion

        #region Behavior Overrides

        /// <summary>
        /// Behaviorがアタッチされたときにイベントハンドラを登録します
        /// </summary>
        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject != null)
            {
                AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
                AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
                AssociatedObject.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
                AssociatedObject.MouseLeave += OnMouseLeave;
                AssociatedObject.MouseEnter += OnMouseEnter;
                AssociatedObject.GotKeyboardFocus += OnGotKeyboardFocus;
                AssociatedObject.LostKeyboardFocus += OnLostKeyboardFocus;
            }
        }

        /// <summary>
        /// Behaviorがデタッチされるときにイベントハンドラを解除します
        /// </summary>
        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
                AssociatedObject.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
                AssociatedObject.MouseLeave -= OnMouseLeave;
                AssociatedObject.MouseEnter -= OnMouseEnter;
                AssociatedObject.GotKeyboardFocus -= OnGotKeyboardFocus;
                AssociatedObject.LostKeyboardFocus -= OnLostKeyboardFocus;
            }

            base.OnDetaching();
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// キーボードフォーカスを取得したときにテキスト全体を選択します
        /// </summary>
        /// <param name="sender">イベント送信元</param>
        /// <param name="e">イベント引数</param>
        private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            AssociatedObject?.SelectAll();
        }

        /// <summary>
        /// キーボードフォーカスを失ったときに視覚フィードバックをリセットします
        /// </summary>
        /// <param name="sender">イベント送信元</param>
        /// <param name="e">イベント引数</param>
        private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (_originalCursor != null && AssociatedObject != null)
            {
                AssociatedObject.Cursor = _originalCursor;
            }

            ResetVisualFeedback();
            ResetBadge();
        }

        /// <summary>
        /// マウスがコントロールに入ったときにカーソルを左右矢印に変更します
        /// </summary>
        /// <param name="sender">イベント送信元</param>
        /// <param name="e">イベント引数</param>
        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (AssociatedObject != null && !_isDragging)
            {
                if (_originalCursor == null)
                {
                    _originalCursor = AssociatedObject.Cursor;
                }
                AssociatedObject.Cursor = Cursors.SizeWE;
            }
        }

        /// <summary>
        /// マウス左ボタンが押されたときにドラッグ操作を開始します
        /// </summary>
        /// <param name="sender">イベント送信元</param>
        /// <param name="e">イベント引数</param>
        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (AssociatedObject == null) return;

            // ボタン上のクリックは除外（AI最適化ボタンなど）
            if (e.OriginalSource is DependencyObject source)
            {
                var button = FindVisualParent<Button>(source);
                if (button != null)
                {
                    // ボタン上でのクリックはドラッグを開始しない
                    return;
                }
            }

            if (e.ClickCount == 2)
            {
                AssociatedObject.Focus();
                AssociatedObject.SelectAll();
                e.Handled = true;
                return;
            }

            _dragStartPos = e.GetPosition(AssociatedObject);
            _lastAppliedX = _dragStartPos.X;

            if (double.TryParse(AssociatedObject.Text, out double currentValue))
            {
                _dragStartValue = currentValue;
            }
            else
            {
                _dragStartValue = IsIntegerMode ? MinValue : 0.0;
            }

            _isDragging = true;

            // カーソルは既にSizeWEになっているはず

            SaveOriginalBorder();

            AssociatedObject.Focus();
            AssociatedObject.CaptureMouse();

            e.Handled = true;
        }

        /// <summary>
        /// ビジュアルツリーを上方向に探索して指定した型の親要素を見つけます
        /// </summary>
        /// <typeparam name="T">探索する型</typeparam>
        /// <param name="element">開始要素</param>
        /// <returns>見つかった親要素、見つからない場合はnull</returns>
        private static T? FindVisualParent<T>(DependencyObject element) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is T target)
                {
                    return target;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        /// <summary>
        /// マウス移動時にドラッグ操作を処理し、値を更新します
        /// </summary>
        /// <param name="sender">イベント送信元</param>
        /// <param name="e">イベント引数</param>
        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || AssociatedObject == null) return;

            var currentPos = e.GetPosition(AssociatedObject);
            var totalDeltaX = currentPos.X - _dragStartPos.X;

            if (Math.Abs(totalDeltaX) > Core.AppConstants.UI.VirtualSlider.DragThreshold)
            {
                var modifiers = Keyboard.Modifiers;
                var speed = GetDetermineSpeed(modifiers);

                UpdateVisualFeedback(speed);
                UpdateBadge(speed);

                double newValue;

                if (IsIntegerMode)
                {
                    // 整数モード: ピクセル数に基づいてステップを計算
                    newValue = CalculateIntegerValue(currentPos.X, speed);
                }
                else
                {
                    // 小数モード: 従来の乗数ベース計算
                    var multiplier = SliderSettings.GetMultiplier(speed);
                    var change = totalDeltaX * Sensitivity * multiplier;
                    newValue = _dragStartValue + change;
                    newValue = Math.Round(newValue, 2);
                }

                newValue = Math.Max(MinValue, Math.Min(MaxValue, newValue));

                if (IsIntegerMode)
                {
                    AssociatedObject.Text = ((int)newValue).ToString();
                }
                else
                {
                    AssociatedObject.Text = newValue.ToString("F2");
                }

                e.Handled = true;
            }
        }

        /// <summary>
        /// マウス左ボタンが離されたときにドラッグ操作を終了します
        /// </summary>
        /// <param name="sender">イベント送信元</param>
        /// <param name="e">イベント引数</param>
        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                EndDrag();
                e.Handled = true;
            }
        }

        /// <summary>
        /// マウスがコントロールから離れたときにドラッグ操作を終了します
        /// </summary>
        /// <param name="sender">イベント送信元</param>
        /// <param name="e">イベント引数</param>
        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                EndDrag();
            }
            else if (AssociatedObject != null && _originalCursor != null)
            {
                // ドラッグ中でなければカーソルを元に戻す
                AssociatedObject.Cursor = _originalCursor;
                _originalCursor = null;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 整数モード用: ドラッグ距離からステップ数を計算します
        /// </summary>
        /// <param name="currentX">現在のマウスX座標</param>
        /// <param name="speed">ドラッグ速度</param>
        /// <returns>計算された新しい値</returns>
        private double CalculateIntegerValue(double currentX, DragSpeed speed)
        {
            var pixelsPerStep = SliderSettings.GetPixelsPerStep(speed);
            var totalDeltaX = currentX - _dragStartPos.X;

            // ピクセル数をステップ数に変換（切り捨て）
            var steps = (int)(totalDeltaX / pixelsPerStep);

            return _dragStartValue + steps;
        }

        /// <summary>
        /// テキストボックスのボーダー要素の元の状態を保存します
        /// </summary>
        private void SaveOriginalBorder()
        {
            if (AssociatedObject?.Template?.FindName("PART_Border", AssociatedObject) is Border border)
            {
                _originalBorderBrush = border.BorderBrush;
                _originalBorderThickness = border.BorderThickness;
            }
        }

        /// <summary>
        /// ドラッグ速度に応じてボーダーの視覚フィードバックを更新します
        /// </summary>
        /// <param name="speed">現在のドラッグ速度</param>
        private void UpdateVisualFeedback(DragSpeed speed)
        {
            if (AssociatedObject?.Template?.FindName("PART_Border", AssociatedObject) is not Border border)
                return;

            switch (speed)
            {
                case DragSpeed.Fast:
                    border.SetResourceReference(Border.BorderBrushProperty, "M3TertiaryBrush");
                    border.BorderThickness = new Thickness(2);
                    break;
                case DragSpeed.Fine:
                    border.SetResourceReference(Border.BorderBrushProperty, "M3SecondaryBrush");
                    border.BorderThickness = new Thickness(2);
                    break;
                default:
                    border.SetResourceReference(Border.BorderBrushProperty, "M3PrimaryBrush");
                    border.BorderThickness = new Thickness(2);
                    break;
            }
        }

        /// <summary>
        /// ドラッグ速度に応じてバッジテキストを更新します
        /// </summary>
        /// <param name="speed">現在のドラッグ速度</param>
        private void UpdateBadge(DragSpeed speed)
        {
            if (AssociatedObject == null) return;

            switch (speed)
            {
                case DragSpeed.Fast:
                    UI.TextBoxHelper.SetBadgeText(AssociatedObject, "FAST");
                    AssociatedObject.SetResourceReference(UI.TextBoxHelper.BadgeBrushProperty, "M3TertiaryBrush");
                    break;
                case DragSpeed.Fine:
                    UI.TextBoxHelper.SetBadgeText(AssociatedObject, "FINE");
                    AssociatedObject.SetResourceReference(UI.TextBoxHelper.BadgeBrushProperty, "M3SecondaryBrush");
                    break;
                default:
                    UI.TextBoxHelper.SetBadgeText(AssociatedObject, "");
                    break;
            }
        }

        /// <summary>
        /// バッジテキストをクリアします
        /// </summary>
        private void ResetBadge()
        {
            if (AssociatedObject != null)
            {
                UI.TextBoxHelper.SetBadgeText(AssociatedObject, "");
            }
        }

        /// <summary>
        /// ボーダーの視覚フィードバックを元の状態にリセットします
        /// </summary>
        private void ResetVisualFeedback()
        {
            if (AssociatedObject?.Template?.FindName("PART_Border", AssociatedObject) is not Border border)
                return;

            if (_originalBorderBrush != null)
            {
                border.BorderBrush = _originalBorderBrush;
            }
            else
            {
                border.SetResourceReference(Border.BorderBrushProperty, "M3OutlineBrush");
            }

            border.BorderThickness = _originalBorderThickness;
        }

        /// <summary>
        /// ドラッグ操作を終了し、すべての状態をリセットします
        /// </summary>
        private void EndDrag()
        {
            if (AssociatedObject == null) return;

            _isDragging = false;
            AssociatedObject.ReleaseMouseCapture();

            if (_originalCursor != null)
            {
                AssociatedObject.Cursor = _originalCursor;
            }

            ResetBadge();

            if (AssociatedObject.IsFocused)
            {
                if (AssociatedObject.Template?.FindName("PART_Border", AssociatedObject) is Border border)
                {
                    border.SetResourceReference(Border.BorderBrushProperty, "M3PrimaryBrush");
                    border.BorderThickness = new Thickness(2);
                }
            }
            else
            {
                ResetVisualFeedback();
            }
        }

        #endregion
    }
}
