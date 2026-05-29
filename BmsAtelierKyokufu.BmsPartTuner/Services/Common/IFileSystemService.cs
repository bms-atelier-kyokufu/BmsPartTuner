
namespace BmsAtelierKyokufu.BmsPartTuner.Services.Common;

/// <summary>
/// ファイルシステムへのアクセスを抽象化するサービス。
/// ViewModelからの直接的な System.IO 依存を排除し、テスト時のモック化を可能にします。
/// </summary>
public interface IFileSystemService
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
}

public class FileSystemService : IFileSystemService
{
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }
}
