using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.MutationFramework;

/// <summary>
/// ソースコードから変異を生成するクラス。
/// 
/// <para><b>【サポートする変異パターン】</b></para>
/// <list type="bullet">
/// <item><description>二項演算子: ==, !=, &lt;, &lt;=, &gt;, &gt;=, &amp;&amp;, ||, +, -, *, /</description></item>
/// <item><description>ブール定数: true ↔ false</description></item>
/// <item><description>数値リテラル: n → n+1, n → n-1, n → 0</description></item>
/// <item><description>インクリメント/デクリメント: ++, -- の反転</description></item>
/// <item><description>ビット演算子: &amp;, |, &lt;&lt;, &gt;&gt; の変異</description></item>
/// <item><description>文字列リテラル: 空文字列/null への置換</description></item>
/// <item><description>Null合体演算子: ?? のデフォルト値削除</description></item>
/// <item><description>三項演算子: 条件の論理否定</description></item>
/// <item><description>制御フロー: if条件の論理否定</description></item>
/// <item><description>LINQ メソッド: First/Last, Any/All, Take/Skip, OrderBy/OrderByDescending, FirstOrDefault/First など</description></item>
/// <item><description>Where 条件の反転</description></item>
/// </list>
/// 
/// <para><b>【AI生成コードの監査観点】</b></para>
/// <para>
/// 特にAI生成コードにおいて見落とされやすい以下のパターンを重点的に変異：
/// </para>
/// <list type="bullet">
/// <item><description>APIセマンティクスの誤解: FirstOrDefault/Single の不適切な使用</description></item>
/// <item><description>境界値エラー: インクリメント/デクリメントの誤用</description></item>
/// <item><description>Null安全性: Null合体演算子のデフォルト値依存</description></item>
/// <item><description>制御フロー: 条件分岐の論理反転</description></item>
/// </list>
/// </summary>
public static class MutationGenerator
{
    /// <summary>
    /// 二項演算子の変異パターン。
    /// </summary>
    private static readonly Dictionary<SyntaxKind, List<(SyntaxKind NewKind, MutationType Type)>> BinaryMutations = new()
    {
        [SyntaxKind.EqualsExpression] = [(SyntaxKind.NotEqualsExpression, MutationType.EqualToNotEqual)],
        [SyntaxKind.NotEqualsExpression] = [(SyntaxKind.EqualsExpression, MutationType.NotEqualToEqual)],
        [SyntaxKind.LessThanExpression] = [(SyntaxKind.LessThanOrEqualExpression, MutationType.LessThanToLessOrEqual), (SyntaxKind.GreaterThanExpression, MutationType.LessThanToGreaterThan)],
        [SyntaxKind.GreaterThanExpression] = [(SyntaxKind.GreaterThanOrEqualExpression, MutationType.GreaterThanToGreaterOrEqual), (SyntaxKind.LessThanExpression, MutationType.GreaterThanToLessThan)],
        [SyntaxKind.LessThanOrEqualExpression] = [(SyntaxKind.LessThanExpression, MutationType.LessOrEqualToLessThan), (SyntaxKind.GreaterThanOrEqualExpression, MutationType.LessOrEqualToGreaterOrEqual)],
        [SyntaxKind.GreaterThanOrEqualExpression] = [(SyntaxKind.GreaterThanExpression, MutationType.GreaterOrEqualToGreaterThan), (SyntaxKind.LessThanOrEqualExpression, MutationType.GreaterOrEqualToLessOrEqual)],
        [SyntaxKind.LogicalAndExpression] = [(SyntaxKind.LogicalOrExpression, MutationType.AndToOr)],
        [SyntaxKind.LogicalOrExpression] = [(SyntaxKind.LogicalAndExpression, MutationType.OrToAnd)],
        [SyntaxKind.AddExpression] = [(SyntaxKind.SubtractExpression, MutationType.AddToSubtract)],
        [SyntaxKind.SubtractExpression] = [(SyntaxKind.AddExpression, MutationType.SubtractToAdd)],
        [SyntaxKind.MultiplyExpression] = [(SyntaxKind.DivideExpression, MutationType.MultiplyToDivide)],
        [SyntaxKind.DivideExpression] = [(SyntaxKind.MultiplyExpression, MutationType.DivideToMultiply)],
        [SyntaxKind.BitwiseAndExpression] = [(SyntaxKind.BitwiseOrExpression, MutationType.BitwiseAndToOr)],
        [SyntaxKind.BitwiseOrExpression] = [(SyntaxKind.BitwiseAndExpression, MutationType.BitwiseOrToAnd)],
        [SyntaxKind.LeftShiftExpression] = [(SyntaxKind.RightShiftExpression, MutationType.LeftShiftToRightShift)],
        [SyntaxKind.RightShiftExpression] = [(SyntaxKind.LeftShiftExpression, MutationType.RightShiftToLeftShift)]
    };

    /// <summary>
    /// LINQメソッドの変異パターン。
    /// </summary>
    private static readonly Dictionary<string, List<(string NewName, MutationType Type)>> LinqMethodMutations = new()
    {
        ["First"] = [("Last", MutationType.FirstToLast)],
        ["Last"] = [("First", MutationType.LastToFirst)],
        ["FirstOrDefault"] = [("LastOrDefault", MutationType.FirstOrDefaultToLastOrDefault)],
        ["LastOrDefault"] = [("FirstOrDefault", MutationType.LastOrDefaultToFirstOrDefault)],
        ["Any"] = [("All", MutationType.AnyToAll)],
        ["All"] = [("Any", MutationType.AllToAny)],
        ["Take"] = [("Skip", MutationType.TakeToSkip)],
        ["Skip"] = [("Take", MutationType.SkipToTake)],
        ["OrderBy"] = [("OrderByDescending", MutationType.OrderByToOrderByDescending)],
        ["OrderByDescending"] = [("OrderBy", MutationType.OrderByDescendingToOrderBy)],
        ["ThenBy"] = [("ThenByDescending", MutationType.OrderByToOrderByDescending)],
        ["ThenByDescending"] = [("ThenBy", MutationType.OrderByDescendingToOrderBy)],
        ["Min"] = [("Max", MutationType.MinToMax)],
        ["Max"] = [("Min", MutationType.MaxToMin)],
        ["Single"] = [("First", MutationType.SingleToFirst)],
        ["SingleOrDefault"] = [("FirstOrDefault", MutationType.SingleToFirst)],
        ["Sum"] = [("Count", MutationType.SumToCount)],
        ["Count"] = [("Sum", MutationType.CountToSum)],
        ["FirstOrDefault"] = [("First", MutationType.FirstOrDefaultToFirst)],
        ["LastOrDefault"] = [("Last", MutationType.LastOrDefaultToLast)],
        ["SingleOrDefault"] = [("Single", MutationType.SingleOrDefaultToSingle)],
        ["Single"] = [("FirstOrDefault", MutationType.SingleToFirstOrDefault)]
    };

    /// <summary>
    /// ファイルから全ての変異を生成。
    /// </summary>
    /// <param name="filePath">ソースファイルのパス</param>
    /// <returns>変異した構文木と変異情報のコレクション</returns>
    public static IEnumerable<(SyntaxNode Root, MutationInfo Info)> GenerateFromFile(string filePath)
    {
        var sourceCode = File.ReadAllText(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        return Generate(filePath, root);
    }

    /// <summary>
    /// 構文木から全ての変異を生成。
    /// </summary>
    /// <param name="filePath">ファイルパス（エラーメッセージ用）</param>
    /// <param name="root">構文木のルート</param>
    /// <returns>変異した構文木と変異情報のコレクション</returns>
    public static IEnumerable<(SyntaxNode Root, MutationInfo Info)> Generate(string filePath, SyntaxNode root)
    {
        foreach (var m in GenerateBinaryMutations(filePath, root)) yield return m;
        foreach (var m in GenerateBooleanMutations(filePath, root)) yield return m;
        foreach (var m in GenerateNumericMutations(filePath, root)) yield return m;
        foreach (var m in GenerateUnaryMutations(filePath, root)) yield return m;
        foreach (var m in GenerateStringLiteralMutations(filePath, root)) yield return m;
        foreach (var m in GenerateNullCoalescingMutations(filePath, root)) yield return m;
        foreach (var m in GenerateConditionalExpressionMutations(filePath, root)) yield return m;
        foreach (var m in GenerateIfStatementMutations(filePath, root)) yield return m;
        foreach (var m in GenerateLinqMutations(filePath, root)) yield return m;
    }

    /// <summary>
    /// SyntaxKindに対応する演算子トークンを取得。
    /// </summary>
    private static SyntaxToken GetOperatorToken(SyntaxKind kind) => kind switch
    {
        SyntaxKind.EqualsExpression => SyntaxFactory.Token(SyntaxKind.EqualsEqualsToken),
        SyntaxKind.NotEqualsExpression => SyntaxFactory.Token(SyntaxKind.ExclamationEqualsToken),
        SyntaxKind.LessThanExpression => SyntaxFactory.Token(SyntaxKind.LessThanToken),
        SyntaxKind.GreaterThanExpression => SyntaxFactory.Token(SyntaxKind.GreaterThanToken),
        SyntaxKind.LessThanOrEqualExpression => SyntaxFactory.Token(SyntaxKind.LessThanEqualsToken),
        SyntaxKind.GreaterThanOrEqualExpression => SyntaxFactory.Token(SyntaxKind.GreaterThanEqualsToken),
        SyntaxKind.LogicalAndExpression => SyntaxFactory.Token(SyntaxKind.AmpersandAmpersandToken),
        SyntaxKind.LogicalOrExpression => SyntaxFactory.Token(SyntaxKind.BarBarToken),
        SyntaxKind.AddExpression => SyntaxFactory.Token(SyntaxKind.PlusToken),
        SyntaxKind.SubtractExpression => SyntaxFactory.Token(SyntaxKind.MinusToken),
        SyntaxKind.MultiplyExpression => SyntaxFactory.Token(SyntaxKind.AsteriskToken),
        SyntaxKind.DivideExpression => SyntaxFactory.Token(SyntaxKind.SlashToken),
        SyntaxKind.BitwiseAndExpression => SyntaxFactory.Token(SyntaxKind.AmpersandToken),
        SyntaxKind.BitwiseOrExpression => SyntaxFactory.Token(SyntaxKind.BarToken),
        SyntaxKind.LeftShiftExpression => SyntaxFactory.Token(SyntaxKind.LessThanLessThanToken),
        SyntaxKind.RightShiftExpression => SyntaxFactory.Token(SyntaxKind.GreaterThanGreaterThanToken),
        _ => throw new ArgumentException($"Unsupported SyntaxKind: {kind}")
    };

    /// <summary>
    /// 二項演算子の変異を生成。
    /// </summary>
    private static IEnumerable<(SyntaxNode Root, MutationInfo Info)> GenerateBinaryMutations(string filePath, SyntaxNode root)
    {
        foreach (var node in root.DescendantNodes().OfType<BinaryExpressionSyntax>())
        {
            if (!BinaryMutations.TryGetValue(node.Kind(), out var mutations)) continue;

            foreach (var (newKind, mutationType) in mutations)
            {
                var lineSpan = node.GetLocation().GetLineSpan();
                var mutatedNode = SyntaxFactory.BinaryExpression(newKind, node.Left, GetOperatorToken(newKind), node.Right);
                var mutatedRoot = root.ReplaceNode(node, mutatedNode);

                yield return (mutatedRoot, new MutationInfo(filePath, mutationType,
                    lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1,
                    node.ToString(), mutatedNode.ToString()));
            }
        }
    }

    /// <summary>
    /// ブール定数の変異を生成。
    /// </summary>
    private static IEnumerable<(SyntaxNode Root, MutationInfo Info)> GenerateBooleanMutations(string filePath, SyntaxNode root)
    {
        foreach (var node in root.DescendantNodes().OfType<LiteralExpressionSyntax>().Where(l => l.Kind() == SyntaxKind.TrueLiteralExpression))
        {
            var lineSpan = node.GetLocation().GetLineSpan();
            var mutatedNode = SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression);
            yield return (root.ReplaceNode(node, mutatedNode), new MutationInfo(filePath, MutationType.TrueToFalse,
                lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1, "true", "false"));
        }

        foreach (var node in root.DescendantNodes().OfType<LiteralExpressionSyntax>().Where(l => l.Kind() == SyntaxKind.FalseLiteralExpression))
        {
            var lineSpan = node.GetLocation().GetLineSpan();
            var mutatedNode = SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);
            yield return (root.ReplaceNode(node, mutatedNode), new MutationInfo(filePath, MutationType.FalseToTrue,
                lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1, "false", "true"));
        }
    }

    /// <summary>
    /// 数値リテラルの変異を生成。
    /// </summary>
    private static IEnumerable<(SyntaxNode Root, MutationInfo Info)> GenerateNumericMutations(string filePath, SyntaxNode root)
    {
        var literals = root.DescendantNodes().OfType<LiteralExpressionSyntax>()
            .Where(l => l.Kind() == SyntaxKind.NumericLiteralExpression && l.Token.Value is int or long or double or float or decimal);

        foreach (var node in literals)
        {
            var lineSpan = node.GetLocation().GetLineSpan();
            var value = node.Token.Value;

            if (value is int intVal && intVal != 0)
            {
                var mutatedNode = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0));
                yield return (root.ReplaceNode(node, mutatedNode), new MutationInfo(filePath, MutationType.NumericToZero,
                    lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1, node.ToString(), "0"));
            }

            if (value is int intVal2)
            {
                var mutatedNode = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(intVal2 + 1));
                yield return (root.ReplaceNode(node, mutatedNode), new MutationInfo(filePath, MutationType.NumericIncrement,
                    lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1, node.ToString(), (intVal2 + 1).ToString()));
            }

            if (value is int intVal3 && intVal3 > 0)
            {
                var mutatedNode = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(intVal3 - 1));
                yield return (root.ReplaceNode(node, mutatedNode), new MutationInfo(filePath, MutationType.NumericDecrement,
                    lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1, node.ToString(), (intVal3 - 1).ToString()));
            }
        }
    }

    /// <summary>
    /// LINQ関連の変異を生成。
    /// </summary>
    private static IEnumerable<(SyntaxNode Root, MutationInfo Info)> GenerateLinqMutations(string filePath, SyntaxNode root)
    {
        foreach (var m in GenerateLinqMethodNameMutations(filePath, root)) yield return m;
        foreach (var m in GenerateWhereConditionMutations(filePath, root)) yield return m;
    }

    /// <summary>
    /// LINQメソッド名の変異を生成（First → Last など）。
    /// </summary>
    private static IEnumerable<(SyntaxNode Root, MutationInfo Info)> GenerateLinqMethodNameMutations(string filePath, SyntaxNode root)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) continue;
            var methodName = memberAccess.Name.Identifier.Text;
            if (!LinqMethodMutations.TryGetValue(methodName, out var mutations)) continue;

            foreach (var (newName, mutationType) in mutations)
            {
                var lineSpan = invocation.GetLocation().GetLineSpan();
                var newMemberAccess = memberAccess.WithName(SyntaxFactory.IdentifierName(newName).WithTriviaFrom(memberAccess.Name));
                var mutatedInvocation = invocation.WithExpression(newMemberAccess);

                yield return (root.ReplaceNode(invocation, mutatedInvocation), new MutationInfo(filePath, mutationType,
                    lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1, $".{methodName}(", $".{newName}("));
            }
        }
    }

    /// <summary>
    /// Where条件の反転変異を生成。
    /// </summary>
    private static IEnumerable<(SyntaxNode Root, MutationInfo Info)> GenerateWhereConditionMutations(string filePath, SyntaxNode root)
    {
        var whereInvocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma && ma.Name.Identifier.Text == "Where");

        foreach (var invocation in whereInvocations)
        {
            if (invocation.ArgumentList.Arguments.Count == 0) continue;
            var firstArg = invocation.ArgumentList.Arguments[0].Expression;

            if (firstArg is SimpleLambdaExpressionSyntax simpleLambda)
            {
                var negatedBody = NegateExpression(simpleLambda.Body);
                if (negatedBody == null) continue;

                var lineSpan = invocation.GetLocation().GetLineSpan();
                var mutatedLambda = simpleLambda.WithBody(negatedBody);
                var mutatedArg = SyntaxFactory.Argument(mutatedLambda);
                var mutatedArgList = invocation.ArgumentList.WithArguments(SyntaxFactory.SingletonSeparatedList(mutatedArg));
                var mutatedInvocation = invocation.WithArgumentList(mutatedArgList);

                yield return (root.ReplaceNode(invocation, mutatedInvocation), new MutationInfo(filePath, MutationType.WhereConditionNegation,
                    lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1, simpleLambda.Body.ToString(), negatedBody.ToString()));
            }
            else if (firstArg is ParenthesizedLambdaExpressionSyntax parenLambda && parenLambda.Body is ExpressionSyntax bodyExpr)
            {
                var negatedBody = NegateExpression(bodyExpr);
                if (negatedBody == null) continue;

                var lineSpan = invocation.GetLocation().GetLineSpan();
                var mutatedLambda = parenLambda.WithBody(negatedBody);
                var mutatedArg = SyntaxFactory.Argument(mutatedLambda);
                var mutatedArgList = invocation.ArgumentList.WithArguments(SyntaxFactory.SingletonSeparatedList(mutatedArg));
                var mutatedInvocation = invocation.WithArgumentList(mutatedArgList);

                yield return (root.ReplaceNode(invocation, mutatedInvocation), new MutationInfo(filePath, MutationType.WhereConditionNegation,
                    lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1, bodyExpr.ToString(), negatedBody.ToString()));
            }
        }
    }

    /// <summary>
    /// 式を論理否定。
    /// </summary>
    private static ExpressionSyntax? NegateExpression(CSharpSyntaxNode body)
    {
        if (body is not ExpressionSyntax expr) return null;
        if (expr is PrefixUnaryExpressionSyntax prefixUnary && prefixUnary.OperatorToken.IsKind(SyntaxKind.ExclamationToken))
            return prefixUnary.Operand;
        return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, SyntaxFactory.ParenthesizedExpression(expr));
    }

    /// <summary>
    /// 単項演算子の変異を生成（インクリメント/デクリメント）。
    /// <para><b>【Why: AI生成コードの境界値エラー検出】</b></para>
    /// <para>
    /// AIはループカウンタの更新式を機械的に書くが、前置/後置の違いや
    /// 境界条件との組み合わせでoff-by-oneエラーを起こしやすい。
    /// </para>
    /// </summary>
    private static IEnumerable<(SyntaxNode Root, MutationInfo Info)> GenerateUnaryMutations(string filePath, SyntaxNode root)
    {
        foreach (var node in root.DescendantNodes().OfType<PrefixUnaryExpressionSyntax>())
        {
            var lineSpan = node.GetLocation().GetLineSpan();

            if (node.OperatorToken.IsKind(SyntaxKind.PlusPlusToken))
            {
                var mutatedNode = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.PreDecrementExpression, node.Operand);
                yield return (root.ReplaceNode(node, mutatedNode), new MutationInfo(filePath, MutationType.PreIncrementToPreDecrement,
                    lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1, node.ToString(), mutatedNode.ToString()));
            }
            else if (node.OperatorToken.IsKind(SyntaxKind.MinusMinusToken))
            {
                var mutatedNode = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.PreIncrementExpression, node.Operand);
                yield return (root.ReplaceNode(node, mutatedNode), new MutationInfo(filePath, MutationType.PreDecrementToPreIncrement,
                    lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1, node.ToString(), mutatedNode.ToString()));
            }
        }

        foreach (var node in root.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>())
        {
            var lineSpan = node.GetLocation().GetLineSpan();

            if (node.OperatorToken.IsKind(SyntaxKind.PlusPlusToken))
            {
                var mutatedNode = SyntaxFactory.PostfixUnaryExpression(SyntaxKind.PostDecrementExpression, node.Operand);
                yield return (root.ReplaceNode(node, mutatedNode), new MutationInfo(filePath, MutationType.PostIncrementToPostDecrement,
                    lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1, node.ToString(), mutatedNode.ToString()));
            }
            else if (node.OperatorToken.IsKind(SyntaxKind.MinusMinusToken))
            {
                var mutatedNode = SyntaxFactory.PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, node.Operand);
                yield return (root.ReplaceNode(node, mutatedNode), new MutationInfo(filePath, MutationType.PostDecrementToPostIncrement,
                    lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1, node.ToString(), mutatedNode.ToString()));
            }
        }
    }

    /// <summary>
    /// 文字列リテラルの変異を生成。
    /// <para><b>【Why: AI生成コードのNull安全性検証】</b></para>
    /// <para>
    /// AIはガード節で null チェックを書くが、空文字列への対応が漏れやすい。
    /// また、文字列の初期化で "" と null の区別が曖昧になることがある。
    /// </para>
    /// </summary>
    private static IEnumerable<(SyntaxNode Root, MutationInfo Info)> GenerateStringLiteralMutations(string filePath, SyntaxNode root)
    {
        var stringLiterals = root.DescendantNodes().OfType<LiteralExpressionSyntax>()
            .Where(l => l.Kind() == SyntaxKind.StringLiteralExpression && !string.IsNullOrEmpty(l.Token.ValueText));

        foreach (var node in stringLiterals)
        {
            var lineSpan = node.GetLocation().GetLineSpan();

            // 空文字列への変異
            var emptyString = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(""));
            yield return (root.ReplaceNode(node, emptyString), new MutationInfo(filePath, MutationType.StringLiteralToEmpty,
                lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1, node.ToString(), "\"\""));

            // nullへの変異
            var nullLiteral = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
            yield return (root.ReplaceNode(node, nullLiteral), new MutationInfo(filePath, MutationType.StringLiteralToNull,
                lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1, node.ToString(), "null"));
        }
    }

    /// <summary>
    /// Null合体演算子の変異を生成。
    /// <para><b>【Why: AI生成コードのデフォルト値依存検証】</b></para>
    /// <para>
    /// `value ?? defaultValue` のようなコードで、デフォルト値への依存が
    /// 本当に必要かをテストする。AIは防御的に ?? を多用するが、
    /// 実際には null が来ないケースも多い。
    /// </para>
    /// </summary>
    private static IEnumerable<(SyntaxNode Root, MutationInfo Info)> GenerateNullCoalescingMutations(string filePath, SyntaxNode root)
    {
        var coalesceExpressions = root.DescendantNodes().OfType<BinaryExpressionSyntax>()
            .Where(b => b.OperatorToken.IsKind(SyntaxKind.QuestionQuestionToken));

        foreach (var node in coalesceExpressions)
        {
            var lineSpan = node.GetLocation().GetLineSpan();

            // ?? の右辺（デフォルト値）を削除し、左辺のみにする
            var mutatedNode = node.Left;
            yield return (root.ReplaceNode(node, mutatedNode), new MutationInfo(filePath, MutationType.NullCoalescingRemoveDefault,
                lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1, node.ToString(), mutatedNode.ToString()));
        }
    }

    /// <summary>
    /// 三項演算子の条件を論理否定する変異を生成。
    /// <para><b>【Why: AI生成コードの条件分岐検証】</b></para>
    /// <para>
    /// AIは三項演算子を好んで使うが、条件の真偽が逆になっても
    /// コンパイルエラーにならないため、テストで検出する必要がある。
    /// </para>
    /// </summary>
    private static IEnumerable<(SyntaxNode Root, MutationInfo Info)> GenerateConditionalExpressionMutations(string filePath, SyntaxNode root)
    {
        var conditionals = root.DescendantNodes().OfType<ConditionalExpressionSyntax>();

        foreach (var node in conditionals)
        {
            var lineSpan = node.GetLocation().GetLineSpan();
            var negatedCondition = NegateExpression(node.Condition);
            if (negatedCondition == null) continue;

            var mutatedNode = SyntaxFactory.ConditionalExpression(negatedCondition, node.WhenTrue, node.WhenFalse);
            yield return (root.ReplaceNode(node, mutatedNode), new MutationInfo(filePath, MutationType.ConditionalExpressionNegate,
                lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1, node.Condition.ToString(), negatedCondition.ToString()));
        }
    }

    /// <summary>
    /// if文の条件を論理否定する変異を生成。
    /// <para><b>【Why: AI生成コードの制御フロー検証】</b></para>
    /// <para>
    /// AIはガード節を形式的に書くが、その後の処理で条件が逆になっても
    /// 気づかないことがある。特に早期リターンと組み合わせた場合に重要。
    /// </para>
    /// </summary>
    private static IEnumerable<(SyntaxNode Root, MutationInfo Info)> GenerateIfStatementMutations(string filePath, SyntaxNode root)
    {
        var ifStatements = root.DescendantNodes().OfType<IfStatementSyntax>();

        foreach (var node in ifStatements)
        {
            var lineSpan = node.GetLocation().GetLineSpan();
            var negatedCondition = NegateExpression(node.Condition);
            if (negatedCondition == null) continue;

            var mutatedNode = node.WithCondition(negatedCondition);
            yield return (root.ReplaceNode(node, mutatedNode), new MutationInfo(filePath, MutationType.IfConditionNegate,
                lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1, node.Condition.ToString(), negatedCondition.ToString()));
        }
    }
}
