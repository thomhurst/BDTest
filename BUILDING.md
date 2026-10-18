# Build settings and diagnostic rollout

`Directory.Build.props` owns the shared `LangVersion=latest` and
`ImplicitUsings=enable` settings. Projects inherit them; retain local overrides
only when a project needs a different setting. `BDTest.Razor.Reports` uses the
Razor SDK's default language version, compiled views, and static web asset
packaging without per-file content overrides.

## First scope (#689)

`BDTest.Pipeline` and `BDTest.Pipeline.Tests` enable `Nullable=enable` and
`TreatWarningsAsErrors=true` through an explicit project-name condition in
`Directory.Build.props`. Pipeline code is not shipped as a NuGet library, so it
is the first scope without changing consumer-facing nullable contracts.

The pipeline already enabled nullable analysis. Its tests now do too. Test
fixture fields initialized by NUnit's `[SetUp]` use `null!` only at declaration;
nullable values returned by directory traversal and process creation have
explicit failure paths. No nullable warnings are disabled.

Validate with .NET SDK 10.0.402, matching CI:

```powershell
dotnet build BDTest.sln -c Release
dotnet test BDTest.Pipeline.Tests/BDTest.Pipeline.Tests.csproj -c Release
```

Run every other `*Tests.csproj` with `dotnet test -c Release`, except
`BDTest.TUnit.Tests`, which uses `dotnet run --project BDTest.TUnit.Tests -c Release`.
Do not run the publishing pipeline in `Production` locally.

## Remaining rollout

Each later scope should add its projects to the explicit diagnostic condition,
resolve all resulting warnings, and pass its build and relevant tests before
merging. Do not enable warnings-as-errors for the whole solution while projects
still have unresolved warnings. Keep existing target frameworks.

1. Non-shipping tests and samples: `BDTest.Tests`, `BDTest.xUnit.Tests`,
   `BDTest.MSTest.Tests`, `BDTest.TUnit.Tests`, `BDTest.ReportGenerator.Tests`, and
   `BDTest.Example`. Model test setup lifetimes explicitly and resolve nullable
   warnings without suppressing whole diagnostic categories.
2. Non-shipping report host: `BDTest.ReportGenerator.RazorServer`. Review
   configuration and request-bound values, then validate it through
   `BDTest.ReportGenerator.Tests`.
3. Shipped packages: `BDTest`, `BDTest.NUnit`, `BDTest.xUnit`, `BDTest.MSTest`,
   `BDTest.TUnit`, and `BDTest.Razor.Reports`. Assess public parameters, return
   values, generic constraints, and serialized models before adding nullable
   annotations. Preserve accepted inputs and runtime behavior; test each adapter
   and the report middleware. Do not blanket-annotate public APIs or change
   target frameworks as part of the rollout.

The remaining projects keep their existing diagnostic settings in this change.

## Package and assembly versions (#856)

`PackProjectsModule.CreatePackOptions` passes explicit version properties to
`dotnet pack` for every package, including project references:

- `PackageVersion` and `Version` contain the full generated SemVer version.
- Starting with **3.0.0** (including prereleases), `AssemblyVersion` is
  `Major.0.0.0`. It changes only on major releases, so minor and patch updates
  keep the same strong-name identity. The 2.x series retains its existing
  full numeric assembly version to avoid changing identity within that series.
- `FileVersion` contains the numeric release version, with a zero revision for
  three-part versions (for example, `3.2.1-beta.4` becomes `3.2.1.0`).
- `InformationalVersion` contains the full SemVer version. The SDK appends the
  source revision for SourceLink. Reports use this informational version, so
  their version still identifies the release rather than the stable assembly.

For example, `3.1.2` and `3.2.0` both produce `AssemblyVersion=3.0.0.0`, while
`4.0.0` produces `AssemblyVersion=4.0.0.0`. Keep all BDTest package versions
aligned; a stable assembly identity does not make different package versions
interchangeable or guarantee compatibility across major releases.

`BDTest.Pipeline.Tests/PackageVersionTests.cs` checks the properties passed to
packing for stable, prerelease, metadata, minor, patch and major versions.
