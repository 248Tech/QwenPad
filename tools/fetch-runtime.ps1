# Downloads the pinned llama.cpp build (CUDA 12.4, x64) into runtime\llama, and the pinned
# stable-diffusion.cpp build (CUDA 12, x64) into runtime\sd (for image generation).
# The CUDA builds also contain the CPU backends, so they work on PCs without an NVIDIA GPU.
# sd-server reuses the CUDA runtime DLLs (cudart/cublas) from runtime\llama, so sd's cudart zip isn't needed.
param([string]$Build = 'b11756', [string]$SdBuild = 'master-990-530fa39')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dest = Join-Path $root 'runtime\llama'
$tmp = Join-Path $env:TEMP "qwenpad-runtime-$Build"
New-Item -ItemType Directory -Force $dest, $tmp | Out-Null
$base = "https://github.com/ggml-org/llama.cpp/releases/download/$Build"
foreach ($zip in "llama-$Build-bin-win-cuda-12.4-x64.zip", 'cudart-llama-bin-win-cuda-12.4-x64.zip') {
    $out = Join-Path $tmp $zip
    if (-not (Test-Path $out)) { Write-Host "Downloading $zip"; curl.exe -L --fail -o $out "$base/$zip"; if ($LASTEXITCODE) { throw "Download failed: $zip" } }
    Expand-Archive -Force $out $dest
}
Write-Host "llama.cpp $Build ready in $dest"

# stable-diffusion.cpp (image generation)
$sdDest = Join-Path $root 'runtime\sd'
$sdCommit = ($SdBuild -split '-')[-1]
$sdZip = "sd-$SdBuild-bin-win-cuda12-x64.zip"
$sdOut = Join-Path $tmp $sdZip
New-Item -ItemType Directory -Force $sdDest | Out-Null
if (-not (Test-Path $sdOut)) { Write-Host "Downloading $sdZip"; curl.exe -L --fail -o $sdOut "https://github.com/leejet/stable-diffusion.cpp/releases/download/$SdBuild/$sdZip"; if ($LASTEXITCODE) { throw "Download failed: $sdZip" } }
Expand-Archive -Force $sdOut $sdDest
Write-Host "stable-diffusion.cpp $SdBuild ready in $sdDest"

# CCTV notes: mediamtx + ffmpeg (LGPL build), and RTSPanda built from source (needs Go and Node.js).
$mtxVer = 'v1.21.3'
$rtDest = Join-Path $root 'runtime\rtspanda'
New-Item -ItemType Directory -Force (Join-Path $rtDest 'mediamtx'), (Join-Path $root 'runtime\ffmpeg') | Out-Null
$mtxZip = Join-Path $tmp "mediamtx_$mtxVer.zip"
if (-not (Test-Path $mtxZip)) { curl.exe -L --fail -o $mtxZip "https://github.com/bluenviron/mediamtx/releases/download/$mtxVer/mediamtx_${mtxVer}_windows_amd64.zip"; if ($LASTEXITCODE) { throw 'Download failed: mediamtx' } }
Expand-Archive -Force $mtxZip (Join-Path $rtDest 'mediamtx')
$ffZip = Join-Path $tmp 'ffmpeg-lgpl.zip'
if (-not (Test-Path $ffZip)) { curl.exe -L --fail -o $ffZip 'https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-lgpl.zip'; if ($LASTEXITCODE) { throw 'Download failed: ffmpeg' } }
$ffTmp = Join-Path $tmp 'ffmpeg-x'; Remove-Item $ffTmp -Recurse -Force -ErrorAction SilentlyContinue
Expand-Archive -Force $ffZip $ffTmp
Copy-Item (Get-ChildItem $ffTmp -Recurse -Filter ffmpeg.exe | Select-Object -First 1).FullName (Join-Path $root 'runtime\ffmpeg\ffmpeg.exe') -Force
# RTSPanda: clone (or reuse -RtspandaDir) and run its build.ps1
$rtSrc = if ($env:RTSPANDA_DIR) { $env:RTSPANDA_DIR } else { Join-Path $tmp 'RTSPanda' }
if (-not (Test-Path $rtSrc)) { git clone --depth 1 https://github.com/248Tech/RTSPanda.git $rtSrc; if ($LASTEXITCODE) { throw 'git clone failed' } }
Push-Location $rtSrc
try { Push-Location frontend; npm ci; Pop-Location; & .\build.ps1; if (-not (Test-Path backend\rtspanda.exe)) { throw 'RTSPanda build failed' } }
finally { Pop-Location }
Copy-Item (Join-Path $rtSrc 'backend\rtspanda.exe') $rtDest -Force
Write-Host "RTSPanda, mediamtx and ffmpeg ready in runtime\rtspanda and runtime\ffmpeg (set RTSPANDA_DIR to reuse an existing checkout)"

# yt-dlp (YouTube links in CCTV notes)
$ytDest = Join-Path $root 'runtime\ytdlp'
New-Item -ItemType Directory -Force $ytDest | Out-Null
curl.exe -L --fail -o (Join-Path $ytDest 'yt-dlp.exe') 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe'
if ($LASTEXITCODE) { throw 'Download failed: yt-dlp' }
