using System.IO;
using System.Text;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Bms
{
    /// <summary>
    /// <see cref="BmsFileRewriterTests_Atomic"/> の動作を検証するテストクラス。
    /// </summary>
    public class BmsFileRewriterTests_Atomic
    {
        public BmsFileRewriterTests_Atomic()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// WriteBmsFile において、条件 LockedTarget の場合に PreservesOriginalContent されることを検証します。
        /// </summary>
        [Fact]
        public void WriteBmsFile_LockedTarget_PreservesOriginalContent()
        {
            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            using var context = new BmsFamilyTestContext();

            string bmsPath = Path.Combine(context.TempDirectory, "atomic_test.bms");
            const string originalContent = "Original Content";
            const string newContent = "New Content";

            // 1. Create original file
            File.WriteAllText(bmsPath, originalContent, Encoding.GetEncoding("shift_jis"));

            var rewriter = new BmsFileRewriter([], new int[1], 0, 0);

            // 2. Lock the file to simulate write failure (cannot overwrite)
            // Using FileShare.Read to allow reading but deny writing
            using (FileStream fs = new(bmsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // 3. Attempt to write

                Assert.Throws<IOException>(() => BmsFileWriter.WriteBmsFile(bmsPath, newContent));
            }

            // 4. Verify content
            // If it was atomic, original content should remain.
            string currentContent = File.ReadAllText(bmsPath, Encoding.GetEncoding("shift_jis"));
            Assert.Equal(originalContent, currentContent);

            // 5. Verify no temp file remains (optional, might be hard if name is random)
            var tempFiles = Directory.GetFiles(context.TempDirectory, "*.tmp");
            Assert.Empty(tempFiles);
        }

        /// <summary>
        /// WriteBmsFile において、条件 Success の場合に WritesToTempAndMoves されることを検証します。
        /// </summary>
        [Fact]
        public void WriteBmsFile_Success_WritesToTempAndMoves()
        {
            using var context = new BmsFamilyTestContext();

            string bmsPath = Path.Combine(context.TempDirectory, "atomic_success.bms");
            const string content = "Success Content";

            var rewriter = new BmsFileRewriter([], new int[1], 0, 0);

            BmsFileWriter.WriteBmsFile(bmsPath, content);

            Assert.True(File.Exists(bmsPath));
            string currentContent = File.ReadAllText(bmsPath, Encoding.GetEncoding("shift_jis"));
            Assert.Equal(content, currentContent);
        }
    }
}
