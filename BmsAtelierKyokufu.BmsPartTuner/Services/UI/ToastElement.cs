using BmsAtelierKyokufu.BmsPartTuner.ViewModels;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.UI
{
    /// <summary>
    /// トースト通知UI要素（ViewModelドリブン）
    /// NotificationViewModelに処理を委譲し、ViewModel層のタイマー管理を利用する
    /// </summary>
    public class ToastElement : IUiElementService<ToastViewModel>
    {
        private readonly MainViewModel _viewModel;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="viewModel">MainViewModel インスタンス</param>
        public ToastElement(MainViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        /// <summary>
        /// トースト通知の表示状態
        /// </summary>
        public bool IsVisible => _viewModel.IsToastVisible;

        /// <summary>
        /// トースト通知を表示する
        /// NotificationViewModelのタイマー管理を利用
        /// 注: このメソッドは直接使用されていません。Show(ToastViewModel data)を使用してください。
        /// </summary>
        public void Show()
        {
            // デフォルトメッセージで表示
            // 実際の使用では Show(ToastViewModel data) を使用することが推奨されます
            _viewModel.ShowToast("通知", "ℹ", false);
        }

        /// <summary>
        /// トースト通知を非表示にする
        /// </summary>
        public void Hide()
        {
            _viewModel.HideToast();
        }

        /// <summary>
        /// トースト通知を初期状態にリセットする
        /// </summary>
        public void Clear()
        {
            _viewModel.HideToast();
        }

        /// <summary>
        /// トースト通知をデータと共に表示する
        /// NotificationViewModelのタイマー管理を利用
        /// </summary>
        public void Show(ToastViewModel data)
        {
            _viewModel.ShowToast(data.Message, data.Icon, data.IsError);
        }
    }
}
