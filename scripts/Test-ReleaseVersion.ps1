#!/usr/bin/env pwsh
# Checks the release-version grammar (Get-ReleaseVersion.ps1) and SemVer precedence
# (Compare-ReleaseVersion.ps1), both from ReleaseVersion.psm1.
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

Import-Module (Join-Path $PSScriptRoot 'ReleaseVersion.psm1') -Force

# Each pair is strictly ascending.
$ascending = @(
    @('1.10.1', '1.11.0'),
    @('1.9.0', '1.10.0'),
    @('1.2.3', '1.2.4'),
    @('1.99.99', '2.0.0'),
    @('2.0.0-rc.9', '2.0.0-rc.10'),
    @('2.0.0-rc.1', '2.0.0'),
    @('1.0.0-alpha', '1.0.0-alpha.1'),
    @('1.0.0-alpha.1', '1.0.0-alpha.beta'),
    @('1.0.0-alpha.beta', '1.0.0-beta'),
    @('1.0.0-beta.2', '1.0.0-beta.11'),
    @('1.0.0-rc.1', '1.0.0'),
    @('1.0.0-1', '1.0.0-alpha'),
    @('1.0.0-Beta', '1.0.0-alpha'),
    @('3.0.1', 'v3.1.0'),
    @('1.0.0-99999999999999999999', '1.0.0-100000000000000000000')
)
$comparisons = 0
foreach ($pair in $ascending) {
    $low = ConvertTo-ReleaseVersion $pair[0]
    $high = ConvertTo-ReleaseVersion $pair[1]
    $up = Compare-ReleaseVersion $low $high
    $down = Compare-ReleaseVersion $high $low
    if ($up -ne -1 -or $down -ne 1) {
        $failures.Add("Expected $($pair[0]) < $($pair[1]); got $up and $down.")
    }
    $comparisons++
}
foreach ($pair in @(@('1.2.3', 'v1.2.3'), @('1.0.0-beta.1', 'v1.0.0-beta.1'))) {
    $result = Compare-ReleaseVersion (ConvertTo-ReleaseVersion $pair[0]) (ConvertTo-ReleaseVersion $pair[1])
    if ($result -ne 0) {
        $failures.Add("Expected $($pair[0]) = $($pair[1]); got $result.")
    }
    $comparisons++
}

$compare = Join-Path $PSScriptRoot 'Compare-ReleaseVersion.ps1'
$output = & pwsh -NoProfile -File $compare -Left 'v1.11.0' -Right '1.10.1' 2>$null
if ($LASTEXITCODE -ne 0 -or $output -ne '1') {
    $failures.Add("Expected Compare-ReleaseVersion.ps1 v1.11.0 1.10.1 to print 1; got exit $LASTEXITCODE, '$output'.")
}
& pwsh -NoProfile -File $compare -Left 'release-1' -Right '1.10.1' 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) {
    $failures.Add('Expected Compare-ReleaseVersion.ps1 to reject release-1.')
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host "FAIL: $_" }
    exit 1
}

Write-Host "Release version grammar: $($accepted.Count) accepted, $($rejected.Count) rejected, -Stable checked; $comparisons precedence comparisons."
# The last native call above may have exited non-zero on purpose; report the result explicitly.
exit 0
