using Microsoft.Win32;

namespace BmsAtelierKyokufu.BmsPartTuner.UI.Services;

/// <summary>
/// アプリケーションのテーマ切り替えを管理するサービス。
/// </summary>
[ExcludeFromCodeCoverage]
public class ThemeService
{
    private static readonly Logger<ThemeService> s_logger = new();
    private const string LightThemePath = "/Themes/LightTheme.xaml";
    private const string DarkThemePath = "/Themes/DarkTheme.xaml";
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>
    /// テーマが変更されたときに発生するイベント。
    /// </summary>
    public event EventHandler<bool>? ThemeChanged;

    /// <summary>
    /// 現在ダークテーマが適用されているかどうか。
    /// </summary>
    public bool IsDarkTheme { get; private set; }

    /// <summary>
    /// 指定されたテーマを適用します。
    /// テーマファイルにはすべてのColor定義とBrush定義が含まれているため、
    /// テーマファイルを差し替えるだけでリアルタイムにテーマが切り替わります。
    /// </summary>
    /// <param name="isDark">ダークテーマを適用する場合はtrue</param>
    public virtual void ApplyTheme(bool isDark)
    {
        var themePath = isDark ? DarkThemePath : LightThemePath;

        try
        {
            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

            s_logger.WriteDebug( $"テーマ切り替え開始: {(isDark ? "Dark" : "Light")}");

            // 新しいテーマを読み込む
            var newTheme = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };

            // 既存のテーマを検索
            int themeIndex = -1;
            for (int i = 0; i < mergedDictionaries.Count; i++)
            {
                var source = mergedDictionaries[i].Source?.OriginalString;
                if (source != null && (source.Contains("LightTheme.xaml") || source.Contains("DarkTheme.xaml")))
                {
                    themeIndex = i;
                    break;
                }
            }

            // テーマを差し替え
            if (themeIndex >= 0)
            {
                mergedDictionaries.RemoveAt(themeIndex);
                mergedDictionaries.Insert(themeIndex, newTheme);
            }
            else
            {
                // テーマが見つからない場合は先頭に追加
                mergedDictionaries.Insert(0, newTheme);
            }

            IsDarkTheme = isDark;

            // UI要素の強制再描画を実行
            ForceUiRefresh();

            ThemeChanged?.Invoke(this, isDark);

            s_logger.WriteDebug( $"テーマを適用しました: {(isDark ? "Dark" : "Light")}");
        }
        catch (Exception ex)
        {
            s_logger.WriteDebug( $"テーマの適用に失敗しました: {ex.Message}");
            s_logger.WriteDebug( ex.StackTrace ?? string.Empty);
        }
    }

    /// <summary>
    /// システムがダークモードかどうかを判定します。
    /// </summary>
    public static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int intValue && intValue == 0;
        }
        catch (Exception ex)
        {
            s_logger.WriteDebug( $"システムテーマの読み取りエラー: {ex}");
            return false;
        }
    }

    /// <summary>
    /// システムテーマに追従してテーマを適用します。
    /// </summary>
    public virtual void ApplySystemTheme()
    {
        ApplyTheme(IsSystemDarkMode());
    }

    /// <summary>
    /// UI要素を強制的に再描画します。
    /// テーマ切り替え時に、一部のUI要素（絵文字、アイコンなど）が動的リソースを参照していても
    /// 更新されない場合があるため、全てのWindowとその子要素を再描画します。
    /// </summary>
    private static void ForceUiRefresh()
    {
        Application.Current?.Dispatcher.BeginInvoke(new Action(static () =>
        {
            // 全てのWindowを列挙して無効化（再描画トリガー）
            foreach (Window window in Application.Current.Windows)
            {
                if (window != null)
                {
                    window.InvalidateVisual();

                    // 子要素も再帰的に無効化
                    InvalidateVisualTree(window);
                }
            }
        }), System.Windows.Threading.DispatcherPriority.Render);
    }

    /// <summary>
    /// ビジュアルツリーを再帰的に無効化します。
    /// </summary>
    private static void InvalidateVisualTree(DependencyObject parent)
    {
        if (parent == null) return;

        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is UIElement element)
            {
                element.InvalidateVisual();
            }

            // 再帰的に子要素を処理
            InvalidateVisualTree(child);
        }
    }
}

