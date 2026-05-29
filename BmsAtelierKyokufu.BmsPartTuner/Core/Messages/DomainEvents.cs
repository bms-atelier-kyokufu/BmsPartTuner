using CommunityToolkit.Mvvm.Messaging.Messages;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Messages;

/// <summary>
/// アプリケーションのビジネスロジックや状態遷移を表すドメインイベント群。
/// ViewModel間の状態同期に利用されます。
/// </summary>

/// <summary>
/// 入力パスが変更されたときのメッセージ。
/// </summary>
public record InputPathChangedMessage(string Path);

/// <summary>
/// 自動出力パスが要求されたときのメッセージ。
/// </summary>
public record AutoOutputPathRequestedMessage(string OutputPath);

/// <summary>
/// ファイルリストの読み込みが完了したときのメッセージ。
/// </summary>
public record FileListLoadedMessage(bool IsSuccess, string FilePath, string ErrorMessage);

/// <summary>
/// 定義の最適化（削減）処理が完了したときのメッセージ。
/// </summary>
public record DefinitionReductionCompletedMessage(object? Result, string OutputPath, float Threshold);
