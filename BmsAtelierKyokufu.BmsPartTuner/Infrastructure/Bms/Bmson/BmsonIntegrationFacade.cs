using System.IO;
using System.Text.Json.Serialization;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson.Pipeline;
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
        // 変換パイプラインの構築
        var pipeline = new BmsonConversionPipeline()
            .AddStep(new BmsonValidationStep())
            .AddStep(new BmsonParseStep())
            .AddStep(new BmsonSanitizeStep())
            .AddStep(new BmsonBuildCalculatorsStep())
            .AddStep(new BmsonPrepareAudioSlicerStep())
            .AddStep(new BmsScoreGenerateStep());

        // 使い終わったリソース（AudioSliceManager等）を安全に解放するため、usingを使用
        using var context = new BmsonConversionContext(bmsonFilePath, keyNotesOnly);
        return pipeline.Execute(context);
    }
}

