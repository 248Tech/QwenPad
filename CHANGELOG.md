# Changelog

## [1.11.1] - 2026-10-22

### Fixed
- **Typing in a themed sticky note no longer smears or ghosts the text**, especially with **Background Art** turned down low (or on a plain-paper design). Note windows now composite their children, so the area behind the transparent text box repaints cleanly as you type.

## [1.11.0] - 2026-10-22

### Added
- **14 new sticky note themes**, bringing the total to 23. They're grouped by category in each note's **⋯ > 🎨 Themes** menu:
  - 🍂 **Fall:** Autumn Cat, Haunted Hill
  - 🏖 **Summer:** Beach Day, Palm Beach, Leaf Me a Note, Sunflower
  - 🐾 **Critters:** Capybara, Sleepy Sloth
  - 🌀 **Patterns:** Spiral, Doodle Swirls, Geometric, Holographic, Dollar Note
  - 🏈 **Sports:** Game Day
- **Background Art** setting (⋯ menu): fade a theme's artwork from full down to a faint watermark over plain paper, independently of the note's overall opacity. The text, title and buttons stay fully solid.

### Changed
- The ⋯ menu now lists themes in per-category submenus (with thumbnails) instead of one long list.

## [1.10.0] - 2026-10-22

### Added
- **Sticky notes stay open when you close QwenPad.** If notes are on screen, closing the editor (X, Alt+F4 or File > Exit) leaves them open, and QwenPad waits in the tray with the AI model unloaded to free memory. Launch QwenPad again (or click the tray icon) and the editor comes right back. QwenPad exits when you hide the last note or choose **Quit QwenPad** from the tray. Turn this off with **Notes > Keep Notes Open When QwenPad Closes**.
- **Notes > Open Notes When Windows Starts** (also in the tray menu): reopens your sticky notes at sign-in, without the editor or the model. It uses the new `--notes` command-line switch.
- A tray icon while notes stay open: Open QwenPad, New Sticky Note, Notes List, Show All Notes, Open Notes When Windows Starts, and Quit QwenPad.

### Changed
- Notes created today show just the time (`9:15 AM`) instead of `Today, 9:15 AM`, leaving more room for the title. Hover for the full dates.
- Doodle Pumpkins keeps text clear of the pumpkins, and Hedgehog Hollow's hedgehogs sit lower, away from the first lines of text.
- New README and website: a sticky notes showcase, new screenshots of every design and feature, and a complete command reference.

## [1.9.0] - 2026-10-22

### Added
- **Five new sticky note designs** in each note's ⋯ menu, now grouped by season:
  - 🍂 **Hedgehog Hollow**: cream paper with dotted lines inside a wreath of autumn leaves, acorns, mushrooms and hedgehogs.
  - 🍂 **Doodle Pumpkins**: soft pink paper with hand-drawn pumpkins with faces, doodle flowers and sparkles.
  - ❄ **Snowflake Frost**: an icy blue glow framed by big paper-cut snowflakes.
  - ❄ **Cozy Critters**: a pale winter sky with clouds and wind swirls, and a tabby in a plaid scarf, a bulldog and a budgie in a bobble hat.
  - ❄ **Snowman Night**: periwinkle grid paper with falling snow, pine trees, snowy hills and a snowman in a beanie.
- Like the fall designs, they're drawn in code (sharp at any size, no image files), keep your text colors readable, and show in the Notes List.

## [1.8.1] - 2026-10-22

### Fixed
- **Notes no longer go missing when QwenPad is closed from outside.** If Windows or another program closes the note windows as part of closing QwenPad (for example the taskbar's *Close all windows*, or an updater), those notes used to be saved as hidden and seemed to disappear on the next launch. They now stay visible. A note you hide yourself still stays hidden.
- A bare close message sent to a note window now hides the note instead of destroying the window while QwenPad keeps running.

## [1.8.0] - 2026-10-22

### Added
- **Font color for sticky notes:** the new **A** button on a note's format bar opens a palette with Automatic, Red, Orange, Green, Teal, Blue, Purple, Pink, Brown, Gray, and More Colors. The bar under the A shows the selection's color. Picked colors are saved, and they stay put when you switch light/dark mode, note colors or fall designs. Automatic text still follows the theme and design.
- **Notes > Restore Deleted Notes:** deleting a note now moves it (with its images and drawing) to a trash (`notes\.trash`) for 30 days instead of erasing it.
- **Automatic note backups:** the notes folder is copied to `backups\notes-<date>-v<version>` once a day and whenever QwenPad is updated, keeping the newest 15. **Notes > Open Notes Backups Folder** opens them.
- **Notes > Import Notes:** brings in notes from another QwenPad folder (such as an older portable copy) or a backup. Notes you already have are skipped.
- On startup, the status bar says how many notes are hidden and how to bring them back.

### Changed
- **Crash-safe saving:** notes are written to a temp file and swapped in, and the previous save is kept as `<id>.bak`. If a note file is ever empty or damaged, the backup copy is loaded instead.
- Deleting asks for confirmation whenever the note has a title, images or a drawing, not just text.
- Notes saved by a newer QwenPad (with extra header lines) still open in this version.
## [1.7.2] - 2026-10-22

### Fixed
- **Ctrl+N (and the + button) in a sticky note** now always gives you a visible new note. It takes on the current note's pin, color or design and opacity, so a new note made from a pinned note no longer opens hidden behind it. It goes to a free spot beside the note (right, left, below or above) instead of on top of other notes, and opens with the cursor ready.
## [1.7.1] - 2026-10-22

### Changed
- The four fall designs are now listed directly in each note's ⋯ menu, right under the colors (with thumbnails), instead of in a submenu.
## [1.7.0] - 2026-10-22

### Added
- **🍂 Fall designs for sticky notes** (⋯ menu, under the colors): *Spooky Night* (a black frame of stars and pumpkins, cream scalloped paper, a witch-hat ghost), *Pumpkin Patch* (orange confetti, a cobweb and a spider, a jack-o'-lantern watermark), *Moonlit Ghost* (a starry night where you write on a big friendly ghost) and *Harvest Moon* (teal paper with coral ruled lines and a howling wolf under a full moon). They're drawn in code, so they're sharp at any size, and ruled lines follow your text. The Notes List shows each note's design.
- **Resizable images:** drag an image's corner to resize it (proportions kept), or right-click > Size > Small / Medium / Large / Full Width. Small images sit side by side, and sizes are saved.
- **Notes > Clickable Images** setting, **off by default**, so a stray click doesn't open an image. Right-click > Open always works.

### Changed
- Note text uses Windows' RichEdit 5.0 with a transparent background, so designs show behind the text.
- A note that fails to load is logged to logs\notes-errors.log instead of being skipped silently.
## [1.6.0] - 2026-10-22

### Added
- **Free draw in sticky notes:** the ✏ button (or ⋯ > Draw) opens a sketch area with a **pen**, a semi-transparent **paint brush**, and an **eraser**. Pick from five colors (ink, red, blue, green, highlighter yellow) and three sizes, with undo (Ctrl+Z), clear, and P / B / E to switch tools. Esc or ✓ finishes. Sketches are saved per note as transparent PNGs, and the Notes List marks notes that have one with ✏.

### Fixed
- Task notes from **Tasks From Email** showed a box instead of the calendar emoji; they now say "Due:".
## [1.5.0] - 2026-10-22

### Added
- **Rich text in sticky notes:** bold (Ctrl+B), italic (Ctrl+I), underline (Ctrl+U), strikethrough (Ctrl+T) and bullets (Ctrl+Shift+L), from a format bar along the bottom of each note. Formatting is saved; plain text pastes in the note's own style.
- **Images and GIFs in notes:** the 🖼 button, paste, or drag-and-drop. Images show at the note's width and **animated GIFs play**. Click one to open it, right-click to copy or remove it.
- **Opacity:** ⋯ > Opacity, from 100% down to 30%, per note.
- **Layers:** ⋯ > Layer > Bring to Front / Move Forward / Move Back / Send to Back, or **Ctrl+scroll wheel** over a note.
- **Notes List:** ⋯ > Notes List (or Notes > Notes List, Ctrl+Shift+L) opens a palette of all notes as colored cards with search. Click to select, double-click or Enter to open, right-click to show, hide, pin or delete.
- **Automatic titles:** when a note has text but no title, Qwen writes one a few seconds after you stop typing (once per note; your own titles are never replaced).
- **Qwen > Tasks From Email:** paste an email into the editor and Qwen turns each action item into a blue task note with a title, due date and details.
- The Menu key or Shift+F10 opens a note's ⋯ menu from the keyboard.
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
