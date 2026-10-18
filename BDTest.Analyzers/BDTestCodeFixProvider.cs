using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Operations;

namespace BDTest.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BDTestCodeFixProvider)), Shared]
public sealed class BDTestCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("BDT001", "BDT004");
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null || model == null) return;
        var diagnostic = context.Diagnostics.First();
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        SyntaxNode replacementRoot;
        string title;
        if (diagnostic.Id == "BDT001")
        {
            var statement = node.FirstAncestorOrSelf<ExpressionStatementSyntax>();
            if (statement == null || !CanAwait(statement, model, context.CancellationToken)) return;
            var expression = BDTestAnalyzer.DiscardedExpression(statement, model);
            if (BDTestAnalyzer.IsBuilder(model.GetTypeInfo(expression).Type) &&
                !BDTestAnalyzer.IsRunnable(model.GetTypeInfo(expression).Type)) return;
            var awaited = SyntaxFactory.ExpressionStatement(Await(expression)).WithTriviaFrom(statement);
            replacementRoot = root.ReplaceNode(statement, awaited.WithAdditionalAnnotations(Formatter.Annotation));
            title = "Await the BDTest scenario";
        }
        else
        {
            var argument = node.AncestorsAndSelf().OfType<ArgumentSyntax>().FirstOrDefault(a =>
                model.GetOperation(a) is IArgumentOperation { Parameter.Type: INamedTypeSymbol { DelegateInvokeMethod.ReturnsVoid: true } });
            if (argument == null || model.GetOperation(argument) is not IArgumentOperation operation ||
                BDTestAnalyzer.Unwrap(operation.Value) is not IAnonymousFunctionOperation { Syntax: LambdaExpressionSyntax lambda }) return;
            var discarded = BDTestAnalyzer.DiscardedAwaitables(lambda, model).ToArray();
            LambdaExpressionSyntax updated;
            // An assignment discard is an entire statement, not an assignable await expression.
            if (lambda.Body is BlockSyntax block)
            {
                var statements = discarded.Select(e => e.FirstAncestorOrSelf<ExpressionStatementSyntax>())
                    .OfType<ExpressionStatementSyntax>().Distinct().ToArray();
                updated = lambda.WithBody(block.ReplaceNodes(statements, (original, _) =>
                    SyntaxFactory.ExpressionStatement(Await(BDTestAnalyzer.DiscardedExpression(original, model))).WithTriviaFrom(original)));
            }
            else
            {
                updated = lambda.ReplaceNodes(discarded, (original, _) => Await(original));
            }
            updated = updated switch
            {
                SimpleLambdaExpressionSyntax simple => simple.WithAsyncKeyword(AsyncKeyword()),
                ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.WithAsyncKeyword(AsyncKeyword()),
                _ => updated
            };
            // Explicitly select Task, including on compilers without overload resolution priority.
            var taskDelegate = SyntaxFactory.CastExpression(
                SyntaxFactory.ParseTypeName("global::System.Func<global::System.Threading.Tasks.Task>"),
                SyntaxFactory.ParenthesizedExpression(updated.WithoutTrivia()));
            replacementRoot = root.ReplaceNode(argument.Expression,
                taskDelegate.WithTriviaFrom(argument.Expression).WithAdditionalAnnotations(Formatter.Annotation));
            title = "Await asynchronous work in a Task step";
        }

        var document = context.Document.WithSyntaxRoot(replacementRoot);
        var compilation = await document.Project.GetCompilationAsync(context.CancellationToken).ConfigureAwait(false);
        // Do not offer transformations that need a signature change or produce invalid await expressions.
        if (compilation == null || compilation.GetDiagnostics(context.CancellationToken).Any(d => d.Severity == DiagnosticSeverity.Error)) return;
        context.RegisterCodeFix(CodeAction.Create(title, _ => Task.FromResult(document), diagnostic.Id), diagnostic);
    }

    private static SyntaxToken AsyncKeyword() => SyntaxFactory.Token(SyntaxKind.AsyncKeyword).WithTrailingTrivia(SyntaxFactory.Space);

    private static AwaitExpressionSyntax Await(ExpressionSyntax expression) => SyntaxFactory.AwaitExpression(
        SyntaxFactory.ParenthesizedExpression(expression.WithoutTrivia())).WithTriviaFrom(expression);

    private static bool CanAwait(SyntaxNode statement, SemanticModel model, CancellationToken cancellationToken)
    {
        var function = statement.Ancestors().FirstOrDefault(n => n is AnonymousFunctionExpressionSyntax or
            MethodDeclarationSyntax or LocalFunctionStatementSyntax);
        IMethodSymbol? symbol = function switch
        {
            MethodDeclarationSyntax method => model.GetDeclaredSymbol(method, cancellationToken),
            LocalFunctionStatementSyntax local => model.GetDeclaredSymbol(local, cancellationToken) as IMethodSymbol,
            AnonymousFunctionExpressionSyntax lambda => (model.GetOperation(lambda, cancellationToken) as IAnonymousFunctionOperation)?.Symbol,
            _ => null
        };
        return symbol is { IsAsync: true, ReturnsVoid: false };
    }
}
