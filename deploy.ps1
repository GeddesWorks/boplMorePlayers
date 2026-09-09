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

# ── Read current version + name ──
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$modName = $manifest.name
$parts = $manifest.version_number -split '\.'
$major = [int]$parts[0]; $minor = [int]$parts[1]; $patch = [int]$parts[2]

# ── Bump ──
switch ($BumpType) {
    "major" { $major++; $minor = 0; $patch = 0 }
    "minor" { $minor++; $patch = 0 }
    "patch" { $patch++ }
}
$newVersion = "$major.$minor.$patch"
Write-Host "Version: $($manifest.version_number) -> $newVersion ($modName)" -ForegroundColor Cyan

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

$zipPath = Join-Path $projectDir "thunderstore\$modName-$newVersion.zip"
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

$baseUrl = "https://thunderstore.io/api/experimental"
$headers = @{ Authorization = "Bearer $Token" }
$fileSize = (Get-Item $zipPath).Length
$fileName = Split-Path $zipPath -Leaf

# Step 1: Initiate upload
Write-Host "  Initiating upload ($fileName, $fileSize bytes)..." -ForegroundColor Gray
$initBody = @{ filename = $fileName; file_size_bytes = $fileSize } | ConvertTo-Json
$initResp = Invoke-RestMethod -Uri "$baseUrl/usermedia/initiate-upload/" `
    -Method Post -Headers $headers -ContentType "application/json" -Body $initBody

$uuid = $initResp.user_media.uuid
$uploadUrls = $initResp.upload_urls
Write-Host "  Upload UUID: $uuid ($($uploadUrls.Count) part(s))" -ForegroundColor Gray

# Step 2: Upload file parts to presigned S3 URLs
$fileBytes = [System.IO.File]::ReadAllBytes($zipPath)
$parts = @()
foreach ($part in $uploadUrls) {
    $partNum = $part.part_number
    $offset  = [int]$part.offset
    $length  = [int]$part.length
    $url     = $part.url

    Write-Host "  Uploading part $partNum ($length bytes)..." -ForegroundColor Gray
    $chunk = New-Object byte[] $length
    [Array]::Copy($fileBytes, $offset, $chunk, 0, $length)

    $resp = Invoke-WebRequest -Uri $url -Method Put -Body $chunk `
        -ContentType "application/octet-stream" -UseBasicParsing
    $etag = $resp.Headers["ETag"]
    $parts += @{ ETag = $etag; PartNumber = $partNum }
}

# Step 3: Finish upload
Write-Host "  Finishing upload..." -ForegroundColor Gray
$finishBody = @{ parts = $parts } | ConvertTo-Json -Depth 5
Invoke-RestMethod -Uri "$baseUrl/usermedia/$uuid/finish-upload/" `
    -Method Post -Headers $headers -ContentType "application/json" -Body $finishBody | Out-Null

# Step 4: Submit package
Write-Host "  Submitting package..." -ForegroundColor Gray
$submitBody = @{
    author_name      = $Team
    upload_uuid      = $uuid
    communities      = @("bopl-battle")
    has_nsfw_content = $false
    categories       = @()
} | ConvertTo-Json -Depth 5

$submitResp = Invoke-RestMethod -Uri "$baseUrl/submission/submit/" `
    -Method Post -Headers $headers -ContentType "application/json" -Body $submitBody

Write-Host "`nUploaded v$newVersion!" -ForegroundColor Green
$pkgUrl = "https://thunderstore.io/c/bopl-battle/p/$Team/$modName/v$newVersion/"
Write-Host "Package: $pkgUrl" -ForegroundColor Cyan
