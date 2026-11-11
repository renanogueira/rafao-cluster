$ErrorActionPreference = 'Stop'

$Project = Join-Path $PSScriptRoot 'GpuClusterPilot.csproj'
$Output = Join-Path $PSScriptRoot '..\outputs\RafaoCluster-win-x64'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 8 SDK não encontrado. Instale com: winget install Microsoft.DotNet.SDK.8'
}

dotnet restore $Project
dotnet publish $Project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $Output

Write-Host "`nAplicativo gerado em: $Output\RafaoCluster.exe" -ForegroundColor Green
