using System.IO;
using Microsoft.CodeAnalysis;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.MutationFramework;

/// <summary>
/// 変異テストの設定を保持するクラス。
/// 
/// <para><b>【設計思想】</b></para>
/// <para>
/// - デフォルト値は一般的なC#プロジェクトに適用できるように設定
/// - すべてのプロパティが外部から変更可能（柔軟性）
/// - 並列処理やJSON出力の制御を提供
/// </para>
/// 
/// <para><b>【使用例】</b></para>
/// <code>
/// // 自動検出を使用
/// var config = MutationTestConfiguration.AutoDetect("MyProject");
/// 
/// // カスタマイズ
/// config.MaxParallelism = 4;
/// config.IncludePatterns.Add("Core/");
/// config.SaveResultsToJson = false;
/// </code>
/// </summary>
public class MutationTestConfiguration
{
    /// <summary>
    /// テスト対象のソースディレクトリ（絶対パスまたは相対パス）。
    /// <para>
    /// <b>Why:</b> AutoDetect() を使用すると自動的に設定されます。
    /// 手動で設定する場合は、プロジェクトのルートディレクトリを指定してください。
    /// </para>
    /// </summary>
    public string SourceDirectory { get; set; } = "";

    /// <summary>
    /// 除外するファイルパターン。
    /// <para>
    /// <b>Why:</b> 自動生成ファイルやビルド成果物を除外することで、
    /// 意味のあるコードのみをテスト対象とします。
    /// </para>
    /// <para>
    /// <b>デフォルト値:</b> obj/, bin/, .Designer.cs, .g.cs, .g.i.cs,
    /// GlobalUsings.cs, AssemblyInfo.cs, .xaml.cs
    /// </para>
    /// </summary>
    public List<string> ExcludePatterns { get; set; } =
    [
        "obj/",              // ビルド中間成果物
        "bin/",              // ビルド出力
        ".Designer.cs",      // デザイナー自動生成ファイル
        ".g.cs",             // WPF/WinForms 自動生成
        ".g.i.cs",           // WPF/WinForms 中間生成
        "GlobalUsings.cs",   // C# 10+ グローバル using
        "AssemblyInfo.cs",   // アセンブリメタデータ
        ".xaml.cs",          // XAML コードビハインド（UI依存）
        "RoslynMutation/"    // ミューテーションテストフレームワーク自身（自己参照防止）
    ];

    /// <summary>
    /// 対象とするファイルパターン（空の場合は除外パターン以外すべて）。
    /// <para>
    /// <b>Why:</b> 特定のディレクトリやファイルに絞って変異テストを実行したい場合に使用。
    /// 空の場合、除外パターンに一致しないすべてのファイルが対象になります。
    /// </para>
    /// <para>
    /// <b>使用例:</b> IncludePatterns.Add("Core/"); で Core/ 配下のみ対象
    /// </para>
    /// </summary>
    public List<string> IncludePatterns { get; set; } = [];

    /// <summary>
    /// 並列処理の最大並列度。
    /// <para>
    /// <b>Why:</b> 変異テストは時間がかかるため、CPUコア数に応じて並列処理を行います。
    /// デフォルトは (CPUコア数 - 1) に設定され、システムの応答性を維持します。
    /// </para>
    /// <para>
    /// <b>デフォルト値:</b> Math.Max(1, Environment.ProcessorCount - 1)
    /// </para>
    /// <para>
    /// <b>注意:</b> 1 に設定すると逐次実行になります（デバッグ時に有用）。
    /// </para>
    /// </summary>
    public int MaxParallelism { get; set; } = Math.Max(1, Environment.ProcessorCount - 1);

    /// <summary>
    /// 結果をJSONファイルに保存するかどうか。
    /// <para>
    /// <b>Why:</b> CI/CD パイプラインでの結果分析や、
    /// 時系列での変異スコア追跡を可能にします。
    /// </para>
    /// <para>
    /// <b>デフォルト値:</b> true
    /// </para>
    /// </summary>
    public bool SaveResultsToJson { get; set; } = true;

    /// <summary>
    /// JSON結果の出力ディレクトリ。
    /// <para>
    /// <b>Why:</b> テスト結果を一元管理し、履歴を追跡しやすくします。
    /// </para>
    /// <para>
    /// <b>デフォルト値:</b> "TestResults"
    /// </para>
    /// <para>
    /// <b>注意:</b> 相対パスの場合、作業ディレクトリからの相対パスとして解釈されます。
    /// </para>
    /// </summary>
    public string ResultsDirectory { get; set; } = "TestResults";

    /// <summary>
    /// 進捗を表示する間隔。
    /// <para>
    /// <b>Why:</b> 長時間実行される変異テストで、進捗状況を確認できるようにします。
    /// </para>
    /// <para>
    /// <b>デフォルト値:</b> 100 (100変異ごとに進捗を表示)
    /// </para>
    /// </summary>
    public int ProgressReportInterval { get; set; } = 100;

    /// <summary>
    /// 追加のメタデータ参照。
    /// <para>
    /// <b>Why:</b> プロジェクト固有の依存アセンブリを追加できるようにします。
    /// デフォルトで System.Runtime, System.Collections, System.Linq などは含まれています。
    /// </para>
    /// <para>
    /// <b>使用例:</b>
    /// <code>
    /// config.AdditionalReferences.Add(
    ///     MetadataReference.CreateFromFile(typeof(MyLibrary.MyClass).Assembly.Location)
    /// );
    /// </code>
    /// </para>
    /// </summary>
    public List<MetadataReference> AdditionalReferences { get; set; } = [];

    /// <summary>
    /// ソースディレクトリの候補パスから自動検出。
    /// 
    /// <para><b>【動作】</b></para>
    /// <para>
    /// 複数の候補パスを試行し、以下の条件を満たす最初のパスを SourceDirectory に設定します：
    /// </para>
    /// <list type="number">
    /// <item><description>ディレクトリが存在する</description></item>
    /// <item><description>markerDirectory で指定されたサブディレクトリが存在する（省略可能）</description></item>
    /// </list>
    /// 
    /// <para><b>【Why】</b></para>
    /// <para>
    /// テストの実行環境（Visual Studio テストエクスプローラー、dotnet test、CI/CD）によって
    /// 作業ディレクトリが異なるため、自動的に正しいパスを検出します。
    /// </para>
    /// </summary>
    /// <param name="projectName">
    /// 対象プロジェクトの名前（例: "MyProject"）。
    /// ソリューションルートからの相対パスとして使用されます。
    /// </param>
    /// <param name="markerDirectory">
    /// プロジェクトを識別するためのマーカーディレクトリ（例: "Core", "src"）。
    /// null または空文字列の場合、ディレクトリの存在のみで判定します。
    /// <para>
    /// <b>Why:</b> 同名のディレクトリが複数ある場合に、正しいプロジェクトを特定するため。
    /// </para>
    /// </param>
    /// <returns>
    /// 検出された SourceDirectory が設定された MutationTestConfiguration。
    /// 検出に失敗した場合でも、最も可能性の高いパスが設定されます。
    /// </returns>
    /// <example>
    /// <code>
    /// // "Core" ディレクトリを持つ "MyProject" を検出
    /// var config = MutationTestConfiguration.AutoDetect("MyProject", "Core");
    /// 
    /// // マーカーなしで検出（ディレクトリの存在のみで判定）
    /// var config = MutationTestConfiguration.AutoDetect("MyProject");
    /// </code>
    /// </example>
    public static MutationTestConfiguration AutoDetect(string projectName, string? markerDirectory = "Core")
    {
        var config = new MutationTestConfiguration();

        // 候補パス（実行環境に応じて異なる）
        string[] candidates =
        [
            $"../../../../{projectName}",  // bin/Debug/net10.0-windows/ から
            $"../../../{projectName}",     // bin/Debug/ から
            $"../{projectName}",           // プロジェクトルートから
            projectName                    // ソリューションルートから
        ];

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath))
            {
                // マーカーディレクトリが指定されている場合は、その存在も確認
                if (string.IsNullOrEmpty(markerDirectory) ||
                    Directory.Exists(Path.Combine(fullPath, markerDirectory)))
                {
                    config.SourceDirectory = fullPath;
                    break;
                }
            }
        }

        // 検出失敗時のフォールバック
        if (string.IsNullOrEmpty(config.SourceDirectory))
        {
            config.SourceDirectory = Path.GetFullPath($"../../../../{projectName}");
        }

        return config;
    }
}
