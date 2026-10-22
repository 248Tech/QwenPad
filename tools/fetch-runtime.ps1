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
