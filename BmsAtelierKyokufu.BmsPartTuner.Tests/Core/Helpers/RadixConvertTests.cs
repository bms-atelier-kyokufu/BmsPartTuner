using BmsAtelierKyokufu.BmsPartTuner.Core;
using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Helpers;

/// <summary>
/// <see cref="RadixConvert"/> のテストクラス。
/// 
/// BMSの定義番号（#WAV01 ～ #WAVzz）は、基本的に36進数（0-9, A-Z）ですが、
/// 拡張仕様として62進数（0-9, A-Z, a-z）をサポートする場合があります。
/// ここではそれぞれの基数変換が正しく行われるか検証します。
/// 
/// 【テスト対象】
/// - IntToZZ: 10進数 → 36進数/62進数文字列
/// - ZZToInt: 36進数/62進数文字列 → 10進数
/// 
/// 【テスト設計方針】
/// - 境界値分析: 00, 0Z, ZZ（36進数）、00, 0z, zz（62進数）
/// - 同値分割: 有効値、境界値、大文字小文字混在
/// - 往復変換: 変換の一貫性検証
/// </summary>
public class RadixConvertTests
{
    #region IntToZZ Tests - 10進数から文字列への変換

    #region 36進数変換 (Base36)

    [Theory]
    [InlineData(0, "00")]      // 最小値
    [InlineData(1, "01")]      // 最小有効値
    [InlineData(9, "09")]      // 1桁数字の最大
    [InlineData(10, "0A")]     // アルファベット開始
    [InlineData(35, "0Z")]     // 1桁目の最大（36進数）
    [InlineData(36, "10")]     // 2桁目が1になる最小値
    [InlineData(100, "2S")]    // 中間値
    [InlineData(1295, "ZZ")]   // 36進数最大値（36^2 - 1）
    public void IntToZZ_Base36_ReturnsCorrectString(int input, string expected)
    {
        // Act
        var result = RadixConvert.IntToZZ(input, AppConstants.Definition.RadixBase36);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IntToZZ_DefaultRadix_UsesBase36()
    {
        // Act
        var result = RadixConvert.IntToZZ(35);

        // Assert
        Assert.Equal("0Z", result);
    }

    #endregion

    #region 62進数変換 (Base62)

    [Theory]
    [InlineData(0, "00")]      // 最小値
    [InlineData(35, "0Z")]     // 大文字アルファベット最大
    [InlineData(36, "0a")]     // 小文字アルファベット開始
    [InlineData(61, "0z")]     // 1桁目の最大（62進数）
    [InlineData(62, "10")]     // 2桁目が1になる最小値
    [InlineData(100, "1c")]    // 中間値（62 + 38 = 100）
    [InlineData(3843, "zz")]   // 62進数最大値（62^2 - 1）
    public void IntToZZ_Base62_ReturnsCorrectString(int input, string expected)
    {
        // Act
        var result = RadixConvert.IntToZZ(input, AppConstants.Definition.RadixBase62);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region 無効な基数

    [Theory]
    [InlineData(0)]    // 無効
    [InlineData(10)]   // 無効
    [InlineData(37)]   // 無効
    [InlineData(100)]  // 無効
    public void IntToZZ_InvalidRadix_FallsBackToBase62(int invalidRadix)
    {
        // 仕様: 無効な基数は62進数にフォールバック
        // Act
        var result = RadixConvert.IntToZZ(61, invalidRadix);

        // Assert
        Assert.Equal("0z", result);  // 62進数での61 = "0z"
    }

    #endregion

    #endregion

    #region 無効入力の処理

    #region IntToZZ - 範囲外入力テスト

    [Theory]
    [InlineData(-1, AppConstants.Definition.RadixBase36)]
    [InlineData(-100, AppConstants.Definition.RadixBase36)]
    [InlineData(-1, AppConstants.Definition.RadixBase62)]
    public void IntToZZ_NegativeValue_ThrowsArgumentOutOfRangeException(int value, int radix)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadixConvert.IntToZZ(value, radix));

        Assert.Equal("dec", ex.ParamName);
        Assert.Contains("負の値", ex.Message);
    }

    [Theory]
    [InlineData(1296, AppConstants.Definition.RadixBase36)]  // ZZの次 (36^2)
    [InlineData(2000, AppConstants.Definition.RadixBase36)]
    [InlineData(3844, AppConstants.Definition.RadixBase62)]  // zzの次 (62^2)
    [InlineData(5000, AppConstants.Definition.RadixBase62)]
    public void IntToZZ_ExceedsMaxValue_ThrowsArgumentOutOfRangeException(int value, int radix)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadixConvert.IntToZZ(value, radix));

        Assert.Equal("dec", ex.ParamName);
        Assert.Contains("最大値", ex.Message);
    }

    #endregion

    #region ZZToInt - 無効入力テスト

    [Fact]
    public void ZZToInt_NullString_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            RadixConvert.ZZToInt(null!, AppConstants.Definition.RadixBase36));

        Assert.Contains("zz", ex.ParamName);
    }

    [Theory]
    [InlineData("", AppConstants.Definition.RadixBase36)]
    [InlineData("A", AppConstants.Definition.RadixBase36)]    // 1文字
    [InlineData("ZZZ", AppConstants.Definition.RadixBase36)]  // 3文字
    [InlineData("", AppConstants.Definition.RadixBase62)]
    [InlineData("z", AppConstants.Definition.RadixBase62)]
    [InlineData("zzz", AppConstants.Definition.RadixBase62)]
    public void ZZToInt_InvalidLength_ThrowsArgumentException(string input, int radix)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            RadixConvert.ZZToInt(input, radix));

        Assert.Contains("2文字", ex.Message);
    }

    [Theory]
    [InlineData("!!", AppConstants.Definition.RadixBase36)]
    [InlineData("@#", AppConstants.Definition.RadixBase62)]
    [InlineData("0!", AppConstants.Definition.RadixBase36)]
    [InlineData("!0", AppConstants.Definition.RadixBase62)]
    public void ZZToInt_InvalidCharacters_ThrowsArgumentException(string input, int radix)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            RadixConvert.ZZToInt(input, radix));

        Assert.Contains("無効", ex.Message);
    }

    [Theory]
    [InlineData("0a", AppConstants.Definition.RadixBase36)]  // 小文字はBase36では無効
    [InlineData("0z", AppConstants.Definition.RadixBase36)]  // 小文字はBase36では無効
    [InlineData("zz", AppConstants.Definition.RadixBase36)]  // 小文字はBase36では無効
    public void ZZToInt_Base36_LowercaseLetters_ThrowsArgumentException(string input, int radix)
    {
        // Base36では小文字は範囲外（36以上の値にマップされるため例外スロー）
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            RadixConvert.ZZToInt(input, radix));

        Assert.Contains("無効", ex.Message);
    }

    [Theory]
    [InlineData("00", 10)]
    [InlineData("ZZ", 0)]
    [InlineData("zz", -1)]
    [InlineData("0z", 37)]
    [InlineData("1c", 100)]
    public void ZZToInt_InvalidRadix_ThrowsArgumentOutOfRangeException(string input, int radix)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadixConvert.ZZToInt(input, radix));

        Assert.Equal(nameof(radix), ex.ParamName);
        Assert.Contains("基数", ex.Message);
    }

    #endregion

    #endregion

    #region ZZToInt Tests - 文字列から10進数への変換

    #region 36進数変換 (Base36)

    [Theory]
    [InlineData("00", 0)]      // 最小値
    [InlineData("01", 1)]      // 最小有効値
    [InlineData("09", 9)]      // 1桁数字の最大
    [InlineData("0A", 10)]     // アルファベット開始
    [InlineData("0Z", 35)]     // 1桁目の最大
    [InlineData("10", 36)]     // 2桁目が1になる最小値
    [InlineData("2S", 100)]    // 中間値
    [InlineData("ZZ", 1295)]   // 36進数最大値
    public void ZZToInt_Base36_ReturnsCorrectValue(string input, int expected)
    {
        // Act
        var result = RadixConvert.ZZToInt(input, AppConstants.Definition.RadixBase36);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ZZToInt_DefaultRadix_UsesBase36()
    {
        // Act
        var result = RadixConvert.ZZToInt("0Z");

        // Assert
        Assert.Equal(35, result);
    }

    #endregion

    #region 62進数変換 (Base62)

    [Theory]
    [InlineData("00", 0)]      // 最小値
    [InlineData("0Z", 35)]     // 大文字アルファベット最大
    [InlineData("0a", 36)]     // 小文字アルファベット開始
    [InlineData("0z", 61)]     // 1桁目の最大
    [InlineData("10", 62)]     // 2桁目が1になる最小値
    [InlineData("1c", 100)]    // 中間値
    [InlineData("zz", 3843)]   // 62進数最大値
    public void ZZToInt_Base62_ReturnsCorrectValue(string input, int expected)
    {
        // Act
        var result = RadixConvert.ZZToInt(input, AppConstants.Definition.RadixBase62);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region 大文字小文字の混在（BMSドメイン特有）

    [Theory]
    [InlineData("0a", 36)]     // 小文字
    [InlineData("0A", 10)]     // 大文字（36進数でも62進数でも10）
    [InlineData("aA", 36 * 62 + 10)]  // 小文字大文字混在
    [InlineData("Aa", 10 * 62 + 36)]  // 大文字小文字混在
    public void ZZToInt_MixedCase_DistinguishesCorrectly(string input, int expected)
    {
        // 62進数では大文字小文字は別の値
        // Act
        var result = RadixConvert.ZZToInt(input, AppConstants.Definition.RadixBase62);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #endregion

    #region Roundtrip Tests - 往復変換の一貫性

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(35)]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1295)]  // 36進数最大
    public void Roundtrip_Base36_PreservesValue(int original)
    {
        // Act
        var str = RadixConvert.IntToZZ(original, AppConstants.Definition.RadixBase36);
        var result = RadixConvert.ZZToInt(str, AppConstants.Definition.RadixBase36);

        // Assert
        Assert.Equal(original, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(61)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(3843)]  // 62進数最大
    public void Roundtrip_Base62_PreservesValue(int original)
    {
        // Act
        var str = RadixConvert.IntToZZ(original, AppConstants.Definition.RadixBase62);
        var result = RadixConvert.ZZToInt(str, AppConstants.Definition.RadixBase62);

        // Assert
        Assert.Equal(original, result);
    }

    [Theory]
    [InlineData("00")]
    [InlineData("01")]
    [InlineData("0Z")]
    [InlineData("ZZ")]
    public void Roundtrip_StringToIntToString_Base36_PreservesValue(string original)
    {
        // Act
        var num = RadixConvert.ZZToInt(original, AppConstants.Definition.RadixBase36);
        var result = RadixConvert.IntToZZ(num, AppConstants.Definition.RadixBase36);

        // Assert
        Assert.Equal(original, result);
    }

    [Theory]
    [InlineData("00")]
    [InlineData("0z")]
    [InlineData("1c")]
    [InlineData("zz")]
    public void Roundtrip_StringToIntToString_Base62_PreservesValue(string original)
    {
        // Act
        var num = RadixConvert.ZZToInt(original, AppConstants.Definition.RadixBase62);
        var result = RadixConvert.IntToZZ(num, AppConstants.Definition.RadixBase62);

        // Assert
        Assert.Equal(original, result);
    }

    #endregion

    #region AppConstants Integration - 定数との整合性

    [Fact]
    public void IntToZZ_MaxDefinitionNumberBase36_ReturnsZZ()
    {
        // Arrange
        int maxBase36 = AppConstants.Definition.MaxNumberBase36;  // 1295

        // Act
        var result = RadixConvert.IntToZZ(maxBase36, AppConstants.Definition.RadixBase36);

        // Assert
        Assert.Equal("ZZ", result);
    }

    [Fact]
    public void IntToZZ_MaxDefinitionNumberBase62_Returnszz()
    {
        // Arrange
        int maxBase62 = AppConstants.Definition.MaxNumberBase62;  // 3843

        // Act
        var result = RadixConvert.IntToZZ(maxBase62, AppConstants.Definition.RadixBase62);

        // Assert
        Assert.Equal("zz", result);
    }

    [Fact]
    public void ZZToInt_ZZ_ReturnsMaxDefinitionNumberBase36()
    {
        // Act
        var result = RadixConvert.ZZToInt("ZZ", AppConstants.Definition.RadixBase36);

        // Assert
        Assert.Equal(AppConstants.Definition.MaxNumberBase36, result);
    }

    [Fact]
    public void ZZToInt_zz_ReturnsMaxDefinitionNumberBase62()
    {
        // Act
        var result = RadixConvert.ZZToInt("zz", AppConstants.Definition.RadixBase62);

        // Assert
        Assert.Equal(AppConstants.Definition.MaxNumberBase62, result);
    }

    #endregion

    #region Edge Cases - エッジケース

    [Fact]
    public void IntToZZ_Zero_Returns00()
    {
        // Act
        var result36 = RadixConvert.IntToZZ(0, AppConstants.Definition.RadixBase36);
        var result62 = RadixConvert.IntToZZ(0, AppConstants.Definition.RadixBase62);

        // Assert
        Assert.Equal("00", result36);
        Assert.Equal("00", result62);
    }

    [Theory]
    [InlineData(AppConstants.Definition.RadixBase36)]
    [InlineData(AppConstants.Definition.RadixBase62)]
    public void IntToZZ_MinDefinitionNumber_Returns01(int radix)
    {
        // Arrange
        int minDef = AppConstants.Definition.MinNumber;  // 1

        // Act
        var result = RadixConvert.IntToZZ(minDef, radix);

        // Assert
        Assert.Equal("01", result);
    }

    #endregion

    #region All Valid Characters Tests - 全文字のテスト

    [Fact]
    public void CharToIntLookup_AllDigits_ReturnCorrectValues()
    {
        // 0-9
        for (int i = 0; i <= 9; i++)
        {
            var str = $"0{i}";
            var result = RadixConvert.ZZToInt(str, AppConstants.Definition.RadixBase62);
            Assert.Equal(i, result);
        }
    }

    [Fact]
    public void CharToIntLookup_AllUppercase_ReturnCorrectValues()
    {
        // A-Z (10-35)
        for (int i = 0; i < 26; i++)
        {
            char c = (char)('A' + i);
            var str = $"0{c}";
            var result = RadixConvert.ZZToInt(str, AppConstants.Definition.RadixBase62);
            Assert.Equal(10 + i, result);
        }
    }

    [Fact]
    public void CharToIntLookup_AllLowercase_ReturnCorrectValues()
    {
        // a-z (36-61)
        for (int i = 0; i < 26; i++)
        {
            char c = (char)('a' + i);
            var str = $"0{c}";
            var result = RadixConvert.ZZToInt(str, AppConstants.Definition.RadixBase62);
            Assert.Equal(36 + i, result);
        }
    }

    #endregion

    #region Performance Consideration - パフォーマンス考慮

    [Fact]
    public void IntToZZ_LargeNumberOfConversions_CompletesQuickly()
    {
        // 大量変換でもO(1)であることを確認
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < 10000; i++)
        {
            _ = RadixConvert.IntToZZ(i % 3844, AppConstants.Definition.RadixBase62);
        }

        sw.Stop();

        // 10000回の変換が100ms以内に完了すること
        Assert.True(sw.ElapsedMilliseconds < 100,
            $"Expected < 100ms, but took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void ZZToInt_LargeNumberOfConversions_CompletesQuickly()
    {
        // 大量変換でもO(1)であることを確認
        var testStrings = new[] { "00", "0Z", "1c", "ZZ", "zz" };
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < 10000; i++)
        {
            _ = RadixConvert.ZZToInt(testStrings[i % testStrings.Length], AppConstants.Definition.RadixBase62);
        }

        sw.Stop();

        // 10000回の変換が100ms以内に完了すること
        Assert.True(sw.ElapsedMilliseconds < 100,
            $"Expected < 100ms, but took {sw.ElapsedMilliseconds}ms");
    }

    #endregion
}

