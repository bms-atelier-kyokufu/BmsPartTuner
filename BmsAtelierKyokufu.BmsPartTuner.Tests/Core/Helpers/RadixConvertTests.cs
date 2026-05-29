using BmsAtelierKyokufu.BmsPartTuner.Core;
using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Helpers
{
    /// <summary>
    /// <see cref="RadixConvert"/> のテストクラス。
    /// <para>
    /// BMSの定義番号（#WAV01 ～ #WAVzz）は、基本的に36進数（0-9, A-Z）ですが、
    /// 拡張仕様として62進数（0-9, A-Z, a-z）をサポートする場合があります。
    /// ここではそれぞれの基数変換が正しく行われるか検証します。
    /// </para>
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
        [InlineData(1296, AppConstants.Definition.RadixBase36)]  // ZZ의 次 (36^2)
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

        #endregion
    }
}
