using CommunityToolkit.Mvvm.Messaging.Messages;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Messages;

/// <summary>
/// UI層（View）の更新やダイアログ表示など、プレゼンテーション層に特化したイベント群。
/// </summary>

/// <summary>
/// 音声プレビューの再生状態が変更されたときのメッセージ。
/// UIの再生・停止ボタンの状態などに反映されます。
/// </summary>
public record AudioPlaybackStateChangedMessage(bool IsLoading, bool IsPlaying, string? FileName);

/// <summary>
/// 最適化処理中にエラーが発生したときのメッセージ。
/// トーストやエラーダイアログの表示に使用されます。
/// </summary>
public record OptimizationErrorMessage(string ErrorMessage);

/// <summary>
/// 入力検証エラーが発生したときのメッセージ。
/// UI上のバリデーションエラー表示などに使用されます。
/// </summary>
public record ValidationErrorMessage(string PropertyName, string ErrorMessage);

/// <summary>
/// メディア再生エラーが発生したときのメッセージ。
/// </summary>
public record MediaPlaybackErrorMessage(string Message);

/// <summary>
/// スライド確認UIの表示が要求されたときのメッセージ。
/// </summary>
public record SlideConfirmationRequestedMessage();
