# Merge dependency PRs only after their current head passes the .NET workflow and
# the same fail-closed checks/review gate used by the issue-pr-loop.
[CmdletBinding()]
param(
    [long]$RunId,
    [Parameter(Mandatory)][string]$Repo
)

$ErrorActionPreference = 'Stop'

function Get-GitHubJson([string]$Endpoint, [switch]$Paginate) {
    $pagination = if ($Paginate) { @('--paginate', '--slurp') } else { @() }
    $raw = gh api $Endpoint @pagination
    if ($LASTEXITCODE -ne 0) { throw "GitHub API request failed: $Endpoint" }
    if (-not $raw) { throw "GitHub API returned no data: $Endpoint" }
    $raw | ConvertFrom-Json
}

# Retry pending checks and reviews without rerunning CI. All state is revalidated
# by the same run-specific path, using only code from the trusted workflow commit.
if (-not $RunId) {
    $openPages = @(Get-GitHubJson "repos/$Repo/pulls?state=open&base=main&per_page=100" -Paginate)
    foreach ($pull in ($openPages | ForEach-Object { $_ })) {
        if ($pull.user.login -ne 'dependabot[bot]' -and 'dependencies' -notin $pull.labels.name) { continue }
        $pullHead = [string]$pull.head.sha
        if ($pullHead -notmatch '^[0-9a-f]{40}$') { throw "#$($pull.number) has no valid head SHA." }
        $latest = Get-GitHubJson "repos/$Repo/actions/workflows/dotnet.yml/runs?event=pull_request&head_sha=$pullHead&per_page=1"
        if (@($latest.workflow_runs).Count -eq 1 -and $latest.workflow_runs[0].id) {
            & $PSCommandPath -RunId $latest.workflow_runs[0].id -Repo $Repo
        }
    }
    return
}

$run = Get-GitHubJson "repos/$Repo/actions/runs/$RunId"
$workflowPath = ([string]$run.path -split '@', 2)[0]
if ($run.event -ne 'pull_request' -or $workflowPath -ne '.github/workflows/dotnet.yml' -or
    $run.status -ne 'completed' -or $run.conclusion -ne 'success') {
    Write-Output "Run $RunId is not a successful .NET PR run; skipping."
    return
}
$headSha = [string]$run.head_sha
if ($headSha -notmatch '^[0-9a-f]{40}$') { throw 'CI run has no valid head SHA.' }

# A delayed event from an older success must not override a newer failed/pending run.
$latest = Get-GitHubJson "repos/$Repo/actions/workflows/dotnet.yml/runs?event=pull_request&head_sha=$headSha&per_page=1"
if (@($latest.workflow_runs).Count -ne 1 -or $latest.workflow_runs[0].id -ne $RunId) {
    Write-Output "Run $RunId is no longer the latest .NET run for this head; skipping."
    return
}
$jobPages = @(Get-GitHubJson "repos/$Repo/actions/runs/$RunId/jobs?per_page=100" -Paginate)
$pipeline = @($jobPages.jobs | Where-Object { $_.name -eq 'modularpipeline' })
if ($pipeline.Count -ne 1 -or $pipeline[0].status -ne 'completed' -or $pipeline[0].conclusion -ne 'success') {
    Write-Output 'The required modularpipeline job did not succeed; skipping.'
    return
}

# workflow_run.pull_requests can be empty (including on Dependabot runs).
# Resolve PRs through the tested commit, then fetch their current state.
$pullPages = @(Get-GitHubJson "repos/$Repo/commits/$headSha/pulls?per_page=100" -Paginate)
foreach ($associatedPull in ($pullPages | ForEach-Object { $_ })) {
    $pr = Get-GitHubJson "repos/$Repo/pulls/$($associatedPull.number)"
    if ($pr.state -ne 'open' -or $pr.draft -or $pr.base.ref -ne 'main' -or
        $pr.base.repo.full_name -ne $Repo -or $pr.head.sha -ne $headSha -or
        -not $pr.head.repo.full_name -or $pr.head.repo.full_name -ne $run.head_repository.full_name -or
        ($pr.user.login -ne 'dependabot[bot]' -and 'dependencies' -notin $pr.labels.name)) {
        continue
    }

    $gateOutput = & pwsh (Join-Path $PSScriptRoot 'Assert-PrGreen.ps1') -Pr $pr.number -Repo $Repo -Json
    if ($LASTEXITCODE -ne 0) {
        Write-Output "Merge gate denied #$($pr.number); leaving it open."
        $global:LASTEXITCODE = 0
        continue
    }
    $validatedHead = $gateOutput | ConvertFrom-Json
    if ($validatedHead.headRefOid -ne $headSha) {
        Write-Output "#$($pr.number) changed after CI; leaving it open."
        continue
    }

    # GitHub checks this SHA atomically, closing the race with a new dependency push.
    gh pr merge $pr.number --repo $Repo --merge --match-head-commit $headSha
    if ($LASTEXITCODE -ne 0) { throw "GitHub rejected merge of #$($pr.number)." }
}
