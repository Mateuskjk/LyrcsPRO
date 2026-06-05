# ─────────────────────────────────────────────────────────────────
#  LyricsPro – Build completo + Geração de Instalador
#  Executa: .\build-installer.ps1
# ─────────────────────────────────────────────────────────────────
$ErrorActionPreference = "Stop"
$Root    = $PSScriptRoot
$SrcDir  = "$Root\src\LyricsPro"
$PubDir  = "$Root\publish\win-x64"
$ToolDir = "$Root\tools\GenerateIcon"
$InsDir  = "$Root\installer"

Write-Host ""
Write-Host "╔══════════════════════════════════════════════╗" -ForegroundColor DarkYellow
Write-Host "║        LyricsPro  –  Build & Installer       ║" -ForegroundColor DarkYellow
Write-Host "╚══════════════════════════════════════════════╝" -ForegroundColor DarkYellow
Write-Host ""

# ── 1. Gerar ícones ──────────────────────────────────────────────
Write-Host "[1/4] Gerando logo.png e logo.ico ..." -ForegroundColor Cyan
Push-Location $ToolDir
try {
    dotnet run -- "$SrcDir\Assets\logo.svg" "$SrcDir\Assets"
    if ($LASTEXITCODE -ne 0) { throw "Falha ao gerar ícones" }
} finally { Pop-Location }

# ── 2. Atualizar csproj com ApplicationIcon ──────────────────────
Write-Host "[2/4] Configurando ApplicationIcon no .csproj ..." -ForegroundColor Cyan
$csproj = "$SrcDir\LyricsPro.csproj"
$content = Get-Content $csproj -Raw
if ($content -notmatch 'ApplicationIcon') {
    $content = $content -replace '<RootNamespace>LyricsPro</RootNamespace>',
        "<RootNamespace>LyricsPro</RootNamespace>`n    <ApplicationIcon>Assets\logo.ico</ApplicationIcon>"
    Set-Content $csproj $content -Encoding utf8
    Write-Host "  ApplicationIcon adicionado ao .csproj" -ForegroundColor Green
} else {
    Write-Host "  ApplicationIcon ja configurado" -ForegroundColor Gray
}

# ── 3. dotnet publish (self-contained win-x64) ───────────────────
Write-Host "[3/4] Publicando app (self-contained, win-x64) ..." -ForegroundColor Cyan
if (Test-Path $PubDir) { Remove-Item $PubDir -Recurse -Force }

dotnet publish $SrcDir `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishReadyToRun=true `
    -p:PublishTrimmed=false `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    --output $PubDir

if ($LASTEXITCODE -ne 0) { throw "Falha no dotnet publish" }

$exeSize = [math]::Round((Get-ChildItem $PubDir -Recurse | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host "  Publicado em: $PubDir ($exeSize MB)" -ForegroundColor Green

# ── 4. Compilar instalador com Inno Setup ───────────────────────
Write-Host "[4/4] Compilando instalador com Inno Setup ..." -ForegroundColor Cyan

# Locais comuns do Inno Setup
$iscc = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

New-Item -ItemType Directory -Force -Path "$InsDir\output" | Out-Null

if ($iscc) {
    & $iscc "$InsDir\LyricsPro.iss"
    if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar instalador" }
    $installer = Get-ChildItem "$InsDir\output\*.exe" | Select-Object -Last 1
    Write-Host ""
    Write-Host "✅  Instalador gerado:" -ForegroundColor Green
    Write-Host "    $($installer.FullName)" -ForegroundColor White
    Write-Host "    Tamanho: $([math]::Round($installer.Length/1MB,1)) MB" -ForegroundColor Gray
} else {
    Write-Host ""
    Write-Host "⚠  Inno Setup nao encontrado." -ForegroundColor Yellow
    Write-Host "   Baixe em: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    Write-Host "   Depois abra: $InsDir\LyricsPro.iss" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "✅  Publish pronto em: $PubDir" -ForegroundColor Green
}

Write-Host ""
Write-Host "══════════════════════════════════════════════" -ForegroundColor DarkYellow
Write-Host "  Concluido!" -ForegroundColor DarkYellow
Write-Host "══════════════════════════════════════════════" -ForegroundColor DarkYellow
Write-Host ""
