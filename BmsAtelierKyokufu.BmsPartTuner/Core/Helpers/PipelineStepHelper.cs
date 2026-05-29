namespace BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;

/// <summary>
/// パイプラインステップの表示名を生成するためのヘルパークラス。
/// </summary>
public static class PipelineStepHelper
{
    /// <summary>
    /// ステップクラス名（nameof経由）から "Step" サフィックスを除去した表示名を取得します。
    /// </summary>
    /// <param name="nameofStepClass">nameof(StepClassName) で取得したクラス名</param>
    /// <returns>加工済みのステップ表示名</returns>
    public static string GetStepName(string nameofStepClass)
    {
        if (string.IsNullOrEmpty(nameofStepClass))
        {
            return string.Empty;
        }

        return nameofStepClass.EndsWith("Step") 
            ? nameofStepClass[..^4] 
            : nameofStepClass;
    }
}
