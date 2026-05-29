using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.RoslynMutation.Framework;

/// <summary>
/// 変異テスト実行結果のレポート生成と保存・出力を担当するクラス。
/// </summary>
internal static class MutationTestReporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static MutationTestReport CreateReport(
        List<MutationTestResult> results,
        TimeSpan duration,
        MutationTestConfiguration config,
        Action<string>? logger)
    {
        var killed = results.Count(r => r.IsKilled);
        var survived = results.Count(r => !r.IsKilled);
        var compileErrors = results.Count(r => r.ErrorMessage == "コンパイルエラー");
        var score = results.Count > 0 ? (double)killed / results.Count * 100 : 0;

        var report = new MutationTestReport
        {
            Timestamp = DateTime.Now,
            SourceDirectory = config.SourceDirectory,
            TotalMutations = results.Count,
            Killed = killed,
            Survived = survived,
            CompileErrors = compileErrors,
            MutationScore = score,
            Duration = duration,
            Mutations = [.. results.Select(r => new MutationResultDto
            {
                FilePath = Path.GetRelativePath(config.SourceDirectory, r.Mutation.FilePath),
                MutationType = r.Mutation.Type.ToString(),
                Line = r.Mutation.Line,
                Column = r.Mutation.Column,
                OriginalCode = r.Mutation.OriginalCode,
                MutatedCode = r.Mutation.MutatedCode,
                IsKilled = r.IsKilled,
                ErrorMessage = r.ErrorMessage
            })]
        };

        LogReport(report, logger);

        if (config.SaveResultsToJson)
        {
            SaveReportToJson(report, "MutationTest", config, logger);
        }

        return report;
    }

    public static MutationTestReport CreateEmptyReport(TimeSpan duration, MutationTestConfiguration config)
    {
        return new MutationTestReport
        {
            Timestamp = DateTime.Now,
            SourceDirectory = config.SourceDirectory,
            Duration = duration
        };
    }

    private static void LogReport(MutationTestReport report, Action<string>? logger)
    {
        logger?.Invoke("\n========================================");
        logger?.Invoke("=== Mutation Test Report ===");
        logger?.Invoke("========================================");
        logger?.Invoke($"[TIME] Execution time: {report.Duration.TotalSeconds:F1}s");
        logger?.Invoke($"[TOTAL] Total mutations: {report.TotalMutations}");
        logger?.Invoke($"[KILLED] Killed: {report.Killed} ({(report.TotalMutations > 0 ? report.Killed * 100.0 / report.TotalMutations : 0):F1}%)");
        logger?.Invoke($"  +-- Compile errors: {report.CompileErrors}");
        logger?.Invoke($"  +-- Detected by tests: {report.Killed - report.CompileErrors}");
        logger?.Invoke($"[SURVIVED] Survived: {report.Survived} ({(report.TotalMutations > 0 ? report.Survived * 100.0 / report.TotalMutations : 0):F1}%)");
        logger?.Invoke($"[SCORE] Mutation score: {report.MutationScore:F1}%");
        logger?.Invoke("========================================");

        if (report.Survived > 0)
        {
            logger?.Invoke("\n=== Survived Mutations (Details) ===");
            var survivedByType = report.Mutations.Where(m => !m.IsKilled).GroupBy(m => m.MutationType).OrderByDescending(g => g.Count());

            foreach (var group in survivedByType.Take(5))
            {
                logger?.Invoke($"\n[TYPE] {group.Key}: {group.Count()} mutations");
                foreach (var result in group.Take(3))
                {
                    logger?.Invoke($"  [FILE] {result.FilePath}:{result.Line}");
                    logger?.Invoke($"     {result.OriginalCode} -> {result.MutatedCode}");
                }
                if (group.Count() > 3)
                {
                    logger?.Invoke($"  ... and {group.Count() - 3} more");
                }
            }
        }
    }

    private static void SaveReportToJson(MutationTestReport report, string testName, MutationTestConfiguration config, Action<string>? logger)
    {
        var resultsDir = Path.IsPathRooted(config.ResultsDirectory)
            ? config.ResultsDirectory
            : Path.GetFullPath(config.ResultsDirectory);

        Directory.CreateDirectory(resultsDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{testName}_{timestamp}.json";
        var filePath = Path.Combine(resultsDir, fileName);

        File.WriteAllText(filePath, JsonSerializer.Serialize(report, SerializerOptions));
        logger?.Invoke($"[SAVED] Results saved to: {filePath}");
    }
}
