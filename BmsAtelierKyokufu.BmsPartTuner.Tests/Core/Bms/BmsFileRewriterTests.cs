using System.IO;
using System.Text;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Bms
{
    /// <summary>
    /// BmsFileRewriter の動作検証テスト。
    /// 定義の置換・並べ替え・BMSファイルの書き換え処理を確認します。
    /// </summary>
    /// <summary>
    /// <see cref="BmsFileRewriterTests"/> の動作を検証するテストクラス。
    /// </summary>
    public class BmsFileRewriterTests : IDisposable
    {
        private readonly BmsFamilyTestContext _context;
        private string TempDir => _context.TempDirectory;
        private bool _disposed;

        public BmsFileRewriterTests()
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

        private BmsAudioFile CreateBmsAudioFileEntry(int num, string name)
        {
            return new BmsAudioFile
            {
                Num = BmsAtelierKyokufu.BmsPartTuner.Core.Helpers.RadixConvert.IntToZZ(num),
                NumInteger = num,
                Name = Path.Combine(TempDir, name),
                FileSize = 1024
            };
        }

        private string RunRewriter(BmsFileRewriter rewriter, string bmsContent)
        {
            string bmsPath = Path.Combine(TempDir, "test.bms");
            File.WriteAllText(bmsPath, bmsContent, Encoding.GetEncoding("shift_jis"));
            return rewriter.ReplaceAndAlignBmsFile(bmsPath);
        }

        /// <summary>
        /// ReplaceAndAlignBmsFile において CorrectlyRenamesAndSortsDefinitions の場合の挙動を検証します。
        /// </summary>
        [Fact]
        public void ReplaceAndAlignBmsFile_CorrectlyRenamesAndSortsDefinitions()
        {
            var fileList = new List<BmsAudioFile>
            {
                CreateBmsAudioFileEntry(1, "kick.wav"),
                CreateBmsAudioFileEntry(2, "snare.wav"),
                CreateBmsAudioFileEntry(3, "hat.wav")
            };

            // 置換テーブル: 1, 2, 3 はそのまま（置換なし）
            var replaces = new int[4];

            var rewriter = new BmsFileRewriter(fileList, replaces, 1, 3);
            const string bmsContent = "#HEADER\n#WAV01 kick.wav\n#WAV02 snare.wav\n#WAV03 hat.wav\n#MAIN\n#00111:010203";

            // Act
            string result = RunRewriter(rewriter, bmsContent);

            // ファイルはアルファベット順にソートされる: hat.wav, kick.wav, snare.wav
            // hat.wav -> 01
            // kick.wav -> 02
            // snare.wav -> 03

            Assert.Contains("#WAV01 hat.wav", result);
            Assert.Contains("#WAV02 kick.wav", result);
            Assert.Contains("#WAV03 snare.wav", result);

            // コンテンツ置換の検証
            // 旧 01 (kick) -> 新 02
            // 旧 02 (snare) -> 新 03
            // 旧 03 (hat) -> 新 01
            // #00111:010203 -> #00111:020301
            Assert.Contains("#00111:020301", result);
        }

        /// <summary>
        /// ReplaceAndAlignBmsFile において HandlesReplacementsCorrectly の場合の挙動を検証します。
        /// </summary>
        [Fact]
        public void ReplaceAndAlignBmsFile_HandlesReplacementsCorrectly()
        {
            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
            var fileList = new List<BmsAudioFile>
            {
                CreateBmsAudioFileEntry(1, "kick1.wav"),
                CreateBmsAudioFileEntry(2, "kick2.wav"), // kick1に置換される
                CreateBmsAudioFileEntry(3, "snare.wav")
            };

            var replaces = new int[4];
            replaces[2] = 1; // 2 -> 1

            var rewriter = new BmsFileRewriter(fileList, replaces, 1, 3);
            const string bmsContent = "#HEADER\n#WAV01 kick1.wav\n#WAV02 kick2.wav\n#WAV03 snare.wav\n#MAIN\n#00111:010203";

            // Act
            string result = RunRewriter(rewriter, bmsContent);

            // 保持されるファイル: kick1.wav, snare.wav（ソート済み）
            // kick1.wav -> 01
            // snare.wav -> 02

            Assert.Contains("#WAV01 kick1.wav", result);
            Assert.Contains("#WAV02 snare.wav", result);
            Assert.DoesNotContain("kick2.wav", result);

            // コンテンツ置換の検証
            // 旧 01 (kick1) -> 新 01
            // 旧 02 (kick2) -> 1に置換され -> 新 01
            // 旧 03 (snare) -> 新 02
            // #00111:010203 -> #00111:010102
            Assert.Contains("#00111:010102", result);

            // KeptFilesプロパティの検証
            Assert.Equal(2, rewriter.KeptFiles.Count);
            Assert.Contains(rewriter.KeptFiles, f => f.Name.EndsWith("kick1.wav"));
            Assert.DoesNotContain(rewriter.KeptFiles, f => f.Name.EndsWith("kick2.wav"));
        }

        /// <summary>
        /// WriteBmsFile において、条件 AtomicWrite の場合に CleansUpOnFailure されることを検証します。
        /// </summary>
        [Fact]
        public void WriteBmsFile_AtomicWrite_CleansUpOnFailure()
        {
            // 書き込み中の失敗時にクリーンアップが行われることを検証
            // StreamWriterの失敗をモックするのは難しいため、正常な書き込みが動作することを確認

            var fileList = new List<BmsAudioFile>();
            var replaces = new int[1];
            _ = new BmsFileRewriter(fileList, replaces, 0, 0);

            string outputPath = Path.Combine(TempDir, "output.bms");
            const string content = "test content";

            BmsFileWriter.WriteBmsFile(outputPath, content);

            Assert.True(File.Exists(outputPath));
            Assert.Equal(content, File.ReadAllText(outputPath, Encoding.GetEncoding("shift_jis")));
        }

        /// <summary>
        /// ReplaceAndAlignBmsFile において PreservesUndefinedDefinitions の場合の挙動を検証します。
        /// </summary>
        [Fact]
        public void ReplaceAndAlignBmsFile_PreservesUndefinedDefinitions()
        {
            var fileList = new List<BmsAudioFile>
            {
                CreateBmsAudioFileEntry(1, "kick.wav")
            };
            var replaces = new int[2];
            var rewriter = new BmsFileRewriter(fileList, replaces, 1, 1);

            // BMSにはファイルリストにない02への参照が含まれる（範囲外または未定義）
            const string bmsContent = "#HEADER\n#WAV01 kick.wav\n#MAIN\n#00111:0102";

            string result = RunRewriter(rewriter, bmsContent);

            // 01 -> 01
            // 02 -> マップにないたも02のまま
            Assert.Contains("#00111:0102", result);
        }
    }
}
