
namespace BDTest;

/// <summary>Attaches structured report values to the most recent step without changing its method signature.</summary>
public static class StructuredStepArgumentExtensions
{
    public static T WithTable<T>(this T builder, StepTable table) where T : StepBuilder
    {
        if (table == null) throw new ArgumentNullException(nameof(table));
        return builder.WithTable(() => table);
    }

    /// <summary>Evaluates the table reader immediately before each executed attempt.</summary>
    public static T WithTable<T>(this T builder, Func<StepTable> table) where T : StepBuilder => Attach(builder, table);

    public static T WithDocString<T>(this T builder, StepDocString docString) where T : StepBuilder
    {
        if (docString == null) throw new ArgumentNullException(nameof(docString));
        return builder.WithDocString(() => docString);
    }

    /// <summary>Evaluates the doc string reader immediately before each executed attempt.</summary>
    public static T WithDocString<T>(this T builder, Func<StepDocString> docString) where T : StepBuilder => Attach(builder, docString);

    private static T Attach<T>(T builder, Func<StepArgument> argument) where T : StepBuilder
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (argument == null) throw new ArgumentNullException(nameof(argument));
        builder.ExistingSteps.Last().AttachedArguments.Add(argument);
        return builder;
    }
}
