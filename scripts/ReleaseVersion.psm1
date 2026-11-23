# The one release-version grammar and SemVer precedence for this repository. Used by
# Get-ReleaseVersion.ps1 and Compare-ReleaseVersion.ps1 (and so by dotnet.yml and
# release-draft.yml), and tested by Test-ReleaseVersion.ps1.
#
# Grammar: an optional `v` prefix, then a SemVer 2.0 version without build metadata. Major, minor
# and patch have no leading zeros and are each at most 65534, because packing stamps them into
# AssemblyVersion and FileVersion. An optional prerelease has non-empty, dot-separated identifiers;
# numeric identifiers have no leading zeros.

$number = '(0|[1-9][0-9]*)'
$identifier = '(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)'
$pattern = "^v?$number\.$number\.$number(?:-($identifier(?:\.$identifier)*))?\z"

# Returns the parsed version, or $null when the tag is not a release version.
function ConvertTo-ReleaseVersion {
    param(
        [AllowEmptyString()]
        [string] $Tag
    )

    $match = [regex]::Match($Tag, $pattern, [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) {
        return $null
    }

    $parts = [int[]]::new(3)
    for ($i = 0; $i -lt 3; $i++) {
        $value = $match.Groups[$i + 1].Value
        if ($value.Length -gt 5 -or [int] $value -gt 65534) {
            return $null
        }
        $parts[$i] = [int] $value
    }

    $prerelease = if ($match.Groups[4].Success) { @($match.Groups[4].Value.Split('.')) } else { @() }

    [pscustomobject]@{
        Version    = $Tag -replace '^v', ''
        Major      = $parts[0]
        Minor      = $parts[1]
        Patch      = $parts[2]
        Prerelease = $prerelease
    }
}

# SemVer 2.0 precedence of two parsed versions: -1, 0 or 1.
function Compare-ReleaseVersion {
    param(
        [Parameter(Mandatory)] $Left,
        [Parameter(Mandatory)] $Right
    )

    foreach ($name in 'Major', 'Minor', 'Patch') {
        if ($Left.$name -ne $Right.$name) {
            return [Math]::Sign($Left.$name - $Right.$name)
        }
    }

    $l = @($Left.Prerelease)
    $r = @($Right.Prerelease)
    # A version without a prerelease is higher than the same version with one.
    if ($l.Count -eq 0 -or $r.Count -eq 0) {
        return [Math]::Sign($r.Count - $l.Count)
    }

    for ($i = 0; $i -lt [Math]::Min($l.Count, $r.Count); $i++) {
        $a = $l[$i]
        $b = $r[$i]
        $aNumeric = $a -match '^[0-9]+$'
        $bNumeric = $b -match '^[0-9]+$'
        if ($aNumeric -and $bNumeric) {
            # Identifiers have no leading zeros, so length then ordinal order is numeric order,
            # without overflow for long identifiers.
            $result = if ($a.Length -ne $b.Length) { [Math]::Sign($a.Length - $b.Length) } else { [Math]::Sign([string]::CompareOrdinal($a, $b)) }
        } elseif ($aNumeric) {
            $result = -1
        } elseif ($bNumeric) {
            $result = 1
        } else {
            $result = [Math]::Sign([string]::CompareOrdinal($a, $b))
        }
        if ($result -ne 0) {
            return $result
        }
    }

    return [Math]::Sign($l.Count - $r.Count)
}

Export-ModuleMember -Function ConvertTo-ReleaseVersion, Compare-ReleaseVersion
