# Script de Geracao de Pacotes de Distribuicao do ManyControl (Windows e Android)

$distDir = "$PSScriptRoot\dist"
if (!(Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir | Out-Null }

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "   ManyControl - Gerador de Pacotes      " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# 1. Windows Installer
Write-Host "`n[1/2] Compilando versao para Windows..." -ForegroundColor Cyan
dotnet publish "$PSScriptRoot\ManyControl\ManyControl.csproj" -f net10.0-windows10.0.19041.0 -c Release -p:WindowsPackageType=None

$isccCandidates = @(
    (Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if ($iscc) {
    Write-Host "Criando instalador ManyControl_Setup.exe..." -ForegroundColor Cyan
    & $iscc "$PSScriptRoot\installer.iss"
}

# 2. Android APK
Write-Host "`n[2/2] Compilando pacote Android APK..." -ForegroundColor Cyan
$env:ANDROID_HOME = "E:\VSComponents\Android\android-sdk"
$env:JAVA_HOME = "E:\VSComponents\Android\openjdk\jdk-21.0.8"
$env:PATH = "E:\VSComponents\Android\openjdk\jdk-21.0.8\bin;$env:ANDROID_HOME\platform-tools;$env:PATH"

dotnet publish "$PSScriptRoot\ManyControl\ManyControl.csproj" -f net10.0-android -c Release -p:AndroidSdkDirectory="E:\VSComponents\Android\android-sdk" -p:JavaSdkDirectory="E:\VSComponents\Android\openjdk\jdk-21.0.8" -p:AndroidKeyStore=false

$sourceApk = "$PSScriptRoot\ManyControl\bin\Release\net10.0-android\publish\com.companyname.manycontrol-Signed.apk"
if (Test-Path $sourceApk) {
    Copy-Item -Path $sourceApk -Destination "$distDir\ManyControl.apk" -Force
}

Write-Host "`n=========================================" -ForegroundColor Green
Write-Host "   PACOTES GERADOS COM SUCESSO!          " -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host "1. Instalador Windows: $distDir\ManyControl_Setup.exe" -ForegroundColor Yellow
Write-Host "2. Aplicativo Android:  $distDir\ManyControl.apk" -ForegroundColor Yellow
