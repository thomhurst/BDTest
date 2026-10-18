// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

// Keep the compiler polyfill private to the legacy assembly.
#if !NET5_0_OR_GREATER
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class ModuleInitializerAttribute : Attribute { }
#endif
