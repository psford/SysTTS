# SysTTS Model Download Script
# Downloads Piper voice model and espeak-ng-data for text-to-speech synthesis
# Run: powershell.exe -File scripts/download-models.ps1

param(
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = "Stop"

# Define paths
$VoicesDir = Join-Path $ProjectRoot "voices"
$EspeakDataDir = Join-Path $ProjectRoot "espeak-ng-data"
$TempDir = Join-Path $ProjectRoot "temp"

# Define URLs
$PiperVoiceBaseUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/amy/medium"
$PiperModelFile = "en_US-amy-medium.onnx"
$PiperConfigFile = "en_US-amy-medium.onnx.json"
$EspeakUrl = "https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/espeak-ng-data.tar.bz2"

# Ensure directories exist
Write-Host "Creating directories..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $VoicesDir -Force | Out-Null
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null

# Download Piper voice model ONNX file
Write-Host "Downloading Piper voice model: $PiperModelFile (~45MB)..." -ForegroundColor Cyan
$ModelUrl = "$PiperVoiceBaseUrl/$PiperModelFile"
$ModelPath = Join-Path $VoicesDir $PiperModelFile

try {
    if (Test-Path $ModelPath) {
        Write-Host "  File already exists: $ModelPath" -ForegroundColor Yellow
    } else {
        Invoke-WebRequest -Uri $ModelUrl -OutFile $ModelPath -UseBasicParsing
        Write-Host "  Downloaded: $ModelPath" -ForegroundColor Green
    }
} catch {
    Write-Error "Failed to download $PiperModelFile from $ModelUrl`nError: $_"
}

# Download Piper voice model config JSON file
Write-Host "Downloading Piper voice config: $PiperConfigFile..." -ForegroundColor Cyan
$ConfigUrl = "$PiperVoiceBaseUrl/$PiperConfigFile"
$ConfigPath = Join-Path $VoicesDir $PiperConfigFile

try {
    if (Test-Path $ConfigPath) {
        Write-Host "  File already exists: $ConfigPath" -ForegroundColor Yellow
    } else {
        Invoke-WebRequest -Uri $ConfigUrl -OutFile $ConfigPath -UseBasicParsing
        Write-Host "  Downloaded: $ConfigPath" -ForegroundColor Green
    }
} catch {
    Write-Error "Failed to download $PiperConfigFile from $ConfigUrl`nError: $_"
}

# Download espeak-ng-data
Write-Host "Downloading espeak-ng-data archive..." -ForegroundColor Cyan
$EspeakArchive = Join-Path $TempDir "espeak-ng-data.tar.bz2"

try {
    if (Test-Path $EspeakDataDir) {
        Write-Host "  espeak-ng-data directory already exists: $EspeakDataDir" -ForegroundColor Yellow
    } else {
        if (Test-Path $EspeakArchive) {
            Write-Host "  Archive already exists: $EspeakArchive" -ForegroundColor Yellow
        } else {
            Invoke-WebRequest -Uri $EspeakUrl -OutFile $EspeakArchive -UseBasicParsing
            Write-Host "  Downloaded: $EspeakArchive" -ForegroundColor Green
        }

        # Extract tar.bz2 to project root
        Write-Host "Extracting espeak-ng-data.tar.bz2..." -ForegroundColor Cyan
        tar -xf $EspeakArchive -C $ProjectRoot
        Write-Host "  Extracted to: $EspeakDataDir" -ForegroundColor Green
    }
} catch {
    Write-Error "Failed to download or extract espeak-ng-data`nError: $_"
}

# Cleanup temp directory
Write-Host "Cleaning up temporary files..." -ForegroundColor Cyan
Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue

# Verify downloads
Write-Host "`nVerifying downloads..." -ForegroundColor Cyan
$success = $true

if (Test-Path $ModelPath) {
    $fileSize = (Get-Item $ModelPath).Length / 1MB
    Write-Host ("  [OK] " + $PiperModelFile + " ({0:F2} MB)" -f $fileSize) -ForegroundColor Green
} else {
    Write-Host ("  [FAIL] " + $PiperModelFile + " NOT FOUND") -ForegroundColor Red
    $success = $false
}

if (Test-Path $ConfigPath) {
    Write-Host ("  [OK] " + $PiperConfigFile) -ForegroundColor Green
} else {
    Write-Host ("  [FAIL] " + $PiperConfigFile + " NOT FOUND") -ForegroundColor Red
    $success = $false
}

if (Test-Path $EspeakDataDir) {
    Write-Host "  [OK] espeak-ng-data directory" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] espeak-ng-data directory NOT FOUND" -ForegroundColor Red
    $success = $false
}

if ($success) {
    Write-Host "`nAll downloads completed successfully!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`nSome downloads failed. Please check the errors above." -ForegroundColor Red
    exit 1
}
