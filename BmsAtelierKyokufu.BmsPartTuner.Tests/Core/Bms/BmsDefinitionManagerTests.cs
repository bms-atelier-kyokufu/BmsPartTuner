using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Bms
{
    /// <summary>
    /// <see cref="BmsDefinitionManagerTests"/> の動作を検証するテストクラス。
    /// </summary>
    public class BmsDefinitionManagerTests : IDisposable
    {
        private readonly BmsFamilyTestContext _context;
        private string TmpDir => _context.TempDirectory;
        private bool _disposed;

        public BmsDefinitionManagerTests()
        {
            _context = new BmsFamilyTestContext();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _context.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Constructor において、条件 NullFilePath の場合に ThrowsArgumentNullException されることを検証します。
        /// </summary>
        [Fact]
        public void Constructor_NullFilePath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new BmsDefinitionManager(null!));
        }

        /// <summary>
        /// Constructor において、条件 ValidFilePath の場合に SetsProperties されることを検証します。
        /// </summary>
        [Fact]
        public void Constructor_ValidFilePath_SetsProperties()
        {
            var bmsPath = Path.Combine(TmpDir, "test.bms");
            var manager = new BmsDefinitionManager(bmsPath);

            Assert.Equal(TmpDir, manager.GetBmsDirectory());
            Assert.Empty(manager.GetFileList());
            Assert.Empty(manager.MissingFiles);
        }

        private (BmsDefinitionManager Manager, byte[] WavData, string ExpectedKickPath, string ExpectedSnarePath) SetupVirtualFiles(string kickName, string snareName, bool snareMissing, string bmsContent)
        {
            var wavData = BmsTestWavHelper.CreateSineWavBytes();
            VirtualAudioRegistry.AddFile(kickName, wavData);
            if (!snareMissing)
            {
                VirtualAudioRegistry.AddFile(snareName, wavData);
            }
            var bmsPath = Path.Combine(TmpDir, "test.bms");
            return (new BmsDefinitionManager(bmsPath, bmsContent), wavData, Path.Combine(TmpDir, kickName), Path.Combine(TmpDir, snareName));
        }

        private static string GetUniqueWavFileName(string part)
        {
            return $"{Guid.NewGuid():N}_{part}.wav";
        }

        /// <summary>
        /// CreateFileList において、条件 AllFilesVirtual の場合に ParsedSuccessfullyBase36 されることを検証します。
        /// </summary>
        [Fact]
        public void CreateFileList_AllFilesVirtual_ParsedSuccessfullyBase36()
        {
            var kickName = GetUniqueWavFileName("kick");
            var snareName = GetUniqueWavFileName("snare");
            var bmsContent = $"\n#WAV01 {kickName}\n#WAV02 {snareName}\n";

            var (Manager, WavData, ExpectedKickPath, ExpectedSnarePath) = SetupVirtualFiles(kickName, snareName, false, bmsContent);
            var fileList = Manager.CreateFileList();

            Assert.Equal(2, fileList.Count);
            Assert.Empty(Manager.MissingFiles);

            var file01 = fileList.FirstOrDefault(f => f.Num == "01");
            Assert.NotNull(file01);
            Assert.Equal(1, file01.NumInteger);
            Assert.Equal(ExpectedKickPath, file01.Name);
            Assert.Equal(WavData.Length, file01.FileSize);

            var file02 = fileList.FirstOrDefault(f => f.Num == "02");
            Assert.NotNull(file02);
            Assert.Equal(2, file02.NumInteger);
            Assert.Equal(ExpectedSnarePath, file02.Name);
            Assert.Equal(WavData.Length, file02.FileSize);
        }

        /// <summary>
        /// CreateFileList において、条件 AllFilesVirtual の場合に ParsedSuccessfullyBase62 されることを検証します。
        /// </summary>
        [Fact]
        public void CreateFileList_AllFilesVirtual_ParsedSuccessfullyBase62()
        {
            var kickName = GetUniqueWavFileName("kick");
            var snareName = GetUniqueWavFileName("snare");
            // Contains lower case (a1) which triggers Base62
            var bmsContent = $"\n#WAV01 {kickName}\n#WAVa1 {snareName}\n";

            var (Manager, WavData, ExpectedKickPath, ExpectedSnarePath) = SetupVirtualFiles(kickName, snareName, false, bmsContent);
            var fileList = Manager.CreateFileList();

            Assert.Equal(2, fileList.Count);
            Assert.Empty(Manager.MissingFiles);

            var fileA1 = fileList.FirstOrDefault(f => f.Num == "a1");
            Assert.NotNull(fileA1);
            Assert.Equal(ExpectedSnarePath, fileA1.Name);
            // Verify it has calculated the integer value based on Base62 radix (62)
            Assert.Equal(2233, fileA1.NumInteger);
        }

        /// <summary>
        /// CreateFileList において、条件 WithMissingFiles の場合に AddsToMissingFilesAndExcludesFromList されることを検証します。
        /// </summary>
        [Fact]
        public void CreateFileList_WithMissingFiles_AddsToMissingFilesAndExcludesFromList()
        {
            var kickName = GetUniqueWavFileName("kick");
            var snareName = GetUniqueWavFileName("snare");
            var bmsContent = $"\n#WAV01 {kickName}\n#WAV02 {snareName}\n";

            var (Manager, WavData, ExpectedKickPath, ExpectedSnarePath) = SetupVirtualFiles(kickName, snareName, true, bmsContent);
            var fileList = Manager.CreateFileList();

            Assert.Single(fileList);
            Assert.Single(Manager.MissingFiles);
            Assert.Equal(snareName, Manager.MissingFiles[0]);

            var file01 = fileList.FirstOrDefault(f => f.Num == "01");
            Assert.NotNull(file01);
            Assert.Equal(ExpectedKickPath, file01.Name);
        }

        /// <summary>
        /// CreateFileList において、条件 WithPhysicalFiles の場合に ResolvedCorrectly されることを検証します。
        /// </summary>
        [Fact]
        public void CreateFileList_WithPhysicalFiles_ResolvedCorrectly()
        {
            var bmsPath = Path.Combine(TmpDir, "test.bms");
            var physicalWavPath1 = Path.Combine(TmpDir, "physical1.wav");
            var physicalWavPath2 = Path.Combine(TmpDir, "physical2.wav");

            BmsTestWavHelper.CreateSineWavFile(physicalWavPath1, writeToDisk: true);
            BmsTestWavHelper.CreateSineWavFile(physicalWavPath2, writeToDisk: true);

            // Write absolute paths or relative paths in BMS content
            var bmsContent = $@"
#WAV01 physical1.wav
#WAV02 {physicalWavPath2.Replace("\\", "/")}
";
            var manager = new BmsDefinitionManager(bmsPath, bmsContent);
            var fileList = manager.CreateFileList();

            Assert.Equal(2, fileList.Count);
            Assert.Empty(manager.MissingFiles);

            var file01 = fileList.FirstOrDefault(f => f.Num == "01");
            Assert.NotNull(file01);
            Assert.Equal(physicalWavPath1, file01.Name);
            Assert.True(file01.FileSize > 0);

            var file02 = fileList.FirstOrDefault(f => f.Num == "02");
            Assert.NotNull(file02);
            // Standardize paths for comparison
            Assert.Equal(Path.GetFullPath(physicalWavPath2), Path.GetFullPath(file02.Name));
            Assert.True(file02.FileSize > 0);
        }
    }
}
