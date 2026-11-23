#!/usr/bin/env pwsh
<#
.SYNOPSIS
Validates a release tag and prints the package version it ships.

.DESCRIPTION
The one release-version grammar for this repository, used by .github/workflows/dotnet.yml for a
published release's tag and by .github/workflows/release-draft.yml for the version it suggests.

Accepts an optional `v` prefix, then a SemVer 2.0 version without build metadata: major.minor.patch
without leading zeros, and an optional prerelease of non-empty, dot-separated identifiers whose
numeric identifiers have no leading zeros. Major, minor and patch must each be at most 65534,
because packing stamps them into AssemblyVersion and FileVersion. Prints the version without the
prefix, and fails for any other tag.

.PARAMETER Tag
The release tag or version, for example 3.0.1, v3.1.0 or 4.0.0-beta.1.

.PARAMETER Stable
Rejects prerelease versions.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [AllowEmptyString()]
    [string] $Tag,

    [switch] $Stable
)

$ErrorActionPreference = 'Stop'

$number = '(0|[1-9][0-9]*)'
$identifier = '(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)'
$pattern = "^v?$number\.$number\.$number(?:-($identifier(?:\.$identifier)*))?\z"

$match = [regex]::Match($Tag, $pattern, [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
if (-not $match.Success) {
    Write-Error "'$Tag' is not a release version. Use a tag such as 3.0.1, v3.1.0 or 4.0.0-beta.1."
    exit 1
}

foreach ($group in 1..3) {
    $part = $match.Groups[$group].Value
    if ($part.Length -gt 5 -or [int] $part -gt 65534) {
        Write-Error "'$Tag' has a version number above 65534, which AssemblyVersion and FileVersion cannot hold."
        exit 1
    }
}

if ($Stable -and $match.Groups[4].Success) {
    Write-Error "'$Tag' is a prerelease; a stable version was expected."
    exit 1
}

$Tag -replace '^v', ''
