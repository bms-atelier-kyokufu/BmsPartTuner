using BmsAtelierKyokufu.BmsPartTuner.Core;
using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Helpers
{
    /// <summary>
    /// <see cref="RadixConvertRoundtripTests"/> の動作を検証するテストクラス。
    /// </summary>
    public class RadixConvertRoundtripTests
    {
        #region Roundtrip Tests - 往復変換の一貫性

        /// <summary>
        /// テスト を検証します。
        /// </summary>
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

        /// <summary>
        /// テスト を検証します。
        /// </summary>
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

        /// <summary>
        /// テスト を検証します。
        /// </summary>
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

        /// <summary>
        /// テスト を検証します。
        /// </summary>
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

        /// <summary>
        /// IntToZZ において、条件 MaxDefinitionNumberBase36 の場合に ReturnsZZ されることを検証します。
        /// </summary>
        [Fact]
        public void IntToZZ_MaxDefinitionNumberBase36_ReturnsZZ()
        {
            // Arrange
            const int maxBase36 = AppConstants.Definition.MaxNumberBase36;  // 1295

            // Act
            var result = RadixConvert.IntToZZ(maxBase36, AppConstants.Definition.RadixBase36);

            // Assert
            Assert.Equal("ZZ", result);
        }

        /// <summary>
        /// IntToZZ において、条件 MaxDefinitionNumberBase62 の場合に Returnszz されることを検証します。
        /// </summary>
        [Fact]
        public void IntToZZ_MaxDefinitionNumberBase62_Returnszz()
        {
            // Arrange
            const int maxBase62 = AppConstants.Definition.MaxNumberBase62;  // 3843

            // Act
            var result = RadixConvert.IntToZZ(maxBase62, AppConstants.Definition.RadixBase62);

            // Assert
            Assert.Equal("zz", result);
        }

        /// <summary>
        /// ZZToInt において、条件 ZZ の場合に ReturnsMaxDefinitionNumberBase36 されることを検証します。
        /// </summary>
        [Fact]
        public void ZZToInt_ZZ_ReturnsMaxDefinitionNumberBase36()
        {
            // Act
            var result = RadixConvert.ZZToInt("ZZ", AppConstants.Definition.RadixBase36);

            // Assert
            Assert.Equal(AppConstants.Definition.MaxNumberBase36, result);
        }

        /// <summary>
        /// ZZToInt において、条件 zz の場合に ReturnsMaxDefinitionNumberBase62 されることを検証します。
        /// </summary>
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

        /// <summary>
        /// IntToZZ において、条件 Zero の場合に Returns00 されることを検証します。
        /// </summary>
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

        /// <summary>
        /// IntToZZ において、条件 MinDefinitionNumber の場合に Returns01 されることを検証します。
        /// </summary>
        [Theory]
        [InlineData(AppConstants.Definition.RadixBase36)]
        [InlineData(AppConstants.Definition.RadixBase62)]
        public void IntToZZ_MinDefinitionNumber_Returns01(int radix)
        {
            // Arrange
            const int minDef = AppConstants.Definition.MinNumber;  // 1

            // Act
            var result = RadixConvert.IntToZZ(minDef, radix);

            // Assert
            Assert.Equal("01", result);
        }

        #endregion

        #region Performance Consideration - パフォーマンス考慮

        /// <summary>
        /// IntToZZ において、条件 LargeNumberOfConversions の場合に CompletesQuickly されることを検証します。
        /// </summary>
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

        /// <summary>
        /// ZZToInt において、条件 LargeNumberOfConversions の場合に CompletesQuickly されることを検証します。
        /// </summary>
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
}
