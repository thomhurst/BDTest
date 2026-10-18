namespace BDTest.Test;

// Compatibility for synchronous setup access and legacy teardown hooks. Run user
// async callbacks without a synchronization context that the caller may be blocking.
internal static class ContextAsyncBridge
{
    internal static T Run<T>(Func<ValueTask<T>> action) =>
        Task.Run(async () => await action().ConfigureAwait(false)).GetAwaiter().GetResult();

    internal static void Run(Func<ValueTask> action) =>
        Task.Run(async () => await action().ConfigureAwait(false)).GetAwaiter().GetResult();
}
