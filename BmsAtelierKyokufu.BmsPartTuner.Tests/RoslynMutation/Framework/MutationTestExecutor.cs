using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.RoslynMutation.Framework;

/// <summary>
/// 変異のビルドと実行検証を担当するクラス。
/// </summary>
internal static class MutationTestExecutor
{
    public static MutationTestResult TestMutation(
        SyntaxNode mutatedRoot,
        MutationInfo info,
        MutationTestConfiguration config,
        MutantTestCaseRegistry testCaseRegistry)
    {
        var (assembly, _) = MutationCompiler.Compile(mutatedRoot.SyntaxTree, config.AdditionalReferences);

        if (assembly == null)
        {
            return new MutationTestResult(info, true, "コンパイルエラー");
        }

        var isKilled = CheckIfMutantIsKilled(assembly, info, testCaseRegistry);
        return new MutationTestResult(info, isKilled);
    }

    private static bool CheckIfMutantIsKilled(Assembly assembly, MutationInfo info, MutantTestCaseRegistry testCaseRegistry)
    {
        try
        {
            var typeName = Path.GetFileNameWithoutExtension(info.FilePath);

            // 登録されたテストケースがあれば使用
            var testCase = testCaseRegistry.GetTestCase(typeName);
            if (testCase != null)
            {
                return testCase.TestMutant(assembly);
            }

            // 汎用テスト
            return GenericMutantTest(assembly, typeName);
        }
        catch
        {
            return true; // 例外 = Killed
        }
    }

    private static bool GenericMutantTest(Assembly assembly, string typeName)
    {
        var types = assembly.GetTypes().Where(t => t.Name == typeName || t.Name.EndsWith(typeName)).ToList();
        if (types.Count == 0) return true;

        foreach (var type in types)
        {
            try
            {
                // 静的メソッドをテスト
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.GetParameters().Length == 0))
                {
                    try { method.Invoke(null, null); }
                    catch { return true; }
                }

                // インスタンスメソッドをテスト
                if (type.GetConstructor(Type.EmptyTypes) != null)
                {
                    var instance = Activator.CreateInstance(type);
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.GetParameters().Length == 0 && m.ReturnType != typeof(void)))
                    {
                        try { method.Invoke(instance, null); }
                        catch { return true; }
                    }
                }
            }
            catch { return true; }
        }

        return false;
    }
}
