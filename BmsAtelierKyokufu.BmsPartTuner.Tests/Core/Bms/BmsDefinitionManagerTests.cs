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
        private readonly string _tempDir;
        private bool _disposed;

        public BmsDefinitionManagerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "BmsDefinitionManagerTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (_disposed) return;
            VirtualAudioRegistry.Clear();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
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
            var bmsPath = Path.Combine(_tempDir, "test.bms");
            var manager = new BmsDefinitionManager(bmsPath);

            Assert.Equal(_tempDir, manager.GetBmsDirectory());
            Assert.Empty(manager.GetFileList());
            Assert.Empty(manager.MissingFiles);
        }

        /// <summary>
        /// CreateFileList において、条件 AllFilesVirtual の場合に ParsedSuccessfullyBase36 されることを検証します。
        /// </summary>
        [Fact]
        public void CreateFileList_AllFilesVirtual_ParsedSuccessfullyBase36()
        {
            var kickName = Guid.NewGuid().ToString("N") + "_kick.wav";
            var snareName = Guid.NewGuid().ToString("N") + "_snare.wav";

            // Prepare virtual files
            var wavData = BmsTestWavHelper.CreateSineWavBytes();
            VirtualAudioRegistry.AddFile(kickName, wavData);
            VirtualAudioRegistry.AddFile(snareName, wavData);

            var bmsPath = Path.Combine(_tempDir, "test.bms");
            var bmsContent = $@"
#WAV01 {kickName}
#WAV02 {snareName}
";
            var manager = new BmsDefinitionManager(bmsPath, bmsContent);
            var fileList = manager.CreateFileList();

            Assert.Equal(2, fileList.Count);
            Assert.Empty(manager.MissingFiles);

            var expectedKickPath = Path.Combine(_tempDir, kickName);
            var expectedSnarePath = Path.Combine(_tempDir, snareName);

            var file01 = fileList.FirstOrDefault(f => f.Num == "01");
            Assert.NotNull(file01);
            Assert.Equal(1, file01.NumInteger);
            Assert.Equal(expectedKickPath, file01.Name);
            Assert.Equal(wavData.Length, file01.FileSize);

            var file02 = fileList.FirstOrDefault(f => f.Num == "02");
            Assert.NotNull(file02);
            Assert.Equal(2, file02.NumInteger);
            Assert.Equal(expectedSnarePath, file02.Name);
            Assert.Equal(wavData.Length, file02.FileSize);
        }

        /// <summary>
        /// CreateFileList において、条件 AllFilesVirtual の場合に ParsedSuccessfullyBase62 されることを検証します。
        /// </summary>
        [Fact]
        public void CreateFileList_AllFilesVirtual_ParsedSuccessfullyBase62()
        {
            var kickName = Guid.NewGuid().ToString("N") + "_kick.wav";
            var snareName = Guid.NewGuid().ToString("N") + "_snare.wav";

            // Case-sensitive check
            var wavData = BmsTestWavHelper.CreateSineWavBytes();
            VirtualAudioRegistry.AddFile(kickName, wavData);
            VirtualAudioRegistry.AddFile(snareName, wavData);

            var bmsPath = Path.Combine(_tempDir, "test.bms");
            // Contains lower case (a1) which triggers Base62
            var bmsContent = $@"
#WAV01 {kickName}
#WAVa1 {snareName}
";
            var manager = new BmsDefinitionManager(bmsPath, bmsContent);
            var fileList = manager.CreateFileList();

            Assert.Equal(2, fileList.Count);
            Assert.Empty(manager.MissingFiles);

            var expectedSnarePath = Path.Combine(_tempDir, snareName);

            var fileA1 = fileList.FirstOrDefault(f => f.Num == "a1");
            Assert.NotNull(fileA1);
            Assert.Equal(expectedSnarePath, fileA1.Name);
            // Verify it has calculated the integer value based on Base62 radix (62)
            Assert.Equal(2233, fileA1.NumInteger);
        }

        /// <summary>
        /// CreateFileList において、条件 WithMissingFiles の場合に AddsToMissingFilesAndExcludesFromList されることを検証します。
        /// </summary>
        [Fact]
        public void CreateFileList_WithMissingFiles_AddsToMissingFilesAndExcludesFromList()
        {
            var kickName = Guid.NewGuid().ToString("N") + "_kick.wav";
            var snareName = Guid.NewGuid().ToString("N") + "_snare.wav";

            var wavData = BmsTestWavHelper.CreateSineWavBytes();
            VirtualAudioRegistry.AddFile(kickName, wavData);
            // snare.wav is missing

            var bmsPath = Path.Combine(_tempDir, "test.bms");
            var bmsContent = $@"
#WAV01 {kickName}
#WAV02 {snareName}
";
            var manager = new BmsDefinitionManager(bmsPath, bmsContent);
            var fileList = manager.CreateFileList();

            Assert.Single(fileList);
            Assert.Single(manager.MissingFiles);
            Assert.Equal(snareName, manager.MissingFiles[0]);

            var expectedKickPath = Path.Combine(_tempDir, kickName);
            var file01 = fileList.FirstOrDefault(f => f.Num == "01");
            Assert.NotNull(file01);
            Assert.Equal(expectedKickPath, file01.Name);
        }

        /// <summary>
        /// CreateFileList において、条件 WithPhysicalFiles の場合に ResolvedCorrectly されることを検証します。
        /// </summary>
        [Fact]
        public void CreateFileList_WithPhysicalFiles_ResolvedCorrectly()
        {
            var bmsPath = Path.Combine(_tempDir, "test.bms");
            var physicalWavPath1 = Path.Combine(_tempDir, "physical1.wav");
            var physicalWavPath2 = Path.Combine(_tempDir, "physical2.wav");

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
