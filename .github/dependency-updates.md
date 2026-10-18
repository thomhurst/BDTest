# Dependency updates

Renovate is the only configured dependency update service. Dependabot version
updates are disabled by removing its configuration. GitHub security alerts are
independent of that configuration.

`renovate.json` labels every update `dependencies`. Minor, patch, pin, and digest
updates also receive `dependency-automerge`. Major updates receive `major-update`
and must be reviewed and merged manually. Renovate's own automerge is disabled;
`scripts/Merge-DependencyPr.ps1` performs merges through the repository's current
head CI and review gate. It accepts only `renovate[bot]` PRs with both opt-in labels
and rejects `major-update`, even when the opt-in label remains present. Existing
Dependabot PRs and unclassified Renovate PRs require manual handling.

Framework-aligned `Microsoft.AspNetCore.*` and `Microsoft.Extensions.*` NuGet
packages stay below 11.0.0 while the consuming projects target `net10.0`. Update
that ceiling in the same reviewed change that upgrades those target frameworks.
This is an explicit version ceiling, not automatic NuGet target framework
detection. Other packages can also drop target framework support; every update
still has to pass the solution build, tests, and review checks before merging.

Validate policy changes with:

```powershell
npx --yes --package renovate renovate-config-validator --strict --no-global renovate.json
pwsh scripts/Test-DependencyAutomerge.ps1
```
