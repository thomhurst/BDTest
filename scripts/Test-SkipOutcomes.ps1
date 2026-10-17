[CmdletBinding()]
param([string] $Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$resultRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('bdtest-skip-outcomes-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $resultRoot | Out-Null

# Exit success alone cannot detect a regression from Skipped to Passed.
# Inspect the runner's TRX as well as the status assertions inside each probe.
$projects = @(
    @{ Name = 'BDTest.Tests'; Skips = @('GlobalRuleSkips', 'GlobalRuleSkipsSynchronously', 'AllStepsSkip', 'FrameworkSkipInStep', 'GenuineInconclusive') },
    @{ Name = 'BDTest.MSTest.Tests'; Skips = @('GlobalRuleSkips', 'GlobalRuleSkipsSynchronously', 'AllStepsSkip', 'GenuineInconclusive') },
    @{ Name = 'BDTest.TUnit.Tests'; Skips = @('GlobalRuleSkips', 'GlobalRuleSkipsSynchronously', 'AllStepsSkip', 'FrameworkSkipInStep') }
)

foreach ($project in $projects) {
    $projectPath = Join-Path $repoRoot ($project.Name + '/' + $project.Name + '.csproj')
    $resultPath = Join-Path $resultRoot $project.Name
    if ($project.Name -eq 'BDTest.TUnit.Tests') {
        & dotnet run --project $projectPath -c $Configuration -- --treenode-filter '/*/*/SkipOutcomeTests/*' --report-trx --report-trx-filename outcomes.trx --results-directory $resultPath
    }
    else {
        & dotnet test $projectPath -c $Configuration --filter 'FullyQualifiedName~SkipOutcomeTests' --logger 'trx;LogFileName=outcomes.trx' --results-directory $resultPath
    }
    if ($LASTEXITCODE -ne 0) { throw "$($project.Name) runner failed. Results: $resultPath" }

    [xml] $trx = Get-Content -LiteralPath (Join-Path $resultPath 'outcomes.trx') -Raw
    $results = @($trx.SelectNodes("//*[local-name()='UnitTestResult']"))
    $expected = @{ PartialStepsPass = 'Passed' }
    foreach ($name in $project.Skips) { $expected[$name] = 'NotExecuted' }
    if ($results.Count -ne $expected.Count) {
        throw "$($project.Name): expected $($expected.Count) probes, found $($results.Count). Results: $resultPath"
    }
    foreach ($name in $expected.Keys) {
        $matching = @($results | Where-Object { ($_.testName -split '\.')[-1] -eq $name })
        if ($matching.Count -ne 1 -or $matching[0].outcome -ne $expected[$name]) {
            throw "$($project.Name): $name must be $($expected[$name]). Results: $resultPath"
        }
    }
    Write-Host "$($project.Name): verified $($results.Count) runner outcomes."
}
Write-Host "Runner outcome evidence: $resultRoot"
