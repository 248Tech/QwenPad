using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QwenPad
{
    /// <summary>
    /// Seasonal sticky-note backgrounds, drawn in code so they scale to any note size and ship no image files.
    /// Design 0 is "none" (the plain note color). Each design brings its own text colors and content insets.
    /// New designs are only ever appended, since notes save the design by number.
    /// </summary>
    static class NoteDesigns
    {
        public static readonly string[] Names =
        {
            "None (plain color)", "Spooky Night", "Pumpkin Patch", "Moonlit Ghost", "Harvest Moon",
            "Hedgehog Hollow", "Doodle Pumpkins", "Snowflake Frost", "Cozy Critters", "Snowman Night"
        };
        public const int Count = 10;

        /// <summary>"Fall" or "Winter", for grouping in menus.</summary>
        public static string Season(int design) { return design >= 7 ? "Winter" : "Fall"; }

        public static string Icon(int design) { return design >= 7 ? "❄" : "🍂"; }

        public sealed class Style
        {
            public Color Text, Muted, HeaderFore, Hover;
            public int SideInset, BottomInset;   // keeps text on the "paper" part of the design
            public bool DarkHeader;               // the header strip sits on a dark part of the design
        }

        public static Style StyleOf(int design)
        {
            Color light = Color.FromArgb(60, 255, 255, 255), dark = Color.FromArgb(50, 0, 0, 0);
            switch (design)
            {
                case 1: return new Style { Text = Hex(0x3A2A1E), Muted = Hex(0x8C6D55), HeaderFore = Hex(0xF3D7B8), Hover = light, SideInset = 20, BottomInset = 20, DarkHeader = true };
                case 2: return new Style { Text = Hex(0x3B1D0A), Muted = Hex(0x7A3E17), HeaderFore = Hex(0x3B1D0A), Hover = dark, SideInset = 10, BottomInset = 14 };
                case 3: return new Style { Text = Hex(0x2A2638), Muted = Hex(0x7B7590), HeaderFore = Hex(0xE9C46A), Hover = light, SideInset = 26, BottomInset = 26, DarkHeader = true };
                case 4: return new Style { Text = Hex(0x1F3A3D), Muted = Hex(0x3F6D70), HeaderFore = Hex(0x1F3A3D), Hover = dark, SideInset = 10, BottomInset = 14 };
                case 5: return new Style { Text = Hex(0x4A2E1A), Muted = Hex(0x9A6B42), HeaderFore = Hex(0x4A2E1A), Hover = dark, SideInset = 24, BottomInset = 26 };
                case 6: return new Style { Text = Hex(0x3A2436), Muted = Hex(0x96708C), HeaderFore = Hex(0x3A2436), Hover = dark, SideInset = 34, BottomInset = 16 };
                case 7: return new Style { Text = Hex(0x0E3646), Muted = Hex(0x2F6A7E), HeaderFore = Hex(0xFFFFFF), Hover = light, SideInset = 24, BottomInset = 24, DarkHeader = true };
                case 8: return new Style { Text = Hex(0x2C3A4C), Muted = Hex(0x7A8DA3), HeaderFore = Hex(0x2C3A4C), Hover = dark, SideInset = 10, BottomInset = 12 };
                case 9: return new Style { Text = Hex(0x23264A), Muted = Hex(0x4B4F7E), HeaderFore = Hex(0x23264A), Hover = dark, SideInset = 10, BottomInset = 12 };
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
                    case 5: HedgehogHollow(g, r, body, lineHeight); break;
                    case 6: DoodlePumpkins(g, r, headerHeight); break;
                    case 7: SnowflakeFrost(g, r, headerHeight); break;
                    case 8: CozyCritters(g, r, headerHeight); break;
                    case 9: SnowmanNight(g, r, headerHeight); break;
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

        // ---------------------------------------------------------------- 5. Hedgehog Hollow

        static void HedgehogHollow(Graphics g, Rectangle r, Rectangle body, int lineH)
        {
            using (var bg = new SolidBrush(Hex(0xFBF3E2))) g.FillRectangle(bg, r);
            using (var path = new GraphicsPath())
            {
                // Warm vignette toward the edges, like printed paper.
                path.AddRectangle(r);
                using (var pg = new PathGradientBrush(path) { CenterColor = Color.FromArgb(0, Hex(0xE6C48E)), SurroundColors = new[] { Color.FromArgb(80, Hex(0xE6C48E)) } })
                    g.FillRectangle(pg, r);
            }
            using (var line = new Pen(Color.FromArgb(170, Hex(0xC9A25C)), 1f) { DashStyle = DashStyle.Dot })
                RuledLines(g, line, body, lineH, body.Left + 4, body.Right - 4);

            // A wreath of oak and maple leaves, with acorns, all the way round.
            var rnd = new Random(5501);
            Color[] leaves = { Hex(0xE8692A), Hex(0xC9462E), Hex(0xF2B33D), Hex(0xA0892E), Hex(0xD9822B), Hex(0xB5532A) };
            foreach (var p in Perimeter(r, 10, 8))
            {
                float px = p.X + rnd.Next(-4, 5), py = p.Y + rnd.Next(-4, 5);
                var c = leaves[rnd.Next(leaves.Length)];
                if (rnd.Next(3) == 0) Leaf(g, px, py, 5 + rnd.Next(3), rnd.Next(360), c);
                else MapleLeaf(g, px, py, 7 + rnd.Next(4), rnd.Next(360), c);
                if (rnd.Next(9) == 0) Acorn(g, px - 3, py - 3, 8);
            }
            float s = Clamp(Math.Min(r.Width, r.Height) * 0.16f, 20, 42);
            // Mushrooms and hedgehogs nestled in the wreath, low down, where text reaches last.
            Mushroom(g, 3, r.Height * 0.46f, s * 0.55f, false);
            Mushroom(g, s * 0.45f, r.Height * 0.46f + s * 0.15f, s * 0.4f, true);
            Mushroom(g, r.Width - s * 0.62f, r.Height * 0.40f, s * 0.6f, true);
            Mushroom(g, r.Width * 0.18f, r.Height - s * 0.62f, s * 0.5f, false);
            Acorn(g, r.Width - s * 0.5f, r.Height * 0.30f, s * 0.35f);
            Acorn(g, r.Width * 0.30f, r.Height - s * 0.45f, s * 0.32f);
            Hedgehog(g, 1, r.Height * 0.68f, s, true);
            Hedgehog(g, r.Width - s * 1.3f - 1, r.Height * 0.56f, s * 0.8f, false);
            Hedgehog(g, r.Width * 0.58f, r.Height - s * 0.85f - 1, s * 1.05f, false);
        }

        // ---------------------------------------------------------------- 6. Doodle Pumpkins

        static void DoodlePumpkins(Graphics g, Rectangle r, int headerH)
        {
            using (var bg = new SolidBrush(Hex(0xF8EFF1))) g.FillRectangle(bg, r);
            var rnd = new Random(6113);
            using (var speck = new SolidBrush(Color.FromArgb(14, Hex(0x7A4A6A))))
                for (int i = 0; i < r.Width * r.Height / 60; i++) g.FillRectangle(speck, rnd.Next(r.Width), rnd.Next(r.Height), 1, 1);
            var ink = Hex(0x3A2436);
            float s = Clamp(Math.Min(r.Width, r.Height) * 0.2f, 26, 50), W = r.Width, H = r.Height;

            // Little doodle flowers and sparkles in the margins, then the pumpkins over them.
            float[] flowers = { 0.06f, 0.30f, 0.94f, 0.36f, 0.10f, 0.62f, 0.90f, 0.86f, 0.30f, 0.95f, 0.70f, 0.96f };
            for (int i = 0; i < flowers.Length; i += 2) DoodleFlower(g, flowers[i] * W, headerH + flowers[i + 1] * (H - headerH), s * 0.28f, ink, i * 17);
            float[] sparks = { 0.16f, 0.22f, 0.88f, 0.22f, 0.04f, 0.52f, 0.95f, 0.62f, 0.20f, 0.84f, 0.80f, 0.74f, 0.55f, 0.97f };
            for (int i = 0; i < sparks.Length; i += 2) Sparkle(g, sparks[i] * W, headerH + sparks[i + 1] * (H - headerH), s * 0.09f, ink);

            DoodlePumpkin(g, W - s * 0.35f, headerH + s * 0.70f, s * 1.1f, Hex(0xE8644A), false, -12, ink);
            DoodlePumpkin(g, W - s * 0.30f, H * 0.52f, s, Hex(0xF2B4C0), true, 10, ink);
            DoodlePumpkin(g, W - s * 0.35f, H - s * 0.55f, s * 1.05f, Hex(0xD2387A), true, -6, ink);
            DoodlePumpkin(g, s * 0.32f, headerH + s * 0.75f, s * 0.7f, Hex(0xF5D35E), false, 15, ink);
            DoodlePumpkin(g, s * 0.22f, H * 0.47f, s, Hex(0xF6B39C), true, -10, ink);
            DoodlePumpkin(g, s * 0.30f, H - s * 0.85f, s * 0.95f, Hex(0xE07BB5), false, 8, ink);
            DoodlePumpkin(g, W * 0.45f, H - s * 0.30f, s * 0.9f, Hex(0xF08A3A), true, -4, ink);
        }

        // ---------------------------------------------------------------- 7. Snowflake Frost

        static void SnowflakeFrost(Graphics g, Rectangle r, int headerH)
        {
            var edge = Hex(0x3C9AB5);
            using (var bg = new SolidBrush(edge)) g.FillRectangle(bg, r);
            using (var path = new GraphicsPath())
            {
                var oval = RectangleF.Inflate(r, r.Width * 0.18f, r.Height * 0.18f);
                path.AddEllipse(oval);
                using (var pg = new PathGradientBrush(path) { CenterColor = Hex(0xBDEAF5), SurroundColors = new[] { edge } })
                    g.FillRectangle(pg, r);
            }
            // A deeper strip under the header so its white buttons stay readable.
            using (var strip = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, r.Width), headerH + 8), Color.FromArgb(110, Hex(0x1F6F88)), Color.FromArgb(0, Hex(0x1F6F88)), 90f))
                g.FillRectangle(strip, 0, 0, r.Width, headerH + 8);

            // A frame of big paper-cut snowflakes, with small ones between them.
            var rnd = new Random(7707);
            float band = Clamp(Math.Min(r.Width, r.Height) * 0.15f, 18, 44);
            int i = 0;
            foreach (var p in Perimeter(r, band * 0.25f, band * 0.95f))
            {
                float rad = band * (0.42f + (float)rnd.NextDouble() * 0.42f);
                Snowflake(g, p.X + rnd.Next(-4, 5), p.Y + rnd.Next(-4, 5), rad, rnd.Next(60), Color.FromArgb(245, 255, 255, 255), i++ % 3);
            }
            for (int k = 0; k < (r.Width + r.Height) / 7; k++)
            {
                float x = rnd.Next(r.Width), y = rnd.Next(r.Height);
                float d = Math.Min(Math.Min(x, r.Width - x), Math.Min(y, r.Height - y));
                if (d > band * 1.3f) continue;   // keep the middle clear for writing
                Snowflake(g, x, y, 2.5f + (float)rnd.NextDouble() * 4, rnd.Next(60), Color.FromArgb(220, 255, 255, 255), k % 3);
            }
        }

        // ---------------------------------------------------------------- 8. Cozy Critters

        static void CozyCritters(Graphics g, Rectangle r, int headerH)
        {
            using (var bg = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, r.Width), Math.Max(1, r.Height)), Hex(0xF3F8FC), Hex(0xE2E9F6), 90f))
                g.FillRectangle(bg, r);
            float W = r.Width, H = r.Height;
            var cloudEdge = Color.FromArgb(80, Hex(0x9FC6DF));
            Cloud(g, -W * 0.15f, H * 0.52f, W * 0.75f, cloudEdge);
            Cloud(g, W * 0.40f, H * 0.30f, W * 0.75f, cloudEdge);
            Cloud(g, W * 0.05f, H * 0.82f, W * 0.55f, cloudEdge);
            using (var wind = new Pen(Color.FromArgb(85, Hex(0x8DBCD8)), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                Swirl(g, wind, W * 0.08f, headerH + H * 0.16f, W * 0.30f, 9);
                Swirl(g, wind, W * 0.52f, headerH + H * 0.08f, W * 0.30f, 8);
                Swirl(g, wind, W * 0.18f, headerH + H * 0.34f, W * 0.24f, 7);
            }
            using (var sp = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
            using (var spEdge = new Pen(Color.FromArgb(90, Hex(0x8DBCD8)), 1f))
            {
                float[] at = { 0.42f, 0.10f, 0.47f, 0.18f, 0.88f, 0.12f, 0.32f, 0.55f, 0.90f, 0.70f };
                for (int i = 0; i < at.Length; i += 2) Sparkle4(g, at[i] * W, headerH + at[i + 1] * (H - headerH), 4 + i % 3 * 2, sp, spEdge);
            }
            using (var snow = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
            {
                var rnd = new Random(8819);
                for (int i = 0; i < 18; i++) { float d = 2 + rnd.Next(4); g.FillEllipse(snow, rnd.Next((int)(W * 0.45f)), H * 0.70f + rnd.Next((int)(H * 0.28f)), d, d); }
            }
            // A tabby in a plaid scarf and a bulldog with a budgie in a bobble hat, bottom-right.
            float a = Clamp(Math.Min(W, H) * 0.48f, 64, 170);
            Bulldog(g, W - a * 0.98f, H - a * 0.86f, a);
            Cat(g, W - a * 1.62f, H - a * 0.66f, a * 0.72f);
            Budgie(g, W - a * 0.62f, H - a * 1.06f, a * 0.24f);
        }

        // ---------------------------------------------------------------- 9. Snowman Night

        static void SnowmanNight(Graphics g, Rectangle r, int headerH)
        {
            float W = r.Width, H = r.Height;
            using (var bg = new SolidBrush(Hex(0xACB1DB))) g.FillRectangle(bg, r);
            using (var grid = new Pen(Color.FromArgb(45, 255, 255, 255), 1f))
            {
                for (float x = 8; x < W; x += 16) g.DrawLine(grid, x, 0, x, H);
                for (float y = 8; y < H; y += 16) g.DrawLine(grid, 0, y, W, y);
            }
            // Snow falling from the top.
            var rnd = new Random(9907);
            for (int i = 0; i < W / 9; i++)
            {
                float x = rnd.Next(r.Width), y = headerH + (float)Math.Pow(rnd.NextDouble(), 1.8) * H * 0.35f;
                if (rnd.Next(4) == 0) Snowflake(g, x, y, 4 + rnd.Next(4), rnd.Next(60), Color.FromArgb(235, 255, 255, 255), rnd.Next(3));
                else using (var dot = new SolidBrush(Color.FromArgb(190, 255, 255, 255))) g.FillEllipse(dot, x, y, 2, 2);
            }
            var navy = Hex(0x2B2F55);
            float y0 = H * 0.80f;
            // Pine trees on the horizon, behind the hills.
            using (var tree = new SolidBrush(navy))
                for (float x = 4 + rnd.Next(10); x < W; x += 14 + rnd.Next(26))
                {
                    float th = 8 + rnd.Next(10), tw = th * 0.55f, ty = y0 + (x / W - 0.5f) * H * 0.02f;
                    g.FillPolygon(tree, new[] { new PointF(x, ty - th), new PointF(x - tw / 2, ty + 2), new PointF(x + tw / 2, ty + 2) });
                    g.FillRectangle(tree, x - 1, ty, 2, 4);
                }
            using (var snow = new SolidBrush(Hex(0xF8F8FC)))
            using (var line = new Pen(navy, 1.6f))
            {
                using (var back = new GraphicsPath())
                {
                    back.AddBezier(0, y0 + H * 0.05f, W * 0.30f, y0 - H * 0.01f, W * 0.55f, y0 - H * 0.03f, W, y0 + H * 0.03f);
                    back.AddLine(W, y0 + H * 0.03f, W, H + 2);
                    back.AddLine(W, H + 2, 0, H + 2);
                    back.CloseFigure();
                    g.FillPath(snow, back);
                    g.DrawBezier(line, 0, y0 + H * 0.05f, W * 0.30f, y0 - H * 0.01f, W * 0.55f, y0 - H * 0.03f, W, y0 + H * 0.03f);
                }
                float s = Clamp(Math.Min(W, H) * 0.30f, 40, 110), sx = W * 0.64f, groundY = y0 - H * 0.005f;
                Snowman(g, sx, groundY, s, navy);
                using (var front = new GraphicsPath())
                {
                    front.AddBezier(-2, y0 + H * 0.12f, W * 0.20f, y0 + H * 0.06f, W * 0.42f, y0 + H * 0.08f, W * 0.62f, H + 2);
                    front.AddLine(W * 0.62f, H + 2, -2, H + 2);
                    front.CloseFigure();
                    g.FillPath(snow, front);
                    g.DrawBezier(line, -2, y0 + H * 0.12f, W * 0.20f, y0 + H * 0.06f, W * 0.42f, y0 + H * 0.08f, W * 0.62f, H + 2);
                }
            }
        }

        // ---------------------------------------------------------------- shapes

        static float Clamp(float v, float lo, float hi) { return Math.Max(lo, Math.Min(hi, v)); }

        /// <summary>Points every `step` px along a rectangle `inset` px inside the edge.</summary>
        static System.Collections.Generic.List<PointF> Perimeter(Rectangle r, float inset, float step)
        {
            var list = new System.Collections.Generic.List<PointF>();
            float l = inset, t = inset, rt = r.Width - inset, b = r.Height - inset;
            if (rt <= l || b <= t || step < 2) return list;
            for (float x = l; x < rt; x += step) list.Add(new PointF(x, t));
            for (float y = t; y < b; y += step) list.Add(new PointF(rt, y));
            for (float x = rt; x > l; x -= step) list.Add(new PointF(x, b));
            for (float y = b; y > t; y -= step) list.Add(new PointF(l, y));
            return list;
        }

        static PointF[] OnGrid(float[] xy, float x, float y, float w, float h)
        {
            var pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(x + xy[i * 2] / 100f * w, y + xy[i * 2 + 1] / 100f * h);
            return pts;
        }

        static void MapleLeaf(Graphics g, float cx, float cy, float s, float angle, Color c)
        {
            float[] xy =
            {
                50,0, 58,22, 72,14, 70,32, 92,28, 80,46, 98,56, 74,62, 78,76, 56,70, 52,92, 48,92, 44,70,
                22,76, 26,62, 2,56, 20,46, 8,28, 30,32, 28,14, 42,22
            };
            var st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(angle);
            using (var b = new SolidBrush(c)) g.FillPolygon(b, OnGrid(xy, -s, -s, s * 2, s * 2));
            using (var vein = new Pen(Color.FromArgb(130, Hex(0x6A2A0E)), 0.8f))
            {
                g.DrawLine(vein, 0, s * 0.9f, 0, -s * 0.6f);
                g.DrawLine(vein, 0, s * 0.1f, -s * 0.6f, -s * 0.2f);
                g.DrawLine(vein, 0, s * 0.1f, s * 0.6f, -s * 0.2f);
            }
            g.Restore(st);
        }

        static void Acorn(Graphics g, float x, float y, float s)
        {
            using (var nut = new SolidBrush(Hex(0xB9813F)))
            using (var cap = new SolidBrush(Hex(0x6B4423)))
            using (var hi = new SolidBrush(Color.FromArgb(90, 255, 255, 255)))
            {
                g.FillEllipse(nut, x + s * 0.12f, y + s * 0.28f, s * 0.76f, s * 0.72f);
                g.FillEllipse(hi, x + s * 0.24f, y + s * 0.45f, s * 0.14f, s * 0.24f);
                g.FillPie(cap, x, y + s * 0.10f, s, s * 0.46f, 180, 180);
                g.FillRectangle(cap, x, y + s * 0.32f, s, s * 0.06f);
                using (var stem = new Pen(cap, Math.Max(1f, s * 0.08f))) g.DrawLine(stem, x + s * 0.5f, y + s * 0.12f, x + s * 0.6f, y);
            }
        }

        static void Mushroom(Graphics g, float x, float y, float s, bool red)
        {
            using (var stem = new SolidBrush(Hex(0xF3E6CC)))
            using (var edge = new Pen(Color.FromArgb(120, Hex(0x6B4A2F)), 0.8f))
            using (var cap = new SolidBrush(red ? Hex(0xC8402A) : Hex(0xA0672F)))
            {
                var sr = new RectangleF(x + s * 0.36f, y + s * 0.38f, s * 0.28f, s * 0.62f);
                g.FillRectangle(stem, sr);
                g.DrawRectangle(edge, sr.X, sr.Y, sr.Width, sr.Height);
                g.FillPie(cap, x, y, s, s * 0.84f, 180, 180);
                g.DrawArc(edge, x, y, s, s * 0.84f, 180, 180);
                if (red)
                    using (var dot = new SolidBrush(Color.FromArgb(235, 255, 248, 235)))
                    {
                        g.FillEllipse(dot, x + s * 0.22f, y + s * 0.12f, s * 0.13f, s * 0.11f);
                        g.FillEllipse(dot, x + s * 0.52f, y + s * 0.06f, s * 0.15f, s * 0.12f);
                        g.FillEllipse(dot, x + s * 0.70f, y + s * 0.24f, s * 0.11f, s * 0.09f);
                    }
            }
        }

        /// <summary>A hedgehog about 1.3 s wide and s tall, facing right (or left).</summary>
        static void Hedgehog(Graphics g, float x, float y, float s, bool facingRight)
        {
            var st = g.Save();
            g.TranslateTransform(facingRight ? x : x + s * 1.3f, y);
            if (!facingRight) g.ScaleTransform(-1, 1);
            // Spiky back: a zig-zag around the top of an oval.
            float cx = s * 0.55f, cy = s * 0.78f, ax = s * 0.55f, ay = s * 0.62f;
            var spikes = new System.Collections.Generic.List<PointF> { new PointF(cx - ax, cy) };
            for (int i = 0; i <= 18; i++)
            {
                double a = Math.PI + i * Math.PI / 18;
                float k = i % 2 == 0 ? 1.12f : 0.94f;
                spikes.Add(new PointF(cx + (float)Math.Cos(a) * ax * k, cy + (float)Math.Sin(a) * ay * k));
            }
            using (var back = new SolidBrush(Hex(0x6B4A2F))) g.FillPolygon(back, spikes.ToArray());
            using (var quill = new Pen(Color.FromArgb(150, Hex(0xB08A5E)), Math.Max(0.8f, s * 0.03f)))
                for (int i = 0; i < 9; i++)
                {
                    double a = Math.PI * 1.08 + i * Math.PI * 0.84 / 8;
                    g.DrawLine(quill, cx + (float)Math.Cos(a) * ax * 0.45f, cy + (float)Math.Sin(a) * ay * 0.45f, cx + (float)Math.Cos(a) * ax * 0.9f, cy + (float)Math.Sin(a) * ay * 0.9f);
                }
            using (var face = new SolidBrush(Hex(0xF1D9B5)))
            using (var ink = new SolidBrush(Hex(0x2A1A10)))
            {
                g.FillEllipse(ink, s * 0.25f, s * 0.86f, s * 0.16f, s * 0.12f);
                g.FillEllipse(ink, s * 0.66f, s * 0.86f, s * 0.16f, s * 0.12f);
                g.FillPolygon(face, new[] { new PointF(s * 0.78f, s * 0.42f), new PointF(s * 1.30f, s * 0.70f), new PointF(s * 0.98f, s * 0.92f), new PointF(s * 0.70f, s * 0.90f) });
                g.FillEllipse(face, s * 0.66f, s * 0.42f, s * 0.42f, s * 0.50f);
                g.FillEllipse(ink, s * 1.22f, s * 0.64f, s * 0.11f, s * 0.10f);
                g.FillEllipse(ink, s * 0.92f, s * 0.56f, s * 0.08f, s * 0.09f);
                using (var cheek = new SolidBrush(Color.FromArgb(110, Hex(0xE9877A)))) g.FillEllipse(cheek, s * 0.84f, s * 0.70f, s * 0.14f, s * 0.08f);
            }
            g.Restore(st);
        }

        static void DoodlePumpkin(Graphics g, float cx, float cy, float s, Color fill, bool face, float angle, Color ink)
        {
            var st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(angle);
            using (var b = new SolidBrush(Color.FromArgb(235, fill)))
            using (var shade = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
            using (var hi = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
            using (var pen = new Pen(ink, Math.Max(1.2f, s * 0.045f)) { LineJoin = LineJoin.Round })
            {
                // Lobes left, right, then the middle one on top, each outlined, so the outlines read as ribs.
                var lobes = new[]
                {
                    new RectangleF(-s * 0.52f, -s * 0.34f, s * 0.58f, s * 0.70f),
                    new RectangleF(-s * 0.06f, -s * 0.34f, s * 0.58f, s * 0.70f),
                    new RectangleF(-s * 0.27f, -s * 0.40f, s * 0.54f, s * 0.78f)
                };
                foreach (var l in lobes)
                {
                    g.FillEllipse(b, l);
                    g.FillEllipse(shade, l.X + l.Width * 0.55f, l.Y + l.Height * 0.25f, l.Width * 0.35f, l.Height * 0.6f);
                    g.FillEllipse(hi, l.X + l.Width * 0.18f, l.Y + l.Height * 0.15f, l.Width * 0.22f, l.Height * 0.35f);
                    g.DrawEllipse(pen, l);
                }
                using (var stem = new SolidBrush(Hex(0x8A6A5A)))
                {
                    var sp = new[] { new PointF(-s * 0.06f, -s * 0.36f), new PointF(-s * 0.02f, -s * 0.56f), new PointF(s * 0.09f, -s * 0.54f), new PointF(s * 0.06f, -s * 0.36f) };
                    g.FillPolygon(stem, sp);
                    g.DrawPolygon(pen, sp);
                }
                using (var leaf = new SolidBrush(Hex(0x3D8E8E)))
                {
                    var st2 = g.Save();
                    g.TranslateTransform(-s * 0.16f, -s * 0.46f);
                    g.RotateTransform(-30);
                    g.FillEllipse(leaf, -s * 0.16f, -s * 0.06f, s * 0.24f, s * 0.12f);
                    g.DrawEllipse(pen, -s * 0.16f, -s * 0.06f, s * 0.24f, s * 0.12f);
                    g.Restore(st2);
                }
                if (face)
                    using (var eye = new SolidBrush(ink))
                    using (var cheek = new SolidBrush(Color.FromArgb(150, Hex(0xFF8FA8))))
                    using (var white = new SolidBrush(Color.White))
                    using (var mouth = new Pen(ink, Math.Max(1f, s * 0.03f)))
                    {
                        g.FillEllipse(eye, -s * 0.13f, -s * 0.04f, s * 0.06f, s * 0.07f);
                        g.FillEllipse(eye, s * 0.07f, -s * 0.04f, s * 0.06f, s * 0.07f);
                        g.DrawArc(mouth, -s * 0.05f, s * 0.0f, s * 0.10f, s * 0.08f, 10, 160);
                        g.FillEllipse(cheek, -s * 0.24f, s * 0.04f, s * 0.10f, s * 0.06f);
                        g.FillEllipse(cheek, s * 0.14f, s * 0.04f, s * 0.10f, s * 0.06f);
                        g.FillEllipse(white, -s * 0.20f, s * 0.05f, s * 0.025f, s * 0.025f);
                        g.FillEllipse(white, s * 0.18f, s * 0.05f, s * 0.025f, s * 0.025f);
                    }
            }
            g.Restore(st);
        }

        static void DoodleFlower(Graphics g, float cx, float cy, float s, Color ink, float angle)
        {
            var st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(angle);
            using (var pen = new Pen(ink, Math.Max(1f, s * 0.08f)))
            {
                for (int i = 0; i < 5; i++)
                {
                    g.RotateTransform(72);
                    g.DrawEllipse(pen, -s * 0.16f, -s * 0.52f, s * 0.32f, s * 0.44f);
                }
                g.DrawEllipse(pen, -s * 0.07f, -s * 0.07f, s * 0.14f, s * 0.14f);
            }
            g.Restore(st);
        }

        static void Sparkle(Graphics g, float cx, float cy, float s, Color ink)
        {
            using (var pen = new Pen(ink, Math.Max(1f, s * 0.35f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pen, cx - s, cy, cx + s, cy);
                g.DrawLine(pen, cx, cy - s, cx, cy + s);
            }
        }

        /// <summary>A four-pointed twinkle.</summary>
        static void Sparkle4(Graphics g, float cx, float cy, float s, Brush fill, Pen edge)
        {
            var pts = new[]
            {
                new PointF(cx, cy - s), new PointF(cx + s * 0.25f, cy - s * 0.25f), new PointF(cx + s, cy), new PointF(cx + s * 0.25f, cy + s * 0.25f),
                new PointF(cx, cy + s), new PointF(cx - s * 0.25f, cy + s * 0.25f), new PointF(cx - s, cy), new PointF(cx - s * 0.25f, cy - s * 0.25f)
            };
            g.FillPolygon(fill, pts);
            g.DrawPolygon(edge, pts);
        }

        /// <summary>A six-armed snowflake; the variant changes the branch pattern.</summary>
        static void Snowflake(Graphics g, float cx, float cy, float rad, float angle, Color c, int variant)
        {
            var st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(angle);
            using (var pen = new Pen(c, Math.Max(1f, rad * 0.13f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var b = new SolidBrush(c))
            {
                for (int i = 0; i < 6; i++)
                {
                    g.DrawLine(pen, 0, 0, 0, -rad);
                    if (rad >= 5)
                    {
                        float[] at = variant == 0 ? new[] { 0.45f, 0.72f } : variant == 1 ? new[] { 0.55f } : new[] { 0.35f, 0.6f, 0.82f };
                        foreach (float t in at)
                        {
                            float len = rad * (variant == 1 ? 0.38f : 0.26f) * (1.1f - t * 0.5f);
                            g.DrawLine(pen, 0, -rad * t, -len, -rad * t - len);
                            g.DrawLine(pen, 0, -rad * t, len, -rad * t - len);
                        }
                        if (variant == 1) g.FillEllipse(b, -rad * 0.09f, -rad * 1.05f, rad * 0.18f, rad * 0.18f);
                    }
                    g.RotateTransform(60);
                }
                if (rad >= 5)
                {
                    var hex = new PointF[6];
                    for (int i = 0; i < 6; i++) hex[i] = new PointF((float)Math.Cos(i * Math.PI / 3) * rad * 0.2f, (float)Math.Sin(i * Math.PI / 3) * rad * 0.2f);
                    g.FillPolygon(b, hex);
                }
            }
            g.Restore(st);
        }

        static void Cloud(Graphics g, float x, float y, float w, Color edge)
        {
            using (var p = new GraphicsPath())
            {
                float h = w * 0.32f;
                p.AddEllipse(x, y + h * 0.35f, w, h * 0.65f);
                p.AddEllipse(x + w * 0.12f, y + h * 0.05f, w * 0.38f, h * 0.75f);
                p.AddEllipse(x + w * 0.38f, y - h * 0.25f, w * 0.40f, h * 0.95f);
                p.AddEllipse(x + w * 0.66f, y + h * 0.15f, w * 0.28f, h * 0.65f);
                using (var region = new Region(p))
                using (var fill = new SolidBrush(Color.FromArgb(110, 255, 255, 255)))
                    g.FillRegion(fill, region);
                using (var outline = new GraphicsPath())
                {
                    // Only the cloud's bottom edge is outlined, as in the concept's soft clouds.
                    using (var pen = new Pen(edge, 2f)) g.DrawArc(pen, x, y + h * 0.35f, w, h * 0.65f, 15, 150);
                }
            }
        }

        static void Swirl(Graphics g, Pen pen, float x, float y, float len, float curl)
        {
            var pts = new System.Collections.Generic.List<PointF>();
            for (int i = 0; i <= 8; i++) pts.Add(new PointF(x + len * i / 8f, y + (float)Math.Sin(i * 0.8) * 3));
            float ex = x + len, ey = y + (float)Math.Sin(6.4) * 3;
            for (int i = 1; i <= 10; i++)
            {
                double a = -Math.PI / 2 + i * 0.6;
                float rr = curl * (1 - i / 12f);
                pts.Add(new PointF(ex + (float)Math.Cos(a) * rr, ey - curl + (float)Math.Sin(a) * rr + curl));
            }
            g.DrawCurve(pen, pts.ToArray(), 0.5f);
        }

        static void Cat(Graphics g, float x, float y, float c)
        {
            var inkC = Hex(0x2A2222);
            using (var ink = new Pen(inkC, Math.Max(1.2f, c * 0.022f)) { LineJoin = LineJoin.Round })
            using (var fur = new SolidBrush(Hex(0xC9A27F)))
            using (var pink = new SolidBrush(Hex(0xE9A0A0)))
            using (var stripe = new Pen(Hex(0x7A5638), Math.Max(1.2f, c * 0.035f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.FillEllipse(fur, x + c * 0.14f, y + c * 0.58f, c * 0.72f, c * 0.6f);
                g.DrawEllipse(ink, x + c * 0.14f, y + c * 0.58f, c * 0.72f, c * 0.6f);
                foreach (int side in new[] { -1, 1 })
                {
                    float m = x + c * 0.5f;
                    var ear = new[] { new PointF(m + side * c * 0.36f, y + c * 0.32f), new PointF(m + side * c * 0.30f, y + c * 0.02f), new PointF(m + side * c * 0.06f, y + c * 0.18f) };
                    g.FillPolygon(fur, ear);
                    g.DrawPolygon(ink, ear);
                    g.FillPolygon(pink, new[] { new PointF(m + side * c * 0.30f, y + c * 0.26f), new PointF(m + side * c * 0.28f, y + c * 0.09f), new PointF(m + side * c * 0.14f, y + c * 0.19f) });
                }
                var head = new RectangleF(x + c * 0.1f, y + c * 0.12f, c * 0.8f, c * 0.62f);
                g.FillEllipse(fur, head);
                g.DrawEllipse(ink, head);
                g.DrawLine(stripe, x + c * 0.5f, y + c * 0.16f, x + c * 0.5f, y + c * 0.26f);
                g.DrawLine(stripe, x + c * 0.42f, y + c * 0.17f, x + c * 0.40f, y + c * 0.25f);
                g.DrawLine(stripe, x + c * 0.58f, y + c * 0.17f, x + c * 0.60f, y + c * 0.25f);
                Eyes(g, x + c * 0.5f, y + c * 0.40f, c * 0.13f, c * 0.27f, inkC);
                g.FillPolygon(pink, new[] { new PointF(x + c * 0.47f, y + c * 0.52f), new PointF(x + c * 0.53f, y + c * 0.52f), new PointF(x + c * 0.5f, y + c * 0.56f) });
                using (var thin = new Pen(inkC, Math.Max(0.8f, c * 0.012f)))
                {
                    g.DrawArc(thin, x + c * 0.44f, y + c * 0.53f, c * 0.06f, c * 0.05f, 0, 180);
                    g.DrawArc(thin, x + c * 0.50f, y + c * 0.53f, c * 0.06f, c * 0.05f, 0, 180);
                    foreach (int side in new[] { -1, 1 })
                        for (int k = 0; k < 2; k++)
                            g.DrawLine(thin, x + c * (0.5f + side * 0.12f), y + c * (0.54f + k * 0.03f), x + c * (0.5f + side * 0.36f), y + c * (0.52f + k * 0.06f));
                }
            }
            // Red plaid scarf: a band round the neck and a hanging end.
            using (var scarf = new GraphicsPath())
            {
                scarf.AddRectangle(new RectangleF(x + c * 0.14f, y + c * 0.66f, c * 0.72f, c * 0.13f));
                scarf.AddRectangle(new RectangleF(x + c * 0.56f, y + c * 0.72f, c * 0.16f, c * 0.32f));
                using (var red = new SolidBrush(Hex(0xC8384A))) g.FillPath(red, scarf);
                var st = g.Save();
                g.SetClip(scarf);
                using (var dark = new Pen(Color.FromArgb(160, Hex(0x7E1E2E)), Math.Max(1f, c * 0.025f)))
                using (var light = new Pen(Color.FromArgb(120, Hex(0xF2A0A8)), Math.Max(1f, c * 0.015f)))
                    for (float t = 0; t < c; t += c * 0.07f)
                    {
                        g.DrawLine(dark, x + t, y + c * 0.6f, x + t, y + c * 1.1f);
                        g.DrawLine(light, x, y + c * 0.62f + t * 0.5f, x + c, y + c * 0.62f + t * 0.5f);
                    }
                g.Restore(st);
                using (var ink = new Pen(Hex(0x2A2222), Math.Max(1.2f, c * 0.022f))) g.DrawPath(ink, scarf);
            }
        }

        static void Bulldog(Graphics g, float x, float y, float d)
        {
            var inkC = Hex(0x2A2222);
            using (var ink = new Pen(inkC, Math.Max(1.2f, d * 0.02f)) { LineJoin = LineJoin.Round })
            using (var white = new SolidBrush(Hex(0xF7F1EA)))
            using (var brown = new SolidBrush(Hex(0xB5652F)))
            using (var dark = new SolidBrush(Hex(0x7A3F1E)))
            {
                var bodyR = new RectangleF(x + d * 0.08f, y + d * 0.60f, d * 0.88f, d * 0.6f);
                g.FillEllipse(white, bodyR);
                using (var clip = new GraphicsPath())
                {
                    clip.AddEllipse(bodyR);
                    var st = g.Save();
                    g.SetClip(clip);
                    g.FillEllipse(brown, x + d * 0.62f, y + d * 0.62f, d * 0.5f, d * 0.6f);
                    g.Restore(st);
                }
                g.DrawEllipse(ink, bodyR);
                // Floppy ears, then the head over them.
                g.FillEllipse(dark, x + d * 0.02f, y + d * 0.14f, d * 0.26f, d * 0.30f);
                g.DrawEllipse(ink, x + d * 0.02f, y + d * 0.14f, d * 0.26f, d * 0.30f);
                g.FillEllipse(dark, x + d * 0.72f, y + d * 0.14f, d * 0.26f, d * 0.30f);
                g.DrawEllipse(ink, x + d * 0.72f, y + d * 0.14f, d * 0.26f, d * 0.30f);
                var head = new RectangleF(x + d * 0.12f, y + d * 0.10f, d * 0.76f, d * 0.64f);
                g.FillEllipse(white, head);
                using (var clip = new GraphicsPath())
                {
                    clip.AddEllipse(head);
                    var st = g.Save();
                    g.SetClip(clip);
                    g.FillEllipse(brown, x + d * 0.56f, y + d * 0.02f, d * 0.42f, d * 0.40f);
                    g.FillEllipse(brown, x + d * 0.08f, y + d * 0.04f, d * 0.26f, d * 0.24f);
                    g.Restore(st);
                }
                g.DrawEllipse(ink, head);
                Eyes(g, x + d * 0.5f, y + d * 0.36f, d * 0.10f, d * 0.30f, inkC);
                var muzzle = new RectangleF(x + d * 0.28f, y + d * 0.44f, d * 0.44f, d * 0.26f);
                g.FillEllipse(white, muzzle);
                g.DrawArc(ink, muzzle, 0, 180);
                using (var nose = new SolidBrush(inkC)) g.FillEllipse(nose, x + d * 0.42f, y + d * 0.44f, d * 0.16f, d * 0.10f);
                g.DrawLine(ink, x + d * 0.5f, y + d * 0.54f, x + d * 0.5f, y + d * 0.60f);
                g.DrawArc(ink, x + d * 0.36f, y + d * 0.55f, d * 0.14f, d * 0.08f, 0, 160);
                g.DrawArc(ink, x + d * 0.50f, y + d * 0.55f, d * 0.14f, d * 0.08f, 20, 160);
                g.DrawArc(ink, x + d * 0.40f, y + d * 0.20f, d * 0.20f, d * 0.08f, 200, 140);
            }
        }

        static void Budgie(Graphics g, float x, float y, float b)
        {
            using (var ink = new Pen(Hex(0x2A2222), Math.Max(1f, b * 0.05f)))
            using (var blue = new SolidBrush(Hex(0x7CC7E8)))
            using (var white = new SolidBrush(Hex(0xFFFFFF)))
            using (var hat = new SolidBrush(Hex(0x3A9FB0)))
            using (var beak = new SolidBrush(Hex(0xF2A65A)))
            using (var eye = new SolidBrush(Hex(0x1A1A1A)))
            {
                // Long tail, a round blue body, a white face mask with a beak, and a scalloped wing.
                var tail = new[] { new PointF(x + b * 0.62f, y + b * 0.95f), new PointF(x + b * 1.25f, y + b * 1.45f), new PointF(x + b * 1.12f, y + b * 1.5f), new PointF(x + b * 0.55f, y + b * 1.08f) };
                g.FillPolygon(blue, tail);
                g.DrawPolygon(ink, tail);
                var bodyR = new RectangleF(x + b * 0.05f, y + b * 0.12f, b * 0.9f, b * 1.05f);
                g.FillEllipse(blue, bodyR);
                g.DrawEllipse(ink, bodyR);
                g.FillEllipse(white, x + b * 0.22f, y + b * 0.22f, b * 0.56f, b * 0.42f);
                using (var spot = new SolidBrush(Hex(0x2E4FA0)))
                {
                    g.FillEllipse(spot, x + b * 0.24f, y + b * 0.52f, b * 0.08f, b * 0.08f);
                    g.FillEllipse(spot, x + b * 0.68f, y + b * 0.52f, b * 0.08f, b * 0.08f);
                }
                using (var wing = new SolidBrush(Hex(0x5AAED3)))
                using (var scallop = new Pen(Color.FromArgb(160, Hex(0x2A5E7A)), Math.Max(0.8f, b * 0.03f)))
                {
                    g.FillEllipse(wing, x + b * 0.55f, y + b * 0.6f, b * 0.38f, b * 0.5f);
                    for (int i = 0; i < 3; i++) g.DrawArc(scallop, x + b * 0.6f, y + b * (0.66f + i * 0.12f), b * 0.28f, b * 0.12f, 0, 180);
                }
                g.FillEllipse(eye, x + b * 0.32f, y + b * 0.32f, b * 0.09f, b * 0.11f);
                g.FillEllipse(eye, x + b * 0.59f, y + b * 0.32f, b * 0.09f, b * 0.11f);
                g.FillPolygon(beak, new[] { new PointF(x + b * 0.44f, y + b * 0.42f), new PointF(x + b * 0.56f, y + b * 0.42f), new PointF(x + b * 0.5f, y + b * 0.54f) });
                // Bobble hat.
                g.FillPie(hat, x + b * 0.15f, y - b * 0.1f, b * 0.7f, b * 0.6f, 180, 180);
                g.FillRectangle(hat, x + b * 0.13f, y + b * 0.16f, b * 0.74f, b * 0.08f);
                g.FillEllipse(hat, x + b * 0.4f, y - b * 0.26f, b * 0.2f, b * 0.2f);
            }
        }

        static void Eyes(Graphics g, float cx, float cy, float size, float apart, Color ink)
        {
            using (var b = new SolidBrush(ink))
            using (var shine = new SolidBrush(Color.White))
                foreach (int side in new[] { -1, 1 })
                {
                    float ex = cx + side * apart / 2 - size / 2;
                    g.FillEllipse(b, ex, cy - size / 2, size, size * 1.1f);
                    g.FillEllipse(shine, ex + size * 0.2f, cy - size * 0.3f, size * 0.35f, size * 0.35f);
                    g.FillEllipse(shine, ex + size * 0.6f, cy + size * 0.15f, size * 0.15f, size * 0.15f);
                }
        }

        /// <summary>A snowman in a bobble beanie and scarf, standing on groundY.</summary>
        static void Snowman(Graphics g, float cx, float groundY, float s, Color navy)
        {
            using (var snow = new SolidBrush(Hex(0xFBFBFE)))
            using (var line = new Pen(navy, Math.Max(1.4f, s * 0.025f)))
            using (var dark = new SolidBrush(navy))
            using (var stick = new Pen(Hex(0x5B4636), Math.Max(1.4f, s * 0.03f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var carrot = new SolidBrush(Hex(0xF08A3A)))
            {
                float br = s * 0.30f, hr = s * 0.21f, by = groundY - br, hy = by - br - hr * 0.75f;
                g.DrawLine(stick, cx - br * 0.8f, by - br * 0.3f, cx - br * 1.6f, by - br * 0.9f);
                g.DrawLine(stick, cx - br * 1.35f, by - br * 0.7f, cx - br * 1.55f, by - br * 0.45f);
                g.DrawLine(stick, cx + br * 0.8f, by - br * 0.3f, cx + br * 1.6f, by - br * 0.95f);
                g.DrawLine(stick, cx + br * 1.4f, by - br * 0.8f, cx + br * 1.7f, by - br * 0.75f);
                g.FillEllipse(snow, cx - br, by - br, br * 2, br * 2);
                g.DrawEllipse(line, cx - br, by - br, br * 2, br * 2);
                g.FillEllipse(snow, cx - hr, hy - hr, hr * 2, hr * 2);
                g.DrawEllipse(line, cx - hr, hy - hr, hr * 2, hr * 2);
                // Coal buttons, eyes and smile; a carrot nose.
                for (int i = 0; i < 3; i++) g.FillEllipse(dark, cx - s * 0.02f, by - br * 0.5f + i * br * 0.4f, s * 0.04f, s * 0.04f);
                g.FillEllipse(dark, cx - hr * 0.45f, hy - hr * 0.2f, s * 0.035f, s * 0.04f);
                g.FillEllipse(dark, cx + hr * 0.3f, hy - hr * 0.2f, s * 0.035f, s * 0.04f);
                for (int i = 0; i < 5; i++)
                {
                    double a = Math.PI * (0.2 + i * 0.15);
                    g.FillEllipse(dark, cx + (float)Math.Cos(a) * hr * 0.55f - s * 0.012f, hy + hr * 0.05f + (float)Math.Sin(a) * hr * 0.35f, s * 0.024f, s * 0.024f);
                }
                g.FillPolygon(carrot, new[] { new PointF(cx - s * 0.01f, hy + hr * 0.02f), new PointF(cx + hr * 0.75f, hy + hr * 0.14f), new PointF(cx - s * 0.01f, hy + hr * 0.24f) });
                // Scarf and beanie.
                g.FillRectangle(dark, cx - hr * 0.95f, hy + hr * 0.72f, hr * 1.9f, hr * 0.3f);
                g.FillRectangle(dark, cx + hr * 0.35f, hy + hr * 0.9f, hr * 0.32f, hr * 0.75f);
                g.FillPie(dark, cx - hr * 0.95f, hy - hr * 1.25f, hr * 1.9f, hr * 1.5f, 180, 180);
                g.FillRectangle(dark, cx - hr * 1.0f, hy - hr * 0.56f, hr * 2.0f, hr * 0.22f);
                g.FillEllipse(dark, cx - hr * 0.22f, hy - hr * 1.5f, hr * 0.44f, hr * 0.44f);
                using (var rib = new Pen(Color.FromArgb(90, 255, 255, 255), 1f))
                    for (float t = -0.8f; t <= 0.8f; t += 0.27f) g.DrawLine(rib, cx + hr * t, hy - hr * 0.54f, cx + hr * t, hy - hr * 0.36f);
            }
        }

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
