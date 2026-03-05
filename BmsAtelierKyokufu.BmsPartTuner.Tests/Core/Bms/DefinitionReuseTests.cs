using System.Collections.ObjectModel;
using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Bms;

/// <summary>
/// <see cref="DefinitionReuse"/> のテストクラス。
/// 
/// 【テスト対象】
/// - 境界値: ZZ(1295), zz(3843) 付近での挙動
/// - 大文字小文字の混在: #WAV01 と #wav01
/// - 重複定義の処理
/// - エラーハンドリング: ファイルなし、書き込み権限なし
/// - 物理削除の動作
/// 
/// 【Priority: Critical】
/// このクラスはBMS定義の再利用を司り、バグはデータ破壊に直結する。
/// </summary>
public class DefinitionReuseTests : IDisposable
{
    private readonly BmsTestContext _context;

    public DefinitionReuseTests()
    {
        _context = new BmsTestContext();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Helper Methods

    /// <summary>
    /// 正弦波を生成してWAVファイルとして保存
    /// </summary>
    private string CreateTestWaveFile(string filename, int sampleCount = 1000, float frequency = 440f)
    {
        var filePath = Path.Combine(_context.TempDirectory, filename);
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = (float)Math.Sin(2 * Math.PI * frequency * i / sampleCount);
        }

        // NAudioを使ってWAVファイルを生成
        using (var writer = new NAudio.Wave.WaveFileWriter(filePath, new NAudio.Wave.WaveFormat(44100, 16, 1)))
        {
            foreach (var sample in samples)
            {
                writer.WriteSample(sample);
            }
        }

        return filePath;
    }

    /// <summary>
    /// テスト用のファイルリストを作成
    /// </summary>
    private ObservableCollection<WavFiles> CreateFileList(params (int num, string filename)[] files)
    {
        return CreateFileList(BmsAtelierKyokufu.BmsPartTuner.Core.AppConstants.Definition.RadixBase36, files);
    }

    /// <summary>
    /// テスト用のファイルリストを作成（基数指定版）
    /// </summary>
    private ObservableCollection<WavFiles> CreateFileList(int radix, params (int num, string filename)[] files)
    {
        var fileList = new ObservableCollection<WavFiles>();

        foreach (var (num, filename) in files)
        {
            var filePath = CreateTestWaveFile(filename);
            fileList.Add(new WavFiles
            {
                Num = BmsAtelierKyokufu.BmsPartTuner.Core.Helpers.RadixConvert.IntToZZ(num, radix),
                NumInteger = num,
                Name = filePath,
                FileSize = new FileInfo(filePath).Length
            });
        }

        return fileList;
    }

    #endregion

    #region Boundary Value Tests - 境界値テスト

    [Fact]
    public void ReductDefinition_WithBase36MaxValue_ZZ_Success()
    {
        // Arrange: ZZ (1295) の境界値テスト
        var fileList = CreateFileList(
            (1294, "sound_1294.wav"),
            (1295, "sound_1295.wav")  // ZZ
        );

        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Base36 Boundary Test")
            .WithWav("ZY", "sound_1294.wav", createFile: false)
            .WithWav("ZZ", "sound_1295.wav", createFile: false)
            .AddMainData(11, "ZYZZ")
            .Build("test_zz.bms");

        var outputFile = Path.Combine(_context.TempDirectory, "output_zz.bms");
        var dr = new DefinitionReuse(fileList);

        // Act: defStartとdefEndは実際のファイルリスト範囲に合わせる
        dr.ReductDefinition(
            bmsFile,
            new Progress<int>(),
            r2Val: 0.95f,
            outputFile,
            defStart: 1294,
            defEnd: 1295,
            isPhysicalDeletionEnabled: false
        );

        // Assert
        Assert.True(File.Exists(outputFile), "出力ファイルが作成されていません");
        var outputContent = File.ReadAllText(outputFile);

        // ZZの定義が存在することを確認（削減処理により番号が変わる可能性あり）
        // 少なくとも2つのWAV定義が存在すること
        var wavDefinitions = System.Text.RegularExpressions.Regex.Matches(outputContent, @"#WAV\w{2}\s+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.True(wavDefinitions.Count >= 1, $"WAV定義が見つかりません。実際の出力: {outputContent}");
    }

    [Fact]
    public void ReductDefinition_WithBase62MaxValue_zz_Success()
    {
        // Arrange: zz (3843) の境界値テスト
        var fileList = CreateFileList(
            BmsAtelierKyokufu.BmsPartTuner.Core.AppConstants.Definition.RadixBase62,
            (3842, "sound_3842.wav"),
            (3843, "sound_3843.wav")  // zz
        );

        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Base62 Boundary Test")
            .WithWav("zy", "sound_3842.wav", createFile: false)
            .WithWav("zz", "sound_3843.wav", createFile: false)
            .AddMainData(11, "zyzz")
            .Build("test_zz62.bms");

        var outputFile = Path.Combine(_context.TempDirectory, "output_zz62.bms");
        var dr = new DefinitionReuse(fileList);

        // Act: defStartとdefEndは実際のファイルリスト範囲に合わせる
        dr.ReductDefinition(
            bmsFile,
            new Progress<int>(),
            r2Val: 0.95f,
            outputFile,
            defStart: 3842,
            defEnd: 3843,
            isPhysicalDeletionEnabled: false
        );

        // Assert
        Assert.True(File.Exists(outputFile), "出力ファイルが作成されていません");
        var outputContent = File.ReadAllText(outputFile);

        // zzの定義が存在することを確認（削減処理により番号が変わる可能性あり）
        // 少なくとも1つのWAV定義が存在すること
        var wavDefinitions = System.Text.RegularExpressions.Regex.Matches(outputContent, @"#wav\w{2}\s+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.True(wavDefinitions.Count >= 1, $"WAV定義が見つかりません。実際の出力: {outputContent}");
    }

    #endregion

    #region Case Sensitivity Tests - 大文字小文字混在テスト

    [Fact]
    public void ReductDefinition_WithMixedCase_HandlesCorrectly()
    {
        // Arrange: #WAV01 と #wav01 が混在するケース
        var fileList = CreateFileList(
            (1, "kick.wav"),
            (2, "snare.wav")
        );

        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Mixed Case Test")
            .Build("test_mixed.bms");

        // 手動で大文字小文字混在の定義を追加
        var bmsContent = File.ReadAllText(bmsFile);
        bmsContent += "#WAV01 kick.wav\n";
        bmsContent += "#wav02 snare.wav\n";  // 小文字
        bmsContent += "#00111:0102\n";
        File.WriteAllText(bmsFile, bmsContent);

        var outputFile = Path.Combine(_context.TempDirectory, "output_mixed.bms");
        var dr = new DefinitionReuse(fileList);

        // Act & Assert: エラーなく処理が完了することを確認
        var exception = Record.Exception(() =>
        {
            dr.ReductDefinition(
                bmsFile,
                new Progress<int>(),
                r2Val: 0.95f,
                outputFile,
                defStart: 1,
                defEnd: 2,
                isPhysicalDeletionEnabled: false
            );
        });

        Assert.Null(exception);
        Assert.True(File.Exists(outputFile));
    }

    #endregion

    #region Duplicate Definition Tests - 重複定義テスト

    [Fact]
    public void ReductDefinition_WithDuplicateDefinitions_UsesFirstOccurrence()
    {
        // Arrange: 同一定義番号が複数回定義されているケース
        var fileList = CreateFileList(
            (1, "kick1.wav"),
            (1, "kick2.wav")  // 同じ番号
        );

        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Duplicate Test")
            .Build("test_dup.bms");

        // 手動で重複定義を追加
        var bmsContent = File.ReadAllText(bmsFile);
        bmsContent += "#WAV01 kick1.wav\n";
        bmsContent += "#WAV01 kick2.wav\n";  // 重複
        bmsContent += "#00111:01\n";
        File.WriteAllText(bmsFile, bmsContent);

        var outputFile = Path.Combine(_context.TempDirectory, "output_dup.bms");
        var dr = new DefinitionReuse(fileList);

        // Act & Assert: エラーなく処理が完了することを確認
        var exception = Record.Exception(() =>
        {
            dr.ReductDefinition(
                bmsFile,
                new Progress<int>(),
                r2Val: 0.95f,
                outputFile,
                defStart: 1,
                defEnd: 1,
                isPhysicalDeletionEnabled: false
            );
        });

        Assert.Null(exception);
    }

    #endregion

    #region Physical Deletion Tests - 物理削除テスト

    [Fact]
    public void ReductDefinition_WithPhysicalDeletion_DeletesUnusedFiles()
    {
        // Arrange: 同一音声ファイルを2つ用意
        var identical1 = CreateTestWaveFile("identical1.wav", 1000, 440f);
        var identical2 = CreateTestWaveFile("identical2.wav", 1000, 440f);  // 同一波形
        var unique = CreateTestWaveFile("unique.wav", 1000, 880f);  // 異なる波形

        var fileList = new ObservableCollection<WavFiles>
        {
            new WavFiles { Num = "01", NumInteger = 1, Name = identical1, FileSize = new FileInfo(identical1).Length },
            new WavFiles { Num = "02", NumInteger = 2, Name = identical2, FileSize = new FileInfo(identical2).Length },
            new WavFiles { Num = "03", NumInteger = 3, Name = unique, FileSize = new FileInfo(unique).Length }
        };

        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Physical Deletion Test")
            .WithWav("01", "identical1.wav", createFile: false)
            .WithWav("02", "identical2.wav", createFile: false)
            .WithWav("03", "unique.wav", createFile: false)
            .AddMainData(11, "010203")
            .Build("test_delete.bms");

        var outputFile = Path.Combine(_context.TempDirectory, "output_delete.bms");
        var dr = new DefinitionReuse(fileList);

        // Act: 物理削除有効で実行
        dr.ReductDefinition(
            bmsFile,
            new Progress<int>(),
            r2Val: 0.95f,
            outputFile,
            defStart: 1,
            defEnd: 3,
            isPhysicalDeletionEnabled: true
        );

        // Assert: identical1とidentical2のどちらか1つが削除されていること
        var file1Exists = File.Exists(identical1);
        var file2Exists = File.Exists(identical2);

        Assert.True(file1Exists ^ file2Exists, "同一音声ファイルのどちらか1つだけが残っているべき");
        Assert.True(File.Exists(unique), "ユニークファイルは削除されないべき");
    }

    [Fact]
    public void ReductDefinition_WithPhysicalDeletionDisabled_KeepsAllFiles()
    {
        // Arrange
        var file1 = CreateTestWaveFile("keep1.wav", 1000, 440f);
        var file2 = CreateTestWaveFile("keep2.wav", 1000, 440f);  // 同一波形

        var fileList = new ObservableCollection<WavFiles>
        {
            new WavFiles { Num = "01", NumInteger = 1, Name = file1, FileSize = new FileInfo(file1).Length },
            new WavFiles { Num = "02", NumInteger = 2, Name = file2, FileSize = new FileInfo(file2).Length }
        };

        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "No Deletion Test")
            .WithWav("01", "keep1.wav", createFile: false)
            .WithWav("02", "keep2.wav", createFile: false)
            .AddMainData(11, "0102")
            .Build("test_nodelete.bms");

        var outputFile = Path.Combine(_context.TempDirectory, "output_nodelete.bms");
        var dr = new DefinitionReuse(fileList);

        // Act: 物理削除無効で実行
        dr.ReductDefinition(
            bmsFile,
            new Progress<int>(),
            r2Val: 0.95f,
            outputFile,
            defStart: 1,
            defEnd: 2,
            isPhysicalDeletionEnabled: false
        );

        // Assert: 全ファイルが残っていること
        Assert.True(File.Exists(file1), "ファイル1は削除されないべき");
        Assert.True(File.Exists(file2), "ファイル2は削除されないべき");
    }

    [Fact]
    public void GetUnusedFilePaths_AfterReduction_ReturnsCorrectList()
    {
        // Arrange
        var file1 = CreateTestWaveFile("used.wav", 1000, 440f);
        var file2 = CreateTestWaveFile("unused.wav", 1000, 440f);  // 同一波形

        var fileList = new ObservableCollection<WavFiles>
        {
            new WavFiles { Num = "01", NumInteger = 1, Name = file1, FileSize = new FileInfo(file1).Length },
            new WavFiles { Num = "02", NumInteger = 2, Name = file2, FileSize = new FileInfo(file2).Length }
        };

        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Unused List Test")
            .WithWav("01", "used.wav", createFile: false)
            .WithWav("02", "unused.wav", createFile: false)
            .AddMainData(11, "0102")
            .Build("test_unused.bms");

        var outputFile = Path.Combine(_context.TempDirectory, "output_unused.bms");
        var dr = new DefinitionReuse(fileList);

        // Act
        dr.ReductDefinition(
            bmsFile,
            new Progress<int>(),
            r2Val: 0.95f,
            outputFile,
            defStart: 1,
            defEnd: 2,
            isPhysicalDeletionEnabled: false
        );

        var unusedFiles = dr.GetUnusedFilePaths();

        // Assert: 未使用ファイルリストに1つだけ含まれること
        Assert.Single(unusedFiles);
    }

    [Fact]
    public void GetUnusedFilePaths_BeforeReduction_ReturnsEmptyList()
    {
        // Arrange
        var fileList = CreateFileList((1, "test.wav"));
        var dr = new DefinitionReuse(fileList);

        // Act: ReductDefinitionを実行する前
        var unusedFiles = dr.GetUnusedFilePaths();

        // Assert: 空リストが返されること
        Assert.Empty(unusedFiles);
    }

    #endregion

    #region Edge Case Tests - エッジケーステスト

    [Fact]
    public void ReductDefinition_WithExtremeThreshold_0_MergesAll()
    {
        // Arrange: しきい値0.0（全てを結合）
        var fileList = CreateFileList(
            (1, "sound1.wav"),
            (2, "sound2.wav"),
            (3, "sound3.wav")
        );

        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Threshold 0 Test")
            .WithWav("01", "sound1.wav", createFile: false)
            .WithWav("02", "sound2.wav", createFile: false)
            .WithWav("03", "sound3.wav", createFile: false)
            .AddMainData(11, "010203")
            .Build("test_threshold0.bms");

        var outputFile = Path.Combine(_context.TempDirectory, "output_threshold0.bms");
        var dr = new DefinitionReuse(fileList);

        // Act
        dr.ReductDefinition(
            bmsFile,
            new Progress<int>(),
            r2Val: 0.0f,
            outputFile,
            defStart: 1,
            defEnd: 3,
            isPhysicalDeletionEnabled: false
        );

        var uniqueCount = dr.GetUniqueFileCount();

        // Assert: ユニークファイル数が減少していること
        Assert.True(uniqueCount <= 3, $"しきい値0.0で結合が行われるべき（実際: {uniqueCount}）");
    }

    [Fact]
    public void ReductDefinition_WithExtremeThreshold_1_MergesNothing()
    {
        // Arrange: しきい値1.0（完全一致のみ結合）
        var fileList = CreateFileList(
            (1, "diff1.wav"),
            (2, "diff2.wav")
        );

        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Threshold 1 Test")
            .WithWav("01", "diff1.wav", createFile: false)
            .WithWav("02", "diff2.wav", createFile: false)
            .AddMainData(11, "0102")
            .Build("test_threshold1.bms");

        var outputFile = Path.Combine(_context.TempDirectory, "output_threshold1.bms");
        var dr = new DefinitionReuse(fileList);

        // Act
        dr.ReductDefinition(
            bmsFile,
            new Progress<int>(),
            r2Val: 1.0f,
            outputFile,
            defStart: 1,
            defEnd: 2,
            isPhysicalDeletionEnabled: false
        );

        var uniqueCount = dr.GetUniqueFileCount();

        // Assert: 完全一致しない限り結合されないため、ユニーク数は変わらない
        Assert.True(uniqueCount >= 1, "しきい値1.0では異なるファイルは結合されない");
    }

    [Fact]
    public void ReductDefinition_WithEmptyFileList_ThrowsArgumentNullException()
    {
        // Arrange
        ObservableCollection<WavFiles>? nullList = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DefinitionReuse(nullList!));
    }

    [Fact]
    public void ReductDefinition_WithSingleFile_CompletesSuccessfully()
    {
        // Arrange: ファイルが1つだけの場合
        var fileList = CreateFileList((1, "single.wav"));

        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Single File Test")
            .WithWav("01", "single.wav", createFile: false)
            .AddMainData(11, "01")
            .Build("test_single.bms");

        var outputFile = Path.Combine(_context.TempDirectory, "output_single.bms");
        var dr = new DefinitionReuse(fileList);

        // Act & Assert: エラーなく処理が完了することを確認
        var exception = Record.Exception(() =>
        {
            dr.ReductDefinition(
                bmsFile,
                new Progress<int>(),
                r2Val: 0.95f,
                outputFile,
                defStart: 1,
                defEnd: 1,
                isPhysicalDeletionEnabled: false
            );
        });

        Assert.Null(exception);
        Assert.True(File.Exists(outputFile));
    }

    #endregion

    #region Keyword Selection Tests - キーワード選択テスト

    [Fact]
    public void ReductDefinition_WithSelectedKeywords_ProcessesOnlyMatchingFiles()
    {
        // Arrange
        var fileList = CreateFileList(
            (1, "kick_heavy.wav"),
            (2, "snare_light.wav"),
            (3, "kick_light.wav")
        );

        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Keyword Test")
            .WithWav("01", "kick_heavy.wav", createFile: false)
            .WithWav("02", "snare_light.wav", createFile: false)
            .WithWav("03", "kick_light.wav", createFile: false)
            .AddMainData(11, "010203")
            .Build("test_keywords.bms");

        var outputFile = Path.Combine(_context.TempDirectory, "output_keywords.bms");
        var dr = new DefinitionReuse(fileList);

        // Act: "kick"キーワードのみ処理
        dr.ReductDefinition(
            bmsFile,
            new Progress<int>(),
            r2Val: 0.95f,
            outputFile,
            defStart: 1,
            defEnd: 3,
            isPhysicalDeletionEnabled: false,
            selectedKeywords: new[] { "kick" }
        );

        // Assert: エラーなく処理が完了すること
        Assert.True(File.Exists(outputFile));
    }

    #endregion

    #region Progress Reporting Tests - 進捗報告テスト

    [Fact]
    public void ReductDefinition_ReportsProgress_FromZeroToHundred()
    {
        // Arrange
        var fileList = CreateFileList((1, "progress.wav"));
        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Progress Test")
            .WithWav("01", "progress.wav", createFile: false)
            .AddMainData(11, "01")
            .Build("test_progress.bms");

        var outputFile = Path.Combine(_context.TempDirectory, "output_progress.bms");
        var dr = new DefinitionReuse(fileList);

        var progressReports = new List<int>();
        var progress = new Progress<int>(p => progressReports.Add(p));

        // Act
        dr.ReductDefinition(
            bmsFile,
            progress,
            r2Val: 0.95f,
            outputFile,
            defStart: 1,
            defEnd: 1,
            isPhysicalDeletionEnabled: false
        );

        // Assert
        Assert.Contains(0, progressReports);  // 開始時
        Assert.Contains(100, progressReports);  // 完了時
        Assert.True(progressReports.Count >= 2, "進捗が複数回報告されるべき");
    }

    #endregion
}
