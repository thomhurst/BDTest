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

The TUnit core suite models optional callback state and missing repository records
as nullable. Required reflection metadata, JSON properties, HTTP content, and
exception assertion results fail explicitly when absent. Intentional null inputs
still exercise runtime guards. Context capture tests verify uniqueness within each case using independent execution
flows, so per-test fixture instances and filtered runs preserve the same checks. Standalone/core tests own configured runs instead of saving and restoring global
settings. Compatibility tests access obsolete lifecycle bridges through reflection,
preserving their behavior without disabling obsolete diagnostics.

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
`BDTest.Tests`, `BDTest.TUnit.Tests`, `BDTest.Pipeline.Tests` and `BDTest.ReportGenerator.Tests`, which use
`dotnet run --project <project> -c Release`. NUnit adapter and sample coverage lives in
`BDTest.NUnit.Tests`, run with `dotnet test`. Runner selection evaluates Release `PackageReference`
items, so imported or conditional TUnit references work and xUnit v3 executables
keep using VSTest. Both runner paths publish TRX under
`artifacts/test-results/<project>`.
Do not run the publishing pipeline in `Production` locally.

The report-generator migration (#949) preserves all 101 NUnit cases, including
argument cases: authentication 43, Azure storage 37, JSON streams 6, startup 8,
storage initialization 2 and personalization 5. None were removed or merged.
TUnit creates a separate fixture per case; authentication hosts and stores are
created and disposed by per-test hooks, and other tests own their local resources.
No suite-wide serialization is needed. The shared historical-report validator
uses framework-independent comparisons so both the core and host TUnit
suites check the same snapshot, including ordered collections and missing values.

Run the host suite with CI-compatible TRX output:

```powershell
dotnet run --project BDTest.ReportGenerator.Tests -c Release -- --report-trx --results-directory "$PWD/artifacts/test-results/BDTest.ReportGenerator.Tests"
dotnet run --project BDTest.ReportGenerator.Tests -c Release -- --treenode-filter "/*/*/AzureStorageDataStoreTests/*"
```

## NUnit extraction and core migration baseline (#962, #950)

At `8c485fb`, `dotnet test BDTest.Tests/BDTest.Tests.csproj -c Release --list-tests`
discovered **2,036 cases**, including argument cases and explicit probes. The
baseline TRX also contains 2,036 definitions: 1,986 passed and 50 were not executed.
The VSTest console summary counts 1,998 (1,986 passed + 12 skips); it excludes
38 explicit probes. Use discovered definitions, not that console total, for
migration accounting. NUnit repeats execute within a discovered case, so they do
not add definitions.

The extraction moves **140 cases** to `BDTest.NUnit.Tests` and retains **1,896**
in `BDTest.Tests`. No cases are removed or merged. Existing namespaces, method
names, attributes, argument values and repeat counts are preserved so discovery
identities can be compared directly. Comparing the baseline TRX `(className, testName)`
multiset with both extracted-suite TRX files found no missing or additional cases.
Framework-neutral nested context fixtures
stay in core; the two NUnit context fixtures move with their original outer
story attribute. The example report-isolation method moves separately from its
core fixture, preserving its full test name and three repetitions.

| Moved coverage | Discovered cases |
| --- | ---: |
| Shared adapter and assembly conformance | 40 |
| Composition tests and explicit composition probes | 30 |
| Runner outcome tests and explicit outcome probes | 34 |
| Context lifetime, identity, lifecycle and teardown | 18 |
| Preparation retry and retry | 3 |
| Skip outcomes | 6 |
| Method identity | 4 |
| API sample, README scenario and report isolation | 5 |
| **Total** | **140** |

`BDTest.NUnit.Tests` is unsigned, references the NUnit sample and adapter, and
runs their shared contracts with the source generator and analyzers enabled.
The core suite now references `BDTest.TUnit` and has no NUnit, NUnit3TestAdapter,
Microsoft.NET.Test.Sdk or sample dependency. Its local TUnit assertion and
output-directory helpers preserve the same behavior as the NUnit helpers linked
by the dedicated adapter suite. Both suites still share the framework-independent
context fixture and do not reference each other's assemblies.
`OutputCaptureInspection` preserves internal capture/buffer cleanup checks via
reflection and fails explicitly if those members disappear. It adds no friend
grant or shipped API. Core and Razor reports still grant friend access only to
the signed `BDTest.Tests` assembly.

Run the dedicated suite and the native runner outcome checks with:

```powershell
dotnet test BDTest.NUnit.Tests/BDTest.NUnit.Tests.csproj -c Release
pwsh scripts/Test-SkipOutcomes.ps1
pwsh scripts/Test-AdapterConformance.ps1
```

The solution includes the new project. The pipeline discovers it by its existing
Tests-project rule and selects VSTest from its evaluated package references;
TRX results go to `artifacts/test-results/BDTest.NUnit.Tests`.

## Completed core TUnit migration (#950)

Immediately before conversion, at `cf906e3`, core discovery and the baseline TRX
contained **1,905 cases**: the retained 1,896 plus nine `CompactReportPageTests`
added after extraction. That baseline passed 1,903 cases and skipped two Windows
symbolic-link privilege probes.

Five additional NUnit-specific cases belong in `BDTest.NUnit.Tests`: the native
success exception and four scenario-hook assertion-scope cases. Their NUnit
names, inputs and assertions are preserved, bringing that suite to **145 discovered
definitions**, including explicit runner probes. Core retains **1,900 logical
cases** from that baseline; none were removed. Integrating #967 adds 27 attachment
rendering cases (26 view cases and one upload integration case), all converted
to TUnit, for **1,927 logical cases**. TUnit exposes repetitions as separate definitions:
eight output-capture cases run ten times (`Repeat(9)`), and two isolation cases
run three times (`Repeat(2)`). Thus core discovers **2,003 executable cases**
(1,927 + 72 + 4), preserving the number of executions. NUnit class arguments
become TUnit class arguments; matrix values and typed data sources preserve every
row. The two parameterized report fixtures now share a class identity across
argument sets in TRX, while their case counts remain 54 and 44.
The eight standalone parallel `Test1`–`Test8` captures become eight argument rows
of `ContextIsIndependentOfOtherFlows`; `ZZZ_EnsureContextsAreUnique` becomes
`EnsureContextsAreUnique`, without cross-case ordering or completion dependencies.
`NUNitIgnoreException` becomes `TUnitSkipException`; native NUnit skip coverage
continues in the dedicated adapter suite.

Run core tests and focused fixtures with:

```powershell
dotnet run --project BDTest.Tests -c Release -- --report-trx --results-directory "$PWD/artifacts/test-results/BDTest.Tests"
dotnet run --project BDTest.Tests -c Release -- --treenode-filter "/*/*/ScenarioOutlineTests/*"
```

The TUnit `TUNU0001` migration fixer was applied and then reviewed manually.
Unsupported assertion chains, exact/dynamic exception checks, collection ordering
and item identity, matrix data, fixture arguments, cultures, hooks and helper
awaits were corrected. In particular, the fixer discarded fluent retry chains;
those scenarios were restored. Synchronous context callbacks and allocation/GC
probes stay synchronous, with explicit invariant checks where awaiting would
change the behavior under test.

Every test gets a fresh fixture. Report hosts use per-test setup/disposal and
run owners isolate framework registrations and stores. Context isolation tests
create their own sequential/concurrent callers instead of waiting for other test
cases. Targeted `NotInParallel` constraints protect shared counters and real-time
expiry assertions; other cases retain parallel execution. Narrow, documented
TUnit analyzer suppressions cover intentional per-case state, public BDTest
retry hooks, and the custom worker entry point; nullable analysis, warnings-as-errors,
source generators and BDTest analyzers remain enabled.

The macOS file-store CI job uses `dotnet run` with the TUnit tree-node filter
`/*/*/FileSystemReportStoreTests/*`.

The executable dispatches `file-store-worker` arguments to the existing isolated
process protocol and all normal runner arguments to the generated Microsoft
Testing Platform application, retaining extension registration and TRX output.
Signing and friend grants are unchanged: only signed `BDTest.Tests` has access
to core and Razor internals. Pipeline runner selection still uses evaluated
package references, including VSTest for xUnit executable projects.

Initial validation on Windows with SDK 10.0.402: Release solution build completed with
zero warnings/errors. Two full core runs with default parallel execution each
reported 1,974 passed and the same two symbolic-link privilege skips as the
baseline (1,976 total). All eleven other test projects were exercised; the
pipeline's changed runner-selection expectation passed a focused rerun.
Signing verification, native skip outcomes (21), adapter conformance (155),
additional native lifecycle outcomes (46), and the documentation build passed.

After integrating #967, the Release build and all eleven other test projects
passed. The full core run passed 2,001 cases with two expected symbolic-link
skips (2,003 total). The first run overlapped other suites and npm installation
and hit the unchanged 10-second attachment disk-write guard; the full rerun with
no competing validation passed. The attachment ownership regression now permits
parallel execution and joins its background writer with a five-minute cooperative
cancellation guard instead of a ten-second disk-throughput deadline (#983).
Validate it alongside other build/test activity. The file-store CI command selected all 25
cases (23 passed and two Windows skips); native Darwin locking remains covered
by macOS CI. Signing, report controls and the documentation build also passed.

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
`BDTest.xUnit`, `BDTest.MSTest`, `BDTest.TUnit`, `BDTest.Contracts`, `BDTest.Reporting`, `BDTest.Razor.Reports`,
`BDTest.Reports.Storage.AzureBlob` and `BDTest.Reports.Storage.FileSystem`.
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
`BDTest.Contracts`, `BDTest.Reporting`, `BDTest.Razor.Reports`, `BDTest.Reports.Storage.FileSystem`,
`BDTest.Reports.Storage.AzureBlob`,
`BDTest.SourceGenerator`, and `BDTest.Analyzers`.
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
Store packages use the `BDTest.Reports.Storage` prefix.
`BDTest.Reports.Storage.FileSystem` and `BDTest.Reports.Storage.AzureBlob` are signed,
packable .NET 10 runtime packages with packed READMEs.
This decision requires no package rename or NuGet deprecation.

`BDTest.Pipeline.Tests/PackageSelectionTests.cs` evaluates the complete package
set, imported opt-ins, defaults, metadata and compiler payload settings. Run it
with `dotnet run --project BDTest.Pipeline.Tests -c Release -- --treenode-filter "/*/*/PackageSelectionTests/*"`.

## Independent report contracts (#933)

`BDTest.Contracts` targets `netstandard2.0` and inherits nullable analysis and
warnings-as-errors from `Directory.Build.props`. It has no reference to the execution package.
`BDTest.Contracts.Tests` runs contract immutability and serialization checks with
only a project reference to contracts. The package opts into the same metadata,
strong-name signing, symbols and validation as the other runtime packages.

## Public API baselines (#974)

The unshipped entries reconciled in #980 record the immutable result migration
already merged in #968, including contract enums, pending step definitions,
builder/hook results and JSON metadata. The original shipped snapshot stays
intact; explicit removal entries preserve the migration's audit trail. Core's
contract and legacy JSON serializers use separate compact and explicit-formatting
overloads to satisfy the optional-parameter overload compatibility diagnostics.

The consumer namespace migration (#973) records removed old names and replacement
root names in unshipped baselines for each affected target. It preserves existing
caller-info overloads and their optional defaults. Those moved methods carry
individual `RS0026` suppressions: the overload sets already existed before the
namespace move. Addition/removal diagnostics remain enabled, and newly added
methods do not inherit these exceptions. Shipped snapshots remain unchanged.

Every packable runtime project imports `build/PublicApi.targets` through the
existing package metadata hook. It privately references
`Microsoft.CodeAnalysis.PublicApiAnalyzers`; compiler extensions
(`IsRoslynComponent=true`), tests, samples and tooling are excluded. The analyzer
and repository configuration do not flow to NuGet consumers. This complements
SDK package validation: ordinary builds now check the reviewed source API, while
packing still compares compatible target assets.

Each runtime project's `PublicAPI/<TargetFramework>/` directory contains
`PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`. Both start with
`#nullable enable`. The initial shipped files snapshot the reviewed main-branch
surface when enforcement was introduced; they are not a claim that every API in
the next-major roadmap has already been released on NuGet. Separate files cover
every existing target, including modern-only `TimeProvider` APIs. A missing file
fails the build rather than silently disabling analysis. New runtime packages and
new targets must provide both files before they can build.

Public and protected types/members are tracked, including nullable signatures,
optional parameter defaults and the documented public JSON metadata contexts.
Razor's `AspNetCoreGeneratedDocument` namespace is excluded in
`build/PublicApi.globalconfig`: generated view classes are implementation details,
not supported consumer APIs. Do not add handwritten supported APIs there.
The three public JSON context types have a scoped `RS0041` exception because
System.Text.Json deliberately generates nullable-oblivious `JsonTypeInfo`
properties. Those signatures remain recorded with the analyzer's `~` marker and
remain subject to addition/removal checks. Handwritten APIs outside these generated
context types retain `RS0041` and the existing nullable compiler diagnostics.

For an intentional API change:

1. Review its compatibility and update the matching usage/migration documentation.
   Baseline edits acknowledge a change; they do not make a breaking change safe.
2. Build every target of the affected project. `RS0016` reports an unrecorded
   signature and `RS0017` reports a stale one. Add the exact signature (including
   `!`, `?`, defaults and modifiers) to the corresponding
   `PublicAPI.Unshipped.txt`, or use the analyzer's IDE code fix. Check each target:
   an IDE code fix may operate only on its active framework.
3. For an approved removal or signature change, keep the original shipped entry
   and add `*REMOVED*` followed immediately by that exact entry in the unshipped
   file. Record the replacement as a new entry. If the old entry was itself
   unshipped, edit/remove that entry instead. Keep entries sorted within each file.
4. Build the full solution and run relevant consumer tests. Include baseline
   changes in the same PR as source changes, including future namespace migrations
   and result-contract work from #935. The initial snapshot already includes the
   helper internalization from #827/#975. Never regenerate the
   shipped snapshot to hide an unexplained difference or disable API diagnostics.
5. When preparing a release, fold reviewed unshipped additions into shipped,
   remove entries named by `*REMOVED*`, and reset unshipped to `#nullable enable`.
   Rebuild every target after this deliberate baseline promotion.

```powershell
dotnet build BDTest.sln -c Release
pwsh scripts/Test-PublicApiBaselines.ps1
```

The regression script builds a disposable multi-target fixture using the same
production imports. It proves that unrecorded public/protected additions, stale
removals, nullable changes, missing files and wrong-target entries fail, and that
intentional baseline updates pass. It also checks excluded projects and the
generated-code boundary. CI runs it before the ordinary pipeline; logs are saved
under `artifacts/test-results/public-api/`.

## Independent hosted report consumers (#936)

Razor reports, the Azure Blob store and the sample host consume `BDTest.Contracts`
without a project or assembly reference to execution. `HostedReportJson` dispatches
schema version 1 and unversioned historical JSON to immutable results. ReportGenerator
Tests also have no execution reference and assert the consumer assembly boundary.
The core suite keeps legacy fixture builders and snapshots them at host boundaries.

The hosted API baselines record contract types and the public schema-aware reader.
The existing snapshot APIs from #935 are also recorded in core/contracts baselines;
`Serialize` has explicit one-argument and two-argument overloads for each report model
so their source call patterns work without overlapping optional-parameter overloads.
See [host migration](docs/docs/migration/immutable-results.md#hosted-reports).

## Execution-side reporting package (#992)

`BDTest.Reporting` is a signed, packable runtime package targeting
`netstandard2.0;net8.0`. It depends on core; core and adapters never depend on it.
The solution, package-selection tests and signing verification include it.
It owns the shared export builder, JSON writer/historical reader, standalone HTML
and HTTP uploader. Core's immutable `GetSnapshot()` boundary reuses `ResultSnapshot`;
reporting receives no friend access. The legacy generated `BDTestJsonContext`
stays with the execution models so their internal serialization members remain
private. `HtmlWriter` stays in core because it captures evidence during execution.
The package grants only the signed core test suite access to its internal builder.

Public API baselines record moved types as removals from core and additions to
reporting; namespaces and signatures stay the same. This is an authorized
next-major assembly move, requiring a package reference and consumer rebuild.
See [consumer migration](docs/docs/migration/reporting-package.md).
