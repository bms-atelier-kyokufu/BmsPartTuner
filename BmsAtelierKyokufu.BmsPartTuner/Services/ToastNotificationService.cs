using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BmsAtelierKyokufu.BmsPartTuner.Controls;
using BmsAtelierKyokufu.BmsPartTuner.ViewModels;

namespace BmsAtelierKyokufu.BmsPartTuner.Services;

/// <summary>
/// トースト通知サービス。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>Material Design風のトースト通知を表示</item>
/// <item>アニメーション（表示→維持→非表示）の制御</item>
/// <item>エラー/通常状態の視覚的区別</item>
/// </list>
/// 
/// <para>【アニメーション】</para>
/// ToastSequence Storyboardを使用して、以下のシーケンスを実行:
/// <list type="number">
/// <item>フェードイン</item>
/// <item>3秒間表示</item>
/// <item>フェードアウト</item>
/// </list>
/// 
/// <para>【テーマ対応】</para>
/// M3ErrorBrush、ToastBackgroundBrushをResourcesから取得し、
/// テーマに応じた色を自動適用します。
/// </remarks>
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
            _showStoryboard.Completed += (s, e) =>
            {
                if (_container != null)
                {
                    _container.Visibility = Visibility.Collapsed;
                }
            };
        }
    }

    /// <summary>
    /// UIコントロールを初期化（ToastControl版）。
    /// </summary>
    /// <param name="control">ToastControlインスタンス。</param>
    /// <remarks>
    /// <para>【Why 2つの初期化メソッド】</para>
    /// 既存コード（MainWindow）との互換性を保ちつつ、
    /// 新しいToastControlコンポーネントにも対応するため。
    /// </remarks>
    public void Initialize(ToastControl control)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));

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
            _showStoryboard.Completed += (s, e) => { _container.Visibility = Visibility.Collapsed; };
        }
    }

    /// <summary>
    /// トースト通知を即座に非表示。
    /// </summary>
    public void Hide()
    {
        if (_container != null)
        {
            _container.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// ステートをリセット。
    /// </summary>
    /// <remarks>
    /// ToastNotificationServiceでは<see cref="Hide"/>と同義です。
    /// </remarks>
    public void Clear()
    {
        Hide();
    }

    /// <summary>
    /// トースト通知を表示。
    /// </summary>
    /// <param name="data">表示するデータ。</param>
    /// <remarks>
    /// <para>【処理内容】</para>
    /// <list type="number">
    /// <item>前のアニメーションを停止（連続クリック対策）</item>
    /// <item>メッセージとアイコンを設定</item>
    /// <item>エラー状態に応じて背景色を変更</item>
    /// <item>アニメーション開始</item>
    /// </list>
    /// 
    /// <para>【テーマ対応】</para>
    /// <list type="bullet">
    /// <item>エラー: M3ErrorBrush（Material 3 Error Color）</item>
    /// <item>通常: ToastBackgroundBrush（テーマに応じた背景色）</item>
    /// </list>
    /// </remarks>
    public void Show(ToastViewModel data)
    {
        if (_container == null)
            throw new InvalidOperationException("Initialize()を先に呼び出してください");

        _showStoryboard?.Stop();

        if (_message != null) _message.Text = data.Message;
        if (_icon != null) _icon.Text = data.Icon;

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
