# BDTest

Follow the root [AGENTS.md](../../../../AGENTS.md), `Directory.Build.props`, and the conventions of the surrounding code. See [BUILDING.md](../../../../BUILDING.md) and the [contributor guide](../../../../docs/docs/contributing.md) for maintained build and packaging guidance.

- Preserve each project's actual `TargetFramework`/`TargetFrameworks`; runtime packages have package-specific targets (see [supported targets](../../../../docs/docs/getting-started/installation.md#supported-targets)). Treat shipped libraries' public types as a shipped contract. Example, sample report-server, pipeline, and test projects have no compatibility contract. Package selection uses evaluated Release `IsPackable`; follow the [package opt-in and verification guidance](../../../../BUILDING.md#package-selection-and-naming-858) when adding a shipped project.
- Build with `dotnet build BDTest.sln -c Release`. Run each test project that CI runs: every `*.csproj` whose path contains `Tests`, through `dotnet test <project> -c Release`. TUnit test projects and `BDTest.xUnit.Tests` run on Microsoft.Testing.Platform, so run them with `dotnet run --project <project> -c Release` instead.
- CI is the [.NET workflow](../../../../.github/workflows/dotnet.yml): it runs `dotnet run -c Release` in `BDTest.Pipeline` (a ModularPipelines pipeline) that runs the tests, then packs the packages. Pull requests do not publish packages. Do not run the pipeline locally in `Production`, because that environment uploads packages to NuGet.
- Add a regression test in the test project of the framework the change affects.
- PRs labelled `dependencies` (Renovate/Dependabot) are merged by the [dependencies auto-merge workflow](../../../../.github/workflows/dependencies-automerge.yml) once their CI and the merge gate pass; they do not use native auto-merge. Do not merge them with `Merge-Pr.ps1` or arm them with `Enable-AutoMerge.ps1`; fix their CI failures or close a superseded one and link the replacement.
- No Aspire AppHost or other services. Docker serves only the shared lock Redis; never stop it.

## Draft reviews

[AGENTS.md](../../../../AGENTS.md) applies the skill's draft review lifecycle to every agent-created PR, inside or outside the loop. Renovate dependency PRs are not drafts and keep their own merge path. No Claude review workflow runs here; the required reviewers are the two automated reviewers that review every PR:

- **Codex** (`chatgpt-codex-connector[bot]`) reviews automatically only when a PR is opened for review or marked ready, so request it on drafts with `@codex review`. Its verdict for a head is one of: the `codex-pull-request-review-summary` comment whose `codex-review-state:v1` payload has `code_review.commit_sha` equal to the full head SHA, `status` `completed`, and `has_any_findings` `false`; a `codex-review-receipt` comment that reports no findings and names this head; or a thumbs-up from `chatgpt-codex-connector[bot]` on this head's request, verified through the comment reactions API. The summary comment is rewritten in place, so re-read it on each visit.
- **Greptile** (`greptile-apps[bot]`) posts a `greptile_summary` comment, rewritten in place, whose "Last reviewed commit" link names the reviewed head, plus inline threads for findings. When that link does not name the current head, request a review on the draft with `@greptileai review`. Its verdict for a head is that summary naming the full head SHA with every listed finding fixed or dispositioned and its thread resolved. The confidence score is informational.

CodeRabbit (`coderabbitai[bot]`) usually reports a rate limit instead of a review, so it is not required. Disposition any finding it does post like other review feedback.

Automated review rounds (rework budget): use the larger of Greptile's `Reviews (N)` count in its summary and Codex's rounds, which are its `chatgpt-codex-connector[bot]` reviews plus its `codex-review-receipt` comments.

Draft CI: the [.NET](../../../../.github/workflows/dotnet.yml), [documentation](../../../../.github/workflows/docs.yml), and [CodeQL](../../../../.github/workflows/codeql-analysis.yml) workflows skip every job on draft PR events before allocating a runner. `ready_for_review` starts them; `converted_to_draft` cancels an in-flight run through workflow concurrency. Push, schedule, and manual runs are unchanged. Run `pwsh scripts/Test-IssuePrLoop.ps1` after changing how `Start-IssuePrLoop.ps1` surveys drafts.

## Auto-merge

The `main` ruleset requires the `CI Passed` check from GitHub Actions and resolved review conversations. It requires no approving review, because agents cannot approve their own PRs; `Assert-PrGreen.ps1 -ForAutoMerge` checks for current-head Codex and Greptile reviews instead. Repository admins bypass the ruleset, so direct pushes of agent instructions to `main` still work.

`CI Passed` is the `ci-passed` aggregator job in the [.NET workflow](../../../../.github/workflows/dotnet.yml). It needs every other job in that workflow, so add each new job to its `needs`. Its name is an expression, so a skipped run (a draft, or a cancelled run) reports the check under the raw expression text and never satisfies the requirement; a job skipped by `if:` would otherwise count as passing. Keep the expression name, and keep `CI Passed` the only required check unless another required check gets the same protection.

[Auto-merge guard](../../../../.github/workflows/auto-merge-guard.yml) disarms auto-merge whenever a PR's head changes, so each new head needs fresh approvals and a new `Enable-AutoMerge.ps1` call. GitHub deletes the merged head branch on the remote; `Remove-MergedWorktrees.ps1` removes the local worktree and branch. Run `pwsh scripts/Test-AssertPrGreenAutoMerge.ps1` after changing either gate mode or `Enable-AutoMerge.ps1`.
