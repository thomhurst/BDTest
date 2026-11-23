#!/usr/bin/env pwsh
<#
.SYNOPSIS
Validates a release tag and prints the package version it ships.

.DESCRIPTION
Applies the repository's one release-version grammar (ReleaseVersion.psm1), used by
.github/workflows/dotnet.yml for a published release's tag and by .github/workflows/release-draft.yml
for the version it suggests. Prints the version without the `v` prefix, and fails for any other tag.

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

Import-Module (Join-Path $PSScriptRoot 'ReleaseVersion.psm1') -Force

$version = ConvertTo-ReleaseVersion $Tag
if ($null -eq $version) {
    [Console]::Error.WriteLine("'$Tag' is not a release version. Use a tag such as 3.0.1, v3.1.0 or 4.0.0-beta.1, with each number at most 65534.")
    exit 1
}

if ($Stable -and $version.Prerelease.Count -gt 0) {
    [Console]::Error.WriteLine("'$Tag' is a prerelease; a stable version was expected.")
    exit 1
}

$version.Version
