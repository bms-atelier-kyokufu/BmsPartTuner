using BmsAtelierKyokufu.BmsPartTuner.ViewModels;

namespace BmsAtelierKyokufu.BmsPartTuner.Services
{
    /// <summary>
    /// スライド確認UI要素
    /// </summary>
    public class SlideConfirmationElement : IUiElementService<MainViewModel>
    {
        private MainViewModel? _viewModel;

        /// <summary>
        /// スライド完了時に発生するイベント
        /// </summary>
        public event EventHandler? SlideCompleted;


        /// <summary>
        /// スライド確認UIの表示状態
        /// </summary>
        public bool IsVisible => _viewModel != null && _viewModel.IsSlideConfirmationVisible;

        public void Initialize(MainViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }


        /// <summary>
        /// スライド確認UIを非表示にする
        /// </summary>
        public void Hide()
        {
            if (_viewModel == null)
                throw new InvalidOperationException("Initialize()を先に呼び出してください");

            _viewModel.HideSlideConfirmation();
        }

        /// <summary>
        /// スライド確認UIを初期状態にリセットする
        /// </summary>
        public void Clear()
        {
            if (_viewModel == null)
                throw new InvalidOperationException("Initialize()を先に呼び出してください");
            _viewModel.HideSlideConfirmation();
        }

        /// <summary>
        /// スライド完了を通知する（View側から呼ばれる想定）
        /// </summary>
        public void OnSlideCompleted()
        {
            SlideCompleted?.Invoke(this, EventArgs.Empty);
            Hide();
        }

        void IUiElementService<MainViewModel>.Show(MainViewModel vm)
        {
            if (vm == null)
                throw new ArgumentNullException(nameof(vm));
            vm.ShowSlideConfirmation();
        }
    }
}
