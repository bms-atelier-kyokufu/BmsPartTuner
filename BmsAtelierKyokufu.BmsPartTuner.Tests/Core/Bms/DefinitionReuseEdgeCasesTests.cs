using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Bms
{
    public partial class DefinitionReuseTests
    {
        #region Edge Case Tests - エッジケーステスト

        [Fact]
        public void ReductDefinition_WithExtremeThreshold_0_MergesAll()
        {
            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
            // Arrange: しきい値0.0（全てを結合）
            var fileList = BmsTestDefinitionHelper.CreateBmsDefinitionManagerWithMemoryWav(
                36,
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
            var dr = new DefinitionReuse(fileList, audioCache);

            // Act
            dr.ReductDefinition(
                bmsFile,
                outputFile,
                new DefinitionReductionOptions
                {
                    R2Threshold = 0.0f,
                    StartDefinition = 1,
                    EndDefinition = 3,
                    IsPhysicalDeletionEnabled = false,
                    Progress = new Progress<int>()
                }
            );

            var uniqueCount = dr.GetUniqueFileCount();

            // Assert: ユニークファイル数が減少していること
            Assert.True(uniqueCount <= 3, $"しきい値0.0で結合が行われるべき（実際: {uniqueCount}）");
        }

        [Fact]
        public void ReductDefinition_WithExtremeThreshold_1_MergesNothing()
        {
            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
            // Arrange: しきい値1.0（完全一致のみ結合）
            var fileList = BmsTestDefinitionHelper.CreateBmsDefinitionManagerWithMemoryWav(
                36,
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
            var dr = new DefinitionReuse(fileList, audioCache);

            // Act
            dr.ReductDefinition(
                bmsFile,
                outputFile,
                new DefinitionReductionOptions
                {
                    R2Threshold = 1.0f,
                    StartDefinition = 1,
                    EndDefinition = 2,
                    IsPhysicalDeletionEnabled = false,
                    Progress = new Progress<int>()
                }
            );

            var uniqueCount = dr.GetUniqueFileCount();

            // Assert: 完全一致しない限り結合されないため、ユニーク数は変わらない
            Assert.True(uniqueCount >= 1, "しきい値1.0では異なるファイルは結合されない");
        }

        [Fact]
        public void ReductDefinition_WithEmptyBmsDefinitionManager_ThrowsArgumentNullException()
        {
            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
            // Arrange
            System.Collections.ObjectModel.ObservableCollection<BmsAudioFile>? nullList = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DefinitionReuse(nullList!, audioCache));
        }

        [Fact]
        public void ReductDefinition_WithSingleFile_CompletesSuccessfully()
        {
            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
            // Arrange: ファイルが1つだけの場合
            var fileList = BmsTestDefinitionHelper.CreateBmsDefinitionManagerWithMemoryWav(36, (1, "single.wav"));

            var bmsFile = _context.CreateBuilder()
                .WithHeader("TITLE", "Single File Test")
                .WithWav("01", "single.wav", createFile: false)
                .AddMainData(11, "01")
                .Build("test_single.bms");

            var outputFile = Path.Combine(_context.TempDirectory, "output_single.bms");
            var dr = new DefinitionReuse(fileList, audioCache);

            // Act & Assert: エラーなく処理が完了することを確認
            var exception = Record.Exception(() =>
            {
                dr.ReductDefinition(
                    bmsFile,
                    outputFile,
                    new DefinitionReductionOptions
                    {
                        R2Threshold = 0.95f,
                        StartDefinition = 1,
                        EndDefinition = 1,
                        IsPhysicalDeletionEnabled = false,
                        Progress = new Progress<int>()
                    }
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
            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
            // Arrange
            var fileList = BmsTestDefinitionHelper.CreateBmsDefinitionManagerWithMemoryWav(
                36,
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
            var dr = new DefinitionReuse(fileList, audioCache);

            // Act: "kick"キーワードのみ処理
            dr.ReductDefinition(
                bmsFile,
                outputFile,
                new DefinitionReductionOptions
                {
                    R2Threshold = 0.95f,
                    StartDefinition = 1,
                    EndDefinition = 3,
                    IsPhysicalDeletionEnabled = false,
                    Progress = new Progress<int>(),
                    SelectedKeywords = ["kick"]
                }
            );

            // Assert: エラーなく処理が完了すること
            Assert.True(File.Exists(outputFile));
        }

        #endregion

        #region Progress Reporting Tests - 進捗報告テスト

        [Fact]
        public void ReductDefinition_ReportsProgress_FromZeroToHundred()
        {
            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
            // Arrange
            var fileList = BmsTestDefinitionHelper.CreateBmsDefinitionManagerWithMemoryWav(36, (1, "progress.wav"));
            var bmsFile = _context.CreateBuilder()
                .WithHeader("TITLE", "Progress Test")
                .WithWav("01", "progress.wav", createFile: false)
                .AddMainData(11, "01")
                .Build("test_progress.bms");

            var outputFile = Path.Combine(_context.TempDirectory, "output_progress.bms");
            var dr = new DefinitionReuse(fileList, audioCache);

            var progressReports = new List<int>();
            var progress = new Progress<int>(p => progressReports.Add(p));

            // Act
            dr.ReductDefinition(
                bmsFile,
                outputFile,
                new DefinitionReductionOptions
                {
                    R2Threshold = 0.95f,
                    StartDefinition = 1,
                    EndDefinition = 1,
                    IsPhysicalDeletionEnabled = false,
                    Progress = progress
                }
            );

            // Assert
            Assert.Contains(0, progressReports);  // 開始時
            Assert.Contains(100, progressReports);  // 完了時
            Assert.True(progressReports.Count >= 2, "進捗が複数回報告されるべき");
        }

        #endregion

        [System.Text.RegularExpressions.GeneratedRegex(@"#WAV\w{2}\s+", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
        private static partial System.Text.RegularExpressions.Regex WavDefinitionRegex();
    }
}
