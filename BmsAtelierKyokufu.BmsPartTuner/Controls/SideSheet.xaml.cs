using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace BmsAtelierKyokufu.BmsPartTuner.Controls;

/// <summary>
/// Material 3準拠のモーダル・サイドシートコントロール。
/// 右側からスライドインするパネルを提供します。
/// </summary>
public partial class SideSheet : UserControl
{
    private static readonly Duration AnimationDuration = TimeSpan.FromMilliseconds(300);
    private readonly CubicEase _easeOut = new() { EasingMode = EasingMode.EaseOut };
    private readonly CubicEase _easeIn = new() { EasingMode = EasingMode.EaseIn };

    /// <summary>
    /// サイドシートが開いているかどうか。
    /// </summary>
    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(
            nameof(IsOpen),
            typeof(bool),
            typeof(SideSheet),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));

    /// <summary>
    /// サイドシートの幅。
    /// </summary>
    public static readonly DependencyProperty SheetWidthProperty =
        DependencyProperty.Register(
            nameof(SheetWidth),
            typeof(double),
            typeof(SideSheet),
            new PropertyMetadata(400.0, OnSheetWidthChanged));

    /// <summary>
    /// サイドシートの内容。
    /// </summary>
    public new static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(
            nameof(Content),
            typeof(object),
            typeof(SideSheet),
            new PropertyMetadata(null));

    /// <summary>
    /// サイドシートが開いているかどうかを取得または設定します。
    /// </summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>
    /// サイドシートの幅を取得または設定します。
    /// </summary>
    public double SheetWidth
    {
        get => (double)GetValue(SheetWidthProperty);
        set => SetValue(SheetWidthProperty, value);
    }

    /// <summary>
    /// サイドシートの内容を取得または設定します。
    /// </summary>
    public new object Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public SideSheet()
    {
        InitializeComponent();
        UpdateSheetWidth(SheetWidth);
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SideSheet sideSheet)
        {
            var isOpen = (bool)e.NewValue;
            sideSheet.AnimateSheet(isOpen);
        }
    }

    private static void OnSheetWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SideSheet sideSheet)
        {
            sideSheet.UpdateSheetWidth((double)e.NewValue);
        }
    }

    private void UpdateSheetWidth(double width)
    {
        SheetContainer.Width = width;
        if (!IsOpen)
        {
            SheetTranslate.X = width;
        }
    }

    private void AnimateSheet(bool isOpen)
    {
        if (isOpen)
        {
            // 開く: Scrimを表示してからシートをスライドイン
            Scrim.Visibility = Visibility.Visible;

            var scrimAnimation = new DoubleAnimation(0, 1, AnimationDuration) { EasingFunction = _easeOut };
            var sheetAnimation = new DoubleAnimation(SheetWidth, 0, AnimationDuration) { EasingFunction = _easeOut };

            Scrim.BeginAnimation(OpacityProperty, scrimAnimation);
            SheetTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, sheetAnimation);
        }
        else
        {
            // 閉じる: シートをスライドアウトしてからScrimを非表示
            var scrimAnimation = new DoubleAnimation(1, 0, AnimationDuration) { EasingFunction = _easeIn };
            var sheetAnimation = new DoubleAnimation(0, SheetWidth, AnimationDuration) { EasingFunction = _easeIn };

            scrimAnimation.Completed += (s, e) =>
            {
                Scrim.Visibility = Visibility.Collapsed;
            };

            Scrim.BeginAnimation(OpacityProperty, scrimAnimation);
            SheetTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, sheetAnimation);
        }
    }

    private void Scrim_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Scrimクリックで閉じる
        IsOpen = false;
    }
}
