using System.Collections.ObjectModel;
using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Bms
{
    public partial class DefinitionReuseTests
    {
        #region Physical Deletion Tests - 物理削除テスト

        /// <summary>
        /// ReductDefinition において、条件 WithPhysicalDeletion の場合に DeletesUnusedFiles されることを検証します。
        /// </summary>
        [Fact]
        public void ReductDefinition_WithPhysicalDeletion_DeletesUnusedFiles()
        {
            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            // Arrange: 同一音声ファイルを2つ用意
            var identical1 = BmsTestWavHelper.CreateSineWavFile(Path.Combine(_context.TempDirectory, "identical1.wav"), 1000, 440.0);
            var identical2 = BmsTestWavHelper.CreateSineWavFile(Path.Combine(_context.TempDirectory, "identical2.wav"), 1000, 440.0);  // 同一波形
            var unique = BmsTestWavHelper.CreateSineWavFile(Path.Combine(_context.TempDirectory, "unique.wav"), 1000, 880.0);  // 異なる波形

            var fileList = new ObservableCollection<BmsAudioFile>
            {
                new() { Num = "01", NumInteger = 1, Name = identical1, FileSize = new FileInfo(identical1).Length },
                new() { Num = "02", NumInteger = 2, Name = identical2, FileSize = new FileInfo(identical2).Length },
                new() { Num = "03", NumInteger = 3, Name = unique, FileSize = new FileInfo(unique).Length }
            };

            var bmsFile = _context.CreateBuilder()
                .WithHeader("TITLE", "Physical Deletion Test")
                .WithWav("01", "identical1.wav", createFile: false)
                .WithWav("02", "identical2.wav", createFile: false)
                .WithWav("03", "unique.wav", createFile: false)
                .AddMainData(11, "010203")
                .Build("test_delete.bms");

            var outputFile = Path.Combine(_context.TempDirectory, "output_delete.bms");
            var dr = new DefinitionReuse(new ObservableCollection<BmsAudioFile>(fileList), audioCache);

            // Act: 物理削除有効で実行
            dr.ReductDefinition(
                bmsFile,
                outputFile,
                new DefinitionReductionOptions
                {
                    R2Threshold = 0.95f,
                    StartDefinition = 1,
                    EndDefinition = 3,
                    IsPhysicalDeletionEnabled = true,
                    Progress = new Progress<int>()
                }
            );

            // Assert: identical1とidentical2のどちらか1つが削除されていること
            var file1Exists = File.Exists(identical1);
            var file2Exists = File.Exists(identical2);

            Assert.True(file1Exists ^ file2Exists, "同一音声ファイルのどちらか1つだけが残っているべき");
            Assert.True(File.Exists(unique), "ユニークファイルは削除されないべき");
        }

        /// <summary>
        /// ReductDefinition において、条件 WithPhysicalDeletionDisabled の場合に KeepsAllFiles されることを検証します。
        /// </summary>
        [Fact]
        public void ReductDefinition_WithPhysicalDeletionDisabled_KeepsAllFiles()
        {
            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            // Arrange
            var file1 = BmsTestWavHelper.CreateSineWavFile(Path.Combine(_context.TempDirectory, "keep1.wav"), 1000, 440.0);
            var file2 = BmsTestWavHelper.CreateSineWavFile(Path.Combine(_context.TempDirectory, "keep2.wav"), 1000, 440.0);  // 同一波形
            var fileList = new ObservableCollection<BmsAudioFile>
            {
                new() { Num = "01", NumInteger = 1, Name = file1, FileSize = new FileInfo(file1).Length },
                new() { Num = "02", NumInteger = 2, Name = file2, FileSize = new FileInfo(file2).Length }
            };

            var bmsFile = _context.CreateBuilder()
                .WithHeader("TITLE", "No Deletion Test")
                .WithWav("01", "keep1.wav", createFile: false)
                .WithWav("02", "keep2.wav", createFile: false)
                .AddMainData(11, "0102")
                .Build("test_nodelete.bms");

            var outputFile = Path.Combine(_context.TempDirectory, "output_nodelete.bms");
            var dr = new DefinitionReuse(fileList, audioCache);

            // Act: 物理削除無効で実行
            dr.ReductDefinition(
                bmsFile,
                outputFile,
                new DefinitionReductionOptions
                {
                    R2Threshold = 0.95f,
                    StartDefinition = 1,
                    EndDefinition = 2,
                    IsPhysicalDeletionEnabled = false,
                    Progress = new Progress<int>()
                }
            );

            // Assert: 全ファイルが残っていること
            Assert.True(File.Exists(file1), "ファイル1は削除されないべき");
            Assert.True(File.Exists(file2), "ファイル2は削除されないべき");
        }

        /// <summary>
        /// GetUnusedFilePaths において、条件 AfterReduction の場合に ReturnsCorrectList されることを検証します。
        /// </summary>
        [Fact]
        public void GetUnusedFilePaths_AfterReduction_ReturnsCorrectList()
        {
            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            // Arrange
            var file1 = BmsTestWavHelper.CreateSineWavFile(Path.Combine(_context.TempDirectory, "used.wav"), 1000, 440.0);
            var file2 = BmsTestWavHelper.CreateSineWavFile(Path.Combine(_context.TempDirectory, "unused.wav"), 1000, 440.0);  // 同一波形
            var fileList = new ObservableCollection<BmsAudioFile>
            {
                new() { Num = "01", NumInteger = 1, Name = file1, FileSize = new FileInfo(file1).Length },
                new() { Num = "02", NumInteger = 2, Name = file2, FileSize = new FileInfo(file2).Length }
            };

            var bmsFile = _context.CreateBuilder()
                .WithHeader("TITLE", "Unused List Test")
                .WithWav("01", "used.wav", createFile: false)
                .WithWav("02", "unused.wav", createFile: false)
                .AddMainData(11, "0102")
                .Build("test_unused.bms");

            var outputFile = Path.Combine(_context.TempDirectory, "output_unused.bms");
            var dr = new DefinitionReuse(fileList, audioCache);

            // Act
            dr.ReductDefinition(
                bmsFile,
                outputFile,
                new DefinitionReductionOptions
                {
                    R2Threshold = 0.95f,
                    StartDefinition = 1,
                    EndDefinition = 2,
                    IsPhysicalDeletionEnabled = false,
                    Progress = new Progress<int>()
                }
            );

            var unusedFiles = dr.GetUnusedFilePaths();

            // Assert: 未使用ファイルリストに1つだけ含まれること
            Assert.Single(unusedFiles);
        }

        /// <summary>
        /// GetUnusedFilePaths において、条件 BeforeReduction の場合に ReturnsEmptyList されることを検証します。
        /// </summary>
        [Fact]
        public void GetUnusedFilePaths_BeforeReduction_ReturnsEmptyList()
        {
            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            // Arrange
            var fileList = BmsTestDefinitionHelper.CreateBmsDefinitionManagerWithPhysicalWav(_context.TempDirectory, 36, (1, "test.wav"));
            var dr = new DefinitionReuse(fileList, audioCache);

            // Act: ReductDefinitionを実行する前
            var unusedFiles = dr.GetUnusedFilePaths();

            // Assert: 空リストが返されること
            Assert.Empty(unusedFiles);
        }

        #endregion
    }
}
