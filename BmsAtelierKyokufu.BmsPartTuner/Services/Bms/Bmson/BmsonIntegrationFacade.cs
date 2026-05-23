using System.Text.Json;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.Bms.Bmson;

/// <summary>
/// bmsonファイルを入力として受け取り、解析・クリーンアップ・スライス・BMSスコア生成までを一貫して行うファサード。
/// </summary>
public class BmsonIntegrationFacade
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
        if (!File.Exists(bmsonFilePath))
            throw new FileNotFoundException("Bmson file not found.", bmsonFilePath);

        // 1. JSONパース
        string json = File.ReadAllText(bmsonFilePath, Encoding.UTF8);
        var bmson = JsonSerializer.Deserialize<BmsonFormat>(json)
            ?? throw new InvalidOperationException("Failed to parse the bmson file (returned null).");

        // 2. サニタイズ（数学的制約の保証）
        BmsonSanitizer.Sanitize(bmson);

        // 3. 数学的時間モデルの構築
        var timeCalc = new PulseToBmsTimeCalculator(bmson.Info.Resolution, bmson.Lines);
        var realTimeCalc = new PulseToRealTimeCalculator(bmson.Info.Resolution, bmson.Info.InitBpm, bmson.BpmEvents, bmson.StopEvents);

        // 4. 音声スライスエンジンの準備
        string bmsonDir = Path.GetDirectoryName(bmsonFilePath) ?? string.Empty;
        var audioSlicer = new AudioSliceManager(bmsonDir);

        // 5. スコアジェネレータの実行
        // ※内部でPre-Sliceを行い、スライス数を数えた上で最適な進数(36 or 62)を自動選択する
        var generator = new BmsScoreGenerator(bmson, timeCalc, realTimeCalc, audioSlicer, keyNotesOnly);
        return generator.GenerateBmsText();
    }
}
