# Changelog

## [1.2.0] - 2026-10-20

First public release.

### Added
- **First-run model downloader** with Qwen3.5 9B (recommended), Qwen3.5 4B (lighter) and Qwen3.5 9B Abliterated. Resumable, disk-space checked, SHA-256 verified, pinned to exact Hugging Face revisions.
- **Ctrl+K in sticky notes:** Ask Qwen to rewrite a note's selection or write at its cursor, with live streaming, Esc to stop and one-step undo.
- **Dark mode:** View > Theme > System / Light / Dark. Covers the title bar, menus, scrollbars, dialogs and note colors, and follows Windows live.
- **Qwen > Model** menu to switch between installed `.gguf` models.
- Bundled VC++ runtime so the portable zip runs on clean Windows installs.

### Changed
- Sticky notes are owned per QwenPad folder, so separate portable copies no longer block each other.

## [1.1.0]

- Sticky notes (Notes menu), Summarize to Note, embedded app icon, portable packaging.

## [1.0.0]

- Notepad clone with local Qwen3.5 9B: Ask (Ctrl+K), Continue (Ctrl+Enter), Rewrite, Summarize and Key Points, with streaming and single-step undo.
