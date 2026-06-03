using BmsAtelierKyokufu.BmsPartTuner.Core;


namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core;

/// <summary>
/// <see cref="AppConstants"/> のテストクラス。
/// <para>
/// 【テスト対象】
/// - 定数値の妥当性検証
/// - ヘルパーメソッドの動作確認
/// - 定数間の整合性チェック
/// </para>
/// <para>
/// 【テスト設計方針】
/// - 定数が期待される範囲内にあることを確認
/// - ヘルパーメソッドのエッジケース対応
/// </para>
/// </summary>

/// <summary>
/// <see cref="AppConstantsTests"/> の動作を検証するテストクラス。
/// </summary>
public class AppConstantsTests
{
    #region Constant Value Tests


    /// <summary>
    /// Base36Limit において IsCorrectValue の場合の挙動を検証します。
    /// </summary>
    [Fact]

    public void Base36Limit_IsCorrectValue()
    {

        Assert.Equal(1295, AppConstants.Definition.MaxNumberBase36);
    }


    /// <summary>
    /// Base62Limit において IsCorrectValue の場合の挙動を検証します。
    /// </summary>
    [Fact]

    public void Base62Limit_IsCorrectValue()
    {

        Assert.Equal(3843, AppConstants.Definition.MaxNumberBase62);
    }


    /// <summary>
    /// MaxGroupSize において IsPositive の場合の挙動を検証します。
    /// </summary>
    [Fact]

    public void MaxGroupSize_IsPositive()
    {

        Assert.True(AppConstants.Grouping.MaxGroupSize > 0);
        Assert.True(AppConstants.Grouping.MaxGroupSize <= 200); // 合理的な上限
    }


    /// <summary>
    /// RmsQuantizationFactor において IsPositive の場合の挙動を検証します。
    /// </summary>
    [Fact]

    public void RmsQuantizationFactor_IsPositive()
    {

        Assert.True(AppConstants.Grouping.RmsQuantizationFactor > 0);
    }


    /// <summary>
    /// MinBatchSize において IsPositive の場合の挙動を検証します。
    /// </summary>
    [Fact]

    public void MinBatchSize_IsPositive()
    {

        Assert.True(AppConstants.Cache.MinBatchSize > 0);
    }


    /// <summary>
    /// BatchSizeDivisor において IsPositive の場合の挙動を検証します。
    /// </summary>
    [Fact]

    public void BatchSizeDivisor_IsPositive()
    {

        Assert.True(AppConstants.Cache.BatchSizeDivisor > 0);
    }

    #endregion

    #region Progress Constants Tests


    /// <summary>
    /// ProgressValues において AreInAscendingOrder の場合の挙動を検証します。
    /// </summary>
    [Fact]

    public void ProgressValues_AreInAscendingOrder()
    {
        // 進捗値は昇順であるべき

        Assert.True(AppConstants.Progress.PreloadComplete < AppConstants.Progress.ComparisonComplete);
        Assert.True(AppConstants.Progress.ComparisonComplete < AppConstants.Progress.RewriteComplete);
        Assert.True(AppConstants.Progress.RewriteComplete < AppConstants.Progress.Complete);
    }


    /// <summary>
    /// ProgressValues において AreInValidRange の場合の挙動を検証します。
    /// </summary>
    [Fact]

    public void ProgressValues_AreInValidRange()
    {
        // 進捗値は0-100の範囲内
        Assert.InRange(AppConstants.Progress.PreloadComplete, 0, 100);
        Assert.InRange(AppConstants.Progress.ComparisonComplete, 0, 100);
        Assert.InRange(AppConstants.Progress.RewriteComplete, 0, 100);
        Assert.Equal(100, AppConstants.Progress.Complete);
    }

    #endregion

    #region R2 Threshold Constants Tests


    /// <summary>
    /// R2ThresholdRange において IsValid の場合の挙動を検証します。
    /// </summary>
    [Fact]

    public void R2ThresholdRange_IsValid()
    {

        Assert.True(AppConstants.Threshold.Min >= 0.0f);
        Assert.True(AppConstants.Threshold.Min <= AppConstants.Threshold.Max);
        Assert.Equal(1.0f, AppConstants.Threshold.Max);
    }

    /// <summary>
    /// R2ThresholdDisplay において IsValid の場合の挙動を検証します。
    /// </summary>
    [Fact]
    public void R2ThresholdDisplay_IsValid()
    {
        Assert.True(AppConstants.Threshold.MinDisplay >= 0);
        Assert.True(AppConstants.Threshold.MinDisplay < AppConstants.Threshold.MaxDisplay);
        Assert.InRange(AppConstants.Threshold.DefaultDisplay,
            AppConstants.Threshold.MinDisplay,
            AppConstants.Threshold.MaxDisplay);
    }

    #endregion

    #region Supported Extensions Tests

    /// <summary>
    /// SupportedBmsExtensions において IsNotEmpty の場合の挙動を検証します。
    /// </summary>
    [Fact]
    public void SupportedBmsExtensions_IsNotEmpty()
    {
        Assert.NotEmpty(AppConstants.Files.SupportedBmsExtensions);
    }

    /// <summary>
    /// SupportedBmsExtensions において ContainsDotPrefix の場合の挙動を検証します。
    /// </summary>
    [Fact]
    public void SupportedBmsExtensions_ContainsDotPrefix()
    {
        var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
        Assert.All(AppConstants.Files.SupportedBmsExtensions, ext => Assert.StartsWith(".", ext));
    }

    /// <summary>
    /// SupportedBmsExtensions において ContainsBms の場合の挙動を検証します。
    /// </summary>
    [Fact]
    public void SupportedBmsExtensions_ContainsBms()
    {
        Assert.Contains(".bms", AppConstants.Files.SupportedBmsExtensions);
    }

    /// <summary>
    /// SupportedOutputBmsExtensions において IsNotEmpty の場合の挙動を検証します。
    /// </summary>
    [Fact]
    public void SupportedOutputBmsExtensions_IsNotEmpty()
    {
        Assert.NotEmpty(AppConstants.Files.SupportedOutputBmsExtensions);
    }

    /// <summary>
    /// SupportedOutputBmsExtensions において ContainsDotPrefix の場合の挙動を検証します。
    /// </summary>
    [Fact]
    public void SupportedOutputBmsExtensions_ContainsDotPrefix()
    {
        var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
        Assert.All(AppConstants.Files.SupportedOutputBmsExtensions, ext => Assert.StartsWith(".", ext));
    }

    /// <summary>
    /// SupportedOutputBmsExtensions において ContainsBms の場合の挙動を検証します。
    /// </summary>
    [Fact]
    public void SupportedOutputBmsExtensions_ContainsBms()
    {
        Assert.Contains(".bms", AppConstants.Files.SupportedOutputBmsExtensions);
    }

    /// <summary>
    /// SupportedOutputBmsExtensions において DoesNotContainBmson の場合の挙動を検証します。
    /// </summary>
    [Fact]
    public void SupportedOutputBmsExtensions_DoesNotContainBmson()
    {
        Assert.DoesNotContain(".bmson", AppConstants.Files.SupportedOutputBmsExtensions);
    }

    #endregion

    #region Default Value Tests

    /// <summary>
    /// DefaultDefinitionStart において IsValidFormat の場合の挙動を検証します。
    /// </summary>
    [Fact]
    public void DefaultDefinitionStart_IsValidFormat()
    {
        Assert.Equal("01", AppConstants.Definition.Start);
        Assert.Equal(2, AppConstants.Definition.Start.Length);
    }

    /// <summary>
    /// DefaultDefinitionEnd において IsValidFormat の場合の挙動を検証します。
    /// </summary>
    [Fact]
    public void DefaultDefinitionEnd_IsValidFormat()
    {
        Assert.Equal("00", AppConstants.Definition.End);
        Assert.Equal(2, AppConstants.Definition.End.Length);
    }

    /// <summary>
    /// DefaultOutputFileName において HasValidExtension の場合の挙動を検証します。
    /// </summary>
    [Fact]
    public void DefaultOutputFileName_HasValidExtension()
    {
        Assert.EndsWith(".bms", AppConstants.Files.DefaultOutputFileName);
    }

    /// <summary>
    /// OptimizedFileSuffix において IsNotEmpty の場合の挙動を検証します。
    /// </summary>
    [Fact]
    public void OptimizedFileSuffix_IsNotEmpty()
    {
        Assert.NotEmpty(AppConstants.Files.OptimizedFileSuffix);
        Assert.StartsWith("_", AppConstants.Files.OptimizedFileSuffix);
    }

    #endregion

    #region GetFileTypeName Tests

    /// <summary>
    /// テスト を検証します。
    /// </summary>
    [Theory]
    [InlineData(".bms", "BMSファイル")]
    [InlineData(".bme", "BMEファイル")]
    [InlineData(".bml", "BMLファイル")]
    [InlineData(".pms", "PMSファイル")]
    [InlineData(".bmson", "BMSONファイル")]
    public void GetFileTypeName_KnownExtensions_ReturnsCorrectName(
        string extension, string expectedName)
    {
        var result = AppConstants.Files.GetFileTypeName(extension);

        Assert.Equal(expectedName, result);
    }

    /// <summary>
    /// GetFileTypeName において、条件 CaseInsensitive の場合に ReturnsCorrectName されることを検証します。
    /// </summary>
    [Theory]
    [InlineData(".BMS", "BMSファイル")]  // 大文字
    [InlineData(".Bms", "BMSファイル")]  // 混在
    [InlineData(".BmE", "BMEファイル")]  // 混在
    public void GetFileTypeName_CaseInsensitive_ReturnsCorrectName(
        string extension, string expectedName)
    {
        var result = AppConstants.Files.GetFileTypeName(extension);

        Assert.Equal(expectedName, result);
    }

    /// <summary>
    /// テスト を検証します。
    /// </summary>
    [Theory]
    [InlineData(".txt")]
    [InlineData(".wav")]
    [InlineData(".unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void GetFileTypeName_UnknownExtension_ReturnsGenericName(string? extension)
    {
        var result = AppConstants.Files.GetFileTypeName(extension!);

        Assert.Equal("ファイル", result);
    }

    #endregion

    #region Consistency Tests

    /// <summary>
    /// Base62Limit において IsGreaterThanBase36Limit の場合の挙動を検証します。
    /// </summary>
    [Fact]
    public void Base62Limit_IsGreaterThanBase36Limit()
    {
        // Base62はBase36より多くの定義をサポートすべき
        Assert.True(AppConstants.Definition.MaxNumberBase62 > AppConstants.Definition.MaxNumberBase36);
    }

    /// <summary>
    /// MinBatchSize において IsSmallerThanMaxGroupSize の場合の挙動を検証します。
    /// </summary>
    [Fact]
    public void MinBatchSize_IsSmallerThanMaxGroupSize()
    {
        // バッチサイズはグループサイズより小さいべき
        Assert.True(AppConstants.Cache.MinBatchSize <= AppConstants.Grouping.MaxGroupSize);
    }

    #endregion
}

