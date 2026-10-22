# Builds QwenPad and assembles a self-contained portable folder plus zip.
#   .\package.ps1 -Release        -> dist\QwenPad-v<ver>-win-x64\ + .zip + SHA256SUMS.txt (no model; downloaded on first run)
#   .\package.ps1                 -> ..\QwenPad-Portable\ + .zip with the bundled model (personal build)
#   .\package.ps1 -NoZip          -> folder only
#   .\package.ps1 -NoModel        -> app + runtime only (drop a .gguf into models\ later)
#   .\package.ps1 -AllModels      -> ship every model in models\ (default: abliterated only)
param(
    [string]$Dest,
    [switch]$Release,
    [switch]$NoZip,
    [switch]$NoModel,
    [switch]$AllModels
)
$ErrorActionPreference = 'Stop'
$src = $PSScriptRoot
$version = ([regex]::Match((Get-Content (Join-Path $src 'src\AssemblyInfo.cs') -Raw), 'AssemblyVersion\("(\d+\.\d+\.\d+)')).Groups[1].Value
if ($Release) { $NoModel = $true }
if (-not $Dest) {
    $Dest = if ($Release) { Join-Path $src "dist\QwenPad-v$version-win-x64" } else { Join-Path (Split-Path $src -Parent) 'QwenPad-Portable' }
}
if (-not (Test-Path (Join-Path $src 'runtime\llama\llama-server.exe'))) { throw 'runtime\llama is missing. Run tools\fetch-runtime.ps1 first.' }

Write-Host "== QwenPad $version -> $Dest"
if ($Release -and (Test-Path $Dest)) { Remove-Item $Dest -Recurse -Force }
New-Item -ItemType Directory -Force $Dest | Out-Null

Write-Host '== Icon + build'
& (Join-Path $src 'tools\make-icon.ps1')
& (Join-Path $src 'build.cmd') (Join-Path $Dest 'QwenPad.exe')
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

Write-Host '== Runtime (llama.cpp + VC++ runtime)'
robocopy (Join-Path $src 'runtime\llama') (Join-Path $Dest 'runtime\llama') /E /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'Copying runtime failed' }
# llama.cpp links the VC++ 2015-2022 runtime; ship it app-local so clean PCs work (Microsoft permits this).
foreach ($dll in 'vcruntime140.dll', 'vcruntime140_1.dll', 'msvcp140.dll') {
    $sys = Join-Path $env:WINDIR "System32\$dll"
    if (Test-Path $sys) { Copy-Item $sys (Join-Path $Dest "runtime\llama\$dll") -Force } else { Write-Warning "$dll not found; users will need the VC++ redistributable." }
}
# stable-diffusion.cpp (image generation), if fetched. It reuses the CUDA DLLs from runtime\llama.
if (Test-Path (Join-Path $src 'runtime\sd\sd-server.exe')) {
    Write-Host '== Runtime (stable-diffusion.cpp)'
    robocopy (Join-Path $src 'runtime\sd') (Join-Path $Dest 'runtime\sd') /E /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw 'Copying sd runtime failed' }
    foreach ($dll in 'vcruntime140.dll', 'vcruntime140_1.dll', 'msvcp140.dll') {
        $sys = Join-Path $env:WINDIR "System32\$dll"
        if (Test-Path $sys) { Copy-Item $sys (Join-Path $Dest "runtime\sd\$dll") -Force }
    }
} else { Write-Warning 'runtime\sd not found; the release will not include image generation. Run tools\fetch-runtime.ps1.' }

New-Item -ItemType Directory -Force (Join-Path $Dest 'models'), (Join-Path $Dest 'models\image'), (Join-Path $Dest 'notes') | Out-Null
Set-Content (Join-Path $Dest 'models\image\PUT-IMAGE-MODELS-HERE.txt') 'Qwen > Image Studio downloads the Qwen-Image model here on first use (Qwen > Image Model > Download Image Model).'
$defaultModel = 'Qwen3.5-9B-Q4_K_M.gguf'
if (-not $NoModel) {
    $defaultModel = 'Qwen3.5-9B-Abliterated-Q4_K_M.gguf'
    Write-Host '== Model'
    robocopy (Join-Path $src 'models') (Join-Path $Dest 'models') $(if ($AllModels) { '*.gguf' } else { $defaultModel }) /NFL /NDL /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw 'Copying model failed' }
}
Set-Content (Join-Path $Dest 'models\PUT-GGUF-MODELS-HERE.txt') 'QwenPad lists every .gguf file in this folder under Qwen > Model. Use Qwen > Model > Download a Model to fetch one.'

Write-Host '== Defaults and docs'
@"
; QwenPad settings. Relative paths are resolved from the QwenPad folder, so the folder can live anywhere.
[Model]
ServerUrl=http://127.0.0.1:8765
ServerExe=runtime\llama\llama-server.exe
ModelPath=models\$defaultModel
ServerArgs=-c 16384 --reasoning-budget 0
AutoStartServer=1
MaxTokens=2048
Temperature=0.7
[Editor]
FontName=Consolas
FontSize=11
FontStyle=0
WordWrap=1
StatusBar=1
Theme=system
Maximized=0
[Images]
ImageServerExe=runtime\sd\sd-server.exe
ImageServerUrl=http://127.0.0.1:8766
ImageModelDir=models\image
ImageEncoder=uncensored
ImageServerArgs=--offload-to-cpu --vae-tiling
ImageIdleMinutes=5
ImageSize=768x768
ImageOutputDir=images
"@ | Set-Content (Join-Path $Dest 'QwenPad.ini') -Encoding utf8NoBOM
foreach ($doc in 'README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'CHANGELOG.md') {
    if (Test-Path (Join-Path $src $doc)) { Copy-Item (Join-Path $src $doc) (Join-Path $Dest $doc) -Force }
}
if (Test-Path (Join-Path $Dest 'logs')) { Remove-Item (Join-Path $Dest 'logs') -Recurse -Force }

$size = (Get-ChildItem $Dest -Recurse -File | Measure-Object Length -Sum).Sum
Write-Host ('Folder ready: {0} ({1:N2} GB)' -f $Dest, ($size / 1GB))

if (-not $NoZip) {
    $zip = "$Dest.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Push-Location (Split-Path $Dest -Parent)
    try { tar.exe -a -c -f $zip (Split-Path $Dest -Leaf) } finally { Pop-Location }   # bsdtar writes ZIP64 for big models
    if ($LASTEXITCODE -ne 0) { throw 'Zip failed' }
    Write-Host ('Zip ready: {0} ({1:N2} GB)' -f $zip, ((Get-Item $zip).Length / 1GB))
    if ($Release) {
        $sum = "{0}  {1}" -f (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $zip -Leaf)
        Set-Content (Join-Path (Split-Path $zip -Parent) 'SHA256SUMS.txt') $sum
        Write-Host "SHA256: $sum"
    }
}
exit 0
