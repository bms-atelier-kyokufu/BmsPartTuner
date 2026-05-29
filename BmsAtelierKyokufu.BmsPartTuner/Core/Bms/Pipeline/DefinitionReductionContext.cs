using System.Collections.Generic;
using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms.Pipeline;

/// <summary>
/// BMS定義削減パイプラインの実行コンテキスト。
/// 各処理ステップ間で共有すべき状態や中間データを保持します。
/// </summary>
internal sealed class DefinitionReductionContext
{
    // 入力・設定パラメータ
    public string InputBmsFileName { get; }
    public string OutputSaveFileName { get; }
    public DefinitionReductionOptions Options { get; }
    public NormalizationMode NormalizationMode { get; }
    public IReadOnlyList<BmsAudioFile> FileList { get; }
    public string? InputBmsContent { get; }

    // パイプライン内で初期化・更新される共有状態
    public DefinitionRangeManager RangeManager { get; }
    public IReadOnlyDictionary<string, ICachedSoundData> AudioCache { get; set; }
    public int[] Replaces { get; }
    
    // 中間生成物
    public DefinitionStatistics? Statistics { get; set; }
    public BmsFileRewriter? Rewriter { get; set; }
    public string? RewriteData { get; set; }

    public DefinitionReductionContext(
        string inputBmsFileName,
        string outputSaveFileName,
        DefinitionReductionOptions options,
        NormalizationMode normalizationMode,
        IReadOnlyList<BmsAudioFile> fileList,
        IReadOnlyDictionary<string, ICachedSoundData> audioCache,
        string? inputBmsContent)
    {
        InputBmsFileName = inputBmsFileName;
        OutputSaveFileName = outputSaveFileName;
        Options = options;
        NormalizationMode = normalizationMode;
        FileList = fileList;
        AudioCache = audioCache;
        InputBmsContent = inputBmsContent;

        Replaces = new int[AppConstants.Definition.ReplaceTableSize];
        RangeManager = new DefinitionRangeManager(FileList);
    }
}
