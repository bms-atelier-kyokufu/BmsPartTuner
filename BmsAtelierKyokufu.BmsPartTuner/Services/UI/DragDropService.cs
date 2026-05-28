namespace BmsAtelierKyokufu.BmsPartTuner.Services.UI;

/// <summary>
/// BMSファイルなどのドラッグ&amp;ドロップ機能を管理するサービス。
/// サポートされる拡張子のフィルタリングと、ドラッグ時の視覚的フィードバック（半透明化）を提供し、
/// ドロップ完了時にはイベントを通じてUIからロジック層へ通知を行います。
/// </summary>
public class DragDropService(string[] supportedExtensions) : IDragDropService
{
    private readonly string[] _supportedExtensions = supportedExtensions ?? throw new ArgumentNullException(nameof(supportedExtensions));

    /// <summary>
    /// ファイルがドロップされた時のイベント。
    /// </summary>
    public event EventHandler<FileDroppedEventArgs>? FileDropped;

    /// <summary>
    /// ファイルドロップイベントの引数を初期化します。
    /// </summary>
    public class FileDroppedEventArgs(string filePath, bool isSupported) : EventArgs
    {
        /// <summary>ドロップされたファイルパス。</summary>
        public string FilePath { get; } = filePath;

        /// <summary>サポートされているファイルかどうか。</summary>
        public bool IsSupported { get; } = isSupported;
    }

    /// <summary>
    /// 指定されたUI要素に対してドラッグ&amp;ドロップ機能を設定します。
    /// ドラッグ入場、退場時の視覚的フィードバック処理やドロップ時のイベントハンドリングを登録します。
    /// </summary>
    /// <param name="element">対象 of UIElement.</param>
    public void SetupDragAndDrop(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        element.AllowDrop = true;
        element.PreviewDragOver += OnPreviewDragOver;
        element.Drop += OnDrop;
        element.DragEnter += OnDragEnter;
        element.DragLeave += OnDragLeave;
    }

    /// <summary>
    /// ドラッグオーバー時の処理。サポートされるファイルの場合はCopyエフェクトを、それ以外はNoneエフェクトを設定します。
    /// </summary>
    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && IsSupportedFile(files[0]))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    /// <summary>
    /// ドラッグ入場時の処理（視覚フィードバック）。サポートされるファイルがドラッグされた場合、要素を半透明（Opacity = 0.7）にします。
    /// </summary>
    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && IsSupportedFile(files[0]))
            {
                if (sender is UIElement element)
                {
                    element.Opacity = 0.7;
                }
            }
        }
    }

    /// <summary>
    /// ドラッグ退場時の処理。要素のOpacityを元の状態（1.0）に戻します。
    /// </summary>
    private void OnDragLeave(object sender, DragEventArgs e)
    {
        if (sender is UIElement element)
        {
            element.Opacity = 1.0;
        }
    }

    /// <summary>
    /// ドロップ時の処理。要素のOpacityを元に戻し、サポート状況を判定してイベントを発火します。
    /// </summary>
    private void OnDrop(object sender, DragEventArgs e)
    {
        if (sender is UIElement element)
        {
            element.Opacity = 1.0;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                var filePath = files[0];
                var isSupported = IsSupportedFile(filePath);
                FileDropped?.Invoke(this, new FileDroppedEventArgs(filePath, isSupported));
            }
        }
    }

    /// <summary>
    /// サポートされているファイルかチェック。
    /// </summary>
    /// <param name="filePath">ファイルパス。</param>
    /// <returns>サポートされている場合true。</returns>
    public bool IsSupportedFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;

        var extension = Path.GetExtension(filePath).ToLower();
        return _supportedExtensions.Contains(extension);
    }

    /// <summary>
    /// サポート拡張子を表示用に結合。
    /// </summary>
    /// <returns>カンマ区切りの拡張子リスト（例: ".bms, .bme, .bml"）。</returns>
    public string GetSupportedExtensionsPattern()
    {
        return string.Join(", ", _supportedExtensions);
    }

    /// <summary>
    /// ダイアログ用の拡張子パターンを取得。
    /// </summary>
    /// <returns>セミコロン区切りのワイルドカードパターン（例: "*.bms;*.bme;*.bml"）。</returns>
    public string GetDialogExtensionPattern()
    {
        return string.Join(";", _supportedExtensions.Select(static e => "*" + e));
    }
}
