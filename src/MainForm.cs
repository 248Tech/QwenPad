using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QwenPad
{
    sealed class MainForm : Form
    {
        const string AppName = "QwenPad";
        /// <summary>Friendly name from the model file, e.g. "Qwen3.5-9B-Abliterated-Q4_K_M.gguf" -> "Qwen3.5 9B Abliterated".</summary>
        string ModelName { get { return FriendlyModelName(settings.ModelPath); } }

        static string FriendlyModelName(string path)
        {
            string n = Path.GetFileNameWithoutExtension(path ?? "");
            n = Regex.Replace(n, @"[-._](I?Q\d.*|b?f16|f32)$", "", RegexOptions.IgnoreCase);
            return n.Length > 0 ? n.Replace('-', ' ') : "Qwen";
        }
        const string SystemPrompt =
            "You are Qwen, a writing assistant built into QwenPad, a plain-text editor. " +
            "Follow the user's instruction and reply with ONLY the resulting text: no preamble, no explanations, " +
            "no surrounding quotes, and no Markdown formatting unless the text itself already uses it. " +
            "Keep the original language unless asked to translate.";

        readonly Settings settings = Settings.Load();
        readonly TextBox editor = new TextBox();
        readonly MenuStrip menu = new MenuStrip();
        readonly StatusStrip status = new StatusStrip();
        readonly ToolStripStatusLabel lblAi = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        readonly ToolStripStatusLabel lblPos = Cell(130);
        readonly ToolStripStatusLabel lblZoom = Cell(60);
        readonly ToolStripStatusLabel lblEol = Cell(120);
        readonly ToolStripStatusLabel lblEnc = Cell(110);
        readonly System.Windows.Forms.Timer uiTimer = new System.Windows.Forms.Timer { Interval = 100 };
        readonly System.Windows.Forms.Timer healthTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        readonly ServerManager server = new ServerManager();
        readonly LlmClient llm;
        readonly LlmClient noteLlm;   // separate client so a note summary doesn't share the editor's abort handle
        readonly LlmClient titleLlm;  // background titles and email tasks, so they never cancel a note's Ask
        readonly NoteManager notes;
        readonly List<ToolStripItem> aiItems = new List<ToolStripItem>();

        ToolStripMenuItem miWordWrap, miStatusBar, miStop, miServer;
        ToolStripMenuItem miNotes;
        int notesStaticCount;

        // Document state
        string filePath;
        bool dirty, loading;
        Encoding encoding = new UTF8Encoding(false);
        string eol = "\r\n";
        Font baseFont;
        int zoom = 100;
        int lastCaret = -1;
        bool posDirty = true;

        // Find / replace
        FindReplaceDialog findDialog;
        string findText = "", replaceText = "";
        bool matchCase, wrapAround = true, searchUp;

        // AI state
        string serverState = "offline";
        bool healthBusy;
        string note; DateTime noteUntil;
        bool generating;
        volatile bool cancelRequested;
        int genStart, genChunks;
        string genOriginal = "", genPrefix = "", genShown = "";
        bool genReplace, genSmartSpace;
        char genPrevChar;
        DateTime genStarted;
        string lastAsk = "";
        bool lastAskReplace = true;

        // Printing
        string printText;
        int printPos;

        public MainForm(string fileToOpen)
        {
            llm = new LlmClient(settings);
            noteLlm = new LlmClient(settings);
            titleLlm = new LlmClient(settings);
            notes = new NoteManager(InsertFromNote, AskInNote, AutoTitleNote);
            Theme.SetMode(settings.Theme);
            Text = AppName;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Size = new Size(900, 640);
            if (settings.BoundsVisible()) { StartPosition = FormStartPosition.Manual; Bounds = settings.Bounds; }
            if (settings.Maximized) WindowState = FormWindowState.Maximized;

            editor.Multiline = true;
            editor.AcceptsTab = true;
            editor.AcceptsReturn = true;
            editor.HideSelection = false;
            editor.MaxLength = 0;
            editor.BorderStyle = BorderStyle.None;
            editor.BackColor = Theme.EditorBack;   // set explicitly so read-only (during generation) doesn't turn it gray
            editor.ForeColor = Theme.EditorFore;
            editor.HandleCreated += delegate { Theme.ApplyScrollbars(editor); };
            editor.Dock = DockStyle.Fill;
            editor.AllowDrop = true;
            try { baseFont = new Font(settings.FontName, settings.FontSize, settings.FontStyle); }
            catch { baseFont = new Font("Consolas", 11f); }
            editor.Font = baseFont;
            SetWordWrap(settings.WordWrap);

            editor.TextChanged += delegate { posDirty = true; if (!loading && !dirty) { dirty = true; UpdateTitle(); } };
            editor.KeyDown += Editor_KeyDown;
            editor.MouseWheel += Editor_MouseWheel;
            editor.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            editor.DragDrop += (s, e) =>
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0 && ConfirmDiscard()) OpenFile(files[0]);
            };

            BuildMenu();
            editor.ContextMenuStrip = BuildContextMenu();

            status.Items.AddRange(new ToolStripItem[] { lblAi, lblPos, lblZoom, lblEol, lblEnc });
            status.ShowItemToolTips = true;
            status.Visible = settings.StatusBar;
            miStatusBar.Checked = settings.StatusBar;

            Controls.Add(editor);
            Controls.Add(status);
            Controls.Add(menu);
            MainMenuStrip = menu;

            uiTimer.Tick += delegate { UpdatePosition(); };
            uiTimer.Start();
            healthTimer.Tick += delegate { PollHealth(); };

            Theme.Changed += OnThemeChanged;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            ApplyTheme();

            NewDocument();
            if (!string.IsNullOrEmpty(fileToOpen))
            {
                if (File.Exists(fileToOpen)) OpenFile(fileToOpen);
                else { filePath = Path.GetFullPath(fileToOpen); UpdateTitle(); }
            }
            SetAiStatus();
        }

        static ToolStripStatusLabel Cell(int width)
        {
            return new ToolStripStatusLabel { AutoSize = false, Width = width, TextAlign = ContentAlignment.MiddleLeft, BorderSides = ToolStripStatusLabelBorderSides.Left, BorderStyle = Border3DStyle.Etched };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (notes.Available) Activate();   // claims and reopens saved sticky notes, keeping focus on the editor
            // Reuse a server that is already running (e.g. from another QwenPad window); otherwise start one.
            ThreadPool.QueueUserWorkItem(delegate
            {
                string st = llm.CheckHealth();
                SafeInvoke(() =>
                {
                    serverState = st;
                    if (st == "offline" && settings.AutoStartServer) StartServer(false);
                    SetAiStatus();
                    healthTimer.Start();
                });
            });
        }

        #region Menus

        void BuildMenu()
        {
            var file = new ToolStripMenuItem("&File");
            file.DropDownItems.AddRange(new ToolStripItem[]
            {
                Item("&New", Keys.Control | Keys.N, delegate { if (ConfirmDiscard()) NewDocument(); }),
                Item("New &Window", Keys.Control | Keys.Shift | Keys.N, delegate { Process.Start(Application.ExecutablePath); }),
                Item("&Open...", Keys.Control | Keys.O, delegate { OpenWithDialog(); }),
                Item("&Save", Keys.Control | Keys.S, delegate { Save(); }),
                Item("Save &As...", Keys.Control | Keys.Shift | Keys.S, delegate { SaveAs(); }),
                new ToolStripSeparator(),
                Item("&Print...", Keys.Control | Keys.P, delegate { Print(); }),
                new ToolStripSeparator(),
                Item("E&xit", Keys.None, delegate { Close(); })
            });

            var miUndo = ItemD("&Undo", "Ctrl+Z", delegate { if (!generating) editor.Undo(); });
            var miCut = ItemD("Cu&t", "Ctrl+X", delegate { if (!generating) editor.Cut(); });
            var miCopy = ItemD("&Copy", "Ctrl+C", delegate { editor.Copy(); });
            var miPaste = ItemD("&Paste", "Ctrl+V", delegate { if (!generating) editor.Paste(); });
            var miDelete = ItemD("De&lete", "Del", delegate { DeleteSelection(); });
            var miFindNext = Item("Find &Next", Keys.F3, delegate { FindNextOrPrompt(false); });
            var miFindPrev = Item("Find Pre&vious", Keys.Shift | Keys.F3, delegate { FindNextOrPrompt(true); });
            var edit = new ToolStripMenuItem("&Edit");
            edit.DropDownItems.AddRange(new ToolStripItem[]
            {
                miUndo, new ToolStripSeparator(), miCut, miCopy, miPaste, miDelete, new ToolStripSeparator(),
                Item("&Find...", Keys.Control | Keys.F, delegate { ShowFind(false); }),
                miFindNext, miFindPrev,
                Item("&Replace...", Keys.Control | Keys.H, delegate { ShowFind(true); }),
                Item("&Go To...", Keys.Control | Keys.G, delegate { GoTo(); }),
                new ToolStripSeparator(),
                Item("Select &All", Keys.Control | Keys.A, delegate { editor.SelectAll(); }),
                Item("Time/&Date", Keys.F5, delegate { if (!generating) editor.Paste(DateTime.Now.ToShortTimeString() + " " + DateTime.Now.ToShortDateString()); })
            });
            edit.DropDownOpening += delegate
            {
                bool sel = editor.SelectionLength > 0;
                miUndo.Enabled = editor.CanUndo && !generating;
                miCut.Enabled = miDelete.Enabled = sel && !generating;
                miCopy.Enabled = sel;
                miPaste.Enabled = Clipboard.ContainsText() && !generating;
                miFindNext.Enabled = miFindPrev.Enabled = editor.TextLength > 0;
            };

            miWordWrap = Item("&Word Wrap", Keys.None, delegate { SetWordWrap(!editor.WordWrap); });
            var format = new ToolStripMenuItem("F&ormat");
            format.DropDownItems.AddRange(new ToolStripItem[] { miWordWrap, Item("&Font...", Keys.None, delegate { ChooseFont(); }) });

            var zoomMenu = new ToolStripMenuItem("&Zoom");
            var zin = Item("Zoom &In", Keys.Control | Keys.Oemplus, delegate { SetZoom(zoom + 10); });
            zin.ShortcutKeyDisplayString = "Ctrl+Plus";
            var zout = Item("Zoom &Out", Keys.Control | Keys.OemMinus, delegate { SetZoom(zoom - 10); });
            zout.ShortcutKeyDisplayString = "Ctrl+Minus";
            zoomMenu.DropDownItems.AddRange(new ToolStripItem[] { zin, zout, Item("&Restore Default Zoom", Keys.Control | Keys.D0, delegate { SetZoom(100); }) });
            miStatusBar = Item("&Status Bar", Keys.None, delegate { status.Visible = miStatusBar.Checked = !status.Visible; });
            var themeMenu = new ToolStripMenuItem("&Theme");
            var themeItems = new[]
            {
                Item("Use &System Setting", Keys.None, delegate { SetTheme("system"); }),
                Item("&Light", Keys.None, delegate { SetTheme("light"); }),
                Item("&Dark", Keys.None, delegate { SetTheme("dark"); })
            };
            themeMenu.DropDownItems.AddRange(themeItems);
            themeMenu.DropDownOpening += delegate
            {
                themeItems[0].Checked = Theme.Mode == "system";
                themeItems[1].Checked = Theme.Mode == "light";
                themeItems[2].Checked = Theme.Mode == "dark";
            };
            var view = new ToolStripMenuItem("&View");
            view.DropDownItems.AddRange(new ToolStripItem[] { zoomMenu, miStatusBar, themeMenu });

            miNotes = new ToolStripMenuItem("&Notes");
            miNotes.DropDownItems.AddRange(new ToolStripItem[]
            {
                Item("&New Sticky Note", Keys.Control | Keys.Shift | Keys.M, delegate { if (NotesAvailable()) notes.New("", Bounds); }),
                Item("Note from &Selection", Keys.None, delegate { if (NotesAvailable()) notes.New(editor.SelectedText, Bounds); }),
                Item("✦ Su&mmarize to Note", Keys.None, delegate { SummarizeToNote(); }),
                new ToolStripSeparator(),
                Item("Show &All Notes", Keys.None, delegate { notes.ShowAll(); }),
                Item("&Hide All Notes", Keys.None, delegate { notes.HideAll(); }),
                Item("📌 &Pin All Notes on Top", Keys.None, delegate { notes.ShowAll(); notes.PinAll(true); }),
                Item("&Unpin All Notes", Keys.None, delegate { notes.PinAll(false); }),
                Item("☰ Notes &List...", Keys.Control | Keys.Shift | Keys.L, delegate { if (NotesAvailable()) notes.ShowList(this); }),
                new ToolStripSeparator()
            });
            notesStaticCount = miNotes.DropDownItems.Count;
            miNotes.DropDownOpening += delegate { RefreshNotesMenu(); };

            var qwen = new ToolStripMenuItem("&Qwen");
            AddAiItems(qwen.DropDownItems, true);
            miStop = Item("&Stop Generating", Keys.None, delegate { CancelGeneration(); });
            miStop.ShortcutKeyDisplayString = "Esc";
            miStop.Enabled = false;
            miServer = Item("Start &Local Model", Keys.None, delegate { ToggleServer(); });
            var miModel = new ToolStripMenuItem("&Model");
            miModel.DropDownItems.Add("(none)");   // placeholder so the submenu arrow shows
            miModel.DropDownOpening += delegate { RefreshModelMenu(miModel); };
            qwen.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripSeparator(), miStop, new ToolStripSeparator(), miModel, miServer,
                Item("Show Server &Log", Keys.None, delegate { ShowServerLog(); }),
                Item("S&ettings...", Keys.None, delegate { ShowSettings(); })
            });
            qwen.DropDownOpening += delegate { UpdateServerMenu(); };

            var help = new ToolStripMenuItem("&Help");
            help.DropDownItems.Add(Item("&About QwenPad", Keys.None, delegate { ShowAbout(); }));

            menu.Items.AddRange(new ToolStripItem[] { file, edit, format, view, miNotes, qwen, help });
        }

        ContextMenuStrip BuildContextMenu()
        {
            var cm = new ContextMenuStrip();
            var undo = Item("&Undo", Keys.None, delegate { editor.Undo(); });
            var cut = Item("Cu&t", Keys.None, delegate { editor.Cut(); });
            var copy = Item("&Copy", Keys.None, delegate { editor.Copy(); });
            var paste = Item("&Paste", Keys.None, delegate { editor.Paste(); });
            var del = Item("&Delete", Keys.None, delegate { DeleteSelection(); });
            cm.Items.AddRange(new ToolStripItem[]
            {
                undo, new ToolStripSeparator(), cut, copy, paste, del, new ToolStripSeparator(),
                Item("Select &All", Keys.None, delegate { editor.SelectAll(); }), new ToolStripSeparator()
            });
            AddAiItems(cm.Items, false);
            var toNote = Item("Send to Sticky &Note", Keys.None, delegate { if (NotesAvailable()) notes.New(editor.SelectedText, Bounds); });
            cm.Items.Add(new ToolStripSeparator());
            cm.Items.Add(toNote);
            cm.Opening += delegate
            {
                bool sel = editor.SelectionLength > 0;
                undo.Enabled = editor.CanUndo && !generating;
                cut.Enabled = del.Enabled = sel && !generating;
                copy.Enabled = sel;
                paste.Enabled = Clipboard.ContainsText() && !generating;
                toNote.Enabled = sel;
            };
            return cm;
        }

        /// <summary>Adds the AI actions; shortcuts are only registered on the main menu copy.</summary>
        void AddAiItems(ToolStripItemCollection items, bool main)
        {
            Func<string, Keys, EventHandler, ToolStripMenuItem> add = (text, keys, onClick) =>
            {
                var mi = Item(text, main ? keys : Keys.None, onClick);
                if (!main && keys != Keys.None) mi.ShortcutKeyDisplayString = new KeysConverter().ConvertToString(keys);
                aiItems.Add(mi);
                return mi;
            };

            var rewrite = new ToolStripMenuItem("&Rewrite");
            aiItems.Add(rewrite);
            rewrite.DropDownItems.AddRange(new ToolStripItem[]
            {
                add("&Improve Writing", main ? Keys.Control | Keys.Shift | Keys.I : Keys.None, delegate { Rewrite("Improve the clarity, flow and word choice of the text while keeping its meaning, tone and approximate length.", false); }),
                add("&Fix Spelling && Grammar", main ? Keys.Control | Keys.Shift | Keys.G : Keys.None, delegate { Rewrite("Correct the spelling, grammar and punctuation mistakes in the text. Change nothing else.", false); }),
                add("Make &Shorter", Keys.None, delegate { Rewrite("Rewrite the text to be noticeably more concise while keeping the key information.", false); }),
                add("Make &Longer", Keys.None, delegate { Rewrite("Expand the text with more detail and explanation while keeping its tone and style.", false); }),
                new ToolStripSeparator(),
                add("&Professional Tone", Keys.None, delegate { Rewrite("Rewrite the text in a professional, polished tone.", false); }),
                add("&Casual Tone", Keys.None, delegate { Rewrite("Rewrite the text in a relaxed, casual tone.", false); }),
                add("F&riendly Tone", Keys.None, delegate { Rewrite("Rewrite the text in a warm, friendly tone.", false); }),
                new ToolStripSeparator(),
                add("&Translate To...", Keys.None, delegate { Translate(); })
            });

            items.AddRange(new ToolStripItem[]
            {
                add("&Ask Qwen...", Keys.Control | Keys.K, delegate { Ask(); }),
                add("&Continue Writing", Keys.Control | Keys.Enter, delegate { ContinueWriting(); }),
                rewrite,
                add("Su&mmarize", Keys.None, delegate { Rewrite("Summarize the text in a short paragraph.", true); }),
                add("&Key Points", Keys.None, delegate { Rewrite("List the key points of the text as a short bulleted list using \"- \" bullets.", true); }),
                add("✦ Tasks From &Email", Keys.None, delegate { TasksFromEmail(); })
            });
        }

        static ToolStripMenuItem Item(string text, Keys keys, EventHandler onClick)
        {
            var mi = new ToolStripMenuItem(text, null, onClick);
            if (keys != Keys.None) mi.ShortcutKeys = keys;
            return mi;
        }

        /// <summary>Menu item whose shortcut is shown but handled natively by the edit control.</summary>
        static ToolStripMenuItem ItemD(string text, string keyDisplay, EventHandler onClick)
        {
            var mi = new ToolStripMenuItem(text, null, onClick);
            mi.ShortcutKeyDisplayString = keyDisplay;
            return mi;
        }

        #endregion

        #region Files

        void NewDocument()
        {
            loading = true;
            editor.Clear();
            editor.ClearUndo();
            loading = false;
            filePath = null;
            dirty = false;
            encoding = new UTF8Encoding(false);
            eol = "\r\n";
            UpdateTitle();
            UpdateFileStatus();
        }

        void OpenWithDialog()
        {
            if (!ConfirmDiscard()) return;
            using (var dlg = new OpenFileDialog { Filter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*" })
            {
                if (filePath != null) dlg.InitialDirectory = Path.GetDirectoryName(filePath);
                if (dlg.ShowDialog(this) == DialogResult.OK) OpenFile(dlg.FileName);
            }
        }

        void OpenFile(string path)
        {
            if (generating) CancelGeneration();
            byte[] bytes;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var ms = new MemoryStream())
                {
                    fs.CopyTo(ms);
                    bytes = ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Encoding enc;
            string text = Decode(bytes, out enc);
            eol = text.Contains("\r\n") ? "\r\n" : text.Contains("\n") ? "\n" : text.Contains("\r") ? "\r" : "\r\n";
            text = NormalizeNewlines(text);

            loading = true;
            editor.Text = text;
            editor.ClearUndo();
            editor.Select(0, 0);
            loading = false;
            encoding = enc;
            filePath = Path.GetFullPath(path);
            dirty = false;
            UpdateTitle();
            UpdateFileStatus();
        }

        bool Save()
        {
            if (filePath == null) return SaveAs();
            return WriteFile(filePath);
        }

        bool SaveAs()
        {
            using (var dlg = new SaveFileDialog { Filter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*", DefaultExt = "txt", AddExtension = true })
            {
                dlg.FileName = filePath != null ? Path.GetFileName(filePath) : "*.txt";
                if (filePath != null) dlg.InitialDirectory = Path.GetDirectoryName(filePath);
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                return WriteFile(dlg.FileName);
            }
        }

        bool WriteFile(string path)
        {
            try
            {
                string text = editor.Text;
                if (eol != "\r\n") text = text.Replace("\r\n", eol);
                byte[] pre = encoding.GetPreamble(), body = encoding.GetBytes(text);
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(pre, 0, pre.Length);
                    fs.Write(body, 0, body.Length);
                }
                filePath = Path.GetFullPath(path);
                dirty = false;
                UpdateTitle();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        bool ConfirmDiscard()
        {
            if (!dirty) return true;
            var r = MessageBox.Show(this, "Do you want to save changes to " + DocName + "?", AppName,
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.None);
            if (r == DialogResult.Yes) return Save();
            return r == DialogResult.No;
        }

        string DocName { get { return filePath != null ? Path.GetFileName(filePath) : "Untitled"; } }

        void UpdateTitle()
        {
            Text = (dirty ? "*" : "") + DocName + " - " + AppName;
        }

        static string Decode(byte[] b, out Encoding enc)
        {
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) { enc = new UTF8Encoding(true); return Encoding.UTF8.GetString(b, 3, b.Length - 3); }
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) { enc = Encoding.Unicode; return enc.GetString(b, 2, b.Length - 2); }
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) { enc = Encoding.BigEndianUnicode; return enc.GetString(b, 2, b.Length - 2); }
            try
            {
                string s = new UTF8Encoding(false, true).GetString(b);
                enc = new UTF8Encoding(false);
                return s;
            }
            catch (DecoderFallbackException)
            {
                enc = Encoding.Default;
                return enc.GetString(b);
            }
        }

        static string NormalizeNewlines(string s)
        {
            return s.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");
        }

        static string EncodingName(Encoding e)
        {
            if (e is UTF8Encoding) return e.GetPreamble().Length > 0 ? "UTF-8 with BOM" : "UTF-8";
            if (e.CodePage == 1200) return "UTF-16 LE";
            if (e.CodePage == 1201) return "UTF-16 BE";
            return "ANSI";
        }

        void UpdateFileStatus()
        {
            lblEnc.Text = EncodingName(encoding);
            lblEol.Text = eol == "\r\n" ? "Windows (CRLF)" : eol == "\n" ? "Unix (LF)" : "Macintosh (CR)";
            lblZoom.Text = zoom + "%";
            posDirty = true;
        }

        #endregion

        #region Editing

        void Editor_KeyDown(object sender, KeyEventArgs e)
        {
            if (generating)
            {
                // The editor is read-only while streaming, but undo/cut/paste keys must not touch it either.
                bool allowed = (e.Control && (e.KeyCode == Keys.C || e.KeyCode == Keys.A || e.KeyCode == Keys.Insert)) ||
                               (!e.Control && e.KeyCode >= Keys.PageUp && e.KeyCode <= Keys.Down);
                if (!allowed) { e.Handled = true; e.SuppressKeyPress = true; }
                return;
            }
            // Ctrl+Backspace deletes the previous word instead of inserting a box character.
            if (e.Control && e.KeyCode == Keys.Back)
            {
                e.SuppressKeyPress = true;
                if (editor.SelectionLength > 0) { editor.Paste(""); return; }
                int end = editor.SelectionStart, i = end;
                string t = editor.Text;
                while (i > 0 && char.IsWhiteSpace(t[i - 1])) i--;
                while (i > 0 && !char.IsWhiteSpace(t[i - 1])) i--;
                if (i < end) { editor.Select(i, end - i); editor.Paste(""); }
            }
        }

        void Editor_MouseWheel(object sender, MouseEventArgs e)
        {
            if (ModifierKeys != Keys.Control) return;
            SetZoom(zoom + (e.Delta > 0 ? 10 : -10));
            var he = e as HandledMouseEventArgs;
            if (he != null) he.Handled = true;
        }

        void DeleteSelection()
        {
            if (generating) return;
            if (editor.SelectionLength == 0)
            {
                int pos = editor.SelectionStart;
                if (pos >= editor.TextLength) return;
                int len = (pos + 1 < editor.TextLength && editor.Text.Substring(pos, 2) == "\r\n") ? 2 : 1;
                editor.Select(pos, len);
            }
            editor.Paste("");
        }

        void SetWordWrap(bool wrap)
        {
            editor.WordWrap = wrap;
            editor.ScrollBars = wrap ? ScrollBars.Vertical : ScrollBars.Both;
            if (miWordWrap != null) miWordWrap.Checked = wrap;
            settings.WordWrap = wrap;
            posDirty = true;
        }

        void ChooseFont()
        {
            using (var dlg = new FontDialog { Font = baseFont, ShowEffects = false, AllowVerticalFonts = false })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                baseFont = dlg.Font;
                settings.FontName = baseFont.Name;
                settings.FontSize = baseFont.Size;
                settings.FontStyle = baseFont.Style;
                SetZoom(zoom);
            }
        }

        void SetZoom(int value)
        {
            zoom = Math.Max(10, Math.Min(500, value));
            editor.Font = new Font(baseFont.FontFamily, Math.Max(1f, baseFont.Size * zoom / 100f), baseFont.Style);
            lblZoom.Text = zoom + "%";
        }

        void UpdatePosition()
        {
            int caret = editor.SelectionStart;
            if (!posDirty && caret == lastCaret) return;
            lastCaret = caret;
            posDirty = false;
            int line, col;
            if (!editor.WordWrap)
            {
                line = editor.GetLineFromCharIndex(caret);
                col = caret - editor.GetFirstCharIndexFromLine(line);
            }
            else
            {
                // With word wrap the control reports visual lines; count logical ones instead.
                string t = editor.Text;
                line = 0;
                int lineStart = 0;
                for (int i = 0; i < caret && i < t.Length; i++)
                    if (t[i] == '\n') { line++; lineStart = i + 1; }
                col = caret - lineStart;
            }
            lblPos.Text = "Ln " + (line + 1) + ", Col " + (col + 1);
        }

        void GoTo()
        {
            string t = editor.Text;
            int lines = 1;
            foreach (char c in t) if (c == '\n') lines++;
            int current = 1;
            for (int i = 0; i < editor.SelectionStart && i < t.Length; i++) if (t[i] == '\n') current++;
            using (var dlg = new InputDialog("Go To Line", "&Line number:", current.ToString(), 220, null, false, false))
            {
                dlg.Validator = GoToValidation.Lines(lines);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                int target = int.Parse(dlg.Value.Trim()) - 1, pos = 0;
                for (int n = 0; n < target; n++) pos = t.IndexOf('\n', pos) + 1;
                editor.Select(pos, 0);
                editor.ScrollToCaret();
            }
        }

        #endregion

        #region Find / Replace

        void ShowFind(bool replace)
        {
            if (findDialog == null) findDialog = new FindReplaceDialog(this) { Owner = this };
            string sel = editor.SelectedText;
            string initial = sel.Length > 0 && sel.IndexOf('\n') < 0 ? sel : findText;
            findDialog.ShowMode(replace, initial, matchCase, wrapAround, searchUp);
        }

        internal void SetSearch(string find, string replace, bool caseSensitive, bool wrap, bool up)
        {
            findText = find;
            replaceText = replace;
            matchCase = caseSensitive;
            wrapAround = wrap;
            searchUp = up;
        }

        void FindNextOrPrompt(bool up)
        {
            if (findText.Length == 0) { ShowFind(false); return; }
            bool saved = searchUp;
            searchUp = up;
            FindNext(true);
            searchUp = saved;
        }

        internal bool FindNext(bool notify)
        {
            if (findText.Length == 0) return false;
            var cmp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            string t = editor.Text;
            int idx = -1;
            if (!searchUp)
            {
                int from = editor.SelectionStart + editor.SelectionLength;
                if (from <= t.Length) idx = t.IndexOf(findText, from, cmp);
                if (idx < 0 && wrapAround) idx = t.IndexOf(findText, 0, cmp);
            }
            else
            {
                int from = editor.SelectionStart - 1;
                if (from >= 0) idx = t.LastIndexOf(findText, from, cmp);
                if (idx < 0 && wrapAround && t.Length > 0) idx = t.LastIndexOf(findText, t.Length - 1, cmp);
            }
            if (idx < 0)
            {
                if (notify)
                {
                    IWin32Window owner = findDialog != null && findDialog.Visible ? (IWin32Window)findDialog : this;
                    MessageBox.Show(owner, "Cannot find \"" + findText + "\"", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return false;
            }
            editor.Select(idx, findText.Length);
            editor.ScrollToCaret();
            return true;
        }

        internal void ReplaceOne()
        {
            if (generating) return;
            var cmp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            if (editor.SelectionLength > 0 && string.Equals(editor.SelectedText, findText, cmp))
                editor.Paste(NormalizeNewlines(replaceText));
            FindNext(true);
        }

        internal void ReplaceAll()
        {
            if (generating || findText.Length == 0) return;
            var cmp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            string t = editor.Text, rep = NormalizeNewlines(replaceText);
            var sb = new StringBuilder(t.Length);
            int pos = 0, count = 0, idx;
            while ((idx = t.IndexOf(findText, pos, cmp)) >= 0)
            {
                sb.Append(t, pos, idx - pos).Append(rep);
                pos = idx + findText.Length;
                count++;
            }
            if (count == 0) { FindNext(true); return; }
            sb.Append(t, pos, t.Length - pos);
            editor.SelectAll();
            editor.Paste(sb.ToString());   // a single undoable edit
            editor.Select(0, 0);
        }

        #endregion

        #region Printing

        void Print()
        {
            using (var doc = new PrintDocument { DocumentName = DocName })
            using (var dlg = new PrintDialog { Document = doc, UseEXDialog = true })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                printText = editor.Text;
                printPos = 0;
                doc.PrintPage += (s, e) =>
                {
                    int chars, lines;
                    string rest = printText.Substring(printPos);
                    e.Graphics.MeasureString(rest, baseFont, e.MarginBounds.Size, StringFormat.GenericTypographic, out chars, out lines);
                    e.Graphics.DrawString(rest.Substring(0, chars), baseFont, Brushes.Black, e.MarginBounds, StringFormat.GenericTypographic);
                    printPos += Math.Max(chars, 1);
                    e.HasMorePages = printPos < printText.Length;
                };
                try { doc.Print(); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        #endregion

        #region Qwen actions

        /// <summary>Applies an instruction to the selection (or whole document if nothing is selected).</summary>
        void Rewrite(string instruction, bool insertAfter)
        {
            if (!EnsureReady()) return;
            int start = editor.SelectionStart, len = editor.SelectionLength;
            if (len == 0) { start = 0; len = editor.TextLength; }
            string text = editor.Text.Substring(start, len);
            if (text.Trim().Length == 0) { ShowNote("Type or select some text first."); return; }
            string prompt = instruction + "\n\n<text>\n" + text + "\n</text>";
            if (insertAfter)
                Generate(prompt, start + len, 0, text.EndsWith("\r\n") ? "\r\n" : "\r\n\r\n", false, false);
            else
                Generate(prompt, start, len, "", true, false);
        }

        void Translate()
        {
            using (var dlg = new InputDialog("Translate", "Translate the selection (or whole document) into:", "Spanish", 260, null, false, false))
            {
                dlg.Validator = v => v.Trim().Length == 0 ? "Enter a language." : null;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Rewrite("Translate the text into " + dlg.Value.Trim() + ". Preserve formatting and line breaks.", false);
            }
        }

        void ContinueWriting()
        {
            if (!EnsureReady()) return;
            int caret = editor.SelectionStart + editor.SelectionLength;
            string all = editor.Text;
            string before = all.Substring(0, caret), after = all.Substring(caret);
            if (before.Trim().Length == 0) { ShowNote("Write a few words first, or use Ask Qwen (Ctrl+K)."); return; }
            if (before.Length > 8000) before = before.Substring(before.Length - 8000);
            if (after.Length > 2000) after = after.Substring(0, 2000);
            string prompt =
                "Continue writing the document from exactly where the text before the cursor ends. Match its style, tone, " +
                "language and formatting. Write the natural next part: about one to three paragraphs of prose, or the next few items " +
                "if it is a list, or the next lines if it is code. Output only the new text to insert; never repeat the existing text." +
                "\n\n<before_cursor>\n" + before + "\n</before_cursor>";
            if (after.Trim().Length > 0) prompt += "\n\n<after_cursor>\n" + after + "\n</after_cursor>";
            editor.Select(caret, 0);
            Generate(prompt, caret, 0, "", false, true);
        }

        void Ask()
        {
            if (!EnsureReady()) return;
            bool hasSel = editor.SelectionLength > 0;
            using (var dlg = new InputDialog("Ask Qwen",
                hasSel ? "What should Qwen do with the selected text?" : "What should Qwen write at the cursor?",
                lastAsk, 460, "&Replace the selection with the result", lastAskReplace, hasSel))
            {
                dlg.Validator = v => v.Trim().Length == 0 ? "Type an instruction, e.g. \"turn this into a polite email\"." : null;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                lastAsk = dlg.Value.Trim();
                if (hasSel) lastAskReplace = dlg.Checked;

                int start = editor.SelectionStart, len = editor.SelectionLength;
                if (hasSel && dlg.Checked)
                {
                    Generate(lastAsk + "\n\n<text>\n" + editor.SelectedText + "\n</text>", start, len, "", true, false);
                    return;
                }

                string all = editor.Text;
                int caret = start + len;
                string before = all.Substring(0, caret), after = all.Substring(caret);
                if (before.Length > 8000) before = before.Substring(before.Length - 8000);
                if (after.Length > 2000) after = after.Substring(0, 2000);
                string prompt = "Instruction: " + lastAsk;
                if (hasSel) prompt += "\n\nThe user selected this text:\n<selection>\n" + editor.SelectedText + "\n</selection>";
                if (all.Trim().Length > 0)
                    prompt += "\n\nYour reply will be inserted into the document at the position marked [CURSOR]. " +
                              "Output only the text to insert.\n\n<document>\n" + before + "[CURSOR]" + after + "\n</document>";
                editor.Select(caret, 0);
                Generate(prompt, caret, 0, hasSel ? "\r\n" : "", false, !hasSel);
            }
        }

        bool EnsureReady()
        {
            if (generating) return false;
            if (serverState == "ready") return true;
            if (serverState == "loading")
            {
                ShowNote("Qwen is still loading the model; try again in a moment.");
                return false;
            }
            if (MessageBox.Show(this, "The local Qwen model isn't running. Start it now?\n\nLoading takes a little while; the status bar shows when it's ready.",
                    AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                StartServer(true);
            return false;
        }

        /// <summary>
        /// Streams the model's reply into the editor, replacing [start, start+length).
        /// When done, the whole change is re-applied as one undoable edit so Ctrl+Z reverts it.
        /// </summary>
        void Generate(string userPrompt, int start, int length, string prefix, bool replace, bool smartSpace)
        {
            generating = true;
            cancelRequested = false;
            genStart = start;
            genOriginal = editor.Text.Substring(start, length);
            genPrefix = prefix;
            genReplace = replace;
            genSmartSpace = smartSpace;
            genPrevChar = start > 0 ? editor.Text[start - 1] : '\n';
            genShown = "";
            genChunks = 0;
            genStarted = DateTime.Now;

            SetGenerating(true);
            if (length > 0) ReplaceRange(start, length, "");
            lblAi.Text = "Qwen is thinking… (Esc to stop)";

            var t = new Thread(() =>
            {
                string error = null;
                var raw = new StringBuilder();
                try
                {
                    llm.Stream(SystemPrompt, userPrompt, delta =>
                    {
                        raw.Append(delta);
                        string snapshot = raw.ToString();
                        SafeInvoke(() => OnGenProgress(snapshot));
                    }, () => cancelRequested);
                }
                catch (Exception ex)
                {
                    if (!cancelRequested) error = ex.Message;
                }
                string final = raw.ToString();
                SafeInvoke(() => OnGenDone(final, error));
            });
            t.IsBackground = true;
            t.Start();
        }

        void OnGenProgress(string raw)
        {
            if (!generating) return;
            genChunks++;
            string vis = VisibleText(raw, false);
            if (vis.Length == 0) return;
            string full = Decorate(vis);
            if (full.StartsWith(genShown, StringComparison.Ordinal))
                ReplaceRange(genStart + genShown.Length, 0, full.Substring(genShown.Length));
            else
                ReplaceRange(genStart, genShown.Length, full);
            genShown = full;
            editor.Select(genStart + genShown.Length, 0);
            editor.ScrollToCaret();
            double secs = (DateTime.Now - genStarted).TotalSeconds;
            lblAi.Text = string.Format("Qwen is writing… {0:0.0} tok/s (Esc to stop)", secs > 0 ? genChunks / secs : 0);
        }

        void OnGenDone(string raw, string error)
        {
            if (!generating) return;
            generating = false;

            string vis = VisibleText(raw, true);
            string final = vis.Length > 0 ? Decorate(vis) : "";
            if (genReplace && final.Length > 0)
                final = LeadingWhitespace(genOriginal) + final + TrailingWhitespace(genOriginal);

            // Swap the streamed text back for the original, then apply the result as one undoable paste.
            SendMessage(editor.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                ReplaceRange(genStart, genShown.Length, genOriginal);
                editor.ReadOnly = false;
                if (final.Length > 0 && final != genOriginal)
                {
                    editor.Select(genStart, genOriginal.Length);
                    editor.Paste(final);
                    if (genReplace) editor.Select(genStart, final.Length);
                    else editor.Select(genStart + final.Length, 0);
                }
                else
                {
                    editor.Select(genStart, genOriginal.Length);
                }
            }
            finally
            {
                SendMessage(editor.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                editor.Invalidate();
            }
            editor.ScrollToCaret();
            SetGenerating(false);

            double secs = (DateTime.Now - genStarted).TotalSeconds;
            if (error != null)
            {
                ShowNote("Qwen error: " + error);
                MessageBox.Show(this, "Qwen ran into a problem:\n\n" + error, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (final.Length == 0)
                ShowNote(cancelRequested ? "Stopped." : "Qwen didn't return any text.");
            else
                ShowNote(string.Format("{0}{1} tokens in {2:0.0}s ({3:0.0} tok/s). Ctrl+Z to undo.",
                    cancelRequested ? "Stopped. " : "Done: ", genChunks, secs, secs > 0 ? genChunks / secs : 0));
        }

        void CancelGeneration()
        {
            if (!generating) return;
            cancelRequested = true;
            llm.Abort();
        }

        void SetGenerating(bool on)
        {
            editor.ReadOnly = on;
            foreach (var item in aiItems) item.Enabled = !on;
            miStop.Enabled = on;
            UseWaitCursor = false;
            editor.Cursor = on ? Cursors.AppStarting : Cursors.IBeam;
        }

        /// <summary>Hides &lt;think&gt; blocks and, on the final pass, wrappers the model sometimes echoes back.</summary>
        string VisibleText(string raw, bool final)
        {
            string s = StripThinking(raw, final);
            if (final)
            {
                s = s.TrimEnd();
                s = StripTag(s, "text");
                s = StripTag(s, "before_cursor");
                s = s.Replace("[CURSOR]", "");
                if (s.StartsWith("```") && genOriginal.IndexOf("```", StringComparison.Ordinal) < 0)
                {
                    int nl = s.IndexOf('\n');
                    s = nl >= 0 ? s.Substring(nl + 1) : "";
                    if (s.TrimEnd().EndsWith("```")) s = s.TrimEnd().Substring(0, s.TrimEnd().Length - 3).TrimEnd();
                }
            }
            return s.Replace("\n", "\r\n");
        }

        /// <summary>Removes &lt;think&gt; content and normalizes newlines to \n.</summary>
        static string StripThinking(string raw, bool final)
        {
            string s = Regex.Replace(raw, "<think>.*?</think>", "", RegexOptions.Singleline);
            int open = s.IndexOf("<think>", StringComparison.Ordinal);
            if (open >= 0) s = s.Substring(0, open);
            if (!final)
            {
                // Hold back a partially-received tag such as "<thi".
                int lt = s.LastIndexOf('<');
                if (lt >= 0 && s.Length - lt < 8 && "<think>".StartsWith(s.Substring(lt), StringComparison.Ordinal)) s = s.Substring(0, lt);
            }
            return s.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        static string StripTag(string s, string tag)
        {
            string open = "<" + tag + ">", close = "</" + tag + ">";
            string t = s.Trim();
            if (t.StartsWith(open)) t = t.Substring(open.Length).TrimStart('\n');
            if (t.EndsWith(close)) t = t.Substring(0, t.Length - close.Length).TrimEnd('\n');
            return t.Length == s.Trim().Length ? s : t;
        }

        /// <summary>Applies the prefix and spacing rules to the visible model output.</summary>
        string Decorate(string vis)
        {
            if (genReplace) vis = vis.TrimStart();
            else if (genSmartSpace && vis.Length > 0)
            {
                if (char.IsWhiteSpace(genPrevChar))
                {
                    if (genPrevChar == ' ' || genPrevChar == '\t') vis = vis.TrimStart(' ', '\t');
                }
                else if (char.IsLetterOrDigit(vis[0]) || vis[0] == '(' || vis[0] == '"')
                    vis = " " + vis;
            }
            return genPrefix + vis;
        }

        static string LeadingWhitespace(string s)
        {
            int i = 0;
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            return s.Substring(0, i);
        }

        static string TrailingWhitespace(string s)
        {
            int i = s.Length;
            while (i > 0 && char.IsWhiteSpace(s[i - 1])) i--;
            return i == 0 ? "" : s.Substring(i);
        }

        /// <summary>Programmatic, non-undoable replacement (works while the editor is read-only).</summary>
        void ReplaceRange(int start, int length, string text)
        {
            bool ro = editor.ReadOnly;
            editor.ReadOnly = false;
            editor.Select(start, length);
            editor.SelectedText = text;
            editor.ReadOnly = ro;
        }

        #endregion

        #region Theme

        void SetTheme(string mode)
        {
            settings.Theme = mode;
            settings.Save();
            Theme.SetMode(mode);
        }

        void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (Theme.Mode == "system") SafeInvoke(() => Theme.Refresh(false));
        }

        void OnThemeChanged(object sender, EventArgs e) { ApplyTheme(); }

        void ApplyTheme()
        {
            editor.BackColor = Theme.EditorBack;
            editor.ForeColor = Theme.EditorFore;
            BackColor = Theme.Surface;
            status.RenderMode = ToolStripRenderMode.ManagerRenderMode;
            status.BackColor = Theme.Surface;
            foreach (var lbl in new[] { lblAi, lblPos, lblZoom, lblEol, lblEnc })
                lbl.ForeColor = Theme.SurfaceFore;
            Theme.ApplyWindow(this);
            Theme.ApplyScrollbars(editor);
            if (findDialog != null) Theme.ApplyForm(findDialog);
            notes.ApplyTheme();
            menu.Invalidate();
            status.Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyWindow(this);
        }

        #endregion

        #region Sticky notes

        /// <summary>Ctrl+K inside a sticky note: rewrite the note's selection, or write at its cursor.</summary>
        void AskInNote(StickyNote note)
        {
            // Report readiness in the note itself; the main window's status bar may not even be visible.
            if (serverState == "loading") { note.SetStatus("⏳ Qwen is still loading the model…"); return; }
            if (serverState != "ready")
            {
                if (MessageBox.Show(note, "The local Qwen model isn't running. Start it now?", AppName,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    StartServer(true);
                    if (serverState == "loading") note.SetStatus("⏳ Starting Qwen… press Ctrl+K again when ready");
                }
                return;
            }
            var body = note.Body;
            bool hasSel = body.SelectionLength > 0;
            using (var dlg = new InputDialog("Ask Qwen",
                hasSel ? "What should Qwen do with the selected text?" : "What should Qwen write in this note?",
                lastAsk, 460, "&Replace the selection with the result", lastAskReplace, hasSel))
            {
                dlg.Validator = v => v.Trim().Length == 0 ? "Type an instruction, e.g. \"turn this into a checklist\"." : null;
                dlg.TopMost = note.TopMost;
                note.SetStatus("");
                if (dlg.ShowDialog(note) != DialogResult.OK || note.IsDisposed) return;
                note.Activate();
                lastAsk = dlg.Value.Trim();
                if (hasSel) lastAskReplace = dlg.Checked;

                int start = body.SelectionStart, len = body.SelectionLength;
                bool replace = hasSel && dlg.Checked;
                string prompt;
                if (replace)
                    prompt = lastAsk + "\n\n<text>\n" + body.SelectedText + "\n</text>";
                else
                {
                    string all = body.Text;
                    int caret = start + len;
                    prompt = "Instruction: " + lastAsk + "\n\nThis is for a short sticky note, so keep it brief.";
                    if (hasSel) prompt += "\n\nThe user selected this text:\n<selection>\n" + body.SelectedText + "\n</selection>";
                    if (all.Trim().Length > 0)
                        prompt += "\n\nYour reply will be inserted into the note at the position marked [CURSOR]. " +
                                  "Output only the text to insert.\n\n<document>\n" + all.Substring(0, caret) + "[CURSOR]" + all.Substring(caret) + "\n</document>";
                    start = caret;
                    len = 0;
                }
                StreamIntoNote(note, prompt, start, len, replace);
            }
        }

        /// <summary>Streams a reply into [start, start+length) of a note, then commits it as one undoable edit.</summary>
        void StreamIntoNote(StickyNote note, string prompt, int start, int length, bool replace)
        {
            string original = note.Body.Text.Substring(start, length);
            char prev = start > 0 ? note.Body.Text[start - 1] : '\n';
            string shown = "";
            bool cancelled = false;
            note.SetBusy(true);
            note.CancelStreaming = () => { cancelled = true; noteLlm.Abort(); };
            if (length > 0) note.ReplaceRange(start, length, "");

            Func<string, string> decorate = vis =>
            {
                if (replace) return vis.TrimStart();
                if (vis.Length > 0 && !char.IsWhiteSpace(prev) && char.IsLetterOrDigit(vis[0])) return " " + vis;
                return vis;
            };

            var t = new Thread(() =>
            {
                var raw = new StringBuilder();
                string error = null;
                try
                {
                    noteLlm.Stream(SystemPrompt, prompt, delta =>
                    {
                        raw.Append(delta);
                        string vis = NoteText(raw.ToString(), false);
                        SafeInvoke(() =>
                        {
                            if (note.IsDisposed || !note.Busy) return;
                            string full = decorate(vis);
                            if (full.StartsWith(shown, StringComparison.Ordinal)) note.ReplaceRange(start + shown.Length, 0, full.Substring(shown.Length));
                            else note.ReplaceRange(start, shown.Length, full);
                            shown = full;
                        });
                    }, () => cancelled || note.IsDisposed);
                }
                catch (Exception ex) { if (!cancelled) error = ex.Message; }
                string final = NoteText(raw.ToString(), true).Replace("[CURSOR]", "");
                SafeInvoke(() =>
                {
                    if (note.IsDisposed) return;
                    string result = final.Length > 0 ? decorate(final) : "";
                    if (replace && result.Length > 0) result = LeadingWhitespace(original) + result + TrailingWhitespace(original);
                    note.ReplaceRange(start, shown.Length, original);
                    note.SetBusy(false);
                    note.CancelStreaming = null;
                    if (result.Length > 0 && result != original)
                    {
                        note.Body.Select(start, original.Length);
                        note.ReplaceSelectionUndoable(result);   // single undoable edit
                        if (replace) note.Body.Select(start, result.Length);
                    }
                    else note.Body.Select(start, original.Length);
                    if (error != null) note.SetStatus("⚠ " + error);
                    else if (result.Length == 0 && !cancelled) note.SetStatus("Qwen returned nothing.");
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        bool NotesAvailable()
        {
            if (notes.Available) return true;
            MessageBox.Show(this, "Sticky notes are open in another QwenPad window. Use that window, or close it first.",
                AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        /// <summary>Rebuilds the list of notes below the fixed Notes menu items.</summary>
        void RefreshNotesMenu()
        {
            var items = miNotes.DropDownItems;
            while (items.Count > notesStaticCount) items.RemoveAt(items.Count - 1);
            bool available = notes.Available;
            for (int i = 0; i < notesStaticCount; i++) items[i].Enabled = available;
            items[1].Enabled = available && editor.SelectionLength > 0;
            items[4].Enabled = available && notes.CountVisible() < notes.Notes.Count;
            items[5].Enabled = available && notes.CountVisible() > 0;
            items[6].Enabled = available && notes.Notes.Count > 0 && notes.CountPinned() < notes.Notes.Count;
            items[7].Enabled = available && notes.CountPinned() > 0;
            if (!available)
            {
                items.Add(new ToolStripMenuItem("Notes are open in another QwenPad window") { Enabled = false });
                return;
            }
            if (notes.Notes.Count == 0)
            {
                items.Add(new ToolStripMenuItem("(No notes yet)") { Enabled = false });
                return;
            }
            foreach (var n in notes.Notes)
            {
                var note = n;
                items.Add(new ToolStripMenuItem(note.Title.Replace("&", "&&"), StickyNote.Swatch(note.ColorIndex), delegate { notes.ShowNote(note); })
                {
                    Checked = note.WantsVisible,
                    ToolTipText = note.WantsVisible ? "Bring to front" : "Show note"
                });
            }
        }

        /// <summary>Generates a title for a note in the background. False if Qwen isn't ready (the note retries later).</summary>
        bool AutoTitleNote(StickyNote note)
        {
            if (serverState != "ready") return false;
            string text = note.Body.Text.Trim();
            if (text.Length > 3000) text = text.Substring(0, 3000);
            string prompt = "Write a short title (2 to 6 words) for this sticky note. Output only the title: no quotes, " +
                            "no trailing period and no \"Title:\" prefix.\n\n<note>\n" + text + "\n</note>";
            var t = new Thread(() =>
            {
                var raw = new StringBuilder();
                try { titleLlm.Stream(SystemPrompt, prompt, d => raw.Append(d), () => note.IsDisposed || raw.Length > 300); }
                catch { }
                string title = CleanTitle(NoteText(raw.ToString(), true));
                SafeInvoke(() => note.ApplyAutoTitle(title));
            });
            t.IsBackground = true;
            t.Start();
            return true;
        }

        static string CleanTitle(string s)
        {
            s = s.Replace("\r", "").Trim();
            int nl = s.IndexOf('\n');
            if (nl >= 0) s = s.Substring(0, nl);
            s = Regex.Replace(s, @"^\s*title\s*:\s*", "", RegexOptions.IgnoreCase);
            s = s.Trim().Trim('"', '\'', '*', '#', '“', '”', '`', ' ').TrimEnd('.', ':').Trim();
            return s.Length > 60 ? s.Substring(0, 60).TrimEnd() : s;
        }

        bool tasksRunning;

        /// <summary>Qwen > Tasks From Email: turns the email in the editor (or the selection) into task sticky notes.</summary>
        void TasksFromEmail()
        {
            if (!NotesAvailable()) return;
            if (tasksRunning) { ShowNote("✦ Qwen is still reading the email…"); return; }
            string email = (editor.SelectionLength > 0 ? editor.SelectedText : editor.Text).Trim();
            if (email.Length < 20)
            {
                MessageBox.Show(this, "Paste an email into the editor (or select one), then choose Qwen > Tasks From Email.",
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!EnsureReady()) return;
            if (email.Length > 12000) email = email.Substring(0, 12000);
            string prompt =
                "Read the email below and pull out the concrete tasks the reader needs to do: requests, deadlines, follow-ups " +
                "and things they agreed to. For each task output exactly this block:\n" +
                "TASK: <short imperative title, at most 8 words>\n" +
                "DUE: <the deadline or date mentioned, or none>\n" +
                "DETAILS: <one or two short sentences with the specifics: who, what, where>\n" +
                "---\n" +
                "Output only these blocks, at most 10, most important first. If the email asks nothing of the reader, output NONE." +
                "\n\n<email>\n" + email + "\n</email>";
            tasksRunning = true;
            note = "✦ Qwen is reading the email for tasks…";
            noteUntil = DateTime.Now.AddMinutes(5);
            SetAiStatus();
            var t = new Thread(() =>
            {
                var raw = new StringBuilder();
                string error = null;
                try { titleLlm.Stream(SystemPrompt, prompt, d => raw.Append(d), () => false); }
                catch (Exception ex) { error = ex.Message; }
                string result = raw.ToString();
                SafeInvoke(() => CreateTaskNotes(result, error));
            });
            t.IsBackground = true;
            t.Start();
        }

        void CreateTaskNotes(string raw, string error)
        {
            tasksRunning = false;
            var tasks = ParseTasks(NoteText(raw, true));
            if (tasks.Count == 0)
            {
                ShowNote(error != null ? "Qwen error: " + error : "No tasks found in that email.");
                MessageBox.Show(this, error != null ? "Qwen ran into a problem:\n\n" + error : "Qwen didn't find any tasks for you in that email.",
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            foreach (var task in tasks)
            {
                string body = task[2];
                // Plain "Due:" (emoji don't render in the notes' handwritten font).
                if (task[1].Length > 0 && !task[1].Equals("none", StringComparison.OrdinalIgnoreCase)) body += (body.Length > 0 ? "\r\n" : "") + "Due: " + task[1];
                notes.New(body, Bounds, task[0], 4);   // blue, so tasks stand out from ordinary notes
            }
            ShowNote(string.Format("✦ Created {0} task note{1} from the email.", tasks.Count, tasks.Count == 1 ? "" : "s"));
        }

        /// <summary>Parses TASK / DUE / DETAILS blocks into {title, due, details}; tolerant of Markdown decoration.</summary>
        static List<string[]> ParseTasks(string s)
        {
            var list = new List<string[]>();
            string title = null, due = "", details = "";
            Action flush = () =>
            {
                if (!string.IsNullOrEmpty(title)) list.Add(new[] { title, due, details });
                title = null; due = ""; details = "";
            };
            foreach (string rawLine in s.Replace("\r", "").Split('\n'))
            {
                string line = rawLine.Replace("**", "").Trim().TrimStart('-', '#', '*', ' ').Trim();
                line = Regex.Replace(line, @"^\d+[.)]\s*", "");
                if (rawLine.Trim().StartsWith("---")) { flush(); continue; }
                var m = Regex.Match(line, @"^(TASK|DUE|DETAILS)\s*:\s*(.*)$", RegexOptions.IgnoreCase);
                if (!m.Success)
                {
                    if (title != null && line.Length > 0) details = (details + " " + line).Trim();
                    continue;
                }
                string key = m.Groups[1].Value.ToUpperInvariant(), value = m.Groups[2].Value.Trim();
                if (key == "TASK") { flush(); title = value.TrimEnd('.'); }
                else if (key == "DUE") due = value.Trim('.', ' ');
                else details = value;
            }
            flush();
            return list.Count > 10 ? list.GetRange(0, 10) : list;
        }

        void InsertFromNote(string text)
        {
            if (generating || text.Length == 0) return;
            Activate();
            editor.Focus();
            editor.Paste(NormalizeNewlines(text));
        }

        /// <summary>Has Qwen condense the selection (or document) into a new sticky note, streaming into it.</summary>
        void SummarizeToNote()
        {
            if (!NotesAvailable() || !EnsureReady()) return;
            string text = editor.SelectionLength > 0 ? editor.SelectedText : editor.Text;
            if (text.Trim().Length == 0) { ShowNote("Type or select some text first."); return; }
            if (text.Length > 12000) text = text.Substring(0, 12000);
            var note = notes.New("", Bounds);
            if (note == null) return;
            note.SetBusy(true);
            string prompt = "Turn the text into a short sticky note: a short title on the first line, then up to five concise " +
                            "bullet points that start with \"• \". Plain text only.\n\n<text>\n" + text + "\n</text>";
            var t = new Thread(() =>
            {
                var raw = new StringBuilder();
                string error = null;
                try
                {
                    noteLlm.Stream(SystemPrompt, prompt, delta =>
                    {
                        raw.Append(delta);
                        string s = NoteText(raw.ToString(), false);
                        SafeInvoke(() => { if (!note.IsDisposed) note.SetStreamText(s); });
                    }, () => note.IsDisposed);
                }
                catch (Exception ex) { error = ex.Message; }
                string final = NoteText(raw.ToString(), true);
                SafeInvoke(() =>
                {
                    if (note.IsDisposed) return;
                    note.SetBusy(false);
                    note.SetStreamText(final.Length > 0 ? final : "Qwen couldn't summarize this" + (error != null ? ": " + error : "."));
                    if (final.Length > 0) note.TakeTitleFromFirstLine();   // the prompt asks for a title on the first line
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        static string NoteText(string raw, bool final)
        {
            string s = StripThinking(raw, final);
            if (final) s = StripTag(s.Trim(), "text").Trim();
            return s.Replace("\n", "\r\n");
        }

        #endregion

        #region Local model server

        void StartServer(bool interactive)
        {
            if (!File.Exists(Settings.Resolve(settings.ModelPath)) && Settings.FindModels().Length == 0)
            {
                // First run of a release build: no model shipped yet.
                if (interactive || !downloadOffered) { downloadOffered = true; OfferModelDownload(true); }
                else ShowNote("No model installed · Qwen > Model > Download a Model");
                return;
            }
            string err = server.Start(settings);
            if (err != null)
            {
                ShowNote("Couldn't start the local model.");
                if (interactive || settings.AutoStartServer)
                    MessageBox.Show(this, err + "\n\nYou can change the paths in Qwen > Settings.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            serverState = "loading";
            SetAiStatus();
        }

        void ToggleServer()
        {
            if (server.Running)
            {
                CancelGeneration();
                server.Stop();
                serverState = "offline";
                SetAiStatus();
            }
            else if (serverState == "offline") StartServer(true);
        }

        bool downloadOffered;

        void OfferModelDownload(bool firstRun)
        {
            using (var dlg = new ModelDownloadDialog(firstRun))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.DownloadedPath != null) SwitchModel(dlg.DownloadedPath);
                else if (firstRun) ShowNote("No model installed · Qwen > Model > Download a Model");
            }
        }

        void RefreshModelMenu(ToolStripMenuItem menuItem)
        {
            var items = menuItem.DropDownItems;
            items.Clear();
            string current = Settings.Resolve(settings.ModelPath);
            foreach (string path in Settings.FindModels())
            {
                string p = path;
                double gb = new FileInfo(p).Length / (1024.0 * 1024 * 1024);
                items.Add(new ToolStripMenuItem(string.Format("{0}  ({1:0.0} GB)", FriendlyModelName(p), gb), null, delegate { SwitchModel(p); })
                {
                    Checked = string.Equals(p, current, StringComparison.OrdinalIgnoreCase),
                    ToolTipText = Path.GetFileName(p)
                });
            }
            if (items.Count == 0) items.Add(new ToolStripMenuItem("(no .gguf files in models\\)") { Enabled = false });
            items.Add(new ToolStripSeparator());
            items.Add(Item("&Download a Model...", Keys.None, delegate { OfferModelDownload(false); }));
            items.Add(Item("&Browse for a .gguf...", Keys.None, delegate
            {
                using (var dlg = new OpenFileDialog { Filter = "GGUF models (*.gguf)|*.gguf" })
                    if (dlg.ShowDialog(this) == DialogResult.OK) SwitchModel(dlg.FileName);
            }));
        }

        void SwitchModel(string path)
        {
            if (generating) CancelGeneration();
            // Keep paths inside the app folder relative so the portable folder can move.
            string app = Settings.AppDir;
            settings.ModelPath = path.StartsWith(app, StringComparison.OrdinalIgnoreCase) ? path.Substring(app.Length) : path;
            settings.Save();
            if (server.Running)
            {
                server.Stop();
                StartServer(true);
            }
            else if (serverState == "offline") StartServer(true);
            else
                MessageBox.Show(this, "The model is being served by another QwenPad window. Close it (or use Qwen > Stop Local Model there) and this window will load " + ModelName + ".",
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetAiStatus();
        }

        void UpdateServerMenu()
        {
            if (server.Running) { miServer.Text = "Stop &Local Model"; miServer.Enabled = true; }
            else { miServer.Text = "Start &Local Model"; miServer.Enabled = serverState == "offline"; }
        }

        void PollHealth()
        {
            if (healthBusy) return;
            healthBusy = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string st = llm.CheckHealth();
                SafeInvoke(() =>
                {
                    healthBusy = false;
                    // While our server process is alive but not answering yet, it is still loading.
                    if (st == "offline" && server.Running) st = "loading";
                    if (st != serverState) { serverState = st; SetAiStatus(); }
                    else if (note != null && DateTime.Now > noteUntil) SetAiStatus();
                });
            });
        }

        void ShowNote(string text)
        {
            note = text;
            noteUntil = DateTime.Now.AddSeconds(8);
            SetAiStatus();
        }

        void SetAiStatus()
        {
            if (generating) return;
            if (note != null && DateTime.Now <= noteUntil) { lblAi.Text = note; return; }
            note = null;
            switch (serverState)
            {
                case "ready": lblAi.Text = "✦ " + ModelName + " ready · Ctrl+K ask · Ctrl+Enter continue"; break;
                case "loading": lblAi.Text = "⏳ Loading " + ModelName + "…"; break;
                default:
                    lblAi.Text = server.ExitedUnexpectedly
                        ? "⚠ The local model stopped. See Qwen > Show Server Log."
                        : "○ " + ModelName + " offline · Qwen > Start Local Model";
                    break;
            }
            lblAi.ToolTipText = settings.ServerUrl;
        }

        void ShowServerLog()
        {
            if (File.Exists(ServerManager.LogPath))
                Process.Start(Application.ExecutablePath, "\"" + ServerManager.LogPath + "\"");
            else
                MessageBox.Show(this, "No server log yet. The log is written when QwenPad starts the local model.", AppName);
        }

        void ShowSettings()
        {
            using (var dlg = new SettingsDialog(settings))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                bool restart = dlg.Apply(settings);
                settings.Save();
                if (restart && server.Running &&
                    MessageBox.Show(this, "Restart the local model now to apply the new settings?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    CancelGeneration();
                    server.Stop();
                    StartServer(true);
                }
                SetAiStatus();
            }
        }

        void ShowAbout()
        {
            MessageBox.Show(this,
                AppName + " " + Application.ProductVersion + ": a lightweight Notepad with a local AI.\n" +
                "https://github.com/248Tech/QwenPad\n\n" +
                "AI: " + ModelName + " running on this PC through llama.cpp. Nothing leaves your machine.\n\n" +
                "Ctrl+K  Ask Qwen (about the selection, or write at the cursor)\n" +
                "Ctrl+Enter  Continue writing\n" +
                "Ctrl+Shift+I  Improve writing\n" +
                "Ctrl+Shift+G  Fix spelling & grammar\n" +
                "Esc  Stop generating\n\n" +
                "With nothing selected, Rewrite and Summarize use the whole document.\nEvery AI edit can be undone with Ctrl+Z.",
                "About " + AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion

        #region Form plumbing

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && generating) { CancelGeneration(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!ConfirmDiscard()) { e.Cancel = true; return; }
            CancelGeneration();
            settings.Maximized = WindowState == FormWindowState.Maximized;
            settings.Bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            settings.StatusBar = status.Visible;
            settings.Save();
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            uiTimer.Stop();
            healthTimer.Stop();
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            Theme.Changed -= OnThemeChanged;
            notes.Dispose();
            server.Dispose();
            base.OnFormClosed(e);
        }

        void SafeInvoke(Action a)
        {
            try { if (!IsDisposed && IsHandleCreated) BeginInvoke(a); }
            catch (InvalidOperationException) { }
        }

        const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        #endregion
    }
}
