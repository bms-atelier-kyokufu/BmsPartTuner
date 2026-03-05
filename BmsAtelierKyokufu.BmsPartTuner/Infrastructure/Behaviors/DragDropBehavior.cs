using System.Windows;
using Microsoft.Xaml.Behaviors;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Behaviors;

/// <summary>
/// ドラッグ＆ドロップをサポートするBehavior。
/// コードビハインドからのDrag&Drop処理をXAMLに移動します。
/// </summary>
/// <remarks>
/// <para>【WPF原則】</para>
/// MVVM パターンに準拠するため、イベントハンドラーをコードビハインドから
/// Behavior に移動し、XAML でバインド可能にします。
/// 
/// <para>【使用例】</para>
/// <code>
/// &lt;TextBox&gt;
///     &lt;i:Interaction.Behaviors&gt;
///         &lt;behaviors:DragDropBehavior 
///             SupportedExtensions=".bms,.bme,.bml,.pms"
///             DroppedFilePath="{Binding InputPath, Mode=TwoWay}"
///             DropSuccessCommand="{Binding FileDroppedCommand}"
///             DropFailureCommand="{Binding UnsupportedFileDroppedCommand}" /&gt;
///     &lt;/i:Interaction.Behaviors&gt;
/// &lt;/TextBox&gt;
/// </code>
/// </remarks>
public class DragDropBehavior : Behavior<UIElement>
{
    #region 依存関係プロパティ

    /// <summary>
    /// サポートする拡張子（カンマ区切り、例: ".bms,.bme,.bml,.pms"）
    /// </summary>
    public static readonly DependencyProperty SupportedExtensionsProperty =
        DependencyProperty.Register(
            nameof(SupportedExtensions),
            typeof(string),
            typeof(DragDropBehavior),
            new PropertyMetadata(".bms,.bme,.bml,.pms"));

    public string SupportedExtensions
    {
        get => (string)GetValue(SupportedExtensionsProperty);
        set => SetValue(SupportedExtensionsProperty, value);
    }

    /// <summary>
    /// ドロップされたファイルパスをバインドするプロパティ。
    /// </summary>
    public static readonly DependencyProperty DroppedFilePathProperty =
        DependencyProperty.Register(
            nameof(DroppedFilePath),
            typeof(string),
            typeof(DragDropBehavior),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string DroppedFilePath
    {
        get => (string)GetValue(DroppedFilePathProperty);
        set => SetValue(DroppedFilePathProperty, value);
    }

    /// <summary>
    /// ドロップ成功時に実行するコマンド。
    /// コマンドパラメータにファイルパスが渡されます。
    /// </summary>
    public static readonly DependencyProperty DropSuccessCommandProperty =
        DependencyProperty.Register(
            nameof(DropSuccessCommand),
            typeof(ICommand),
            typeof(DragDropBehavior),
            new PropertyMetadata(null));

    public ICommand? DropSuccessCommand
    {
        get => (ICommand?)GetValue(DropSuccessCommandProperty);
        set => SetValue(DropSuccessCommandProperty, value);
    }

    /// <summary>
    /// ドロップ失敗（非対応形式）時に実行するコマンド。
    /// コマンドパラメータにファイルパスが渡されます。
    /// </summary>
    public static readonly DependencyProperty DropFailureCommandProperty =
        DependencyProperty.Register(
            nameof(DropFailureCommand),
            typeof(ICommand),
            typeof(DragDropBehavior),
            new PropertyMetadata(null));

    public ICommand? DropFailureCommand
    {
        get => (ICommand?)GetValue(DropFailureCommandProperty);
        set => SetValue(DropFailureCommandProperty, value);
    }

    #endregion

    #region Behavior実装

    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject.AllowDrop = true;
        AssociatedObject.DragOver += OnDragOver;
        AssociatedObject.Drop += OnDrop;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.DragOver -= OnDragOver;
        AssociatedObject.Drop -= OnDrop;

        base.OnDetaching();
    }

    #endregion

    #region イベントハンドラー

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0 && IsSupportedFile(files[0]))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;


        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files == null || files.Length == 0)
            return;

        var filePath = files[0];

        if (IsSupportedFile(filePath))
        {
            // ファイルパスをバインドされたプロパティに設定
            DroppedFilePath = filePath;

            // 成功コマンドを実行
            if (DropSuccessCommand?.CanExecute(filePath) == true)
            {
                DropSuccessCommand.Execute(filePath);
            }
        }
        else
        {
            // 失敗コマンドを実行
            if (DropFailureCommand?.CanExecute(filePath) == true)
            {
                DropFailureCommand.Execute(filePath);
            }
        }

        e.Handled = true;
    }

    private bool IsSupportedFile(string filePath)
    {
        if (string.IsNullOrEmpty(SupportedExtensions))
            return true;

        var extension = System.IO.Path.GetExtension(filePath)?.ToLowerInvariant();
        var supportedList = SupportedExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries);

        return supportedList.Any(ext => ext.Trim().Equals(extension, StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
