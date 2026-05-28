using BmsAtelierKyokufu.BmsPartTuner.Core;
using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Helpers
{
    public class RadixConvertDecodeTests
    {
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
        [InlineData("aA", (36 * 62) + 10)]  // 小文字大文字混在
        [InlineData("Aa", (10 * 62) + 36)]  // 大文字小文字混在
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
    }
}
