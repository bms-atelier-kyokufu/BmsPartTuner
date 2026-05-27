using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Optimization
{
    /// <summary>
    /// SimulationEngine 内のユーティリティ静的メソッド（RMS範囲計算、しきい値生成）の動作検証テスト。
    /// </summary>
    public class SimulationEngineUtilityTests
    {
        #region RMS範囲計算テスト

        [Fact]
        public void CalculateRmsRange_SilentAudio_ReturnsZeroToThreshold()
        {
            var (min, max) = SimulationEngine.CalculateRmsRange(0.0000005f);

            Assert.Equal(0.0f, min);
            Assert.True(max > 0.0f);
        }

        [Fact]
        public void CalculateRmsRange_NormalAudio_ReturnsProportionalRange()
        {
            var (min, max) = SimulationEngine.CalculateRmsRange(0.5f);

            Assert.True(min < 0.5f);
            Assert.True(max > 0.5f);
            Assert.True(min > 0.0f);
        }

        [Fact]
        public void CalculateRmsRange_HighRmsAudio_ReturnsWiderRange()
        {
            var (min1, max1) = SimulationEngine.CalculateRmsRange(0.1f);
            var (min2, max2) = SimulationEngine.CalculateRmsRange(1.0f);

            float range1 = max1 - min1;
            float range2 = max2 - min2;
            Assert.True(range2 > range1);
        }

        [Fact]
        public void CalculateRmsRange_VeryLowRms_ReturnsNonNegativeRange()
        {
            var (min, max) = SimulationEngine.CalculateRmsRange(0.00001f);

            Assert.True(min >= 0.0f, "RMS範囲の最小値は非負であるべき");
            Assert.True(max > min, "RMS範囲の最大値は最小値より大きいべき");
        }

        [Fact]
        public void CalculateRmsRange_VeryHighRms_ReturnsValidRange()
        {
            var (min, max) = SimulationEngine.CalculateRmsRange(0.99f);

            Assert.True(min >= 0.0f);
            Assert.True(max <= 2.0f); // 現実的な上限チェック
            Assert.True(max > min);
        }

        #endregion

        #region 閾値生成テスト

        [Fact]
        public void GenerateThresholds_ValidRange_ReturnsDescendingList()
        {
            var thresholds = SimulationEngine.GenerateThresholds(0.5f, 0.9f, 0.1f);

            Assert.NotEmpty(thresholds);
            Assert.Equal(0.9f, thresholds[0], 2);
            Assert.InRange(thresholds.Count, 4, 5);

            for (int i = 0; i < thresholds.Count - 1; i++)
            {
                Assert.True(thresholds[i] > thresholds[i + 1],
                    $"Expected descending order but thresholds[{i}]={thresholds[i]} <= thresholds[{i + 1}]={thresholds[i + 1]}");
            }

            Assert.InRange(thresholds[^1], 0.5f, 0.7f);
        }

        [Fact]
        public void GenerateThresholds_SingleValue_ReturnsSingleElement()
        {
            var thresholds = SimulationEngine.GenerateThresholds(0.8f, 0.8f, 0.1f);

            Assert.Single(thresholds);
            Assert.Equal(0.8f, thresholds[0]);
        }

        [Fact]
        public void GenerateThresholds_SmallStep_ReturnsMoreElements()
        {
            var thresholds1 = SimulationEngine.GenerateThresholds(0.5f, 0.9f, 0.1f);
            var thresholds2 = SimulationEngine.GenerateThresholds(0.5f, 0.9f, 0.05f);

            Assert.True(thresholds2.Count > thresholds1.Count);
        }

        [Fact]
        public void GenerateThresholds_VerySmallRange_ReturnsAtLeastOne()
        {
            var thresholds = SimulationEngine.GenerateThresholds(0.95f, 0.96f, 0.01f);

            Assert.NotEmpty(thresholds);
            Assert.True(thresholds.Count >= 1);
            Assert.True(thresholds.All(t => t >= 0.95f && t <= 0.96f));
        }

        [Fact]
        public void GenerateThresholds_VerySmallStep_GeneratesManyThresholds()
        {
            var thresholds = SimulationEngine.GenerateThresholds(0.90f, 0.91f, 0.001f);

            Assert.True(thresholds.Count >= 10);
        }

        [Fact]
        public void GenerateThresholds_StepLargerThanRange_ReturnsSingleMaxValue()
        {
            var thresholds = SimulationEngine.GenerateThresholds(0.5f, 0.6f, 1.0f);

            Assert.Single(thresholds);
            Assert.Equal(0.6f, thresholds[0]);
        }

        [Fact]
        public void GenerateThresholds_LargeNumberOfThresholds_CompletsesQuickly()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var thresholds = SimulationEngine.GenerateThresholds(0.0f, 1.0f, 0.001f);

            sw.Stop();

            Assert.NotEmpty(thresholds);
            Assert.True(thresholds.Count >= 1000);
            Assert.True(sw.ElapsedMilliseconds < 1000, "しきい値生成は1秒以内に完了すべき");
        }

        #endregion
    }
}
