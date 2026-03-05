using BmsAtelierKyokufu.BmsPartTuner.ViewModels;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.UI
{
    /// <summary>
    /// 結果カードUI要素（ViewModelドリブン）
    /// </summary>
    public class ResultCardElement : IUiElementService<ResultCardData>
    {
        private readonly MainViewModel _viewModel;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="viewModel">MainViewModel インスタンス</param>
        public ResultCardElement(MainViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        /// <summary>
        /// 結果カードの表示状態
        /// </summary>
        public bool IsVisible => _viewModel.IsResultCardVisible;

        /// <summary>
        /// 結果カードを表示する
        /// </summary>
        public void Show()
        {
            _viewModel.IsResultCardVisible = true;
        }

        /// <summary>
        /// 結果カードを非表示にする
        /// </summary>
        public void Hide()
        {
            _viewModel.HideResultCard();
        }

        /// <summary>
        /// 結果カードを初期状態にリセットする
        /// </summary>
        public void Clear()
        {
            _viewModel.HideResultCard();
        }

        /// <summary>
        /// 結果カードをデータと共に表示する
        /// </summary>
        public void Show(ResultCardData data)
        {
            _viewModel.ShowResultCard(
                data.Threshold, data.Summary, data.Reduction, data.Time,
                data.Margin, data.IsOptimization);
        }
    }
}
