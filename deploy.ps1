<#
.SYNOPSIS
  Build, bump version, and upload to Thunderstore in one step.

.EXAMPLE
  # First time — set env vars once so you never pass them again:
  [System.Environment]::SetEnvironmentVariable("THUNDERSTORE_TOKEN", "tss_xxx", "User")
  [System.Environment]::SetEnvironmentVariable("THUNDERSTORE_TEAM",  "yourteam", "User")
  # (restart terminal after setting these)

  .\deploy.ps1                # bump patch, build, upload
  .\deploy.ps1 -BumpType minor
  .\deploy.ps1 -SkipUpload    # just build the zip
#>
param(
    [ValidateSet("patch","minor","major")]
    [string]$BumpType = "patch",

    [string]$Token = $env:THUNDERSTORE_TOKEN,
    [string]$Team  = $env:THUNDERSTORE_TEAM,
    [switch]$SkipUpload,

    [string]$BoplBattleRootDir = $(
        if ($env:BOPL_BATTLE_ROOT) { $env:BOPL_BATTLE_ROOT }
        else { "C:\Program Files (x86)\Steam\steamapps\common\Bopl Battle" }
    ),
    [string]$BoplProfileDir = $(
        if ($env:BOPL_PROFILE_DIR) { $env:BOPL_PROFILE_DIR }
        else { "C:\Users\colli\AppData\Roaming\Thunderstore Mod Manager\DataFolder\BoplBattle\profiles\test my mods" }
    )
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectDir   = $PSScriptRoot
$csproj       = Join-Path $projectDir "BoplMorePlayersLocal8.csproj"
$manifestPath = Join-Path $projectDir "thunderstore\manifest.json"

# ── Read current version ──
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$parts = $manifest.version_number -split '\.'
$major = [int]$parts[0]; $minor = [int]$parts[1]; $patch = [int]$parts[2]

# ── Bump ──
switch ($BumpType) {
    "major" { $major++; $minor = 0; $patch = 0 }
    "minor" { $minor++; $patch = 0 }
    "patch" { $patch++ }
}
$newVersion = "$major.$minor.$patch"
Write-Host "Version: $($manifest.version_number) -> $newVersion" -ForegroundColor Cyan

# ── Update manifest.json ──
$manifest.version_number = $newVersion
$manifest | ConvertTo-Json -Depth 10 | Set-Content $manifestPath -Encoding utf8

# ── Update csproj <Version> to match ──
$csprojXml = [xml](Get-Content $csproj -Raw)
$versionNode = $csprojXml.SelectSingleNode("//Version")
if ($versionNode) { $versionNode.InnerText = $newVersion }
$csprojXml.Save($csproj)

# ── Build + pack ──
Write-Host "Building and packing v$newVersion..." -ForegroundColor Cyan
& dotnet build $csproj -t:PackThunderstore `
    "-p:BoplBattleRootDir=$BoplBattleRootDir" `
    "-p:BoplProfileDir=$BoplProfileDir" `
    -nologo -v minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

$zipPath = Join-Path $projectDir "thunderstore\BoplExpLocal_TestBuild-$newVersion.zip"
if (-not (Test-Path $zipPath)) {
    Write-Host "Expected zip not found: $zipPath" -ForegroundColor Red
    exit 1
}
Write-Host "Package: $zipPath" -ForegroundColor Green

# ── Upload ──
if ($SkipUpload) {
    Write-Host "Done (skipped upload)." -ForegroundColor Yellow
    exit 0
}
if (-not $Token) {
    Write-Host "No THUNDERSTORE_TOKEN set. Upload manually at https://thunderstore.io/c/bopl-battle/create/" -ForegroundColor Yellow
    exit 0
}
if (-not $Team) {
    Write-Host "No THUNDERSTORE_TEAM set. Upload manually at https://thunderstore.io/c/bopl-battle/create/" -ForegroundColor Yellow
    exit 0
}

Write-Host "Uploading v$newVersion to Thunderstore (team: $Team)..." -ForegroundColor Cyan

$metadataJson = "{`"author_name`":`"$Team`",`"categories`":[],`"communities`":[`"bopl-battle`"],`"has_nsfw_content`":false}"

# Use curl.exe (ships with Windows 10+) for reliable binary multipart upload
& curl.exe -s -S -f `
    -X POST "https://thunderstore.io/api/experimental/submission/submit/" `
    -H "Authorization: Bearer $Token" `
    -F "metadata=$metadataJson;type=application/json" `
    -F "file=@$zipPath;type=application/zip"

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nUpload failed. You can upload manually at https://thunderstore.io/c/bopl-battle/create/" -ForegroundColor Red
    exit 1
}

Write-Host "`nUploaded v$newVersion!" -ForegroundColor Green
