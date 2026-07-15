# VTStudioToolBox Build Script
# Usage: powershell -ExecutionPolicy Bypass -File build.ps1
# Output: Desktop\VTStudioToolBox_Releases\

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Parse version from cfg.cs
$csContent = Get-Content "cfg.cs" -Raw
if ($csContent -match 'AppVersion\s*=\s*"(.+?)"') {
    $raw = $Matches[1]
} else {
    Write-Host "[ERROR] Failed to parse version from cfg.cs" -ForegroundColor Red
    exit 1
}

# [Release] 1.1 (Build.2607141815) -> Release / 1.1(Build.2607141815)
if ($raw -match '\[(\w+)\]\s+(\S+)\s+\((.+?)\)') {
    $buildType  = $Matches[1]
    $version    = $Matches[2]
    $buildNum   = $Matches[3]
    $versionTag = "${version}(${buildNum})"
} else {
    Write-Host "[ERROR] Version format mismatch: $raw" -ForegroundColor Red
    exit 1
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host " VTStudioToolBox Build Script" -ForegroundColor Cyan
Write-Host " Version : $versionTag" -ForegroundColor Cyan
Write-Host " Build   : $buildType" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$desktop   = [Environment]::GetFolderPath("Desktop")
$outputDir = Join-Path $desktop "VTStudioToolBox_Releases"
if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

function Build-Release {
    param(
        [string]$Platform,
        [string]$Rid,
        [bool]$SingleFile
    )

    $tag      = if ($SingleFile) { "Portable" } else { "Release" }
    $platUp   = $Platform.ToUpper()
    $tempDir  = Join-Path $env:TEMP ("vtsbuild_{0}_{1}" -f $Platform, $tag)
    $outName  = "${buildType}_VTStudioToolBox_${versionTag}_${tag}_${platUp}"

    if ($SingleFile) {
        $artifact = Join-Path $outputDir "$outName.exe"

        Write-Host "[BUILD] $platUp Portable (SingleFile)..." -ForegroundColor Yellow
        dotnet publish -c Release `
            -p:Platform=$Platform `
            -p:PublishSingleFile=true `
            -p:SelfContained=true `
            -p:IncludeAllContentForSelfExtract=true `
            -p:RuntimeIdentifier=$Rid `
            -o $tempDir

        if ($LASTEXITCODE -ne 0) { Write-Host "[FAIL] $platUp Portable" -ForegroundColor Red; exit 1 }

        # Portable: copy single exe directly, no content directories
        if (Test-Path $artifact) { Remove-Item $artifact -Force }
        Copy-Item (Join-Path $tempDir "VTStudioToolBox.exe") $artifact -Force
    } else {
        $artifact = Join-Path $outputDir "$outName.zip"

        Write-Host "[BUILD] $platUp Release (Normal)..." -ForegroundColor Yellow
        dotnet publish -c Release `
            -p:Platform=$Platform `
            -p:SelfContained=true `
            -p:RuntimeIdentifier=$Rid `
            -o $tempDir

        if ($LASTEXITCODE -ne 0) { Write-Host "[FAIL] $platUp Release" -ForegroundColor Red; exit 1 }

        # Release: copy content directories
        $srcDir = "."
        $contentDirs = @("platform-tools", "Tools", "Strings", "Assets")
        foreach ($dir in $contentDirs) {
            $srcPath = Join-Path $srcDir $dir
            $dstPath = Join-Path $tempDir $dir
            if (Test-Path $srcPath) {
                if (-not (Test-Path $dstPath)) {
                    New-Item -ItemType Directory -Path $dstPath -Force | Out-Null
                }
                Copy-Item "$srcPath\*" $dstPath -Recurse -Force
            }
        }
        Copy-Item "EULA.html" $tempDir -Force -ErrorAction SilentlyContinue

        # Verify critical content files exist and have reasonable sizes
        $requiredFiles = @(
            "platform-tools\adb.exe",
            "Strings\zh-CN.json",
            "Assets\logo.png"
        )
        foreach ($file in $requiredFiles) {
            $fullPath = Join-Path $tempDir $file
            if (-not (Test-Path $fullPath)) {
                Write-Host "[WARN] Missing content: $file" -ForegroundColor Yellow
            } elseif ((Get-Item $fullPath).Length -lt 1000) {
                Write-Host "[WARN] Content file too small ($file): $((Get-Item $fullPath).Length) bytes" -ForegroundColor Yellow
            }
        }

        if (Test-Path $artifact) { Remove-Item $artifact -Force }
        Compress-Archive -Path (Join-Path $tempDir "*") -DestinationPath $artifact -Force
    }

    if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue }
    $sizeMB = [math]::Round((Get-Item $artifact).Length / 1MB, 1)
    Write-Host "[OK] $outName  ($sizeMB MB)" -ForegroundColor Green
}

Build-Release -Platform "x64"   -Rid "win-x64"   -SingleFile $true
Build-Release -Platform "ARM64" -Rid "win-arm64"  -SingleFile $true
Build-Release -Platform "x64"   -Rid "win-x64"   -SingleFile $false
Build-Release -Platform "ARM64" -Rid "win-arm64"  -SingleFile $false

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host " All builds complete!" -ForegroundColor Green
Write-Host " Output: $outputDir" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
