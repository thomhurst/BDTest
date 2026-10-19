using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace BDTest.Compiler;

internal static class StepTextValidation
{
    internal static readonly DiagnosticDescriptor Rule = new("BDT002", "Use valid StepText parameters", "{0}",
        "BDTest", DiagnosticSeverity.Error, isEnabledByDefault: true,
        helpLinkUri: "https://bdtest.tomlonghurst.dev/docs/advanced/analyzers#bdt002");

    internal static IEnumerable<Diagnostic> ValidateReferencedStep(IInvocationOperation invocation)
    {
        if (!BDTestSymbols.IsStepName(invocation.TargetMethod.Name) ||
            !BDTestSymbols.IsBuilderMethod(invocation.TargetMethod)) yield break;
        var argument = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 0);
        IOperation? target = argument?.Value;
        while (target is IConversionOperation || target is IDelegateCreationOperation)
            target = target is IConversionOperation conversion ? conversion.Operand : ((IDelegateCreationOperation)target).Target;
        var method = (target as IMethodReferenceOperation)?.Method;
        if (target?.Syntax is LambdaExpressionSyntax lambda)
        {
            var body = lambda.Body is AwaitExpressionSyntax awaited ? awaited.Expression : lambda.Body;
            method = (invocation.SemanticModel?.GetOperation(body) as IInvocationOperation)?.TargetMethod;
        }
        if (method == null || argument == null) yield break;
        for (IMethodSymbol? current = method; current != null; current = current.OverriddenMethod)
        {
            var attribute = current.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "BDTest.StepTextAttribute" &&
                a.AttributeClass.ContainingAssembly.Name == "BDTest");
            if (attribute == null) continue;
            // Local declarations are checked once, directly at the attribute. Imported and
            // inherited attributes need the selected method's visible parameter names.
            if (SymbolEqualityComparer.Default.Equals(current, method) && attribute.ApplicationSyntaxReference != null)
                yield break;
            foreach (var message in Validate(attribute, method))
                yield return Diagnostic.Create(Rule, argument.Syntax.GetLocation(), message);
            yield break;
        }
    }

    internal static IEnumerable<string> Validate(AttributeData attribute, IMethodSymbol method)
    {
        if (attribute.ConstructorArguments.Length == 0 || attribute.ConstructorArguments[0].Value is not string template)
            return Enumerable.Empty<string>();
        IEnumerable<string?>? overrides = Enumerable.Empty<string>();
        if (attribute.ConstructorArguments.Length > 1)
        {
            var value = attribute.ConstructorArguments[1];
            if (value.Kind == TypedConstantKind.Error) return Enumerable.Empty<string>();
            if (value.Kind == TypedConstantKind.Array)
            {
                if (value.IsNull) overrides = null;
                else if (value.Values.Any(item => item.Kind == TypedConstantKind.Error)) return Enumerable.Empty<string>();
                else overrides = value.Values.Select(item => item.Value as string);
            }
            else overrides = new[] { value.Value as string };
        }
        return NamedStepTemplate.Validate(template, method.Parameters
            .Where(p => p.Type.ToDisplayString() != "System.Threading.CancellationToken").Select(p => p.Name), overrides);
    }
}
