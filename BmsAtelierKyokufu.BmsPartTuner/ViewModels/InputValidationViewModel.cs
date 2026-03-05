using BmsAtelierKyokufu.BmsPartTuner.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BmsAtelierKyokufu.BmsPartTuner.ViewModels;

/// <summary>
/// 入力値の検証を担当するViewModel。
/// 責務: パス検証、ファイル存在確認、フォーマット検証
/// </summary>
public partial class InputValidationViewModel : ObservableObject
{
    /// <summary>入力パスが有効かどうか。</summary>
    [ObservableProperty]
    private bool isInputPathValid;

    /// <summary>出力パスが有効かどうか。</summary>
    [ObservableProperty]
    private bool isOutputPathValid;

    /// <summary>入力パスのエラーメッセージ。</summary>
    [ObservableProperty]
    private string inputPathErrorMessage = string.Empty;

    /// <summary>出力パスのエラーメッセージ。</summary>
    [ObservableProperty]
    private string outputPathErrorMessage = string.Empty;

    /// <summary>
    /// 検証エラーが発生したイベント。
    /// </summary>
    public event EventHandler<ValidationErrorEventArgs>? ValidationErrorOccurred;

    /// <summary>
    /// InputValidationViewModelを初期化。
    /// </summary>
    public InputValidationViewModel()
    {
        IsInputPathValid = false;
        IsOutputPathValid = false;
    }

    /// <summary>
    /// 入力パスを検証。
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
            return false;
        }

        var extension = Path.GetExtension(inputPath).ToLower();
        if (!Array.Exists(AppConstants.Files.SupportedBmsExtensions, ext => ext == extension))
        {
            InputPathErrorMessage = $"サポートされていない形式です ({GetSupportedExtensionsPattern()})";
            IsInputPathValid = false;
            ValidationErrorOccurred?.Invoke(this, new ValidationErrorEventArgs("InputPath", InputPathErrorMessage));
            return false;
        }

        InputPathErrorMessage = string.Empty;
        IsInputPathValid = true;
        return true;
    }

    /// <summary>
    /// 出力パスを検証。
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
                return false;
            }
        }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
        {
            OutputPathErrorMessage = "パスが無効です";
            IsOutputPathValid = false;
            ValidationErrorOccurred?.Invoke(this, new ValidationErrorEventArgs("OutputPath", OutputPathErrorMessage));
            return false;
        }

        OutputPathErrorMessage = string.Empty;
        IsOutputPathValid = true;
        return true;
    }

    /// <summary>
    /// 全入力を検証。
    /// </summary>
    public bool ValidateAll(string inputPath, string outputPath)
    {
        var inputValid = ValidateInputPath(inputPath);
        var outputValid = ValidateOutputPath(outputPath);
        return inputValid && outputValid;
    }

    /// <summary>
    /// 入力と出力の両方が設定されているかを確認。
    /// </summary>
    public bool ArePathsSpecified(string inputPath, string outputPath)
    {
        return !string.IsNullOrWhiteSpace(inputPath?.Trim('"')) &&
               !string.IsNullOrWhiteSpace(outputPath?.Trim('"'));
    }

    private string GetSupportedExtensionsPattern()
    {
        return string.Join(", ", AppConstants.Files.SupportedBmsExtensions);
    }

    #region イベント引数クラス

    /// <summary>
    /// 検証エラーのイベント引数。
    /// </summary>
    public class ValidationErrorEventArgs : EventArgs
    {
        public string PropertyName { get; }
        public string ErrorMessage { get; }

        public ValidationErrorEventArgs(string propertyName, string errorMessage)
        {
            PropertyName = propertyName;
            ErrorMessage = errorMessage;
        }
    }

    #endregion
}
