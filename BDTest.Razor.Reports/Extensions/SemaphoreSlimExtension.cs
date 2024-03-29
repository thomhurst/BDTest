namespace BDTest.Razor.Reports.Extensions;

public static class SemaphoreSlimExtension
{
    public static async Task RunAsync(this System.Threading.SemaphoreSlim semaphore, Func<Task> action,
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            await action().ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static async Task<T> RunAndReturnAsync<T>(this System.Threading.SemaphoreSlim semaphore,
        Func<Task<T>> action, System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return await action().ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }
}