using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.MutationFramework;

/// <summary>
/// 変異したコードをメモリ上でコンパイルするクラス。
/// 
/// <para><b>【Why: メモリ上でコンパイルする理由】</b></para>
/// <list type="bullet">
/// <item><description>ディスクI/Oを避けることで高速化</description></item>
/// <item><description>一時ファイルの管理が不要</description></item>
/// <item><description>並列処理で複数の変異を同時にコンパイル可能</description></item>
/// </list>
/// 
/// <para><b>【デフォルト参照アセンブリ】</b></para>
/// <para>
/// System.Runtime, System.Collections, System.Linq, Console などの
/// 基本的なアセンブリは自動的に含まれます。
/// </para>
/// </summary>
public static class MutationCompiler
{
    /// <summary>
    /// デフォルトで参照されるメタデータ参照のリスト。
    /// <para>
    /// <b>Why:</b> 一般的なC#コードで使用される基本的な型を含めることで、
    /// ほとんどのコードが追加の参照なしでコンパイル可能になります。
    /// </para>
    /// </summary>
    private static readonly List<MetadataReference> DefaultReferences;

    static MutationCompiler()
    {
        DefaultReferences =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),        // mscorlib/System.Private.CoreLib
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),    // System.Linq
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),       // System.Console
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location)
        ];

        // System.Runtime.Extensions は環境によって存在しない場合がある
        try
        {
            DefaultReferences.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Runtime.Extensions").Location));
        }
        catch { }
    }

    /// <summary>
    /// 構文木をメモリ上でコンパイル。
    /// 
    /// <para><b>【処理フロー】</b></para>
    /// <list type="number">
    /// <item><description>デフォルト参照と追加参照をマージ</description></item>
    /// <item><description>CSharpCompilation を作成</description></item>
    /// <item><description>MemoryStream にコンパイル結果を出力</description></item>
    /// <item><description>成功時は CollectibleAssemblyLoadContext でアセンブリをロード</description></item>
    /// </list>
    /// 
    /// <para><b>【Why: CollectibleAssemblyLoadContext を使用する理由】</b></para>
    /// <para>
    /// 変異テストでは大量のアセンブリをロードするため、メモリリークを防ぐために
    /// アンロード可能な AssemblyLoadContext を使用します。
    /// ガベージコレクションで自動的にアンロードされます。
    /// </para>
    /// </summary>
    /// <param name="syntaxTree">
    /// コンパイル対象の構文木。
    /// 通常は <see cref="CSharpSyntaxTree.ParseText"/> で作成します。
    /// </param>
    /// <param name="additionalReferences">
    /// 追加のメタデータ参照。
    /// プロジェクト固有の依存アセンブリがある場合に指定します。
    /// null の場合、デフォルト参照のみが使用されます。
    /// </param>
    /// <returns>
    /// コンパイルされたアセンブリと、コンパイルエラーのタプル。
    /// <para>
    /// <b>Assembly:</b> コンパイル成功時はアセンブリ、失敗時は null<br/>
    /// <b>Errors:</b> コンパイルエラーの診断情報のコレクション
    /// </para>
    /// </returns>
    /// <example>
    /// <code>
    /// var syntaxTree = CSharpSyntaxTree.ParseText("public class Test { }");
    /// var (assembly, errors) = MutationCompiler.Compile(syntaxTree);
    /// 
    /// if (assembly != null)
    /// {
    ///     // コンパイル成功
    ///     var type = assembly.GetType("Test");
    /// }
    /// else
    /// {
    ///     // コンパイル失敗
    ///     foreach (var error in errors)
    ///     {
    ///         Console.WriteLine(error.GetMessage());
    ///     }
    /// }
    /// </code>
    /// </example>
    public static (Assembly? Assembly, IEnumerable<Diagnostic> Errors) Compile(
        SyntaxTree syntaxTree,
        IEnumerable<MetadataReference>? additionalReferences = null)
    {
        var references = DefaultReferences.ToList();
        if (additionalReferences != null)
        {
            references.AddRange(additionalReferences);
        }

        var compilation = CSharpCompilation.Create(
            $"MutationAssembly_{Guid.NewGuid():N}",  // 一意の名前を生成
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            // コンパイルエラーのみを返す（警告は除外）
            return (null, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        }

        ms.Seek(0, SeekOrigin.Begin);
        var context = new CollectibleAssemblyLoadContext();
        return (context.LoadFromStream(ms), Enumerable.Empty<Diagnostic>());
    }
}

/// <summary>
/// アンロード可能なAssemblyLoadContext。
/// 
/// <para><b>【Why: この実装が必要な理由】</b></para>
/// <list type="bullet">
/// <item><description>
/// 変異テストでは数百〜数千のアセンブリをロードするため、
/// 通常の AssemblyLoadContext ではメモリリークが発生します。
/// </description></item>
/// <item><description>
/// isCollectible: true を指定することで、GC でアンロード可能になります。
/// </description></item>
/// <item><description>
/// 各変異ごとに新しいコンテキストを作成することで、アセンブリの衝突を回避します。
/// </description></item>
/// </list>
/// 
/// <para><b>【注意事項】</b></para>
/// <para>
/// - このコンテキストでロードされたアセンブリは、コンテキストへの参照がなくなると GC の対象になります
/// - 長時間参照を保持すると、メモリリークの原因になる可能性があります
/// </para>
/// </summary>
internal class CollectibleAssemblyLoadContext : AssemblyLoadContext
{
    /// <summary>
    /// アンロード可能な AssemblyLoadContext を初期化。
    /// </summary>
    public CollectibleAssemblyLoadContext() : base(isCollectible: true) { }
}
