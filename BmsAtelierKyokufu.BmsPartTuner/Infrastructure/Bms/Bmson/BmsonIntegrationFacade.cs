using System.Text.Json;
using System.Text.Json.Serialization;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(BmsonFormat))]
internal partial class BmsonJsonContext : JsonSerializerContext;

/// <summary>
/// bmsonファイルを入力として受け取り、解析・クリーンアップ・スライス・BMSスコア生成までを一貫して行うファサード。
/// </summary>
public static class BmsonIntegrationFacade
{
    /// <summary>
    /// bmsonファイルをBMSフォーマットに変換し、結果のテキストを返します。
    /// （音声スライスは VirtualAudioRegistry にオンメモリで保持されます）
    /// </summary>
    /// <param name="bmsonFilePath">入力bmsonファイルのフルパス</param>
    /// <param name="keyNotesOnly">trueの場合、BGMレーンを無視して演奏ノーツのみを抽出する</param>
    /// <returns>生成されたBMSテキスト</returns>
    public static string GenerateBmsText(string bmsonFilePath, bool keyNotesOnly)
    {
        PerformanceDebugLogger.Clear();
        PerformanceDebugLogger.WriteDebug(nameof(BmsonIntegrationFacade), "=== Downconvert started ===");
        var timerTotal = PerformanceDebugLogger.StartTimer();

        if (!File.Exists(bmsonFilePath))
            throw new FileNotFoundException("Bmson file not found.", bmsonFilePath);

        var timer = PerformanceDebugLogger.StartTimer();

        // 1. JSONパース (Source Generation + Streamを使用)
        using var stream = File.OpenRead(bmsonFilePath);
        var bmson = JsonSerializer.Deserialize(stream, BmsonJsonContext.Default.BmsonFormat)
            ?? throw new InvalidOperationException("Failed to parse the bmson file (returned null).");
        PerformanceDebugLogger.WriteDebug(nameof(BmsonIntegrationFacade), $"JSON read & parse: {timer.Lap("JSON read & parse")} ms");

        // 2. サニタイズ（数学的制約の保証）
        BmsonSanitizer.Sanitize(bmson);
        PerformanceDebugLogger.WriteDebug(nameof(BmsonIntegrationFacade), $"Sanitize: {timer.Lap("Sanitize")} ms");

        // 3. 数学的時間モデルの構築
        var timeCalc = new PulseToBmsTimeCalculator(bmson.Info.Resolution, bmson.Lines);
        var realTimeCalc = new PulseToRealTimeCalculator(bmson.Info.Resolution, bmson.Info.InitBpm, bmson.BpmEvents, bmson.StopEvents);
        PerformanceDebugLogger.WriteDebug(nameof(BmsonIntegrationFacade), $"Time calculators build: {timer.Lap("Time calculators build")} ms");

        // 4. 音声スライスエンジンの準備
        string bmsonDir = Path.GetDirectoryName(bmsonFilePath) ?? string.Empty;
        using var audioSlicer = new AudioSliceManager(bmsonDir);

        PerformanceDebugLogger.LogMemoryUsage("Before BmsScoreGenerator (Engine ready)");

        // 5. スコアジェネレータの実行
        // ※内部でPre-Sliceを行い、スライス数を数えた上で最適な進数(36 or 62)を自動選択する
        var generator = new BmsScoreGenerator(bmson, timeCalc, realTimeCalc, audioSlicer, keyNotesOnly);
        string result = generator.GenerateBmsText();
        PerformanceDebugLogger.WriteDebug(nameof(BmsonIntegrationFacade), $"BmsScoreGenerator run: {timer.Lap("BmsScoreGenerator run")} ms");

        PerformanceDebugLogger.LogMemoryUsage("After BmsScoreGenerator (Downconvert finished)");

        PerformanceDebugLogger.WriteDebug(nameof(BmsonIntegrationFacade), $"=== Downconvert finished. Total: {timerTotal.Lap("Total")} ms ===");
        return result;
    }
}
