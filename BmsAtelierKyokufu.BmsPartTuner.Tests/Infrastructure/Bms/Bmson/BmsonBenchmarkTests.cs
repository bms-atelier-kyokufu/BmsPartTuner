using System.Diagnostics;
using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson;
using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;
using Xunit.Abstractions;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Infrastructure.Bms.Bmson;

/// <summary>
/// <see cref="BmsonBenchmarkTests"/> の動作を検証するテストクラス。
/// </summary>
public class BmsonBenchmarkTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Benchmark において、条件 Downconvert の場合に Performance されることを検証します。
    /// </summary>
    [Fact]
    [Trait("Category", "Benchmark")]
    public void Benchmark_Downconvert_Performance()
    {
        using var context = new BmsFamilyTestContext();

        // 1. ダミーの BmsonFormat を作成
        var bmson = context.CreateBuilder<BmsonBuilder>()
            .WithInfo(resolution: 240, initBpm: 130)
            .AddBpmEvents(Enumerable.Range(1, 100).Select(i => new BmsonBpmEvent { Y = i * 1000, Bpm = 130 + (i % 10) }))
            .AddStopEvents(Enumerable.Range(1, 50).Select(i => new BmsonStopEvent { Y = i * 2000, Duration = 240 }))
            .AddLines(Enumerable.Range(1, 200).Select(i => new BmsonLineEvent { Y = i * 960 }))
            .AddSoundChannels(Enumerable.Range(0, 10).Select(chIdx => new BmsonSoundChannel
            {
                Name = $"bgm_{chIdx}.wav",
                Notes = [.. Enumerable.Range(0, 10000).Select(noteIdx => new BmsonNote
                {
                    X = 0, // BGM
                    Y = noteIdx * 10,
                    C = true
                })]
            }))
            .Build();

        // 2. 依存コンポーネントの作成
        var timeCalc = new PulseToBmsTimeCalculator(bmson.Info.Resolution, bmson.Lines);
        var realTimeCalc = new PulseToRealTimeCalculator(bmson.Info.Resolution, bmson.Info.InitBpm, bmson.BpmEvents, bmson.StopEvents);
        var audioSlicer = new AudioSliceManager(context.TempDirectory, false);

        // 3. ベンチマーク実行
        var sw = Stopwatch.StartNew();

        var generator = new BmsScoreGenerator(bmson, timeCalc, realTimeCalc, audioSlicer, false);
        string result = generator.GenerateBmsText();

        sw.Stop();

        _output.WriteLine($"[Benchmark] Execution time: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"[Benchmark] Result size: {result.Length} chars");

        // 出力されたことをアサート（適当なチェック）
        Assert.NotEmpty(result);
    }

    /// <summary>
    /// Test において、条件 DoublePlay の場合に And されることを検証します。
    /// </summary>
    [Fact]
    [Trait("Category", "Benchmark")]
    public void Benchmark_Downconvert_OverlappingNotes()
    {
        using var context = new BmsFamilyTestContext();

        // 1. ダミーの BmsonFormat を作成
        var bmson = context.CreateBuilder<BmsonBuilder>()
            .WithInfo(resolution: 240, initBpm: 130)
            .AddBpmEvents(Enumerable.Range(1, 100)
                .Select(i => new BmsonBpmEvent { Y = i * 1000, Bpm = 130 + (i % 10) }))
            .AddStopEvents(Enumerable.Range(1, 50)
                .Select(i => new BmsonStopEvent { Y = i * 2000, Duration = 240 }))
            .AddLines(Enumerable.Range(1, 200)
                .Select(i => new BmsonLineEvent { Y = i * 960 }))
            .AddSoundChannels(Enumerable.Range(0, 10)
                .Select(chIdx => new BmsonSoundChannel
                {
                    Name = $"bgm_{chIdx}.wav",
                    Notes = [.. Enumerable.Range(0, 10000).Select(noteIdx => new BmsonNote
                    {
                        X = 0, // BGM
                        Y = 0, // すべて同じタイミング！
                        C = noteIdx > 0
                    })]
                }))
            .Build();

        // 2. 依存コンポーネントの作成
        var timeCalc = new PulseToBmsTimeCalculator(bmson.Info.Resolution, bmson.Lines);
        var realTimeCalc = new PulseToRealTimeCalculator(bmson.Info.Resolution, bmson.Info.InitBpm, bmson.BpmEvents, bmson.StopEvents);

        // 実在しないパスを指定してファイルI/Oをスキップさせる
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var audioSlicer = new AudioSliceManager(tempDir, false);

        // 3. ベンチマーク実行
        var sw = Stopwatch.StartNew();

        var generator = new BmsScoreGenerator(bmson, timeCalc, realTimeCalc, audioSlicer, false);
        string result = generator.GenerateBmsText();

        sw.Stop();

        _output.WriteLine($"[Benchmark Overlapping] Execution time: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"[Benchmark Overlapping] Result size: {result.Length} chars");

        // 出力されたことをアサート（適当なチェック）
        Assert.NotEmpty(result);
    }


    [Fact]
    public void Test_DoublePlay_And_BpmRounding_Guardrails()
    {
        using var context = new BmsFamilyTestContext();

        // 1. ダミーの BmsonFormat を作成
        var bmson = context.CreateBuilder<BmsonBuilder>()
            .WithInfo(resolution: 240, initBpm: 130.0004) // 130 に丸められるはず
            .AddBpmEvent(240, 145.1234)
            .AddBpmEvent(480, 145.1226)
            .AddSoundChannel("test.wav", new BmsonNote { X = 9, Y = 0, C = false })
            .AddLine(960)
            .Build();

        // サニタイズ（Y=0 の小節線挿入やソート）
        bmson = BmsonSanitizer.Sanitize(bmson);

        var timeCalc = new PulseToBmsTimeCalculator(bmson.Info.Resolution, bmson.Lines);
        var realTimeCalc = new PulseToRealTimeCalculator(bmson.Info.Resolution, bmson.Info.InitBpm, bmson.BpmEvents, bmson.StopEvents);

        var tempDir = context.TempDirectory;
        var audioSlicer = new AudioSliceManager(tempDir, false);

        var generator = new BmsScoreGenerator(bmson, timeCalc, realTimeCalc, audioSlicer, false);
        string result = generator.GenerateBmsText();

        // アサーション
        // 1. ダブルプレイ判定により #PLAYER 3 が出力されていること
        Assert.Contains("#PLAYER 3", result);

        // 2. 初期BPMが丸められて #BPM 130 になっていること
        Assert.Contains("#BPM 130", result);

        // 3. 重複するBPMイベントが 145.123 に丸められ、マージされて #BPM01 145.123 のみが定義されていること
        Assert.Contains("#BPM01 145.123", result);
        Assert.DoesNotContain("#BPM02", result); // 2個目のBPMスロットは生成されないはず
    }
}
