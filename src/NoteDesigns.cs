using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QwenPad
{
    /// <summary>
    /// Fall-themed sticky-note backgrounds, drawn in code so they scale to any note size and ship no image files.
    /// Design 0 is "none" (the plain note color). Each design brings its own text colors and content insets.
    /// </summary>
    static class NoteDesigns
    {
        public static readonly string[] Names = { "None (plain color)", "Spooky Night", "Pumpkin Patch", "Moonlit Ghost", "Harvest Moon" };
        public const int Count = 5;

        public sealed class Style
        {
            public Color Text, Muted, HeaderFore, Hover;
            public int SideInset, BottomInset;   // keeps text on the "paper" part of the design
        }

        public static Style StyleOf(int design)
        {
            switch (design)
            {
                case 1: return new Style { Text = Hex(0x3A2A1E), Muted = Hex(0x8C6D55), HeaderFore = Hex(0xF3D7B8), Hover = Color.FromArgb(60, 255, 255, 255), SideInset = 20, BottomInset = 20 };
                case 2: return new Style { Text = Hex(0x3B1D0A), Muted = Hex(0x7A3E17), HeaderFore = Hex(0x3B1D0A), Hover = Color.FromArgb(50, 0, 0, 0), SideInset = 10, BottomInset = 14 };
                case 3: return new Style { Text = Hex(0x2A2638), Muted = Hex(0x7B7590), HeaderFore = Hex(0xE9C46A), Hover = Color.FromArgb(60, 255, 255, 255), SideInset = 26, BottomInset = 26 };
                case 4: return new Style { Text = Hex(0x1F3A3D), Muted = Hex(0x3F6D70), HeaderFore = Hex(0x1F3A3D), Hover = Color.FromArgb(50, 0, 0, 0), SideInset = 10, BottomInset = 14 };
                default: return null;
            }
        }

        /// <summary>Renders a design. body is the text area (for ruled lines), lineHeight its line spacing.</summary>
        public static Bitmap Render(int design, Size size, int headerHeight, Rectangle body, int lineHeight)
        {
            var bmp = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height));
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, size.Width, size.Height);
                switch (design)
                {
                    case 1: SpookyNight(g, r, headerHeight, body, lineHeight); break;
                    case 2: PumpkinPatch(g, r, headerHeight); break;
                    case 3: MoonlitGhost(g, r, headerHeight); break;
                    case 4: HarvestMoon(g, r, body, lineHeight); break;
                }
            }
            return bmp;
        }

        /// <summary>Small preview for menus (16 px) and larger ones for the Notes List.</summary>
        public static Bitmap Thumbnail(int design, int w, int h)
        {
            // Render at note size, then scale down, so small previews look like the real thing.
            int rw = Math.Max(w, 240), rh = Math.Max(h, (int)(240.0 * h / Math.Max(1, w)));
            using (var big = Render(design, new Size(rw, rh), 31, Rectangle.FromLTRB(24, 60, rw - 24, rh - 40), 22))
            {
                var bmp = new Bitmap(Math.Max(1, w), Math.Max(1, h));
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(big, 0, 0, bmp.Width, bmp.Height);
                }
                return bmp;
            }
        }

        // ---------------------------------------------------------------- 1. Spooky Night

        static void SpookyNight(Graphics g, Rectangle r, int headerH, Rectangle body, int lineH)
        {
            using (var bg = new SolidBrush(Hex(0x171414))) g.FillRectangle(bg, r);
            using (var lattice = new Pen(Color.FromArgb(16, 255, 255, 255)))
                for (int d = -r.Height; d < r.Width; d += 14)
                {
                    g.DrawLine(lattice, d, 0, d + r.Height, r.Height);
                    g.DrawLine(lattice, d + r.Height, 0, d, r.Height);
                }

            var paper = Rectangle.FromLTRB(12, headerH - 2, r.Width - 12, r.Height - 12);
            var rnd = new Random(1031);
            // Stars and dots on the frame only.
            using (var star = new SolidBrush(Hex(0xF0A040)))
            using (var dot = new SolidBrush(Color.FromArgb(200, Hex(0xF6C27A))))
                for (int i = 0; i < r.Width * r.Height / 900; i++)
                {
                    int x = rnd.Next(r.Width), y = rnd.Next(r.Height);
                    if (paper.Contains(x, y)) continue;
                    if (rnd.Next(3) == 0) Star(g, star, x, y, 2.5f + (float)rnd.NextDouble() * 2.5f);
                    else g.FillEllipse(dot, x, y, 1.6f, 1.6f);
                }
            // Little pumpkins on the frame sides.
            float ps = 11;
            Pumpkin(g, 1, paper.Top + paper.Height * 0.35f, ps);
            Pumpkin(g, r.Width - ps - 1, paper.Top + paper.Height * 0.62f, ps);
            Pumpkin(g, paper.Left + paper.Width * 0.30f, r.Height - ps - 1, ps);

            using (var path = Scalloped(paper, 9))
            {
                using (var fill = new SolidBrush(Hex(0xFBEFE3))) g.FillPath(fill, path);
                using (var edge = new Pen(Hex(0xE08A2E), 1.8f)) g.DrawPath(edge, path);
            }
            var inner = Rectangle.Inflate(paper, -5, -5);
            using (var path = Scalloped(inner, 7))
            using (var edge = new Pen(Color.FromArgb(90, Hex(0x8A4A1C)), 1f)) g.DrawPath(edge, path);

            // Dotted ruled lines aligned to the text.
            using (var line = new Pen(Color.FromArgb(150, Hex(0xC9925C)), 1f) { DashStyle = DashStyle.Dot })
                RuledLines(g, line, body, lineH, Math.Max(paper.Left + 8, body.Left), Math.Min(paper.Right - 8, body.Right));

            // A friendly ghost in a witch hat hugging a pumpkin, bottom-right.
            float gs = Math.Min(88, Math.Min(r.Width, r.Height) * 0.30f);
            Ghost(g, r.Width - gs * 1.05f - 4, r.Height - gs * 1.15f - 4, gs, true, Hex(0xFFFFFF), Hex(0x2B2222));
        }

        // ---------------------------------------------------------------- 2. Pumpkin Patch

        static void PumpkinPatch(Graphics g, Rectangle r, int headerH)
        {
            using (var bg = new LinearGradientBrush(r, Hex(0xF5893A), Hex(0xE8692A), 60f)) g.FillRectangle(bg, r);
            var rnd = new Random(2207);
            Color[] confetti = { Color.FromArgb(200, 255, 255, 255), Color.FromArgb(220, Hex(0xFFD15C)), Color.FromArgb(170, Hex(0x5A2A10)) };
            for (int i = 0; i < r.Width * r.Height / 700; i++)
            {
                var c = confetti[rnd.Next(confetti.Length)];
                float x = rnd.Next(r.Width), y = rnd.Next(r.Height);
                switch (rnd.Next(3))
                {
                    case 0: using (var b = new SolidBrush(c)) g.FillEllipse(b, x, y, 2.4f, 2.4f); break;
                    case 1: using (var p = new Pen(c, 1f)) g.DrawEllipse(p, x, y, 4.5f, 4.5f); break;
                    default:
                        using (var p = new Pen(c, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        {
                            double a = rnd.NextDouble() * Math.PI;
                            g.DrawLine(p, x, y, x + (float)Math.Cos(a) * 5, y + (float)Math.Sin(a) * 5);
                        }
                        break;
                }
            }
            // Faint jack-o'-lantern watermark, bottom-right.
            float js = Math.Min(r.Width, r.Height) * 0.55f;
            var st = g.Save();
            using (var veil = new SolidBrush(Color.FromArgb(46, Hex(0x7A2E05))))
                JackFace(g, r.Width - js * 0.92f, r.Height - js * 0.86f, js, veil);
            g.Restore(st);
            // Cobweb in the top-left corner, under the header.
            Cobweb(g, new PointF(0, headerH), Math.Min(70, r.Width * 0.28f), Color.FromArgb(200, 255, 255, 255));
            // Spider dangling top-right.
            float sx = r.Width - 34, sy = headerH + Math.Min(46, r.Height * 0.18f);
            using (var thread = new Pen(Color.FromArgb(200, 255, 255, 255), 1f)) g.DrawLine(thread, sx, headerH, sx, sy);
            Spider(g, sx, sy, 7, Hex(0x231208));
        }

        // ---------------------------------------------------------------- 3. Moonlit Ghost

        static void MoonlitGhost(Graphics g, Rectangle r, int headerH)
        {
            using (var bg = new SolidBrush(Hex(0x15131D))) g.FillRectangle(bg, r);
            var rnd = new Random(3313);
            Color[] colors = { Hex(0xE9C46A), Hex(0xE9C46A), Hex(0xE9C46A), Hex(0xF2A3A0), Hex(0x9BC58B), Hex(0xF08A4B) };
            for (int i = 0; i < r.Width * r.Height / 600; i++)
            {
                float x = rnd.Next(r.Width), y = rnd.Next(r.Height);
                var c = colors[rnd.Next(colors.Length)];
                using (var b = new SolidBrush(c))
                {
                    if (rnd.Next(4) == 0) Star(g, b, x, y, 2.5f + (float)rnd.NextDouble() * 3.5f);
                    else g.FillEllipse(b, x, y, 1.5f, 1.5f);
                }
            }
            // Crescent moon top-right.
            float m = Math.Min(30, r.Width * 0.11f);
            Crescent(g, r.Width - m * 1.6f, headerH + 6, m, Hex(0xE9C46A), Hex(0x15131D));
            // The big pale ghost you write on: a flat dome that starts above the title so every line sits on the ghost.
            var gr = RectangleF.FromLTRB(r.Width * 0.06f, headerH - 14, r.Width * 0.94f, r.Height - 6);
            using (var path = GhostShape(gr, Math.Min(26f, gr.Height * 0.2f)))
            {
                using (var fill = new SolidBrush(Hex(0xEEEBF5))) g.FillPath(fill, path);
                using (var shade = new Pen(Color.FromArgb(70, Hex(0x8F88A8)), 2f)) g.DrawPath(shade, path);
            }
        }

        // ---------------------------------------------------------------- 4. Harvest Moon

        static void HarvestMoon(Graphics g, Rectangle r, Rectangle body, int lineH)
        {
            using (var bg = new SolidBrush(Hex(0x9EE3DE))) g.FillRectangle(bg, r);
            var rnd = new Random(4421);
            using (var tex = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                for (int i = 0; i < r.Width * r.Height / 40; i++) g.FillRectangle(tex, rnd.Next(r.Width), rnd.Next(r.Height), 1, 1);
            // Full moon, bottom-right, with a howling wolf in front of it.
            float ms = Math.Min(r.Width, r.Height) * 0.48f;
            var moon = new RectangleF(r.Width - ms * 0.78f, r.Height - ms * 0.78f, ms, ms);
            using (var b = new SolidBrush(Color.FromArgb(245, 255, 255, 255))) g.FillEllipse(b, moon);
            using (var crater = new SolidBrush(Color.FromArgb(28, Hex(0x5A6F7A))))
            {
                g.FillEllipse(crater, moon.Left + ms * 0.22f, moon.Top + ms * 0.20f, ms * 0.16f, ms * 0.13f);
                g.FillEllipse(crater, moon.Left + ms * 0.50f, moon.Top + ms * 0.42f, ms * 0.10f, ms * 0.09f);
            }
            // Coral hand-drawn ruled lines aligned to the text (drawn before the wolf so it sits on top).
            using (var line = new Pen(Color.FromArgb(210, Hex(0xF07A5C)), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                RuledLinesWobbly(g, line, body, lineH, body.Left + 2, body.Right - 2, rnd, moon);
            Wolf(g, new RectangleF(moon.Left - ms * 0.12f, moon.Top + ms * 0.05f, ms * 0.66f, ms * 0.66f), Hex(0x3E4A56));
            // A few falling leaves.
            Leaf(g, r.Width * 0.82f, body.Top + 4, 9, 25, Hex(0xE8692A));
            Leaf(g, r.Width * 0.90f, body.Top + 26, 7, -35, Hex(0xC9462E));
        }

        // ---------------------------------------------------------------- shapes

        static void RuledLines(Graphics g, Pen pen, Rectangle body, int lineH, float x1, float x2)
        {
            if (lineH < 6) return;
            for (float y = body.Top + lineH - 2; y < body.Bottom; y += lineH) g.DrawLine(pen, x1, y, x2, y);
        }

        static void RuledLinesWobbly(Graphics g, Pen pen, Rectangle body, int lineH, float x1, float x2, Random rnd, RectangleF avoid)
        {
            if (lineH < 6) return;
            int n = 0;
            for (float y = body.Top + lineH - 2; y < body.Bottom; y += lineH, n++)
            {
                float end = x2;
                if (y > avoid.Top + avoid.Height * 0.15f) end = Math.Min(x2, avoid.Left - 6);   // stop short of the moon
                if (end - x1 < 30) continue;
                var pts = new PointF[8];
                for (int i = 0; i < pts.Length; i++)
                    pts[i] = new PointF(x1 + (end - x1) * i / (pts.Length - 1), y + (float)(rnd.NextDouble() - 0.5) * 1.6f);
                g.DrawCurve(pen, pts, 0.4f);
            }
        }

        static void Star(Graphics g, Brush b, float cx, float cy, float rOuter)
        {
            var pts = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5;
                float rr = i % 2 == 0 ? rOuter : rOuter * 0.45f;
                pts[i] = new PointF(cx + (float)Math.Cos(a) * rr, cy + (float)Math.Sin(a) * rr);
            }
            g.FillPolygon(b, pts);
        }

        static void Pumpkin(Graphics g, float x, float y, float s)
        {
            using (var body = new SolidBrush(Hex(0xF08A2E)))
            using (var rib = new Pen(Color.FromArgb(120, Hex(0xB05310)), 0.8f))
            using (var stem = new SolidBrush(Hex(0x5E7A2C)))
            using (var face = new SolidBrush(Hex(0x3A1A08)))
            {
                g.FillEllipse(body, x, y + s * 0.15f, s, s * 0.82f);
                g.DrawEllipse(rib, x + s * 0.28f, y + s * 0.15f, s * 0.44f, s * 0.82f);
                g.FillRectangle(stem, x + s * 0.45f, y, s * 0.12f, s * 0.22f);
                g.FillPolygon(face, new[] { new PointF(x + s * 0.28f, y + s * 0.42f), new PointF(x + s * 0.40f, y + s * 0.42f), new PointF(x + s * 0.34f, y + s * 0.32f) });
                g.FillPolygon(face, new[] { new PointF(x + s * 0.60f, y + s * 0.42f), new PointF(x + s * 0.72f, y + s * 0.42f), new PointF(x + s * 0.66f, y + s * 0.32f) });
                g.FillPie(face, x + s * 0.30f, y + s * 0.45f, s * 0.40f, s * 0.25f, 0, 180);
            }
        }

        static void JackFace(Graphics g, float x, float y, float s, Brush ink)
        {
            using (var outline = new Pen(ink, s * 0.035f))
            {
                g.DrawEllipse(outline, x, y + s * 0.12f, s, s * 0.80f);
                g.DrawEllipse(outline, x + s * 0.25f, y + s * 0.12f, s * 0.50f, s * 0.80f);
                g.DrawLine(outline, x + s * 0.5f, y + s * 0.13f, x + s * 0.55f, y);
            }
            g.FillPolygon(ink, new[] { new PointF(x + s * 0.24f, y + s * 0.45f), new PointF(x + s * 0.40f, y + s * 0.45f), new PointF(x + s * 0.32f, y + s * 0.30f) });
            g.FillPolygon(ink, new[] { new PointF(x + s * 0.60f, y + s * 0.45f), new PointF(x + s * 0.76f, y + s * 0.45f), new PointF(x + s * 0.68f, y + s * 0.30f) });
            using (var mouth = new GraphicsPath())
            {
                mouth.AddLines(new[]
                {
                    new PointF(x + s * 0.22f, y + s * 0.56f), new PointF(x + s * 0.34f, y + s * 0.62f), new PointF(x + s * 0.40f, y + s * 0.56f),
                    new PointF(x + s * 0.50f, y + s * 0.64f), new PointF(x + s * 0.60f, y + s * 0.56f), new PointF(x + s * 0.66f, y + s * 0.62f),
                    new PointF(x + s * 0.78f, y + s * 0.56f), new PointF(x + s * 0.66f, y + s * 0.74f), new PointF(x + s * 0.34f, y + s * 0.74f)
                });
                mouth.CloseFigure();
                g.FillPath(ink, mouth);
            }
        }

        static GraphicsPath GhostShape(RectangleF r) { return GhostShape(r, Math.Min(r.Width / 2, r.Height * 0.45f)); }

        /// <summary>Ghost outline: a dome of the given height (flatter when smaller), straight sides and a wavy hem.</summary>
        static GraphicsPath GhostShape(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float waves = 4, waveW = r.Width / waves, waveH = Math.Min(14, r.Height * 0.06f);
            p.AddArc(r.Left, r.Top, r.Width, radius * 2, 180, 180);
            p.AddLine(r.Right, r.Top + radius, r.Right, r.Bottom - waveH);
            for (int i = 0; i < waves; i++)
            {
                float x0 = r.Right - i * waveW;
                p.AddBezier(x0, r.Bottom - waveH, x0 - waveW * 0.25f, r.Bottom + waveH * 0.6f, x0 - waveW * 0.75f, r.Bottom + waveH * 0.6f, x0 - waveW, r.Bottom - waveH);
            }
            p.CloseFigure();
            return p;
        }

        /// <summary>Small cute ghost (optionally in a witch hat) hugging a pumpkin.</summary>
        static void Ghost(Graphics g, float x, float y, float s, bool hat, Color body, Color ink)
        {
            var r = new RectangleF(x, y + s * 0.30f, s * 0.80f, s * 0.80f);
            using (var shape = GhostShape(r))
            {
                using (var shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) { var m = new Matrix(); m.Translate(2, 3); var sh = (GraphicsPath)shape.Clone(); sh.Transform(m); g.FillPath(shadow, sh); }
                using (var fill = new SolidBrush(body)) g.FillPath(fill, shape);
                using (var edge = new Pen(Color.FromArgb(90, ink), 1f)) g.DrawPath(edge, shape);
            }
            float cx = r.Left + r.Width / 2, ey = r.Top + r.Width * 0.30f, e = s * 0.045f;
            using (var b = new SolidBrush(ink))
            {
                g.FillEllipse(b, cx - s * 0.13f - e, ey, e * 2, e * 2.4f);
                g.FillEllipse(b, cx + s * 0.13f - e, ey, e * 2, e * 2.4f);
                g.FillPie(b, cx - s * 0.06f, ey + e * 2.4f, s * 0.12f, s * 0.09f, 0, 180);
            }
            using (var cheek = new SolidBrush(Color.FromArgb(120, Hex(0xF5A08A))))
            {
                g.FillEllipse(cheek, cx - s * 0.25f, ey + e * 2.6f, e * 2.4f, e * 1.4f);
                g.FillEllipse(cheek, cx + s * 0.25f - e * 2.4f, ey + e * 2.6f, e * 2.4f, e * 1.4f);
            }
            if (hat)
            {
                using (var hb = new SolidBrush(Hex(0x262020)))
                using (var band = new SolidBrush(Hex(0xC9762B)))
                {
                    // Cone with a tip that flops over to the right, on a wide brim.
                    float baseY = r.Top + s * 0.03f;
                    g.FillPolygon(hb, new[]
                    {
                        new PointF(cx - s * 0.21f, baseY), new PointF(cx - s * 0.02f, y - s * 0.02f),
                        new PointF(cx + s * 0.08f, y - s * 0.08f), new PointF(cx + s * 0.27f, y + s * 0.04f),
                        new PointF(cx + s * 0.08f, y + s * 0.02f), new PointF(cx + s * 0.21f, baseY)
                    });
                    g.FillEllipse(hb, cx - s * 0.38f, baseY - s * 0.05f, s * 0.76f, s * 0.13f);
                    g.FillRectangle(band, cx - s * 0.20f, baseY - s * 0.07f, s * 0.40f, s * 0.05f);
                }
            }
            Pumpkin(g, cx - s * 0.20f, r.Top + r.Height * 0.52f, s * 0.40f);
        }

        static GraphicsPath Scalloped(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            if (r.Width < radius * 4 || r.Height < radius * 4) { p.AddRectangle(new Rectangle(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height))); return p; }
            int d = radius * 2;
            int nx = Math.Max(2, r.Width / d), ny = Math.Max(2, r.Height / d);
            float sx = r.Width / (float)nx, sy = r.Height / (float)ny;
            for (int i = 0; i < nx; i++) p.AddArc(r.Left + i * sx, r.Top - sy * 0.0f - radius * 0.6f, sx, radius * 1.4f, 180, 180);
            for (int i = 0; i < ny; i++) p.AddArc(r.Right - radius * 0.8f, r.Top + i * sy, radius * 1.4f, sy, 270, 180);
            for (int i = nx - 1; i >= 0; i--) p.AddArc(r.Left + i * sx, r.Bottom - radius * 0.8f, sx, radius * 1.4f, 0, 180);
            for (int i = ny - 1; i >= 0; i--) p.AddArc(r.Left - radius * 0.6f, r.Top + i * sy, radius * 1.4f, sy, 90, 180);
            p.CloseFigure();
            return p;
        }

        static void Cobweb(Graphics g, PointF corner, float size, Color c)
        {
            using (var pen = new Pen(c, 1f))
            {
                const int spokes = 6;
                var ends = new PointF[spokes];
                for (int i = 0; i < spokes; i++)
                {
                    double a = i * (Math.PI / 2) / (spokes - 1);
                    ends[i] = new PointF(corner.X + (float)Math.Cos(a) * size, corner.Y + (float)Math.Sin(a) * size);
                    g.DrawLine(pen, corner, ends[i]);
                }
                for (int ring = 1; ring <= 4; ring++)
                {
                    float t = ring / 4.5f;
                    var pts = new PointF[spokes];
                    for (int i = 0; i < spokes; i++) pts[i] = new PointF(corner.X + (ends[i].X - corner.X) * t, corner.Y + (ends[i].Y - corner.Y) * t);
                    for (int i = 0; i < spokes - 1; i++)
                    {
                        var mid = new PointF((pts[i].X + pts[i + 1].X) / 2 - (pts[i + 1].Y - pts[i].Y) * -0.12f, (pts[i].Y + pts[i + 1].Y) / 2 + (pts[i + 1].X - pts[i].X) * -0.12f);
                        g.DrawCurve(pen, new[] { pts[i], mid, pts[i + 1] }, 0.6f);
                    }
                }
            }
        }

        static void Spider(Graphics g, float x, float y, float s, Color c)
        {
            using (var b = new SolidBrush(c))
            using (var legs = new Pen(c, 1.1f))
            {
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 4; i++)
                    {
                        float ly = y - s * 0.3f + i * s * 0.25f;
                        g.DrawLines(legs, new[] { new PointF(x, ly), new PointF(x + side * s * 0.9f, ly - s * 0.35f), new PointF(x + side * s * 1.3f, ly + s * 0.3f) });
                    }
                g.FillEllipse(b, x - s * 0.5f, y - s * 0.5f, s, s * 1.1f);
                g.FillEllipse(b, x - s * 0.32f, y - s * 0.85f, s * 0.64f, s * 0.55f);
            }
            using (var eye = new SolidBrush(Color.White))
            {
                g.FillEllipse(eye, x - s * 0.22f, y - s * 0.72f, s * 0.16f, s * 0.16f);
                g.FillEllipse(eye, x + s * 0.06f, y - s * 0.72f, s * 0.16f, s * 0.16f);
            }
        }

        static void Crescent(Graphics g, float x, float y, float s, Color moon, Color sky)
        {
            using (var b = new SolidBrush(moon)) g.FillEllipse(b, x, y, s, s);
            using (var b = new SolidBrush(sky)) g.FillEllipse(b, x + s * 0.32f, y - s * 0.08f, s, s);
        }

        static void Wolf(Graphics g, RectangleF r, Color c)
        {
            // A sitting wolf howling up and to the right, defined on a 100 x 100 grid.
            // Tail curled on the ground, haunch, back, neck, pricked ear, long snout pointed at the moon, chest, front leg.
            float[] xy =
            {
                4,97, 10,90, 20,88, 28,84, 30,72, 33,60, 38,48, 45,38, 48,28, 49,17, 53,9, 56,18, 60,16,
                78,3, 83,3, 82,7, 73,13, 76,16, 66,24, 61,33, 62,46, 64,60, 65,90, 71,95, 71,100,
                55,100, 52,93, 46,91, 36,96, 22,99
            };
            var pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(r.Left + xy[i * 2] / 100f * r.Width, r.Top + xy[i * 2 + 1] / 100f * r.Height);
            using (var b = new SolidBrush(c)) g.FillPolygon(b, pts);
            using (var eye = new SolidBrush(Hex(0xF0A040)))
                g.FillEllipse(eye, r.Left + 0.59f * r.Width, r.Top + 0.17f * r.Height, r.Width * 0.025f, r.Width * 0.018f);
        }

        static void Leaf(Graphics g, float x, float y, float s, float angle, Color c)
        {
            var st = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(angle);
            using (var b = new SolidBrush(c)) g.FillEllipse(b, -s, -s * 0.45f, s * 2, s * 0.9f);
            using (var vein = new Pen(Color.FromArgb(150, Hex(0x7A2E10)), 1f)) { g.DrawLine(vein, -s, 0, s, 0); g.DrawLine(vein, -s - 3, 0, -s, 0); }
            g.Restore(st);
        }

        static Color Hex(int rgb) { return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF); }
    }
}
