#nullable enable

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;

namespace BDTest.TestUtilities;

internal static class ExceptionRegistrationProbe
{
    internal static async Task Verify(Type openFixtureType, string setupMethod, bool preRegistered,
        params (string Registry, Type Exception)[] registrations)
    {
        // Each load context starts with empty registries, independent of runner setup.
        for (var round = 0; round < 4; round++)
        {
            var context = new IsolatedContext();
            try
            {
                var assembly = context.LoadFromAssemblyPath(openFixtureType.Assembly.Location);
                var fixtureType = assembly.GetType(openFixtureType.FullName!, throwOnError: true)!;
                var core = context.LoadFromAssemblyPath(typeof(Settings.BDTestSettings).Assembly.Location);
                var settings = core.GetType("BDTest.Settings.BDTestSettings")!
                    .GetProperty("CustomExceptionSettings")!.GetValue(null)!;
                var registries = registrations.Select(registration =>
                    (Bag: (ConcurrentBag<Type>)settings.GetType().GetProperty(registration.Registry)!.GetValue(settings)!,
                        registration.Exception)).ToArray();
                foreach (var (bag, exception) in registries)
                {
                    bag.Add(typeof(ArgumentException));
                    if (preRegistered)
                    {
                        bag.Add(exception);
                    }
                }

                using var ready = new CountdownEvent(16);
                using var start = new ManualResetEventSlim();
                var contextType = typeof(object);
                var tasks = new List<Task>();
                for (var index = 0; index < 16; index++)
                {
                    contextType = typeof(List<>).MakeGenericType(contextType);
                    var closedType = fixtureType.MakeGenericType(contextType);
                    var method = closedType.GetMethod(setupMethod)!;
                    tasks.Add(Task.Factory.StartNew(() =>
                    {
                        ready.Signal();
                        start.Wait();
                        for (var call = 0; call < 64; call++)
                        {
                            method.Invoke(method.IsStatic ? null : Activator.CreateInstance(closedType), null);
                        }
                    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default));
                }

                try
                {
                    if (!ready.Wait(TimeSpan.FromSeconds(20)))
                    {
                        throw new TimeoutException("Registration workers did not start.");
                    }
                }
                finally
                {
                    start.Set();
                    await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
                }

                foreach (var (bag, exception) in registries)
                {
                    var count = bag.Count(type => type == exception);
                    if (count != 1 || !bag.Contains(typeof(ArgumentException)))
                    {
                        throw new InvalidOperationException($"Expected one {exception.Name} registration and the existing custom exception; found {count} framework registrations.");
                    }
                }
            }
            finally
            {
                context.Unload();
            }
        }
    }

    private sealed class IsolatedContext() : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Isolate BDTest and its adapters; share the runner and BCL with the test process.
            if (assemblyName.Name == "BDTest" || assemblyName.Name!.StartsWith("BDTest."))
            {
                return LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, assemblyName.Name + ".dll"));
            }

            return null;
        }
    }
}
