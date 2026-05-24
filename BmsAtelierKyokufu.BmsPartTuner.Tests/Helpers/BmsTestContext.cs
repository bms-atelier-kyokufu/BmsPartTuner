using System.IO;
using System.Text;
using BmsAtelierKyokufu.BmsPartTuner.Core;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers
{
    /// <summary>
    /// A helper class to manage temporary BMS file environment for testing.
    /// It creates a temporary directory and cleans it up upon disposal.
    /// </summary>
    public class BmsTestContext : IDisposable
    {
        private readonly string _tempDirectory;
        private bool _disposed;

        public BmsTestContext()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
        }

        public string TempDirectory => _tempDirectory;

        /// <summary>
        /// Creates a new BmsFileBuilder linked to this context.
        /// </summary>
        public BmsFileBuilder CreateBuilder()
        {
            return new BmsFileBuilder(this);
        }

        public void Dispose()
        {
            if (_disposed) return;
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch
            {
                // Best effort cleanup; ignore errors (e.g., file in use)
            }
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Fluent builder for constructing BMS files and associated dummy assets.
    /// </summary>
    public class BmsFileBuilder(BmsTestContext context)
    {
        private readonly BmsTestContext _context = context;
        private readonly StringBuilder _headerContent = new();
        private readonly StringBuilder _wavDefinitions = new();
        private readonly StringBuilder _mainData = new();
        private Encoding _encoding = Encoding.UTF8;

        // Base36 characters for index generation
        private const string Base36Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        /// <summary>
        /// Adds a header field.
        /// </summary>
        public BmsFileBuilder WithHeader(string key, string value)
        {
            _headerContent.AppendLine($"#{key} {value}");
            return this;
        }

        /// <summary>
        /// Defines a WAV file definition (e.g., #WAV01 filename.wav) and creates a dummy file.
        /// </summary>
        /// <param name="index">The integer index (e.g., 1 -> 01, 36 -> 10).</param>
        /// <param name="filename">The filename of the wav.</param>
        /// <param name="createFile">If true, creates a dummy file. If false, assumes file already exists.</param>
        /// <param name="writeToDisk">If true, writes the dummy WAV file to disk. If false, registers it only in VirtualAudioRegistry.</param>
        public BmsFileBuilder WithWav(int index, string filename, bool createFile = true, bool writeToDisk = true)
        {
            string indexStr = ToBmsIndex(index);
            _wavDefinitions.AppendLine($"#WAV{indexStr} {filename}");
            if (createFile)
            {
                CreateDummyFile(filename, writeToDisk);
            }
            return this;
        }

        /// <summary>
        /// Defines a WAV file definition with a custom string index (e.g., for "ZZ" or larger if manually specified).
        /// </summary>
        /// <param name="indexStr">The custom index string.</param>
        /// <param name="filename">The filename of the wav.</param>
        /// <param name="createFile">If true, creates a dummy file. If false, assumes file already exists.</param>
        /// <param name="writeToDisk">If true, writes the dummy WAV file to disk. If false, registers it only in VirtualAudioRegistry.</param>
        public BmsFileBuilder WithWav(string indexStr, string filename, bool createFile = true, bool writeToDisk = true)
        {
            _wavDefinitions.AppendLine($"#WAV{indexStr} {filename}");
            if (createFile)
            {
                CreateDummyFile(filename, writeToDisk);
            }
            return this;
        }


        /// <summary>
        /// Adds main data to the BMS file.
        /// </summary>
        /// <param name="measure">The measure number (0-999).</param>
        /// <param name="channel">The channel number (e.g., 11 for BGM).</param>
        /// <param name="data">The data string (e.g., "01020102").</param>
        public BmsFileBuilder AddMainData(int measure, int channel, string data)
        {
            _mainData.AppendLine($"#{measure:D3}{channel:D2}:{data}");
            return this;
        }

        /// <summary>
        /// Adds main data assuming measure 1 (001) for convenience.
        /// </summary>
        public BmsFileBuilder AddMainData(int channel, string data)
        {
            return AddMainData(1, channel, data);
        }

        /// <summary>
        /// Sets the encoding for the generated file.
        /// </summary>
        public BmsFileBuilder WithEncoding(Encoding encoding)
        {
            _encoding = encoding;
            return this;
        }

        /// <summary>
        /// Adds random noise or invalid lines to the file content.
        /// </summary>
        public BmsFileBuilder AddNoise(string noise)
        {
            _mainData.AppendLine(noise);
            return this;
        }

        /// <summary>
        /// Builds the BMS file and writes it to the temporary directory.
        /// </summary>
        /// <param name="filename">The name of the BMS file to create.</param>
        /// <returns>The full path to the created BMS file.</returns>
        public string Build(string filename)
        {
            var path = Path.Combine(_context.TempDirectory, filename);
            var sb = new StringBuilder();

            sb.Append(_headerContent);
            sb.Append(_wavDefinitions);
            sb.Append(_mainData);

            File.WriteAllText(path, sb.ToString(), _encoding);
            return path;
        }

        private void CreateDummyFile(string filename, bool writeToDisk = true)
        {
            var path = Path.Combine(_context.TempDirectory, filename);
            BmsTestWavHelper.CreateDummyWavFile(path, writeToDisk);
        }

        private static string ToBmsIndex(int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            string result = "";
            int target = index;

            // Handle 0 specifically if needed, but usually 00.
            if (target == 0) return AppConstants.Definition.End;

            while (target > 0)
            {
                result = Base36Chars[target % 36] + result;
                target /= 36;
            }

            // Pad to at least 2 chars
            if (result.Length < 2)
            {
                result = result.PadLeft(2, '0');
            }

            return result;
        }
    }
}
