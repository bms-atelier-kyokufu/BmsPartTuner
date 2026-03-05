using System.Windows;

namespace BmsAtelierKyokufu.BmsPartTuner.Services;

/// <summary>
/// ドラッグ&amp;ドロップサービス。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>BMSファイルのドラッグ&amp;ドロップ機能</item>
/// <item>サポートされる拡張子のフィルタリング</item>
/// <item>視覚的フィードバック（ドラッグ中の半透明化）</item>
/// </list>
/// 
/// <para>【視覚フィードバック】</para>
/// サポートされるファイルがドラッグされた場合、
/// 対象要素を半透明（Opacity = 0.7）にして、
/// ドロップ可能であることを視覚的に示します。
/// 
/// <para>【イベント駆動】</para>
/// ファイルがドロップされた際、<see cref="FileDropped"/>イベントを発火し、
/// ViewModelに通知します。これにより、UIとロジックの分離を実現します。
/// 
/// <para>【拡張子フィルタ】</para>
/// コンストラクタで指定された拡張子のみを受け入れます。
/// 例: [".bms", ".bme", ".bml", ".pms"]
/// </remarks>
public class DragDropService : IDragDropService
{
    private readonly string[] _supportedExtensions;

    /// <summary>
    /// ファイルがドロップされた時のイベント。
    /// </summary>
    public event EventHandler<FileDroppedEventArgs>? FileDropped;

    /// <summary>
    /// ファイルドロップイベントの引数。
    /// </summary>
    public class FileDroppedEventArgs : EventArgs
    {
        /// <summary>ドロップされたファイルパス。</summary>
        public string FilePath { get; }

        /// <summary>サポートされているファイルかどうか。</summary>
        public bool IsSupported { get; }

        /// <summary>
        /// FileDroppedEventArgsを初期化。
        /// </summary>
        /// <param name="filePath">ファイルパス。</param>
        /// <param name="isSupported">サポートフラグ。</param>
        public FileDroppedEventArgs(string filePath, bool isSupported)
        {
            FilePath = filePath;
            IsSupported = isSupported;
        }
    }

    /// <summary>
    /// DragDropServiceを初期化。
    /// </summary>
    /// <param name="supportedExtensions">サポートされる拡張子の配列。</param>
    /// <exception cref="ArgumentNullException">supportedExtensionsがnullの場合。</exception>
    public DragDropService(string[] supportedExtensions)
    {
        _supportedExtensions = supportedExtensions ?? throw new ArgumentNullException(nameof(supportedExtensions));
    }

    /// <summary>
    /// UI要素にドラッグ&amp;ドロップ機能を設定。
    /// </summary>
    /// <param name="element">対象のUIElement。</param>
    /// <remarks>
    /// <para>【設定内容】</para>
    /// <list type="bullet">
    /// <item>AllowDrop = true</item>
    /// <item>PreviewDragOver: ドラッグ中の処理</item>
    /// <item>Drop: ドロップ時の処理</item>
    /// <item>DragEnter: 視覚フィードバック開始</item>
    /// <item>DragLeave: 視覚フィードバック終了</item>
    /// </list>
    /// </remarks>
    public void SetupDragAndDrop(UIElement element)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));

        element.AllowDrop = true;
        element.PreviewDragOver += OnPreviewDragOver;
        element.Drop += OnDrop;
        element.DragEnter += OnDragEnter;
        element.DragLeave += OnDragLeave;
    }

    /// <summary>
    /// ドラッグオーバー時の処理。
    /// </summary>
    /// <remarks>
    /// サポートされるファイルの場合はCopyエフェクト、
    /// それ以外はNoneエフェクトを設定します。
    /// </remarks>
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
    /// ドラッグ入場時の処理（視覚フィードバック）。
    /// </summary>
    /// <remarks>
    /// サポートされるファイルがドラッグされた場合、
    /// 要素を半透明（Opacity = 0.7）にします。
    /// </remarks>
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
    /// ドラッグ退場時の処理。
    /// </summary>
    /// <remarks>
    /// 要素のOpacityを元に戻します（1.0）。
    /// </remarks>
    private void OnDragLeave(object sender, DragEventArgs e)
    {
        if (sender is UIElement element)
        {
            element.Opacity = 1.0;
        }
    }

    /// <summary>
    /// ドロップ時の処理。
    /// </summary>
    /// <remarks>
    /// <para>【処理内容】</para>
    /// <list type="number">
    /// <item>要素のOpacityを元に戻す</item>
    /// <item>ファイルパスを取得</item>
    /// <item>サポート状況を判定</item>
    /// <item><see cref="FileDropped"/>イベントを発火</item>
    /// </list>
    /// </remarks>
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
        return string.Join(";", _supportedExtensions.Select(e => "*" + e));
    }
}
