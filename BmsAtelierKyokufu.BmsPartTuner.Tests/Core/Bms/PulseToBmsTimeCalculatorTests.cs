using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Bms;

public class PulseToBmsTimeCalculatorTests
{
    [Fact]
    public void GetMeasureNumber_WithStandardLines_ReturnsCorrectMeasure()
    {
        List<BmsonLineEvent> lines =
        [
            new() { Y = 0 },     // m=0
            new() { Y = 960 },   // m=1
            new() { Y = 1920 }   // m=2
        ];

        var calc = new PulseToBmsTimeCalculator(240, lines);

        Assert.Equal(0, calc.GetMeasureNumber(0));
        Assert.Equal(0, calc.GetMeasureNumber(959));
        Assert.Equal(1, calc.GetMeasureNumber(960));
        Assert.Equal(1, calc.GetMeasureNumber(1919));
        Assert.Equal(2, calc.GetMeasureNumber(1920));
        Assert.Equal(2, calc.GetMeasureNumber(5000));
    }

    [Fact]
    public void GetStepIndex_StandardMeasure_CalculatesCorrectStep()
    {
        List<BmsonLineEvent> lines =
        [
            new() { Y = 0 },
            new() { Y = 960 }
        ];

        // Resolution = 240. Measure = 960 pulses.
        // We output 240 steps. step = y * 240 / 960 = y / 4.
        var calc = new PulseToBmsTimeCalculator(240, lines);

        Assert.Equal(0, calc.GetStepIndex(0, 240));
        Assert.Equal(60, calc.GetStepIndex(240, 240)); // 240/4 = 60
        Assert.Equal(120, calc.GetStepIndex(480, 240)); // 480/4 = 120
        Assert.Equal(239, calc.GetStepIndex(959, 240)); // 959/4 = 239.75 -> 239

        // Safety bounds: y=960 is the start of the NEXT measure (m=1), so its local step is 0!
        Assert.Equal(0, calc.GetStepIndex(960, 240));
    }

    [Fact]
    public void GetMeterMultiplier_NonStandardMeasure_ReturnsCorrectRatio()
    {
        List<BmsonLineEvent> lines =
        [
            new() { Y = 0 },
            new() { Y = 480 },  // m=0 is 2/4 (length 480)
            new() { Y = 1440 }  // m=1 is 4/4 (length 960)
        ];

        var calc = new PulseToBmsTimeCalculator(240, lines);

        Assert.Equal(0.5, calc.GetMeterMultiplier(0)); // 480 / 960
        Assert.Equal(1.0, calc.GetMeterMultiplier(1)); // 960 / 960
    }
}
