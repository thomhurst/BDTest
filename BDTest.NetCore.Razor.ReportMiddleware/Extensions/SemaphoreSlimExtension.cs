using System;
using System.Threading.Tasks;

namespace BDTest.NetCore.Razor.ReportMiddleware.Extensions
{
    public static class SemaphoreSlimExtension
    {
        public static async Task RunAsync(this System.Threading.SemaphoreSlim semaphore, Func<Task> action,
            System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await semaphore.WaitAsync(cancellationToken);
                await action();
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
                await semaphore.WaitAsync(cancellationToken);
                return await action();
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}