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
        /// このコンテキストに関連付けられた新しい <see cref="BmsBuilder"/> インスタンスを作成します。
        /// </summary>
        /// <returns>BMSファイルの流れるような構築を行うビルダーインスタンス。</returns>
        public BmsBuilder CreateBuilder()
        {
            return new BmsBuilder(this);
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
