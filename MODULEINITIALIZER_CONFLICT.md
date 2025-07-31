# ModuleInitializer Attribute Conflict Resolution

If you encounter the following error when using BDTest in .NET 5+ projects:

```
Error CS0433: The type 'ModuleInitializerAttribute' exists in both 'BDTest, Version=x.x.x.x, Culture=neutral, PublicKeyToken=...' and 'System.Runtime, Version=x.x.x.x, Culture=neutral, PublicKeyToken=...'
```

## Why This Happens

BDTest targets .NET Standard 2.0 for broad compatibility. Since `ModuleInitializerAttribute` was introduced in .NET 5, BDTest includes its own implementation for older frameworks. When consumed by .NET 5+ projects, both the BDTest version and the system version become available, causing a naming conflict.

## Solution: Use Extern Alias

Add an extern alias to your BDTest reference to resolve the conflict:

### 1. Update your project file

```xml
<ProjectReference Include="path/to/BDTest.csproj" Aliases="bdtest" />
```

or for package references:

```xml
<PackageReference Include="BDTest" Version="x.x.x" Aliases="bdtest" />
```

### 2. Add extern alias directive to your C# files

```csharp
extern alias bdtest;
using System.Runtime.CompilerServices;

[ModuleInitializer] // This resolves to the system ModuleInitializerAttribute
public static void MyModuleInitializer()
{
    // Your initialization code
}
```

The `extern alias` directive ensures BDTest's types are isolated while your code uses the system's `ModuleInitializerAttribute`.

## Alternative Approach

If you prefer not to use extern alias, you can also reference the system attribute explicitly:

```csharp
[global::System.Runtime.CompilerServices.ModuleInitializerAttribute]
public static void MyModuleInitializer()
{
    // Your initialization code  
}
```

Note: This approach may still cause conflicts in some scenarios, so extern alias is recommended.