namespace BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;

/// <summary>
/// 62進数変換ヘルパークラス。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>10進数 ⇔ 62進数（ZZ形式）の相互変換</item>
/// <item>BMS定義番号の文字列表現をサポート</item>
/// </list>
/// 
/// <para>【BMS定義番号の仕様】</para>
/// BMSフォーマットでは、定義番号を以下の文字セットで表現します:
/// <list type="bullet">
/// <item>0-9: 0～9（10種）</item>
/// <item>A-Z: 10～35（26種）</item>
/// <item>a-z: 36～61（26種）</item>
/// </list>
/// 
/// 合計62種の文字を使用し、2桁で00～zz（0～3843）を表現可能。
/// 
/// <para>【36進数 vs 62進数】</para>
/// <list type="bullet">
/// <item>36進数: 0-9, A-Z のみ（従来のBMS仕様）</item>
/// <item>62進数: 0-9, A-Z, a-z すべて（拡張仕様、BMS++等）</item>
/// </list>
/// 
/// <para>【ルックアップテーブル最適化】</para>
/// 文字 ⇔ 値の変換を配列ベースのルックアップテーブルで高速化。
/// 計算量: O(1)（文字列パースやループ不要）
/// 
/// <para>【例】</para>
/// <code>
/// IntToZZ(0) → "00"
/// IntToZZ(35) → "0z" (36進数)
/// IntToZZ(35) → "0Z" (62進数)
/// IntToZZ(61) → "0z" (62進数)
/// IntToZZ(3843) → "zz" (62進数)
/// 
/// ZZToInt("00") → 0
/// ZZToInt("0z", Base36) → 35
/// ZZToInt("zz", Base62) → 3843
/// </code>
/// </remarks>
public static class RadixConvert
{

    // ルックアップテーブル: 0-61の値を対応する文字にマッピング
    private static readonly char[] IntToCharLookup = new char[62]
    {
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',  // 0-9
        'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',  // 10-19
        'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',  // 20-29
        'U', 'V', 'W', 'X', 'Y', 'Z',                      // 30-35
        'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',  // 36-45
        'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',  // 46-55
        'u', 'v', 'w', 'x', 'y', 'z'                       // 56-61
    };

    // 逆ルックアップテーブル: 文字から値へのマッピング
    // ASCII範囲で最大の文字'z'(122)まで対応
    private static readonly byte[] CharToIntLookup = new byte[123];

    static RadixConvert()
    {
        // 初期化: 無効な文字は0を返す
        for (int i = 0; i < CharToIntLookup.Length; i++)
        {
            CharToIntLookup[i] = 0;
        }

        // 0-9
        for (int i = 0; i < 10; i++)
        {
            CharToIntLookup['0' + i] = (byte)i;
        }

        // A-Z (10-35)
        for (int i = 0; i < 26; i++)
        {
            CharToIntLookup['A' + i] = (byte)(10 + i);
        }

        // a-z (36-61)
        for (int i = 0; i < 26; i++)
        {
            CharToIntLookup['a' + i] = (byte)(36 + i);
        }
    }

    /// <summary>
    /// 数値を指定された基数で2桁の文字列に変換。
    /// </summary>
    /// <param name="dec">10進数値。</param>
    /// <param name="radix">基数（36または62、デフォルト: 36）。</param>
    /// <returns>2桁の文字列（例: "0z"）。</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dec"/>が負の値、または指定された基数での最大値を超える場合。
    /// <paramref name="radix"/>が36または62以外の場合。
    /// </exception>
    /// <remarks>
    /// <para>【変換式】</para>
    /// 2桁表記 = [dec / radix][dec % radix]
    /// 
    /// <para>【例】</para>
    /// IntToZZ(35, 36) → "0Z" （35 = 0 * 36 + 35）
    /// IntToZZ(100, 62) → "1K" （100 = 1 * 62 + 38）
    /// 
    /// <para>【有効範囲】</para>
    /// Base36: 0 ～ 1295 (ZZ)
    /// Base62: 0 ～ 3843 (zz)
    /// </remarks>
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

        return new string(new char[]
        {
            IntToCharLookup[dec / radix],
            IntToCharLookup[dec % radix],
        });
    }

    /// <summary>
    /// 文字列を指定された基数で数値に変換。
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
    /// <remarks>
    /// <para>【変換式】</para>
    /// 数値 = (1桁目の値 × radix) + 2桁目の値
    /// 
    /// <para>【例】</para>
    /// ZZToInt("0Z", 36) → 35 （0 * 36 + 35）
    /// ZZToInt("1K", 62) → 100 （1 * 62 + 38）
    /// 
    /// <para>【有効文字】</para>
    /// Base36: 0-9, A-Z
    /// Base62: 0-9, A-Z, a-z
    /// 
    /// <para>【Why ルックアップテーブル】</para>
    /// 文字コードから値を直接取得することで、
    /// 文字列パース（IndexOf, Substring等）を不要にし、
    /// O(1)の高速変換を実現します。
    /// </remarks>
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

        return value0 * radix + value1;
    }
}
