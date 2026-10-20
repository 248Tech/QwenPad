using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QwenPad
{
    /// <summary>
    /// Light/dark theming. Uses the OS for what it can (title bar, scrollbars) and a color table for menus.
    /// Mode is "system", "light" or "dark".
    /// </summary>
    static class Theme
    {
        public static bool Dark { get; private set; }
        public static string Mode { get; private set; }
        public static event EventHandler Changed;

        public static Color EditorBack { get { return Dark ? Color.FromArgb(30, 30, 30) : SystemColors.Window; } }
        public static Color EditorFore { get { return Dark ? Color.FromArgb(212, 212, 212) : SystemColors.WindowText; } }
        public static Color Surface { get { return Dark ? Color.FromArgb(43, 43, 43) : SystemColors.Control; } }
        public static Color SurfaceFore { get { return Dark ? Color.FromArgb(230, 230, 230) : SystemColors.ControlText; } }
        public static Color InputBack { get { return Dark ? Color.FromArgb(30, 30, 30) : SystemColors.Window; } }
        public static Color ButtonBack { get { return Dark ? Color.FromArgb(55, 55, 55) : SystemColors.Control; } }
        public static Color Border { get { return Dark ? Color.FromArgb(80, 80, 80) : SystemColors.ControlDark; } }

        static Theme() { Mode = "system"; }

        public static void SetMode(string mode)
        {
            Mode = mode == "dark" || mode == "light" ? mode : "system";
            Refresh(true);
        }

        /// <summary>Re-evaluates the theme (e.g. after the Windows setting changes).</summary>
        public static void Refresh(bool force)
        {
            bool dark = Mode == "dark" || (Mode == "system" && SystemPrefersDark());
            if (dark == Dark && !force) return;
            Dark = dark;
            ToolStripManager.Renderer = dark ? (ToolStripRenderer)new DarkRenderer() : new ToolStripProfessionalRenderer();
            var h = Changed;
            if (h != null) h(null, EventArgs.Empty);
        }

        static bool SystemPrefersDark()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k != null ? k.GetValue("AppsUseLightTheme") : null;
                    return v is int && (int)v == 0;
                }
            }
            catch { return false; }
        }

        /// <summary>Dark or light title bar for a top-level window.</summary>
        public static void ApplyWindow(Form f)
        {
            if (!f.IsHandleCreated) return;
            int on = Dark ? 1 : 0;
            try
            {
                // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE (Win10 20H1+); 19 on earlier Win10 builds.
                if (DwmSetWindowAttribute(f.Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref on, 4);
                SetWindowPos(f.Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020);   // repaint the frame
                // Windows 10 only repaints the caption on an activation change, so toggle it and restore the real state.
                bool active = GetForegroundWindow() == f.Handle;
                SendMessage(f.Handle, 0x0086, (IntPtr)(active ? 0 : 1), IntPtr.Zero);
                SendMessage(f.Handle, 0x0086, (IntPtr)(active ? 1 : 0), IntPtr.Zero);
            }
            catch { }
        }

        /// <summary>Dark or light scrollbars on a scrollable control.</summary>
        public static void ApplyScrollbars(Control c)
        {
            if (!c.IsHandleCreated) return;
            try { SetWindowTheme(c.Handle, Dark ? "DarkMode_Explorer" : "Explorer", null); } catch { }
        }

        /// <summary>Themes a dialog: title bar plus every child control.</summary>
        public static void ApplyForm(Form f)
        {
            ApplyWindow(f);
            f.BackColor = Surface;
            f.ForeColor = SurfaceFore;
            ApplyControls(f);
        }

        static void ApplyControls(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is TextBox || c is NumericUpDown || c is ComboBox)
                {
                    c.BackColor = InputBack;
                    c.ForeColor = EditorFore;
                    if (c is TextBox) ((TextBox)c).BorderStyle = Dark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                }
                else if (c is Button)
                {
                    var b = (Button)c;
                    if (Dark)
                    {
                        b.FlatStyle = FlatStyle.Flat;
                        b.FlatAppearance.BorderColor = Border;
                        b.BackColor = ButtonBack;
                        b.ForeColor = SurfaceFore;
                    }
                    else
                    {
                        b.FlatStyle = FlatStyle.Standard;
                        b.BackColor = SystemColors.Control;
                        b.ForeColor = SystemColors.ControlText;
                        b.UseVisualStyleBackColor = true;
                    }
                }
                else
                {
                    c.BackColor = Surface;
                    c.ForeColor = SurfaceFore;
                }
                if (c.HasChildren) ApplyControls(c);
            }
        }

        sealed class DarkColors : ProfessionalColorTable
        {
            static readonly Color Bg = Color.FromArgb(43, 43, 43), Drop = Color.FromArgb(37, 37, 38),
                Sel = Color.FromArgb(65, 65, 68), Line = Color.FromArgb(70, 70, 70), Check = Color.FromArgb(70, 70, 95);
            public override Color MenuStripGradientBegin { get { return Bg; } }
            public override Color MenuStripGradientEnd { get { return Bg; } }
            public override Color StatusStripGradientBegin { get { return Bg; } }
            public override Color StatusStripGradientEnd { get { return Bg; } }
            public override Color ToolStripGradientBegin { get { return Bg; } }
            public override Color ToolStripGradientMiddle { get { return Bg; } }
            public override Color ToolStripGradientEnd { get { return Bg; } }
            public override Color ToolStripBorder { get { return Bg; } }
            public override Color ToolStripDropDownBackground { get { return Drop; } }
            public override Color ImageMarginGradientBegin { get { return Drop; } }
            public override Color ImageMarginGradientMiddle { get { return Drop; } }
            public override Color ImageMarginGradientEnd { get { return Drop; } }
            public override Color MenuItemSelected { get { return Sel; } }
            public override Color MenuItemSelectedGradientBegin { get { return Sel; } }
            public override Color MenuItemSelectedGradientEnd { get { return Sel; } }
            public override Color MenuItemPressedGradientBegin { get { return Drop; } }
            public override Color MenuItemPressedGradientMiddle { get { return Drop; } }
            public override Color MenuItemPressedGradientEnd { get { return Drop; } }
            public override Color MenuItemBorder { get { return Sel; } }
            public override Color MenuBorder { get { return Line; } }
            public override Color SeparatorDark { get { return Line; } }
            public override Color SeparatorLight { get { return Drop; } }
            public override Color CheckBackground { get { return Check; } }
            public override Color CheckSelectedBackground { get { return Check; } }
            public override Color CheckPressedBackground { get { return Check; } }
            public override Color ButtonSelectedHighlight { get { return Sel; } }
            public override Color ButtonSelectedGradientBegin { get { return Sel; } }
            public override Color ButtonSelectedGradientMiddle { get { return Sel; } }
            public override Color ButtonSelectedGradientEnd { get { return Sel; } }
            public override Color GripDark { get { return Line; } }
            public override Color GripLight { get { return Bg; } }
        }

        sealed class DarkRenderer : ToolStripProfessionalRenderer
        {
            public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? Color.FromArgb(230, 230, 230) : Color.FromArgb(115, 115, 115);
                base.OnRenderItemText(e);
            }

            // Status-bar cell dividers: a thin gray line instead of the bright etched 3D border.
            protected override void OnRenderLabelBackground(ToolStripItemRenderEventArgs e)
            {
                var lbl = e.Item as ToolStripStatusLabel;
                if (lbl == null || lbl.BorderSides == ToolStripStatusLabelBorderSides.None) { base.OnRenderLabelBackground(e); return; }
                using (var pen = new Pen(Color.FromArgb(75, 75, 75)))
                    e.Graphics.DrawLine(pen, 0, 3, 0, e.Item.Height - 4);
            }

            protected override void OnRenderStatusStripSizingGrip(ToolStripRenderEventArgs e) { }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                if (!(e.ToolStrip is StatusStrip)) { base.OnRenderToolStripBorder(e); return; }
                using (var pen = new Pen(Color.FromArgb(60, 60, 60))) e.Graphics.DrawLine(pen, 0, 0, e.ToolStrip.Width, 0);
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = Color.FromArgb(200, 200, 200);
                base.OnRenderArrow(e);
            }

            // The stock check glyph is black; draw a light one instead.
            protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
            {
                var r = e.ImageRectangle;
                using (var bg = new SolidBrush(Color.FromArgb(70, 70, 95))) e.Graphics.FillRectangle(bg, r.X - 2, r.Y - 2, r.Width + 4, r.Height + 4);
                if (e.Item.Image != null) return;
                var old = e.Graphics.SmoothingMode;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(235, 235, 235), 2f))
                    e.Graphics.DrawLines(pen, new[]
                    {
                        new PointF(r.Left + r.Width * 0.18f, r.Top + r.Height * 0.52f),
                        new PointF(r.Left + r.Width * 0.42f, r.Top + r.Height * 0.76f),
                        new PointF(r.Left + r.Width * 0.84f, r.Top + r.Height * 0.26f)
                    });
                e.Graphics.SmoothingMode = old;
            }
        }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    }
}
