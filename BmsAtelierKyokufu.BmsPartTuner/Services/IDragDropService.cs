using System.Windows;

namespace BmsAtelierKyokufu.BmsPartTuner.Services
{
    /// <summary>
    /// ドラッグ&ドロップサービスのインターフェース
    /// ファイルのドラッグ&ドロップ機能を提供
    /// </summary>
    public interface IDragDropService
    {
        /// <summary>
        /// ファイルがドロップされたときに発火するイベント
        /// </summary>
        event EventHandler<DragDropService.FileDroppedEventArgs>? FileDropped;

        /// <summary>
        /// 指定されたUI要素にドラッグ&ドロップ機能を設定
        /// </summary>
        /// <param name="element">ドラッグ&ドロップを有効にするUI要素</param>
        void SetupDragAndDrop(UIElement element);

        /// <summary>
        /// 指定されたファイルパスがサポートされているかチェック
        /// </summary>
        /// <param name="filePath">チェックするファイルパス</param>
        /// <returns>サポートされている場合true</returns>
        bool IsSupportedFile(string filePath);

        /// <summary>
        /// サポートされている拡張子のパターン文字列を取得
        /// </summary>
        /// <returns>拡張子パターン（例: ".bms, .bme, .bml"）</returns>
        string GetSupportedExtensionsPattern();

        /// <summary>
        /// ファイルダイアログ用の拡張子フィルター文字列を取得
        /// </summary>
        /// <returns>ダイアログフィルター文字列</returns>
        string GetDialogExtensionPattern();
    }
}
