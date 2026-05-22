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
    /// bmsonファイルをBMSファイルにダウンコンバートします。
    /// 生成されたファイル群は、出力先ディレクトリに保存されます。
    /// </summary>
    /// <param name="bmsonFilePath">入力bmsonファイルのフルパス</param>
    /// <param name="outputDir">WAVスライスとBMSファイルを保存するディレクトリ</param>
    /// <param name="keyNotesOnly">trueの場合、BGMレーンを無視して演奏ノーツのみを抽出する</param>
    /// <returns>生成されたBMSファイルのフルパス</returns>
    public static string Downconvert(string bmsonFilePath, string outputDir, bool keyNotesOnly)
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
        var audioSlicer = new AudioSliceManager(bmsonDir, outputDir);

        // 5. スコアジェネレータの実行
        // ※内部でPre-Sliceを行い、スライス数を数えた上で最適な進数(36 or 62)を自動選択する
        var generator = new BmsScoreGenerator(bmson, timeCalc, realTimeCalc, audioSlicer, keyNotesOnly);
        string bmsText = generator.GenerateBmsText();

        // 6. BMSファイルとして保存
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(bmsonFilePath);
        string outBmsPath = Path.Combine(outputDir, $"{fileNameWithoutExt}_downconverted.bms");

        // Shift_JIS (CP932) で保存
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var shiftJis = Encoding.GetEncoding(932);
        File.WriteAllText(outBmsPath, bmsText, shiftJis);

        return outBmsPath;
    }
}
