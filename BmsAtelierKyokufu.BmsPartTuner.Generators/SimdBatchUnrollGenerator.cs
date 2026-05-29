using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace BmsAtelierKyokufu.BmsPartTuner.Generators
{
    [Generator]
    public class SimdBatchUnrollGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Filter syntax trees for methods with our attribute
            var methodDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                    transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
                .Where(static m => m != null);

            // Generate source code for each method
            context.RegisterSourceOutput(methodDeclarations, static (spc, source) => Execute(spc, source));
        }

        private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
        {
            return node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0;
        }

        private static MethodDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
            var methodDeclarationSyntax = (MethodDeclarationSyntax)context.Node;

            foreach (var attributeListSyntax in methodDeclarationSyntax.AttributeLists)
            {
                foreach (var attributeSyntax in attributeListSyntax.Attributes)
                {
                    if (context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol is IMethodSymbol attributeSymbol)
                    {
                        string attributeName = attributeSymbol.ContainingType.ToDisplayString();
                        if (attributeName == "BmsAtelierKyokufu.BmsPartTuner.Core.Attributes.GenerateSimdBatchUnrollAttribute")
                        {
                            return methodDeclarationSyntax;
                        }
                    }
                }
            }

            return null;
        }

        private static void Execute(SourceProductionContext context, MethodDeclarationSyntax? methodSyntax)
        {
            if (methodSyntax == null) return;

            // Extract namespace, class name, and method details (simplified for brevity)
            if (methodSyntax.Parent is not ClassDeclarationSyntax classDecl) return;

            var namespaceDecl = classDecl.Parent as BaseNamespaceDeclarationSyntax;
            string namespaceName = namespaceDecl?.Name.ToString() ?? "BmsAtelierKyokufu.BmsPartTuner.Core.Audio";
            string className = classDecl.Identifier.Text;
            string methodName = methodSyntax.Identifier.Text;

            // Very basic attribute parsing
            int batchSize = 4;
            int unrollFactor = 4;
            string logicType = "";

            var attribute = methodSyntax.AttributeLists.SelectMany(al => al.Attributes)
                .FirstOrDefault(a => a.Name.ToString().Contains("GenerateSimdBatchUnroll"));

            if (attribute?.ArgumentList != null)
            {
                foreach (var arg in attribute.ArgumentList.Arguments)
                {
                    if (arg.NameEquals?.Name.Identifier.Text == "BatchSize")
                    {
                        if (int.TryParse(arg.Expression.ToString(), out int bs)) batchSize = bs;
                    }
                    else if (arg.NameEquals?.Name.Identifier.Text == "UnrollFactor")
                    {
                        if (int.TryParse(arg.Expression.ToString(), out int uf)) unrollFactor = uf;
                    }
                    else if (arg.NameEquals?.Name.Identifier.Text == "LogicType")
                    {
                        logicType = arg.Expression.ToString().Trim('"');
                    }
                }
            }

            bool isStatic = classDecl.Modifiers.Any(m => m.ValueText == "static");

            string source = GenerateMethodSource(namespaceName, className, methodName, unrollFactor, logicType, isStatic);
            context.AddSource($"{className}_{methodName}.g.cs", SourceText.From(source, Encoding.UTF8));
        }

        private static string GenerateMethodSource(string ns, string cls, string method, int unrollFactor, string logicType, bool isStatic)
        {
            string classModifiers = isStatic ? "public static partial class" : "public partial class";
            string methodBody = "";

            if (logicType == "PearsonNormalized")
            {
                var initDots = string.Join("\n", Enumerable.Range(0, unrollFactor).Select(k => $"            Vector<float> dot{k} = Vector<float>.Zero;"));

                var unrolledLoopBody = string.Join("\n", Enumerable.Range(0, unrollFactor).Select(k =>
                    $"                dot{k} += new Vector<float>(normalizedWav1.Slice(i + {k} * vectorSize, vectorSize)) * new Vector<float>(normalizedWav2.Slice(i + {k} * vectorSize, vectorSize));"));

                var sumDots = string.Join("\n", Enumerable.Range(0, unrollFactor).Select(k => $"            dotTotal += dot{k};"));

                methodBody = $$"""
        public static partial float {{method}}(ReadOnlySpan<float> normalizedWav1, ReadOnlySpan<float> normalizedWav2)
        {
            if (normalizedWav1.Length != normalizedWav2.Length || normalizedWav1.Length == 0) return 0.0F;
            int length = normalizedWav1.Length;
            int vectorSize = Vector<float>.Count;
            int unrollStep = vectorSize * {{unrollFactor}};
            int vectorizedLength = length - (length % unrollStep);

{{initDots}}

            for (int i = 0; i < vectorizedLength; i += unrollStep)
            {
{{unrolledLoopBody}}
            }

            Vector<float> dotTotal = Vector<float>.Zero;
{{sumDots}}
            float dotProduct = Vector.Dot(dotTotal, new Vector<float>(1.0f));

            // Remainder for remaining unrolled chunks
            int remainderStart = vectorizedLength;
            int vectorizedLength2 = length - (length % vectorSize);
            for (int i = remainderStart; i < vectorizedLength2; i += vectorSize)
            {
                dotProduct += Vector.Dot(new Vector<float>(normalizedWav1.Slice(i, vectorSize)) * new Vector<float>(normalizedWav2.Slice(i, vectorSize)), new Vector<float>(1.0f));
            }

            // Scalar remainder
            for (int i = vectorizedLength2; i < length; i++)
            {
                dotProduct += normalizedWav1[i] * normalizedWav2[i];
            }

            return Math.Max(-1.0f, Math.Min(1.0f, dotProduct));
        }
""";
            }
            else if (logicType == "PearsonStats")
            {
                var initVars = string.Join("\n", Enumerable.Range(0, unrollFactor).Select(k =>
                    $"            Vector<float> sx{k} = Vector<float>.Zero, sy{k} = Vector<float>.Zero, sx2_{k} = Vector<float>.Zero, sy2_{k} = Vector<float>.Zero, sxy{k} = Vector<float>.Zero;"));

                var unrolledLoopBody = string.Join("\n", Enumerable.Range(0, unrollFactor).Select(k =>
                    $$"""
                Vector<float> x{{k}} = new Vector<float>(wav1.Slice(i + {{k}} * vectorSize, vectorSize));
                Vector<float> y{{k}} = new Vector<float>(wav2.Slice(i + {{k}} * vectorSize, vectorSize));
                sx{{k}} += x{{k}}; sy{{k}} += y{{k}}; sx2_{{k}} += x{{k}} * x{{k}}; sy2_{{k}} += y{{k}} * y{{k}}; sxy{{k}} += x{{k}} * y{{k}};
"""));

                var sumVars = string.Join("\n", Enumerable.Range(0, unrollFactor).Select(k =>
                    $"            totX += sx{k}; totY += sy{k}; totX2 += sx2_{k}; totY2 += sy2_{k}; totXY += sxy{k};"));

                methodBody = $$"""
        private static partial void {{method}}(ReadOnlySpan<float> wav1, ReadOnlySpan<float> wav2, int vectorizedLength, int vectorSize, out float sumX, out float sumY, out float sumX2, out float sumY2, out float sumXY)
        {
            int unrollStep = vectorSize * {{unrollFactor}};
            int unrolledLen = vectorizedLength - (vectorizedLength % unrollStep);

{{initVars}}

            for (int i = 0; i < unrolledLen; i += unrollStep)
            {
{{unrolledLoopBody}}
            }

            Vector<float> totX = Vector<float>.Zero, totY = Vector<float>.Zero, totX2 = Vector<float>.Zero, totY2 = Vector<float>.Zero, totXY = Vector<float>.Zero;
{{sumVars}}

            for (int i = unrolledLen; i < vectorizedLength; i += vectorSize)
            {
                Vector<float> x = new Vector<float>(wav1.Slice(i, vectorSize));
                Vector<float> y = new Vector<float>(wav2.Slice(i, vectorSize));
                totX += x; totY += y; totX2 += x * x; totY2 += y * y; totXY += x * y;
            }

            Vector<float> ones = new Vector<float>(1.0f);
            sumX = Vector.Dot(totX, ones);
            sumY = Vector.Dot(totY, ones);
            sumX2 = Vector.Dot(totX2, ones);
            sumY2 = Vector.Dot(totY2, ones);
            sumXY = Vector.Dot(totXY, ones);
        }
""";
            }

            return $$"""
// <auto-generated/>
using System;
using System.Numerics;
using Vector = System.Numerics.Vector;

namespace {{ns}}
{
    {{classModifiers}} {{cls}}
    {
{{methodBody}}
    }
}
""";
        }
    }
}
