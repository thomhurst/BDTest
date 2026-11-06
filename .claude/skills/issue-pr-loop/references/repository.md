# BDTest

Follow the root [AGENTS.md](../../../../AGENTS.md), `Directory.Build.props`, and the conventions of the surrounding code. See [BUILDING.md](../../../../BUILDING.md) and the [contributor guide](../../../../docs/docs/contributing.md) for maintained build and packaging guidance.

- Preserve each project's actual `TargetFramework`/`TargetFrameworks`; runtime packages have package-specific targets (see [supported targets](../../../../docs/docs/getting-started/installation.md#supported-targets)). Treat shipped libraries' public types as a shipped contract. Example, sample report-server, pipeline, and test projects have no compatibility contract. Package selection uses evaluated Release `IsPackable`; follow the [package opt-in and verification guidance](../../../../BUILDING.md#package-selection-and-naming-858) when adding a shipped project.
- Build with `dotnet build BDTest.sln -c Release`. Run each test project that CI runs: every `*.csproj` whose path contains `Tests`, through `dotnet test <project> -c Release`. TUnit test projects and `BDTest.xUnit.Tests` run on Microsoft.Testing.Platform, so run them with `dotnet run --project <project> -c Release` instead.
- CI is the [.NET workflow](../../../../.github/workflows/dotnet.yml): it runs `dotnet run -c Release` in `BDTest.Pipeline` (a ModularPipelines pipeline) that runs the tests, then packs the packages. Pull requests do not publish packages. Do not run the pipeline locally in `Production`, because that environment uploads packages to NuGet.
- Add a regression test in the test project of the framework the change affects.
- PRs labelled `dependencies` (Renovate/Dependabot) are merged by the [dependencies auto-merge workflow](../../../../.github/workflows/dependencies-automerge.yml) once their CI and the merge gate pass; they do not use native auto-merge. Do not merge them with `Merge-Pr.ps1` or arm them with `Enable-AutoMerge.ps1`; fix their CI failures or close a superseded one and link the replacement.
- No Aspire AppHost or other services. Docker serves only the shared lock Redis; never stop it.

The mechanics this loop defers to:

| Need | Owner |
|---|---|
| One PR's review cycle: fetch every surface, verify findings, fix, one push, reply, resolve and unresolve threads | [Review cycle](#review-cycle) below and `AGENTS.md` § Conventions |
| Merge protocol | [Merge gate](#merge-gate) and [Auto-merge](#auto-merge) below |
| Out-of-scope filing bar and procedure | `AGENTS.md` § Autonomous self-improvement and § Writing issues |
| Native sub-issue and `blocked_by` writes | [Issue relationships](#issue-relationships) below |
| Worktree location, lock registration, evidence directory, cleanup | [Worktrees and evidence](#worktrees-and-evidence) below |
| PR review lifecycle | [draft-review.md](draft-review.md) and [Draft reviews](#draft-reviews) below |
| Validation | `AGENTS.md` § Commands and § Conventions, and [Implement and ship](#implement-and-ship) below |

Lock owner variable: `BDTEST_AGENT_LOCK_OWNER_ID` (read by `scripts/AgentLocks.ps1` after `-OwnerId`, before `CODEX_THREAD_ID`).

## Owner-reserved decisions

Major dependency updates (`major-update`) are reviewed and merged by hand ([dependency updates](../../../../.github/dependency-updates.md)): fix their CI if needed, then leave them to the owner. No other issue is reserved beyond the `[human]` row in SKILL.md § Resolve, do not defer.

## Queue

Apply the dependency PR rule above before choosing repair work: such a PR may need closure and a linked replacement instead of a fix. After a fix push, allow a bot review/CI cycle before considering merge in a later iteration. Use commentary while active.

`scripts/Start-IssuePrLoop.ps1` ranks claimable issues by how many they block, then by fewest comments. It reads no `priority:*` or `ci` label, so the tier order in [pick-an-issue.md](pick-an-issue.md) applies only when you choose an issue yourself.

## Draft reviews

[AGENTS.md](../../../../AGENTS.md) applies the skill's draft review lifecycle to every agent-created PR, inside or outside the loop. Renovate dependency PRs are not drafts and keep their own merge path. No Claude review workflow runs here; the required reviewers are the two automated reviewers that review every PR:

- **Codex** (`chatgpt-codex-connector[bot]`) reviews automatically only when a PR is opened for review or marked ready, so request it on drafts with `@codex review`. Its verdict for a head is one of: the `codex-pull-request-review-summary` comment whose `codex-review-state:v1` payload has `code_review.commit_sha` equal to the full head SHA, `status` `completed`, and `has_any_findings` `false`; a `codex-review-receipt` comment that reports no findings and names this head; or a thumbs-up from `chatgpt-codex-connector[bot]` on this head's request, verified through the comment reactions API. The summary comment is rewritten in place, so re-read it on each visit.
- **Greptile** (`greptile-apps[bot]`) posts a `greptile_summary` comment, rewritten in place, whose "Last reviewed commit" link names the reviewed head, plus inline threads for findings. When that link does not name the current head, request a review on the draft with `@greptileai review`. Its verdict for a head is that summary naming the full head SHA with every listed finding fixed or dispositioned and its thread resolved. The confidence score is informational.

[draft-review.md](draft-review.md) names Claude and Codex; here read every Claude step as Greptile, which does not review drafts by itself. Request either reviewer as its step 1 says, with a marker of `<!-- codex-review:<full-head-SHA> -->` or `<!-- greptile-review:<full-head-SHA> -->`, and apply the same 15-minute timeout and no-duplicate rules. Step 5's Claude re-run does not apply.

After promotion: readiness can trigger automatic reviews of the same head; read them on the next visit. When one raises a blocking finding, return the PR to draft (`gh pr ready <N> --undo`, which disarms auto-merge) and fix it through the lifecycle. A push disarms auto-merge, so a CI fix also goes back through draft review and is re-armed only after the new head's approvals.

CodeRabbit (`coderabbitai[bot]`) usually reports a rate limit instead of a review, so it is not required. Disposition any finding it does post like other review feedback.

Automated review rounds (rework budget): use the larger of Greptile's `Reviews (N)` count in its summary and Codex's rounds, which are its `chatgpt-codex-connector[bot]` reviews plus its `codex-review-receipt` comments.

Draft CI: the [.NET](../../../../.github/workflows/dotnet.yml), [documentation](../../../../.github/workflows/docs.yml), and [CodeQL](../../../../.github/workflows/codeql-analysis.yml) workflows skip every job on draft PR events before allocating a runner. `ready_for_review` starts them; `converted_to_draft` cancels an in-flight run through workflow concurrency. Push, schedule, and manual runs are unchanged. Run `pwsh scripts/Test-IssuePrLoop.ps1` after changing how `Start-IssuePrLoop.ps1` surveys drafts.

## Merge gate

The normal path is native auto-merge, armed by step 5 of [draft-review.md](draft-review.md) with `pwsh scripts/Enable-AutoMerge.ps1 -Pr <N>`, which runs `Assert-PrGreen.ps1 -ForAutoMerge` (pending CI allowed; failures, a missing current-head review, unresolved threads, or a base branch without the required check deny) and arms a squash merge pinned to the gated head. Re-arm a ready, approved PR whose auto-merge was disarmed the same way. To merge a ready PR whose checks already passed without waiting, use `Merge-Pr.ps1`.

An immediate merge requires an open, ready-for-review PR promoted through the draft lifecycle; `MERGEABLE` and `CLEAN`; every applicable check present, terminal, and passing (`SUCCESS`, `SKIPPED`, or `NEUTRAL`, with successful legacy commit statuses); no unresolved threads, unaddressed comments, or outstanding review-body concerns; approval when required; and a bot review/CI cycle after the last fix push. Missing expected checks and nonterminal checks block merging. Reassess if the head changes.

`Merge-Pr.ps1` calls `Assert-PrGreen.ps1`, merges on a passing mechanical gate, and cleans up. Run it and `Enable-AutoMerge.ps1` from the shared checkout, never from inside the worktree. Both are necessary but not sufficient: judge review dispositions and approval yourself first. Never bypass them with direct `gh pr merge` or `gh pr merge --auto`.

## Auto-merge

The `main` ruleset requires the `CI Passed` check from GitHub Actions and resolved review conversations. It requires no approving review, because agents cannot approve their own PRs; `Assert-PrGreen.ps1 -ForAutoMerge` checks for current-head Codex and Greptile reviews instead. Repository admins bypass the ruleset, so direct pushes of agent instructions to `main` still work.

`CI Passed` is the `ci-passed` aggregator job in the [.NET workflow](../../../../.github/workflows/dotnet.yml). It needs every other job in that workflow, so add each new job to its `needs`. Its name is an expression, so a skipped run (a draft, or a cancelled run) reports the check under the raw expression text and never satisfies the requirement; a job skipped by `if:` would otherwise count as passing. Keep the expression name, and keep `CI Passed` the only required check unless another required check gets the same protection.

[Auto-merge guard](../../../../.github/workflows/auto-merge-guard.yml) disarms auto-merge whenever a PR's head changes, so each new head needs fresh approvals and a new `Enable-AutoMerge.ps1` call. GitHub deletes the merged head branch on the remote; `Remove-MergedWorktrees.ps1` removes the local worktree and branch. Run `pwsh scripts/Test-AssertPrGreenAutoMerge.ps1` after changing either gate mode or `Enable-AutoMerge.ps1`.

## Review cycle

Run it under the PR lock. `AGENTS.md` § Conventions sets the baseline; this is the full procedure.

Fetch every surface, including pagination and replies: REST `pulls/<N>/reviews`, `pulls/<N>/comments`, and `issues/<N>/comments` under `repos/{owner}/{repo}`, and GraphQL `reviewThreads` with IDs, resolution state, bodies, authors, and timestamps. Never infer a reviewer's surface from its identity; check both every cycle. A conversation comment that carries findings or a verdict is a review with no thread to resolve: disposition it with one PR comment that quotes or links it.

A `COMMENTED` review body can block with zero unresolved threads. Address outstanding concerns, `CHANGES_REQUESTED`, current inline findings, and unresolved threads. Empty acknowledgments are non-blocking. Assess older findings against the current head and later discussion; uncertain concerns block merging.

Verify every finding against the code and classify it:

- **Blocking:** a correctness, concurrency, security, contract, or performance defect, or missing tests for changed behavior. Fix each valid one in this PR.
- **Non-blocking:** naming, style, wording, optional refactors, extra tests for unchanged behavior, and "consider" suggestions. Implement one only when it is small and you already push a blocking fix. Otherwise decline it in the disposition with a one-line reason and push nothing for it.

Decline a wrong finding with evidence from the code. Move genuinely out-of-scope architectural work to a filed follow-up issue, linked in the reply. "Not done" or "left as is" without a classification, reason, or linked issue is not a disposition. Reply once per review covering each finding, and reply on each thread. Discuss disagreement once; implement reaffirmed blocking feedback. A disposition-only reply on the current head needs no new push, so it starts no new review cycle.

Resolve every thread, whether a human or a bot opened it, as soon as it is dispositioned: the fix commit is in the remote PR head and the reply names it, or the reply pushes back with evidence. Use GraphQL `resolveReviewThread`, without waiting for another review or CI cycle; that cycle still gates merging. Leave a thread open only while it has no disposition: the fix is not pushed, the item waits on the user, or the reviewer replied after your disposition. On each later cycle re-fetch every thread, resolved ones included. A reviewer reply newer than your disposition reopens it: run GraphQL `unresolveReviewThread` and handle it as reaffirmed feedback. Reply to addressed review-body findings and request re-review when an outstanding changes-requested review needs updating. Never dismiss a review to bypass approval requirements. Afterwards re-fetch reviews, threads, checks, and head SHA.

## Issue relationships

Native relationships take the numeric REST issue `.id`, not the issue number or `gh issue view --json id` (a GraphQL ID). Pass it with `-F` so it is sent as an integer:

```powershell
$childId = gh api "repos/$repoSlug/issues/<child>" --jq .id
gh api "repos/$repoSlug/issues/<parent>/sub_issues" -X POST -F "sub_issue_id=$childId"

$blockerId = gh api "repos/$repoSlug/issues/<blocker>" --jq .id
gh api "repos/$repoSlug/issues/<blocked>/dependencies/blocked_by" -X POST -F "issue_id=$blockerId"
```

Verify each write with `gh api "repos/$repoSlug/issues/<parent>/sub_issues"` or `gh api "repos/$repoSlug/issues/<blocked>/dependencies/blocked_by"`.

## Worktrees and evidence

Capture these with the values in SKILL.md § Isolation and ownership:

```powershell
$repoName = ($repoSlug -split '/')[-1]
$worktreeRoot = Join-Path (Split-Path $repo -Parent) ($repoName + '-worktrees')
```

Verify the repository and intended base; examples use `origin/main`. Use a writable temporary `<repoName>-worktrees` root if needed. No sparse-checkout or special push refspec applies here. Shell directory changes do not persist, so set `workdir` on each call, and compare `git rev-parse --show-toplevel` with the worktree's resolved absolute path. Keep explicit lock names.

For PR work, create the detached worktree with `git worktree add --detach --lock --reason 'PR checkout setup'`. Wrap `gh pr checkout` and lock-path registration in `try/finally`: in `finally`, run `git worktree unlock <absolute-path>` for this temporary lock, including when checkout or registration fails, before the outer Redis release. Report an unlock failure and preserve the path for recovery. The temporary lock protects setup from concurrent cleanup; Redis remains the ownership authority.

Write nothing loose beside the repository. Every file an agent produces outside a worktree has one home, so cleanup can find it:

- Evidence (build and test logs, reports, PR and comment body drafts, diffs, validation projects) goes in `<repo-parent>/BDTest-evidence/<lock-name>/`, for example `C:/git/BDTest-evidence/issue-123/build.log`. `scripts/Remove-MergedWorktrees.ps1` deletes an entry once its newest file is older than `-EvidenceRetentionDays` (default 7).
- Throwaway checkouts and baselines are worktrees under `$worktreeRoot`, never plain copies.
- Downloaded SDKs and CLI tools go in `<repo-parent>/toolchains/<name>-<version>/`, shared by every repository.
- Never write to `<repo-parent>` itself or to the shared checkout.

Use the repository scripts for merged-worktree cleanup; `[gone]` or ancestry alone is insufficient. Preserve dirty, locked, open-PR, harness-managed, and locally divergent worktrees. Inspect untracked source before cleanup, because the scripts may clear build artifacts. Remove an abandoned worktree by hand only after verifying its repository and path, its ownership, and that it holds no valuable unpublished work.

Stop only the services, processes, and containers started for this worktree. Never blanket-delete containers or volumes, and never stop the shared lock Redis. Lifecycle, preservation, and crash behavior: [WorktreeLifecycle.md](../../../../scripts/WorktreeLifecycle.md).

## Implement and ship

Follow the validation, resource limits, target frameworks, generated-code boundaries, and performance requirements in `AGENTS.md`. Use TDD for bugs and behavior changes where feasible, and explain exceptions. Run focused and relevant broader checks, including docs and frontend checks when affected. Review the full diff for correctness, reuse, and efficiency (`/simplify` when available, otherwise an equivalent review). Preserve performance and contracts, and batch related fixes into one push.
