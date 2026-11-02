#!/usr/bin/env pwsh
# Exercise the loop survey and deferral fingerprints for draft PRs without GitHub, Redis, or an agent process.
$ErrorActionPreference = 'Stop'

$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot 'Start-IssuePrLoop.ps1'), [ref]$null, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors -join "`n") }
foreach ($name in 'Get-Hash', 'Get-CheckState', 'Get-LatestOutsideComment', 'Get-PrFingerprint',
    'Reset-SurveyStats', 'New-Candidate', 'Get-Candidates', 'Get-IssueFingerprint', 'Test-HasLabel', 'Test-HasOpenPr',
    'ConvertTo-Instant', 'Test-HasNewFeedback') {
    $definition = $ast.Find({ param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $false)
    if (-not $definition) { throw "Missing function $name" }
    . ([scriptblock]::Create($definition.Extent.Text))
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if (($Actual | ConvertTo-Json -Compress -Depth 10) -ne ($Expected | ConvertTo-Json -Compress -Depth 10)) {
        throw "$Message. Expected '$Expected', got '$Actual'."
    }
}

# Isolate the PR survey: no red main, issues, or stranded branches.
function Get-MainCandidate { }
function Get-OpenIssues { }
function Get-StrandedBranches { @{} }
$script:activity = @{}
function Get-PrActivity { $script:activity }
function gh { $global:LASTEXITCODE = 0; ConvertTo-Json -InputObject $script:prs -Depth 20 }

$repoSlug = 'test/repo'
$prFields = ''
$excludedPrLabels = @('needs-human', 'on-hold')
$loopMarker = '<!-- issue-pr-loop -->'
$surveyStats = [ordered]@{}
$script:promoted = [Collections.Generic.HashSet[int]]::new()
$draft = [pscustomobject]@{
    number = 1; title = 'Draft awaiting review'; isDraft = $true
    headRefOid = 'head-sha'; mergeable = 'MERGEABLE'; mergeStateStatus = 'DRAFT'
    author = @{ login = 'author' }; labels = @(); reviews = @(); comments = @()
    statusCheckRollup = @(@{ name = 'modularpipeline'; conclusion = 'SKIPPED' })
}
$ready = $draft.PSObject.Copy()
$ready.number = 2
$ready.isDraft = $false
$ready.mergeStateStatus = 'CLEAN'
$ready.statusCheckRollup = @(@{ name = 'modularpipeline'; conclusion = 'SUCCESS' })
$script:prs = @($draft, $ready)

Reset-SurveyStats
$candidates = @(Get-Candidates)
Assert-Equal @($candidates.Number | Sort-Object) @(1, 2) 'Draft and ready PRs must both enter the queue'
Assert-Equal $surveyStats['pr-draft'] 1 'Draft count must describe open drafts'
Assert-Equal ($candidates | Where-Object Number -eq 1).Light $false 'A draft requires review processing, not a light merge'
Assert-Equal ($candidates | Where-Object Number -eq 2).Light $true 'A clean ready PR keeps the light merge path'

$draft.mergeStateStatus = 'CLEAN'
Reset-SurveyStats
Assert-Equal ((Get-Candidates) | Where-Object Number -eq 1).Light $false 'A clean draft still requires promotion first'

$draft.statusCheckRollup = @(@{ name = 'modularpipeline'; status = 'IN_PROGRESS' })
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number) @(2) 'A draft waiting only for checks must yield to other work'
$draft.mergeable = 'CONFLICTING'
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number | Sort-Object) @(1, 2) 'Conflicting drafts remain actionable during pending checks'
$draft.mergeable = 'MERGEABLE'
$draft.statusCheckRollup = @(@{ name = 'Analyze (csharp)'; conclusion = 'FAILURE' })
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number | Sort-Object) @(1, 2) 'Failed draft checks must enter the repair queue'
foreach ($label in $excludedPrLabels) {
    $draft.labels = @(@{ name = $label })
    Reset-SurveyStats
    Assert-Equal @((Get-Candidates).Number) @(2) "Drafts labelled $label must stay parked"
}
$draft.labels = @()

# Review feedback without a disposition is actionable while checks still run.
$draft.statusCheckRollup = @(@{ name = 'modularpipeline'; status = 'IN_PROGRESS' })
$script:activity = @{ 1 = [pscustomobject]@{ HeadCommittedAt = '2026-10-24T10:00:00Z'; UnresolvedThreads = 1 } }
Reset-SurveyStats
$feedbackCandidate = @(Get-Candidates) | Where-Object Number -eq 1
Assert-Equal $feedbackCandidate.Rank 2 'An unresolved review thread must make a pending PR actionable'
Assert-Equal ($feedbackCandidate.Hint -like '*review feedback*') $true 'A pending PR with feedback must say why it was chosen'
$script:activity[1].UnresolvedThreads = 0
$draft.reviews = @(@{ author = @{ login = 'reviewer' }; state = 'CHANGES_REQUESTED'; submittedAt = '2026-10-24T11:00:00Z' })
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number | Sort-Object) @(1, 2) 'A changes-requested review after the last push is feedback'
$draft.reviews += @{ author = @{ login = 'author' }; state = 'COMMENTED'; submittedAt = '2026-10-24T11:30:00Z' }
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number) @(2) 'Feedback the loop already answered must not wake a pending PR'
$draft.reviews = @(@{ author = @{ login = 'reviewer' }; state = 'CHANGES_REQUESTED'; submittedAt = '2026-10-24T09:00:00Z' })
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number) @(2) 'Feedback older than the last push must not wake a pending PR'
$draft.reviews = @(@{ author = @{ login = 'reviewer' }; state = 'COMMENTED'; submittedAt = '2026-10-24T11:00:00Z' })
$draft.comments = @(@{ body = 'Review: no blocking issues found.'; createdAt = '2026-10-24T11:00:00Z' })
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number) @(2) 'Comments and comment-only reviews without open threads must not wake a pending PR'
$script:activity[2] = [pscustomobject]@{ HeadCommittedAt = '2026-10-24T10:00:00Z'; UnresolvedThreads = 2 }
Reset-SurveyStats
Assert-Equal (@(Get-Candidates) | Where-Object Number -eq 2).Light $false 'A green PR with open threads needs full effort, not the merge gate'
$script:activity = @{}
$draft.comments = @()
$draft.reviews = @()
$draft.statusCheckRollup = @(@{ name = 'Analyze (csharp)'; conclusion = 'FAILURE' })

# At the open-PR limit, existing PRs, finished parents, and stale claims still run but no new issue starts.
function New-TestIssue([int]$Number, [string[]]$Labels = @(), [int]$Children = 0) {
    [pscustomobject]@{
        number = $Number; title = "Issue $Number"; author = @{ __typename = 'User' }; lastEditedAt = $null
        labels = @{ nodes = @($Labels | ForEach-Object { @{ name = $_ } }) }; assignees = @{ nodes = @() }
        closedByPullRequestsReferences = @{ nodes = @() }; comments = @{ totalCount = 0; nodes = @() }
        issueDependenciesSummary = @{ blockedBy = 0; blocking = 0 }
        subIssuesSummary = @{ total = $Children; completed = $Children }; subIssues = @{ nodes = @() }
    }
}
$script:issues = @((New-TestIssue 10), (New-TestIssue 11 @('in-progress')), (New-TestIssue 12 -Children 2), (New-TestIssue 13))
function Get-OpenIssues { $script:issues }
$excludedIssueLabels = @()
$parkedLabels = @()
$MaxOpenPrs = 2
$parked = $draft.PSObject.Copy(); $parked.number = 3; $parked.labels = @(@{ name = 'on-hold' })
$script:prs = @($draft, $parked)
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number) @(1, 12, 10, 13, 11) 'Parked PRs must not count toward the open-PR limit'
$script:prs = @($draft, $ready)
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number) @(2, 1, 12, 11) 'At the open-PR limit only existing work may be queued'
Assert-Equal $surveyStats['issue-wip'] 2 'Issues held by the open-PR limit must be counted'
$MaxOpenPrs = 0
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Count) 6 'A zero open-PR limit must not hold issues'
$script:issues = @()
function Get-OpenIssues { }
$script:prs = @($draft, $ready)

$before = Get-PrFingerprint $draft
$draft.isDraft = $false
Assert-Equal ((Get-PrFingerprint $draft) -ne $before) $true 'Marking ready must invalidate a deferral'
$draft.isDraft = $true

$summary = [pscustomobject]@{
    id = 'summary'; createdAt = '2026-10-24T11:00:00Z'; reactionGroups = @()
    body = '<!-- codex-pull-request-review-summary --> reviewed old-sha'
}
$draft.comments = @($summary)
$before = Get-PrFingerprint $draft
$summary.body = '<!-- codex-pull-request-review-summary --> reviewed head-sha'
Assert-Equal ((Get-PrFingerprint $draft) -ne $before) $true 'A reviewer summary rewritten in place must wake a waiting draft'

$request = [pscustomobject]@{
    id = 'request'; createdAt = '2026-10-24T12:00:00Z'
    body = "<!-- issue-pr-loop -->`n<!-- codex-review:head-sha -->`n@codex review"
    reactionGroups = @()
}
$draft.comments = @($summary, $request)
$before = Get-PrFingerprint $draft
$request.reactionGroups = @(@{ content = 'THUMBS_UP'; users = @{ totalCount = 1 } })
Assert-Equal ((Get-PrFingerprint $draft) -ne $before) $true 'Reaction verdicts on this head''s request must wake a waiting draft'
$before = Get-PrFingerprint $draft
$draft.comments += [pscustomobject]@{ id = 'reply'; body = '<!-- issue-pr-loop --> Disposition'; createdAt = '2026-10-24T13:00:00Z'; reactionGroups = @() }
Assert-Equal (Get-PrFingerprint $draft) $before 'Ordinary loop comments must not invalidate a deferral'
$draft.headRefOid = 'new-head'
$before = Get-PrFingerprint $draft
$request.reactionGroups = @(@{ content = 'EYES'; users = @{ totalCount = 1 } })
Assert-Equal (Get-PrFingerprint $draft) $before 'Reactions on an old-head request must not wake the new head'

Write-Host 'Issue/PR loop draft survey and fingerprint tests passed.'
