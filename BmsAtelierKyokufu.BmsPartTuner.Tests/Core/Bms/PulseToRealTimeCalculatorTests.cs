using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Bms;

public class PulseToRealTimeCalculatorTests
{
    [Fact]
    public void GetTimeSec_ConstantBpm_ReturnsCorrectTime()
    {
        // 120 BPM = 2 beats per second. 
        // 1 beat = 240 pulses. 
        // 240 pulses = 0.5 seconds.
        var calc = new PulseToRealTimeCalculator(240, 120, null, null);

        Assert.Equal(0.0, calc.GetTimeSec(0));
        Assert.Equal(0.5, calc.GetTimeSec(240));
        Assert.Equal(1.0, calc.GetTimeSec(480));
    }

    [Fact]
    public void GetTimeSec_WithBpmChange_CalculatesPiecewise()
    {
        // Start with 120 BPM
        // At y=480 (1.0 sec), change to 60 BPM (1 beat per sec)
        List<BmsonBpmEvent> bpmEvents =
        [
            new() { Y = 480, Bpm = 60 }
        ];

        var calc = new PulseToRealTimeCalculator(240, 120, bpmEvents, null);

        Assert.Equal(0.0, calc.GetTimeSec(0));
        Assert.Equal(0.5, calc.GetTimeSec(240));
        Assert.Equal(1.0, calc.GetTimeSec(480)); // exactly at change

        // After change, 240 pulses take 1.0 sec
        Assert.Equal(2.0, calc.GetTimeSec(720)); // 480 + 240 -> 1.0 + 1.0
    }

    [Fact]
    public void GetTimeSec_WithStop_AppliesDelayAfterY()
    {
        // 120 BPM
        // Stop at y=240, duration = 480 pulses (2 beats = 1.0 sec)
        List<BmsonStopEvent> stopEvents =
        [
            new() { Y = 240, Duration = 480 }
        ];

        var calc = new PulseToRealTimeCalculator(240, 120, null, stopEvents);

        // Before stop
        Assert.Equal(0.0, calc.GetTimeSec(0));

        // Exactly at stop: notes here trigger BEFORE the pause
        Assert.Equal(0.5, calc.GetTimeSec(240));

        // After stop: time jumps by 1.0 sec
        // y=480 normally takes 1.0 sec. With 1.0 sec delay, it should be 2.0 sec.
        Assert.Equal(2.0, calc.GetTimeSec(480));
    }
}
