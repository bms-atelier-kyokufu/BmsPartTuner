using BmsAtelierKyokufu.BmsPartTuner.Core.Messages;
using CommunityToolkit.Mvvm.Messaging;

namespace BmsAtelierKyokufu.BmsPartTuner.ViewModels;

/// <summary>
/// ユーザー入力（ファイルパス等）の検証を担当するViewModel。
/// </summary>
public partial class InputValidationViewModel : ObservableObject
{
    /// <summary>
    /// 入力パスが有効かどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsInputPathValid { get; set; }

    /// <summary>
    /// 出力パスが有効かどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsOutputPathValid { get; set; }

    /// <summary>
    /// 入力パスに関するエラーメッセージ。
    /// </summary>
    [ObservableProperty]
    public partial string InputPathErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 出力パスに関するエラーメッセージ。
    /// </summary>
    [ObservableProperty]
    public partial string OutputPathErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 検証エラーが発生した際に発生するイベント。
    /// </summary>
    public event EventHandler<ValidationErrorEventArgs>? ValidationErrorOccurred;

    public InputValidationViewModel()
    {
        IsInputPathValid = false;
        IsOutputPathValid = false;
    }

    /// <summary>
    /// 指定された入力パスの存在とフォーマットを検証します。
    /// </summary>
    public bool ValidateInputPath(string inputPath)
    {
        inputPath = inputPath?.Trim('"') ?? string.Empty;

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            InputPathErrorMessage = string.Empty;
            IsInputPathValid = false;
            return true; // 空は警告ではなく未入力扱い
        }

        if (!File.Exists(inputPath))
        {
            InputPathErrorMessage = "ファイルが見つかりません";
            IsInputPathValid = false;
            ValidationErrorOccurred?.Invoke(this, new ValidationErrorEventArgs("InputPath", InputPathErrorMessage));
            WeakReferenceMessenger.Default.Send(new ValidationErrorMessage("InputPath", InputPathErrorMessage));
            return false;
        }

        var extension = Path.GetExtension(inputPath).ToLower();
        if (!Array.Exists(AppConstants.Files.SupportedBmsExtensions, ext => ext == extension))
        {
            InputPathErrorMessage = $"サポートされていない形式です ({GetSupportedExtensionsPattern()})";
            IsInputPathValid = false;
            ValidationErrorOccurred?.Invoke(this, new ValidationErrorEventArgs("InputPath", InputPathErrorMessage));
            WeakReferenceMessenger.Default.Send(new ValidationErrorMessage("InputPath", InputPathErrorMessage));
            return false;
        }

        InputPathErrorMessage = string.Empty;
        IsInputPathValid = true;
        return true;
    }

    /// <summary>
    /// 指定された出力パスのディレクトリの存在と拡張子を検証します。
    /// </summary>
    public bool ValidateOutputPath(string outputPath)
    {
        outputPath = outputPath?.Trim('"') ?? string.Empty;

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            OutputPathErrorMessage = string.Empty;
            IsOutputPathValid = false;
            return true; // 空は警告ではなく未入力扱い
        }

        try
        {
            var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                OutputPathErrorMessage = $"フォルダが見つかりません: {outputDir}";
                IsOutputPathValid = false;
                ValidationErrorOccurred?.Invoke(this, new ValidationErrorEventArgs("OutputPath", OutputPathErrorMessage));
                WeakReferenceMessenger.Default.Send(new ValidationErrorMessage("OutputPath", OutputPathErrorMessage));
                return false;
            }
        }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
        {
            OutputPathErrorMessage = "パスが無効です";
            IsOutputPathValid = false;
            ValidationErrorOccurred?.Invoke(this, new ValidationErrorEventArgs("OutputPath", OutputPathErrorMessage));
            WeakReferenceMessenger.Default.Send(new ValidationErrorMessage("OutputPath", OutputPathErrorMessage));
            return false;
        }

        var extension = Path.GetExtension(outputPath).ToLower();
        if (!Array.Exists(AppConstants.Files.SupportedOutputBmsExtensions, ext => ext == extension))
        {
            OutputPathErrorMessage = $"出力ファイルはBMS形式である必要があります ({GetSupportedOutputExtensionsPattern()})";
            IsOutputPathValid = false;
            ValidationErrorOccurred?.Invoke(this, new ValidationErrorEventArgs("OutputPath", OutputPathErrorMessage));
            WeakReferenceMessenger.Default.Send(new ValidationErrorMessage("OutputPath", OutputPathErrorMessage));
            return false;
        }

        OutputPathErrorMessage = string.Empty;
        IsOutputPathValid = true;
        return true;
    }

    /// <summary>
    /// 入力パスと出力パスの両方を検証します。
    /// </summary>
    public bool ValidateAll(string inputPath, string outputPath)
    {
        var inputValid = ValidateInputPath(inputPath);
        var outputValid = ValidateOutputPath(outputPath);
        return inputValid && outputValid;
    }

    /// <summary>
    /// 入力パスと出力パスの両方が指定されているかどうかを確認します。
    /// </summary>
    public static bool ArePathsSpecified(string inputPath, string outputPath)
    {
        return !string.IsNullOrWhiteSpace(inputPath?.Trim('"')) &&
               !string.IsNullOrWhiteSpace(outputPath?.Trim('"'));
    }

    private static string GetSupportedExtensionsPattern()
    {
        return string.Join(", ", AppConstants.Files.SupportedBmsExtensions);
    }

    private static string GetSupportedOutputExtensionsPattern()
    {
        return string.Join(", ", AppConstants.Files.SupportedOutputBmsExtensions);
    }

    /// <summary>
    /// 検証エラーイベントの引数を提供します。
    /// </summary>
    public class ValidationErrorEventArgs(string propertyName, string errorMessage) : EventArgs
    {
        public string PropertyName { get; } = propertyName;
        public string ErrorMessage { get; } = errorMessage;
    }
}
