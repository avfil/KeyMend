[CmdletBinding()]
param(
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$project = Join-Path $root 'src\KeyMend\KeyMend.csproj'
$publishDir = Join-Path $root 'artifacts\publish'
$installerDir = Join-Path $root 'artifacts\installer'
$setupSource = Join-Path $root 'installer\KeyMend.iss'
$exePath = Join-Path $publishDir 'KeyMend.exe'
$zipPath = Join-Path $installerDir 'KeyMend-win-x64.zip'

foreach ($directory in @($publishDir, $installerDir)) {
    if (Test-Path $directory) {
        Remove-Item $directory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $directory | Out-Null
}

& dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    --output $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path $exePath)) {
    throw "Publish did not produce $exePath"
}

Compress-Archive -LiteralPath $exePath -DestinationPath $zipPath -Force

if ($SkipInstaller) {
    Write-Host "Portable archive: $zipPath"
    return
}

$iscc = Get-Command iscc.exe -ErrorAction SilentlyContinue
if (-not $iscc) {
    $isccCandidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    )
    foreach ($candidate in $isccCandidates) {
        if (Test-Path $candidate) {
            $iscc = Get-Command $candidate
            break
        }
    }
}

if (-not $iscc) {
    throw 'Inno Setup compiler (ISCC.exe) was not found. Install Inno Setup or rerun with -SkipInstaller.'
}

$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath)
$productVersion = ($versionInfo.ProductVersion -split '\+')[0]
if ([string]::IsNullOrWhiteSpace($productVersion)) {
    $productVersion = ($versionInfo.FileVersion -split '\+')[0]
}
$versionInfoVersion = '{0}.{1}.{2}.{3}' -f $versionInfo.FileMajorPart, $versionInfo.FileMinorPart, $versionInfo.FileBuildPart, $versionInfo.FilePrivatePart

& $iscc.Source "/DAppVersion=$productVersion" "/DVersionInfoVersion=$versionInfoVersion" "/DPublishDir=$publishDir" "/DArtifactsDir=$installerDir" $setupSource
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE"
}

Write-Host "Portable archive: $zipPath"
Write-Host "Installer: $(Join-Path $installerDir 'KeyMend-Setup.exe')"
