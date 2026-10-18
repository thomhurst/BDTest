$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'Merge-DependencyPr.ps1'
$repo = 'test/repo'
$headSha = 'a' * 40

function Reset-Fixture {
    $fixture = @{}
    $fixture.run = @{
        id = 42; event = 'pull_request'; status = 'completed'; conclusion = 'success'
        path = '.github/workflows/dotnet.yml'; head_sha = $headSha
        head_repository = @{ full_name = $repo }
    }
    $fixture.pull = @{
        number = 123; state = 'open'; draft = $false
        user = @{ login = 'dependabot[bot]' }; labels = @()
        head = @{ sha = $headSha; repo = @{ full_name = $repo } }
        base = @{ ref = 'main'; repo = @{ full_name = $repo } }
    }
    $fixture.associatedPulls = @(@{ number = 123 })
    $fixture.jobs = @(@{ name = 'modularpipeline'; status = 'completed'; conclusion = 'success' })
    $fixture.latestRunId = 42
    $fixture.gateExit = 0
    $fixture.gateHead = $headSha
    $fixture.apiFailure = $null
    $fixture.mergeExit = 0
    $fixture.merges = @()
    $fixture.gateCalls = 0
    $global:dependencyMergeFixture = $fixture
}

# All GitHub operations are mocked. A test must never contact GitHub or merge a real PR.
function gh {
    $fixture = $global:dependencyMergeFixture
    $global:LASTEXITCODE = 0
    if ($args[0] -eq 'pr' -and $args[1] -eq 'merge') {
        $fixture.merges += ,@($args)
        $global:LASTEXITCODE = $fixture.mergeExit
        return
    }
    if ($args[0] -ne 'api') { throw "Unexpected gh operation: $args" }
    $endpoint = [string]$args[1]
    if ($endpoint -eq $fixture.apiFailure) { $global:LASTEXITCODE = 1; return }
    $response = switch ($endpoint) {
        "repos/$repo/actions/runs/42" { $fixture.run; break }
        "repos/$repo/commits/$headSha/pulls?per_page=100" { ,@($fixture.associatedPulls); break }
        "repos/$repo/pulls/123" { $fixture.pull; break }
        "repos/$repo/pulls/124" { @{ number = 124; state = 'closed' }; break }
        "repos/$repo/actions/runs/42/jobs?per_page=100" { ,@(@{ jobs = $fixture.jobs }); break }
        "repos/$repo/actions/workflows/dotnet.yml/runs?event=pull_request&head_sha=$headSha&per_page=1" {
            @{ workflow_runs = @(@{ id = $fixture.latestRunId }) }; break
        }
        default { throw "Unexpected API endpoint: $endpoint" }
    }
    if ($endpoint -like '*/commits/*/pulls?*') {
        # Match gh api --paginate --slurp: an outer array containing response pages.
        if ($args -notcontains '--paginate' -or $args -notcontains '--slurp') { throw 'PR lookup must paginate.' }
        $pages = @($fixture.associatedPulls | ForEach-Object { ,@($_) })
        ConvertTo-Json -InputObject $pages -Depth 20 -Compress
    }
    else {
        ConvertTo-Json -InputObject $response -Depth 20 -Compress
    }
}

function pwsh {
    $fixture = $global:dependencyMergeFixture
    $fixture.gateCalls++
    if ($args -notcontains '-Json' -or $args -notcontains '-Repo' -or $args -notcontains $repo) {
        throw 'The existing merge gate must return its validated head for the explicit repository.'
    }
    $global:LASTEXITCODE = $fixture.gateExit
    @{ headRefOid = $fixture.gateHead; headRefName = 'dependency-update' } | ConvertTo-Json -Compress
}

function Test-Case([string]$Name, [scriptblock]$Arrange, [bool]$ShouldMerge = $false, [bool]$ShouldThrow = $false) {
    Reset-Fixture
    $fixture = $global:dependencyMergeFixture
    & $Arrange
    $caught = $false
    try { & $scriptPath -RunId 42 -Repo $repo | Out-Null }
    catch {
        $caught = $true
        if (-not $ShouldThrow) { throw }
    }
    if ($caught -ne $ShouldThrow) { throw "${Name}: unexpected exception result ($caught)." }
    $expected = if ($ShouldMerge) { 1 } else { 0 }
    if ($fixture.merges.Count -ne $expected) { throw "${Name}: expected $expected merges; got $($fixture.merges.Count)." }
    if ($ShouldMerge) {
        $merge = $fixture.merges[0]
        $shaIndex = [Array]::IndexOf($merge, '--match-head-commit')
        if ($shaIndex -lt 0 -or $merge[$shaIndex + 1] -ne $headSha -or
            $merge -contains '--auto' -or $merge -contains '--admin' -or $fixture.gateCalls -ne 1) {
            throw "${Name}: merge must use the gated SHA without bypass or deferred auto-merge."
        }
    }
    Write-Output "PASS $Name"
}

Test-Case 'Successful current-head CI merges Dependabot PR' {} -ShouldMerge $true
Test-Case 'Dependency label supports Renovate and other authors' {
    $fixture.pull.user.login = 'renovate[bot]'; $fixture.pull.labels = @(@{ name = 'dependencies' })
} -ShouldMerge $true
Test-Case 'Associated PRs across multiple pages are all considered' {
    $fixture.associatedPulls = @(@{ number = 124 }, @{ number = 123 })
} -ShouldMerge $true
Test-Case 'Failed CI never reaches the merge gate' { $fixture.run.conclusion = 'failure' }
Test-Case 'Cancelled CI does not merge' { $fixture.run.conclusion = 'cancelled' }
Test-Case 'Pending CI does not merge' { $fixture.run.status = 'in_progress' }
Test-Case 'Push runs do not merge PRs' { $fixture.run.event = 'push' }
Test-Case 'An unrelated workflow cannot authorize a merge' { $fixture.run.path = '.github/workflows/other.yml' }
Test-Case 'Missing pipeline job does not merge' { $fixture.jobs = @() }
Test-Case 'Skipped pipeline job does not merge' { $fixture.jobs[0].conclusion = 'skipped' }
Test-Case 'Older successful run cannot override a newer run' { $fixture.latestRunId = 43 }
Test-Case 'A force-pushed PR cannot use stale successful CI' { $fixture.pull.head.sha = 'b' * 40 }
Test-Case 'A changed head returned by the gate does not merge' { $fixture.gateHead = 'b' * 40 }
Test-Case 'Failed, pending, missing checks or reviews denied by the existing gate do not merge' { $fixture.gateExit = 1 }
Test-Case 'Ordinary PRs do not merge' { $fixture.pull.user.login = 'contributor' }
Test-Case 'Closed PRs do not merge' { $fixture.pull.state = 'closed' }
Test-Case 'Draft PRs do not merge' { $fixture.pull.draft = $true }
Test-Case 'PRs targeting another branch do not merge' { $fixture.pull.base.ref = 'release' }
Test-Case 'Foreign base repositories do not merge' { $fixture.pull.base.repo.full_name = 'other/repo' }
Test-Case 'Mismatched source repositories do not merge' { $fixture.pull.head.repo.full_name = 'other/repo' }
Test-Case 'Missing associated PRs do not merge' { $fixture.associatedPulls = @() }
Test-Case 'API failure fails closed' { $fixture.apiFailure = "repos/$repo/actions/runs/42" } -ShouldThrow $true
Test-Case 'GitHub rejection of a changed head is surfaced' { $fixture.mergeExit = 1 } -ShouldMerge $true -ShouldThrow $true
Test-Case 'Missing gate head does not merge' { $fixture.gateHead = $null }
Test-Case 'Invalid CI head fails closed' { $fixture.run.head_sha = 'invalid' } -ShouldThrow $true
foreach ($endpoint in @(
    "repos/$repo/actions/workflows/dotnet.yml/runs?event=pull_request&head_sha=$headSha&per_page=1",
    "repos/$repo/actions/runs/42/jobs?per_page=100",
    "repos/$repo/commits/$headSha/pulls?per_page=100",
    "repos/$repo/pulls/123"
)) {
    Test-Case "API failure fails closed: $endpoint" { $fixture.apiFailure = $endpoint } -ShouldThrow $true
}
Remove-Variable dependencyMergeFixture -Scope Global
