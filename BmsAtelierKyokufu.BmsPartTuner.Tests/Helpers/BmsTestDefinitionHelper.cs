using System.Collections.ObjectModel;
using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;
using BmsAtelierKyokufu.BmsPartTuner.Models;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers
{
    public static class BmsTestDefinitionHelper
    {
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

        public static List<BmsAudioFile> CreateBmsDefinitionManager(params int[] numbers)
        {
            return [.. numbers.Select(n => CreateBmsAudioFile(n))];
        }

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

        public static ObservableCollection<BmsAudioFile> CreateBmsDefinitionManagerWithMemoryWav(int radix, params (int num, string filename)[] files)
        {
            var fileList = new ObservableCollection<BmsAudioFile>();

            foreach (var (num, filename) in files)
            {
                // Create in-memory wav file and register it in VirtualAudioRegistry
                var data = BmsTestWavHelper.CreateSineWavBytes();
                BmsPartTuner.Core.Audio.VirtualAudioRegistry.AddFile(filename, data);

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
