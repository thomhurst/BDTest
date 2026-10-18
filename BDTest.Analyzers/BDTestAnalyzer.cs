using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
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
    public static readonly DiagnosticDescriptor StepText = Rule("BDT002", "Use valid StepText parameters",
        "{0}", DiagnosticSeverity.Error);
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
        context.RegisterSyntaxNodeAction(AnalyzeVoidExpression, SyntaxKind.ArrowExpressionClause,
            SyntaxKind.SimpleLambdaExpression, SyntaxKind.ParenthesizedLambdaExpression);
        context.RegisterOperationAction(AnalyzeStep, OperationKind.Invocation);
        context.RegisterSymbolAction(AnalyzeAttributes, SymbolKind.Method, SymbolKind.NamedType);
    }

    internal static bool IsType(ITypeSymbol? type, string name) => type?.ToDisplayString() == name &&
        type.ContainingAssembly?.Name == "BDTest";

    internal static bool IsBuilder(ITypeSymbol? type)
    {
        for (var current = type as INamedTypeSymbol; current != null; current = current.BaseType)
            if (IsType(current, "BDTest.Test.Steps.StepBuilder") ||
                (current.Name == "ScenarioOutlineBuilder" && current.ContainingNamespace.ToDisplayString() == "BDTest.Test" &&
                 current.ContainingAssembly.Name == "BDTest")) return true;
        return false;
    }

    internal static bool IsRunnable(ITypeSymbol? type) => type is INamedTypeSymbol named &&
        (IsType(named, "BDTest.Test.RunnableStepBuilder") || IsRunnable(named.BaseType) ||
         (named.Name == "ScenarioOutlineBuilder" && named.ContainingNamespace.ToDisplayString() == "BDTest.Test" &&
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

    private static void AnalyzeVoidExpression(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is LambdaExpressionSyntax { Body: ExpressionSyntax body } lambda &&
            context.SemanticModel.GetOperation(lambda) is IAnonymousFunctionOperation { Symbol.ReturnsVoid: true })
            ReportDiscard(context, body);
        else if (context.Node is ArrowExpressionClauseSyntax arrow && arrow.Parent != null &&
                 context.SemanticModel.GetDeclaredSymbol(arrow.Parent) is IMethodSymbol { ReturnsVoid: true })
            ReportDiscard(context, arrow.Expression);
    }

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
                    foreach (var expression in DiscardedAwaitables(lambda.Syntax, model))
                        context.ReportDiagnostic(Diagnostic.Create(AsyncStep, expression.GetLocation(), "discards awaitable work"));
                }
            }
            else if (target is IMethodReferenceOperation method && (method.Method.IsAsync ||
                     method.Method.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() ==
                         "System.Runtime.CompilerServices.AsyncStateMachineAttribute")))
                context.ReportDiagnostic(Diagnostic.Create(AsyncStep, argument.Syntax.GetLocation(), "is async void"));
        }

        // Cancellation parameters are hidden only for cancellation-aware step overloads.
        if (!invoke.Parameters.Any(p => IsCancellation(p.Type))) return;
        IMethodSymbol? stepMethod = target is IMethodReferenceOperation reference ? reference.Method : null;
        if (target?.Syntax is LambdaExpressionSyntax syntax)
        {
            var body = syntax.Body is AwaitExpressionSyntax awaited ? awaited.Expression : syntax.Body;
            if (body is ExpressionSyntax && model.GetOperation(body) is IInvocationOperation call)
                stepMethod = call.TargetMethod;
        }
        if (stepMethod == null || !stepMethod.Parameters.Any(p => IsCancellation(p.Type))) return;
        var attribute = GetStepText(stepMethod);
        if (attribute == null) return;
        var total = stepMethod.Parameters.Length;
        if (ValidateStepText(attribute, total) != null) return; // Already reported at the declaration.
        var message = ValidateStepText(attribute, stepMethod.Parameters.Count(p => !IsCancellation(p.Type)));
        if (message != null) context.ReportDiagnostic(Diagnostic.Create(StepText, argument.Syntax.GetLocation(), message));
    }

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

    private static bool IsCancellation(ITypeSymbol type) => type.ToDisplayString() == "System.Threading.CancellationToken";

    private static AttributeData? GetStepText(IMethodSymbol method)
    {
        for (IMethodSymbol? current = method; current != null; current = current.OverriddenMethod)
        {
            var attribute = current.GetAttributes().FirstOrDefault(a => IsType(a.AttributeClass, "BDTest.Attributes.StepTextAttribute"));
            if (attribute != null) return attribute;
        }
        return null;
    }

    private static void AnalyzeAttributes(SymbolAnalysisContext context)
    {
        foreach (var attribute in context.Symbol.GetAttributes())
        {
            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation();
            if (location == null) continue;
            if (context.Symbol is IMethodSymbol method && IsType(attribute.AttributeClass, "BDTest.Attributes.StepTextAttribute"))
            {
                var message = ValidateStepText(attribute, method.Parameters.Length);
                if (message != null) context.ReportDiagnostic(Diagnostic.Create(StepText, location, message));
            }
            else if (IsType(attribute.AttributeClass, "BDTest.Attributes.StoryAttribute"))
            {
                var supplied = attribute.NamedArguments.Where(a => a.Value.Value is string text && !string.IsNullOrEmpty(text))
                    .Select(a => a.Key).ToList();
                if (supplied.Contains("AsA") == supplied.Contains("AsAn") || !supplied.Contains("IWant") || !supplied.Contains("SoThat"))
                    context.ReportDiagnostic(Diagnostic.Create(Story, location));
            }
        }
    }

    private static string? ValidateStepText(AttributeData attribute, int count)
    {
        if (attribute.ConstructorArguments.Length == 0 || attribute.ConstructorArguments[0].Value is not string template) return null;
        if (attribute.ConstructorArguments.Length > 1)
        {
            var value = attribute.ConstructorArguments[1];
            if (value.Kind == TypedConstantKind.Error) return null;
            var overrides = value.Kind == TypedConstantKind.Array
                ? (value.IsNull ? ImmutableArray<TypedConstant>.Empty : value.Values) : ImmutableArray.Create(value);
            foreach (var item in overrides)
            {
                if (item.Kind == TypedConstantKind.Error) return null;
                if (item.Value is not string text) return "StepText overrides must contain an index and a colon";
                var colon = text.IndexOf(':');
                if (colon <= 0 || text.Substring(0, colon).Any(c => c < '0' || c > '9') ||
                    !int.TryParse(text.Substring(0, colon), NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index >= count)
                    return "StepText override refers to a nonexistent visible parameter; use a zero-based index followed by a colon";
                template = template.Replace("{" + text.Substring(0, colon) + "}", text.Substring(colon + 1));
            }
        }
        return CompositeTemplate.IsValid(template, count) ? null :
            "StepText contains an invalid composite format or a placeholder outside the visible parameter count (" + count + ")";
    }
}
