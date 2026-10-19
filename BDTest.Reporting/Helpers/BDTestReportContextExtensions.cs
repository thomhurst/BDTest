namespace BDTest.Helpers;

/// <summary>Maps live framework or hook contexts to framework-independent report metadata.</summary>
public static class BDTestReportContextExtensions
{
    /// <summary>Invokes a typed adapter once to obtain metadata for HTML, JSON or report uploads.</summary>
    /// <remarks>
    /// Call while the framework context is valid. The adapter selects the metadata to export;
    /// BDTest neither reflects over the context nor retains it. The returned descriptor is owned
    /// by the caller and must not be mutated concurrently with export.
    /// </remarks>
    public static BDTestRunDescriptor ToBDTestRunDescriptor<TContext>(this TContext context,
        Func<TContext, BDTestRunDescriptor> adapter) where TContext : class
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (adapter == null) throw new ArgumentNullException(nameof(adapter));
        return adapter(context) ?? throw new InvalidOperationException("The report context adapter returned a null descriptor.");
    }

    /// <summary>Creates HTML for the supplied run using metadata selected from a framework or hook context.</summary>
    /// <remarks>
    /// The context provides metadata only; it does not select or filter scenarios. Call after the
    /// selected run's tests finish. This returns HTML without writing a file or disposing the run.
    /// Adapter exceptions propagate to the caller.
    /// </remarks>
    public static string CreateHtmlReport<TContext>(this TContext context, BDTestRun run,
        Func<TContext, BDTestRunDescriptor> adapter) where TContext : class
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        return BDTestHtmlHelper.GetTestHtmlReport(context.ToBDTestRunDescriptor(adapter), run);
    }
}
