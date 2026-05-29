using System.Text.Json;
namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson.Pipeline;

/// <summary>
/// 入力bmsonファイルが存在するかどうかを検証するステップ。
/// </summary>
public sealed class BmsonValidationStep : IBmsonConversionStep
{
    public string Name => Core.Helpers.PipelineStepHelper.GetStepName(nameof(BmsonValidationStep));
    public void Execute(BmsonConversionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.BmsonFilePath))
            throw new ArgumentException("Bmson file path cannot be empty.", nameof(context));

        if (!File.Exists(context.BmsonFilePath))
            throw new FileNotFoundException("Bmson file not found.", context.BmsonFilePath);
    }
}

/// <summary>
/// Source Generatorを利用して JSON ストリームから BMSON データをデシリアライズするステップ。
/// </summary>
public sealed class BmsonParseStep : IBmsonConversionStep
{
    public string Name => Core.Helpers.PipelineStepHelper.GetStepName(nameof(BmsonParseStep));
    public void Execute(BmsonConversionContext context)
    {
        using var stream = File.OpenRead(context.BmsonFilePath);

        // アセンブリ内で定義されている BmsonJsonContext を使用して高速にデシリアライズ
        context.Bmson = JsonSerializer.Deserialize(stream, BmsonJsonContext.Default.BmsonFormat)
            ?? throw new InvalidOperationException("Failed to parse the bmson file (returned null).");
    }
}

/// <summary>
/// 数学的制約を保証するために BMSON データをサニタイズするステップ。
/// </summary>
public sealed class BmsonSanitizeStep : IBmsonConversionStep
{
    public string Name => Core.Helpers.PipelineStepHelper.GetStepName(nameof(BmsonSanitizeStep));
    public void Execute(BmsonConversionContext context)
    {
        if (context.Bmson == null)
            throw new InvalidOperationException("BMSON data must be parsed before sanitization.");

        context.Bmson = BmsonSanitizer.Sanitize(context.Bmson);
    }
}

/// <summary>
/// パルス数と時間の相互変換を行うための電卓を構築するステップ。
/// </summary>
public sealed class BmsonBuildCalculatorsStep : IBmsonConversionStep
{
    public string Name => Core.Helpers.PipelineStepHelper.GetStepName(nameof(BmsonBuildCalculatorsStep));
    public void Execute(BmsonConversionContext context)
    {
        if (context.Bmson == null)
            throw new InvalidOperationException("BMSON data must be parsed and sanitized before building calculators.");

        var bmson = context.Bmson;
        context.BmsTimeCalculator = new PulseToBmsTimeCalculator(bmson.Info.Resolution, bmson.Lines);
        context.RealTimeCalculator = new PulseToRealTimeCalculator(bmson.Info.Resolution, bmson.Info.InitBpm, bmson.BpmEvents, bmson.StopEvents);
    }
}

/// <summary>
/// 音声スライスエンジン（AudioSliceManager）を初期化するステップ。
/// </summary>
public sealed class BmsonPrepareAudioSlicerStep : IBmsonConversionStep
{
    public string Name => Core.Helpers.PipelineStepHelper.GetStepName(nameof(BmsonPrepareAudioSlicerStep));
    public void Execute(BmsonConversionContext context)
    {
        string bmsonDir = Path.GetDirectoryName(context.BmsonFilePath) ?? string.Empty;

        // コンテキストが管理する IDisposable なリソースとして AudioSliceManager を生成
        context.AudioSlicer = new AudioSliceManager(bmsonDir);

        PerformanceDebugLogger.LogMemoryUsage("Before BmsScoreGenerator (Engine ready)");
    }
}

/// <summary>
/// スコアジェネレータ（BmsScoreGenerator）を実行して、BMSテキストを出力する最終ステップ。
/// </summary>
public sealed class BmsScoreGenerateStep : IBmsonConversionStep
{
    public string Name => Core.Helpers.PipelineStepHelper.GetStepName(nameof(BmsScoreGenerateStep));
    public void Execute(BmsonConversionContext context)
    {
        if (context.Bmson == null)
            throw new InvalidOperationException("BMSON data is not available.");
        if (context.BmsTimeCalculator == null)
            throw new InvalidOperationException("PulseToBmsTimeCalculator is not initialized.");
        if (context.RealTimeCalculator == null)
            throw new InvalidOperationException("PulseToRealTimeCalculator is not initialized.");
        if (context.AudioSlicer == null)
            throw new InvalidOperationException("AudioSliceManager is not initialized.");

        // スコアジェネレータのインスタンス化と実行
        var generator = new BmsScoreGenerator(
            context.Bmson,
            context.BmsTimeCalculator,
            context.RealTimeCalculator,
            context.AudioSlicer,
            context.KeyNotesOnly);

        context.ResultBmsText = generator.GenerateBmsText();

        PerformanceDebugLogger.LogMemoryUsage("After BmsScoreGenerator (Downconvert finished)");
    }
}
