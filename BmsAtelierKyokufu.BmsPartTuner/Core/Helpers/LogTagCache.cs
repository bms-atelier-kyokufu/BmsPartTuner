namespace BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;

/// <summary>
/// 各型のロガータグ文字列をキャッシュするクラス。
/// ソースジェネレータによってコンパイル時に型名が登録され、リフレクションなしでアクセスできます。
/// </summary>
public static class LogTagCache<T>
{
    public static string Tag { get; set; }

    static LogTagCache()
    {
        Tag = typeof(T).Name;
    }
}
