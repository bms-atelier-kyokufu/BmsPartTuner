using System.Diagnostics.CodeAnalysis;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;
using BmsAtelierKyokufu.BmsPartTuner.UI.Services;
using BmsAtelierKyokufu.BmsPartTuner.UI.ViewModels;
using BmsAtelierKyokufu.BmsPartTuner.UI.Views.Controls;

namespace BmsAtelierKyokufu.BmsPartTuner.UI.Views.Windows
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// ViewModelとUIサービスを橋渡しする薄い層
    /// </summary>
    [ExcludeFromCodeCoverage]
    public partial class MainWindow : Window
    {
        #region フィールド

        private readonly MainViewModel _viewModel;
        private readonly IUiElementService<ToastViewModel> _toastService;
        private readonly IUiElementService<ResultCardData> _resultCardService;
        private readonly IDragDropService _dragDropService;
        private readonly FileListFilterService _filterService;
        private ICollectionView? _collectionView;

        #endregion

        #region コンストラクタ

        public MainWindow(
            MainViewModel viewModel,
            IUiElementService<ToastViewModel> toastService,
            IUiElementService<ResultCardData> resultCardService,
            IDragDropService dragDropService,
            FileListFilterService filterService)
        {
            InitializeComponent();

            // DIから受け取ったサービスを保持
            _viewModel = viewModel;
            _toastService = toastService;
            _resultCardService = resultCardService;
            _dragDropService = dragDropService;
            _filterService = filterService;

            DataContext = _viewModel;

            // ウィンドウハンドルが生成された後にタイトルバーテーマを適用
            SourceInitialized += (s, e) => WindowThemeHelper.ApplyTitleBarTheme(this, _viewModel.Settings.IsDarkTheme);

            // イベント登録
            InitializeEventHandlers();
            InitializeUIBindings();

            // テーマ変更時にタイトルバー更新
            viewModel.Settings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.IsDarkTheme))
                {
                    WindowThemeHelper.ApplyTitleBarTheme(this, viewModel.Settings.IsDarkTheme);
                }
            };

            // ウィンドウクローズ時のクリーンアップ
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            // リソースの解放
            _viewModel?.Dispose();
        }

        #endregion

        #region 初期化

        private void InitializeEventHandlers()
        {
            // DragDropService - M3FilePathTextBoxの内部TextBoxにアクセス
            _dragDropService.FileDropped += (s, e) =>
            {
                if (e.IsSupported)
                {
                    _viewModel.FileOperations.InputPath = e.FilePath;
                }
                else
                {
                    _viewModel.ShowToast($"非対応形式: {_dragDropService.GetSupportedExtensionsPattern()}", "⚠", true);
                }
            };

            // Drag & Dropのセットアップ
            _dragDropService.SetupDragAndDrop(InputFilePathTextBox);
            _dragDropService.SetupDragAndDrop(this);

            // ViewModelのプロパティ変更監視
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // スライド確認要求イベントのハンドラ登録
            _viewModel.SlideConfirmationRequested += OnSlideConfirmationRequested;
        }



        private void InitializeUIBindings()
        {
            // R2TextBoxのAIボタンのコマンド設定を確認
            this.Loaded += (s, e) =>
            {
                // テンプレートを強制適用
                R2TextBox.ApplyTemplate();

                // R2TextBoxのテンプレートからAIボタンを探す
                if (R2TextBox.Template?.FindName("PART_AiButton", R2TextBox) is Button aiButton)
                {
                    // コマンドが正しくバインドされていなければ直接設定
                    if (aiButton.Command == null)
                    {
                        // 添付プロパティから取得したコマンドを直接設定
                        var command = Infrastructure.UI.TextBoxHelper.GetAiOptimizationCommand(R2TextBox);
                        if (command != null)
                        {
                            aiButton.Command = command;
                        }
                        else
                        {
                            // ViewModelから直接取得
                            aiButton.Command = _viewModel.ExecuteThresholdOptimizationCommand;
                        }
                    }
                }
            };

            // FileListViewの更新監視（FilterService用）
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "FileListItems")
                {
                    _collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(_viewModel.BmsDefinitionManager.FileListItems);

                    // Filter Chipsの生成（選択可能版）
                    if (_viewModel.BmsDefinitionManager.FileListItems?.Count > 0)
                    {
                        // Check if items are currently bound to FileListItems
                        if (FilesListView.ItemsSource == _viewModel.BmsDefinitionManager.FileListItems)
                        {
                            var items = FilesListView.SelectedItems;
                            var files = items.Cast<BmsAudioFile>().ToList();
                            _viewModel.BmsDefinitionManager.DeleteFiles(files);
                        }
                        var chips = FileListFilterService.GenerateSelectableFilterChips(_viewModel.BmsDefinitionManager.FileListItems);
                        if (chips != null)
                        {
                            // FilterChipsをViewModelに設定
                            _viewModel.BmsDefinitionManager.FilterChips = chips;

                            // チップ選択変更時のイベント購読
                            foreach (var chip in chips)
                            {
                                chip.PropertyChanged += (sender, args) =>
                                {
                                    if (args.PropertyName == nameof(FileListFilterService.SelectableFilterChip.IsSelected))
                                    {
                                        UpdateChipFilter();
                                    }
                                };
                            }
                        }
                    }
                }
            };

            // FileListViewModelの選択キーワード変更イベント購読
            _viewModel.BmsDefinitionManager.SelectedKeywordsChanged += (s, e) => UpdateChipFilter();
            _viewModel.BmsDefinitionManager.InstrumentFilterChanged += (s, e) => UpdateTextAndInstrumentFilter();
        }

        private void UpdateChipFilter()
        {
            if (_collectionView == null) return;
            var selectedKeywords = _viewModel.BmsDefinitionManager.GetSelectedKeywords();
            _collectionView.Filter = FileListFilterService.CreateChipFilterPredicate(selectedKeywords);
            _collectionView.Refresh();
        }

        #endregion

        #region イベントハンドラ

        private void MainGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.Notification.IsSlideConfirmationVisible)
            {
                _viewModel.Notification.IsSlideConfirmationVisible = false;
                e.Handled = true;
            }
        }

        private void OnSlideConfirmationRequested(object? sender, EventArgs e)
        {
            _viewModel.ShowSlideConfirmation();
        }

        private async void SlideConfirmation_Completed(object sender, RoutedEventArgs e)
        {
            _viewModel.HideSlideConfirmation();
            await _viewModel.ExecuteDefinitionReductionAfterConfirmationAsync();
        }

        private void SlideConfirmation_Cancelled(object sender, RoutedEventArgs e)
        {
            // キャンセル時のトースト通知は廃止
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateTextAndInstrumentFilter();
        }

        private void UpdateTextAndInstrumentFilter()
        {
            if (_collectionView == null) return;

            var selectedInstruments = new HashSet<string>(
                _viewModel.BmsDefinitionManager.InstrumentGroups.Where(g => g.IsSelected).Select(g => g.Name),
                StringComparer.OrdinalIgnoreCase);

            _collectionView.Filter = FileListFilterService.CreateFilterPredicate(
                _viewModel.BmsDefinitionManager.FilterText,
                selectedInstruments);
            _collectionView.Refresh();
        }

        private void FilterTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                _viewModel.BmsDefinitionManager.FilterText = FilterTextBox.Text;
            }
        }

        private void FilterChip_Click(object sender, RoutedEventArgs e)
        {
            if (e is SmartFilterChips.ChipClickEventArgs args)
            {
                _viewModel.BmsDefinitionManager.ToggleChipSelection(args.Chip);
            }
        }
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // ViewModelの状態変化に応じたUI更新
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.StatusMessage):
                    // StatusMessageはXAMLでバインディング済み
                    break;

                case nameof(MainViewModel.IsBusy):
                    // IsBusyによるUI制御
                    if (!_viewModel.IsBusy)
                    {
                        _viewModel.HideSlideConfirmation();
                    }
                    break;

                case nameof(NotificationViewModel.IsSlideConfirmationVisible):
                    // スライド確認UIが非表示になったらリセット
                    if (!_viewModel.Notification.IsSlideConfirmationVisible)
                    {
                        // UIスレッドで実行
                        Dispatcher.BeginInvoke(new Action(() => SlideConfirmation?.Reset()), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    break;
            }
        }

        #endregion

        // Windowが閉じたらViewModelのリソースを解放
        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            (_viewModel as System.IDisposable)?.Dispose();
        }
    }
}
