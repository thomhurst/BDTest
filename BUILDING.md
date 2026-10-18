# Build settings and diagnostic rollout

`Directory.Build.props` owns shared `LangVersion=latest`,
`ImplicitUsings=enable`, `Nullable=enable` and `TreatWarningsAsErrors=true`
settings unconditionally. All projects inherit them, including newly added projects. Keep existing target
frameworks. `BDTest.Razor.Reports` uses the Razor SDK's default language version,
compiled views, and static web asset packaging without per-file content overrides.

## Completed scopes

`Nullable=enable` and `TreatWarningsAsErrors=true` apply to the entire repository.
The rollout covered:

- `BDTest.Pipeline` and `BDTest.Pipeline.Tests` (the initial rollout in #689).
- `BDTest.Example` and `BDTest.ReportGenerator.RazorServer`.
- `BDTest.xUnit.Tests`, `BDTest.MSTest.Tests`, and `BDTest.TUnit.Tests`.
- `BDTest.Tests`, including shared report JSON and historical report fixtures (#922).
- `BDTest.Targets.Tests` and `BDTest.Legacy.Tests` (modern and legacy asset coverage).
- `BDTest.SourceGenerator`, `BDTest.Analyzers`, and their test projects. These
  packages already used nullable analysis; they now also treat warnings as errors.
- `BDTest` and the NUnit, xUnit, MSTest and TUnit adapters (#920), on every
  existing target framework.
- `BDTest.Razor.Reports`, its compiled views and `BDTest.ReportGenerator.Tests`
  (#921), completing the rollout.
- `BDTest.Contracts` and its tests, which now inherit the same central settings.

Test fields initialized by framework setup hooks use `null!` only at declaration,
with their lifecycle documented. Values absent before setup or execution remain
nullable. Reflection and process helpers fail explicitly when required fixture
metadata or processes are missing. Source-generator tests retain nullable argument
values; their dynamically compiled input strings keep independent compilation
settings so existing consumer compatibility tests remain unchanged.

The core NUnit suite models optional callback state and missing repository records
as nullable. Required reflection metadata, JSON properties, HTTP content, and
exception assertion results fail explicitly when absent. Intentional null inputs
still exercise runtime guards. Context capture tests verify uniqueness in fixture
teardown instead of using obsolete NUnit ordering attributes, preserving parallel
fixture execution. Compatibility tests access obsolete settings and standalone
lifecycle bridges through reflection, preserving their behavior without disabling
obsolete diagnostics.

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

## Completed nullable rollout

#855 was split into sibling deliverables under roadmap #860. #919, #922, #920
and #921 are complete. No per-project nullable overrides or per-file nullable
directives remain. Keep the existing `CS1591` suppression for partially documented
public APIs; completing XML documentation is outside this rollout.

Reporting annotations preserve optional policies, upload keys, request filters,
custom-link values and adjacent-scenario navigation. Missing reports and attachment
streams return null. Legacy model stores accept a null model, including a JSON
`null` root forwarded by the storage bridge. Summary and index JSON retain omitted
field defaults and explicit null identifiers, counts and optional metadata; storage
writes still require a record ID. Views handle absent historical collections and
counts without changing stored data. Required MVC context and embedded assets fail
explicitly when missing. No nullable warning categories are suppressed.

`ReportNullableContractTests` covers summary serialization, optional host setup,
missing storage results, legacy null roots, query removal and historical rendering.
The report host tests preserve null configuration and missing-blob fixtures, and
use explicit nullable test state and responses.

Core annotations preserve optional caller metadata, run descriptors, factories,
services, missing story attributes, unexecuted output and deserialized report
fields. `Deserialize` and `DeserializeAsync` return null for a JSON `null` root.
Report collections keep their historical empty defaults and accept explicit JSON
nulls, including `Scenarios` and `NotRun` before server normalization. This avoids
changing MVC's inferred input requirements. Runtime-only scenario
references are nullable after deserialization or fixture collection. Context
creation still requires a non-null object. Internal lifecycle slots are nullable
until bound and after cleanup; normalized tags and structured arguments retain
non-null getters with nullable setter inputs. No nullable directives or warning
suppressions are added. Tests cover accepted nulls, omitted historical fields,
null JSON roots and lazy argument values. See
[consumer migration notes](docs/docs/migration/target-frameworks.md#nullable-reference-contracts).

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

## Strong-name signing and adapter boundaries (#857)

`Directory.Build.props` defines `BDTestPublicKey` once and resolves
`StrongNameKeyPair.snk` relative to the repository, independent of the working
directory. The signed runtime packages are `BDTest`, `BDTest.NUnit`,
`BDTest.xUnit`, `BDTest.MSTest`, `BDTest.TUnit`, `BDTest.Contracts` and `BDTest.Razor.Reports`.
Their assembly names, key and version policy remain unchanged for every target.
Keep this explicit list current when adding a runtime package.

`BDTest.SourceGenerator` and `BDTest.Analyzers` remain unsigned compiler
extensions, packed under `analyzers/dotnet/cs`, with their existing identities.
The pipeline, samples, report host and tests are unsigned except `BDTest.Tests`.
That test assembly needs signing because core and Razor reports grant it friend
access through SDK `InternalsVisibleTo` items using `$(BDTestPublicKey)`.
No official adapter receives friend access. Custom adapters need neither a
friend grant nor the repository signing key.

The protected API on `BDTestBase` owns constructor capture through
`CaptureStartupOutput`, `BindStartupOutput`, `CompleteStartupOutput` and
`BeginPendingTestExecution`. NUnit overrides `ExecuteScenarioStepsAsync` to
isolate assertion state without modifying the runner's overall result. See
[custom adapters](docs/docs/frameworks/custom-adapters.md) for the contract.
Coordinate further public API cleanup with #827; preserve the retry runner
regressions introduced by #820.

After a Release solution build, run:

```powershell
pwsh scripts/Test-AssemblySigning.ps1
```

This verifies assembly names, public keys, signing flags and matching identities
across targets, including the unsigned compiler extensions and tooling. The
pipeline runs it in `VerifyPackageArtifactsModule` after packing and before
either the NuGet or local-feed upload module. A failed check prevents publication.

## Package selection and naming (#858)

`Directory.Build.props` defaults `IsPackable` to `false`. Shipped projects opt in
with `<IsPackable>true</IsPackable>` in their project files. The complete package
set is `BDTest`, `BDTest.NUnit`, `BDTest.xUnit`, `BDTest.MSTest`, `BDTest.TUnit`,
`BDTest.Contracts`, `BDTest.Razor.Reports`, `BDTest.SourceGenerator`, and `BDTest.Analyzers`.
Tests, pipeline tools, examples and the sample report host are not packable.
Project and directory names do not decide whether a project ships.

`PackProjectsModule` evaluates `IsPackable` through `dotnet msbuild` for each
project in Release configuration before packing it. `build/PackageMetadata.targets`
applies shared package metadata and the icon through `BeforeMicrosoftNETSdkTargets`,
after project opt-ins and before SDK defaults initialize XML documentation. It uses
the same `IsPackable` condition. It supplies a year-free copyright
and preserves compiler packages' explicit `IncludeSymbols=false` and analyzer
payloads. Runtime packages retain symbol packages and SDK package validation.
When adding a package, opt it in explicitly, supply its package ID, title,
description and tags, and update `PackageSelectionTests` and artifact verification.

Keep the existing package IDs for the next major version, including the
`BDTest.xUnit` casing for xUnit v3. `BDTest.Razor.Reports` has the display title
**BDTest - ASP.NET Core Reports**; its ID, namespaces and static asset paths do not
change. Keep `BDTest.ReportGenerator.RazorServer` as the non-shipping sample host.
Future store packages use the `BDTest.Reports.Storage` prefix; the planned IDs are
`BDTest.Reports.Storage.AzureBlob` and `BDTest.Reports.Storage.FileSystem`.
This decision requires no package rename or NuGet deprecation.

`BDTest.Pipeline.Tests/PackageSelectionTests.cs` evaluates the complete package
set, imported opt-ins, defaults, metadata and compiler payload settings. Run it
with `dotnet test BDTest.Pipeline.Tests -c Release --filter FullyQualifiedName~PackageSelectionTests`.

## Independent report contracts (#933)

`BDTest.Contracts` targets `netstandard2.0` and inherits nullable analysis and
warnings-as-errors from `Directory.Build.props`. It has no reference to the execution package.
`BDTest.Contracts.Tests` runs contract immutability and serialization checks with
only a project reference to contracts. The package opts into the same metadata,
strong-name signing, symbols and validation as the other runtime packages.
