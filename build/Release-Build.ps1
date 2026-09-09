#Requires -Version 5.1
<#
    .SYNOPSIS
    Baut Serial Manager, verpackt es mit Velopack (vpk) zu einem
    Windows-Installer und erzeugt die Update-Pakete.

    .DESCRIPTION
    Führt folgende Schritte aus:
      1. dotnet publish (self-contained, win-x64, Single-File-EXE)
      2. vpk pack        -> erzeugt Setup.exe + Release-Dateien
                             (unter build/Releases)
      3. (optional) vpk upload github -> lädt die Release-Dateien
                             direkt in die GitHub Releases des Repos hoch.

    Voraussetzungen (einmalig):
      dotnet tool install -g vpk

    .PARAMETER Version
    Die zu veröffentlichende Version, z. B. "1.1.0".
    Muss zu <Version> in der .csproj passen bzw. wird beim Publish
    per -p:Version an das Projekt übergeben.

    .PARAMETER Publish
    Wenn gesetzt, wird das Release zusätzlich per "vpk upload github"
    veröffentlicht (benötigt ein GitHub-Token mit Schreibrechten auf
    das Repo, siehe README.md).

    .EXAMPLE
    ./build/Release-Build.ps1 -Version 1.1.0

    .EXAMPLE
    $env:GITHUB_TOKEN = "ghp_xxx"
    ./build/Release-Build.ps1 -Version 1.1.0 -Publish
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [switch]$Publish
)

$ErrorActionPreference = "Stop"

$repoRoot   = Split-Path -Parent $PSScriptRoot
$project    = Join-Path $repoRoot "Serial Manager\Serial Manager.csproj"
$publishDir = Join-Path $repoRoot "build\publish"
$releaseDir = Join-Path $repoRoot "build\Releases"
$iconPath   = Join-Path $repoRoot "Serial Manager\Assets\Logo.ico"
$repoUrl    = "https://github.com/Technofreaklol/Serial_Manager"

Write-Host "==> Alte Build-Ordner werden bereinigt..." -ForegroundColor Cyan
Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "==> dotnet publish ($Version)..." -ForegroundColor Cyan
dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish ist fehlgeschlagen." }

Write-Host "==> Vorherige Releases laden (für Delta-Updates)..." -ForegroundColor Cyan
vpk download github --repoUrl $repoUrl -o $releaseDir
# Beim allerersten Release gibt es noch nichts zum Laden - das ist kein
# Fehler, deshalb wird der Exit-Code hier bewusst ignoriert.

Write-Host "==> vpk pack..." -ForegroundColor Cyan
vpk pack `
    --packId "SerialManager" `
    --packVersion $Version `
    --packDir $publishDir `
    --mainExe "Serial Manager.exe" `
    --packTitle "Serial Manager" `
    --packAuthors "Nikolai Hieber" `
    --icon $iconPath `
    -o $releaseDir

if ($LASTEXITCODE -ne 0) { throw "vpk pack ist fehlgeschlagen." }

Write-Host ""
Write-Host "==> Fertig! Installer & Update-Pakete liegen in:" -ForegroundColor Green
Write-Host "    $releaseDir"

if ($Publish) {
    if (-not $env:GITHUB_TOKEN) {
        throw "Für -Publish muss die Umgebungsvariable GITHUB_TOKEN gesetzt sein " +
              "(GitHub Personal Access Token mit Schreibrecht auf das Repo)."
    }

    Write-Host ""
    Write-Host "==> Release wird auf GitHub veröffentlicht..." -ForegroundColor Cyan

    vpk upload github `
        --repoUrl $repoUrl `
        --token $env:GITHUB_TOKEN `
        --publish `
        --releaseName "Serial Manager $Version" `
        --tag "v$Version" `
        -o $releaseDir

    if ($LASTEXITCODE -ne 0) { throw "vpk upload github ist fehlgeschlagen." }

    Write-Host "==> Release v$Version wurde veröffentlicht." -ForegroundColor Green
}
else {
    Write-Host ""
    Write-Host "Hinweis: Zum Veröffentlichen entweder -Publish verwenden," -ForegroundColor Yellow
    Write-Host "oder die Dateien aus '$releaseDir' manuell an ein neues" -ForegroundColor Yellow
    Write-Host "GitHub-Release (Tag v$Version) anhängen." -ForegroundColor Yellow
}
