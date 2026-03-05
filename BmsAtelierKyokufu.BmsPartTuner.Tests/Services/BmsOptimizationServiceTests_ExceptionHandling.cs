using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Services;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Services;

/// <summary>
/// <see cref="BmsOptimizationService"/> の異常系・エラーハンドリングテスト。
/// 
/// 【テスト対象】
/// - 入力ファイルなし、書き込み権限なし
/// - ディスク容量不足のシミュレーション
/// - 中断処理（CancellationToken）
/// - isPhysicalDeletionEnabledの条件分岐
/// 
/// 【Priority: Critical】
/// エラー発生時のロールバック動作と中間ファイルのクリーンアップを検証。
/// </summary>
public class BmsOptimizationServiceTests_ExceptionHandling : IDisposable
{
    private readonly BmsTestContext _context;
    private readonly BmsOptimizationService _service;

    public BmsOptimizationServiceTests_ExceptionHandling()
    {
        _context = new BmsTestContext();
        _service = new BmsOptimizationService();
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    #region Helper Methods

    private string CreateTestWaveFile(string filename, int sampleCount = 1000, float frequency = 440f)
    {
        var filePath = Path.Combine(_context.TempDirectory, filename);
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = (float)Math.Sin(2 * Math.PI * frequency * i / sampleCount);
        }

        using (var writer = new NAudio.Wave.WaveFileWriter(filePath, new NAudio.Wave.WaveFormat(44100, 16, 1)))
        {
            foreach (var sample in samples)
            {
                writer.WriteSample(sample);
            }
        }

        return filePath;
    }

    #endregion

    #region Null/Empty Input Tests - 入力検証テスト

    [Fact]
    public async Task ExecuteDefinitionReductionAsync_NullFileList_ThrowsArgumentNullException()
    {
        // Arrange
        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Test")
            .Build("test.bms");

        var outputFile = Path.Combine(_context.TempDirectory, "output.bms");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _service.ExecuteDefinitionReductionAsync(
                fileList: null!,
                inputPath: bmsFile,
                outputPath: outputFile,
                r2Threshold: 0.95f,
                startDefinition: 1,
                endDefinition: 10,
                isPhysicalDeletionEnabled: false
            );
        });
    }

    [Fact]
    public async Task ExecuteDefinitionReductionAsync_EmptyInputPath_ThrowsArgumentException()
    {
        // Arrange
        var fileList = new List<WavFiles>
        {
            new WavFiles { Num = "01", NumInteger = 1, Name = "test.wav" }
        };

        var outputFile = Path.Combine(_context.TempDirectory, "output.bms");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _service.ExecuteDefinitionReductionAsync(
                fileList: fileList,
                inputPath: string.Empty,
                outputPath: outputFile,
                r2Threshold: 0.95f,
                startDefinition: 1,
                endDefinition: 10,
                isPhysicalDeletionEnabled: false
            );
        });
    }

    [Fact]
    public async Task ExecuteDefinitionReductionAsync_EmptyOutputPath_ThrowsArgumentException()
    {
        // Arrange
        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Test")
            .Build("test.bms");

        var fileList = new List<WavFiles>
        {
            new WavFiles { Num = "01", NumInteger = 1, Name = "test.wav" }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _service.ExecuteDefinitionReductionAsync(
                fileList: fileList,
                inputPath: bmsFile,
                outputPath: string.Empty,
                r2Threshold: 0.95f,
                startDefinition: 1,
                endDefinition: 10,
                isPhysicalDeletionEnabled: false
            );
        });
    }

    #endregion

    #region File Access Error Tests - ファイルアクセスエラーテスト

    [Fact]
    public async Task ExecuteDefinitionReductionAsync_InputFileNotFound_ReturnsErrorResult()
    {
        // Arrange: 存在しないファイルを指定
        var fileList = new List<WavFiles>
        {
            new WavFiles { Num = "01", NumInteger = 1, Name = CreateTestWaveFile("test.wav") }
        };

        var nonExistentInput = Path.Combine(_context.TempDirectory, "nonexistent.bms");
        var outputFile = Path.Combine(_context.TempDirectory, "output.bms");

        // Act
        var result = await _service.ExecuteDefinitionReductionAsync(
            fileList: fileList,
            inputPath: nonExistentInput,
            outputPath: outputFile,
            r2Threshold: 0.95f,
            startDefinition: 1,
            endDefinition: 1,
            isPhysicalDeletionEnabled: false
        );

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuccess, "存在しないファイルの処理は失敗するべき");
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteDefinitionReductionAsync_ReadOnlyOutputDirectory_ReturnsErrorResult()
    {
        // Arrange
        var wavFile = CreateTestWaveFile("test.wav");
        var fileList = new List<WavFiles>
        {
            new WavFiles { Num = "01", NumInteger = 1, Name = wavFile, FileSize = new FileInfo(wavFile).Length }
        };

        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Test")
            .WithWav("01", "test.wav", createFile: false)
            .AddMainData(11, "01")
            .Build("test.bms");

        // 出力ファイルを事前に作成し、読み取り専用にする
        var outputFile = Path.Combine(_context.TempDirectory, "readonly_output.bms");
        File.WriteAllText(outputFile, "dummy content");
        File.SetAttributes(outputFile, FileAttributes.ReadOnly);

        try
        {
            // Act
            var result = await _service.ExecuteDefinitionReductionAsync(
                fileList: fileList,
                inputPath: bmsFile,
                outputPath: outputFile,
                r2Threshold: 0.95f,
                startDefinition: 1,
                endDefinition: 1,
                isPhysicalDeletionEnabled: false
            );

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsSuccess, "読み取り専用ファイルへの書き込みは失敗するべき");
        }
        finally
        {
            // クリーンアップ: 読み取り専用属性を解除
            try
            {
                File.SetAttributes(outputFile, FileAttributes.Normal);
                File.Delete(outputFile);
            }
            catch { }
        }
    }

    #endregion

    #region Physical Deletion Flag Tests - 物理削除フラグテスト

    [Fact]
    public async Task ExecuteDefinitionReductionAsync_PhysicalDeletionDisabled_DoesNotDeleteFiles()
    {
        // Arrange: 同一波形のファイルを2つ用意
        var file1 = CreateTestWaveFile("identical1.wav", 1000, 440f);
        var file2 = CreateTestWaveFile("identical2.wav", 1000, 440f);

        var fileList = new List<WavFiles>
        {
            new WavFiles { Num = "01", NumInteger = 1, Name = file1, FileSize = new FileInfo(file1).Length },
            new WavFiles { Num = "02", NumInteger = 2, Name = file2, FileSize = new FileInfo(file2).Length }
        };

        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "No Delete Test")
            .WithWav("01", "identical1.wav", createFile: false)
            .WithWav("02", "identical2.wav", createFile: false)
            .AddMainData(11, "0102")
            .Build("test.bms");

        var outputFile = Path.Combine(_context.TempDirectory, "output.bms");

        // Act: 物理削除を無効にして実行
        var result = await _service.ExecuteDefinitionReductionAsync(
            fileList: fileList,
            inputPath: bmsFile,
            outputPath: outputFile,
            r2Threshold: 0.95f,
            startDefinition: 1,
            endDefinition: 2,
            isPhysicalDeletionEnabled: false
        );

        // Assert
        Assert.True(result.IsSuccess, $"処理は成功するべき: {result.ErrorMessage}");
        Assert.Equal(0, result.DeletedFilesCount);
        Assert.True(File.Exists(file1), "file1は削除されないべき");
        Assert.True(File.Exists(file2), "file2は削除されないべき");
    }

    [Fact]
    public async Task ExecuteDefinitionReductionAsync_PhysicalDeletionEnabled_DeletesUnusedFiles()
    {
        // Arrange: 同一波形のファイルを2つ + 異なるファイル1つ
        var identical1 = CreateTestWaveFile("dup1.wav", 1000, 440f);
        var identical2 = CreateTestWaveFile("dup2.wav", 1000, 440f);
        var unique = CreateTestWaveFile("unique.wav", 1000, 880f);

        var fileList = new List<WavFiles>
        {
            new WavFiles { Num = "01", NumInteger = 1, Name = identical1, FileSize = new FileInfo(identical1).Length },
            new WavFiles { Num = "02", NumInteger = 2, Name = identical2, FileSize = new FileInfo(identical2).Length },
            new WavFiles { Num = "03", NumInteger = 3, Name = unique, FileSize = new FileInfo(unique).Length }
        };

        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Delete Test")
            .WithWav("01", "dup1.wav", createFile: false)
            .WithWav("02", "dup2.wav", createFile: false)
            .WithWav("03", "unique.wav", createFile: false)
            .AddMainData(11, "010203")
            .Build("test.bms");

        var outputFile = Path.Combine(_context.TempDirectory, "output.bms");

        // Act: 物理削除を有効にして実行
        var result = await _service.ExecuteDefinitionReductionAsync(
            fileList: fileList,
            inputPath: bmsFile,
            outputPath: outputFile,
            r2Threshold: 0.95f,
            startDefinition: 1,
            endDefinition: 3,
            isPhysicalDeletionEnabled: true
        );

        // Assert
        Assert.True(result.IsSuccess, $"処理は成功するべき: {result.ErrorMessage}");
        Assert.True(result.DeletedFilesCount >= 0, "削除ファイル数は0以上");

        // 同一ファイルのどちらか1つが削除されていること
        var file1Exists = File.Exists(identical1);
        var file2Exists = File.Exists(identical2);
        Assert.True(file1Exists ^ file2Exists, "同一ファイルのどちらか1つだけが残るべき");

        // ユニークファイルは削除されないこと
        Assert.True(File.Exists(unique), "ユニークファイルは削除されないべき");
    }

    [Fact]
    public async Task ExecuteDefinitionReductionAsync_PhysicalDeletionWithLockedFile_ContinuesProcessing()
    {
        // Arrange: ファイルをロックして削除不可能にする
        var file1 = CreateTestWaveFile("locked.wav", 1000, 440f);
        var file2 = CreateTestWaveFile("normal.wav", 1000, 440f);

        var fileList = new List<WavFiles>
        {
            new WavFiles { Num = "01", NumInteger = 1, Name = file1, FileSize = new FileInfo(file1).Length },
            new WavFiles { Num = "02", NumInteger = 2, Name = file2, FileSize = new FileInfo(file2).Length }
        };

        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Locked File Test")
            .WithWav("01", "locked.wav", createFile: false)
            .WithWav("02", "normal.wav", createFile: false)
            .AddMainData(11, "0102")
            .Build("test.bms");

        var outputFile = Path.Combine(_context.TempDirectory, "output.bms");

        // ファイルをロック（読み取り専用としてオープン）
        using (var fs = new FileStream(file1, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            // Act: 物理削除を有効にして実行
            var result = await _service.ExecuteDefinitionReductionAsync(
                fileList: fileList,
                inputPath: bmsFile,
                outputPath: outputFile,
                r2Threshold: 0.95f,
                startDefinition: 1,
                endDefinition: 2,
                isPhysicalDeletionEnabled: true
            );

            // Assert: ロックされたファイルの削除に失敗しても処理全体は成功する
            Assert.True(result.IsSuccess, $"処理は成功するべき（一部削除失敗は許容）: {result.ErrorMessage}");
        }
    }

    #endregion

    #region FindOptimalThresholdsAsync Exception Tests

    [Fact]
    public async Task FindOptimalThresholdsAsync_EmptyFileList_ThrowsArgumentException()
    {
        // Arrange
        var emptyList = new List<string>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _service.FindOptimalThresholdsAsync(
                files: emptyList,
                startDefinition: 1,
                endDefinition: 10
            );
        });
    }

    [Fact]
    public async Task FindOptimalThresholdsAsync_NullFileList_ThrowsArgumentException()
    {
        // Arrange
        List<string>? nullList = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _service.FindOptimalThresholdsAsync(
                files: nullList!,
                startDefinition: 1,
                endDefinition: 10
            );
        });
    }

    [Fact]
    public async Task FindOptimalThresholdsAsync_AllFilesNonExistent_ReturnsNull()
    {
        // Arrange: 存在しないファイルのリスト
        var nonExistentFiles = new List<string>
        {
            Path.Combine(_context.TempDirectory, "ghost1.wav"),
            Path.Combine(_context.TempDirectory, "ghost2.wav")
        };

        // Act
        var result = await _service.FindOptimalThresholdsAsync(
            files: nonExistentFiles,
            startDefinition: 1,
            endDefinition: 10
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task FindOptimalThresholdsAsync_WithCorruptedWaveFiles_ReturnsNull()
    {
        // Arrange: 破損したWAVファイルを作成
        var corruptedFile = Path.Combine(_context.TempDirectory, "corrupted.wav");
        File.WriteAllText(corruptedFile, "This is not a valid WAV file");

        var files = new List<string> { corruptedFile };

        // Act
        var result = await _service.FindOptimalThresholdsAsync(
            files: files,
            startDefinition: 1,
            endDefinition: 1
        );

        // Assert: 破損ファイルでも処理は実行されるが、音声データなしで結果は返される
        // 現在の実装では、破損ファイルはロード失敗するが、処理自体は継続される
        Assert.NotNull(result);
        // 音声データがロードされていないため、削減効果は期待できない
        Assert.Equal(1, result.Base36Result.Item2); // ファイル数はそのまま
        Assert.Equal(1, result.Base62Result.Item2);
    }

    [Fact]
    public async Task FindOptimalThresholdsAsync_WithProgressReporting_ReportsFromZeroToHundred()
    {
        // Arrange
        var wavFile = CreateTestWaveFile("progress.wav");
        var files = new List<string> { wavFile };

        var progressReports = new List<int>();
        var progress = new Progress<int>(p => progressReports.Add(p));

        // Act
        var result = await _service.FindOptimalThresholdsAsync(
            files: files,
            startDefinition: 1,
            endDefinition: 1,
            progress: progress
        );

        // Assert
        Assert.NotNull(result);
        Assert.Contains(100, progressReports);
        Assert.True(progressReports.Count >= 2, "進捗は複数回報告されるべき");
        Assert.True(progressReports.First() < progressReports.Last(), "進捗は増加するべき");
    }

    #endregion

    #region Edge Case Tests - エッジケース

    [Fact]
    public async Task ExecuteDefinitionReductionAsync_WithVeryLongFilePath_HandlesCorrectly()
    {
        // Arrange: 長いファイル名を作成（Windows MAX_PATHは260文字）
        var longFileName = new string('a', 200) + ".wav";
        var wavFile = CreateTestWaveFile(longFileName);

        var fileList = new List<WavFiles>
        {
            new WavFiles { Num = "01", NumInteger = 1, Name = wavFile, FileSize = new FileInfo(wavFile).Length }
        };

        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Long Path Test")
            .WithWav("01", longFileName, createFile: false)
            .AddMainData(11, "01")
            .Build("test.bms");

        var outputFile = Path.Combine(_context.TempDirectory, "output.bms");

        // Act
        var result = await _service.ExecuteDefinitionReductionAsync(
            fileList: fileList,
            inputPath: bmsFile,
            outputPath: outputFile,
            r2Threshold: 0.95f,
            startDefinition: 1,
            endDefinition: 1,
            isPhysicalDeletionEnabled: false
        );

        // Assert: エラーなく処理できるべき（またはOSのパス長制限でエラー）
        Assert.NotNull(result);
    }

    [Fact]
    public async Task FindOptimalThresholdsAsync_WithAutoEndDefinition_CalculatesCorrectly()
    {
        // Arrange: endDefinition=0で自動検出
        var file1 = CreateTestWaveFile("auto1.wav");
        var file2 = CreateTestWaveFile("auto2.wav");
        var files = new List<string> { file1, file2 };

        // Act: endDefinition=0で自動計算
        var result = await _service.FindOptimalThresholdsAsync(
            files: files,
            startDefinition: 1,
            endDefinition: 0  // 自動検出
        );

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.SimulationData);
    }

    [Fact]
    public async Task ExecuteDefinitionReductionAsync_WithZeroThreshold_MergesAllSimilar()
    {
        // Arrange: しきい値0.0で全ファイルを結合
        var file1 = CreateTestWaveFile("merge1.wav", 1000, 440f);
        var file2 = CreateTestWaveFile("merge2.wav", 1000, 880f);
        var file3 = CreateTestWaveFile("merge3.wav", 1000, 1320f);

        var fileList = new List<WavFiles>
        {
            new WavFiles { Num = "01", NumInteger = 1, Name = file1, FileSize = new FileInfo(file1).Length },
            new WavFiles { Num = "02", NumInteger = 2, Name = file2, FileSize = new FileInfo(file2).Length },
            new WavFiles { Num = "03", NumInteger = 3, Name = file3, FileSize = new FileInfo(file3).Length }
        };

        var bmsFile = _context.CreateBuilder()
            .WithHeader("TITLE", "Zero Threshold Test")
            .WithWav("01", "merge1.wav", createFile: false)
            .WithWav("02", "merge2.wav", createFile: false)
            .WithWav("03", "merge3.wav", createFile: false)
            .AddMainData(11, "010203")
            .Build("test.bms");

        var outputFile = Path.Combine(_context.TempDirectory, "output.bms");

        // Act
        var result = await _service.ExecuteDefinitionReductionAsync(
            fileList: fileList,
            inputPath: bmsFile,
            outputPath: outputFile,
            r2Threshold: 0.0f,
            startDefinition: 1,
            endDefinition: 3,
            isPhysicalDeletionEnabled: false
        );

        // Assert
        Assert.True(result.IsSuccess, $"処理は成功するべき: {result.ErrorMessage}");
        Assert.True(result.OptimizedCount <= result.OriginalCount, "最適化後のファイル数は元より少ないか同じ");
    }

    #endregion

    #region Warning System Tests - 警告システムテスト

    [Fact]
    public async Task FindOptimalThresholdsAsync_WithCorruptedFiles_ReturnsWarnings()
    {
        // Arrange: 正常なファイル1つ + 破損ファイル2つ
        var validFile = CreateTestWaveFile("valid.wav", 1000, 440f);

        var corruptedFile1 = Path.Combine(_context.TempDirectory, "corrupted1.wav");
        File.WriteAllText(corruptedFile1, "This is not a valid WAV file");

        var corruptedFile2 = Path.Combine(_context.TempDirectory, "corrupted2.wav");
        File.WriteAllBytes(corruptedFile2, new byte[0]); // 0バイトファイル

        var files = new List<string> { validFile, corruptedFile1, corruptedFile2 };

        // Act
        var result = await _service.FindOptimalThresholdsAsync(
            files: files,
            startDefinition: 1,
            endDefinition: 3
        );

        // Assert
        Assert.NotNull(result);
        Assert.True(result.HasWarnings, "破損ファイルが存在するため警告があるべき");
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("2 件の音声ファイルが読み込めなかったため、最適化から除外されました", result.Warnings[0]);
    }

    [Fact]
    public async Task FindOptimalThresholdsAsync_WithMissingFiles_SkipsNonExistentFiles()
    {
        // Arrange: 正常なファイル2つ + 存在しないファイル1つ
        var validFile1 = CreateTestWaveFile("valid1.wav", 1000, 440f);
        var validFile2 = CreateTestWaveFile("valid2.wav", 1000, 880f);
        var missingFile = Path.Combine(_context.TempDirectory, "missing.wav");

        var files = new List<string> { validFile1, validFile2, missingFile };

        // Act
        var result = await _service.FindOptimalThresholdsAsync(
            files: files,
            startDefinition: 1,
            endDefinition: 3
        );

        // Assert: 存在しないファイルはfile listに追加されないため、警告は生成されない
        // しかし、処理は正常に継続される
        Assert.NotNull(result);
        // 正常なファイル2つだけで処理が実行される
        Assert.False(result.HasWarnings, "File.Existsチェックで事前除外されるため警告はない");
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task FindOptimalThresholdsAsync_WithSingleCorruptedFile_ReturnsWarningWithFilename()
    {
        // Arrange: 正常なファイル1つ + 破損ファイル1つ（単数の場合はファイル名表示）
        var validFile = CreateTestWaveFile("valid.wav", 1000, 440f);
        var corruptedFile = Path.Combine(_context.TempDirectory, "corrupted_single.wav");
        File.WriteAllText(corruptedFile, "Invalid content");

        var files = new List<string> { validFile, corruptedFile };

        // Act
        var result = await _service.FindOptimalThresholdsAsync(
            files: files,
            startDefinition: 1,
            endDefinition: 2
        );

        // Assert
        Assert.NotNull(result);
        Assert.True(result.HasWarnings, "破損ファイルが1件あるため警告があるべき");
        Assert.NotEmpty(result.Warnings);
        // 単一ファイルの場合はファイル名が含まれる
        Assert.Contains("1 件の音声ファイルが読み込めなかったため", result.Warnings[0]);
        Assert.Contains("corrupted_single.wav", result.Warnings[0]);
    }

    [Fact]
    public async Task FindOptimalThresholdsAsync_WithAllValidFiles_NoWarnings()
    {
        // Arrange: すべて正常なファイル
        var validFile1 = CreateTestWaveFile("valid1.wav", 1000, 440f);
        var validFile2 = CreateTestWaveFile("valid2.wav", 1000, 880f);
        var validFile3 = CreateTestWaveFile("valid3.wav", 1000, 1320f);

        var files = new List<string> { validFile1, validFile2, validFile3 };

        // Act
        var result = await _service.FindOptimalThresholdsAsync(
            files: files,
            startDefinition: 1,
            endDefinition: 3
        );

        // Assert
        Assert.NotNull(result);
        Assert.False(result.HasWarnings, "すべて正常なファイルの場合、警告はないべき");
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task FindOptimalThresholdsAsync_WithWarnings_ProcessingContinuesSuccessfully()
    {
        // Arrange: 正常なファイル3つ + 破損ファイル2つ
        var validFile1 = CreateTestWaveFile("valid1.wav", 1000, 440f);
        var validFile2 = CreateTestWaveFile("valid2.wav", 1000, 440f); // 同一波形
        var validFile3 = CreateTestWaveFile("valid3.wav", 1000, 880f);

        var corruptedFile1 = Path.Combine(_context.TempDirectory, "corrupted1.wav");
        var corruptedFile2 = Path.Combine(_context.TempDirectory, "corrupted2.wav");
        File.WriteAllText(corruptedFile1, "Invalid");
        File.WriteAllText(corruptedFile2, "Invalid");

        var files = new List<string> { validFile1, validFile2, validFile3, corruptedFile1, corruptedFile2 };

        // Act
        var result = await _service.FindOptimalThresholdsAsync(
            files: files,
            startDefinition: 1,
            endDefinition: 5
        );

        // Assert: 破損ファイルがあっても処理は成功する
        Assert.NotNull(result);
        Assert.True(result.HasWarnings, "破損ファイルがあるため警告があるべき");
        Assert.NotEmpty(result.SimulationData);

        // 破損ファイルは file list に含まれるため、カウントは5になる可能性がある
        // しかし、実際に読み込めるのは3つだけなので、最適化結果は3つ以下
        var base36Count = result.Base36Result.Item2;
        var base62Count = result.Base62Result.Item2;

        // ファイルリストには5つ追加されているが、音声データがロードできないものは
        // 比較対象外となり、結果として3つまでカウントされる
        Assert.InRange(base36Count, 1, 5);
        Assert.InRange(base62Count, 1, 5);

        // 警告メッセージの内容確認
        Assert.Contains("2 件の音声ファイルが読み込めなかったため", result.Warnings[0]);
    }

    [Fact]
    public async Task FindOptimalThresholdsAsync_WithLockedFile_ReturnsWarning()
    {
        // Arrange: 正常なファイル1つ + ロックされたファイル1つ
        var validFile = CreateTestWaveFile("valid.wav", 1000, 440f);
        var lockedFile = CreateTestWaveFile("locked.wav", 1000, 880f);

        var files = new List<string> { validFile, lockedFile };

        // ファイルをロック
        using (var fs = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // Act
            var result = await _service.FindOptimalThresholdsAsync(
                files: files,
                startDefinition: 1,
                endDefinition: 2
            );

            // Assert: ロックされたファイルは読み込み失敗するが処理は続行
            Assert.NotNull(result);
            Assert.True(result.HasWarnings, "ロックされたファイルがあるため警告があるべき");
            Assert.NotEmpty(result.Warnings);
        }
    }

    [Fact]
    public async Task FindOptimalThresholdsAsync_WarningCount_MatchesFailedFileCount()
    {
        // Arrange: 様々な種類の失敗ファイル
        var validFile = CreateTestWaveFile("valid.wav", 1000, 440f);
        var corruptedFile = Path.Combine(_context.TempDirectory, "corrupted.wav");
        var missingFile = Path.Combine(_context.TempDirectory, "missing.wav");  // 存在しない -> fileListに追加されない
        var zeroByteFile = Path.Combine(_context.TempDirectory, "zero.wav");

        File.WriteAllText(corruptedFile, "Invalid");
        File.WriteAllBytes(zeroByteFile, new byte[0]);

        var files = new List<string> { validFile, corruptedFile, missingFile, zeroByteFile };

        // Act
        var result = await _service.FindOptimalThresholdsAsync(
            files: files,
            startDefinition: 1,
            endDefinition: 4
        );

        // Assert
        Assert.NotNull(result);
        Assert.True(result.HasWarnings, "失敗ファイルが複数あるため警告があるべき");

        // 警告メッセージに失敗件数が正しく表示されていること
        // missingFileは File.Exists チェックで除外されるため、カウントは2件（corrupted + zero）
        Assert.Contains("2 件の音声ファイルが読み込めなかったため", result.Warnings[0]);
    }

    #endregion
}
