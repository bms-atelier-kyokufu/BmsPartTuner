namespace BmsAtelierKyokufu.BmsPartTuner.UI.ViewModels.Validators;

/// <summary>
/// MainViewModelの入力バリデーション（IDataErrorInfo）を担当する静的クラス。
/// </summary>
public static class MainInputValidator
{
    public static string ValidateInputPath(string? inputPath, IFileSystemService fileSystemService)
    {
        inputPath = inputPath?.Trim('"') ?? string.Empty;

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return string.Empty;
        }

        if (!fileSystemService.FileExists(inputPath))
        {
            return "ファイルが見つかりません";
        }

        var extension = Path.GetExtension(inputPath).ToLower();
        if (!Array.Exists(AppConstants.Files.SupportedBmsExtensions, ext => ext == extension))
        {
            var pattern = string.Join(", ", AppConstants.Files.SupportedBmsExtensions);
            return $"サポートされていない形式です ({pattern})";
        }
        return string.Empty;
    }

    public static string ValidateOutputPath(string? outputPath)
    {
        outputPath = outputPath?.Trim('"') ?? string.Empty;

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return string.Empty;
        }

        try
        {
            var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                return $"フォルダが見つかりません: {outputDir}";
            }
        }
        catch (Exception)
        {
            return "パスが無効です";
        }

        var extension = Path.GetExtension(outputPath).ToLower();
        if (!Array.Exists(AppConstants.Files.SupportedOutputBmsExtensions, ext => ext == extension))
        {
            var pattern = string.Join(", ", AppConstants.Files.SupportedOutputBmsExtensions);
            return $"出力ファイルはBMS形式である必要があります ({pattern})";
        }
        return string.Empty;
    }
}
