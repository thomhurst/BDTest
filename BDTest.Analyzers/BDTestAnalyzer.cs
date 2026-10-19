using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using BDTest.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace BDTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BDTestAnalyzer : DiagnosticAnalyzer
{
    private static DiagnosticDescriptor Rule(string id, string title, string message, DiagnosticSeverity severity) =>
        new(id, title, message, "BDTest", severity, isEnabledByDefault: true,
            helpLinkUri: "https://thomhurst.github.io/BDTest/docs/advanced/analyzers#" + id.ToLowerInvariant());

    public static readonly DiagnosticDescriptor Execution = Rule("BDT001", "Execute and observe the BDTest scenario",
        "{0}", DiagnosticSeverity.Warning);
    public static readonly DiagnosticDescriptor StepText = StepTextValidation.Rule;
    public static readonly DiagnosticDescriptor Story = Rule("BDT003", "Supply a complete Story",
        "Story requires exactly one of AsA/AsAn, plus IWant and SoThat", DiagnosticSeverity.Warning);
    public static readonly DiagnosticDescriptor AsyncStep = Rule("BDT004", "Do not discard asynchronous step work",
        "An Action step {0}; select a Task or ValueTask step overload", DiagnosticSeverity.Error);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Execution, StepText, Story, AsyncStep);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeDiscard, SyntaxKind.ExpressionStatement);
        context.RegisterSyntaxNodeAction(AnalyzeExpressionBody, SyntaxKind.ArrowExpressionClause,
            SyntaxKind.SimpleLambdaExpression, SyntaxKind.ParenthesizedLambdaExpression);
        context.RegisterOperationAction(AnalyzeStep, OperationKind.Invocation);
        context.RegisterSymbolAction(AnalyzeAttributes, SymbolKind.Method, SymbolKind.NamedType);
    }

    internal static bool IsType(ITypeSymbol? type, string name) => type?.ToDisplayString() == name &&
        type.ContainingAssembly?.Name == "BDTest";

    internal static bool IsBuilder(ITypeSymbol? type)
    {
        for (var current = type as INamedTypeSymbol; current != null; current = current.BaseType)
            if (IsType(current, "BDTest.StepBuilder") ||
                (current.Name == "ScenarioOutlineBuilder" && current.ContainingNamespace.ToDisplayString() == "BDTest" &&
                 current.ContainingAssembly.Name == "BDTest")) return true;
        return false;
    }

    internal static bool IsRunnable(ITypeSymbol? type) => type is INamedTypeSymbol named &&
        (IsType(named, "BDTest.RunnableStepBuilder") || IsRunnable(named.BaseType) ||
         (named.Name == "ScenarioOutlineBuilder" && named.ContainingNamespace.ToDisplayString() == "BDTest" &&
          named.ContainingAssembly.Name == "BDTest"));

    internal static ExpressionSyntax DiscardedExpression(ExpressionStatementSyntax statement, SemanticModel model)
    {
        if (statement.Expression is AssignmentExpressionSyntax assignment &&
            model.GetOperation(assignment) is ISimpleAssignmentOperation { Target: IDiscardOperation })
            return assignment.Right;
        return statement.Expression;
    }

    private static void AnalyzeDiscard(SyntaxNodeAnalysisContext context)
    {
        var statement = (ExpressionStatementSyntax)context.Node;
        var expression = DiscardedExpression(statement, context.SemanticModel);
        ReportDiscard(context, expression);
    }

    private static void AnalyzeExpressionBody(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is LambdaExpressionSyntax { Body: ExpressionSyntax body } lambda &&
            context.SemanticModel.GetOperation(lambda) is IAnonymousFunctionOperation operation &&
            DiscardsExpressionBody(operation.Symbol))
            ReportDiscard(context, body);
        else if (context.Node is ArrowExpressionClauseSyntax arrow && arrow.Parent != null &&
                 context.SemanticModel.GetDeclaredSymbol(arrow.Parent) is IMethodSymbol method && DiscardsExpressionBody(method))
            ReportDiscard(context, arrow.Expression);
    }

    private static bool DiscardsExpressionBody(IMethodSymbol method) => method.ReturnsVoid ||
        (method.IsAsync && method.ReturnType.ToDisplayString() is
            "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask");

    private static void ReportDiscard(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        var operation = Unwrap(context.SemanticModel.GetOperation(expression));
        // Only prove direct discards. Locals, returns and helper arguments can escape for later execution.
        if (operation is not IInvocationOperation invocation) return;
        if (IsBuilder(invocation.Type) && CreatesBuilder(invocation))
            context.ReportDiagnostic(Diagnostic.Create(Execution, expression.GetLocation(),
                "This BDTest chain is discarded without execution"));
        else if (IsExecution(invocation) || IsConfiguredExecution(invocation))
            context.ReportDiagnostic(Diagnostic.Create(Execution, expression.GetLocation(),
                "Await or return the BDTestAsync task"));
    }

    private static bool CreatesBuilder(IInvocationOperation invocation)
    {
        if (invocation.TargetMethod.ContainingAssembly.Name != "BDTest") return false;
        if ((BDTestSymbols.IsStepName(invocation.TargetMethod.Name) && BDTestSymbols.IsBuilderMethod(invocation.TargetMethod)) ||
            invocation.TargetMethod.Name == "ScenarioOutline") return true;
        var receiver = invocation.Instance ?? (invocation.TargetMethod.IsExtensionMethod ? invocation.Arguments.FirstOrDefault()?.Value : null);
        return Unwrap(receiver) is IInvocationOperation inner && IsBuilder(inner.Type) && CreatesBuilder(inner);
    }

    private static bool IsExecution(IInvocationOperation invocation) => invocation.TargetMethod.Name == "BDTestAsync" &&
        IsRunnable(invocation.Instance?.Type) && invocation.TargetMethod.ContainingAssembly.Name == "BDTest";

    private static bool IsConfiguredExecution(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name == "ConfigureAwait" &&
        Unwrap(invocation.Instance) is IInvocationOperation inner && IsExecution(inner);

    internal static IOperation? Unwrap(IOperation? operation)
    {
        while (true)
        {
            switch (operation)
            {
                case IConversionOperation conversion: operation = conversion.Operand; break;
                case IDelegateCreationOperation creation: operation = creation.Target; break;
                case IParenthesizedOperation parenthesized: operation = parenthesized.Operand; break;
                default: return operation;
            }
        }
    }

    private static void AnalyzeStep(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var model = invocation.SemanticModel;
        if (model == null) return;
        if (!BDTestSymbols.IsStepName(invocation.TargetMethod.Name) ||
            !BDTestSymbols.IsBuilderMethod(invocation.TargetMethod) ||
            invocation.TargetMethod.ContainingAssembly.Name != "BDTest") return;
        var argument = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 0);
        if (argument?.Parameter?.Type is not INamedTypeSymbol { DelegateInvokeMethod: { } invoke }) return;
        var target = Unwrap(argument.Value);
        if (invoke.ReturnsVoid)
        {
            if (target is IAnonymousFunctionOperation lambda)
            {
                if (lambda.Symbol.IsAsync)
                    context.ReportDiagnostic(Diagnostic.Create(AsyncStep, argument.Syntax.GetLocation(), "is async void"));
                else
                {
                    foreach (var call in AsyncVoidCalls(lambda.Syntax, model))
                        context.ReportDiagnostic(Diagnostic.Create(AsyncStep, call.GetLocation(), "calls an async void method"));
                    foreach (var expression in DiscardedAwaitables(lambda.Syntax, model))
                        context.ReportDiagnostic(Diagnostic.Create(AsyncStep, expression.GetLocation(), "discards awaitable work"));
                }
            }
            else if (target is IMethodReferenceOperation method && IsAsyncVoid(method.Method))
                context.ReportDiagnostic(Diagnostic.Create(AsyncStep, argument.Syntax.GetLocation(), "is async void"));
        }

        if (context.Compilation.Assembly.GetTypeByMetadataName("BDTest.Generated.StepTextValidationMarker") == null)
            foreach (var diagnostic in StepTextValidation.ValidateReferencedStep(invocation))
                context.ReportDiagnostic(diagnostic);
    }

    private static bool IsAsyncVoid(IMethodSymbol method) => method.ReturnsVoid && (method.IsAsync ||
        method.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() ==
            "System.Runtime.CompilerServices.AsyncStateMachineAttribute"));

    internal static IEnumerable<InvocationExpressionSyntax> AsyncVoidCalls(SyntaxNode lambda, SemanticModel model) =>
        lambda.DescendantNodes(n => n == lambda ||
                n is not AnonymousFunctionExpressionSyntax && n is not LocalFunctionStatementSyntax)
            .OfType<InvocationExpressionSyntax>()
            .Where(call => model.GetOperation(call) is IInvocationOperation operation && IsAsyncVoid(operation.TargetMethod));

    internal static IEnumerable<ExpressionSyntax> DiscardedAwaitables(SyntaxNode lambda, SemanticModel model)
    {
        if (lambda is LambdaExpressionSyntax { Body: ExpressionSyntax expression } && IsAwaitable(model.GetTypeInfo(expression).Type))
            yield return expression;
        foreach (var statement in lambda.DescendantNodes(n => n == lambda ||
                     n is not AnonymousFunctionExpressionSyntax && n is not LocalFunctionStatementSyntax)
                     .OfType<ExpressionStatementSyntax>())
        {
            var value = DiscardedExpression(statement, model);
            if (value is AwaitExpressionSyntax || value is AssignmentExpressionSyntax) continue;
            if (IsAwaitable(model.GetTypeInfo(value).Type)) yield return value;
        }
    }

    private static bool IsAwaitable(ITypeSymbol? type)
    {
        for (var current = type as INamedTypeSymbol; current != null; current = current.BaseType)
        {
            if (current.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks" &&
                current.Name is "Task" or "ValueTask") return true;
            if (current.GetMembers("GetAwaiter").OfType<IMethodSymbol>().Any(m => !m.IsStatic && m.Parameters.Length == 0 &&
                    m.DeclaredAccessibility == Accessibility.Public && m.ReturnType is INamedTypeSymbol awaiter &&
                    awaiter.AllInterfaces.Any(i => i.ToDisplayString() == "System.Runtime.CompilerServices.INotifyCompletion") &&
                    awaiter.GetMembers("IsCompleted").OfType<IPropertySymbol>().Any(p =>
                        p.DeclaredAccessibility == Accessibility.Public && p.Type.SpecialType == SpecialType.System_Boolean) &&
                    awaiter.GetMembers("GetResult").OfType<IMethodSymbol>().Any(r =>
                        r.DeclaredAccessibility == Accessibility.Public && !r.IsStatic && r.Parameters.Length == 0))) return true;
        }
        return false;
    }

    private static void AnalyzeAttributes(SymbolAnalysisContext context)
    {
        foreach (var attribute in context.Symbol.GetAttributes())
        {
            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation();
            if (location == null) continue;
            if (context.Symbol is IMethodSymbol method && IsType(attribute.AttributeClass, "BDTest.StepTextAttribute"))
            {
                if (context.Compilation.Assembly.GetTypeByMetadataName("BDTest.Generated.StepTextValidationMarker") != null) continue;
                foreach (var message in StepTextValidation.Validate(attribute, method))
                    context.ReportDiagnostic(Diagnostic.Create(StepText, location, message));
            }
            else if (IsType(attribute.AttributeClass, "BDTest.StoryAttribute"))
            {
                var supplied = attribute.NamedArguments.Where(a => a.Value.Value is string text && !string.IsNullOrEmpty(text))
                    .Select(a => a.Key).ToList();
                if (supplied.Contains("AsA") == supplied.Contains("AsAn") || !supplied.Contains("IWant") || !supplied.Contains("SoThat"))
                    context.ReportDiagnostic(Diagnostic.Create(Story, location));
            }
        }
    }

}
