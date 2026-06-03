using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson;
using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Infrastructure.Bms.Bmson;

/// <summary>
/// <see cref="BmsScoreGeneratorTests"/> の動作を検証するテストクラス。
/// </summary>
public class BmsScoreGeneratorTests
{
    private static BmsonFormat CreateBaseBmson()
    {
        return new BmsonFormat
        {
            Info = new BmsonInfo
            {
                Resolution = 240,
                InitBpm = 130,
                Title = "Test Title",
                Genre = "Test Genre",
                Artist = "Test Artist",
                Level = 5,
                JudgeRank = 50, // Normal rank
                Total = 200
            }
        };
    }

    private string GenerateBms(BmsonFormat bmson, string tempDir)
    {
        bmson = BmsonSanitizer.Sanitize(bmson);

        var timeCalc = new PulseToBmsTimeCalculator(bmson.Info.Resolution, bmson.Lines);
        var realTimeCalc = new PulseToRealTimeCalculator(bmson.Info.Resolution, bmson.Info.InitBpm, bmson.BpmEvents, bmson.StopEvents);
        var audioSlicer = new AudioSliceManager(tempDir, false);

        var generator = new BmsScoreGenerator(bmson, timeCalc, realTimeCalc, audioSlicer, false);
        return generator.GenerateBmsText();
    }

    /// <summary>
    /// GenerateBmsText において、条件 ChordNotes の場合に PushedToBgmChannel されることを検証します。
    /// </summary>
    [Fact]
    public void GenerateBmsText_ChordNotes_PushedToBgmChannel()
    {
        using var context = new BmsTestContext();
        var tempDir = context.TempDirectory;
        BmsTestWavHelper.CreateSilenceWavFile(Path.Combine(tempDir, "bgm.wav"), 0.1, 2);

        var bmson = CreateBaseBmson();
        bmson.SoundChannels.Add(new BmsonSoundChannel
        {
            Name = "bgm.wav",
            Notes =
            [
                new BmsonNote { X = 1, Y = 0, C = false }, // 1音目
                new BmsonNote { X = 1, Y = 0, C = true }   // 2音目 (和音 - C=trueで同一ブロック)
            ]
        });
        bmson.Lines.Add(new BmsonLineEvent { Y = 960 });

        string result = GenerateBms(bmson, tempDir);

        Assert.Contains("#00011:", result);
        Assert.Contains("#00001:", result);
    }

    /// <summary>
    /// GenerateBmsText において、条件 LongNote の場合に PlacedOnLnChannel されることを検証します。
    /// </summary>
    [Fact]
    public void GenerateBmsText_LongNote_PlacedOnLnChannel()
    {
        using var context = new BmsTestContext();
        var tempDir = context.TempDirectory;
        BmsTestWavHelper.CreateSilenceWavFile(Path.Combine(tempDir, "ln.wav"), 0.1, 2);

        var bmson = CreateBaseBmson();
        bmson.SoundChannels.Add(new BmsonSoundChannel
        {
            Name = "ln.wav",
            Notes =
            [
                new BmsonNote { X = 1, Y = 0, L = 240, C = false }
            ]
        });
        bmson.Lines.Add(new BmsonLineEvent { Y = 960 });

        string result = GenerateBms(bmson, tempDir);

        Assert.Contains("#00051:", result);
    }

    /// <summary>
    /// GenerateBmsText において、条件 DuplicateBpmEvents の場合に MergedAndRounded されることを検証します。
    /// </summary>
    [Fact]
    public void GenerateBmsText_DuplicateBpmEvents_MergedAndRounded()
    {
        using var context = new BmsTestContext();
        var tempDir = context.TempDirectory;
        BmsTestWavHelper.CreateSilenceWavFile(Path.Combine(tempDir, "bgm.wav"), 0.1, 2);

        var bmson = CreateBaseBmson();
        bmson.BpmEvents.Add(new BmsonBpmEvent { Y = 240, Bpm = 145.1234 });
        bmson.BpmEvents.Add(new BmsonBpmEvent { Y = 480, Bpm = 145.1226 });

        bmson.SoundChannels.Add(new BmsonSoundChannel
        {
            Name = "bgm.wav",
            Notes = [new BmsonNote { X = 1, Y = 0, C = false }]
        });
        bmson.Lines.Add(new BmsonLineEvent { Y = 960 });

        string result = GenerateBms(bmson, tempDir);

        Assert.Contains("#BPM01 145.123", result);
        Assert.DoesNotContain("#BPM02", result);
    }

    /// <summary>
    /// GenerateBmsText において、条件 TotalValue の場合に ApproachB されることを検証します。
    /// </summary>
    [Fact]
    public void GenerateBmsText_TotalValue_ApproachB()
    {
        using var context = new BmsTestContext();
        var tempDir = context.TempDirectory;
        BmsTestWavHelper.CreateSilenceWavFile(Path.Combine(tempDir, "bgm.wav"), 0.1, 2);

        // 1. total = 100 (デフォルト値)
        var bmsonDefault = CreateBaseBmson();
        bmsonDefault = bmsonDefault with { Info = bmsonDefault.Info with { Total = 100 } };
        bmsonDefault.SoundChannels.Add(new BmsonSoundChannel
        {
            Name = "bgm.wav",
            Notes = [new BmsonNote { X = 1, Y = 0, C = false }]
        });
        bmsonDefault.Lines.Add(new BmsonLineEvent { Y = 960 });

        string resultDefault = GenerateBms(bmsonDefault, tempDir);
        Assert.DoesNotContain("#TOTAL", resultDefault);

        // 2. total = 80 (カスタム値)
        var bmsonCustom = CreateBaseBmson();
        bmsonCustom = bmsonCustom with { Info = bmsonCustom.Info with { Total = 80 } };
        bmsonCustom.SoundChannels.Add(new BmsonSoundChannel
        {
            Name = "bgm.wav",
            Notes = [new BmsonNote { X = 1, Y = 0, C = false }]
        });
        bmsonCustom.Lines.Add(new BmsonLineEvent { Y = 960 });

        string resultCustom = GenerateBms(bmsonCustom, tempDir);
        Assert.Contains("#TOTAL 208", resultCustom);
    }
}
