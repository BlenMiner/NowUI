using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NowUI.Analyzers
{
    /// <summary>
    /// Flags the two provably-dead NowUI statement patterns. Both only fire on a
    /// bare expression statement whose value is an opted-in struct, which is 100%
    /// reliable: [NowBuilder] structs have no effect until consumed, and a
    /// discarded [NowScope] struct can never be disposed (those scopes have no
    /// public end-call alternative). Assign to '_' to opt out at a call site.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NowBuilderDiscardAnalyzer : DiagnosticAnalyzer
    {
        public static readonly DiagnosticDescriptor BuilderDiscarded = new DiagnosticDescriptor(
            "NOWUI001",
            "NowUI builder result discarded",
            "'{0}' renders nothing until it is consumed — did you forget .Draw() (or .Begin())?",
            "Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "NowUI builders are inert value types; discarding the chain draws nothing. Finish the chain with Draw()/Begin(), or assign to '_' to discard intentionally.");

        public static readonly DiagnosticDescriptor ScopeDiscarded = new DiagnosticDescriptor(
            "NOWUI002",
            "NowUI scope discarded without dispose",
            "'{0}' is a scope and is never disposed here — wrap it in a using statement",
            "Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "NowUI scopes (masks, fonts, themes, layout groups, control content) restore state on Dispose. Discarding one as a statement leaks the pushed state for the rest of the frame.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(BuilderDiscarded, ScopeDiscarded);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeExpressionStatement, SyntaxKind.ExpressionStatement);
        }

        static void AnalyzeExpressionStatement(SyntaxNodeAnalysisContext context)
        {
            var statement = (ExpressionStatementSyntax)context.Node;

            // Assignments (including '_ = ...') and increment/decrement are
            // intentional; only bare value-producing expressions are suspect.
            if (statement.Expression is AssignmentExpressionSyntax ||
                statement.Expression is PrefixUnaryExpressionSyntax ||
                statement.Expression is PostfixUnaryExpressionSyntax)
            {
                return;
            }

            var type = context.SemanticModel.GetTypeInfo(statement.Expression, context.CancellationToken).Type;

            if (type == null || type.TypeKind != TypeKind.Struct)
                return;

            // A chain ending in a [NowConsumer] call (Draw/Reserve) did its work;
            // the builder it returns exists only for further chaining.
            if (statement.Expression is InvocationExpressionSyntax invocation &&
                context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method)
            {
                foreach (var methodAttribute in method.GetAttributes())
                {
                    if (methodAttribute.AttributeClass?.Name == "NowConsumerAttribute")
                        return;
                }
            }

            foreach (var attribute in type.GetAttributes())
            {
                string name = attribute.AttributeClass?.Name;

                if (name == "NowBuilderAttribute")
                {
                    context.ReportDiagnostic(Diagnostic.Create(BuilderDiscarded, statement.GetLocation(), type.Name));
                    return;
                }

                if (name == "NowScopeAttribute")
                {
                    context.ReportDiagnostic(Diagnostic.Create(ScopeDiscarded, statement.GetLocation(), type.Name));
                    return;
                }
            }
        }
    }
}
