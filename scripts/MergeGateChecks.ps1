# Pure check and verdict helpers for Assert-PrGreen.ps1. They make no GitHub calls, so
# Test-AssertPrGreenAutoMerge.ps1 can drive them with fixtures.

# The `main` ruleset's only required status check: the aggregator job in dotnet.yml.
$AutoMergeRequiredCheck = 'CI Passed'
$CodexReviewer = 'chatgpt-codex-connector[bot]'
$GreptileReviewer = 'greptile-apps[bot]'

# True when the active rules for the base branch require $Context as a status check.
# Native auto-merge waits only for required checks, so without this rule arming merges at once.
function Test-RulesRequireCheck {
    param([AllowNull()]$Rules, [Parameter(Mandatory)][string]$Context)
    foreach ($rule in @($Rules | Where-Object { $null -ne $_ })) {
        if ($rule.type -ne 'required_status_checks') { continue }
        if (@($rule.parameters.required_status_checks | Where-Object { $_.context -eq $Context }).Count -gt 0) { return $true }
    }
    return $false
}

# Problems that forbid an immediate merge: every check must be terminal and passing
# (CheckRun: COMPLETED with SUCCESS/SKIPPED/NEUTRAL; StatusContext: SUCCESS).
function Get-MergeCheckProblems {
    param([AllowNull()]$Checks)
    $allowedConclusions = @('SUCCESS', 'SKIPPED', 'NEUTRAL')
    foreach ($c in @($Checks | Where-Object { $null -ne $_ })) {
        if ($null -ne $c.status) {
            if ($c.status -ne 'COMPLETED') { "$($c.name): status=$($c.status)"; continue }
            if ($allowedConclusions -notcontains $c.conclusion) { "$($c.name): conclusion=$($c.conclusion)" }
        }
        elseif ($null -ne $c.state) {
            if ($c.state -ne 'SUCCESS') { "$($c.context): state=$($c.state)" }
        }
        else {
            "unrecognized check entry: $($c | ConvertTo-Json -Compress)"
        }
    }
}

function Get-CheckStartKey($Check) {
    # A queued execution has no start time yet; it is the newest one.
    $started = ConvertTo-UtcDateTimeOffset $Check.startedAt
    if ($null -eq $started -or $started.Year -lt 2000) { return [DateTimeOffset]::MaxValue }
    return $started
}

# statusCheckRollup keeps superseded executions of one check on the same head, for example a
# run cancelled by conversion to draft and the fresh run after the PR is ready again.
# Keep only the newest execution of each check.
function Select-CurrentChecks {
    param([AllowNull()]$Checks)
    $groups = @($Checks | Where-Object { $null -ne $_ }) | Group-Object {
        if ($null -ne $_.status) { "run|$($_.workflowName)|$($_.name)" } else { "status|$($_.context)" }
    }
    foreach ($group in $groups) {
        $group.Group | Sort-Object { Get-CheckStartKey $_ } | Select-Object -Last 1
    }
}

# Problems that forbid arming auto-merge. Pending checks are fine (GitHub waits for the
# required ones); failed, cancelled, or unrecognized checks are not.
function Get-AutoMergeCheckProblems {
    param([AllowNull()]$Checks)
    $allowedConclusions = @('SUCCESS', 'SKIPPED', 'NEUTRAL')
    foreach ($c in @(Select-CurrentChecks $Checks)) {
        if ($null -ne $c.status) {
            if ($c.status -ne 'COMPLETED') { continue }
            if ($allowedConclusions -notcontains $c.conclusion) { "$($c.name): conclusion=$($c.conclusion)" }
        }
        elseif ($null -ne $c.state) {
            if ($c.state -in 'SUCCESS', 'PENDING', 'EXPECTED') { continue }
            "$($c.context): state=$($c.state)"
        }
        else {
            "unrecognized check entry: $($c | ConvertTo-Json -Compress)"
        }
    }
}

function Test-ShaNamesHead([string]$Candidate, [Parameter(Mandatory)][string]$HeadSha) {
    $Candidate -match '^[0-9a-f]{7,40}$' -and $HeadSha.StartsWith($Candidate, [StringComparison]::OrdinalIgnoreCase)
}

# Codex has reviewed the head when it filed a review on it, its summary's state payload
# records a completed code review of it, its receipt names it, or it reacted with a
# thumbs-up to a review request for it ($ApprovedRequestIds, verified by the caller).
function Test-CodexReviewedHead {
    param([AllowNull()]$Comments, [AllowNull()]$Reviews, [Parameter(Mandatory)][string]$HeadSha, [AllowNull()]$ApprovedRequestIds)
    if (@($Reviews | Where-Object { $_.user.login -eq $CodexReviewer -and $_.commit_id -eq $HeadSha }).Count -gt 0) { return $true }
    if (@($ApprovedRequestIds | Where-Object { $null -ne $_ }).Count -gt 0) { return $true }
    foreach ($comment in @($Comments | Where-Object { $_.user.login -eq $CodexReviewer })) {
        $body = [string]$comment.body
        $state = [regex]::Match($body, '<!-- codex-review-state:v1 (\{.*?\}) -->')
        if ($state.Success) {
            try { $codeReview = ($state.Groups[1].Value | ConvertFrom-Json).code_review } catch { $codeReview = $null }
            if ($codeReview -and $codeReview.commit_sha -eq $HeadSha -and $codeReview.status -eq 'completed') { return $true }
        }
        if ($body.Contains('<!-- codex-review-receipt:v1 -->')) {
            $receipt = [regex]::Match($body, '\*\*Reviewed commit:\*\*\s*`([0-9a-f]{7,40})`')
            if ($receipt.Success -and (Test-ShaNamesHead $receipt.Groups[1].Value $HeadSha)) { return $true }
        }
    }
    return $false
}

# Greptile has reviewed the head when it filed a review on it or its summary's
# "Last reviewed commit" link names it.
function Test-GreptileReviewedHead {
    param([AllowNull()]$Comments, [AllowNull()]$Reviews, [Parameter(Mandatory)][string]$HeadSha)
    if (@($Reviews | Where-Object { $_.user.login -eq $GreptileReviewer -and $_.commit_id -eq $HeadSha }).Count -gt 0) { return $true }
    foreach ($comment in @($Comments | Where-Object { $_.user.login -eq $GreptileReviewer })) {
        $body = [string]$comment.body
        if (-not $body.Contains('<!-- greptile_summary -->')) { continue }
        $link = [regex]::Match($body, 'Last reviewed commit:.*?/commit/([0-9a-f]{40})\b')
        if ($link.Success -and $link.Groups[1].Value -eq $HeadSha) { return $true }
    }
    return $false
}

# Review requests for this head: loop comments carrying `<!-- codex-review:<head> -->`.
function Get-CodexRequestIds {
    param([AllowNull()]$Comments, [Parameter(Mandatory)][string]$HeadSha)
    @($Comments | Where-Object { ([string]$_.body).Contains("<!-- codex-review:$HeadSha -->") } | ForEach-Object { $_.id })
}
