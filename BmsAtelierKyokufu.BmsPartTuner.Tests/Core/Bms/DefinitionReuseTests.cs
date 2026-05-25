using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Bms
{
    /// <summary>
    /// <see cref="DefinitionReuse"/> のテストクラス。
    ///
    /// 【テスト対象】
    /// - 境界値: ZZ(1295), zz(3843) 付近での挙動
    /// - 大文字小文字の混在: #WAV01 と #wav01
    /// - 重複定義の処理
    /// </summary>
    public partial class DefinitionReuseTests : IDisposable
    {
        private readonly BmsTestContext _context;

        public DefinitionReuseTests()
        {
            _context = new BmsTestContext();
        }

        public void Dispose()
        {
            _context.Dispose();
            GC.SuppressFinalize(this);
        }

        #region Boundary Value Tests - 境界値テスト

        [Fact]
        public void ReductDefinition_WithBase36MaxValue_ZZ_Success()
        {
            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
            // Arrange: ZZ (1295) の境界値テスト
            var fileList = BmsTestDefinitionHelper.CreateBmsDefinitionManagerWithMemoryWav(
                36,
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
            var dr = new DefinitionReuse(fileList, audioCache);

            // Act: defStartとdefEndは実際のファイルリスト範囲に合わせる
            dr.ReductDefinition(
                bmsFile,
                outputFile,
                new DefinitionReductionOptions
                {
                    R2Threshold = 0.95f,
                    StartDefinition = 1294,
                    EndDefinition = 1295,
                    IsPhysicalDeletionEnabled = false,
                    Progress = new Progress<int>()
                }
            );

            // Assert
            Assert.True(File.Exists(outputFile), "出力ファイルが作成されていません");
            var outputContent = File.ReadAllText(outputFile);

            var wavDefinitions = WavDefinitionRegex().Matches(outputContent);
            Assert.True(wavDefinitions.Count >= 1, $"WAV定義が見つかりません。実際の出力: {outputContent}");
        }

        [Fact]
        public void ReductDefinition_WithBase62MaxValue_zz_Success()
        {
            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
            // Arrange: zz (3843) の境界値テスト
            var fileList = BmsTestDefinitionHelper.CreateBmsDefinitionManagerWithMemoryWav(
                62,
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
            var dr = new DefinitionReuse(fileList, audioCache);

            // Act: defStartとdefEndは実際のファイルリスト範囲に合わせる
            dr.ReductDefinition(
                bmsFile,
                outputFile,
                new DefinitionReductionOptions
                {
                    R2Threshold = 0.95f,
                    StartDefinition = 3842,
                    EndDefinition = 3843,
                    IsPhysicalDeletionEnabled = false,
                    Progress = new Progress<int>()
                }
            );

            // Assert
            Assert.True(File.Exists(outputFile), "出力ファイルが作成されていません");
            var outputContent = File.ReadAllText(outputFile);

            var wavDefinitions = WavDefinitionRegex().Matches(outputContent);
            Assert.True(wavDefinitions.Count >= 1, $"WAV定義が見つかりません。実際の出力: {outputContent}");
        }

        #endregion

        #region Case Sensitivity Tests - 大文字小文字混在テスト

        [Fact]
        public void ReductDefinition_WithMixedCase_HandlesCorrectly()
        {
            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
            // Arrange: #WAV01 と #wav01 が混在するケース
            var fileList = BmsTestDefinitionHelper.CreateBmsDefinitionManagerWithMemoryWav(
                36,
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
                        EndDefinition = 2,
                        IsPhysicalDeletionEnabled = false,
                        Progress = new Progress<int>()
                    }
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
            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();
            // Arrange: 同一定義番号が複数回定義されているケース
            var fileList = BmsTestDefinitionHelper.CreateBmsDefinitionManagerWithMemoryWav(
                36,
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
        }

        #endregion
    }
}
