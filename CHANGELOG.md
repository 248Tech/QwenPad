# Changelog

## [1.4.0] - 2026-10-22

### Added
- **📌 Pin notes on top of all windows** with a pin button in each note's header (highlighted while pinned), **Ctrl+P** inside a note, or **Notes > Pin All Notes on Top / Unpin All Notes**. Pinned notes stay above every other app and are restored pinned on the next launch.

### Fixed
- Pinning a note at runtime (the old ⋯ > Always on Top) often didn't actually keep it on top. Windows declined the first request while moving the note's own tooltip windows; notes now apply and verify the setting themselves.

## [1.3.0] - 2026-10-21

### Added
- **Sticky-note titles:** each note has its own editable title line (Enter jumps to the text). Summarize to Note fills it in automatically, and the taskbar and Notes menu show it.
- **Dates on notes:** the creation date is shown beside the title ("Today, 9:41 AM", "Oct 20", or with the year for older notes); hover it to see the created and last-edited times. Older notes use their file dates.
- **Handwritten font** for note titles, text and dates (Ink Free, falling back to Segoe Print on older Windows).

## [1.2.4] - 2026-10-21

### Fixed
- **Sticky notes really resize now.** Since 1.2.1 the resize cursor appeared on a note's edges and corners, but Windows refused to start the resize because the window didn't have a sizable frame. Notes now use a zero-width sizable frame, so dragging any edge or corner works. Moving a note by its header no longer triggers Windows Snap.

## [1.2.3] - 2026-10-21

### Fixed
- **Sticky-note edges are easy to grab.** The resize border is now about 10 px wide (scaled up on high-DPI screens), with double-size corners, and it works over the note's text and header, just like a normal window frame. The top edge and both top corners resize too.

## [1.2.2] - 2026-10-21

### Added
- **Sticky notes auto-expand** as you type (or as Qwen writes), up to the bottom of the screen / 3/4 of its height; a scrollbar appears only once a note can't grow further. Notes shrink back to your chosen size when text is removed.
- **Easier resizing:** a visible grip in the bottom-right corner, wider edge and corner grab zones, and a resize zone along the top edge. The size you drag to becomes the note's base size and is saved.

## [1.2.1] - 2026-10-20

### Fixed
- **Ctrl+K in sticky notes** now always responds: shortcuts work wherever focus is in the note, and when Qwen isn't ready (loading, offline or still writing) the note says so instead of doing nothing. The Ask dialog stays on-screen and in front.
- Sticky notes no longer disappear or come back hidden after being minimized or after using Ask Qwen; visibility is saved reliably.

### Changed
- **Each sticky note is its own window** with a taskbar button, grouped with QwenPad. Minimize/restore from the taskbar, Alt+Tab to it, and the taskbar shows the note's first line.

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
