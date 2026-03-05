using System.Reflection;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.MutationFramework;

/// <summary>
/// 特定の型に対するテストケースを定義するインターフェース。
/// 
/// <para><b>【目的】</b></para>
/// <para>
/// 変異したコードが正しく動作しないことを検証するカスタムテストロジックを提供します。
/// 汎用的なテストでは検出できない変異を、型固有のロジックで検出できます。
/// </para>
/// 
/// <para><b>【実装ガイド】</b></para>
/// <list type="number">
/// <item><description><see cref="TypeName"/> で対象の型名（名前空間なし）を返す</description></item>
/// <item><description><see cref="TestMutant"/> でリフレクションを使って型をテストする</description></item>
/// <item><description>変異が検出された場合は true、検出されなかった場合は false を返す</description></item>
/// </list>
/// 
/// <para><b>【Why: なぜカスタムテストケースが必要か】</b></para>
/// <para>
/// - 汎用テストは引数なしのメソッドしか実行できない
/// - 複雑な入出力パターンを持つメソッドを正確にテストするため
/// - ビジネスロジックの正確性を保証するため
/// </para>
/// </summary>
/// <example>
/// <code>
/// public class CalculatorTestCase : IMutantTestCase
/// {
///     public string TypeName => "Calculator";
///     
///     public bool TestMutant(Assembly assembly)
///     {
///         var type = assembly.GetType("MyApp.Calculator");
///         if (type == null) return true;
///         
///         var addMethod = type.GetMethod("Add", new[] { typeof(int), typeof(int) });
///         if (addMethod != null)
///         {
///             try
///             {
///                 var result = (int)addMethod.Invoke(null, new object[] { 2, 3 });
///                 
///                 // 期待値 5 と異なる場合は変異が検出された (Killed)
///                 if (result != 5) return true;
///             }
///             catch
///             {
///                 // 例外が発生した場合も変異が検出された (Killed)
///                 return true;
///             }
///         }
///         
///         // すべてのテストをパスした場合は変異が生存 (Survived)
///         return false;
///     }
/// }
/// </code>
/// </example>
public interface IMutantTestCase
{
    /// <summary>
    /// 対象の型名（名前空間なし）。
    /// <para>
    /// <b>Why:</b> ファイル名と型名が一致することを前提としています。
    /// 例: "Calculator.cs" の場合、TypeName は "Calculator" にします。
    /// </para>
    /// </summary>
    string TypeName { get; }

    /// <summary>
    /// 変異したアセンブリをテストし、変異が検出されたかどうかを返す。
    /// 
    /// <para><b>【重要な返り値の意味】</b></para>
    /// <list type="table">
    /// <item>
    /// <term>true</term>
    /// <description>変異が検出された (Killed) - テストが正常に機能している</description>
    /// </item>
    /// <item>
    /// <term>false</term>
    /// <description>変異が生存 (Survived) - テストが不十分、または変異が等価</description>
    /// </item>
    /// </list>
    /// 
    /// <para><b>【Why: 返り値が反直感的な理由】</b></para>
    /// <para>
    /// 「変異が検出された = true」は、「テストが失敗した = true」ではなく、
    /// 「変異によってコードの動作が変わったことを検出できた = true」を意味します。
    /// つまり、テストの品質が高いことを示します。
    /// </para>
    /// </summary>
    /// <param name="assembly">
    /// 変異したコードがコンパイルされたアセンブリ。
    /// リフレクションを使用して型やメソッドにアクセスします。
    /// </param>
    /// <returns>
    /// true = 変異が検出された（Killed、良いテスト）<br/>
    /// false = 変異が生存（Survived、改善が必要）
    /// </returns>
    bool TestMutant(Assembly assembly);
}

/// <summary>
/// テストケースを登録・管理するレジストリ。
/// 
/// <para><b>【Why: レジストリパターンを使用する理由】</b></para>
/// <para>
/// - 型名とテストケースの対応を一元管理
/// - ランタイムでの動的な登録・検索を可能にする
/// - 大文字小文字を区別しない検索（柔軟性）
/// </para>
/// </summary>
/// <example>
/// <code>
/// var registry = new MutantTestCaseRegistry();
/// registry.Register(new CalculatorTestCase());
/// registry.Register(new StringUtilsTestCase());
/// 
/// var testCase = registry.GetTestCase("Calculator");
/// if (testCase != null)
/// {
///     bool isKilled = testCase.TestMutant(assembly);
/// }
/// </code>
/// </example>
public class MutantTestCaseRegistry
{
    /// <summary>
    /// 型名をキー、テストケースを値とする辞書。
    /// <para>
    /// <b>Why:</b> 大文字小文字を区別しない比較を行うことで、
    /// ファイル名の大文字小文字の違いに対応します。
    /// </para>
    /// </summary>
    private readonly Dictionary<string, IMutantTestCase> _testCases = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// テストケースを登録。
    /// <para>
    /// <b>注意:</b> 同じ型名で複数回登録すると、後のものが上書きされます。
    /// </para>
    /// </summary>
    /// <param name="testCase">登録するテストケース</param>
    public void Register(IMutantTestCase testCase)
    {
        _testCases[testCase.TypeName] = testCase;
    }

    /// <summary>
    /// 型名に対応するテストケースを取得。
    /// </summary>
    /// <param name="typeName">検索する型名（大文字小文字は区別されません）</param>
    /// <returns>
    /// 対応するテストケース。登録されていない場合は null。
    /// </returns>
    public IMutantTestCase? GetTestCase(string typeName)
    {
        return _testCases.TryGetValue(typeName, out var testCase) ? testCase : null;
    }

    /// <summary>
    /// 登録されているすべてのテストケースを取得。
    /// <para>
    /// <b>用途:</b> デバッグやテストカバレッジの確認に使用。
    /// </para>
    /// </summary>
    /// <returns>登録されているすべてのテストケースのコレクション</returns>
    public IEnumerable<IMutantTestCase> GetAll() => _testCases.Values;
}
