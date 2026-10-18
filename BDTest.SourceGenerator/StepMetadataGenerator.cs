using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace BDTest.SourceGenerator;

[Generator(LanguageNames.CSharp)]
public sealed class StepMetadataGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var sites = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is InvocationExpressionSyntax invocation && IsStepName(GetName(invocation)),
            static (syntax, _) => Describe(syntax)).Where(static site => site != null);
        context.RegisterSourceOutput(sites.Collect(), static (output, items) => Emit(output, items));
    }

    private static string? GetName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        SimpleNameSyntax name => name.Identifier.ValueText,
        _ => null
    };

    private static bool IsStepName(string? name) => name is "Given" or "When" or "Then" or "And" or "But";

    private static Site? Describe(GeneratorSyntaxContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetOperation(invocation) is not IInvocationOperation builder ||
            !IsBDTestBuilder(builder.TargetMethod)) return null;

        var argument = builder.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 0)?.Syntax as ArgumentSyntax;
        if (argument?.Expression is not LambdaExpressionSyntax lambda) return null;
        var callName = invocation.Expression is MemberAccessExpressionSyntax member ? (SyntaxNode)member.Name : invocation.Expression;
        var location = callName.GetLocation().GetMappedLineSpan();
        var file = location.Path;
        var line = location.StartLinePosition.Line + 1;
        var expression = lambda.ToString();
        string? method = null;
        var arguments = new List<string>();
        var supportsArguments = false;
        if (lambda.Body is InvocationExpressionSyntax call &&
            context.SemanticModel.GetOperation(call) is IInvocationOperation operation &&
            !operation.TargetMethod.IsExtensionMethod && operation.TargetMethod.MethodKind == MethodKind.Ordinary)
        {
            method = DescribeMethod(operation.TargetMethod);
            if (method != null)
            {
                supportsArguments = true;
                foreach (var value in operation.Arguments.OrderBy(a => a.Parameter?.Ordinal))
                {
                    // Optional/default and expanded params arguments need a separate binding contract.
                    if (value.IsImplicit || value.ArgumentKind != ArgumentKind.Explicit ||
                        value.Parameter?.RefKind != RefKind.None ||
                        DescribeStepArgument(value.Value, value.Parameter, lambda) is not string description)
                    {
                        supportsArguments = false;
                        arguments.Clear();
                        break;
                    }
                    arguments.Add(description);
                }
            }
        }
        return new Site(file, line, expression,
            "new global::BDTest.Metadata.StepCallSiteMetadata(" + Quote(file) + ", " + line + ", " + Quote(expression) +
            ", " + (method ?? "null") + ", " + (supportsArguments ? "true" : "false") +
            (arguments.Count == 0 ? "" : ", " + string.Join(", ", arguments)) + ")");
    }

    private static bool IsBDTestBuilder(IMethodSymbol method)
    {
        // Match the bound symbol rather than unrelated methods with the same spelling.
        var type = method.ContainingType;
        var space = type.ContainingNamespace.ToDisplayString();
        return (type.Name == "BDTestBase" && space == "BDTest.Test") ||
               space == "BDTest.Test.Steps" || space.StartsWith("BDTest.Test.Steps.", StringComparison.Ordinal);
    }

    private static string? DescribeMethod(IMethodSymbol method)
    {
        var declaringType = TypeReference(method.ContainingType);
        var genericArguments = method.TypeArguments.Select(TypeReference).ToArray();
        if (declaringType == null || genericArguments.Any(a => a == null)) return null;
        var signatures = method.ConstructedFrom.Parameters.Select(p => Quote(Signature(p.Type) +
            (p.RefKind == RefKind.None ? "" : "&")));
        return "new global::BDTest.Metadata.StepMethodMetadata(" + declaringType + ", " + Quote(method.MetadataName) +
               ", " + Quote(method.OriginalDefinition.GetDocumentationCommentId() ?? "") + ", new string[] { " +
               string.Join(", ", signatures) + " }" +
               (genericArguments.Length == 0 ? "" : ", " + string.Join(", ", genericArguments)) + ")";
    }

    private static string? DescribeStepArgument(IOperation operation, IParameterSymbol parameter,
        LambdaExpressionSyntax lambda)
    {
        // The runner supplies this token at invocation time. Its value is omitted from text,
        // so represent it without capturing a parameter that does not exist at build time.
        if (operation is IParameterReferenceOperation reference &&
            parameter.Type.ToDisplayString() == "System.Threading.CancellationToken" &&
            reference.Parameter.Type.ToDisplayString() == "System.Threading.CancellationToken" &&
            reference.Parameter.DeclaringSyntaxReferences.Any(syntax => lambda.Span.Contains(syntax.Span)))
        {
            return "new global::BDTest.Metadata.StepArgumentMetadata(" + Quote(parameter.Name) + ", (object)null)";
        }
        return DescribeArgument(operation, parameter.Name);
    }

    private static string? DescribeArgument(IOperation operation, string parameterName)
    {
        if (operation is IConversionOperation conversion && PreservesValue(conversion))
            return DescribeArgument(conversion.Operand, parameterName);
        if (operation.ConstantValue.HasValue)
        {
            var value = operation.ConstantValue.Value;
            // Enum constants need their runtime enum type, not a boxed underlying number.
            if (operation.Type?.TypeKind == TypeKind.Enum) return null;
            var literal = value == null ? "(object)null" : SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false);
            if (literal == null) return null;
            if (operation.Type?.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte or
                SpecialType.System_Int16 or SpecialType.System_UInt16)
                literal = "(" + operation.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")(" + literal + ")";
            return "new global::BDTest.Metadata.StepArgumentMetadata(" + Quote(parameterName) + ", (object)(" + literal + "))";
        }

        var members = new List<string>();
        if (!DescribeMemberPath(operation, members) || members.Count == 0) return null;
        return "new global::BDTest.Metadata.StepArgumentMetadata(" + Quote(parameterName) + ", " + string.Join(", ", members) + ")";
    }

    // Boxing preserves the same runtime value returned by reflection; numeric and user conversions do not.
    private static bool PreservesValue(IConversionOperation conversion) =>
        conversion.Conversion.IsIdentity || conversion.Conversion.IsReference ||
        (conversion.IsImplicit && !conversion.Conversion.IsUserDefined &&
         conversion.Operand.Type?.IsValueType == true && conversion.Type?.IsReferenceType == true);

    private static bool DescribeMemberPath(IOperation operation, List<string> members)
    {
        if (operation is IConversionOperation conversion && PreservesValue(conversion))
            return DescribeMemberPath(conversion.Operand, members);
        if (operation is IInstanceReferenceOperation instance)
            // Reflection dispatches virtual getters, so it cannot preserve a base-qualified read.
            return instance.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance &&
                   instance.Syntax is not BaseExpressionSyntax;

        ISymbol member;
        IOperation? receiver;
        bool property;
        if (operation is IFieldReferenceOperation field)
        {
            member = field.Field;
            receiver = field.Instance;
            property = false;
        }
        else if (operation is IPropertyReferenceOperation prop && prop.Arguments.Length == 0 && prop.Property.GetMethod != null)
        {
            member = prop.Property;
            receiver = prop.Instance;
            property = true;
        }
        else return false;

        if (receiver != null && !DescribeMemberPath(receiver, members)) return false;
        var declaringType = TypeReference(member.ContainingType);
        if (declaringType == null) return false;
        members.Add("new global::BDTest.Metadata.StepMemberReference(" + declaringType + ", " + Quote(member.MetadataName) +
                    ", " + (property ? "true" : "false") + ", " + (member.IsStatic ? "true" : "false") + ")");
        return true;
    }

    private static string? TypeReference(ITypeSymbol type)
    {
        if (type is IDynamicTypeSymbol)
            return "new global::BDTest.Metadata.StepTypeReference(typeof(object).Assembly.GetName().Name, \"System.Object\")";
        if (type is not INamedTypeSymbol named || named.IsAnonymousType || named.IsUnboundGenericType) return null;
        var allArguments = TypeArguments(named).Select(TypeReference).ToArray();
        if (allArguments.Any(a => a == null)) return null;
        return "new global::BDTest.Metadata.StepTypeReference(" + Quote(named.ContainingAssembly.Name) + ", " +
               Quote(MetadataName(named.OriginalDefinition)) +
               (allArguments.Length == 0 ? "" : ", " + string.Join(", ", allArguments)) + ")";
    }

    private static IEnumerable<ITypeSymbol> TypeArguments(INamedTypeSymbol type) =>
        (type.ContainingType == null ? Enumerable.Empty<ITypeSymbol>() : TypeArguments(type.ContainingType)).Concat(type.TypeArguments);

    private static string MetadataName(INamedTypeSymbol type) => type.ContainingType == null
        ? (type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString() + ".") + type.MetadataName
        : MetadataName(type.ContainingType) + "+" + type.MetadataName;

    private static string Signature(ITypeSymbol type)
    {
        if (type is IDynamicTypeSymbol) return "System.Object";
        if (type is IArrayTypeSymbol array) return Signature(array.ElementType) + "[" + new string(',', array.Rank - 1) + "]";
        if (type is ITypeParameterSymbol parameter)
            return (parameter.TypeParameterKind == TypeParameterKind.Method ? "!!" : "!") + parameter.Ordinal;
        if (type is INamedTypeSymbol named)
        {
            var arguments = TypeArguments(named).ToArray();
            return MetadataName(named.OriginalDefinition) + (arguments.Length == 0 ? "" : "[" + string.Join(",", arguments.Select(Signature)) + "]");
        }
        return type.ToDisplayString();
    }

    private static string Quote(string text) => SymbolDisplay.FormatLiteral(text, quote: true);

    private static void Emit(SourceProductionContext context, ImmutableArray<Site?> sites)
    {
        var source = new StringBuilder("// <auto-generated/>\n[assembly: global::BDTest.Metadata.StepMetadataProviderAttribute(typeof(global::BDTest.Generated.StepMetadataProvider))]\nnamespace BDTest.Generated {\ninternal sealed class StepMetadataProvider : global::BDTest.Metadata.IStepMetadataProvider {\npublic global::BDTest.Metadata.StepCallSiteMetadata Find(string file, int line, string expression) {\n");
        // A duplicate identity is ambiguous. Let caller text win rather than select the wrong method.
        foreach (var group in sites.Where(s => s != null).GroupBy(s => (s!.File, s.Line, s.Expression)))
        {
            var values = group.Select(s => s!.Code).Distinct().ToArray();
            if (values.Length != 1) continue;
            source.Append("if (file == ").Append(Quote(group.Key.File)).Append(" && line == ").Append(group.Key.Line)
                .Append(" && expression == ").Append(Quote(group.Key.Expression)).Append(") return ").Append(values[0]).Append(";\n");
        }
        source.Append("return null;\n}\n}\n}\n");
        if (sites.Length != 0) context.AddSource("BDTest.StepMetadata.g.cs", source.ToString());
    }

    private sealed class Site
    {
        public Site(string file, int line, string expression, string code)
        {
            File = file;
            Line = line;
            Expression = expression;
            Code = code;
        }
        public string File { get; }
        public int Line { get; }
        public string Expression { get; }
        public string Code { get; }
    }
}
