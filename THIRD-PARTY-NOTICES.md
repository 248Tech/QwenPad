# Third-party notices

QwenPad itself is MIT-licensed (see [LICENSE](LICENSE)). The release package bundles, or downloads on request, the following components, each under its own license.

## Bundled in the release zip (`runtime\llama\`)

| Component | Version | License | Source |
|---|---|---|---|
| llama.cpp (`llama-server.exe`, `ggml*.dll`, `llama.dll`) | b11756 | MIT | https://github.com/ggml-org/llama.cpp |
| NVIDIA CUDA runtime (`cudart64_12.dll`, `cublas64_12.dll`, `cublasLt64_12.dll`) | 12.4 | NVIDIA CUDA Toolkit EULA (redistributable components) | https://docs.nvidia.com/cuda/eula/ |
| Microsoft Visual C++ runtime (`vcruntime140.dll`, `vcruntime140_1.dll`, `msvcp140.dll`) | 14.x | Microsoft Visual C++ Redistributable license (app-local deployment) | https://learn.microsoft.com/cpp/windows/redistributing-visual-cpp-files |

## Downloaded on first run (optional, user's choice)

| Model | License | Source |
|---|---|---|
| Qwen3.5 9B / 4B (GGUF by unsloth) | Apache-2.0 | https://huggingface.co/unsloth/Qwen3.5-9B-GGUF · https://huggingface.co/unsloth/Qwen3.5-4B-GGUF |
| Huihui Qwen3.5 9B Abliterated (GGUF by mradermacher) | Apache-2.0 | https://huggingface.co/mradermacher/Huihui-Qwen3.5-9B-abliterated-GGUF |

Qwen is a trademark of Alibaba Cloud. QwenPad is an independent project, not affiliated with or endorsed by Alibaba Cloud, NVIDIA or Microsoft.

## llama.cpp license

```
MIT License

Copyright (c) 2023-2026 The ggml authors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
