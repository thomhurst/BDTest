[CmdletBinding()]
param(
    [string] $PackageDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/packages'),
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

$expectedIds = @($projects | ForEach-Object { $_.BaseName })
$verifiedIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$packages = @(Get-ChildItem $PackageDirectory -Filter '*.nupkg' -File |
    Where-Object { $_.Name -notlike '*.symbols.nupkg' } | Sort-Object Name)
if ($packages.Count -ne $projects.Count) {
    throw "Expected $($projects.Count) primary packages in $PackageDirectory; found $($packages.Count)."
}

foreach ($packageFile in $packages) {
    $packagePath = $packageFile.FullName
    $symbolsPath = [System.IO.Path]::ChangeExtension($packagePath, '.snupkg')
    $package = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    $symbols = $null
    try {
        $manifestName = @($package.Entries | Where-Object { $_.FullName.EndsWith('.nuspec') })[0].FullName
        $manifest = Read-PackageXml $package $manifestName
        $packageId = [string] $manifest.package.metadata.id
        if ($packageId -notin $expectedIds -or -not $verifiedIds.Add($packageId)) {
            throw "Unexpected or duplicate package ID: $packageId"
        }

        $metadata = $manifest.package.metadata
        foreach ($field in @('title', 'authors', 'description', 'copyright', 'projectUrl', 'tags')) {
            if ([string]::IsNullOrWhiteSpace([string] $metadata.$field)) {
                throw "Missing package metadata ${field}: $packageId"
            }
        }
        if ($metadata.license.type -ne 'expression' -or $metadata.license.InnerText -ne 'Apache-2.0') {
            throw "Missing Apache-2.0 license expression: $packageId"
        }
        if ($metadata.icon -ne 'nuget_icon.png' -or @($package.Entries | Where-Object FullName -EQ 'nuget_icon.png').Count -ne 1) {
            throw "Missing or duplicate package icon: $packageId"
        }
        if ($metadata.repository.type -ne 'git' -or $metadata.repository.url -ne 'https://github.com/thomhurst/BDTest') {
            throw "Missing repository metadata: $packageId"
        }

        # Shipping projects use literal versions and share the release version for project references.
        $project = $projects | Where-Object BaseName -EQ $packageId
        $projectXml = [xml] (Get-Content $project.FullName -Raw)
        $expectedDependencies = @{}
        foreach ($reference in $projectXml.SelectNodes('//PackageReference[not(@PrivateAssets="all")]')) {
            $expectedDependencies[$reference.Include] = $reference.Version
        }
        foreach ($reference in $projectXml.SelectNodes('//ProjectReference')) {
            $expectedDependencies[[System.IO.Path]::GetFileNameWithoutExtension($reference.Include.Replace('\', '/'))] = [string] $metadata.version
        }
        $dependencies = @($manifest.SelectNodes('//*[local-name()="dependencies"]//*[local-name()="dependency"]'))
        foreach ($expected in $expectedDependencies.GetEnumerator()) {
            $dependency = @($dependencies | Where-Object id -EQ $expected.Key)
            if ($dependency.Count -ne 1 -or $dependency[0].version -ne $expected.Value) {
                throw "Package dependency mismatch for $($expected.Key): expected $($expected.Value) in $packageId"
            }
        }
        if ($dependencies.Count -ne $expectedDependencies.Count) {
            throw "Unexpected package dependencies: $packageId"
        }
        if (@($package.Entries | Where-Object { $_.FullName -match '^build/.*\.dll$' }).Count -ne 0) {
            throw "Redundant build-folder assembly: $packageId"
        }

        if ($projectXml.SelectSingleNode('//IsRoslynComponent')?.InnerText -eq 'true') {
            $analyzerPath = "analyzers/dotnet/cs/$packageId.dll"
            $analyzers = @($package.Entries | Where-Object { $_.FullName.EndsWith('.dll') })
            if ($analyzers.Count -ne 1 -or $analyzers[0].FullName -ne $analyzerPath) {
                throw "Expected only $analyzerPath in analyzer package: $packageId"
            }
            Write-Host "Verified $packageId $($metadata.version): metadata, dependencies and analyzer-only contents."
            continue
        }

        $symbols = [System.IO.Compression.ZipFile]::OpenRead($symbolsPath)
        $symbolManifest = Read-PackageXml $symbols $manifestName
        if ($manifest.package.metadata.id -ne $symbolManifest.package.metadata.id -or
            $manifest.package.metadata.version -ne $symbolManifest.package.metadata.version) {
            throw "Package/symbol identity mismatch: $packagePath"
        }

        $assemblies = @($package.Entries | Where-Object { $_.FullName -match '^lib/.*\.dll$' })
        if ($assemblies.Count -eq 0) { throw "No library assemblies in $packagePath" }
        foreach ($assembly in $assemblies) {
            $xmlPath = [System.IO.Path]::ChangeExtension($assembly.FullName, '.xml')
            $documentation = Read-PackageXml $package $xmlPath
            if ($documentation.doc.assembly.name -ne [System.IO.Path]::GetFileNameWithoutExtension($assembly.Name)) {
                throw "XML documentation assembly mismatch: $xmlPath"
            }
        }

        foreach ($assembly in $assemblies) {
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
        Write-Host "Verified $($manifest.package.metadata.id) $($manifest.package.metadata.version): metadata, dependencies, library contents, XML, matching portable PDBs, Source Link and deterministic builds."
    }
    finally {
        if ($null -ne $symbols) { $symbols.Dispose() }
        $package.Dispose()
    }
}
