using System.Windows;
using System.Windows.Controls;

namespace BmsAtelierKyokufu.BmsPartTuner.Controls
{
    /// <summary>
    /// ToastControl.xaml の相互作用ロジック
    /// Why: アニメーションのトリガーと、バインディング用のプロパティを提供します。
    /// </summary>
    public partial class ToastControl : UserControl
    {
        public ToastControl()
        {
            InitializeComponent();
            this.Loaded += ToastControl_Loaded;
        }

        private void ToastControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is INotifyPropertyChanged notifyPropertyChanged)
            {
                notifyPropertyChanged.PropertyChanged += DataContext_PropertyChanged;
            }

            // 初期状態を設定
            GoToVisualState(IsToastVisible ? "Active" : "Inactive", false);
        }

        private void DataContext_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // DataContextの変更を監視（将来の拡張用）
        }

        // ==========================================================
        // Dependency Properties
        // ==========================================================

        public static readonly DependencyProperty IsToastVisibleProperty =
            DependencyProperty.Register(nameof(IsToastVisible), typeof(bool), typeof(ToastControl),
                new PropertyMetadata(false, OnIsToastVisibleChanged));

        /// <summary>
        /// トーストの表示・非表示を制御します。
        /// 値が変更されると、VisualStateManager を介してアニメーションが実行されます。
        /// </summary>
        public bool IsToastVisible
        {
            get => (bool)GetValue(IsToastVisibleProperty);
            set => SetValue(IsToastVisibleProperty, value);
        }

        private static void OnIsToastVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ToastControl control)
            {
                var newValue = (bool)e.NewValue;
                var stateName = newValue ? "Active" : "Inactive";
                control.GoToVisualState(stateName, true);
            }
        }

        /// <summary>
        /// VisualStateを変更します。
        /// UserControlの場合、ルートのGridに対してGoToElementStateを呼び出す必要があります。
        /// </summary>
        private void GoToVisualState(string stateName, bool useTransitions)
        {
            // PART_Root（Grid）に対してGoToElementStateを使用
            if (PART_Root != null)
            {
                VisualStateManager.GoToElementState(PART_Root, stateName, useTransitions);
            }
            else
            {
                // フォールバック: コントロール自体に対してGoToState
                VisualStateManager.GoToState(this, stateName, useTransitions);
            }
        }

        public static readonly DependencyProperty IsErrorProperty =
            DependencyProperty.Register(nameof(IsError), typeof(bool), typeof(ToastControl), new PropertyMetadata(false));

        public bool IsError
        {
            get => (bool)GetValue(IsErrorProperty);
            set => SetValue(IsErrorProperty, value);
        }

        public static readonly DependencyProperty ToastMessageProperty =
            DependencyProperty.Register(nameof(ToastMessage), typeof(string), typeof(ToastControl), new PropertyMetadata(string.Empty));

        public string ToastMessage
        {
            get => (string)GetValue(ToastMessageProperty);
            set => SetValue(ToastMessageProperty, value);
        }

        public static readonly DependencyProperty ToastIconProperty =
            DependencyProperty.Register(nameof(ToastIcon), typeof(string), typeof(ToastControl), new PropertyMetadata(string.Empty));

        public string ToastIcon
        {
            get => (string)GetValue(ToastIconProperty);
            set => SetValue(ToastIconProperty, value);
        }

        // ==========================================================
        // Event Handlers
        // ==========================================================

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            IsToastVisible = false;
        }
    }
}
