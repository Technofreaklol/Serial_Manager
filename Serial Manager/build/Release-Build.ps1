#Requires -Version 5.1
<#
    .SYNOPSIS
    Baut Serial Manager und verpackt es mit Inno Setup zu einem
    klassischen Windows-Installer (Setup.exe), der nach
    "C:\Program Files\SerialManager" installiert.

    .DESCRIPTION
    Führt folgende Schritte aus:
      1. dotnet publish (self-contained, win-x64, mehrere Dateien -
                          KEIN Single-File-EXE mehr)
      2. ISCC.exe (Inno Setup Compiler) -> erzeugt SerialManagerSetup.exe
                          (unter build/Releases)
      3. (optional) gh release create -> lädt SerialManagerSetup.exe
                          direkt als neues GitHub-Release hoch.

    Voraussetzungen (einmalig):
      - Inno Setup 6 (https://jrsoftware.org/isinfo.php) installiert
        (ISCC.exe im PATH oder im Standard-Installationsordner).
      - Für -Publish: GitHub-CLI ("gh", https://cli.github.com/)
        installiert und ein Token mit Schreibrecht auf das Repo.

    Hinweis zum Umstieg weg von Velopack: Velopack installierte bewusst
    PRO BENUTZER ohne Admin-Rechte unter %LocalAppData%, damit es sich
    selbst im Hintergrund aktualisieren konnte. Ein echtes
    "C:\Program Files"-Setup braucht dagegen zwingend Administrator-
    rechte - auch für Updates (siehe Services/UpdateService.cs). Das ist
    der bewusst in Kauf genommene Kompromiss für eine "normale"
    Installation wie bei den meisten anderen Windows-Anwendungen.

    .PARAMETER Version
    Die zu veröffentlichende Version, z. B. "1.1.0".
    Muss zu <Version> in der .csproj passen bzw. wird beim Publish
    per -p:Version an das Projekt übergeben.

    .PARAMETER Publish
    Wenn gesetzt, wird das Release zusätzlich per "gh release create"
    veröffentlicht (benötigt ein GitHub-Token mit Schreibrechten auf
    das Repo, siehe README.md).

    .PARAMETER SignParams
    Optional. signtool.exe-Parameter zum Signieren von SerialManagerSetup.exe
    (z. B. '/f "C:\certs\meincert.pfx" /p "Kennwort" /tr http://timestamp.digicert.com /td sha256 /fd sha256').
    Ohne diesen Parameter wird NICHT signiert -> Windows zeigt beim
    Ausführen eine "Unbekannter Herausgeber"-Warnung. Siehe README.md
    für Optionen/Kosten.

    .EXAMPLE
    ./build/Release-Build.ps1 -Version 1.1.0

    .EXAMPLE
    $env:GITHUB_TOKEN = "ghp_xxx"
    ./build/Release-Build.ps1 -Version 1.1.0 -Publish

    .EXAMPLE
    ./build/Release-Build.ps1 -Version 1.1.0 -SignParams '/f "C:\certs\cert.pfx" /p "Kennwort" /tr http://timestamp.digicert.com /td sha256 /fd sha256'
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [switch]$Publish,

    [string]$SignParams
)

$ErrorActionPreference = "Stop"

$repoRoot   = Split-Path -Parent $PSScriptRoot
$project    = Join-Path $repoRoot "Serial Manager\Serial Manager.csproj"
$publishDir = Join-Path $repoRoot "build\publish"
$releaseDir = Join-Path $repoRoot "build\Releases"
$issFile    = Join-Path $repoRoot "build\installer.iss"
$repoSlug   = "Technofreaklol/Serial_Manager"

Write-Host "==> Alte Build-Ordner werden bereinigt..." -ForegroundColor Cyan
Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $releaseDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "==> dotnet publish ($Version)..." -ForegroundColor Cyan
dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish ist fehlgeschlagen." }

Write-Host "==> Inno Setup Compiler (ISCC.exe) wird gesucht..." -ForegroundColor Cyan

$iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue

if (-not $iscc) {
    $candidatePaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )

    $foundPath = $candidatePaths | Where-Object { Test-Path $_ } | Select-Object -First 1

    if ($foundPath) {
        $iscc = Get-Item $foundPath
    }
}

if (-not $iscc) {
    throw "ISCC.exe (Inno Setup Compiler) wurde nicht gefunden. " +
          "Bitte Inno Setup 6 installieren: https://jrsoftware.org/isinfo.php"
}

Write-Host "==> Installer wird mit Inno Setup gebaut..." -ForegroundColor Cyan

New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

& $iscc.Source $issFile `
    "/DMyAppVersion=$Version" `
    "/DPublishDir=$publishDir" `
    "/DOutputDir=$releaseDir"

if ($LASTEXITCODE -ne 0) { throw "ISCC.exe (Inno Setup) ist fehlgeschlagen." }

$setupExe = Join-Path $releaseDir "SerialManagerSetup.exe"

if (-not (Test-Path $setupExe)) {
    throw "Der erwartete Installer '$setupExe' wurde nach dem Build nicht gefunden."
}

if ($SignParams) {
    Write-Host "==> Installer wird signiert..." -ForegroundColor Cyan

    $signtool = Get-Command "signtool.exe" -ErrorAction SilentlyContinue

    if (-not $signtool) {
        $signtool = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Windows Kits\10\bin" `
                        -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue |
                    Where-Object { $_.FullName -match "\\x64\\" } |
                    Select-Object -First 1
    }

    if (-not $signtool) {
        throw "signtool.exe wurde nicht gefunden (Windows SDK installiert?)."
    }

    $signtoolPath = if ($signtool -is [System.Management.Automation.CommandInfo]) { $signtool.Source } else { $signtool.FullName }

    # Start-Process mit EINEM Argument-String (statt eines aufgesplitteten
    # Arrays) verwendet, damit in $SignParams enthaltene, in Anführungszeichen
    # gesetzte Werte (Passwort, Pfad mit Leerzeichen) korrekt erhalten
    # bleiben - ein simples "-split ' '" würde diese Anführungszeichen
    # zerstören.
    $signProcess = Start-Process -FilePath $signtoolPath `
        -ArgumentList "sign $SignParams `"$setupExe`"" `
        -NoNewWindow -Wait -PassThru

    if ($signProcess.ExitCode -ne 0) { throw "signtool.exe (Signieren) ist fehlgeschlagen." }
}
else {
    Write-Host "Hinweis: Ohne -SignParams wird NICHT signiert - Windows zeigt beim" -ForegroundColor Yellow
    Write-Host "Ausführen eine 'Unbekannter Herausgeber'-Warnung an. Details siehe README.md." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "==> Fertig! Installer liegt unter:" -ForegroundColor Green
Write-Host "    $setupExe"

if ($Publish) {
    if (-not $env:GITHUB_TOKEN) {
        throw "Für -Publish muss die Umgebungsvariable GITHUB_TOKEN gesetzt sein " +
              "(GitHub Personal Access Token mit Schreibrecht auf das Repo)."
    }

    if (-not (Get-Command "gh" -ErrorAction SilentlyContinue)) {
        throw "Die GitHub-CLI ('gh') wurde nicht gefunden. Installation: https://cli.github.com/"
    }

    # Die GitHub-CLI liest GH_TOKEN (bevorzugt) bzw. GITHUB_TOKEN
    # automatisch für die Authentifizierung - kein separater "gh auth
    # login" nötig.
    $env:GH_TOKEN = $env:GITHUB_TOKEN

    Write-Host ""
    Write-Host "==> Release wird auf GitHub veröffentlicht..." -ForegroundColor Cyan

    gh release create "v$Version" $setupExe `
        --repo $repoSlug `
        --title "Serial Manager $Version" `
        --notes "Serial Manager $Version"

    if ($LASTEXITCODE -ne 0) { throw "gh release create ist fehlgeschlagen." }

    Write-Host "==> Release v$Version wurde veröffentlicht." -ForegroundColor Green
}
else {
    Write-Host ""
    Write-Host "Hinweis: Zum Veröffentlichen entweder -Publish verwenden," -ForegroundColor Yellow
    Write-Host "oder '$setupExe' manuell an ein neues GitHub-Release" -ForegroundColor Yellow
    Write-Host "(Tag v$Version) anhängen." -ForegroundColor Yellow
}