#!/usr/bin/env pwsh
# Checks the release-version grammar in Get-ReleaseVersion.ps1.
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'Get-ReleaseVersion.ps1'

$accepted = @{
    'v1.2.3' = '1.2.3'
    '1.2.3' = '1.2.3'
    'v0.0.0' = '0.0.0'
    'v1.2.3-beta.1' = '1.2.3-beta.1'
    'v1.2.3-beta01' = '1.2.3-beta01'
    'v1.2.3-0' = '1.2.3-0'
    '3.0.1' = '3.0.1'
    '4.0.0-rc.2-x' = '4.0.0-rc.2-x'
    'v65534.65534.65534' = '65534.65534.65534'
}
$rejected = @(
    'v1.02.3', 'v01.2.3', 'v1.2.3-beta..1', 'v1.2.3-beta.01', 'v1.2.3-01', 'v1.2.3-', 'v1.2',
    'v65535.0.0', 'v1.65535.0', 'v1.2.99999999999', 'release-1', 'V1.2.3', 'v1.2.3+build.5',
    ' 1.2.3', '1.2.3 ', "1.2.3`n", ''
)

$failures = [System.Collections.Generic.List[string]]::new()

foreach ($entry in $accepted.GetEnumerator()) {
    $output = & pwsh -NoProfile -File $script -Tag $entry.Key 2>$null
    if ($LASTEXITCODE -ne 0 -or $output -ne $entry.Value) {
        $failures.Add("Expected '$($entry.Key)' to give '$($entry.Value)'; got exit $LASTEXITCODE, '$output'.")
    }
}

foreach ($tag in $rejected) {
    & pwsh -NoProfile -File $script -Tag $tag 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $failures.Add("Expected '$tag' to be rejected.")
    }
}

& pwsh -NoProfile -File $script -Tag 'v1.2.3-beta.1' -Stable 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) {
    $failures.Add('Expected -Stable to reject a prerelease.')
}
$output = & pwsh -NoProfile -File $script -Tag '3.0.1' -Stable 2>$null
if ($LASTEXITCODE -ne 0 -or $output -ne '3.0.1') {
    $failures.Add("Expected -Stable to accept 3.0.1; got exit $LASTEXITCODE, '$output'.")
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host "FAIL: $_" }
    exit 1
}

Write-Host "Release version grammar: $($accepted.Count) accepted, $($rejected.Count) rejected, -Stable checked."
