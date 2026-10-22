using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace QwenPad
{
    enum SketchTool { Pen, Brush, Eraser }

    /// <summary>
    /// Freehand drawing surface for a sticky note. Ink is kept on a transparent bitmap (saved as PNG) so it sits on any
    /// note color in light or dark mode. Pen and brush strokes are drawn on a separate stroke layer first, so a
    /// semi-transparent brush stroke stays even instead of darkening where it overlaps itself.
    /// </summary>
    class SketchCanvas : Control
    {
        Bitmap ink = new Bitmap(1, 1, PixelFormat.Format32bppPArgb);
        Bitmap stroke;            // the stroke being drawn (pen/brush)
        readonly Stack<Bitmap> undo = new Stack<Bitmap>();
        Point last;
        bool down;

        public SketchTool Tool = SketchTool.Pen;
        public Color InkColor = Color.Black;
        public int SizeLevel = 1;          // 0 small, 1 medium, 2 large
        public bool Drawing;                // draw mode on: shows a dashed frame and accepts strokes
        public bool Dirty;
        public event EventHandler Changed;

        public SketchCanvas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Cursor = Cursors.Cross;
        }

        /// <summary>True when there is any ink at all.</summary>
        public bool HasInk
        {
            get
            {
                var data = ink.LockBits(new Rectangle(0, 0, ink.Width, ink.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                try
                {
                    var row = new byte[data.Stride];
                    for (int y = 0; y < ink.Height; y++)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, data.Stride);
                        for (int x = 3; x < ink.Width * 4; x += 4) if (row[x] != 0) return true;
                    }
                    return false;
                }
                finally { ink.UnlockBits(data); }
            }
        }

        float PenWidth
        {
            get
            {
                switch (Tool)
                {
                    case SketchTool.Brush: return new[] { 7f, 13f, 22f }[SizeLevel];
                    case SketchTool.Eraser: return new[] { 10f, 20f, 34f }[SizeLevel];
                    default: return new[] { 1.6f, 2.8f, 4.5f }[SizeLevel];
                }
            }
        }

        public void LoadFrom(string path)
        {
            using (var ms = new MemoryStream(File.ReadAllBytes(path)))
            using (var img = Image.FromStream(ms))
            {
                var b = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(b)) g.DrawImage(img, 0, 0, img.Width, img.Height);
                ink.Dispose();
                ink = b;
            }
            EnsureSize();
            Invalidate();
        }

        public void SaveTo(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var b = new Bitmap(ink.Width, ink.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(b)) g.DrawImage(ink, 0, 0);
                b.Save(path, ImageFormat.Png);
            }
            Dirty = false;
        }

        public void Clear()
        {
            PushUndo();
            using (var g = Graphics.FromImage(ink)) g.Clear(Color.Transparent);
            Touched();
        }

        public bool Undo()
        {
            if (undo.Count == 0) return false;
            ink.Dispose();
            ink = undo.Pop();
            EnsureSize();
            Touched();
            return true;
        }

        void PushUndo()
        {
            undo.Push((Bitmap)ink.Clone());
            if (undo.Count > 20)
            {
                // Drop the oldest snapshot.
                var keep = undo.ToArray();
                undo.Clear();
                for (int i = Math.Min(keep.Length, 20) - 1; i >= 0; i--) undo.Push(keep[i]);
                for (int i = 20; i < keep.Length; i++) keep[i].Dispose();
            }
        }

        void Touched()
        {
            Dirty = true;
            Invalidate();
            var h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }

        /// <summary>Grows the ink bitmap when the canvas gets bigger (never crops existing ink).</summary>
        void EnsureSize()
        {
            int w = Math.Max(ink.Width, ClientSize.Width), h = Math.Max(ink.Height, ClientSize.Height);
            if (w == ink.Width && h == ink.Height) return;
            var b = new Bitmap(Math.Max(1, w), Math.Max(1, h), PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(b)) g.DrawImage(ink, 0, 0);
            ink.Dispose();
            ink = b;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            EnsureSize();
        }

        Pen MakePen(Color c)
        {
            return new Pen(c, PenWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (!Drawing || e.Button != MouseButtons.Left) return;
            EnsureSize();
            PushUndo();
            down = true;
            last = e.Location;
            if (Tool != SketchTool.Eraser)
            {
                if (stroke != null) stroke.Dispose();
                stroke = new Bitmap(ink.Width, ink.Height, PixelFormat.Format32bppPArgb);
            }
            Segment(e.Location, e.Location);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!down) return;
            Segment(last, e.Location);
            last = e.Location;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!down) return;
            down = false;
            if (stroke != null)
            {
                using (var g = Graphics.FromImage(ink)) DrawStroke(g);
                stroke.Dispose();
                stroke = null;
            }
            Touched();
        }

        void Segment(Point a, Point b)
        {
            var target = Tool == SketchTool.Eraser ? ink : stroke;
            using (var g = Graphics.FromImage(target))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (Tool == SketchTool.Eraser) g.CompositingMode = CompositingMode.SourceCopy;
                using (var pen = MakePen(Tool == SketchTool.Eraser ? Color.Transparent : Color.FromArgb(255, InkColor)))
                {
                    if (a == b) g.DrawLine(pen, a.X, a.Y, b.X + 0.1f, b.Y + 0.1f);   // a dot
                    else g.DrawLine(pen, a, b);
                }
            }
            float r = PenWidth / 2 + 2;
            Invalidate(Rectangle.Round(RectangleF.FromLTRB(Math.Min(a.X, b.X) - r, Math.Min(a.Y, b.Y) - r, Math.Max(a.X, b.X) + r, Math.Max(a.Y, b.Y) + r)));
        }

        /// <summary>Composites the stroke layer: opaque for the pen, ~60% for the brush.</summary>
        void DrawStroke(Graphics g)
        {
            if (stroke == null) return;
            if (Tool == SketchTool.Brush)
            {
                var cm = new ColorMatrix { Matrix33 = 0.6f };
                using (var ia = new ImageAttributes())
                {
                    ia.SetColorMatrix(cm);
                    g.DrawImage(stroke, new Rectangle(0, 0, stroke.Width, stroke.Height), 0, 0, stroke.Width, stroke.Height, GraphicsUnit.Pixel, ia);
                }
            }
            else g.DrawImage(stroke, 0, 0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.DrawImage(ink, 0, 0);
            DrawStroke(e.Graphics);
            if (Drawing)
                using (var pen = new Pen(Color.FromArgb(90, ForeColor)) { DashStyle = DashStyle.Dash })
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ink.Dispose();
                if (stroke != null) stroke.Dispose();
                foreach (var b in undo) b.Dispose();
                undo.Clear();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>The note's sketch canvas also lets edge clicks through for resizing and forwards Ctrl+wheel.</summary>
    sealed class EdgeSketchCanvas : SketchCanvas
    {
        protected override void WndProc(ref Message m) { if (!StickyNote.PassNoteMessage(this, ref m)) base.WndProc(ref m); }
    }

    /// <summary>16 px tool icons drawn at runtime (no image files needed).</summary>
    static class SketchIcons
    {
        public static Bitmap Pen(Color c)
        {
            var b = new Bitmap(18, 18);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var p = new Pen(c, 2.2f) { StartCap = LineCap.Triangle, EndCap = LineCap.Round }) g.DrawLine(p, 4, 14, 13, 5);
                using (var p = new Pen(c, 1f)) g.DrawLine(p, 3, 15, 5, 13);
            }
            return b;
        }

        public static Bitmap Brush(Color c)
        {
            var b = new Bitmap(18, 18);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var p = new Pen(c, 2f)) g.DrawLine(p, 9, 9, 15, 3);                           // handle
                using (var br = new SolidBrush(c)) g.FillEllipse(br, 3, 9, 7, 7);                   // bristles
            }
            return b;
        }

        public static Bitmap Eraser(Color c)
        {
            var b = new Bitmap(18, 18);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TranslateTransform(9, 9);
                g.RotateTransform(-40);
                using (var p = new Pen(c, 1.4f)) g.DrawRectangle(p, -7, -3.5f, 14, 7);
                using (var br = new SolidBrush(Color.FromArgb(150, c))) g.FillRectangle(br, -7, -3.5f, 5, 7);
            }
            return b;
        }

        public static Bitmap Dot(Color c, int size)
        {
            var b = new Bitmap(18, 18);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var br = new SolidBrush(c)) g.FillEllipse(br, 9 - size / 2f, 9 - size / 2f, size, size);
            }
            return b;
        }

        public static Bitmap Swatch(Color c, Color border)
        {
            var b = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var br = new SolidBrush(c)) g.FillEllipse(br, 2, 2, 12, 12);
                using (var p = new Pen(border, 1f)) g.DrawEllipse(p, 2, 2, 12, 12);
            }
            return b;
        }
    }
}
