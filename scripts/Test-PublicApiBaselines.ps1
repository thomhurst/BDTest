[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$runId = [Guid]::NewGuid().ToString('N')
$fixtureParent = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts/public-api-regression'))
$fixtureRoot = Join-Path $fixtureParent $runId
$logRoot = Join-Path $repoRoot "artifacts/test-results/public-api/$runId"
$null = New-Item -ItemType Directory -Path $fixtureRoot, $logRoot -Force
$metadataTargets = [Security.SecurityElement]::Escape((Join-Path $repoRoot 'build/PackageMetadata.targets'))
$script:checkCount = 0

function Write-FixtureFile([string] $RelativePath, [string] $Content) {
    $path = Join-Path $fixtureRoot $RelativePath
    $null = New-Item -ItemType Directory -Path (Split-Path $path -Parent) -Force
    [IO.File]::WriteAllText($path, $Content.Replace("`r`n", "`n"))
}

function Invoke-FixtureBuild([string] $Name, [string[]] $ExpectedErrors = @()) {
    $log = Join-Path $logRoot "$Name.log"
    # Rebuild ensures every mutation is compiled even on file systems with coarse timestamps.
    & dotnet build (Join-Path $fixtureRoot 'Fixture.csproj') -t:Rebuild -c Release --nologo -v:minimal *> $log
    $exitCode = $LASTEXITCODE
    $output = Get-Content -LiteralPath $log -Raw
    if (($ExpectedErrors.Count -eq 0 -and $exitCode -ne 0) -or ($ExpectedErrors.Count -gt 0 -and $exitCode -eq 0)) {
        throw "$Name returned $exitCode. See $log`n$output"
    }
    foreach ($expected in $ExpectedErrors) {
        if ($output -notmatch $expected) {
            throw "$Name did not report '$expected'. See $log`n$output"
        }
    }
    $script:checkCount++
    Write-Host "PASS: $Name"
}

function Write-FixtureProject([bool] $Packable = $true, [bool] $CompilerComponent = $false, [bool] $WarningsAsErrors = $true) {
    Write-FixtureFile 'Fixture.csproj' @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net8.0</TargetFrameworks>
    <IsPackable>$($Packable.ToString().ToLowerInvariant())</IsPackable>
    <IsRoslynComponent>$($CompilerComponent.ToString().ToLowerInvariant())</IsRoslynComponent>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>$($WarningsAsErrors.ToString().ToLowerInvariant())</TreatWarningsAsErrors>
  </PropertyGroup>
  <Import Project="$metadataTargets" />
</Project>
"@
}

function Assert-FixtureItems([bool] $ExpectedOptIn) {
    foreach ($framework in @('netstandard2.0', 'net8.0')) {
        $json = & dotnet msbuild (Join-Path $fixtureRoot 'Fixture.csproj') "-p:TargetFramework=$framework" '-getItem:PackageReference,AdditionalFiles'
        if ($LASTEXITCODE -ne 0) { throw 'Could not evaluate fixture items.' }
        $items = ($json -join "`n" | ConvertFrom-Json).Items
        $analyzers = @($items.PackageReference | Where-Object Identity -eq 'Microsoft.CodeAnalysis.PublicApiAnalyzers')
        $baselines = @($items.AdditionalFiles | Where-Object Identity -match 'PublicAPI\.(Shipped|Unshipped)\.txt$')
        if ($ExpectedOptIn) {
            if ($analyzers.Count -ne 1 -or [string]::IsNullOrWhiteSpace($analyzers[0].Version) -or $analyzers[0].PrivateAssets -ne 'all') {
                throw "Incorrect analyzer dependency/version/privacy for $framework."
            }
            $expectedPaths = @('Shipped', 'Unshipped') | ForEach-Object {
                [IO.Path]::GetFullPath((Join-Path $fixtureRoot "PublicAPI/$framework/PublicAPI.$_.txt"))
            }
            $actualPaths = @($baselines | ForEach-Object { [IO.Path]::GetFullPath($_.Identity) })
            if ($actualPaths.Count -ne 2 -or @(Compare-Object $expectedPaths $actualPaths).Count -ne 0) {
                throw "Incorrect per-target baseline selection for $framework."
            }
        }
        elseif ($analyzers.Count -ne 0 -or $baselines.Count -ne 0) {
            throw "An excluded project opted into public API tracking for $framework."
        }
    }
    $script:checkCount++
    Write-Host "PASS: evaluated analyzer and baseline items (opt-in: $ExpectedOptIn)"
}

$source = @'
namespace Fixture
{
    public abstract class Api
    {
        protected Api() { }
        public int Existing() => 1;
        public string? Echo(string? value) => value;
#if NET8_0
        public int Modern() => 8;
#endif
        // ADDITION
    }
}
'@
$shipped = @'
#nullable enable
Fixture.Api
Fixture.Api.Api() -> void
Fixture.Api.Echo(string? value) -> string?
Fixture.Api.Existing() -> int
'@

function Reset-Baselines {
    foreach ($framework in @('netstandard2.0', 'net8.0')) {
        $text = $shipped + "`n"
        if ($framework -eq 'net8.0') { $text += "Fixture.Api.Modern() -> int`n" }
        Write-FixtureFile "PublicAPI/$framework/PublicAPI.Shipped.txt" $text
        Write-FixtureFile "PublicAPI/$framework/PublicAPI.Unshipped.txt" "#nullable enable`n"
    }
}

try {
    # Stop repository-wide props/targets discovery; import the production package policy explicitly.
    Write-FixtureFile 'Directory.Build.props' '<Project />'
    Write-FixtureFile 'Directory.Build.targets' '<Project />'
    Write-FixtureProject
    Write-FixtureFile 'Api.cs' $source
    Reset-Baselines
    Assert-FixtureItems $true
    Invoke-FixtureBuild 'recorded-api'

    Write-FixtureFile 'Api.cs' ($source.Replace('// ADDITION', 'public int Added() => 2;'))
    Invoke-FixtureBuild 'unrecorded-public-addition' @('error RS0016.*Added')
    Write-FixtureProject -WarningsAsErrors $false
    Invoke-FixtureBuild 'addition-without-warnings-as-errors' @('error RS0016.*Added')
    Write-FixtureProject
    foreach ($framework in @('netstandard2.0', 'net8.0')) {
        Write-FixtureFile "PublicAPI/$framework/PublicAPI.Unshipped.txt" "#nullable enable`nFixture.Api.Added() -> int`n"
    }
    Invoke-FixtureBuild 'recorded-public-addition'

    Reset-Baselines
    Write-FixtureFile 'Api.cs' ($source.Replace('// ADDITION', 'protected int Added() => 2;'))
    Invoke-FixtureBuild 'unrecorded-protected-addition' @('error RS0016.*Added')

    Write-FixtureFile 'Api.cs' ($source.Replace('public int Existing() => 1;', ''))
    Invoke-FixtureBuild 'stale-removed-api' @('error RS0017.*Existing')
    Write-FixtureProject -WarningsAsErrors $false
    Invoke-FixtureBuild 'removal-without-warnings-as-errors' @('error RS0017.*Existing')
    Write-FixtureProject
    foreach ($framework in @('netstandard2.0', 'net8.0')) {
        Write-FixtureFile "PublicAPI/$framework/PublicAPI.Unshipped.txt" "#nullable enable`n*REMOVED*Fixture.Api.Existing() -> int`n"
    }
    Invoke-FixtureBuild 'deliberately-removed-api'

    Reset-Baselines
    Write-FixtureFile 'Api.cs' ($source.Replace('string? Echo', 'string Echo').Replace('=> value;', '=> value ?? "";'))
    Invoke-FixtureBuild 'nullable-contract-change' @('error RS0016.*Echo', 'error RS0017.*Echo')

    Reset-Baselines
    Write-FixtureFile 'Api.cs' $source
    Write-FixtureFile 'RazorView.g.cs' @'
// <auto-generated />
#nullable disable
namespace AspNetCoreGeneratedDocument
{
    public static class View
    {
        public static string Render() => "";
    }
}
'@
    Invoke-FixtureBuild 'excluded-razor-generated-namespace'
    Remove-Item -LiteralPath (Join-Path $fixtureRoot 'RazorView.g.cs')

    # Recording an oblivious signature must not hide nullable-policy diagnostics in handwritten code.
    Write-FixtureFile 'Api.cs' ($source.Replace('// ADDITION', "#nullable disable`n        public string Oblivious(string value) => value;`n#nullable enable"))
    foreach ($framework in @('netstandard2.0', 'net8.0')) {
        Write-FixtureFile "PublicAPI/$framework/PublicAPI.Unshipped.txt" "#nullable enable`n~Fixture.Api.Oblivious(string value) -> string`n"
    }
    Invoke-FixtureBuild 'handwritten-oblivious-api' @('error RS0041.*Oblivious')

    # Model JSON generator output: suppress only its oblivious-nullability diagnostic on the partial type.
    Reset-Baselines
    $suppressedSource = $source.Replace('public abstract class Api', '[System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0041")] public abstract partial class Api')
    Write-FixtureFile 'Api.cs' $suppressedSource
    $generatedSource = @'
// <auto-generated />
#nullable disable
namespace Fixture
{
    public abstract partial class Api
    {
        public string Generated(string value) => value;
        // GENERATED ADDITION
    }
}
'@
    Write-FixtureFile 'Api.g.cs' $generatedSource
    foreach ($framework in @('netstandard2.0', 'net8.0')) {
        Write-FixtureFile "PublicAPI/$framework/PublicAPI.Unshipped.txt" "#nullable enable`n~Fixture.Api.Generated(string value) -> string`n"
    }
    Invoke-FixtureBuild 'recorded-generated-api-with-nullability-suppression'
    Write-FixtureFile 'Api.g.cs' ($generatedSource.Replace('// GENERATED ADDITION', 'public string AddedGenerated(string value) => value;'))
    Invoke-FixtureBuild 'unrecorded-generated-api-with-nullability-suppression' @('error RS0016.*AddedGenerated')
    Remove-Item -LiteralPath (Join-Path $fixtureRoot 'Api.g.cs')

    Write-FixtureFile 'Api.cs' $source
    foreach ($kind in @('Shipped', 'Unshipped')) {
        Reset-Baselines
        Remove-Item -LiteralPath (Join-Path $fixtureRoot "PublicAPI/net8.0/PublicAPI.$kind.txt")
        Invoke-FixtureBuild "missing-$kind-baseline" @("Missing PublicAPI/net8.0/PublicAPI\.$kind\.txt")
    }

    Reset-Baselines
    Write-FixtureFile 'PublicAPI/net8.0/PublicAPI.Shipped.txt' ($shipped + "`n")
    Invoke-FixtureBuild 'missing-api-from-modern-target' @('error RS0016.*Modern.*net8\.0')
    Reset-Baselines
    Write-FixtureFile 'PublicAPI/netstandard2.0/PublicAPI.Shipped.txt' ($shipped + "`nFixture.Api.Modern() -> int`n")
    Invoke-FixtureBuild 'extra-api-in-standard-target' @('error RS0017.*Modern.*netstandard2\.0')

    # Missing baselines and public APIs must not trigger the policy in excluded projects.
    foreach ($framework in @('netstandard2.0', 'net8.0')) {
        foreach ($kind in @('Shipped', 'Unshipped')) {
            Remove-Item -LiteralPath (Join-Path $fixtureRoot "PublicAPI/$framework/PublicAPI.$kind.txt")
        }
    }
    Write-FixtureProject -CompilerComponent $true
    Assert-FixtureItems $false
    Invoke-FixtureBuild 'excluded-compiler-component'
    Write-FixtureProject -Packable $false
    Assert-FixtureItems $false
    Invoke-FixtureBuild 'excluded-nonpackable-consumer'

    Write-Host "Public API baseline regression checks passed: $script:checkCount. Logs: $logRoot"
}
finally {
    # Delete only this invocation's GUID fixture, never a caller-provided or shared directory.
    $resolvedFixture = [IO.Path]::GetFullPath($fixtureRoot)
    if ([IO.Path]::GetDirectoryName($resolvedFixture) -ne $fixtureParent -or [IO.Path]::GetFileName($resolvedFixture) -ne $runId) {
        throw "Refusing to delete unexpected fixture path: $resolvedFixture"
    }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
