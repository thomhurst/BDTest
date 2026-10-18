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
            ExplicitRunsIsolateContextOutputAndCompletion = 'Passed'
            ScenarioTimeoutHooks = 'Passed'
            ScenarioOutlineHooks = 'Passed'
            ContextFactoryInitializationAndAsyncDisposal = 'Passed'
            ContextRetryDisposesPreviousAttempt = 'Passed'
            ContextDisposalFailureReachesRunner = 'Failed'
        }
        # TUnit 1.75.0 does not translate InconclusiveTestException into a runner skip.
        if ($project -eq 'BDTest.TUnit.Tests') { $expected.FrameworkInconclusive = 'Failed' }
        $styles = if ($project -in @('BDTest.Tests', 'BDTest.TUnit.Tests')) { 2 } else { 1 }
        if ($results.Count -ne $expected.Count * $styles) {
            throw "$project expected $($expected.Count * $styles) probes, found $($results.Count). See $logPath"
        }
        foreach ($name in $expected.Keys) {
            $matching = @($results | Where-Object { ($_.testName -split '\.')[-1] -eq $name })
            if ($matching.Count -ne $styles -or @($matching | Where-Object outcome -ne $expected[$name]).Count -ne 0) {
                throw "$project $name must be $($expected[$name]). See $trxPath"
            }
            if ($expected[$name] -eq 'Failed') {
                $message = switch ($name) {
                    'Fail' { 'conformance failure' }
                    'NotImplemented' { 'conformance not implemented' }
                    'FrameworkInconclusive' { 'conformance inconclusive' }
                    'ContextDisposalFailureReachesRunner' { 'conformance context disposal' }
                }
                foreach ($result in $matching) {
                    $errorMessage = $result.SelectSingleNode(".//*[local-name()='ErrorInfo']/*[local-name()='Message']")
                    if (-not $errorMessage -or $errorMessage.InnerText -notmatch [regex]::Escape($message)) {
                        throw "$project $name failed for an unexpected reason. See $trxPath"
                    }
                }
            }
        }
        Write-Host "$project verified $($results.Count) runner outcomes."
    }

    # Exercise real setup/body/teardown failures on a fixture with an unrelated base.
    # Its final global hook checks awaited disposal, empty buffers and per-run reports.
    $resultPath = Join-Path $resultRoot 'tunit-composition'
    New-Item -ItemType Directory -Path $resultPath -Force | Out-Null
    $trxPath = Join-Path $resultPath 'composition.trx'
    if (Test-Path -LiteralPath $trxPath) { Remove-Item -LiteralPath $trxPath }
    $logPath = Join-Path $resultPath 'runner.log'
    & dotnet run --project (Join-Path $repoRoot 'BDTest.TUnit.Tests/BDTest.TUnit.Tests.csproj') -c $Configuration -- --treenode-filter '/*/*/CompositionLifecycleTests/*' --report-trx --report-trx-filename composition.trx --results-directory $resultPath *> $logPath
    if ($LASTEXITCODE -ne 2) { throw "TUnit composition must exit 2 for intentional failures. See $logPath" }
    [xml] $trx = Get-Content -LiteralPath $trxPath -Raw
    $results = @($trx.SelectNodes("//*[local-name()='UnitTestResult']"))
    if ($results.Count -ne 18) { throw "Expected 18 TUnit composition probes. See $logPath" }
    foreach ($name in @('RowsAndRepeatsKeepIdentityAndOutput', 'MethodMetadataAndRetry', 'SetupFailure', 'BodyFailure', 'TeardownFailure', 'ContextDisposalFailure')) {
        $matching = @($results | Where-Object testName -Like "$name*")
        $expectedCount = if ($name -eq 'RowsAndRepeatsKeepIdentityAndOutput') { 8 } else { 2 }
        $expectedOutcome = if ($name -like '*Failure') { 'Failed' } else { 'Passed' }
        if ($matching.Count -ne $expectedCount -or @($matching | Where-Object outcome -ne $expectedOutcome).Count -ne 0) {
            throw "TUnit composition $name expected $expectedCount $expectedOutcome results. See $trxPath"
        }
        if ($expectedOutcome -eq 'Failed') {
            $message = 'composition ' + ($name -replace 'Failure$', '').ToLowerInvariant() + ' failure'
            if ($name -eq 'ContextDisposalFailure') { $message = 'composition context disposal failure' }
            foreach ($result in $matching) {
                $errorMessage = $result.SelectSingleNode(".//*[local-name()='ErrorInfo']/*[local-name()='Message']")
                if (-not $errorMessage -or $errorMessage.InnerText -notmatch [regex]::Escape($message) -or $errorMessage.InnerText -match 'Expected|AssertionException' -or ($name -ne 'ContextDisposalFailure' -and $errorMessage.InnerText -match 'AfterEvery\(Test\) hook failed')) {
                    throw "TUnit composition $name failed unexpectedly or cleanup failed. See $trxPath"
                }
            }
        }
    }
    Write-Host 'TUnit composition verified 18 lifecycle outcomes.'
}
finally {
    $env:BDTEST_CONFORMANCE_RUNNER = $previousMode
}
Write-Host "Adapter conformance evidence: $resultRoot"
# GitHub Actions propagates LASTEXITCODE, including the intentional runner failures.
# Report success only after every exit code and TRX outcome has been verified.
exit 0
