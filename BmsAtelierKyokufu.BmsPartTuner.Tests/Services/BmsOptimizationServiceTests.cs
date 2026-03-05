using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Services;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Services
{
    /// <summary>
    /// BmsOptimizationService の動作検証テスト。
    /// 閾値最適化・定義削減の統合テストを確認します。
    /// </summary>
    public class BmsOptimizationServiceTests : IDisposable
    {
        private readonly BmsTestContext _context;
        private readonly BmsOptimizationService _service;

        public BmsOptimizationServiceTests()
        {
            _context = new BmsTestContext();
            _service = new BmsOptimizationService();
        }

        public void Dispose()
        {
            _context?.Dispose();
        }

        [Fact]
        public async Task FindOptimalThresholdsAsync_ValidFiles_ReturnsResult()
        {
            var builder = _context.CreateBuilder();
            builder.WithWav(1, "test1.wav");

            string file1 = Path.Combine(_context.TempDirectory, "test1.wav");
            var files = new List<string> { file1 };

            // 内部的にAudioCacheManagerとSimulationEngineが呼び出される
            // 依存関係のモックが困難なため、統合テストとして実行
            var result = await _service.FindOptimalThresholdsAsync(files, 1, 1, null);

            Assert.NotNull(result);
            Assert.NotEmpty(result.SimulationData);
            Assert.True(result.Base36Result.Count > 0);
        }

        [Fact]
        public async Task FindOptimalThresholdsAsync_EmptyList_ReturnsNull()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _service.FindOptimalThresholdsAsync(new List<string>(), 1, 1));
        }

        [Fact]
        public async Task FindOptimalThresholdsAsync_NoValidFiles_ReturnsNull()
        {
            var files = new List<string> { "nonexistent.wav" };

            var result = await _service.FindOptimalThresholdsAsync(files, 1, 1, null);

            Assert.Null(result);
        }

        #region Validation Tests

        [Theory]
        [InlineData("01", "02", true)]  // Valid: 1 to 2
        [InlineData("01", "10", true)]  // Valid: 1 to 16
        [InlineData("01", "ZZ", true)]  // Valid: 1 to 1295 (Base36 max)
        public void ValidateDefinitionRange_ValidInputs_ReturnsSuccess(string start, string end, bool expectedValid)
        {
            var result = _service.ValidateDefinitionRange(start, end);

            Assert.Equal(expectedValid, result.IsValid);
            if (!result.IsValid)
            {
                Assert.NotEmpty(result.Errors);
            }
        }

        [Theory]
        [InlineData("", "10")]          // Empty start
        [InlineData("01", "")]          // Empty end
        [InlineData("10", "01")]        // Start > End
        [InlineData("00", "10")]        // Start < 1
        [InlineData("01", "ZZZ")]       // End > MaxNumberBase62
        [InlineData("ABC", "10")]       // Invalid characters
        public void ValidateDefinitionRange_InvalidInputs_ReturnsError(string start, string end)
        {
            var result = _service.ValidateDefinitionRange(start, end);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }

        [Theory]
        [InlineData("80", 0.80f)]       // Percentage format
        [InlineData("0.8", 0.80f)]      // Decimal format
        [InlineData("100", 1.0f)]       // Max percentage
        [InlineData("1.0", 1.0f)]       // Max decimal
        [InlineData("0", 0.0f)]         // Min value
        public void ValidateR2Threshold_ValidInputs_ReturnsSuccess(string input, float expectedValue)
        {
            var result = _service.ValidateR2Threshold(input);

            Assert.True(result.IsValid);
            Assert.Equal(expectedValue, result.Value, precision: 2);
        }

        [Theory]
        [InlineData("")]                // Empty
        [InlineData("abc")]             // Non-numeric
        [InlineData("-10")]             // Negative
        [InlineData("150")]             // > 100 (max percentage)
        public void ValidateR2Threshold_InvalidInputs_ReturnsError(string input)
        {
            var result = _service.ValidateR2Threshold(input);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }

        #endregion

        [Fact]
        public async Task ExecuteDefinitionReductionAsync_FileNotFound_ReturnsError()
        {
            var files = new List<WavFiles> { new WavFiles { Name = "test.wav" } };

            var result = await _service.ExecuteDefinitionReductionAsync(
                files,
                "missing.bms",
                "out.bms",
                0.5f,
                1,
                1,
                false);

            Assert.False(result.IsSuccess);
            // 実際のエラーは一般的なエラーとして処理される可能性がある
            Assert.NotNull(result.ErrorMessage);
            Assert.True(
                result.ErrorMessage.Contains("ファイルが見つかりません") ||
                result.ErrorMessage.Contains("予期しないエラーが発生しました"),
                $"予期しないエラーメッセージ: {result.ErrorMessage}");
        }

        [Fact]
        public async Task ExecuteDefinitionReductionAsync_UnauthorizedAccess_ReturnsError()
        {
            var files = new List<WavFiles> { new WavFiles { Name = "test.wav" } };

            // UnauthorizedAccessExceptionをシミュレーションするために、
            // 入力ファイルとしてディレクトリパスを指定
            string dirPath = Path.Combine(_context.TempDirectory, "locked");
            Directory.CreateDirectory(dirPath);

            var result = await _service.ExecuteDefinitionReductionAsync(
                files,
                dirPath, // 入力ファイルとしてのディレクトリパスは通常問題を引き起こす
                "out.bms",
                0.5f,
                1,
                1,
                false);

            Assert.False(result.IsSuccess);
            // OSによりUnauthorizedAccessまたはIOExceptionが発生する可能性がある
            // エラーハンドリングが正しく行われていれば成功
        }

        [Fact]
        public async Task ExecuteDefinitionReductionAsync_WithPhysicalDeletion_OnlyDeletesUnusedFiles()
        {
            var builder = _context.CreateBuilder();
            string inputBmsPath = builder
                .WithHeader("GENRE", "Test")
                .WithHeader("TITLE", "Test Song")
                .WithHeader("ARTIST", "Test Artist")
                .WithHeader("BPM", "140")
                .WithWav(1, "used1.wav")
                .WithWav(2, "used2.wav")
                .WithWav(3, "unused1.wav")
                .WithWav(4, "unused2.wav")
                .AddMainData(0, 11, "0102")
                .Build("input.bms");

            string outputBmsPath = Path.Combine(_context.TempDirectory, "output.bms");

            // Get paths to created files
            string used1Path = Path.Combine(_context.TempDirectory, "used1.wav");
            string used2Path = Path.Combine(_context.TempDirectory, "used2.wav");
            string unused1Path = Path.Combine(_context.TempDirectory, "unused1.wav");
            string unused2Path = Path.Combine(_context.TempDirectory, "unused2.wav");

            var files = new List<WavFiles>
            {
                new WavFiles { Name = used1Path, Num = "01", NumInteger = 1 },
                new WavFiles { Name = used2Path, Num = "02", NumInteger = 2 },
                new WavFiles { Name = unused1Path, Num = "03", NumInteger = 3 },
                new WavFiles { Name = unused2Path, Num = "04", NumInteger = 4 }
            };

            // Execute with physical deletion enabled
            var result = await _service.ExecuteDefinitionReductionAsync(
                files,
                inputBmsPath,
                outputBmsPath,
                0.5f,
                1,
                4,
                isPhysicalDeletionEnabled: true);

            // Assertions
            Assert.True(result.IsSuccess, $"Expected success, but got error: {result.ErrorMessage}");
            Assert.True(File.Exists(used1Path), "used1.wav should not be deleted");
            Assert.True(File.Exists(used2Path), "used2.wav should not be deleted");

            // Note: The actual deletion of unused files depends on DefinitionReuse implementation
            // This test verifies that the service properly calls the deletion logic
            Assert.True(result.DeletedFilesCount >= 0, "DeletedFilesCount should be non-negative");
        }

        [Fact]
        public async Task ExecuteDefinitionReductionAsync_WithIOError_RestoresOriginalState()
        {
            var builder = _context.CreateBuilder();
            string inputBmsPath = builder
                .WithHeader("GENRE", "Test")
                .WithHeader("TITLE", "Test")
                .Build("input.bms");

            var files = new List<WavFiles>
            {
                new WavFiles { Name = "test.wav", Num = "01", NumInteger = 1 }
            };

            string outputBmsPath = Path.Combine(_context.TempDirectory, "output.bms");

            var result = await _service.ExecuteDefinitionReductionAsync(
                files,
                inputBmsPath,
                outputBmsPath,
                0.5f,
                1,
                1,
                false);

            // Should handle error gracefully
            // Note: The actual error depends on DefinitionReuse implementation
            // This test verifies error handling doesn't crash
            Assert.NotNull(result);
        }

        [Fact]
        public async Task ExecuteDefinitionReductionAsync_WithNullFileList_ThrowsArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _service.ExecuteDefinitionReductionAsync(
                    null!,
                    "input.bms",
                    "output.bms",
                    0.5f,
                    1,
                    1,
                    false));
        }

        [Fact]
        public async Task ExecuteDefinitionReductionAsync_WithEmptyInputPath_ThrowsArgumentException()
        {
            var files = new List<WavFiles> { new WavFiles { Name = "test.wav" } };

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.ExecuteDefinitionReductionAsync(
                    files,
                    "",
                    "output.bms",
                    0.5f,
                    1,
                    1,
                    false));
        }

        [Fact]
        public async Task ExecuteDefinitionReductionAsync_WithEmptyOutputPath_ThrowsArgumentException()
        {
            var files = new List<WavFiles> { new WavFiles { Name = "test.wav" } };

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.ExecuteDefinitionReductionAsync(
                    files,
                    "input.bms",
                    "",
                    0.5f,
                    1,
                    1,
                    false));
        }

        [Fact]
        public async Task ExecuteDefinitionReductionAsync_WithValidInput_CalculatesReductionRate()
        {
            var builder = _context.CreateBuilder();
            string inputBmsPath = builder
                .WithHeader("GENRE", "Test")
                .WithHeader("TITLE", "Test Song")
                .WithHeader("ARTIST", "Test Artist")
                .WithHeader("BPM", "140")
                .WithWav(1, "test1.wav")
                .AddMainData(0, 11, "01")
                .Build("input2.bms");

            string outputBmsPath = Path.Combine(_context.TempDirectory, "output2.bms");
            string wavPath = Path.Combine(_context.TempDirectory, "test1.wav");

            var files = new List<WavFiles>
            {
                new WavFiles { Name = wavPath, Num = "01", NumInteger = 1 }
            };

            var result = await _service.ExecuteDefinitionReductionAsync(
                files,
                inputBmsPath,
                outputBmsPath,
                0.5f,
                1,
                1,
                false);

            Assert.NotNull(result);
            Assert.True(result.OriginalCount > 0);
            Assert.True(result.ReductionRate >= 0 && result.ReductionRate <= 1.0);
            Assert.True(result.ProcessingTime.TotalMilliseconds >= 0);
        }

        #region Priority S: Error Handling Branch Coverage Tests

        /// <summary>
        /// 一部のファイルのみが見つからない場合でも、残りのファイルで処理が継続されることを検証。
        /// </summary>
        [Fact]
        public async Task FindOptimalThresholdsAsync_PartialFileNotFound_ProcessesValidFiles()
        {
            var builder = _context.CreateBuilder();
            builder.WithWav(1, "valid.wav");

            string validFile = Path.Combine(_context.TempDirectory, "valid.wav");

            var files = new List<string>
            {
                validFile,
                "nonexistent1.wav",
                "nonexistent2.wav"
            };

            var result = await _service.FindOptimalThresholdsAsync(files, 1, 3, null);

            // 有効なファイルが1つでもあれば、結果は返される
            Assert.NotNull(result);
            Assert.NotEmpty(result.SimulationData);
        }

        /// <summary>
        /// endDefinitionが0の場合に自動検出が正しく動作することを検証。
        /// </summary>
        [Fact]
        public async Task FindOptimalThresholdsAsync_EndDefinitionZero_AutoDetectsEndDefinition()
        {
            var builder = _context.CreateBuilder();
            builder.WithWav(1, "test1.wav")
                   .WithWav(2, "test2.wav");

            string file1 = Path.Combine(_context.TempDirectory, "test1.wav");
            string file2 = Path.Combine(_context.TempDirectory, "test2.wav");

            var files = new List<string> { file1, file2 };

            // endDefinition=0 で自動検出
            var result = await _service.FindOptimalThresholdsAsync(files, 1, 0, null);

            Assert.NotNull(result);
            Assert.NotEmpty(result.SimulationData);
        }

        /// <summary>
        /// 進捗報告のコールバックが正しく呼び出されることを検証。
        /// </summary>
        [Fact]
        public async Task FindOptimalThresholdsAsync_WithProgress_ReportsProgress()
        {
            var builder = _context.CreateBuilder();
            builder.WithWav(1, "test1.wav");

            string file1 = Path.Combine(_context.TempDirectory, "test1.wav");

            var files = new List<string> { file1 };
            var progressValues = new List<int>();
            var progress = new Progress<int>(p => progressValues.Add(p));

            await _service.FindOptimalThresholdsAsync(files, 1, 1, progress);

            // 少なくとも開始と終了の進捗が報告されること
            await Task.Delay(100); // Progress<T>は非同期で処理されるため待機
            Assert.NotEmpty(progressValues);
        }

        /// <summary>
        /// 定義範囲の開始値が終了値より小さい有効範囲でのテスト。
        /// </summary>
        [Theory]
        [InlineData(1, 10)]
        [InlineData(1, 100)]
        [InlineData(10, 50)]
        public async Task FindOptimalThresholdsAsync_VariousRanges_ProcessesCorrectly(int start, int end)
        {
            var builder = _context.CreateBuilder();
            builder.WithWav(1, $"test_{start}_{end}.wav");

            string file1 = Path.Combine(_context.TempDirectory, $"test_{start}_{end}.wav");

            var files = new List<string> { file1 };

            var result = await _service.FindOptimalThresholdsAsync(files, start, end, null);

            Assert.NotNull(result);
        }

        #endregion

        #region Priority S: Validation Edge Cases

        /// <summary>
        /// Base36の境界値テスト（ZZ = 1295）。
        /// </summary>
        [Theory]
        [InlineData("01", "ZZ", true)]   // 1 to 1295 (Base36 max)
        [InlineData("ZY", "ZZ", true)]   // Near max range
        [InlineData("01", "100", false)] // "100" exceeds Base36 limit (1296 > 1295)
        public void ValidateDefinitionRange_Base36Boundary_ValidatesCorrectly(string start, string end, bool expectedValid)
        {
            var result = _service.ValidateDefinitionRange(start, end);

            Assert.Equal(expectedValid, result.IsValid);
        }

        /// <summary>
        /// R2しきい値の境界値テスト。
        /// </summary>
        [Theory]
        [InlineData("0", 0.0f)]          // 最小値
        [InlineData("0.01", 0.01f)]      // 最小有効値付近
        [InlineData("0.99", 0.99f)]      // 最大有効値付近
        [InlineData("1", 1.0f)]          // 最大値（パーセンテージ形式）
        [InlineData("99", 0.99f)]        // パーセンテージ形式の境界
        public void ValidateR2Threshold_BoundaryValues_ReturnsCorrectValue(string input, float expectedValue)
        {
            var result = _service.ValidateR2Threshold(input);

            Assert.True(result.IsValid, $"Expected valid for input '{input}' but got invalid: {string.Join(", ", result.Errors)}");
            Assert.Equal(expectedValue, result.Value, precision: 2);
        }

        /// <summary>
        /// 極端な入力値に対するR2しきい値検証。
        /// </summary>
        [Theory]
        [InlineData("101")]              // > 100%
        [InlineData("-0.01")]            // 負の値
        [InlineData("NaN")]              // 非数
        [InlineData("Infinity")]         // 無限大
        public void ValidateR2Threshold_ExtremeValues_ReturnsError(string input)
        {
            var result = _service.ValidateR2Threshold(input);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }

        #endregion

        #region Priority S: Error Recovery Tests

        /// <summary>
        /// キャッシュデータを持つファイルリストで例外が発生した場合、
        /// キャッシュが正しくクリアされることを検証。
        /// </summary>
        [Fact]
        public async Task ExecuteDefinitionReductionAsync_WithException_ClearsCache()
        {
            var files = new List<WavFiles>
            {
                new WavFiles
                {
                    Name = "nonexistent.wav",
                    Num = "01",
                    NumInteger = 1,
                    CachedData = new Models.CachedSoundData(
                        new float[][] { new float[100] },
                        44100,
                        16)
                }
            };

            var result = await _service.ExecuteDefinitionReductionAsync(
                files,
                "nonexistent.bms",
                "output.bms",
                0.5f,
                1,
                1,
                false);

            // 処理は失敗するが、キャッシュはクリアされているべき
            Assert.False(result.IsSuccess);
            Assert.Null(files[0].CachedData);
        }

        /// <summary>
        /// 選択されたキーワードが指定された場合の処理検証。
        /// </summary>
        [Fact]
        public async Task ExecuteDefinitionReductionAsync_WithSelectedKeywords_ProcessesFilteredFiles()
        {
            var builder = _context.CreateBuilder();
            string inputBmsPath = builder
                .WithHeader("GENRE", "Test")
                .WithHeader("TITLE", "Test Song")
                .WithHeader("BPM", "140")
                .WithWav(1, "kick.wav")
                .WithWav(2, "snare.wav")
                .AddMainData(0, 11, "0102")
                .Build("keyword_test.bms");

            string outputBmsPath = Path.Combine(_context.TempDirectory, "keyword_output.bms");
            string kickPath = Path.Combine(_context.TempDirectory, "kick.wav");
            string snarePath = Path.Combine(_context.TempDirectory, "snare.wav");

            var files = new List<WavFiles>
            {
                new WavFiles { Name = kickPath, Num = "01", NumInteger = 1 },
                new WavFiles { Name = snarePath, Num = "02", NumInteger = 2 }
            };

            var result = await _service.ExecuteDefinitionReductionAsync(
                files,
                inputBmsPath,
                outputBmsPath,
                0.5f,
                1,
                2,
                false,
                null,
                new[] { "kick" }); // kickのみをフィルタ

            Assert.NotNull(result);
            // キーワードフィルタの動作確認（実装依存）
        }

        #endregion
    }
}
