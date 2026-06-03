using System.IO;

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers
{
    /// <summary>
    /// テスト用の一時的なBMSまたはBMSONファイル環境を管理するヘルパークラス。
    /// 一時ディレクトリを作成し、破棄（Dispose）時に自動的にクリーンアップを行います。
    /// </summary>
    public class BmsFamilyTestContext : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// <see cref="BmsFamilyTestContext"/> クラスの新しいインスタンスを初期化し、一時ディレクトリを作成します。
        /// </summary>
        public BmsFamilyTestContext()
        {
            TempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(TempDirectory);
        }

        /// <summary>
        /// テスト用の一時ディレクトリのパス。
        /// </summary>
        public string TempDirectory { get; }

        /// <summary>
        /// 指定されたビルダー型のインスタンスを作成します。
        /// </summary>
        public TBuilder CreateBuilder<TBuilder>() where TBuilder : class, IBmsFamilyBuilder<TBuilder>
        {
            return TBuilder.Create(this);
        }

        /// <summary>
        /// ベースとなる共通曲情報（ヘッダー）が設定済みのビルダーインスタンスを作成します。
        /// </summary>
        public TBuilder CreateBaseBuilder<TBuilder>() where TBuilder : class, IBmsFamilyBuilder<TBuilder>
        {
            var builder = CreateBuilder<TBuilder>();
            builder.WithHeader("TITLE", "Test Title");
            builder.WithHeader("GENRE", "Test Genre");
            builder.WithHeader("ARTIST", "Test Artist");
            builder.WithHeader("BPM", "130");
            builder.WithHeader("PLAYLEVEL", "5");
            builder.WithHeader("RANK", "1"); // 1=50 in Bmson
            builder.WithHeader("TOTAL", "200");
            builder.WithHeader("RESOLUTION", "240");
            return builder;
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
}
