using System.Diagnostics.CodeAnalysis;
using System.Windows.Media.Animation;
using BmsAtelierKyokufu.BmsPartTuner.UI.ViewModels;
using BmsAtelierKyokufu.BmsPartTuner.UI.Views.Controls;

namespace BmsAtelierKyokufu.BmsPartTuner.UI.Services;

/// <summary>
/// Material Design風のトースト通知を表示し、アニメーション（表示→維持→非表示）や
/// エラー状態の視覚的区別を制御するサービス。
/// </summary>
[ExcludeFromCodeCoverage]
public class ToastNotificationService : IUiElementService<ToastViewModel>
{
    private Border? _container;
    private TextBlock? _icon;
    private TextBlock? _message;
    private Storyboard? _showStoryboard;

    /// <summary>トースト通知が表示されているかどうか。</summary>
    public bool IsVisible => _container != null && _container.Visibility == Visibility.Visible;

    /// <summary>
    /// デフォルトコンストラクタ（DIコンテナ用）。
    /// </summary>
    public ToastNotificationService()
    {
    }

    /// <summary>
    /// UIコントロールを初期化（個別要素版）。
    /// </summary>
    /// <param name="container">コンテナBorder。</param>
    /// <param name="icon">アイコンTextBlock。</param>
    /// <param name="message">メッセージTextBlock。</param>
    public void Initialize(Border container, TextBlock icon, TextBlock message)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _icon = icon ?? throw new ArgumentNullException(nameof(icon));
        _message = message ?? throw new ArgumentNullException(nameof(message));

        if (Application.Current.MainWindow?.Resources["ToastSequence"] is Storyboard toastSequence)
        {
            _showStoryboard = toastSequence;
            _showStoryboard.Completed += (s, e) => _container?.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// UIコントロールを初期化（ToastControl版）。
    /// 既存コード（MainWindow）との互換性を保ちつつ、新しいToastControlコンポーネントにも対応します。
    /// </summary>
    /// <param name="control">ToastControlインスタンス。</param>
    public void Initialize(ToastControl control)
    {
        ArgumentNullException.ThrowIfNull(control);

        _container = control.FindName("ToastNotification") as Border ?? throw new InvalidOperationException("ToastControl template does not contain ToastNotification border");

        _icon = _container.FindName("Icon") as TextBlock;
        _message = _container.FindName("Message") as TextBlock;

        if (control.Resources["ToastSequence"] is Storyboard localStoryboard)
        {
            _showStoryboard = localStoryboard;
        }
        else if (Application.Current.MainWindow?.Resources["ToastSequence"] is Storyboard appStoryboard)
        {
            _showStoryboard = appStoryboard;
        }

        if (_showStoryboard != null && _container != null)
        {
            _showStoryboard.Completed += (s, e) => _container.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// トースト通知を即座に非表示。
    /// </summary>
    public void Hide()
    {
        _container?.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// ステートをリセットします。Hide()と同義です。
    /// </summary>
    public void Clear()
    {
        Hide();
    }

    /// <summary>
    /// 前のアニメーションを停止し、メッセージとアイコンを設定してトースト通知を表示します。
    /// エラー状態に応じてテーマ対応の背景色を自動的に適用し、フェードインアニメーションを開始します。
    /// </summary>
    /// <param name="data">表示するデータ。</param>
    public void Show(ToastViewModel data)
    {
        if (_container == null)
            throw new InvalidOperationException("Initialize()を先に呼び出してください");

        _showStoryboard?.Stop();

        _message?.Text = data.Message;
        _icon?.Text = data.Icon;

        if (data.IsError)
        {
            _container.Background = Application.Current.TryFindResource("M3ErrorBrush") as Brush
                ?? new SolidColorBrush(Color.FromRgb(179, 38, 30));
        }
        else
        {
            _container.Background = Application.Current.TryFindResource("ToastBackgroundBrush") as Brush
                ?? new SolidColorBrush(Color.FromRgb(50, 50, 50));
        }

        _container.Visibility = Visibility.Visible;

        _showStoryboard?.Begin();
    }
}
