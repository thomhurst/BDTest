# Exercise the loop survey and deferral fingerprints for draft PRs without GitHub, Redis, or an agent process.
$ErrorActionPreference = 'Stop'

$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot 'Start-IssuePrLoop.ps1'), [ref]$null, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors -join "`n") }
foreach ($name in 'Get-Hash', 'Get-CheckState', 'Get-LatestOutsideComment', 'Get-PrFingerprint',
    'Reset-SurveyStats', 'New-Candidate', 'Get-Candidates') {
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
