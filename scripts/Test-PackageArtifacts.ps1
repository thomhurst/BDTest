[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [switch] $RequireNormalizedPaths
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine the source commit.' }

function Read-PackageXml($archive, [string] $entryName) {
    $entry = $archive.GetEntry($entryName)
    if ($null -eq $entry) { throw "Missing $entryName in package." }
    $reader = [System.IO.StreamReader]::new($entry.Open())
    try { return [xml] $reader.ReadToEnd() }
    finally { $reader.Dispose() }
}

function Open-PackageEntry($archive, [string] $entryName) {
    $entry = $archive.GetEntry($entryName)
    if ($null -eq $entry) { throw "Missing $entryName in package." }
    $buffer = [System.IO.MemoryStream]::new()
    $stream = $entry.Open()
    try { $stream.CopyTo($buffer) }
    finally { $stream.Dispose() }
    $buffer.Position = 0
    return ,$buffer
}

# Match the projects selected by PackProjectsModule, including Razor reports.
$projects = @(Get-ChildItem $repoRoot -Recurse -Filter '*.csproj' |
    Where-Object { $_.FullName -notmatch 'Tests|Pipeline|Example|ReportGenerator' })
if ($projects.Count -eq 0) { throw 'No package projects found.' }

foreach ($project in $projects) {
    $output = Join-Path $project.DirectoryName "bin/$Configuration"
    $packages = @(Get-ChildItem $output -Filter '*.nupkg' -File |
        Where-Object { $_.Name -notlike '*.symbols.nupkg' })
    if ($packages.Count -ne 1) { throw "$project must have exactly one primary package; found $($packages.Count)." }

    $packagePath = $packages[0].FullName
    $symbolsPath = [System.IO.Path]::ChangeExtension($packagePath, '.snupkg')
    $package = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    $symbols = $null
    try {
        $symbols = [System.IO.Compression.ZipFile]::OpenRead($symbolsPath)
        $manifestName = @($package.Entries | Where-Object { $_.FullName.EndsWith('.nuspec') })[0].FullName
        $manifest = Read-PackageXml $package $manifestName
        $symbolManifest = Read-PackageXml $symbols $manifestName
        if ($manifest.package.metadata.id -ne $symbolManifest.package.metadata.id -or
            $manifest.package.metadata.version -ne $symbolManifest.package.metadata.version) {
            throw "Package/symbol identity mismatch: $packagePath"
        }

        $assemblies = @($package.Entries | Where-Object { $_.FullName -match '^(lib|build)/.*\.dll$' })
        if ($assemblies.Count -eq 0) { throw "No library assemblies in $packagePath" }
        foreach ($assembly in $assemblies) {
            $xmlPath = [System.IO.Path]::ChangeExtension($assembly.FullName, '.xml')
            $documentation = Read-PackageXml $package $xmlPath
            if ($documentation.doc.assembly.name -ne [System.IO.Path]::GetFileNameWithoutExtension($assembly.Name)) {
                throw "XML documentation assembly mismatch: $xmlPath"
            }
        }

        foreach ($assembly in @($assemblies | Where-Object { $_.FullName.StartsWith('lib/') })) {
            $pdbPath = [System.IO.Path]::ChangeExtension($assembly.FullName, '.pdb')
            $pdbStream = Open-PackageEntry $symbols $pdbPath
            $assemblyStream = Open-PackageEntry $package $assembly.FullName
            $provider = $null
            $pe = $null
            try {
                $provider = [System.Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream(
                    $pdbStream, [System.Reflection.Metadata.MetadataStreamOptions]::LeaveOpen, 0)
                $metadata = $provider.GetMetadataReader([System.Reflection.Metadata.MetadataReaderOptions]::Default, $null)
                $pe = [System.Reflection.PortableExecutable.PEReader]::new($assemblyStream)
                $debugEntries = @($pe.ReadDebugDirectory())
                $codeView = @($debugEntries | Where-Object { $_.Type -eq 'CodeView' })
                $id = [byte[]] @($metadata.DebugMetadataHeader.Id)
                if ($codeView.Count -ne 1 -or
                    $pe.ReadCodeViewDebugDirectoryData($codeView[0]).Guid -ne [guid]::new([byte[]] $id[0..15])) {
                    throw "PDB does not match assembly: $pdbPath"
                }
                if (@($debugEntries | Where-Object { $_.Type -eq 'Reproducible' }).Count -ne 1) {
                    throw "Assembly is not deterministic: $($assembly.FullName)"
                }

                $sourceLinks = @(
                    foreach ($handle in $metadata.CustomDebugInformation) {
                        $info = $metadata.GetCustomDebugInformation($handle)
                        if ($metadata.GetGuid($info.Kind) -eq [guid] 'cc110556-a091-4d38-9fec-25ab9a351a6a') {
                            [System.Text.Encoding]::UTF8.GetString($metadata.GetBlobBytes($info.Value)) | ConvertFrom-Json -AsHashtable
                        }
                    }
                )
                if ($sourceLinks.Count -ne 1 -or $sourceLinks[0].documents.Count -eq 0) {
                    throw "Missing Source Link mapping: $pdbPath"
                }
                foreach ($mapping in $sourceLinks[0].documents.GetEnumerator()) {
                    if ($mapping.Value -ne "https://raw.githubusercontent.com/thomhurst/BDTest/$commit/*") {
                        throw "Unexpected Source Link URL: $($mapping.Value)"
                    }
                    if ($RequireNormalizedPaths -and $mapping.Key -notmatch '^/_/') {
                        throw "Source Link contains a machine-specific path: $($mapping.Key)"
                    }
                }
                if ($metadata.Documents.Count -eq 0) { throw "No source documents in $pdbPath" }
                if ($RequireNormalizedPaths) {
                    foreach ($handle in $metadata.Documents) {
                        $document = $metadata.GetDocument($handle)
                        $name = $metadata.GetString($document.Name)
                        # The SDK maps the repository to /_/ and dependency source roots to /_1/, /_2/, etc.
                        if ($name -notmatch '^/_\d*/') { throw "PDB contains a machine-specific path: $name" }
                    }
                    if ($pe.ReadCodeViewDebugDirectoryData($codeView[0]).Path -notmatch '^/_/') {
                        throw "Assembly contains a machine-specific PDB path: $($assembly.FullName)"
                    }
                }
            }
            finally {
                if ($null -ne $pe) { $pe.Dispose() }
                if ($null -ne $provider) { $provider.Dispose() }
                $assemblyStream.Dispose()
                $pdbStream.Dispose()
            }
        }
        Write-Host "Verified $($manifest.package.metadata.id) $($manifest.package.metadata.version): XML, matching portable PDBs, Source Link and deterministic builds."
    }
    finally {
        if ($null -ne $symbols) { $symbols.Dispose() }
        $package.Dispose()
    }
}
