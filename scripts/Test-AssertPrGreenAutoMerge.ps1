# Exercise Assert-PrGreen.ps1 (both modes) and Enable-AutoMerge.ps1 against mocked GitHub
# responses. A test must never contact GitHub or arm a real PR.
$ErrorActionPreference = 'Stop'
$gatePath = Join-Path $PSScriptRoot 'Assert-PrGreen.ps1'
$armPath = Join-Path $PSScriptRoot 'Enable-AutoMerge.ps1'
$repo = 'test/repo'
$head = 'a' * 40
$oldHead = 'b' * 40

function New-CodexSummary([string]$Sha, [string]$Status = 'completed') {
    $state = @{ code_review = @{ commit_sha = $Sha; has_any_findings = $false; has_unresolved_findings = $false; status = $Status } } |
        ConvertTo-Json -Compress -Depth 5
    @{ id = 10; user = @{ login = 'chatgpt-codex-connector[bot]' }
        body = "<!-- codex-pull-request-review-summary -->`n<!-- codex-review-state:v1 $state -->`n## Codex Review Summary" }
}

function New-GreptileSummary([string]$Sha) {
    @{ id = 11; user = @{ login = 'greptile-apps[bot]' }
        body = "<!-- greptile_summary -->`n<sub>Reviews (1) · Last reviewed commit: [`"fix: thing`"](https://github.com/test/repo/commit/$Sha) · [Reviewed by Greptile](https://www.greptile.com/)</sub>" }
}

function Reset-Fixture {
    $global:gateFixture = @{
        view = @{
            number = 1; state = 'OPEN'; isDraft = $false; mergeable = 'MERGEABLE'; mergeStateStatus = 'BLOCKED'
            headRefOid = $head; headRefName = 'issue-1-thing'; baseRefName = 'main'
            commits = @(@{ committedDate = '2026-10-28T10:00:00Z' })
            statusCheckRollup = @(
                # The skipped draft run reports the aggregator under its raw expression text.
                @{ name = "needs.modularpipeline.result != 'skipped' && 'CI Passed' || 'CI Passed (skipped)'"; workflowName = '.NET'
                    status = 'COMPLETED'; conclusion = 'SKIPPED'; startedAt = '2026-10-28T10:01:00Z' },
                @{ name = 'modularpipeline'; workflowName = '.NET'; status = 'IN_PROGRESS'; conclusion = ''; startedAt = '2026-10-28T10:05:00Z' }
            )
        }
        rules = @(@{ type = 'pull_request' }, @{ type = 'required_status_checks'; parameters = @{ required_status_checks = @(@{ context = 'CI Passed'; integration_id = 15368 }) } })
        comments = @((New-CodexSummary $head), (New-GreptileSummary $head))
        reviews = @()
        reactions = @{}
        threads = @()
        merges = @()
        gateExit = 0
        gateHead = $head
    }
}

function ConvertTo-Pages($Items) { ConvertTo-Json -InputObject @(, @($Items)) -Depth 20 -Compress }

function gh {
    $fx = $global:gateFixture
    $global:LASTEXITCODE = 0
    if ($args[0] -eq 'pr' -and $args[1] -eq 'view') {
        if ($args -contains '--jq') { return $fx.view.headRefOid }
        return ($fx.view | ConvertTo-Json -Depth 20 -Compress)
    }
    if ($args[0] -eq 'pr' -and $args[1] -eq 'merge') { $fx.merges += , @($args); return }
    if ($args[0] -ne 'api') { throw "Unexpected gh operation: $args" }
    $endpoint = [string]$args[1]
    if ($endpoint -eq 'graphql') {
        $query = [string]($args | Where-Object { "$_" -like 'query=*' } | Select-Object -First 1)
        if ($query -like '*latestReviews*') {
            return '[{"data":{"repository":{"pullRequest":{"latestReviews":{"pageInfo":{"hasNextPage":false},"nodes":[]}}}}}]'
        }
        if ($query -like '*reviewThreads*') {
            $nodes = @($fx.threads | ForEach-Object { @{ isResolved = $_ } })
            return (@{ data = @{ repository = @{ pullRequest = @{ reviewThreads = @{ pageInfo = @{ hasNextPage = $false }; nodes = $nodes } } } } } |
                    ConvertTo-Json -Depth 20 -Compress)
        }
        throw "Unexpected GraphQL query: $query"
    }
    if ($args -notcontains '--paginate' -or $args -notcontains '--slurp') { throw "REST lookups must paginate: $endpoint" }
    switch -Regex ($endpoint) {
        '^repos/test/repo/rules/branches/main$' { return ConvertTo-Pages $fx.rules }
        '^repos/test/repo/issues/1/comments\?' { return ConvertTo-Pages $fx.comments }
        '^repos/test/repo/pulls/1/reviews\?' { return ConvertTo-Pages $fx.reviews }
        '^repos/test/repo/issues/comments/(\d+)/reactions\?' { return ConvertTo-Pages @($fx.reactions[[int]$Matches[1]]) }
    }
    throw "Unexpected API endpoint: $endpoint"
}

function Invoke-Gate([switch]$ForAutoMerge) {
    $gateArgs = @{ Pr = 1; Repo = $repo; Json = $true }
    if ($ForAutoMerge) { $gateArgs.ForAutoMerge = $true }
    $output = & $gatePath @gateArgs 2>&1
    [pscustomobject]@{ Exit = $LASTEXITCODE; Output = ($output | Out-String) }
}

function Test-Gate([string]$Name, [scriptblock]$Arrange, [bool]$Allows, [switch]$Default) {
    Reset-Fixture
    $fx = $global:gateFixture
    & $Arrange
    $result = Invoke-Gate -ForAutoMerge:(-not $Default)
    if (($result.Exit -eq 0) -ne $Allows) { throw "${Name}: expected allow=$Allows, got exit $($result.Exit): $($result.Output)" }
    if ($Allows -and ($result.Output | ConvertFrom-Json).headRefOid -ne $head) { throw "${Name}: gate must return the gated head." }
    Write-Output "PASS $Name"
}

# -ForAutoMerge
Test-Gate 'Arms a ready PR with pending CI and current-head reviews' {} $true
Test-Gate 'Never arms a draft' { $fx.view.isDraft = $true } $false
Test-Gate 'Never arms without a base-branch rule requiring CI Passed' { $fx.rules = @(@{ type = 'pull_request' }) } $false
Test-Gate 'Never arms when the required check has another name' {
    $fx.rules[1].parameters.required_status_checks[0].context = 'modularpipeline'
} $false
Test-Gate 'Never arms with a conflict' { $fx.view.mergeable = 'CONFLICTING' } $false
Test-Gate 'Never arms with a failed check' {
    $fx.view.statusCheckRollup += @{ name = 'filesystem-storage-macos'; workflowName = '.NET'; status = 'COMPLETED'; conclusion = 'FAILURE'; startedAt = '2026-10-28T10:05:00Z' }
} $false
Test-Gate 'Ignores a cancelled execution superseded by a newer one' {
    $fx.view.statusCheckRollup += @{ name = 'filesystem-storage-macos'; workflowName = '.NET'; status = 'COMPLETED'; conclusion = 'CANCELLED'; startedAt = '2026-10-28T10:02:00Z' }
    $fx.view.statusCheckRollup += @{ name = 'filesystem-storage-macos'; workflowName = '.NET'; status = 'QUEUED'; conclusion = ''; startedAt = $null }
} $true
Test-Gate 'Never arms when the newest execution was cancelled' {
    $fx.view.statusCheckRollup += @{ name = 'filesystem-storage-macos'; workflowName = '.NET'; status = 'COMPLETED'; conclusion = 'CANCELLED'; startedAt = '2026-10-28T10:06:00Z' }
} $false
Test-Gate 'Never arms with a failed commit status' {
    $fx.view.statusCheckRollup += @{ context = 'external'; state = 'ERROR'; startedAt = '2026-10-28T10:06:00Z' }
} $false
Test-Gate 'Never arms when Codex reviewed only an older head' { $fx.comments = @((New-CodexSummary $oldHead), (New-GreptileSummary $head)) } $false
Test-Gate 'Never arms while the Codex review is still running' { $fx.comments = @((New-CodexSummary $head 'in_progress'), (New-GreptileSummary $head)) } $false
Test-Gate 'Accepts a Codex receipt naming the head' {
    $fx.comments = @((New-CodexSummary $oldHead), (New-GreptileSummary $head),
        @{ id = 12; user = @{ login = 'chatgpt-codex-connector[bot]' }; body = "<!-- codex-review-receipt:v1 -->`n`nCodex Review: Didn't find any major issues.`n`n**Reviewed commit:** ``$($head.Substring(0, 10))``" })
} $true
Test-Gate 'Accepts a Codex review filed on the head' {
    $fx.comments = @((New-GreptileSummary $head))
    $fx.reviews = @(@{ user = @{ login = 'chatgpt-codex-connector[bot]' }; commit_id = $head; state = 'COMMENTED' })
} $true
Test-Gate 'Accepts a Codex thumbs-up on this head''s request' {
    $fx.comments = @((New-GreptileSummary $head), @{ id = 13; user = @{ login = 'author' }; body = "<!-- issue-pr-loop -->`n<!-- codex-review:$head -->`n@codex review" })
    $fx.reactions[13] = @(@{ content = '+1'; user = @{ login = 'chatgpt-codex-connector[bot]' } })
} $true
Test-Gate 'Rejects a thumbs-up from anyone but Codex' {
    $fx.comments = @((New-GreptileSummary $head), @{ id = 13; user = @{ login = 'author' }; body = "<!-- issue-pr-loop -->`n<!-- codex-review:$head -->`n@codex review" })
    $fx.reactions[13] = @(@{ content = '+1'; user = @{ login = 'author' } }, @{ content = 'eyes'; user = @{ login = 'chatgpt-codex-connector[bot]' } })
} $false
Test-Gate 'Never arms when Greptile reviewed only an older head' { $fx.comments = @((New-CodexSummary $head), (New-GreptileSummary $oldHead)) } $false
Test-Gate 'Never arms with an unresolved review thread' { $fx.threads = @($true, $false) } $false

# Default (immediate merge) mode keeps its strict checks.
Test-Gate 'Immediate merge refuses a blocked PR' {} $false -Default
Test-Gate 'Immediate merge refuses pending checks' { $fx.view.mergeStateStatus = 'CLEAN' } $false -Default
Test-Gate 'Immediate merge refuses a draft' {
    $fx.view.mergeStateStatus = 'CLEAN'; $fx.view.isDraft = $true
    $fx.view.statusCheckRollup = @(@{ name = 'CI Passed'; workflowName = '.NET'; status = 'COMPLETED'; conclusion = 'SUCCESS' })
} $false -Default
Test-Gate 'Immediate merge allows a clean green PR' {
    $fx.view.mergeStateStatus = 'CLEAN'
    $fx.view.statusCheckRollup = @(@{ name = 'CI Passed'; workflowName = '.NET'; status = 'COMPLETED'; conclusion = 'SUCCESS' })
} $true -Default

# Enable-AutoMerge.ps1 composes the gate; the gate itself is mocked here.
function pwsh {
    $fx = $global:gateFixture
    if ($args -notcontains '-ForAutoMerge' -or $args -notcontains '-Json' -or $args -notcontains $repo) {
        throw 'Enable-AutoMerge must run the auto-merge gate for the explicit repository.'
    }
    $global:LASTEXITCODE = $fx.gateExit
    @{ headRefOid = $fx.gateHead; headRefName = 'issue-1-thing' } | ConvertTo-Json -Compress
}

function Test-Arm([string]$Name, [scriptblock]$Arrange, [bool]$Arms) {
    Reset-Fixture
    $fx = $global:gateFixture
    & $Arrange
    & $armPath -Pr 1 -Repo $repo 2>&1 | Out-Null
    $armed = $LASTEXITCODE -eq 0
    if ($armed -ne $Arms -or $fx.merges.Count -ne [int]$Arms) { throw "${Name}: expected armed=$Arms, got exit $LASTEXITCODE and $($fx.merges.Count) merge call(s)." }
    if ($Arms) {
        $merge = $fx.merges[0]
        $shaIndex = [Array]::IndexOf($merge, '--match-head-commit')
        if ($merge -notcontains '--auto' -or $merge -notcontains '--squash' -or $merge -contains '--admin' -or
            $shaIndex -lt 0 -or $merge[$shaIndex + 1] -ne $head) {
            throw "${Name}: auto-merge must be armed as a squash pinned to the gated head."
        }
    }
    Write-Output "PASS $Name"
}

Test-Arm 'Arms auto-merge pinned to the gated head' {} $true
Test-Arm 'Does not arm when the gate denies' { $fx.gateExit = 1 } $false
Test-Arm 'Does not arm when the head moved during the gate' { $fx.gateHead = $oldHead } $false

Write-Output 'Assert-PrGreen auto-merge gate and Enable-AutoMerge tests passed.'
# The denial cases leave LASTEXITCODE at 1, and the Actions pwsh shell exits with it.
exit 0
