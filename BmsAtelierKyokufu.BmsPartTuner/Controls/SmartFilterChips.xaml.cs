using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using BmsAtelierKyokufu.BmsPartTuner.Services;

namespace BmsAtelierKyokufu.BmsPartTuner.Controls
{
    /// <summary>
    /// Smart Filter Chips コントロール
    /// パート別フィルタリング用の選択可能なチップを表示
    /// </summary>
    public partial class SmartFilterChips : UserControl
    {
        #region 依存関係プロパティ

        /// <summary>
        /// チップコレクション
        /// </summary>
        public static readonly DependencyProperty ChipsProperty =
            DependencyProperty.Register(
                nameof(Chips),
                typeof(ObservableCollection<FileListFilterService.SelectableFilterChip>),
                typeof(SmartFilterChips),
                new PropertyMetadata(null, OnChipsChanged));

        /// <summary>
        /// チップコレクション
        /// </summary>
        public ObservableCollection<FileListFilterService.SelectableFilterChip>? Chips
        {
            get => (ObservableCollection<FileListFilterService.SelectableFilterChip>?)GetValue(ChipsProperty);
            set => SetValue(ChipsProperty, value);
        }

        #endregion

        #region ルーティングイベント

        /// <summary>
        /// チップクリック時のルーティングイベント
        /// </summary>
        public static readonly RoutedEvent ChipClickEvent =
            EventManager.RegisterRoutedEvent(
                nameof(ChipClick),
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(SmartFilterChips));

        /// <summary>
        /// チップクリック時のイベント
        /// </summary>
        public event RoutedEventHandler ChipClick
        {
            add => AddHandler(ChipClickEvent, value);
            remove => RemoveHandler(ChipClickEvent, value);
        }

        #endregion

        #region コンストラクタ

        public SmartFilterChips()
        {
            InitializeComponent();
        }

        #endregion

        #region イベントハンドラ

        private static void OnChipsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SmartFilterChips control)
            {
                control.ChipsItemsControl.ItemsSource = e.NewValue as ObservableCollection<FileListFilterService.SelectableFilterChip>;
            }
        }

        private void Chip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is FileListFilterService.SelectableFilterChip chip)
            {
                // チップクリックイベントを発火
                var args = new ChipClickEventArgs(ChipClickEvent, chip);
                RaiseEvent(args);
            }
        }

        #endregion

        #region カスタムイベント引数

        /// <summary>
        /// チップクリックイベント引数
        /// </summary>
        public class ChipClickEventArgs : RoutedEventArgs
        {
            public FileListFilterService.SelectableFilterChip Chip { get; }

            public ChipClickEventArgs(RoutedEvent routedEvent, FileListFilterService.SelectableFilterChip chip)
                : base(routedEvent)
            {
                Chip = chip;
            }
        }

        #endregion
    }
}
