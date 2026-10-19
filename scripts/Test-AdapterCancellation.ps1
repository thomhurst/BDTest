[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $ResultsDirectory
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repoRoot = Split-Path $PSScriptRoot -Parent
$resultRoot = if ($ResultsDirectory) { [System.IO.Path]::GetFullPath($ResultsDirectory) }
else { Join-Path ([System.IO.Path]::GetTempPath()) ('bdtest-cancellation-' + [guid]::NewGuid().ToString('N')) }
$previousResults = $env:BDTEST_CANCELLATION_RESULTS
try {
    foreach ($project in @('BDTest.NUnit.Tests', 'BDTest.MSTest.Tests', 'BDTest.xUnit.Tests', 'BDTest.TUnit.Tests')) {
        $resultPath = Join-Path $resultRoot $project
        New-Item -ItemType Directory -Path $resultPath -Force | Out-Null
        # A fresh child directory prevents an earlier run's evidence from satisfying this run.
        $evidencePath = Join-Path $resultPath ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $evidencePath | Out-Null
        $env:BDTEST_CANCELLATION_RESULTS = $evidencePath
        $projectPath = Join-Path $repoRoot "$project/$project.csproj"
        $logPath = Join-Path $evidencePath 'runner.log'
        if ($project -eq 'BDTest.TUnit.Tests') {
            & dotnet run --project $projectPath -c $Configuration -- --treenode-filter '/*/*/AdapterConformanceTests/NativeFrameworkCancellation*' --report-trx --report-trx-filename cancellation.trx --results-directory $evidencePath *> $logPath
        }
        else {
            & dotnet test $projectPath -c $Configuration --filter 'FullyQualifiedName~NativeFrameworkCancellation' --logger 'trx;LogFileName=cancellation.trx' --results-directory $evidencePath *> $logPath
        }
        $runnerExit = $LASTEXITCODE
        # Native cancellation may fail the outer test. It must still complete all
        # in-test assertions and emit evidence for every host/explicit-token pair.
        $expectedExit = if ($project -eq 'BDTest.TUnit.Tests') { 2 } else { 0 }
        if ($runnerExit -ne $expectedExit) { throw "$project runner exited $runnerExit. See $logPath" }
        [xml] $trx = Get-Content -LiteralPath (Join-Path $evidencePath 'cancellation.trx') -Raw
        $results = @($trx.SelectNodes("//*[local-name()='UnitTestResult']"))
        if ($results.Count -ne 4) { throw "$project expected 4 native cancellation probes, found $($results.Count). See $logPath" }
        $expectedOutcome = if ($project -eq 'BDTest.TUnit.Tests') { 'Failed' } else { 'Passed' }
        if (@($results | Where-Object outcome -ne $expectedOutcome).Count -ne 0) {
            throw "$project returned an unexpected runner outcome. See $logPath"
        }
        foreach ($style in @('inheritance', 'composition')) {
            foreach ($explicit in @('False', 'True')) {
                $evidence = Join-Path $evidencePath "$style-$explicit.txt"
                if (!(Test-Path -LiteralPath $evidence) -or (Get-Content -LiteralPath $evidence -Raw) -ne 'verified') {
                    throw "$project did not verify $style with explicit token $explicit. See $logPath"
                }
            }
        }
        Write-Host "${project}: native cancellation verified for both host styles, with and without an explicit token."
    }
}
finally { $env:BDTEST_CANCELLATION_RESULTS = $previousResults }
# GitHub Actions propagates LASTEXITCODE, including the expected TUnit cancellation exit.
# Report success only after every runner outcome and evidence file has been verified.
exit 0
