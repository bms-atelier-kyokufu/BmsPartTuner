using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Infrastructure.Bms.Bmson;

/// <summary>
/// <see cref="AudioSliceManager"/> の音声ファイルフォールバック機能を検証するテストクラス。
/// </summary>
public class AudioSliceManagerTests
{
    [Fact]
    public void PreloadAudioSource_OggToWavFallback_LoadsWavSuccessfully()
    {
        using var context = new BmsFamilyTestContext();
        var tempDir = context.TempDirectory;

        // sound.wav を作成（sound.ogg は存在しない）
        string wavPath = Path.Combine(tempDir, "sound.wav");
        BmsTestWavHelper.CreateSilenceWavFile(wavPath, 0.1, 2);

        using var slicer = new AudioSliceManager(tempDir, throwOnMissingFile: true);

        // 存在しない sound.ogg をロードしようとした際に、sound.wav へフォールバックして正常終了することを確認
        var exception = Record.Exception(() => slicer.PreloadAudioSource("sound.ogg"));
        Assert.Null(exception);
    }

    [Fact]
    public void PreloadAudioSource_WavToOggFallback_AttemptsToLoadOgg()
    {
        using var context = new BmsFamilyTestContext();
        var tempDir = context.TempDirectory;

        // ダミーの sound.ogg を作成（sound.wav は存在しない）
        string oggPath = Path.Combine(tempDir, "sound.ogg");
        File.WriteAllText(oggPath, "dummy ogg content");

        using var slicer = new AudioSliceManager(tempDir, throwOnMissingFile: true);

        // 存在しない sound.wav をロードしようとした際に、sound.ogg へフォールバックしようとすることを確認。
        // ファイルは存在するがデータが無効なためデコードエラー（FileNotFoundException 以外の例外）が発生することを確認。
        var exception = Assert.ThrowsAny<Exception>(() => slicer.PreloadAudioSource("sound.wav"));
        Assert.IsNotType<FileNotFoundException>(exception);
    }
}
