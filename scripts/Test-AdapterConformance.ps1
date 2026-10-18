[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $ResultsDirectory
)

$ErrorActionPreference = 'Stop'
# Expected test failures must be inspected through TRX rather than native-command exceptions.
$PSNativeCommandUseErrorActionPreference = $false
$repoRoot = Split-Path $PSScriptRoot -Parent
$resultRoot = if ($ResultsDirectory) {
    [System.IO.Path]::GetFullPath($ResultsDirectory)
}
else {
    Join-Path ([System.IO.Path]::GetTempPath()) ('bdtest-adapter-conformance-' + [guid]::NewGuid().ToString('N'))
}
New-Item -ItemType Directory -Path $resultRoot -Force | Out-Null
$previousMode = $env:BDTEST_CONFORMANCE_RUNNER
try {
    $env:BDTEST_CONFORMANCE_RUNNER = '1'
    foreach ($project in @('BDTest.Tests', 'BDTest.MSTest.Tests', 'BDTest.xUnit.Tests', 'BDTest.TUnit.Tests')) {
        $projectPath = Join-Path $repoRoot "$project/$project.csproj"
        $resultPath = Join-Path $resultRoot $project
        New-Item -ItemType Directory -Path $resultPath -Force | Out-Null
        $trxPath = Join-Path $resultPath 'conformance.trx'
        # Never accept stale results if this runner fails before writing its report.
        if (Test-Path -LiteralPath $trxPath) { Remove-Item -LiteralPath $trxPath }
        $logPath = Join-Path $resultPath 'runner.log'
        if ($project -eq 'BDTest.TUnit.Tests') {
            & dotnet run --project $projectPath -c $Configuration -- --treenode-filter '/*/*/AdapterConformanceTests/*' --report-trx --report-trx-filename conformance.trx --results-directory $resultPath *> $logPath
        }
        else {
            & dotnet test $projectPath -c $Configuration --filter 'FullyQualifiedName~AdapterConformanceTests' --logger 'trx;LogFileName=conformance.trx' --results-directory $resultPath *> $logPath
        }
        $runnerExit = $LASTEXITCODE
        # VSTest returns 1 for failed tests; Microsoft.Testing.Platform returns 2.
        $expectedExit = if ($project -eq 'BDTest.TUnit.Tests') { 2 } else { 1 }
        if ($runnerExit -ne $expectedExit) { throw "$project must exit $expectedExit for intentional failures; got $runnerExit. See $logPath" }
        [xml] $trx = Get-Content -LiteralPath $trxPath -Raw
        $results = @($trx.SelectNodes("//*[local-name()='UnitTestResult']"))
        # NUnit and MSTest inconclusive results use NotExecuted in TRX. MSTest's
        # ReportSkip uses inconclusive; xUnit v3 falls back to skip for inconclusive.
        # Only NUnit has a success exception; other adapters use normal completion.
        $expected = @{
            Pass = 'Passed'
            Fail = 'Failed'
            NotImplemented = 'Failed'
            SingleStepSkipped = 'Passed'
            AllStepsSkipped = 'NotExecuted'
            GlobalRuleSkips = 'NotExecuted'
            GlobalRuleSkipsWithDirectAwait = 'NotExecuted'
            FrameworkSkip = 'NotExecuted'
            FrameworkInconclusive = 'NotExecuted'
            FrameworkSuccess = 'Passed'
            RetryThenPass = 'Passed'
            ExplicitRunsUseDifferentMappings = 'Passed'
            ScenarioTimeoutHooks = 'Passed'
            ScenarioOutlineHooks = 'Passed'
            ContextFactoryInitializationAndAsyncDisposal = 'Passed'
            ContextRetryDisposesPreviousAttempt = 'Passed'
            ContextDisposalFailureReachesRunner = 'Failed'
        }
        # TUnit 1.75.0 does not translate InconclusiveTestException into a runner skip.
        if ($project -eq 'BDTest.TUnit.Tests') { $expected.FrameworkInconclusive = 'Failed' }
        if ($results.Count -ne $expected.Count) {
            throw "$project expected $($expected.Count) probes, found $($results.Count). See $logPath"
        }
        foreach ($name in $expected.Keys) {
            $matching = @($results | Where-Object { ($_.testName -split '\.')[-1] -eq $name })
            if ($matching.Count -ne 1 -or $matching[0].outcome -ne $expected[$name]) {
                throw "$project $name must be $($expected[$name]). See $trxPath"
            }
            if ($expected[$name] -eq 'Failed') {
                $message = switch ($name) {
                    'Fail' { 'conformance failure' }
                    'NotImplemented' { 'conformance not implemented' }
                    'FrameworkInconclusive' { 'conformance inconclusive' }
                    'ContextDisposalFailureReachesRunner' { 'conformance context disposal' }
                }
                $errorMessage = $matching[0].SelectSingleNode(".//*[local-name()='ErrorInfo']/*[local-name()='Message']")
                if (-not $errorMessage -or $errorMessage.InnerText -notmatch [regex]::Escape($message)) {
                    throw "$project $name failed for an unexpected reason. See $trxPath"
                }
            }
        }
        Write-Host "$project verified $($results.Count) runner outcomes."
    }
}
finally {
    $env:BDTEST_CONFORMANCE_RUNNER = $previousMode
}
Write-Host "Adapter conformance evidence: $resultRoot"
# GitHub Actions propagates LASTEXITCODE, including the intentional runner failures.
# Report success only after every exit code and TRX outcome has been verified.
exit 0
