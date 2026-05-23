using System.Diagnostics;
using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;
using BmsAtelierKyokufu.BmsPartTuner.Services.Bms.Bmson;
using Xunit.Abstractions;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Services.Bms;

public class BmsonBenchmarkTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void Benchmark_Downconvert_Performance()
    {
        // 1. ダミーの BmsonFormat を作成
        var bmson = new BmsonFormat
        {
            Info = new BmsonInfo
            {
                Resolution = 240,
                InitBpm = 130
            }
        };

        // BPMイベントを100個ほど追加
        for (int i = 1; i <= 100; i++)
        {
            bmson.BpmEvents.Add(new BmsonBpmEvent { Y = i * 1000, Bpm = 130 + (i % 10) });
        }

        // STOPイベントを50個ほど追加
        for (int i = 1; i <= 50; i++)
        {
            bmson.StopEvents.Add(new BmsonStopEvent { Y = i * 2000, Duration = 240 });
        }

        // 小節線
        for (int i = 1; i <= 200; i++)
        {
            bmson.Lines.Add(new BmsonLineEvent { Y = i * 960 });
        }

        // サウンドチャンネルを 10 個作成し、それぞれ 2000 ノートを配置
        // すべて C = true とする（O(N^2)の最悪ケースをシミュレート）
        for (int chIdx = 0; chIdx < 10; chIdx++)
        {
            var channel = new BmsonSoundChannel
            {
                Name = $"bgm_{chIdx}.wav",
                Notes = []
            };

            for (int noteIdx = 0; noteIdx < 10000; noteIdx++)
            {
                channel.Notes.Add(new BmsonNote
                {
                    X = 0, // BGM
                    Y = noteIdx * 10,
                    C = true
                });
            }

            bmson.SoundChannels.Add(channel);
        }

        // 2. 依存コンポーネントの作成
        var timeCalc = new PulseToBmsTimeCalculator(bmson.Info.Resolution, bmson.Lines);
        var realTimeCalc = new PulseToRealTimeCalculator(bmson.Info.Resolution, bmson.Info.InitBpm, bmson.BpmEvents, bmson.StopEvents);

        // 実在しないパスを指定してファイルI/Oをスキップさせる
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var audioSlicer = new AudioSliceManager(tempDir, tempDir);

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
}
