namespace BmsAtelierKyokufu.BmsPartTuner.UI.Views.Dialogs;

/// <summary>
/// SettingsView.xaml の相互作用ロジック
/// </summary>
[ExcludeFromCodeCoverage]
public partial class SettingsView : UserControl
{
    /// <summary>
    /// 閉じるコマンド。
    /// </summary>
    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register(
            nameof(CloseCommand),
            typeof(ICommand),
            typeof(SettingsView),
            new PropertyMetadata(null));

    /// <summary>
    /// 閉じるコマンドを取得または設定します。
    /// </summary>
    public ICommand CloseCommand
    {
        get => (ICommand)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public SettingsView()
    {
        InitializeComponent();
    }
}


