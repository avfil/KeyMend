[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\src\KeyMend\Assets')
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'generate-icons\IconGenerator.csproj'
& dotnet run --project $project --configuration Release -- $OutputDir
if ($LASTEXITCODE -ne 0) {
    throw "Icon generation failed with exit code $LASTEXITCODE"
}
