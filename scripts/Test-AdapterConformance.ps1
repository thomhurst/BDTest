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
$previousCleanupFile = $env:BDTEST_CONFORMANCE_CLEANUP_FILE
try {
    $env:BDTEST_CONFORMANCE_RUNNER = '1'
    foreach ($project in @('BDTest.NUnit.Tests', 'BDTest.MSTest.Tests', 'BDTest.xUnit.Tests', 'BDTest.TUnit.Tests')) {
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
            $filter = 'FullyQualifiedName~AdapterConformanceTests'
            if ($project -eq 'BDTest.xUnit.Tests') {
                $filter += '|FullyQualifiedName~NativeCompositionFailureTests|FullyQualifiedName~NativeInheritanceFailureTests'
                $cleanupFile = Join-Path $resultPath 'native-cleanup.txt'
                if (Test-Path -LiteralPath $cleanupFile) { Remove-Item -LiteralPath $cleanupFile }
                $env:BDTEST_CONFORMANCE_CLEANUP_FILE = $cleanupFile
            }
            & dotnet test $projectPath -c $Configuration --filter $filter --logger 'trx;LogFileName=conformance.trx' --results-directory $resultPath *> $logPath
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
        if ($project -eq 'BDTest.xUnit.Tests') { $expected.ExplicitAdapterRunsIsolateContextOutputAndCompletion = 'Passed' }
        # TUnit 1.75.0 does not translate InconclusiveTestException into a runner skip.
        if ($project -eq 'BDTest.TUnit.Tests') { $expected.FrameworkInconclusive = 'Failed' }
        $fixtures = if ($project -in @('BDTest.NUnit.Tests', 'BDTest.xUnit.Tests')) {
            @('AdapterConformanceTests', 'CompositionAdapterConformanceTests')
        }
        else { @('AdapterConformanceTests') }
        $styles = if ($project -in @('BDTest.TUnit.Tests', 'BDTest.MSTest.Tests')) { 2 } else { 1 }
        $failureProbes = if ($project -eq 'BDTest.xUnit.Tests') { 9 } else { 0 }
        $expectedCount = $expected.Count * $fixtures.Count * $styles + $failureProbes
        if ($results.Count -ne $expectedCount) {
            throw "$project expected $expectedCount probes, found $($results.Count). See $logPath"
        }
        $definitions = @{}
        foreach ($definition in $trx.SelectNodes("//*[local-name()='UnitTest']")) {
            $definitions[$definition.id] = $definition.SelectSingleNode("./*[local-name()='TestMethod']").className
        }
        foreach ($fixture in $fixtures) {
            $fixtureResults = if ($fixtures.Count -gt 1) {
                @($results | Where-Object { ($definitions[$_.testId] -split ',')[0] -eq "BDTest.Adapters.Conformance.$fixture" })
            }
            else { $results }
            $expectedFixtureCount = $expected.Count * $styles
            if ($fixtureResults.Count -ne $expectedFixtureCount) {
                throw "$project $fixture expected $expectedFixtureCount probes, found $($fixtureResults.Count). See $trxPath"
            }
            foreach ($name in $expected.Keys) {
                $matching = @($fixtureResults | Where-Object { ($_.testName -split '\.')[-1] -eq $name })
                if ($matching.Count -ne $styles -or @($matching | Where-Object outcome -ne $expected[$name]).Count -ne 0) {
                    throw "$project $fixture.$name must be $($expected[$name]). See $trxPath"
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
                            throw "$project $fixture.$name failed for an unexpected reason. See $trxPath"
                        }
                    }
                }
            }
        }
        if ($project -eq 'BDTest.xUnit.Tests') {
            foreach ($style in @('NativeCompositionFailureTests', 'NativeInheritanceFailureTests')) {
                $failures = @('ConstructorFailure', 'SetupFailure', 'BodyFailure', 'TeardownFailure')
                if ($style -eq 'NativeCompositionFailureTests') { $failures += 'ContextDisposalFailure' }
                foreach ($name in $failures) {
                    $matching = @($results | Where-Object { $_.testName.EndsWith("$style.$name") })
                    if ($matching.Count -ne 1 -or $matching[0].outcome -ne 'Failed') {
                        throw "$style.$name must fail. See $trxPath"
                    }
                    $message = $matching[0].SelectSingleNode(".//*[local-name()='ErrorInfo']/*[local-name()='Message']")
                    if (-not $message -or $message.InnerText -notmatch [regex]::Escape("native $name")) {
                        throw "$style.$name failed for an unexpected reason. See $trxPath"
                    }
                }
            }
            # The assembly fixture writes only after asserting all native
            # failure contexts were disposed, including failed constructors/setup.
            if (-not (Test-Path -LiteralPath $cleanupFile) -or
                (Get-Content -LiteralPath $cleanupFile -Raw).Trim() -ne '10') {
                throw "xUnit native cleanup evidence is missing or incomplete. See $logPath"
            }
        }
        Write-Host "$project verified $($results.Count) runner outcomes."
    }

    # MSTest's original method attribute invokes the adapter proxy, then verifies
    # completion AFTER native setup/body/cleanup and asynchronous host disposal.
    $resultPath = Join-Path $resultRoot 'mstest-lifecycle'
    New-Item -ItemType Directory -Path $resultPath -Force | Out-Null
    $trxPath = Join-Path $resultPath 'lifecycle.trx'
    if (Test-Path -LiteralPath $trxPath) { Remove-Item -LiteralPath $trxPath }
    $logPath = Join-Path $resultPath 'runner.log'
    & dotnet test (Join-Path $repoRoot 'BDTest.MSTest.Tests/BDTest.MSTest.Tests.csproj') -c $Configuration --filter 'FullyQualifiedName~CompositionLifecycleTests|FullyQualifiedName~InheritanceLifecycleTests' --logger 'trx;LogFileName=lifecycle.trx' --results-directory $resultPath *> $logPath
    if ($LASTEXITCODE -ne 1) { throw "MSTest lifecycle must exit 1 for intentional failures. See $logPath" }
    [xml] $trx = Get-Content -LiteralPath $trxPath -Raw
    $results = @($trx.SelectNodes("//*[local-name()='UnitTestResult']"))
    if ($results.Count -ne 28) { throw "Expected 28 MSTest lifecycle probes. See $logPath" }
    foreach ($name in @('RowsAndRepeatsKeepIdentityAndOutput', 'SetupFailure', 'BodyFailure', 'TeardownFailure', 'ContextDisposalFailure', 'BodyAndDisposalFailure')) {
        $matching = @($results | Where-Object testName -Like "$name*")
        $expectedCount = if ($name -eq 'RowsAndRepeatsKeepIdentityAndOutput') { 8 } else { 4 }
        $expectedOutcome = if ($name -like '*Failure') { 'Failed' } else { 'Passed' }
        if ($matching.Count -ne $expectedCount -or @($matching | Where-Object outcome -ne $expectedOutcome).Count -ne 0) {
            throw "MSTest lifecycle $name expected $expectedCount $expectedOutcome results. See $trxPath"
        }
        if ($expectedOutcome -eq 'Failed') {
            $message = 'native ' + ($name -replace 'Failure$', '').ToLowerInvariant() + ' failure'
            if ($name -eq 'ContextDisposalFailure') { $message = 'native context disposal failure' }
            if ($name -eq 'BodyAndDisposalFailure') { $message = 'native body failure' }
            foreach ($result in $matching) {
                $errorMessage = $result.SelectSingleNode(".//*[local-name()='ErrorInfo']/*[local-name()='Message']")
                if (-not $errorMessage -or $errorMessage.InnerText -notmatch [regex]::Escape($message) -or $errorMessage.InnerText -match 'Assert\.|AssertFailedException|Native initialization did not run') {
                    throw "MSTest lifecycle $name failed unexpectedly or completion verification failed. See $trxPath"
                }
                if ($name -eq 'BodyAndDisposalFailure' -and $errorMessage.InnerText -notmatch 'native context disposal failure') {
                    throw "MSTest lifecycle lost the disposal failure after a body failure. See $trxPath"
                }
            }
        }
    }
    Write-Host 'MSTest verified 28 native lifecycle outcomes for composition and inheritance.'

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
    $env:BDTEST_CONFORMANCE_CLEANUP_FILE = $previousCleanupFile
}
Write-Host "Adapter conformance evidence: $resultRoot"
# GitHub Actions propagates LASTEXITCODE, including the intentional runner failures.
# Report success only after every exit code and TRX outcome has been verified.
exit 0
