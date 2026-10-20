# Downloads the pinned llama.cpp build (CUDA 12.4, x64) into runtime\llama.
# The CUDA build also contains the CPU backends, so it works on PCs without an NVIDIA GPU.
param([string]$Build = 'b11756')
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
