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
    private static BmsonBuilder CreateBaseBuilder(BmsFamilyTestContext context)
    {
        return new BmsonBuilder(context)
            .WithInfo(
                resolution: 240,
                initBpm: 130,
                title: "Test Title",
                genre: "Test Genre",
                artist: "Test Artist",
                level: 5,
                judgeRank: 50,
                total: 200);
    }

    private static string GenerateBms(BmsonFormat bmson, string tempDir)
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
        using var context = new BmsFamilyTestContext();
        var tempDir = context.TempDirectory;
        BmsTestWavHelper.CreateSilenceWavFile(Path.Combine(tempDir, "bgm.wav"), 0.1, 2);

        var bmson = CreateBaseBuilder(context)
            .AddSoundChannel("bgm.wav",
                new BmsonNote { X = 1, Y = 0, C = false }, // 1音目
                new BmsonNote { X = 1, Y = 0, C = true }   // 2音目 (和音 - C=trueで同一ブロック)
            )
            .AddLine(960)
            .Build();

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
        using var context = new BmsFamilyTestContext();
        var tempDir = context.TempDirectory;
        BmsTestWavHelper.CreateSilenceWavFile(Path.Combine(tempDir, "ln.wav"), 0.1, 2);

        var bmson = CreateBaseBuilder(context)
            .AddSoundChannel("ln.wav",
                new BmsonNote { X = 1, Y = 0, L = 240, C = false }
            )
            .AddLine(960)
            .Build();

        string result = GenerateBms(bmson, tempDir);

        Assert.Contains("#00051:", result);
    }

    /// <summary>
    /// GenerateBmsText において、条件 DuplicateBpmEvents の場合に MergedAndRounded されることを検証します。
    /// </summary>
    [Fact]
    public void GenerateBmsText_DuplicateBpmEvents_MergedAndRounded()
    {
        using var context = new BmsFamilyTestContext();
        var tempDir = context.TempDirectory;
        BmsTestWavHelper.CreateSilenceWavFile(Path.Combine(tempDir, "bgm.wav"), 0.1, 2);

        var bmson = CreateBaseBuilder(context)
            .AddBpmEvent(240, 145.1234)
            .AddBpmEvent(480, 145.1226)
            .AddSoundChannel("bgm.wav", new BmsonNote { X = 1, Y = 0, C = false })
            .AddLine(960)
            .Build();

        string result = GenerateBms(bmson, tempDir);

        Assert.Contains("#BPM01 145.123", result);
        Assert.DoesNotContain("#BPM02", result);
    }

    /// <summary>
    /// GenerateBmsText において、条件 TotalValue の場合に ApproachB されることを検証します。
    /// </summary>
    [Theory]
    [InlineData(100, false, null)]
    [InlineData(80, true, "#TOTAL 208")]
    public void GenerateBmsText_TotalValue_ApproachB(double total, bool shouldContain, string? expectedSubstring)
    {
        using var context = new BmsFamilyTestContext();
        var tempDir = context.TempDirectory;
        BmsTestWavHelper.CreateSilenceWavFile(Path.Combine(tempDir, "bgm.wav"), 0.1, 2);

        var bmson = CreateBaseBuilder(context)
            .WithInfo(total: total)
            .AddSoundChannel("bgm.wav", new BmsonNote { X = 1, Y = 0, C = false })
            .AddLine(960)
            .Build();

        string result = GenerateBms(bmson, tempDir);

        if (shouldContain)
        {
            Assert.NotNull(expectedSubstring);
            Assert.Contains(expectedSubstring, result);
        }
        else
        {
            Assert.DoesNotContain("#TOTAL", result);
        }
    }
}
