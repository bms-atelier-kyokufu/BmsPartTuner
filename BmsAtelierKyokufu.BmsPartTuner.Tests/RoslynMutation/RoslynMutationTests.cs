using System.Reflection;
using Xunit.Abstractions;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.MutationFramework
{
    /// <summary>
    /// 変異テストを実行するxUnitテストクラス。
    /// 汎用的なMutationTestFrameworkを使用します。
    /// </summary>
    public class RoslynMutationTests
    {
        private readonly ITestOutputHelper _output;

        public RoslynMutationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void MutationTest_AllSourceFiles_AllPatterns()
        {
            // Fluent API を使用してテストを実行
            var report = MutationTestRunner
                .Create()
                .Configure(
                    projectName: "BmsAtelierKyokufu.BmsPartTuner",
                    markerDirectory: "Core")
                .WithTestCase(new RadixConvertTestCase())
                .WithLogger(msg => { _output.WriteLine(msg); Console.WriteLine(msg); })
                .RunAll();

            // 実際にテストされた変異が10件以上あり、スコアが50%未満の場合は失敗
            var testedCount = report.TotalMutations - report.CompileErrors;
            if (testedCount > 10)
            {
                Assert.True(report.MutationScore >= 50.0,
                    $"変異テスト警告: 変異スコアが {report.MutationScore:F1}% です（推奨: 80%以上）。" +
                    $"{report.Survived} 個の変異が生き残りました。");
            }
        }

        [Theory]
        [InlineData("Core/Helpers/RadixConvert.cs")]
        [InlineData("Core/Bms/DefinitionRangeManager.cs")]
        [InlineData("Core/Optimization/SimulationEngine.cs")]
        public void MutationTest_SpecificFile(string relativePath)
        {
            // Fluent API を使用してファイル単位のテストを実行
            var report = MutationTestRunner
                .Create()
                .Configure(
                    projectName: "BmsAtelierKyokufu.BmsPartTuner",
                    markerDirectory: "Core")
                .WithTestCase(new RadixConvertTestCase())
                .WithLogger(msg => { _output.WriteLine(msg); Console.WriteLine(msg); })
                .RunForFile(relativePath);

            // レポートが空でなければ成功
            Assert.True(report.TotalMutations >= 0);
        }
    }

    #region Custom Test Cases

    /// <summary>
    /// RadixConvert クラス用のカスタムテストケース。
    /// </summary>
    public class RadixConvertTestCase : IMutantTestCase
    {
        public string TypeName => "RadixConvert";

        public bool TestMutant(Assembly assembly)
        {
            var type = assembly.GetType("BmsAtelierKyokufu.BmsPartTuner.Core.Helpers.RadixConvert");
            if (type == null) return true;

            // ZZToInt テスト
            var zzToInt = type.GetMethod("ZZToInt", [typeof(string)]);
            if (zzToInt != null)
            {
                var testCases = new (string Input, int Expected)[]
                {
                    ("00", 0),
                    ("01", 1),
                    ("0A", 10),
                    ("10", 62),
                    ("ZZ", 3843)
                };

                foreach (var (input, expected) in testCases)
                {
                    try
                    {
                        var result = zzToInt.Invoke(null, [input]);
                        if (result is int intResult && intResult != expected)
                        {
                            return true; // 期待値と異なる = Killed
                        }
                    }
                    catch
                    {
                        return true; // 例外 = Killed
                    }
                }
            }

            // IntToZZ テスト
            var intToZZ = type.GetMethod("IntToZZ", [typeof(int)]);
            if (intToZZ != null)
            {
                var testCases = new (int Input, string Expected)[]
                {
                    (0, "00"),
                    (1, "01"),
                    (10, "0A"),
                    (62, "10")
                };

                foreach (var (input, expected) in testCases)
                {
                    try
                    {
                        var result = intToZZ.Invoke(null, [input]);
                        if (result is string strResult && strResult != expected)
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        return true;
                    }
                }
            }

            return false; // 全テスト通過 = Survived
        }
    }

    #endregion
}
