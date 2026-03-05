namespace BmsAtelierKyokufu.BmsPartTuner.Services
{
    /// <summary>
    /// UI要素の共通操作インターフェース
    /// 表示・非表示・リセット（初期化）の統一されたAPIを提供
    /// </summary>
    /// <typeparam name="TData">表示するデータの型</typeparam>
    public interface IUiElementService<TData>
    {
        /// <summary>
        /// UI要素が表示されているかどうか
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// 指定されたデータを表示する
        /// </summary>
        /// <param name="data">表示するデータ</param>
        void Show(TData data);

        /// <summary>
        /// UI要素を非表示にする
        /// </summary>
        void Hide();

        /// <summary>
        /// UI要素を初期状態にリセットする
        /// </summary>
        void Clear();
    }
}
