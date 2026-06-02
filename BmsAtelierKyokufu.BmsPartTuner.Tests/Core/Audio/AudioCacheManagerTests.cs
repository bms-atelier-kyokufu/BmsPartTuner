using System.IO;
using System.Text;
using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Audio
{
    /// <summary>
    /// AudioCacheManager の動作検証テスト。
    /// 音声ファイルの読み込み・キャッシュ管理・リソース解放の仕様を確認します。
    /// </summary>
    public class AudioCacheManagerTests : IDisposable
    {
        private readonly string _tempDirectory;
        private bool _disposed;

        public AudioCacheManagerTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            if (_disposed) return;
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
            AudioRegistry.Instance.Clear();
            _disposed = true;
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
                const int sampleRate = 44100;
                const int channels = 1;
                const short bitsPerSample = 16;
                const int dataSize = sampleRate * channels * (bitsPerSample / 8); // 1 second
                const int fileSize = 36 + dataSize;

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

        [Fact]
        public void PointerSoundData_Dispose_NullsOutReferences_AndThrowsObjectDisposedException()
        {
            var samples = new float[][] { [0.5f], [0.5f] };
            var prefixSum = new double[][] { [0.0, 0.5], [0.0, 0.5] };
            var prefixSumSq = new double[][] { [0.0, 0.25], [0.0, 0.25] };
            var signLsh = new ulong[][] { [1UL], [1UL] };
            var signLshMask = new ulong[][] { [1UL], [1UL] };

            var baseData = new BaseAudioOptimizationData(samples, prefixSum, prefixSumSq, signLsh, signLshMask);
            var pointerData = new PointerSoundData("test.wav", baseData, 0, 1);

            // Access before dispose
            Assert.Equal(1UL, pointerData.GetLsh(0)[0]);

            // Dispose
            pointerData.Dispose();

            // Access after dispose should throw ObjectDisposedException
            Assert.Throws<ObjectDisposedException>(() => pointerData.GetLsh(0));
            Assert.Throws<ObjectDisposedException>(() => pointerData.GetRawSpan(0, 0, 1));
        }

        [Fact]
        public void PreNormalizedSoundData_Dispose_NullsOutLshAndSamples()
        {
            var samples = new float[][] { [0.5f, -0.5f] };
            var soundData = new MockCachedSoundData(samples, 44100, 16);

            // Access before dispose
            Assert.True(soundData.GetLsh(0).Length > 0);

            // Dispose
            soundData.Dispose();

            // After dispose, LSH and samples should be cleared
            Assert.True(soundData.GetLsh(0).IsEmpty);
            Assert.Null(soundData.SamplesPerChannel);
        }
    }
}
