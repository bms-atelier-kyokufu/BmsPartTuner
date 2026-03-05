using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace BmsAtelierKyokufu.BmsPartTuner.Services
{
    /// <summary>
    /// スライド確認サービス
    /// 危険な操作（上書き等）の確認UI
    /// M3準拠のプログレッシブ・フィル実装
    /// </summary>
    public class SlideConfirmationService
    {
        private FrameworkElement? _panel;
        private FrameworkElement? _thumb;
        private FrameworkElement? _progressiveFill;
        private Button? _actionButton;
        private bool _isDragging;
        private Point _startPoint;
        private Window? _window;

        public bool IsVisible => _panel != null && _panel.Visibility == Visibility.Visible;

        /// <summary>
        /// スライド完了イベント
        /// </summary>
        public event EventHandler? SlideCompleted;

        /// <summary>
        /// デフォルトコンストラクタ（DIコンテナ用）
        /// </summary>
        public SlideConfirmationService()
        {
        }

        /// <summary>
        /// UIコントロールを初期化
        /// </summary>
        public void Initialize(FrameworkElement panel, FrameworkElement thumb, Button actionButton)
        {
            _panel = panel ?? throw new ArgumentNullException(nameof(panel));
            _thumb = thumb ?? throw new ArgumentNullException(nameof(thumb));
            _actionButton = actionButton ?? throw new ArgumentNullException(nameof(actionButton));

            // プログレッシブ・フィル要素を取得
            _progressiveFill = FindChildByName(_panel, "ProgressiveFill");

            // 親Windowを取得
            _window = Window.GetWindow(_panel);

            // マウスイベントの設定
            _thumb.MouseLeftButtonDown += Thumb_MouseLeftButtonDown;
            _thumb.MouseLeftButtonUp += Thumb_MouseLeftButtonUp;
            _thumb.MouseMove += Thumb_MouseMove;
            _thumb.MouseLeave += Thumb_MouseLeave;

            // Windowレベルでマウスアップをキャッチ（ドラッグ中に外で離した場合）
            if (_window != null)
            {
                _window.PreviewMouseLeftButtonUp += Window_PreviewMouseLeftButtonUp;
            }
        }

        /// <summary>
        /// スライド確認UIを表示
        /// </summary>
        public void Show()
        {
            if (_panel == null)
                throw new InvalidOperationException("Initialize()を先に呼び出してください");

            _panel.Visibility = Visibility.Visible;
            _actionButton!.IsEnabled = false;
            Reset();
        }

        /// <summary>
        /// スライド確認UIを非表示
        /// </summary>
        public void Hide()
        {
            if (_panel != null)
            {
                _panel.Visibility = Visibility.Collapsed;
                if (_actionButton != null)
                {
                    _actionButton.IsEnabled = true;
                }
            }

            // ドラッグ状態をリセット
            if (_isDragging)
            {
                _isDragging = false;
                _thumb?.ReleaseMouseCapture();
            }
        }

        /// <summary>
        /// スライダーを初期位置に戻す
        /// </summary>
        private void Reset()
        {
            if (_thumb != null)
            {
                _thumb.Margin = new Thickness(0);
                // アニメーションをクリア
                _thumb.BeginAnimation(FrameworkElement.MarginProperty, null);
            }

            if (_progressiveFill != null)
            {
                _progressiveFill.Width = 0;
                _progressiveFill.BeginAnimation(FrameworkElement.WidthProperty, null);
            }
        }

        private void Thumb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _startPoint = e.GetPosition(_panel);
            _thumb!.CaptureMouse();
            e.Handled = true;
        }

        private void Thumb_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;

            CompleteSlide();
            e.Handled = true;
        }

        private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // ドラッグ中にウィンドウ内のどこかでマウスを離した場合
            if (_isDragging && _panel?.Visibility == Visibility.Visible)
            {
                CompleteSlide();
            }
        }

        private void Thumb_MouseLeave(object sender, MouseEventArgs e)
        {
            // マウスがサムを離れてもドラッグ中なら継続
            // （CaptureMouse()により、マウスがサムから離れてもイベントを受け取れる）
        }

        private void CompleteSlide()
        {
            if (!_isDragging) return;

            _isDragging = false;
            _thumb!.ReleaseMouseCapture();

            var currentPos = _thumb.Margin.Left;
            var panelWidth = _panel!.ActualWidth;
            var thumbWidth = _thumb.ActualWidth;
            var maxPosition = panelWidth - thumbWidth - 10;

            if (currentPos >= maxPosition * 0.8) // 80%以上でスライド完了
            {
                // 完了アニメーション（M3準拠：滑らかなイージング）
                var thumbAnimation = new ThicknessAnimation
                {
                    To = new Thickness(maxPosition, 0, 0, 0),
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var fillAnimation = new DoubleAnimation
                {
                    To = panelWidth - 4,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                thumbAnimation.Completed += (s, args) =>
                {
                    // 完了時に軽いバウンスエフェクト
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
                        SlideCompleted?.Invoke(this, EventArgs.Empty);
                    };

                    _progressiveFill?.BeginAnimation(UIElement.OpacityProperty, bounceAnimation);
                };

                _thumb.BeginAnimation(FrameworkElement.MarginProperty, thumbAnimation);
                _progressiveFill?.BeginAnimation(FrameworkElement.WidthProperty, fillAnimation);
            }
            else
            {
                // 戻りアニメーション（バネのように戻る）
                var thumbAnimation = new ThicknessAnimation
                {
                    To = new Thickness(0),
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 8 }
                };

                var fillAnimation = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 8 }
                };

                _thumb.BeginAnimation(FrameworkElement.MarginProperty, thumbAnimation);
                _progressiveFill?.BeginAnimation(FrameworkElement.WidthProperty, fillAnimation);
            }
        }

        private void Thumb_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            var currentPos = e.GetPosition(_panel);
            var offset = currentPos.X - _startPoint.X;

            // 左端より左に行かないように
            if (offset < 0) offset = 0;

            var panelWidth = _panel!.ActualWidth;
            var thumbWidth = _thumb!.ActualWidth;
            var maxPosition = panelWidth - thumbWidth - 10;

            // 右端より右に行かないように
            if (offset > maxPosition) offset = maxPosition;

            // アニメーションをクリアして直接設定
            _thumb.BeginAnimation(FrameworkElement.MarginProperty, null);
            _thumb.Margin = new Thickness(offset, 0, 0, 0);

            // プログレッシブ・フィル：スライダーの位置に合わせて背景を満たす
            if (_progressiveFill != null)
            {
                _progressiveFill.BeginAnimation(FrameworkElement.WidthProperty, null);
                // サムの右端までフィルを伸ばす
                var fillWidth = offset + thumbWidth;
                _progressiveFill.Width = fillWidth;
            }
        }

        /// <summary>
        /// 名前でビジュアルツリーから子要素を検索
        /// </summary>
        private FrameworkElement? FindChildByName(DependencyObject parent, string name)
        {
            if (parent == null) return null;

            var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                if (child is FrameworkElement element && element.Name == name)
                    return element;

                var result = FindChildByName(child, name);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// ステートをリセットします。
        /// </summary>
        public void Clear()
        {
            Reset();
        }
    }
}
