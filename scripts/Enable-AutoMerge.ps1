# Enable-AutoMerge.ps1
# Arms GitHub native auto-merge (squash) for a ready PR, pinned to its reviewed head:
#   1. reads the PR head
#   2. runs  Assert-PrGreen.ps1 -ForAutoMerge  (not a draft, `CI Passed` required by the
#      base branch rules, no failed check, Codex and Greptile reviewed this head, no
#      actionable review body, no unresolved thread) and stops on any denial
#   3. arms  gh pr merge --auto --squash --match-head-commit <head>
#
# Run it right after `gh pr ready`; do not wait for CI. GitHub merges once the `main`
# ruleset's required `CI Passed` check passes. A later push disarms auto-merge
# (.github/workflows/auto-merge-guard.yml), so each new head needs its own review cycle
# and a new call to this script.
#
# The caller must read and address every review body, inline finding, and comment
# BEFORE running this; the gate cannot judge review content.
#
# GitHub deletes the merged head branch on the remote; scripts/Remove-MergedWorktrees.ps1
# removes the local worktree and branch after the merge.
#
# Usage: pwsh scripts/Enable-AutoMerge.ps1 -Pr 1234 [-Repo owner/name]
# Exit:  0 = armed; 1 = not armed (reason printed to stderr).

[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$Pr,
    [string]$Repo
)

$ErrorActionPreference = 'Stop'

function Fail([string]$reason) {
    [Console]::Error.WriteLine("NOT ARMED #${Pr} -- $reason")
    exit 1
}

$repoArgs = @()
if ($Repo) { $repoArgs = @('--repo', $Repo) }

# Capture the head before the gate. --match-head-commit makes GitHub refuse to arm when
# a push raced the gate, so an unreviewed head can never be armed.
$head = gh pr view $Pr @repoArgs --json headRefOid --jq '.headRefOid' 2>$null
if ($LASTEXITCODE -ne 0 -or -not $head) { Fail "could not read PR head (exit $LASTEXITCODE)" }
$head = ([string]$head).Trim()
if ($head -notmatch '^[0-9a-f]{40}$') { Fail "PR head '$head' is not a commit SHA" }

$gateArgs = @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'Assert-PrGreen.ps1'), '-Pr', $Pr, '-ForAutoMerge', '-Json')
if ($Repo) { $gateArgs += @('-Repo', $Repo) }
$gateOutput = & pwsh @gateArgs
if ($LASTEXITCODE -ne 0) { Fail 'auto-merge gate denied (see above)' }
try { $gatedHead = [string](($gateOutput | ConvertFrom-Json).headRefOid) }
catch { Fail 'could not parse the gated PR head' }
if ($gatedHead -ne $head) { Fail "head moved from $head to $gatedHead during the gate; review the new head" }

gh pr merge $Pr @repoArgs --auto --squash --match-head-commit $head
if ($LASTEXITCODE -ne 0) { Fail "gh pr merge --auto failed (exit $LASTEXITCODE)" }

Write-Host "Armed auto-merge for #${Pr} at $head."
exit 0
