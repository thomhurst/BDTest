# Assert-PrGreen.ps1
# Fail-closed merge gate for the issue-pr-loop skill.
#
# Default mode exits 0 ONLY when PR #<N> is provably safe to merge now:
#   - state OPEN, not a draft
#   - mergeable == MERGEABLE
#   - mergeStateStatus == CLEAN
#   - every status check is terminal AND passing (CheckRun: status=COMPLETED and
#     conclusion in SUCCESS/SKIPPED/NEUTRAL; StatusContext: state=SUCCESS)
#   - no current-head top-level review has actionable finding headings, unless
#     the trusted review workflow posts an exact-head CLEAR verdict
#   - no unresolved review threads
#
# Any other state — pending/queued/in-progress checks, an empty rollup, a failed
# check, an unresolved thread, or an API hiccup — exits 1 so the caller MUST NOT
# merge. The default on uncertainty is DENY.
#
# -ForAutoMerge mode is the gate for arming native auto-merge (Enable-AutoMerge.ps1).
# CI may still be running, because GitHub waits for required checks itself, but:
#   - state OPEN, not a draft, mergeable == MERGEABLE
#   - the base branch's active rules require the `CI Passed` check; without that rule
#     auto-merge would merge immediately
#   - no current check has failed (the newest execution of each check counts)
#   - Codex and Greptile have each reviewed the current head commit
#   - the review-body and unresolved-thread checks below pass
#
# Why this exists: the `main` ruleset enforces `CI Passed` and resolved conversations,
# but it cannot read review bodies or tell whether the reviewers saw this head. This
# script is the deterministic gate the prose rules alone cannot guarantee. The caller
# still reads every review body and comment itself; the script is necessary but not
# sufficient.
#
# Usage: pwsh scripts/Assert-PrGreen.ps1 -Pr 1234 [-Repo owner/name] [-ForAutoMerge] [-Json]
# Exit:  0 = safe to merge (or arm); 1 = DO NOT MERGE (reason printed to stderr).

[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$Pr,
    [string]$Repo,
    [switch]$Json,
    [switch]$ForAutoMerge
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AssertPrGreenReviewHeuristics.ps1')
. (Join-Path $PSScriptRoot 'MergeGateChecks.ps1')

function Get-GitHubPages([string]$Endpoint, [string]$What) {
    $raw = gh api $Endpoint --paginate --slurp 2>$null
    if ($LASTEXITCODE -ne 0) { Deny "could not fetch $What (exit $LASTEXITCODE)" }
    try { return @(($raw | ConvertFrom-Json) | ForEach-Object { $_ }) }
    catch { Deny "could not parse ${What}: $($_.Exception.Message)" }
}

function Deny([string]$reason) {
    [Console]::Error.WriteLine("DO NOT MERGE #${Pr} -- $reason")
    exit 1
}

$repoArgs = @()
if ($Repo) { $repoArgs = @('--repo', $Repo) }

# Re-fetch fresh — survey output goes stale within seconds.
$raw = gh pr view $Pr @repoArgs --json number,state,isDraft,mergeable,mergeStateStatus,statusCheckRollup,commits,headRefOid,headRefName,baseRefName 2>$null
if ($LASTEXITCODE -ne 0) { Deny "gh pr view failed (exit $LASTEXITCODE)" }
try {
    $view = $raw | ConvertFrom-Json
}
catch {
    Deny "could not parse PR state: $($_.Exception.Message)"
}

if ($view.state -ne 'OPEN') { Deny "state=$($view.state) (need OPEN)" }
if ($view.isDraft) { Deny 'PR is a draft; mark it ready only after its reviews approve the current head' }
if ($view.mergeable -ne 'MERGEABLE') { Deny "mergeable=$($view.mergeable) (need MERGEABLE)" }
$headSha = [string]$view.headRefOid
if ($headSha -notmatch '^[0-9a-f]{40}$') { Deny 'PR state omitted a valid headRefOid' }

if ($Repo) {
    $owner, $name = $Repo -split '/', 2
}
else {
    $nwo = gh repo view --json nameWithOwner --jq '.nameWithOwner' 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $nwo) { Deny "could not resolve repo for review checks" }
    $owner, $name = $nwo.Trim() -split '/', 2
}

$checks = @($view.statusCheckRollup | Where-Object { $null -ne $_ })
if ($ForAutoMerge) {
    $rules = Get-GitHubPages "repos/$owner/$name/rules/branches/$([uri]::EscapeDataString([string]$view.baseRefName))" 'base branch rules'
    if (-not (Test-RulesRequireCheck -Rules $rules -Context $AutoMergeRequiredCheck)) {
        Deny "base branch '$($view.baseRefName)' does not require the '$AutoMergeRequiredCheck' check, so auto-merge would merge without CI"
    }
    $problems = @(Get-AutoMergeCheckProblems $checks)
    if ($problems.Count -gt 0) { Deny ("checks failed: " + ($problems -join '; ')) }

    $comments = Get-GitHubPages "repos/$owner/$name/issues/$Pr/comments?per_page=100" 'issue comments'
    $prReviews = Get-GitHubPages "repos/$owner/$name/pulls/$Pr/reviews?per_page=100" 'reviews'
    $approvedRequests = @(foreach ($id in Get-CodexRequestIds -Comments $comments -HeadSha $headSha) {
            $reactions = Get-GitHubPages "repos/$owner/$name/issues/comments/$id/reactions?per_page=100" 'review request reactions'
            if (@($reactions | Where-Object { $_.content -eq '+1' -and $_.user.login -eq $CodexReviewer }).Count -gt 0) { $id }
        })
    if (-not (Test-CodexReviewedHead -Comments $comments -Reviews $prReviews -HeadSha $headSha -ApprovedRequestIds $approvedRequests)) {
        Deny "no Codex review of head $headSha; request one while the PR is a draft"
    }
    if (-not (Test-GreptileReviewedHead -Comments $comments -Reviews $prReviews -HeadSha $headSha)) {
        Deny "no Greptile review of head $headSha; request one while the PR is a draft"
    }
}
else {
    if ($view.mergeStateStatus -ne 'CLEAN') { Deny "mergeStateStatus=$($view.mergeStateStatus) (need CLEAN)" }
    if ($checks.Count -eq 0) {
        Deny "statusCheckRollup is empty -- no checks registered yet; refusing to merge blind"
    }
    $bad = @(Get-MergeCheckProblems $checks)
    if ($bad.Count -gt 0) {
        Deny ("checks not green: " + ($bad -join '; '))
    }
}

$headCommit = @($view.commits | Where-Object { $null -ne $_ }) | Select-Object -Last 1
$headCommittedAt = if ($headCommit) { ConvertTo-UtcDateTimeOffset $headCommit.committedDate } else { $null }

# gh pr view's latestReviews selection leaves commit.oid empty. Request it
# explicitly and paginate the connection before authenticating workflow verdicts.
$reviewQuery = 'query($owner:String!,$repo:String!,$pr:Int!,$endCursor:String){repository(owner:$owner,name:$repo){pullRequest(number:$pr){latestReviews(first:100,after:$endCursor){pageInfo{hasNextPage endCursor} nodes{id author{login} submittedAt body state commit{oid}}}}}}'
$reviewsRaw = gh api graphql --paginate --slurp -f "query=$reviewQuery" -F "owner=$owner" -F "repo=$name" -F "pr=$Pr" 2>$null
if ($LASTEXITCODE -ne 0) { Deny "could not fetch latest reviews (exit $LASTEXITCODE)" }
try {
    $reviewPages = @($reviewsRaw | ConvertFrom-Json)
    if ($reviewPages.Count -eq 0) { throw 'Latest-review response contained no pages.' }
    $latestReviews = @(
        foreach ($page in $reviewPages) {
            $connection = $page.data.repository.pullRequest.latestReviews
            if ($null -eq $connection) { throw 'Latest-review response did not contain the requested PR.' }
            $connection.nodes | Where-Object { $null -ne $_ }
        }
    )
}
catch {
    Deny "could not parse latest reviews: $($_.Exception.Message)"
}

# Top-level review comments are not review threads, so GitHub does not expose a
# resolved flag for them. Fail closed on common review-finding shapes instead
# of treating a COMMENTED review as automatically safe. A stale Claude review
# from before the current head commit is ignored only when the current-head
# claude-review check completed green.
$requestedChanges = @($latestReviews | Where-Object { $_.state -eq 'CHANGES_REQUESTED' })
if ($requestedChanges.Count -gt 0) {
    $authors = ($requestedChanges | ForEach-Object { $_.author.login }) -join ', '
    Deny "latest review requests changes from: $authors"
}

$actionableReviews = @()
$workflowRuns = $null
$reviewChecks = @($checks)
foreach ($review in $latestReviews) {
    if ($review.state -ne 'COMMENTED') { continue }
    if (Test-StaleBotReviewCanBeIgnored -Review $review -HeadCommitCommittedAt $headCommittedAt -Checks $checks) {
        continue
    }

    $body = [string]$review.body
    $reason = Get-ActionableReviewBodyReason -Body $body
    # Dispatch and pull_request_target checks can belong to the base ref.
    # Fetch its actual workflow jobs before deciding whether its verdict is trusted.
    if ($reason -and $review.author.login -match '^github-actions(?:\[bot\])?$' -and
        [string]$review.commit.oid -eq [string]$view.headRefOid -and
        -not (Test-TrustedBotClearVerdict -Review $review -HeadSha $view.headRefOid -Checks $reviewChecks)) {
        try {
            if ($null -eq $workflowRuns) {
                $workflowRuns = @(
                    foreach ($workflowEvent in @('workflow_dispatch', 'pull_request_target')) {
                        $runsRaw = gh api --paginate --slurp "repos/$owner/$name/actions/workflows/claude-code-review.yml/runs?event=$workflowEvent&status=success&per_page=100" 2>$null
                        if ($LASTEXITCODE -ne 0) { throw "Could not fetch $workflowEvent runs (exit $LASTEXITCODE)." }
                        ($runsRaw | ConvertFrom-Json).workflow_runs
                    }
                )
            }
            $submittedAt = ConvertTo-UtcDateTimeOffset $review.submittedAt
            foreach ($run in $workflowRuns) {
                $createdAt = ConvertTo-UtcDateTimeOffset $run.created_at
                $updatedAt = ConvertTo-UtcDateTimeOffset $run.updated_at
                if ($null -eq $submittedAt -or $null -eq $createdAt -or $null -eq $updatedAt -or
                    $createdAt -gt $submittedAt -or $updatedAt -lt $submittedAt) { continue }
                $jobsRaw = gh api --paginate --slurp "repos/$owner/$name/actions/runs/$($run.id)/jobs?per_page=100" 2>$null
                if ($LASTEXITCODE -ne 0) { throw "Could not fetch review jobs (exit $LASTEXITCODE)." }
                $jobs = @(($jobsRaw | ConvertFrom-Json).jobs)
                $reviewChecks += @(ConvertTo-WorkflowReviewChecks -Run $run -Jobs $jobs)
            }
        }
        catch {
            Deny "could not verify workflow review: $($_.Exception.Message)"
        }
    }
    if ($reason -and -not (Test-TrustedBotClearVerdict `
            -Review $review `
            -HeadSha ([string]$view.headRefOid) `
            -Checks $reviewChecks)) {
        $actionableReviews += "$($review.author.login): $reason"
    }
}
if ($actionableReviews.Count -gt 0) {
    Deny ("latest top-level review comment has actionable finding(s): " + ($actionableReviews -join '; '))
}

# Unresolved review threads block merge even when a review's state is COMMENTED.
$query = 'query($owner:String!,$repo:String!,$pr:Int!,$after:String){repository(owner:$owner,name:$repo){pullRequest(number:$pr){reviewThreads(first:100,after:$after){pageInfo{hasNextPage endCursor} nodes{isResolved}}}}}'
$unresolved = 0
$after = $null
$seenCursors = @{}
do {
    $graphqlArgs = @(
        'api',
        'graphql',
        '-f', "query=$query",
        '-F', "owner=$owner",
        '-F', "repo=$name",
        '-F', "pr=$Pr"
    )
    if ($after) { $graphqlArgs += @('-f', "after=$after") }

    $threadsRaw = & gh @graphqlArgs 2>$null
    if ($LASTEXITCODE -ne 0) { Deny "could not fetch review threads (exit $LASTEXITCODE)" }
    try {
        $reviewThreads = ($threadsRaw | ConvertFrom-Json).data.repository.pullRequest.reviewThreads
    }
    catch {
        Deny "could not parse review threads: $($_.Exception.Message)"
    }
    if ($null -eq $reviewThreads) { Deny "review-thread response did not contain the requested PR" }

    $unresolved += @($reviewThreads.nodes | Where-Object { -not $_.isResolved }).Count
    if ($reviewThreads.pageInfo.hasNextPage) {
        $endCursor = [string]$reviewThreads.pageInfo.endCursor
        if (-not $endCursor) { Deny "review-thread response has another page but no cursor" }
        if ($seenCursors.ContainsKey($endCursor)) { Deny "review-thread pagination repeated cursor '$endCursor'" }
        $seenCursors[$endCursor] = $true
        $after = $endCursor
    }
    else {
        $after = $null
    }
}
while ($after)

if ($unresolved -gt 0) { Deny "$unresolved unresolved review thread(s)" }

if ($Json) {
    [pscustomobject]@{ headRefOid = $view.headRefOid; headRefName = $view.headRefName } | ConvertTo-Json -Compress
}
else {
    if ($ForAutoMerge) {
        Write-Host "OK #${Pr} -- MERGEABLE, no failed checks, Codex and Greptile reviewed $headSha, no unresolved threads. Safe to arm auto-merge."
    }
    else {
        Write-Host "OK #${Pr} -- MERGEABLE, CLEAN, $($checks.Count) check(s) green, no unresolved threads. Safe to merge."
    }
}
exit 0
