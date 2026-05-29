using System.Collections.Concurrent;
using System.IO;
using Microsoft.CodeAnalysis;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.RoslynMutation.Framework;

/// <summary>
/// 変異テストを実行するクラス。
///
/// <para><b>【主な機能】</b></para>
/// <list type="bullet">
/// <item><description>ソースファイルの自動検出</description></item>
/// <item><description>並列での変異生成とテスト実行</description></item>
/// <item><description>進捗表示とログ出力</description></item>
/// <item><description>JSON形式での結果レポート生成</description></item>
/// </list>
///
/// <para><b>【Why: なぜランナークラスが必要か】</b></para>
/// <para>
/// 変異テストは複雑なプロセス（ファイル検出→変異生成→コンパイル→実行→結果集計）
/// であり、これらを一元管理するオーケストレーターが必要です。
/// </para>
/// </summary>
/// <example>
/// <code>
/// // Fluent API による使用例
/// var report = MutationTestRunner
///     .Create()
///     .Configure(projectName: "MyProject", markerDirectory: "Core")
///     .WithTestCase(new MyTestCase())
///     .WithLogger(Console.WriteLine)
///     .RunAll();
///
/// // 詳細なカスタマイズ
/// var report = MutationTestRunner
///     .Create()
///     .Configure(projectName: "MyProject")
///     .WithTestCase(new CalculatorTestCase())
///     .WithMaxParallelism(4)
///     .IncludeFiles("Core/", "Services/")
///     .ExcludeFiles("Legacy/")
///     .DisableJsonOutput()
///     .RunAll();
/// </code>
/// </example>
public class MutationTestRunner
{
    private MutationTestConfiguration _config;
    private readonly MutantTestCaseRegistry _testCaseRegistry;
    private Action<string>? _logger;

    /// <summary>
    /// MutationTestRunner のインスタンスを初期化。
    /// <para>
    /// <b>Note:</b> 直接インスタンス化せず、<see cref="Create"/> メソッドから開始してください。
    /// </para>
    /// </summary>
    private MutationTestRunner()
    {
        _config = new MutationTestConfiguration();
        _testCaseRegistry = new MutantTestCaseRegistry();
        _logger = Console.WriteLine;
    }

    #region Fluent API

    /// <summary>
    /// MutationTestRunner のインスタンスを作成（Fluent API のエントリポイント）。
    /// </summary>
    /// <returns>新しい MutationTestRunner インスタンス</returns>
    /// <example>
    /// <code>
    /// var report = MutationTestRunner
    ///     .Create()
    ///     .Configure(projectName: "MyProject")
    ///     .RunAll();
    /// </code>
    /// </example>
    public static MutationTestRunner Create() => new();

    /// <summary>
    /// プロジェクトの設定を構成（自動検出）。
    /// </summary>
    /// <param name="projectName">対象プロジェクト名</param>
    /// <param name="markerDirectory">プロジェクト識別用のマーカーディレクトリ（省略可）</param>
    /// <returns>メソッドチェーン用の自身のインスタンス</returns>
    public MutationTestRunner Configure(string projectName, string? markerDirectory = "Core")
    {
        _config = MutationTestConfiguration.AutoDetect(projectName, markerDirectory);
        return this;
    }

    /// <summary>
    /// カスタム設定で構成。
    /// </summary>
    /// <param name="config">カスタム設定</param>
    /// <returns>メソッドチェーン用の自身のインスタンス</returns>
    public MutationTestRunner Configure(MutationTestConfiguration config)
    {
        _config = config;
        return this;
    }

    /// <summary>
    /// カスタムテストケースを追加。
    /// </summary>
    /// <param name="testCase">追加するテストケース</param>
    /// <returns>メソッドチェーン用の自身のインスタンス</returns>
    public MutationTestRunner WithTestCase(IMutantTestCase testCase)
    {
        _testCaseRegistry.Register(testCase);
        return this;
    }

    /// <summary>
    /// ログ出力先を設定。
    /// </summary>
    /// <param name="logger">ログ出力用のアクション</param>
    /// <returns>メソッドチェーン用の自身のインスタンス</returns>
    public MutationTestRunner WithLogger(Action<string> logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// 並列度を設定。
    /// </summary>
    /// <param name="maxParallelism">最大並列度</param>
    /// <returns>メソッドチェーン用の自身のインスタンス</returns>
    public MutationTestRunner WithMaxParallelism(int maxParallelism)
    {
        _config.MaxParallelism = maxParallelism;
        return this;
    }

    /// <summary>
    /// 包含パターンを追加。
    /// </summary>
    /// <param name="patterns">包含パターン</param>
    /// <returns>メソッドチェーン用の自身のインスタンス</returns>
    public MutationTestRunner IncludeFiles(params string[] patterns)
    {
        _config.IncludePatterns.AddRange(patterns);
        return this;
    }

    /// <summary>
    /// 除外パターンを追加。
    /// </summary>
    /// <param name="patterns">除外パターン</param>
    /// <returns>メソッドチェーン用の自身のインスタンス</returns>
    public MutationTestRunner ExcludeFiles(params string[] patterns)
    {
        _config.ExcludePatterns.AddRange(patterns);
        return this;
    }

    /// <summary>
    /// JSON出力を無効化。
    /// </summary>
    /// <returns>メソッドチェーン用の自身のインスタンス</returns>
    public MutationTestRunner DisableJsonOutput()
    {
        _config.SaveResultsToJson = false;
        return this;
    }

    #endregion

    /// <summary>
    /// 対象ファイルを検出。
    ///
    /// <para><b>【検出ロジック】</b></para>
    /// <list type="number">
    /// <item><description>SourceDirectory 配下のすべての .cs ファイルを列挙</description></item>
    /// <item><description>ExcludePatterns に一致するファイルを除外</description></item>
    /// <item><description>IncludePatterns が指定されている場合、一致するファイルのみを含める</description></item>
    /// </list>
    ///
    /// <para><b>【Why: yield return を使用する理由】</b></para>
    /// <para>
    /// 大量のファイルを扱う場合でも、遅延評価によりメモリ効率が良くなります。
    /// また、ファイル検出と変異生成を並行して実行できます。
    /// </para>
    /// </summary>
    /// <returns>
    /// 検出されたソースファイルの絶対パスのコレクション。
    /// ディレクトリが存在しない場合は空のコレクションを返します。
    /// </returns>
    public IEnumerable<string> DiscoverSourceFiles()
    {
        if (!Directory.Exists(_config.SourceDirectory))
        {
            _logger?.Invoke($"[ERROR] Source directory not found: {_config.SourceDirectory}");
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(_config.SourceDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(_config.SourceDirectory, file).Replace('\\', '/');

            // 除外パターンのチェック
            if (_config.ExcludePatterns.Any(p => relativePath.Contains(p, StringComparison.OrdinalIgnoreCase)))
                continue;

            // 包含パターンのチェック（指定されている場合のみ）
            if (_config.IncludePatterns.Count > 0 &&
                !_config.IncludePatterns.Any(p => relativePath.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            yield return file;
        }
    }

    /// <summary>
    /// 全ファイルに対して変異テストを実行。
    ///
    /// <para><b>【実行フロー】</b></para>
    /// <list type="number">
    /// <item><description>ソースファイルを検出</description></item>
    /// <item><description>並列で各ファイルから変異を生成</description></item>
    /// <item><description>並列で各変異をコンパイル・テスト</description></item>
    /// <item><description>結果を集計してレポートを作成</description></item>
    /// <item><description>オプションでJSON形式で結果を保存</description></item>
    /// </list>
    ///
    /// <para><b>【Why: 並列処理を使用する理由】</b></para>
    /// <para>
    /// 変異テストは非常に時間がかかる処理です（数百?数千の変異をテスト）。
    /// 並列処理により、マルチコアCPUを活用して実行時間を大幅に短縮できます。
    /// 例: 8コアCPUで7並列実行すると、理論上は7倍の高速化が可能です。
    /// </para>
    ///
    /// <para><b>【進捗表示】</b></para>
    /// <para>
    /// <see cref="MutationTestConfiguration.ProgressReportInterval"/> で指定された
    /// 間隔（デフォルト100）ごとに進捗が表示されます。
    /// </para>
    /// </summary>
    /// <returns>
    /// 変異テストの実行結果を含む <see cref="MutationTestReport"/>。
    /// ソースファイルが見つからない場合は、空のレポートを返します。
    /// </returns>
    public MutationTestReport RunAll()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var sourceFiles = DiscoverSourceFiles().ToList();

        _logger?.Invoke($"[INFO] Source directory: {_config.SourceDirectory}");
        _logger?.Invoke($"[INFO] Source files found: {sourceFiles.Count}");
        _logger?.Invoke($"[INFO] Max parallelism: {_config.MaxParallelism}");

        if (sourceFiles.Count == 0)
        {
            return MutationTestReporter.CreateEmptyReport(stopwatch.Elapsed, _config);
        }

        // 変異生成
        var allMutations = new ConcurrentBag<(SyntaxNode Root, MutationInfo Info)>();
        Parallel.ForEach(sourceFiles, new ParallelOptions { MaxDegreeOfParallelism = _config.MaxParallelism }, file =>
        {
            try
            {
                foreach (var mutation in MutationGenerator.GenerateFromFile(file))
                {
                    allMutations.Add(mutation);
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"[ERROR] File read error: {file} - {ex.Message}");
            }
        });

        _logger?.Invoke($"[INFO] Total mutations: {allMutations.Count}");

        // テスト実行
        var results = new ConcurrentBag<MutationTestResult>();
        var progress = 0;
        var total = allMutations.Count;

        Parallel.ForEach(allMutations, new ParallelOptions { MaxDegreeOfParallelism = _config.MaxParallelism }, mutation =>
        {
            var result = MutationTestExecutor.TestMutation(mutation.Root, mutation.Info, _config, _testCaseRegistry);
            results.Add(result);

            var current = System.Threading.Interlocked.Increment(ref progress);
            if (current % _config.ProgressReportInterval == 0 || current == total)
            {
                _logger?.Invoke($"[PROGRESS] Progress: {current}/{total} ({current * 100 / total}%)");
            }
        });

        stopwatch.Stop();
        return MutationTestReporter.CreateReport([.. results], stopwatch.Elapsed, _config, _logger);
    }

    /// <summary>
    /// 単一ファイルに対して変異テストを実行。
    ///
    /// <para><b>【用途】</b></para>
    /// <list type="bullet">
    /// <item><description>特定のファイルに絞って変異テストを実行</description></item>
    /// <item><description>デバッグやテストケースの開発時に有用</description></item>
    /// <item><description>CI/CDで変更されたファイルのみをテスト</description></item>
    /// </list>
    ///
    /// <para><b>【Why: RunAll() とは別メソッドにする理由】</b></para>
    /// <para>
    /// - ファイル単位でのテストは頻繁に使用されるため、専用メソッドを提供
    /// - パラメータで単一/全体を切り替えるより、メソッド名で意図が明確
    /// </para>
    /// </summary>
    /// <param name="relativePath">
    /// ソースディレクトリからの相対パス（例: "Core/Helpers/Calculator.cs"）。
    /// Windows/Linux両方で動作するよう、区切り文字は / を推奨します。
    /// </param>
    /// <returns>
    /// 変異テストの実行結果を含む <see cref="MutationTestReport"/>。
    /// ファイルが存在しない場合は、空のレポートを返します。
    /// </returns>
    /// <example>
    /// <code>
    /// var report = MutationTestRunner
    ///     .Create()
    ///     .Configure(projectName: "MyProject")
    ///     .WithTestCase(new CalculatorTestCase())
    ///     .RunForFile("Core/Helpers/Calculator.cs");
    ///
    /// Console.WriteLine($"変異スコア: {report.MutationScore:F1}%");
    /// </code>
    /// </example>
    public MutationTestReport RunForFile(string relativePath)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var filePath = Path.Combine(_config.SourceDirectory, relativePath);

        _logger?.Invoke($"[INFO] Target file: {filePath}");

        if (!File.Exists(filePath))
        {
            _logger?.Invoke($"[ERROR] File not found");
            return MutationTestReporter.CreateEmptyReport(stopwatch.Elapsed, _config);
        }

        var mutations = MutationGenerator.GenerateFromFile(filePath).ToList();
        _logger?.Invoke($"[INFO] Total mutations: {mutations.Count}");

        var results = new ConcurrentBag<MutationTestResult>();
        var progress = 0;
        var total = mutations.Count;

        Parallel.ForEach(mutations, new ParallelOptions { MaxDegreeOfParallelism = _config.MaxParallelism }, mutation =>
        {
            var result = MutationTestExecutor.TestMutation(mutation.Root, mutation.Info, _config, _testCaseRegistry);
            results.Add(result);

            var current = System.Threading.Interlocked.Increment(ref progress);
            if (current % 50 == 0 || current == total)
            {
                _logger?.Invoke($"[PROGRESS] Progress: {current}/{total} ({current * 100 / total}%)");
            }
        });

        stopwatch.Stop();
        return MutationTestReporter.CreateReport([.. results], stopwatch.Elapsed, _config, _logger);
    }
}
