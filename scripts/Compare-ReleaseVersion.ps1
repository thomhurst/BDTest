#!/usr/bin/env pwsh
<#
.SYNOPSIS
Prints -1, 0 or 1 for the SemVer 2.0 precedence of two release tags.

.DESCRIPTION
Both tags must satisfy the repository's release-version grammar (ReleaseVersion.psm1); the script
fails otherwise. A `v` prefix is ignored.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [AllowEmptyString()]
    [string] $Left,

    [Parameter(Mandatory)]
    [AllowEmptyString()]
    [string] $Right
)

Import-Module (Join-Path $PSScriptRoot 'ReleaseVersion.psm1') -Force

$l = ConvertTo-ReleaseVersion $Left
$r = ConvertTo-ReleaseVersion $Right
if ($null -eq $l -or $null -eq $r) {
    [Console]::Error.WriteLine("'$Left' and '$Right' must both be release versions.")
    exit 1
}

Compare-ReleaseVersion $l $r
