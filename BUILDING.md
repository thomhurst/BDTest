# Build settings and diagnostic rollout

`Directory.Build.props` owns shared `LangVersion=latest` and
`ImplicitUsings=enable` settings. Projects inherit them. Keep existing target
frameworks. `BDTest.Razor.Reports` uses the Razor SDK's default language version,
compiled views, and static web asset packaging without per-file content overrides.

## Completed scopes

`Nullable=enable` and `TreatWarningsAsErrors=true` are set centrally for:

- `BDTest.Pipeline` and `BDTest.Pipeline.Tests` (the initial rollout in #689).
- `BDTest.Example` and `BDTest.ReportGenerator.RazorServer`.
- `BDTest.xUnit.Tests`, `BDTest.MSTest.Tests`, and `BDTest.TUnit.Tests`.
- `BDTest.Targets.Tests` and `BDTest.Legacy.Tests` (modern and legacy asset coverage).
- `BDTest.SourceGenerator`, `BDTest.Analyzers`, and their test projects. These
  packages already used nullable analysis; they now also treat warnings as errors.

Test fields initialized by framework setup hooks use `null!` only at declaration,
with their lifecycle documented. Values absent before setup or execution remain
nullable. Reflection and process helpers fail explicitly when required fixture
metadata or processes are missing. Source-generator tests retain nullable argument
values; their dynamically compiled input strings keep independent compilation
settings so existing consumer compatibility tests remain unchanged.

The sample host models missing Azure configuration, missing report/attachment
blobs, nullable deserialization results, and unauthenticated users explicitly.
Its bounded concurrent downloads retain nullable result slots until missing or
JSON-null summaries are filtered out. No nullable diagnostic categories are disabled.

## Validation

Use .NET SDK 10.0.402, matching CI:

```powershell
dotnet build BDTest.sln -c Release
```

Run every `*Tests.csproj` with `dotnet test <project> -c Release`, except
`BDTest.TUnit.Tests`, which uses `dotnet run --project BDTest.TUnit.Tests -c Release`.
Do not run the publishing pipeline in `Production` locally.

## Remaining rollout

#855 was split into sibling deliverables under roadmap #860. The initial scope
is #919. The remaining scopes are:

1. #922: `BDTest.Tests` and shared historical report fixtures. Resolve nullable
   diagnostics and obsolete API usage while preserving regression coverage.
2. #920: `BDTest` and the NUnit, xUnit, MSTest, and TUnit adapters. Annotate public
   inputs, returns, contexts, and serialized models to match runtime behavior.
   Preserve accepted nulls, historical reports, and target frameworks. Update
   consumer documentation and migration notes for nullable contract changes.
3. #921: `BDTest.Razor.Reports` and `BDTest.ReportGenerator.Tests`. Review options,
   storage, request values, serialized models, and views. Complete the rollout by
   making both settings unconditional in `Directory.Build.props` and removing
   redundant per-project settings and remaining per-file nullable directives.

The remaining projects keep their existing diagnostic settings in this change.

Keep the existing `CS1591` suppression for partially documented public APIs;
completing XML documentation is separate from nullable analysis. Shared test
files retain nullable directives until every consuming project enables analysis.
Do not disable diagnostic categories or blanket-annotate public APIs to complete
these scopes.

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
