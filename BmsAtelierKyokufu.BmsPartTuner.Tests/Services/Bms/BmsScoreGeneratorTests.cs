using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson;
using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Services.Bms;

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

    [Fact]
    public void GenerateBmsText_ChordNotes_PushedToBgmChannel()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            BmsTestWavHelper.CreateSilenceWavFile(Path.Combine(tempDir, "bgm.wav"), 0.1, 2);

            // 準備: 鍵盤レーン 1 (X = 1) に同じタイミング (Y = 0) で2つの音符（和音）を配置
            var bmson = CreateBaseBmson();
            var channel = new BmsonSoundChannel
            {
                Name = "bgm.wav",
                Notes =
                [
                    new BmsonNote { X = 1, Y = 0, C = false }, // 1音目
                    new BmsonNote { X = 1, Y = 0, C = true }   // 2音目 (和音 - C=trueで同一ブロック)
                ]
            };
            bmson.SoundChannels.Add(channel);
            bmson.Lines.Add(new BmsonLineEvent { Y = 960 });

            bmson = BmsonSanitizer.Sanitize(bmson);

            var timeCalc = new PulseToBmsTimeCalculator(bmson.Info.Resolution, bmson.Lines);
            var realTimeCalc = new PulseToRealTimeCalculator(bmson.Info.Resolution, bmson.Info.InitBpm, bmson.BpmEvents, bmson.StopEvents);

            var audioSlicer = new AudioSliceManager(tempDir, false);

            var generator = new BmsScoreGenerator(bmson, timeCalc, realTimeCalc, audioSlicer, false);
            string result = generator.GenerateBmsText();

            // 検証
            // 和音の2音目がBGMレーン(#00001:)に逃がされていること
            Assert.Contains("#00011:", result);
            Assert.Contains("#00001:", result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void GenerateBmsText_LongNote_PlacedOnLnChannel()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            BmsTestWavHelper.CreateSilenceWavFile(Path.Combine(tempDir, "ln.wav"), 0.1, 2);

            // 準備: 鍵盤レーン 1 (X = 1) にロングノート (L > 0) を配置
            var bmson = CreateBaseBmson();
            var channel = new BmsonSoundChannel
            {
                Name = "ln.wav",
                Notes =
                [
                    new BmsonNote { X = 1, Y = 0, L = 240, C = false }
                ]
            };
            bmson.SoundChannels.Add(channel);
            bmson.Lines.Add(new BmsonLineEvent { Y = 960 });

            bmson = BmsonSanitizer.Sanitize(bmson);

            var timeCalc = new PulseToBmsTimeCalculator(bmson.Info.Resolution, bmson.Lines);
            var realTimeCalc = new PulseToRealTimeCalculator(bmson.Info.Resolution, bmson.Info.InitBpm, bmson.BpmEvents, bmson.StopEvents);

            var audioSlicer = new AudioSliceManager(tempDir, false);

            var generator = new BmsScoreGenerator(bmson, timeCalc, realTimeCalc, audioSlicer, false);
            string result = generator.GenerateBmsText();

            // 検証
            // LN開始・終了がLN用チャンネル（MapLaneToChannel(1, true) => "51"）に配置されていること
            Assert.Contains("#00051:", result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void GenerateBmsText_DuplicateBpmEvents_MergedAndRounded()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            BmsTestWavHelper.CreateSilenceWavFile(Path.Combine(tempDir, "bgm.wav"), 0.1, 2);

            // 準備: 微小な差を持つBPMイベントが、丸めによって同一になる場合の検証
            var bmson = CreateBaseBmson();

            // 丸めると両方 145.123 になる
            bmson.BpmEvents.Add(new BmsonBpmEvent { Y = 240, Bpm = 145.1234 });
            bmson.BpmEvents.Add(new BmsonBpmEvent { Y = 480, Bpm = 145.1226 });

            var channel = new BmsonSoundChannel
            {
                Name = "bgm.wav",
                Notes = [new BmsonNote { X = 1, Y = 0, C = false }]
            };
            bmson.SoundChannels.Add(channel);
            bmson.Lines.Add(new BmsonLineEvent { Y = 960 });

            bmson = BmsonSanitizer.Sanitize(bmson);

            var timeCalc = new PulseToBmsTimeCalculator(bmson.Info.Resolution, bmson.Lines);
            var realTimeCalc = new PulseToRealTimeCalculator(bmson.Info.Resolution, bmson.Info.InitBpm, bmson.BpmEvents, bmson.StopEvents);

            var audioSlicer = new AudioSliceManager(tempDir, false);

            var generator = new BmsScoreGenerator(bmson, timeCalc, realTimeCalc, audioSlicer, false);
            string result = generator.GenerateBmsText();

            // 検証: #BPM01 145.123 が定義され、#BPM02 などの定義は存在しない
            Assert.Contains("#BPM01 145.123", result);
            Assert.DoesNotContain("#BPM02", result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void GenerateBmsText_TotalValue_ApproachB()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            BmsTestWavHelper.CreateSilenceWavFile(Path.Combine(tempDir, "bgm.wav"), 0.1, 2);

            // 1. total = 100 (デフォルト値) の場合は #TOTAL が出力されないことの検証
            var bmsonDefault = CreateBaseBmson();
            bmsonDefault = bmsonDefault with { Info = bmsonDefault.Info with { Total = 100 } }; // デフォルト割合
            var chDefault = new BmsonSoundChannel
            {
                Name = "bgm.wav",
                Notes = [new BmsonNote { X = 1, Y = 0, C = false }] // playable note (X=1)
            };
            bmsonDefault.SoundChannels.Add(chDefault);
            bmsonDefault.Lines.Add(new BmsonLineEvent { Y = 960 });

            bmsonDefault = BmsonSanitizer.Sanitize(bmsonDefault);
            var timeCalcDef = new PulseToBmsTimeCalculator(bmsonDefault.Info.Resolution, bmsonDefault.Lines);
            var realTimeCalcDef = new PulseToRealTimeCalculator(bmsonDefault.Info.Resolution, bmsonDefault.Info.InitBpm, bmsonDefault.BpmEvents, bmsonDefault.StopEvents);
            var audioSlicerDef = new AudioSliceManager(tempDir, false);

            var genDefault = new BmsScoreGenerator(bmsonDefault, timeCalcDef, realTimeCalcDef, audioSlicerDef, false);
            string resultDefault = genDefault.GenerateBmsText();

            Assert.DoesNotContain("#TOTAL", resultDefault);

            // 2. total = 80 (カスタム値) の場合は #TOTAL が計算値 (基準デフォルト * 0.8) で出力されることの検証
            var bmsonCustom = CreateBaseBmson();
            bmsonCustom = bmsonCustom with { Info = bmsonCustom.Info with { Total = 80 } }; // カスタム割合
            var chCustom = new BmsonSoundChannel
            {
                Name = "bgm.wav",
                Notes = [new BmsonNote { X = 1, Y = 0, C = false }] // playable note (N=1)
            };
            bmsonCustom.SoundChannels.Add(chCustom);
            bmsonCustom.Lines.Add(new BmsonLineEvent { Y = 960 });

            bmsonCustom = BmsonSanitizer.Sanitize(bmsonCustom);
            var timeCalcCust = new PulseToBmsTimeCalculator(bmsonCustom.Info.Resolution, bmsonCustom.Lines);
            var realTimeCalcCust = new PulseToRealTimeCalculator(bmsonCustom.Info.Resolution, bmsonCustom.Info.InitBpm, bmsonCustom.BpmEvents, bmsonCustom.StopEvents);
            var audioSlicerCust = new AudioSliceManager(tempDir, false);

            var genCustom = new BmsScoreGenerator(bmsonCustom, timeCalcCust, realTimeCalcCust, audioSlicerCust, false);
            string resultCustom = genCustom.GenerateBmsText();

            // N = 1 のとき:
            // 7.605 * 1 / (0.01 * 1 + 6.5) = 7.605 / 6.51 = 1.168
            // 最低値 260.0 が適用され、defaultTotal = 260.0
            // 260.0 * 80 / 100 = 208.0
            Assert.Contains("#TOTAL 208", resultCustom);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
