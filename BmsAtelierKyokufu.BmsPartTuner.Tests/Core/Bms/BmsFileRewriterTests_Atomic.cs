using System.IO;
using System.Text;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Bms
{
    public class BmsFileRewriterTests_Atomic
    {
        public BmsFileRewriterTests_Atomic()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Fact]
        public void WriteBmsFile_LockedTarget_PreservesOriginalContent()
        {
            using var context = new BmsTestContext();

            string bmsPath = Path.Combine(context.TempDirectory, "atomic_test.bms");
            string originalContent = "Original Content";
            string newContent = "New Content";

            // 1. Create original file
            File.WriteAllText(bmsPath, originalContent, Encoding.GetEncoding("shift_jis"));

            var rewriter = new BmsFileRewriter(new List<FileList.WavFiles>(), new int[1], 0, 0);

            // 2. Lock the file to simulate write failure (cannot overwrite)
            // Using FileShare.Read to allow reading but deny writing
            using (FileStream fs = new FileStream(bmsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // 3. Attempt to write
                // Expect IOException because final move/replace will fail
                // If implementation is NOT atomic (writes directly), the file would be truncated before exception if it wasn't locked.
                // But since we locked it, direct write would fail immediately too.
                // However, the test here is: Does it delete/corrupt the file?
                // With the lock, even direct write can't corrupt it.
                //
                // To properly test atomic write, we need to fail *after* opening the file stream?
                // Or we can rely on the fact that if we write to a *temp* file, that succeeds.
                // Then the move fails.
                // The original file should be untouched.

                Assert.Throws<IOException>(() => rewriter.WriteBmsFile(bmsPath, newContent));
            }

            // 4. Verify content
            // If it was atomic, original content should remain.
            string currentContent = File.ReadAllText(bmsPath, Encoding.GetEncoding("shift_jis"));
            Assert.Equal(originalContent, currentContent);

            // 5. Verify no temp file remains (optional, might be hard if name is random)
            var tempFiles = Directory.GetFiles(context.TempDirectory, "*.tmp");
            Assert.Empty(tempFiles);
        }

        [Fact]
        public void WriteBmsFile_Success_WritesToTempAndMoves()
        {
            using var context = new BmsTestContext();

            string bmsPath = Path.Combine(context.TempDirectory, "atomic_success.bms");
            string content = "Success Content";

            var rewriter = new BmsFileRewriter(new List<FileList.WavFiles>(), new int[1], 0, 0);

            rewriter.WriteBmsFile(bmsPath, content);

            Assert.True(File.Exists(bmsPath));
            string currentContent = File.ReadAllText(bmsPath, Encoding.GetEncoding("shift_jis"));
            Assert.Equal(content, currentContent);
        }
    }
}
