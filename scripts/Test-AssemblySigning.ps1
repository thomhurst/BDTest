[CmdletBinding()]
param([string] $Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
[xml] $props = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
$publicKey = $props.SelectSingleNode('/Project/PropertyGroup/BDTestPublicKey').InnerText
$signedProjects = @('BDTest', 'BDTest.Contracts', 'BDTest.NUnit', 'BDTest.xUnit', 'BDTest.MSTest',
    'BDTest.TUnit', 'BDTest.Razor.Reports', 'BDTest.Reports.Storage.FileSystem', 'BDTest.Reports.Storage.AzureBlob', 'BDTest.Tests')

# Inspect built metadata without loading assemblies or their runtime dependencies.
foreach ($project in Get-ChildItem -LiteralPath $repoRoot -Filter '*.csproj' -Depth 1) {
    [xml] $projectXml = Get-Content -LiteralPath $project.FullName -Raw
    $targets = $projectXml.SelectSingleNode('/Project/PropertyGroup/TargetFrameworks | /Project/PropertyGroup/TargetFramework').InnerText.Split(';')
    $expectedKey = if ($project.BaseName -in $signedProjects) { $publicKey } else { '' }
    $firstIdentity = $null
    foreach ($target in $targets) {
        $assemblyPath = Join-Path $project.DirectoryName "bin/$Configuration/$target/$($project.BaseName).dll"
        $identity = [System.Reflection.AssemblyName]::GetAssemblyName($assemblyPath)
        $keyBytes = $identity.GetPublicKey()
        $actualKey = if ($keyBytes) { [Convert]::ToHexString($keyBytes).ToLowerInvariant() } else { '' }
        if ($identity.Name -ne $project.BaseName -or $actualKey -ne $expectedKey) {
            throw "Unexpected assembly name or public key: $assemblyPath ($($identity.FullName))"
        }
        if ($firstIdentity -and $identity.FullName -ne $firstIdentity) {
            throw "Assembly identity differs across targets: $assemblyPath"
        }
        $firstIdentity = $identity.FullName
        $stream = [System.IO.File]::OpenRead($assemblyPath)
        $reader = [System.Reflection.PortableExecutable.PEReader]::new($stream)
        try {
            $isSigned = ($reader.PEHeaders.CorHeader.Flags -band [System.Reflection.PortableExecutable.CorFlags]::StrongNameSigned) -ne 0
            if ($isSigned -ne ($expectedKey.Length -gt 0)) {
                throw "Unexpected strong-name signing flag: $assemblyPath"
            }
        }
        finally {
            $reader.Dispose()
            $stream.Dispose()
        }
        Write-Host "$($project.BaseName) ($target): $($identity.FullName)"
    }
}
