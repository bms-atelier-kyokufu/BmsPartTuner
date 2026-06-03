using System.IO;
using System.Text;
using BmsAtelierKyokufu.BmsPartTuner.Core;

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers
{
    /// <summary>
    /// テスト用の一時的なBMSファイル環境を管理するヘルパークラス。
    /// 一時ディレクトリを作成し、破棄（Dispose）時に自動的にクリーンアップを行います。
    /// </summary>
    public class BmsTestContext : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// <see cref="BmsTestContext"/> クラスの新しいインスタンスを初期化し、一時ディレクトリを作成します。
        /// </summary>
        public BmsTestContext()
        {
            TempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(TempDirectory);
        }

        /// <summary>
        /// テスト用の一時ディレクトリのパス。
        /// </summary>
        public string TempDirectory { get; }

        /// <summary>
        /// このコンテキストに関連付けられた新しい <see cref="BmsFileBuilder"/> インスタンスを作成します。
        /// </summary>
        /// <returns>BMSファイルの流れるような構築を行うビルダーインスタンス。</returns>
        public BmsFileBuilder CreateBuilder()
        {
            return new BmsFileBuilder(this);
        }

        /// <summary>
        /// 一時ディレクトリの削除および各種オーディオリソースの登録解除を行い、リソースを解放します。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            try
            {
                if (Directory.Exists(TempDirectory))
                {
                    Directory.Delete(TempDirectory, true);
                }
            }
            catch
            {
                // ベストエフォートでのクリーンアップ。ファイル使用中等のエラーは無視します。
            }
            AudioRegistry.Instance.Clear();
            VirtualAudioRegistry.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// BMSファイルおよび関連するダミーアセットを流れるように構築するためのビルダー。
    /// </summary>
    /// <remarks>
    /// コンストラクタでテストコンテキストを受け取ります。
    /// </remarks>
    /// <param name="context">関連付ける <see cref="BmsTestContext"/>。</param>
    public class BmsFileBuilder(BmsTestContext context)
    {
        private readonly BmsTestContext _context = context;
        private readonly StringBuilder _headerContent = new();
        private readonly StringBuilder _wavDefinitions = new();
        private readonly StringBuilder _mainData = new();
        private Encoding _encoding = Encoding.UTF8;

        // Base36 文字列（BMS定義インデックス生成用）
        private const string Base36Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        /// <summary>
        /// ヘッダーフィールドを追加します。
        /// </summary>
        /// <param name="key">キー名。</param>
        /// <param name="value">値。</param>
        /// <returns>このビルダーのインスタンス。</returns>
        public BmsFileBuilder WithHeader(string key, string value)
        {
            _headerContent.AppendLine($"#{key} {value}");
            return this;
        }

        /// <summary>
        /// WAVファイルの定義（例: #WAV01 filename.wav）を追加し、必要に応じてダミーファイルを作成します。
        /// </summary>
        /// <param name="index">整数値インデックス（例: 1 -> 01, 36 -> 10）。</param>
        /// <param name="filename">WAVファイルのファイル名。</param>
        /// <param name="createFile">ダミーファイルを生成するかどうか。</param>
        /// <param name="writeToDisk">ディスクに書き出すかどうか。偽の場合は仮想レジストリ登録のみ。</param>
        /// <returns>このビルダーのインスタンス。</returns>
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
        /// カスタムインデックス文字列（例: "ZZ" など）を使用してWAVファイルの定義を追加し、必要に応じてダミーファイルを作成します。
        /// </summary>
        /// <param name="indexStr">カスタムインデックス文字列。</param>
        /// <param name="filename">WAVファイルのファイル名。</param>
        /// <param name="createFile">ダミーファイルを生成するかどうか。</param>
        /// <param name="writeToDisk">ディスクに書き出すかどうか。偽の場合は仮想レジストリ登録のみ。</param>
        /// <returns>このビルダーのインスタンス。</returns>
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
        /// BMSファイルにメインデータ（配置データ）を追加します。
        /// </summary>
        /// <param name="measure">小節番号 (0〜999)。</param>
        /// <param name="channel">チャンネル番号（例: BGMの場合は 11）。</param>
        /// <param name="data">データ文字列（例: "01020102"）。</param>
        /// <returns>このビルダーのインスタンス。</returns>
        public BmsFileBuilder AddMainData(int measure, int channel, string data)
        {
            _mainData.AppendLine($"#{measure:D3}{channel:D2}:{data}");
            return this;
        }

        /// <summary>
        /// メジャー番号 1 (001) を前提として、BMSファイルにメインデータ（配置データ）を追加します。
        /// </summary>
        /// <param name="channel">チャンネル番号。</param>
        /// <param name="data">データ文字列。</param>
        /// <returns>このビルダーのインスタンス。</returns>
        public BmsFileBuilder AddMainData(int channel, string data)
        {
            return AddMainData(1, channel, data);
        }

        /// <summary>
        /// 生成するファイルの文字エンコーディングを設定します。
        /// </summary>
        /// <param name="encoding">適用するエンコーディング。</param>
        /// <returns>このビルダーのインスタンス。</returns>
        public BmsFileBuilder WithEncoding(Encoding encoding)
        {
            _encoding = encoding;
            return this;
        }

        /// <summary>
        /// ランダムなノイズや無効な行をファイルコンテンツに追加します。
        /// </summary>
        /// <param name="noise">追加するノイズ文字列。</param>
        /// <returns>このビルダーのインスタンス。</returns>
        public BmsFileBuilder AddNoise(string noise)
        {
            _mainData.AppendLine(noise);
            return this;
        }

        /// <summary>
        /// BMSファイルを構築し、一時ディレクトリに書き出します。
        /// </summary>
        /// <param name="filename">作成するBMSファイルのファイル名。</param>
        /// <returns>作成されたBMSファイルのフルパス。</returns>
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

            if (target == 0) return AppConstants.Definition.End;

            while (target > 0)
            {
                result = Base36Chars[target % 36] + result;
                target /= 36;
            }

            if (result.Length < 2)
            {
                result = result.PadLeft(2, '0');
            }

            return result;
        }
    }
}
