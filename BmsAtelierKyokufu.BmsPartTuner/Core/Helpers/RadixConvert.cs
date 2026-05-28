namespace BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;

/// <summary>
/// 10進数 ⇔ 62進数（ZZ形式）の相互変換を行うヘルパークラス。
/// BMS定義番号の文字列表現をサポートし、配列ベースのルックアップテーブルを用いてO(1)で高速に変換します。
/// </summary>
public static class RadixConvert
{

    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    // 逆ルックアップテーブル: 文字から値へのマッピング。ASCII範囲で最大の文字'z'(122)まで対応。
    private static readonly byte[] CharToIntLookup = CreateCharToIntLookup();

    private static byte[] CreateCharToIntLookup()
    {
        var lookup = new byte[123];
        for (int i = 0; i < Alphabet.Length; i++)
        {
            lookup[Alphabet[i]] = (byte)i;
        }
        return lookup;
    }

    /// <summary>
    /// 数値を指定された基数で2桁の文字列に変換します。
    /// （例：Base36の場合、35 → "0Z"）
    /// </summary>
    /// <param name="dec">10進数値。</param>
    /// <param name="radix">基数（36または62、デフォルト: 36）。</param>
    /// <returns>2桁の文字列（例: "0z"）。</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dec"/>が負の値、または指定された基数での最大値を超える場合。
    /// <paramref name="radix"/>が36または62以外の場合。
    /// </exception>
    public static string IntToZZ(int dec, int radix = AppConstants.Definition.RadixBase36)
    {
        // 基数の検証 - 無効な基数はBase62にフォールバック
        if (radix != AppConstants.Definition.RadixBase36 && radix != AppConstants.Definition.RadixBase62)
        {
            radix = AppConstants.Definition.RadixBase62;
        }

        // 負の値チェック
        if (dec < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dec),
                dec,
                "負の値は変換できません。");
        }

        // 最大値チェック（配列境界を超える前に検証）
        // Base36: 36*36-1 = 1295 (ZZ), Base62: 62*62-1 = 3843 (zz)
        int limit = (radix * radix) - 1;
        if (dec > limit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dec),
                dec,
                $"指定された値が{radix}進数の2桁表現の最大値({limit})を超えています。");
        }

        return string.Create(2, (dec, radix), (span, state) =>
        {
            span[0] = Alphabet[state.dec / state.radix];
            span[1] = Alphabet[state.dec % state.radix];
        });
    }

    /// <summary>
    /// 文字列を指定された基数で数値に変換します。
    /// ルックアップテーブルを使用して、文字列パースを不要にしO(1)の高速変換を行います。
    /// </summary>
    /// <param name="zz">2桁の文字列（例: "0z"）。</param>
    /// <param name="radix">基数（36または62、デフォルト: 36）。</param>
    /// <returns>10進数値。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="zz"/>がnullの場合。
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="zz"/>が2文字でない場合、または無効な文字を含む場合。
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="radix"/>が36または62以外の場合。
    /// </exception>
    public static int ZZToInt(string zz, int radix = AppConstants.Definition.RadixBase36)
    {
        // null チェック
        if (zz == null)
        {
            throw new ArgumentNullException(nameof(zz), "入力文字列がnullです。");
        }

        // 長さチェック
        if (zz.Length != 2)
        {
            throw new ArgumentException(
                $"入力は2文字である必要があります。実際: {zz.Length}文字",
                nameof(zz));
        }

        // 基数の検証
        if (radix != AppConstants.Definition.RadixBase36 && radix != AppConstants.Definition.RadixBase62)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radix),
                radix,
                $"基数は{AppConstants.Definition.RadixBase36}または{AppConstants.Definition.RadixBase62}である必要があります。");
        }

        // 文字の有効性チェック
        var char0 = zz[0];
        var char1 = zz[1];

        // ASCII範囲外またはルックアップテーブル範囲外の文字をチェック
        if (char0 < 0 || char0 >= CharToIntLookup.Length ||
            char1 < 0 || char1 >= CharToIntLookup.Length)
        {
            throw new ArgumentException(
                $"無効な文字が含まれています: '{zz}'",
                nameof(zz));
        }

        var value0 = CharToIntLookup[char0];
        var value1 = CharToIntLookup[char1];

        // 基数に対する値の範囲チェック
        // ルックアップテーブルで0が返された場合、それが'0'文字なのか未定義文字なのかを判定
        if ((value0 == 0 && char0 != '0') || (value1 == 0 && char1 != '0') ||
            value0 >= radix || value1 >= radix)
        {
            throw new ArgumentException(
                $"文字 '{zz}' は{radix}進数として無効です。",
                nameof(zz));
        }

        return (value0 * radix) + value1;
    }
}
