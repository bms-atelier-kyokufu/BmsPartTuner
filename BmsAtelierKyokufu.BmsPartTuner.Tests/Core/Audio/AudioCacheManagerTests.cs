using System.IO;
using System.Text;
using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Models;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Audio
{
    /// <summary>
    /// AudioCacheManager の動作検証テスト。
    /// 音声ファイルの読み込み・キャッシュ管理・リソース解放の仕様を確認します。
    /// </summary>
    public class AudioCacheManagerTests : IDisposable
    {
        private readonly string _tempDirectory;

        public AudioCacheManagerTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                try
                {
                    // 残存ファイルを削除
                    // ハンドルが解放されていない場合は例外が発生するが、リソースリークの検出に有用
                    Directory.Delete(_tempDirectory, true);
                }
                catch (IOException)
                {
                    // ファイルがロックされている場合は即座に削除できない
                    // リソース管理のテスト失敗を示唆することが多い
                }
            }
            GC.SuppressFinalize(this);
        }

        private string CreateDummyWav(string fileName, bool isValid = true)
        {
            string path = Path.Combine(_tempDirectory, fileName);
            if (isValid)
            {
                // テスト用の有効なWAVファイルを生成（PCM 44.1kHz mono 16bit, 1秒の無音）
                using FileStream fs = new(path, FileMode.Create);
                using BinaryWriter bw = new(fs);
                int sampleRate = 44100;
                int channels = 1;
                short bitsPerSample = 16;
                int dataSize = sampleRate * channels * (bitsPerSample / 8); // 1 second
                int fileSize = 36 + dataSize;

                // RIFFヘッダー
                bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(fileSize);
                bw.Write(Encoding.ASCII.GetBytes("WAVE"));

                // fmtチャンク
                bw.Write(Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16); // chunk size
                bw.Write((short)1); // PCM
                bw.Write((short)channels);
                bw.Write(sampleRate);
                bw.Write(sampleRate * channels * (bitsPerSample / 8)); // byte rate
                bw.Write((short)(channels * (bitsPerSample / 8))); // block align
                bw.Write(bitsPerSample);

                // dataチャンク
                bw.Write(Encoding.ASCII.GetBytes("data"));
                bw.Write(dataSize);
                bw.Write(new byte[dataSize]); // 無音
            }
            else
            {
                // WAVファイルを模した無効なテキストファイルを生成
                File.WriteAllText(path, "This is not a WAV file but has .wav extension.");
            }
            return path;
        }

        [Fact]
        public void PreloadAudioData_WithValidFile_LoadsData()
        {
            string path = CreateDummyWav("valid.wav");
            BmsAudioFile wavFile = new()
            {
                Name = path,
                FileSize = new FileInfo(path).Length
            };
            List<BmsAudioFile> list = [wavFile];

            var (failedFiles, cache) = AudioCacheManager.PreloadAudioData(list, null);

            Assert.Empty(failedFiles);
            Assert.True(cache.ContainsKey(wavFile.Name));
            Assert.Equal(44100, cache[wavFile.Name].SampleRate);

            // リソース解放確認
            cache[wavFile.Name].Dispose();

        }

        private void RunPreloadFailureTest(Func<string, IDisposable?> setupAction, string fileName)
        {
            string path = Path.Combine(_tempDirectory, fileName);
            using (setupAction?.Invoke(path))
            {
                BmsAudioFile wavFile = new() { Name = path, FileSize = File.Exists(path) ? new FileInfo(path).Length : 0 };
                List<BmsAudioFile> list = [wavFile];

                var (failedFiles, cache) = AudioCacheManager.PreloadAudioData(list, null);

                Assert.False(cache.ContainsKey(wavFile.Name));
                Assert.Single(failedFiles);
                Assert.Equal(path, failedFiles[0]);
            }
        }

        [Fact]
        public void PreloadAudioData_WithMissingFile_DoesNotCrash() =>
            RunPreloadFailureTest(_ => null, "missing.wav");

        [Fact]
        public void PreloadAudioData_WithCorruptFile_DoesNotCrash() =>
            RunPreloadFailureTest(path => { CreateDummyWav(Path.GetFileName(path), isValid: false); return null; }, "corrupt.wav");

        [Fact]
        public void PreloadAudioData_WithZeroByteFile_DoesNotCrash() =>
            RunPreloadFailureTest(path => { File.Create(path).Dispose(); return null; }, "empty.wav");

        [Fact]
        public void PreloadAudioData_WithLockedFile_DoesNotCrash() =>
            RunPreloadFailureTest(path =>
            {
                CreateDummyWav(Path.GetFileName(path));
                return new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }, "locked.wav");

        [Fact]
        public void PreloadAudioData_ResourceManagement_VerifyHandlesClosed()
        {
            string path = CreateDummyWav("resource_test.wav");
            BmsAudioFile wavFile = new() { Name = path, FileSize = new FileInfo(path).Length };
            List<BmsAudioFile> list = [wavFile];

            var (failedFiles, cache) = AudioCacheManager.PreloadAudioData(list, null);

            // ハンドル解放確認：書き込みモードでファイルを開けるか検証
            Assert.Empty(failedFiles);
            Assert.True(cache.ContainsKey(wavFile.Name));
            cache[wavFile.Name].Dispose();


            // ハンドルが解放されていれば書き込みモードで開ける
            try
            {
                using FileStream fs = new(path, FileMode.Open, FileAccess.Write, FileShare.None);
                Assert.True(fs.CanWrite);
            }
            catch (IOException)
            {
                Assert.Fail("ファイルハンドルが解放されていません。");
            }
        }
    }
}
