using System.Windows.Controls;

namespace BmsAtelierKyokufu.BmsPartTuner.Controls.Settings
{
    /// <summary>
    /// LicenseDetailView.xaml の相互作用ロジック
    /// </summary>
    public partial class LicenseDetailView : UserControl
    {
        public LicenseDetailView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// マウスホイールスクロール時の処理
        /// </summary>
        private void HandleMouseWheel(ScrollViewer target, MouseWheelEventArgs e)
        {
            if (target == null)
            {
                return;
            }

            // マウスホイールの方向に応じてスクロール
            target.ScrollToVerticalOffset(target.VerticalOffset - e.Delta);
            e.Handled = true; // イベント処理済みとしてマークして親へのバブリング防止
        }

        /// <summary>
        /// マウスホイールスクロール時の処理
        /// </summary>
        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                HandleMouseWheel(scrollViewer, e);
            }
        }

        /// <summary>
        /// コンテンツ内でのマウスホイールを親ScrollViewerへ転送
        /// </summary>
        private void Content_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            HandleMouseWheel(LicenseScrollViewer, e);
        }
    }
}
