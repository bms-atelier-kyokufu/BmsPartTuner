using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace BmsAtelierKyokufu.BmsPartTuner.Controls
{
    public enum SlideDirection
    {
        LeftToRight,
        RightToLeft
    }

    /// <summary>
    /// スライド確認UIコントロール
    /// M3準拠のプログレッシブ・フィル実装
    /// </summary>
    public partial class SlideConfirmationControl : UserControl
    {
        #region 依存関係プロパティ

        /// <summary>
        /// スライド完了のしきい値（0.0～1.0）
        /// </summary>
        public static readonly DependencyProperty CompletionThresholdProperty =
            DependencyProperty.Register(
                nameof(CompletionThreshold),
                typeof(double),
                typeof(SlideConfirmationControl),
                new PropertyMetadata(0.8));

        /// <summary>
        /// 指示テキスト
        /// </summary>
        public static readonly DependencyProperty SlideInstructionTextProperty =
            DependencyProperty.Register(
                nameof(SlideInstructionText),
                typeof(string),
                typeof(SlideConfirmationControl),
                new PropertyMetadata("スライドして上書きを確定"));

        /// <summary>
        /// スライダーツマミの幅
        /// </summary>
        public static readonly DependencyProperty ThumbWidthProperty =
            DependencyProperty.Register(
                nameof(ThumbWidth),
                typeof(double),
                typeof(SlideConfirmationControl),
                new PropertyMetadata(80.0));

        /// <summary>
        /// スライド方向
        /// </summary>
        public static readonly DependencyProperty DirectionProperty =
            DependencyProperty.Register(
                nameof(Direction),
                typeof(SlideDirection),
                typeof(SlideConfirmationControl),
                new PropertyMetadata(SlideDirection.LeftToRight, OnDirectionChanged));

        public double CompletionThreshold
        {
            get => (double)GetValue(CompletionThresholdProperty);
            set => SetValue(CompletionThresholdProperty, value);
        }

        public string SlideInstructionText
        {
            get => (string)GetValue(SlideInstructionTextProperty);
            set => SetValue(SlideInstructionTextProperty, value);
        }

        public double ThumbWidth
        {
            get => (double)GetValue(ThumbWidthProperty);
            set => SetValue(ThumbWidthProperty, value);
        }

        public SlideDirection Direction
        {
            get => (SlideDirection)GetValue(DirectionProperty);
            set => SetValue(DirectionProperty, value);
        }

        #endregion

        #region ルーティングイベント

        /// <summary>
        /// スライド完了イベント
        /// </summary>
        public static readonly RoutedEvent SlideCompletedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(SlideCompleted),
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(SlideConfirmationControl));

        /// <summary>
        /// スライドキャンセルイベント
        /// </summary>
        public static readonly RoutedEvent SlideCancelledEvent =
            EventManager.RegisterRoutedEvent(
                nameof(SlideCancelled),
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(SlideConfirmationControl));

        public event RoutedEventHandler SlideCompleted
        {
            add { AddHandler(SlideCompletedEvent, value); }
            remove { RemoveHandler(SlideCompletedEvent, value); }
        }

        public event RoutedEventHandler SlideCancelled
        {
            add { AddHandler(SlideCancelledEvent, value); }
            remove { RemoveHandler(SlideCancelledEvent, value); }
        }

        #endregion

        #region フィールド

        private bool _isDragging;
        private Point _startPoint;
        private double _maxSlideDistance;

        #endregion

        #region コンストラクタ

        public SlideConfirmationControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        #endregion

        #region イベントハンドラ

        private static void OnDirectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SlideConfirmationControl control)
            {
                control.UpdateLayoutForDirection();
            }
        }

        private void UpdateLayoutForDirection()
        {
            if (SlideThumb == null || ProgressiveFill == null || ArrowText == null) return;

            // Reset any existing transforms/animations first
            Reset();

            if (Direction == SlideDirection.RightToLeft)
            {
                SlideThumb.HorizontalAlignment = HorizontalAlignment.Right;
                ProgressiveFill.HorizontalAlignment = HorizontalAlignment.Right;
                ArrowText.Text = "⬅";
            }
            else
            {
                SlideThumb.HorizontalAlignment = HorizontalAlignment.Left;
                ProgressiveFill.HorizontalAlignment = HorizontalAlignment.Left;
                ArrowText.Text = "➡";
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateLayoutForDirection();
            // 最大スライド距離を計算
            CalculateMaxSlideDistance();
            UpdateLayoutForDirection(); // Ensure layout is correct on load

            // コントロールが小さすぎる場合の警告
            if (ActualWidth > 0 && ActualWidth < ThumbWidth + 20)
            {
                System.Diagnostics.Debug.WriteLine($"Warning: SlideConfirmationControl is too small (ActualWidth={ActualWidth}, MinRequired={ThumbWidth + 20})");
            }

            // Windowレベルのマウスアップイベントを監視
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewMouseLeftButtonUp += Window_PreviewMouseLeftButtonUp;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Windowレベルのマウスアップイベントの購読を解除
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewMouseLeftButtonUp -= Window_PreviewMouseLeftButtonUp;
            }
        }

        private void SlideThumb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            // アニメーションをクリア（前回のアニメーションが残っている場合）
            ThumbTransform.BeginAnimation(TranslateTransform.XProperty, null);
            ProgressiveFill?.BeginAnimation(FrameworkElement.WidthProperty, null);
            ProgressiveFill?.BeginAnimation(UIElement.OpacityProperty, null);

            _isDragging = true;
            _startPoint = e.GetPosition(SlideConfirmationPanel);
            SlideThumb.CaptureMouse();

            e.Handled = true;
        }

        private void SlideThumb_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;
            CompleteSlide();
            e.Handled = true;
        }

        private void SlideThumb_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            var currentPos = e.GetPosition(SlideConfirmationPanel);
            var delta = currentPos.X - _startPoint.X;

            double offset;
            if (Direction == SlideDirection.RightToLeft)
            {
                // For RightToLeft, dragging left produces negative delta.
                // We want offset to be between 0 (start) and -_maxSlideDistance (end).
                // Or rather, we want TranslateTransform.X to be negative.
                offset = Math.Min(0, Math.Max(delta, -_maxSlideDistance));
            }
            else
            {
                offset = Math.Max(0, Math.Min(delta, _maxSlideDistance));
            }

            // サムの位置を更新（TranslateTransformを使用）
            ThumbTransform.X = offset;

            // プログレッシブ・フィルを更新
            UpdateProgressiveFill(offset);
        }

        private void SlideThumb_MouseLeave(object sender, MouseEventArgs e)
        {
            // CaptureMouse()により、マウスがサムから離れてもイベントを受け取れる
        }

        private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging && IsVisible)
            {
                CompleteSlide();
            }
        }

        #endregion

        #region プライベートメソッド

        private void CalculateMaxSlideDistance()
        {
            if (ActualWidth > 0)
            {
                // 最小値を0に制限（負の値にならないようにする）
                _maxSlideDistance = Math.Max(0, ActualWidth - ThumbWidth - 10);
                System.Diagnostics.Debug.WriteLine($"CalculateMaxSlideDistance: ActualWidth={ActualWidth}, ThumbWidth={ThumbWidth}, MaxSlideDistance={_maxSlideDistance}");

                // スライド不可能な場合の警告
                if (_maxSlideDistance <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Not enough space to slide (need at least {ThumbWidth + 10}px, got {ActualWidth}px)");
                }
            }
            else
            {
                // ActualWidthがまだ0の場合、次のレンダリングサイクルで再計算
                System.Diagnostics.Debug.WriteLine($"CalculateMaxSlideDistance: ActualWidth is 0, will recalculate on next render");
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (ActualWidth > 0)
                    {
                        _maxSlideDistance = Math.Max(0, ActualWidth - ThumbWidth - 10);
                        System.Diagnostics.Debug.WriteLine($"CalculateMaxSlideDistance (delayed): ActualWidth={ActualWidth}, MaxSlideDistance={_maxSlideDistance}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void UpdateProgressiveFill(double offset)
        {
            if (ProgressiveFill == null) return;

            // アニメーションをクリアして直接設定
            ProgressiveFill.BeginAnimation(FrameworkElement.WidthProperty, null);

            // 幅が負にならないように保証
            // For R2L, offset is negative, so use Abs(offset)
            var fillWidth = Math.Abs(offset) + ThumbWidth;
            ProgressiveFill.Width = Math.Max(0, fillWidth);
        }

        private void CompleteSlide()
        {
            if (!_isDragging) return;

            System.Diagnostics.Debug.WriteLine($"CompleteSlide: Releasing mouse capture");

            // マウスキャプチャを解放（ただし_isDraggingはアニメーション完了後にリセット）
            SlideThumb.ReleaseMouseCapture();

            var currentOffset = ThumbTransform.X;
            var progress = _maxSlideDistance > 0 ? Math.Abs(currentOffset) / _maxSlideDistance : 0;

            System.Diagnostics.Debug.WriteLine($"CompleteSlide: progress={progress:F2}, threshold={CompletionThreshold}");

            if (progress >= CompletionThreshold) // デフォルト80%以上でスライド完了
            {
                AnimateCompletion();
            }
            else
            {
                AnimateCancellation();
            }

            // 注意: _isDraggingはアニメーション完了後にfalseに設定される
        }

        private void AnimateCompletion()
        {
            // サムの完了アニメーション
            double targetX = (Direction == SlideDirection.RightToLeft) ? -_maxSlideDistance : _maxSlideDistance;

            var thumbAnimation = new DoubleAnimation
            {
                To = targetX,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // フィルの完了アニメーション
            // ActualWidth - 4が負にならないように保証
            var fillTargetWidth = Math.Max(0, ActualWidth - 4);
            var fillAnimation = new DoubleAnimation
            {
                To = fillTargetWidth,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // 完了時のバウンスエフェクト
            thumbAnimation.Completed += (s, args) =>
            {
                var bounceAnimation = new DoubleAnimation
                {
                    From = 0.3,
                    To = 0.5,
                    Duration = TimeSpan.FromMilliseconds(100),
                    AutoReverse = true,
                    EasingFunction = new BounceEase { Bounces = 1, EasingMode = EasingMode.EaseOut }
                };

                bounceAnimation.Completed += (bs, bargs) =>
                {
                    System.Diagnostics.Debug.WriteLine("AnimateCompletion: Bounce completed");

                    // アニメーション完了後、状態をクリア
                    ThumbTransform.BeginAnimation(TranslateTransform.XProperty, null);
                    ProgressiveFill?.BeginAnimation(FrameworkElement.WidthProperty, null);
                    ProgressiveFill?.BeginAnimation(UIElement.OpacityProperty, null);

                    // 完了位置に固定
                    ThumbTransform.X = targetX;
                    if (ProgressiveFill != null)
                    {
                        ProgressiveFill.Width = Math.Max(0, fillTargetWidth);
                        ProgressiveFill.Opacity = 0.3;
                    }

                    // ドラッグ状態を確実にリセット
                    _isDragging = false;

                    // スライド完了イベントを発火
                    RaiseEvent(new RoutedEventArgs(SlideCompletedEvent));
                };

                ProgressiveFill?.BeginAnimation(UIElement.OpacityProperty, bounceAnimation);
            };

            ThumbTransform.BeginAnimation(TranslateTransform.XProperty, thumbAnimation);
            ProgressiveFill?.BeginAnimation(FrameworkElement.WidthProperty, fillAnimation);
        }

        private void AnimateCancellation()
        {
            // サムのリセットアニメーション（バネのように戻る）
            var thumbAnimation = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new ElasticEase
                {
                    EasingMode = EasingMode.EaseOut,
                    Oscillations = 2,
                    Springiness = 8
                }
            };

            // フィルのリセットアニメーション
            var fillAnimation = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new ElasticEase
                {
                    EasingMode = EasingMode.EaseOut,
                    Oscillations = 2,
                    Springiness = 8
                }
            };

            thumbAnimation.Completed += (s, args) =>
            {
                System.Diagnostics.Debug.WriteLine("AnimateCancellation: Animation completed, resetting state");

                // アニメーション完了後、アニメーションをクリアして状態をリセット
                ThumbTransform.BeginAnimation(TranslateTransform.XProperty, null);
                ProgressiveFill?.BeginAnimation(FrameworkElement.WidthProperty, null);

                // 明示的に初期位置に設定
                ThumbTransform.X = 0;
                if (ProgressiveFill != null)
                {
                    ProgressiveFill.Width = 0;
                }

                // ドラッグ状態を確実にリセット
                _isDragging = false;

                // スライドキャンセルイベントを発火
                RaiseEvent(new RoutedEventArgs(SlideCancelledEvent));
            };

            ThumbTransform.BeginAnimation(TranslateTransform.XProperty, thumbAnimation);
            ProgressiveFill?.BeginAnimation(FrameworkElement.WidthProperty, fillAnimation);
        }

        #endregion

        #region パブリックメソッド

        /// <summary>
        /// スライダーを初期状態にリセット
        /// </summary>
        public void Reset()
        {
            _isDragging = false;
            SlideThumb.ReleaseMouseCapture();

            // アニメーションをクリア
            ThumbTransform.BeginAnimation(TranslateTransform.XProperty, null);
            ProgressiveFill?.BeginAnimation(FrameworkElement.WidthProperty, null);

            // 初期位置に戻す
            ThumbTransform.X = 0;
            if (ProgressiveFill != null)
            {
                ProgressiveFill.Width = 0;
            }
        }

        #endregion

        #region オーバーライド

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            // サイズ変更時に最大スライド距離を再計算
            if (sizeInfo.WidthChanged)
            {
                CalculateMaxSlideDistance();
            }
        }

        #endregion
    }
}
