// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

// The ModuleInitializerAttribute is built into .NET 5.0 and higher.
// For .NET Standard 2.0 and earlier frameworks, we provide our own implementation.
// 
// If you encounter CS0433 "The type 'ModuleInitializerAttribute' exists in both..." error:
// Use global::System.Runtime.CompilerServices.ModuleInitializerAttribute or add extern alias
#if !NET5_0_OR_GREATER
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ModuleInitializerAttribute : Attribute { }
#endif