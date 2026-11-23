param(
    [string]$DotNet = 'dotnet',
    [string]$InnoSetupCompiler,
    [ValidateSet('Patch', 'Minor', 'Major')]
    [string]$Bump = 'Patch'
)

$ErrorActionPreference = 'Stop'
if (-not $InnoSetupCompiler) {
    $compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    $InnoSetupCompiler = $compiler.Source
}
if (-not $InnoSetupCompiler) {
    $programDirectories = @($env:ProgramFiles, [Environment]::GetFolderPath('ProgramFilesX86'),
        (Join-Path $env:LOCALAPPDATA 'Programs'))
    $candidates = foreach ($version in 'Inno Setup 7', 'Inno Setup 6') {
        foreach ($directory in ($programDirectories | Select-Object -Unique)) {
            $candidate = Join-Path $directory "$version\ISCC.exe"
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                $candidate
            }
        }
    }
    $InnoSetupCompiler = $candidates | Select-Object -First 1
}
if (-not $InnoSetupCompiler) {
    throw 'Install Inno Setup or pass -InnoSetupCompiler with the path to ISCC.exe.'
}

$project = Join-Path $PSScriptRoot 'src\SymfonyIdeIntegration\SymfonyIdeIntegration.csproj'
$installer = Join-Path $PSScriptRoot 'installer\SymfonyIdeIntegration.iss'
$versionPath = Join-Path $PSScriptRoot 'VERSION'
$artifacts = (New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'artifacts') -Force).FullName
try {
    $buildLock = [IO.File]::Open((Join-Path $artifacts 'installer.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
} catch [IO.IOException] {
    throw "Cannot acquire the installer build lock in '$artifacts'. $($_.Exception.Message)"
}

$staging = $null
try {
    $current = [IO.File]::ReadAllText($versionPath).Trim()
    if ($current -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
        throw "VERSION must contain a stable Semantic Version in MAJOR.MINOR.PATCH format."
    }
    $parts = @([long]$Matches[1], [long]$Matches[2], [long]$Matches[3])
    switch ($Bump) {
        'Major' { $parts[0]++; $parts[1] = 0; $parts[2] = 0 }
        'Minor' { $parts[1]++; $parts[2] = 0 }
        'Patch' { $parts[2]++ }
    }
    if ($parts | Where-Object { $_ -gt [uint16]::MaxValue }) {
        throw 'Each version component must fit in the Windows version resource (0 to 65535).'
    }
    $version = $parts -join '.'
    $packageName = "SymfonyIdeIntegration-$version-Setup-x64.exe"
    $packagePath = Join-Path $artifacts $packageName
    if (Test-Path -LiteralPath $packagePath) {
        throw "Installer '$packagePath' already exists."
    }

    $staging = (New-Item -ItemType Directory -Path (Join-Path $artifacts "installer-$([guid]::NewGuid())")).FullName
    $publish = Join-Path $staging 'publish'
    & $DotNet publish $project -c Release -r win-x64 --self-contained true "-p:Version=$version" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o $publish
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    & $InnoSetupCompiler "/DPublishDir=$publish" "/O$staging" $installer
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }

    $pendingVersion = Join-Path $staging 'VERSION'
    [IO.File]::WriteAllText($pendingVersion, $version + [char]10)
    [IO.File]::Move((Join-Path $staging $packageName), $packagePath)
    try {
        [IO.File]::Replace($pendingVersion, $versionPath, [NullString]::Value)
    } catch {
        [IO.File]::Delete($packagePath)
        throw
    }
    Write-Output "Built $packagePath"
} finally {
    try {
        if ($staging -and (Split-Path -Parent $staging) -eq $artifacts) {
            Remove-Item -LiteralPath $staging -Recurse -Force
        }
    } finally {
        $buildLock.Dispose()
    }
}
