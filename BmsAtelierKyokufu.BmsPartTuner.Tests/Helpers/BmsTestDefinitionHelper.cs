using System.Collections.ObjectModel;
using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;
using BmsAtelierKyokufu.BmsPartTuner.Models;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers
{
    /// <summary>
    /// テスト用のBMS定義データ（<see cref="BmsAudioFile"/>等）の生成を支援するヘルパークラス。
    /// </summary>
    public static class BmsTestDefinitionHelper
    {
        /// <summary>
        /// 指定されたパラメータに基づいて、テスト用の <see cref="BmsAudioFile"/> インスタンスを生成します。
        /// </summary>
        /// <param name="numInteger">整数値の定義番号。</param>
        /// <param name="num">BMS定義の36/62進数インデックス文字列（空の場合は自動変換されます）。</param>
        /// <param name="namePattern">ファイル名の命名パターン。</param>
        /// <param name="fileSize">ファイルのサイズ。</param>
        /// <returns>生成された <see cref="BmsAudioFile"/>。</returns>
        public static BmsAudioFile CreateBmsAudioFile(int numInteger, string num = "", string namePattern = "test_{0}.wav", long fileSize = 1000)
        {
            int radix = numInteger > 1295 ? 62 : 36;
            return new BmsAudioFile
            {
                NumInteger = numInteger,
                Num = string.IsNullOrEmpty(num) ? RadixConvert.IntToZZ(numInteger, radix) : num,
                Name = string.Format(namePattern, numInteger),
                FileSize = fileSize
            };
        }

        /// <summary>
        /// 指定された定義番号のリストから、テスト用のBMSオーディオファイルリストを生成します。
        /// </summary>
        /// <param name="numbers">定義番号の配列。</param>
        /// <returns>生成された <see cref="BmsAudioFile"/> のリスト。</returns>
        public static List<BmsAudioFile> CreateBmsDefinitionManager(params int[] numbers)
        {
            return [.. numbers.Select(n => CreateBmsAudioFile(n))];
        }

        /// <summary>
        /// 物理的なWAVファイルを生成しつつ、BMSオーディオファイル定義リストを生成します。
        /// </summary>
        /// <param name="tempDir">一時ディレクトリのパス。</param>
        /// <param name="radix">進数（36または62）。</param>
        /// <param name="files">定義番号とファイル名のペアリスト。</param>
        /// <returns>生成された <see cref="BmsAudioFile"/> のコレクション。</returns>
        public static ObservableCollection<BmsAudioFile> CreateBmsDefinitionManagerWithPhysicalWav(string tempDir, int radix, params (int num, string filename)[] files)
        {
            var fileList = new ObservableCollection<BmsAudioFile>();

            foreach (var (num, filename) in files)
            {
                var filePath = Path.Combine(tempDir, filename);
                // Create a basic physical sine wave file
                BmsTestWavHelper.CreateSineWavFile(filePath, writeToDisk: true);

                fileList.Add(new BmsAudioFile
                {
                    Num = RadixConvert.IntToZZ(num, radix),
                    NumInteger = num,
                    Name = filePath,
                    FileSize = new FileInfo(filePath).Length
                });
            }

            return fileList;
        }

        /// <summary>
        /// メモリ上の仮想WAVデータを登録しつつ、BMSオーディオファイル定義リストを生成します。
        /// </summary>
        /// <param name="radix">進数（36または62）。</param>
        /// <param name="files">定義番号とファイル名のペアリスト。</param>
        /// <returns>生成された <see cref="BmsAudioFile"/> のコレクション。</returns>
        public static ObservableCollection<BmsAudioFile> CreateBmsDefinitionManagerWithMemoryWav(int radix, params (int num, string filename)[] files)
        {
            var fileList = new ObservableCollection<BmsAudioFile>();

            foreach (var (num, filename) in files)
            {
                // Create in-memory wav file and register it in VirtualAudioRegistry
                var data = BmsTestWavHelper.CreateSineWavBytes();
                VirtualAudioRegistry.AddFile(filename, data);

                fileList.Add(new BmsAudioFile
                {
                    Num = RadixConvert.IntToZZ(num, radix),
                    NumInteger = num,
                    Name = filename,
                    FileSize = data.Length
                });
            }

            return fileList;
        }
    }
}
