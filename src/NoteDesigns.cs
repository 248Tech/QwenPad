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
            "Hedgehog Hollow", "Doodle Pumpkins", "Snowflake Frost", "Cozy Critters", "Snowman Night",
            "Autumn Cat", "Capybara", "Sleepy Sloth", "Beach Day", "Palm Beach", "Leaf Me a Note",
            "Sunflower", "Game Day", "Dollar Note", "Spiral", "Doodle Swirls", "Geometric",
            "Holographic", "Haunted Hill"
        };

        // Each design's category (index-aligned with Names; "" for None).
        static readonly string[] Cats =
        {
            "", "Fall", "Fall", "Fall", "Fall", "Fall", "Fall", "Winter", "Winter", "Winter",
            "Fall", "Critters", "Critters", "Summer", "Summer", "Summer", "Summer", "Sports",
            "Patterns", "Patterns", "Patterns", "Patterns", "Patterns", "Fall"
        };

        public const int Count = 24;

        /// <summary>Categories in menu order, each with its icon.</summary>
        public static readonly string[] Categories = { "Fall", "Winter", "Summer", "Critters", "Patterns", "Sports" };

        public static string Category(int design) { return design >= 0 && design < Cats.Length ? Cats[design] : ""; }

        public static string CategoryIcon(string category)
        {
            switch (category)
            {
                case "Fall": return "🍂";
                case "Winter": return "❄";
                case "Summer": return "🏖";
                case "Critters": return "🐾";
                case "Patterns": return "🌀";
                case "Sports": return "🏈";
                default: return "✦";
            }
        }

        public static string Icon(int design) { return CategoryIcon(Category(design)); }

        /// <summary>The design's dominant paper color, used to wash the art down for the "Background Art" setting
        /// while keeping the design's text readable.</summary>
        public static Color PaperOf(int design)
        {
            switch (design)
            {
                case 1: return Hex(0xFBEFE3); case 2: return Hex(0xF0872F); case 3: return Hex(0xEEEBF5);
                case 4: return Hex(0x9EE3DE); case 5: return Hex(0xFBF3E2); case 6: return Hex(0xF8EFF1);
                case 7: return Hex(0xBDEAF5); case 8: return Hex(0xF3F8FC); case 9: return Hex(0xACB1DB);
                case 10: return Hex(0xFBF6EE); case 11: return Hex(0xFBF1C9); case 12: return Hex(0xEFF4E6);
                case 13: return Hex(0xDCEFF5); case 14: return Hex(0xDCEFE4); case 15: return Hex(0xF2F6C8);
                case 16: return Hex(0xF6ECD2); case 17: return Hex(0x2E7D32); case 18: return Hex(0xDCE3CE);
                case 19: return Hex(0xF7E0E6); case 20: return Hex(0xFBF7EE); case 21: return Hex(0x14121E);
                case 22: return Hex(0xCFE8E0); case 23: return Hex(0x120A0A);
                default: return Color.White;
            }
        }

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
                case 10: return new Style { Text = Hex(0x3E2A1C), Muted = Hex(0x7A5A44), HeaderFore = Hex(0xF2E6D8), Hover = dark, SideInset = 28, BottomInset = 30, DarkHeader = true };
                case 11: return new Style { Text = Hex(0x5A4A2A), Muted = Hex(0x9A8A5A), HeaderFore = Hex(0x5A4A2A), Hover = dark, SideInset = 10, BottomInset = 44 };
                case 12: return new Style { Text = Hex(0x3F4A2E), Muted = Hex(0x7A8A5E), HeaderFore = Hex(0x3F4A2E), Hover = dark, SideInset = 10, BottomInset = 14 };
                case 13: return new Style { Text = Hex(0x244A5A), Muted = Hex(0x5A7A8A), HeaderFore = Hex(0x244A5A), Hover = dark, SideInset = 10, BottomInset = 40 };
                case 14: return new Style { Text = Hex(0x1F5A52), Muted = Hex(0x4A857C), HeaderFore = Hex(0x1F5A52), Hover = dark, SideInset = 10, BottomInset = 40 };
                case 15: return new Style { Text = Hex(0x3A4A1E), Muted = Hex(0x7A8A4E), HeaderFore = Hex(0x3A4A1E), Hover = dark, SideInset = 10, BottomInset = 52 };
                case 16: return new Style { Text = Hex(0x5A3A14), Muted = Hex(0x9A6A34), HeaderFore = Hex(0x5A3A14), Hover = dark, SideInset = 22, BottomInset = 24 };
                case 17: return new Style { Text = Hex(0xF4F1E8), Muted = Hex(0xCFE0C8), HeaderFore = Hex(0xFFFFFF), Hover = light, SideInset = 14, BottomInset = 20, DarkHeader = true };
                case 18: return new Style { Text = Hex(0x2E3A28), Muted = Hex(0x5A6A50), HeaderFore = Hex(0x2E3A28), Hover = dark, SideInset = 30, BottomInset = 30 };
                case 19: return new Style { Text = Hex(0x6A2A3A), Muted = Hex(0xA06478), HeaderFore = Hex(0x6A2A3A), Hover = dark, SideInset = 12, BottomInset = 40 };
                case 20: return new Style { Text = Hex(0x3A3A3A), Muted = Hex(0x7A7A7A), HeaderFore = Hex(0x3A3A3A), Hover = dark, SideInset = 26, BottomInset = 26 };
                case 21: return new Style { Text = Hex(0xDDEAD0), Muted = Hex(0x7FA07A), HeaderFore = Hex(0xBFE8A8), Hover = light, SideInset = 24, BottomInset = 24, DarkHeader = true };
                case 22: return new Style { Text = Hex(0x3A2A5A), Muted = Hex(0x7A6AA0), HeaderFore = Hex(0x3A2A5A), Hover = dark, SideInset = 12, BottomInset = 20 };
                case 23: return new Style { Text = Hex(0xE8C0A0), Muted = Hex(0xA07050), HeaderFore = Hex(0xE8C0A0), Hover = light, SideInset = 20, BottomInset = 20, DarkHeader = true };
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
                    case 10: AutumnCat(g, r, headerHeight); break;
                    case 11: Capybara(g, r, headerHeight); break;
                    case 12: SleepySloth(g, r, headerHeight); break;
                    case 13: BeachDay(g, r); break;
                    case 14: PalmBeach(g, r); break;
                    case 15: LeafMeANote(g, r, headerHeight); break;
                    case 16: Sunflower(g, r); break;
                    case 17: GameDay(g, r, body, lineHeight); break;
                    case 18: DollarNote(g, r); break;
                    case 19: Spiral(g, r); break;
                    case 20: DoodleSwirls(g, r); break;
                    case 21: Geometric(g, r); break;
                    case 22: Holographic(g, r, headerHeight); break;
                    case 23: HauntedHill(g, r, headerHeight); break;
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

        // ---------------------------------------------------------------- 10. Autumn Cat

        static void AutumnCat(Graphics g, Rectangle r, int headerH)
        {
            using (var bg = new SolidBrush(Hex(0xB5835A))) g.FillRectangle(bg, r);
            // White writing card, leaving a brown frame for the leaves and the cat.
            int m = (int)Clamp(r.Width * 0.09f, 16, 40);
            var card = Rectangle.FromLTRB(m, headerH - 2, r.Width - m, r.Height - (int)Clamp(r.Height * 0.14f, 40, 90));
            using (var path = RoundRect(card, 18))
            {
                using (var fill = new SolidBrush(Hex(0xFBF6EE))) g.FillPath(fill, path);
                using (var edge = new Pen(Color.FromArgb(70, Hex(0x6B4A2F)), 1.4f)) g.DrawPath(edge, path);
            }
            var rnd = new Random(1010);
            Color[] leaves = { Hex(0xD9822B), Hex(0xC9462E), Hex(0xE8A33D), Hex(0x9A6A2E), Hex(0xB5532A) };
            // Leaves, acorns and mushrooms around the brown frame.
            foreach (var p in FramePoints(r, card, 30))
            {
                var c = leaves[rnd.Next(leaves.Length)];
                int k = rnd.Next(5);
                if (k == 0) Acorn(g, p.X - 4, p.Y - 4, 11);
                else if (k == 1) Mushroom(g, p.X - 5, p.Y - 5, 12, rnd.Next(2) == 0);
                else MapleLeaf(g, p.X, p.Y, 7 + rnd.Next(3), rnd.Next(360), c);
            }
            // A black cat in a scarf, sitting at the bottom of the frame.
            float cs = Clamp(Math.Min(r.Width, r.Height) * 0.2f, 34, 74);
            BlackCat(g, r.Width * 0.5f - cs * 0.5f, r.Height - cs - 2, cs);
        }

        // ---------------------------------------------------------------- 11. Capybara

        static void Capybara(Graphics g, Rectangle r, int headerH)
        {
            float W = r.Width, H = r.Height, ground = H * 0.82f;
            using (var bg = new SolidBrush(Hex(0xFBF1C9))) g.FillRectangle(bg, r);
            using (var grass = new SolidBrush(Hex(0x9FD06A)))
            using (var path = new GraphicsPath())
            {
                path.AddBezier(0, ground + H * 0.04f, W * 0.35f, ground - H * 0.02f, W * 0.7f, ground + H * 0.02f, W, ground);
                path.AddLine(W, ground, W, H); path.AddLine(W, H, 0, H); path.CloseFigure();
                g.FillPath(grass, path);
            }
            // Soft clouds and a few hearts.
            Cloud(g, W * 0.04f, headerH + H * 0.06f, W * 0.4f, Color.FromArgb(60, Hex(0xE8C98A)));
            Cloud(g, W * 0.52f, headerH + H * 0.02f, W * 0.4f, Color.FromArgb(60, Hex(0xE8C98A)));
            var rnd = new Random(1111);
            using (var star = new SolidBrush(Hex(0xF2B33D)))
                for (int i = 0; i < 5; i++) Star(g, star, W * (0.1f + (float)rnd.NextDouble() * 0.8f), headerH + H * (0.1f + (float)rnd.NextDouble() * 0.3f), 2f + (float)rnd.NextDouble() * 2);
            float cs = Clamp(Math.Min(W, H) * 0.42f, 58, 150);
            CapybaraMascot(g, W - cs * 0.98f, ground - cs * 0.74f, cs);
        }

        // ---------------------------------------------------------------- 12. Sleepy Sloth

        static void SleepySloth(Graphics g, Rectangle r, int headerH)
        {
            float W = r.Width, H = r.Height;
            using (var bg = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, (int)W), Math.Max(1, (int)H)), Hex(0xEFF4E6), Hex(0xE2EAD6), 90f)) g.FillRectangle(bg, r);
            var rnd = new Random(1212);
            using (var dot = new SolidBrush(Color.FromArgb(40, Hex(0x8AA06A))))
                for (int i = 0; i < W * H / 900; i++) g.FillEllipse(dot, rnd.Next(r.Width), rnd.Next(r.Height), 3, 3);
            // A branch across the top, just under the header, with a hanging sloth on the right.
            float by = headerH + 8;
            using (var branch = new Pen(Hex(0x6B4A2F), Math.Max(3f, H * 0.012f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(branch, 0, by, W, by - 4);
            using (var leaf = new SolidBrush(Hex(0x5E8A3E)))
                for (float x = W * 0.1f; x < W; x += W * 0.28f)
                {
                    g.FillEllipse(leaf, x, by - 10, 16, 8);
                    g.FillEllipse(leaf, x + 18, by - 12, 14, 7);
                }
            float ss = Clamp(Math.Min(W, H) * 0.34f, 48, 120);
            Sloth(g, W - ss * 1.12f, by - 2, ss);
        }

        // ---------------------------------------------------------------- 13. Beach Day

        static void BeachDay(Graphics g, Rectangle r)
        {
            float W = r.Width, H = r.Height, sea = H * 0.6f, sand = H * 0.8f;
            using (var sky = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, (int)W), (int)sea + 1), Hex(0xBEE6F2), Hex(0xEAF7FB), 90f)) g.FillRectangle(sky, 0, 0, W, sea + 1);
            // Sun with a soft glow, top-right.
            float sx = W * 0.8f, sy = H * 0.2f, sr = Clamp(Math.Min(W, H) * 0.12f, 16, 44);
            using (var glow = new GraphicsPath())
            {
                glow.AddEllipse(sx - sr * 2, sy - sr * 2, sr * 4, sr * 4);
                using (var pg = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(150, 255, 240, 180), SurroundColors = new[] { Color.FromArgb(0, 255, 240, 180) } }) g.FillPath(pg, glow);
            }
            using (var sun = new SolidBrush(Hex(0xFFE9A8))) g.FillEllipse(sun, sx - sr, sy - sr, sr * 2, sr * 2);
            Cloud(g, W * 0.05f, H * 0.14f, W * 0.42f, Color.FromArgb(120, 255, 255, 255));
            Cloud(g, W * 0.5f, H * 0.3f, W * 0.36f, Color.FromArgb(110, 255, 255, 255));
            // Sea with a wavy shoreline, then sand.
            using (var water = new LinearGradientBrush(new RectangleF(0, sea - 1, W, sand - sea + 2), Hex(0x6FD0DE), Hex(0x3AA8C8), 90f))
            using (var wp = new GraphicsPath())
            {
                wp.AddBezier(0, sea, W * 0.3f, sea - H * 0.03f, W * 0.7f, sea + H * 0.03f, W, sea - H * 0.01f);
                wp.AddLine(W, sea, W, sand); wp.AddLine(W, sand, 0, sand); wp.CloseFigure();
                g.FillPath(water, wp);
            }
            using (var foam = new Pen(Color.FromArgb(180, 255, 255, 255), 2f))
                g.DrawBezier(foam, 0, sand, W * 0.35f, sand - H * 0.03f, W * 0.65f, sand + H * 0.02f, W, sand - H * 0.01f);
            using (var sandb = new LinearGradientBrush(new RectangleF(0, sand - 1, W, H - sand + 2), Hex(0xF4E4BA), Hex(0xE8D098), 90f)) g.FillRectangle(sandb, 0, sand - 1, W, H - sand + 2);
        }

        // ---------------------------------------------------------------- 14. Palm Beach

        static void PalmBeach(Graphics g, Rectangle r)
        {
            float W = r.Width, H = r.Height, sea = H * 0.62f;
            using (var sky = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, (int)W), Math.Max(1, (int)H)), Hex(0xCFF0EC), Hex(0xF8EFD6), 90f)) g.FillRectangle(sky, r);
            float sx = W * 0.72f, sy = H * 0.22f, sr = Clamp(Math.Min(W, H) * 0.1f, 14, 40);
            using (var sun = new SolidBrush(Color.FromArgb(180, 255, 236, 180))) g.FillEllipse(sun, sx - sr, sy - sr, sr * 2, sr * 2);
            using (var water = new SolidBrush(Hex(0x7FD2CC)))
            using (var wp = new GraphicsPath())
            {
                wp.AddBezier(0, sea, W * 0.3f, sea - H * 0.02f, W * 0.7f, sea + H * 0.02f, W, sea);
                wp.AddLine(W, sea, W, H * 0.82f); wp.AddLine(W, H * 0.82f, 0, H * 0.82f); wp.CloseFigure();
                g.FillPath(water, wp);
            }
            using (var foam = new Pen(Color.FromArgb(160, 255, 255, 255), 2f) { DashStyle = DashStyle.Dot })
                g.DrawBezier(foam, 0, sea, W * 0.3f, sea - H * 0.02f, W * 0.7f, sea + H * 0.02f, W, sea);
            using (var sand = new SolidBrush(Hex(0xF2E4BE))) g.FillRectangle(sand, 0, H * 0.82f - 1, W, H - H * 0.82f + 2);
            // Two palms, bottom-left.
            float ph = Clamp(H * 0.5f, 70, 240);
            PalmTree(g, W * 0.16f, H * 0.86f, ph);
            PalmTree(g, W * 0.3f, H * 0.86f, ph * 0.76f);
        }

        // ---------------------------------------------------------------- 15. Leaf Me a Note

        static void LeafMeANote(Graphics g, Rectangle r, int headerH)
        {
            float W = r.Width, H = r.Height;
            using (var bg = new SolidBrush(Hex(0xF2F6C8))) g.FillRectangle(bg, r);
            // Faint monstera-leaf watermarks.
            var rnd = new Random(1515);
            for (int i = 0; i < Math.Max(5, W * H / 22000); i++)
                MonsteraLeaf(g, (float)rnd.NextDouble() * W, headerH + (float)rnd.NextDouble() * (H - headerH) * 0.8f, 14 + rnd.Next(16), rnd.Next(360), Color.FromArgb(70, 255, 255, 255));
            // A row of smiley potted plants along the bottom.
            int n = Math.Max(3, (int)(W / 90));
            float pw = W / (n + 0.5f), ps = Clamp(pw * 0.6f, 20, 48);
            for (int i = 0; i < n; i++) PottedPlant(g, pw * (i + 0.5f) - ps * 0.5f, H - ps * 1.5f - 2, ps, i);
        }

        // ---------------------------------------------------------------- 16. Sunflower

        static void Sunflower(Graphics g, Rectangle r)
        {
            float W = r.Width, H = r.Height;
            using (var bg = new SolidBrush(Hex(0xF6ECD2))) g.FillRectangle(bg, r);
            using (var sunset = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, (int)W), (int)(H * 0.4f) + 1), Color.FromArgb(150, Hex(0xF6C87A)), Color.FromArgb(0, Hex(0xF6C87A)), 90f)) g.FillRectangle(sunset, 0, 0, W, H * 0.4f);
            float s = Clamp(Math.Min(W, H) * 0.26f, 34, 78);
            SunflowerBloom(g, -s * 0.3f, -s * 0.3f, s);
            SunflowerBloom(g, W - s * 0.7f, H - s * 0.7f, s * 1.1f);
            SunflowerBloom(g, W - s * 0.5f, -s * 0.35f, s * 0.7f);
            // A couple of leaves near the blooms.
            using (var leaf = new SolidBrush(Hex(0x5E8A3E)))
            {
                g.FillEllipse(leaf, s * 0.3f, s * 0.5f, s * 0.5f, s * 0.26f);
                g.FillEllipse(leaf, W - s * 0.9f, H - s * 0.95f, s * 0.5f, s * 0.26f);
            }
        }

        // ---------------------------------------------------------------- 17. Game Day

        static void GameDay(Graphics g, Rectangle r, Rectangle body, int lineH)
        {
            float W = r.Width, H = r.Height;
            using (var bg = new SolidBrush(Hex(0x2E7D32))) g.FillRectangle(bg, r);
            // Mowed vertical stripes.
            using (var stripe = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
                for (float x = 0; x < W; x += W / 7f * 2) g.FillRectangle(stripe, x, 0, W / 7f, H);
            // Yard lines along the text, with hash marks.
            using (var line = new Pen(Color.FromArgb(200, 255, 255, 255), 1.6f))
            {
                RuledLines(g, line, body, lineH, body.Left + 2, body.Right - 2);
                using (var hash = new Pen(Color.FromArgb(150, 255, 255, 255), 1.4f))
                    for (float y = body.Top + lineH - 2; y < body.Bottom; y += lineH)
                        for (float x = body.Left + 14; x < body.Right; x += 26) g.DrawLine(hash, x, y - 3, x, y + 3);
            }
            // End-zone border lines.
            using (var edge = new Pen(Color.FromArgb(220, 255, 255, 255), 2.5f)) { g.DrawLine(edge, 0, 6, W, 6); g.DrawLine(edge, 0, H - 6, W, H - 6); }
            Football(g, W - Clamp(Math.Min(W, H) * 0.32f, 44, 96) - 6, H - Clamp(Math.Min(W, H) * 0.2f, 28, 60) - 10, Clamp(Math.Min(W, H) * 0.32f, 44, 96));
        }

        // ---------------------------------------------------------------- 18. Dollar Note

        static void DollarNote(Graphics g, Rectangle r)
        {
            using (var bg = new SolidBrush(Hex(0xDCE3CE))) g.FillRectangle(bg, r);
            var ink = Color.FromArgb(150, Hex(0x3A4A34));
            // Fine guilloché wash.
            var rnd = new Random(1818);
            using (var fine = new Pen(Color.FromArgb(22, Hex(0x3A4A34)), 1f))
                for (int i = 0; i < r.Height; i += 4) g.DrawLine(fine, 0, i, r.Width, i + (float)Math.Sin(i * 0.3) * 2);
            // Double ornate border.
            using (var pen = new Pen(ink, 1.6f))
            {
                var outer = Rectangle.Inflate(r, -8, -8); g.DrawRectangle(pen, outer);
                var inner = Rectangle.Inflate(r, -14, -14); g.DrawRectangle(pen, inner);
            }
            using (var pen = new Pen(Color.FromArgb(90, Hex(0x3A4A34)), 1f))
            {
                var mid = Rectangle.Inflate(r, -11, -11);
                g.DrawRectangle(pen, mid);
            }
            // Corner "1" medallions.
            float m = Clamp(Math.Min(r.Width, r.Height) * 0.12f, 18, 36);
            foreach (var p in new[] { new PointF(14 + m / 2, 14 + m / 2), new PointF(r.Width - 14 - m / 2, 14 + m / 2), new PointF(14 + m / 2, r.Height - 14 - m / 2), new PointF(r.Width - 14 - m / 2, r.Height - 14 - m / 2) })
                Medallion(g, p.X, p.Y, m, ink);
        }

        // ---------------------------------------------------------------- 19. Spiral

        static void Spiral(Graphics g, Rectangle r)
        {
            using (var bg = new SolidBrush(Hex(0xF7E0E6))) g.FillRectangle(bg, r);
            float cx = r.Width * 0.82f, cy = r.Height * 0.84f;
            float scale = Clamp(Math.Min(r.Width, r.Height) * 0.05f, 6, 16);
            int n = 150;
            using (var ink = new Pen(Color.FromArgb(170, Hex(0x9A3A56)), 1.3f))
            using (var fill = new SolidBrush(Color.FromArgb(70, Hex(0xC76A86))))
                for (int i = n; i >= 0; i--)
                {
                    double a = i * 2.399963;              // golden angle
                    float rad = scale * (float)Math.Sqrt(i);
                    float x = cx + (float)Math.Cos(a) * rad, y = cy + (float)Math.Sin(a) * rad;
                    if (x < -20 || y < -20) continue;
                    float pet = 2 + rad * 0.09f;
                    var st = g.Save(); g.TranslateTransform(x, y); g.RotateTransform((float)(a * 180 / Math.PI) + 90);
                    g.FillEllipse(fill, -pet * 0.5f, -pet, pet, pet * 2);
                    g.DrawEllipse(ink, -pet * 0.5f, -pet, pet, pet * 2);
                    g.Restore(st);
                }
        }

        // ---------------------------------------------------------------- 20. Doodle Swirls

        static void DoodleSwirls(Graphics g, Rectangle r)
        {
            using (var bg = new SolidBrush(Hex(0xFBF7EE))) g.FillRectangle(bg, r);
            Color[] cols = { Hex(0xE8644A), Hex(0xF2A33D), Hex(0xF2C94C), Hex(0x6FB26A), Hex(0x4A90C4), Hex(0x9B6AB5) };
            float band = Clamp(Math.Min(r.Width, r.Height) * 0.16f, 20, 46), step = band * 0.85f;
            int ci = 0;
            // Overlapping rainbow "scales" around the border.
            foreach (var p in BorderRun(r, band * 0.5f, step))
            {
                float rad = band * 0.6f;
                using (var pen = new Pen(cols[ci % cols.Length], Math.Max(2f, band * 0.09f)))
                    for (int k = 0; k < 3; k++) g.DrawArc(pen, p.X - rad + k * 3, p.Y - rad + k * 3, (rad - k * 3) * 2, (rad - k * 3) * 2, 200, 140);
                ci++;
            }
        }

        // ---------------------------------------------------------------- 21. Geometric

        static void Geometric(Graphics g, Rectangle r)
        {
            using (var bg = new SolidBrush(Hex(0x14121E))) g.FillRectangle(bg, r);
            var rnd = new Random(2121);
            using (var dot = new SolidBrush(Color.FromArgb(40, Hex(0x3FA34D))))
                for (int i = 0; i < r.Width * r.Height / 1400; i++) g.FillEllipse(dot, rnd.Next(r.Width), rnd.Next(r.Height), 2, 2);
            float m1 = Clamp(Math.Min(r.Width, r.Height) * 0.42f, 60, 150);
            Mandala(g, r.Width * 0.84f, r.Height * 0.2f, m1 * 0.6f, Hex(0x3FA34D));
            Mandala(g, r.Width * 0.14f, r.Height * 0.86f, m1 * 0.7f, Hex(0x56C06A));
        }

        // ---------------------------------------------------------------- 22. Holographic

        static void Holographic(Graphics g, Rectangle r, int headerH)
        {
            float W = r.Width, H = r.Height;
            using (var grad = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, (int)W), Math.Max(1, (int)H)), Color.White, Color.White, 55f))
            {
                var blend = new ColorBlend(5);
                blend.Colors = new[] { Hex(0xCBB8F0), Hex(0xB8E0F0), Hex(0xBFF0D8), Hex(0xFBE4C0), Hex(0xF6C6DE) };
                blend.Positions = new[] { 0f, 0.3f, 0.55f, 0.8f, 1f };
                grad.InterpolationColors = blend;
                g.FillRectangle(grad, r);
            }
            // A saucer at top with a light beam, and sparkles.
            float ux = W * 0.5f, uy = headerH + H * 0.1f, uw = Clamp(W * 0.36f, 60, 180);
            using (var beam = new GraphicsPath())
            {
                beam.AddPolygon(new[] { new PointF(ux - uw * 0.18f, uy), new PointF(ux + uw * 0.18f, uy), new PointF(ux + uw * 0.5f, uy + H * 0.5f), new PointF(ux - uw * 0.5f, uy + H * 0.5f) });
                using (var pg = new PathGradientBrush(beam) { CenterColor = Color.FromArgb(120, 255, 255, 255), SurroundColors = new[] { Color.FromArgb(10, 255, 255, 255) } }) g.FillPath(pg, beam);
            }
            Ufo(g, ux, uy, uw);
            var rnd = new Random(2222);
            using (var sp = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
            using (var edge = new Pen(Color.FromArgb(120, Hex(0x9B7BD0)), 1f))
                for (int i = 0; i < 10; i++) Sparkle4(g, (float)rnd.NextDouble() * W, headerH + (float)rnd.NextDouble() * (H - headerH), 3 + rnd.Next(4), sp, edge);
        }

        // ---------------------------------------------------------------- 23. Haunted Hill

        static void HauntedHill(Graphics g, Rectangle r, int headerH)
        {
            float W = r.Width, H = r.Height;
            using (var bg = new SolidBrush(Hex(0x120A0A))) g.FillRectangle(bg, r);
            // Red moon glow, top area.
            float mx = W * 0.3f, my = headerH + H * 0.18f, mr = Clamp(Math.Min(W, H) * 0.14f, 18, 48);
            using (var glowPath = new GraphicsPath())
            {
                glowPath.AddEllipse(mx - mr * 3, my - mr * 3, mr * 6, mr * 6);
                using (var pg = new PathGradientBrush(glowPath) { CenterColor = Color.FromArgb(90, Hex(0xC4402A)), SurroundColors = new[] { Color.FromArgb(0, Hex(0xC4402A)) } }) g.FillPath(pg, glowPath);
            }
            using (var moon = new SolidBrush(Hex(0xE86A3A))) g.FillEllipse(moon, mx - mr, my - mr, mr * 2, mr * 2);
            var rnd = new Random(2323);
            using (var star = new SolidBrush(Hex(0xE8A060)))
                for (int i = 0; i < W * H / 1600; i++) { float x = rnd.Next(r.Width), y = headerH + rnd.Next((int)(H * 0.6f)); if (rnd.Next(3) == 0) Star(g, star, x, y, 2 + (float)rnd.NextDouble() * 2); else g.FillEllipse(star, x, y, 1.4f, 1.4f); }
            // Bare crooked tree, right side.
            BareTree(g, W * 0.8f, H, H * 0.6f, Hex(0x2A1810));
            // Hill with pumpkins and mushrooms.
            using (var hill = new SolidBrush(Hex(0x1E1008)))
            using (var hp = new GraphicsPath())
            {
                hp.AddBezier(0, H * 0.86f, W * 0.3f, H * 0.78f, W * 0.7f, H * 0.82f, W, H * 0.76f);
                hp.AddLine(W, H * 0.76f, W, H); hp.AddLine(W, H, 0, H); hp.CloseFigure();
                g.FillPath(hill, hp);
            }
            float ps = Clamp(Math.Min(W, H) * 0.12f, 14, 30);
            Pumpkin(g, W * 0.12f, H - ps * 1.1f, ps);
            Pumpkin(g, W * 0.3f, H - ps * 0.95f, ps * 0.8f);
            Mushroom(g, W * 0.56f, H - ps * 1.1f, ps * 0.8f, true);
            Pumpkin(g, W * 0.68f, H - ps * 1.05f, ps);
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

        static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            if (r.Width < d || r.Height < d) { p.AddRectangle(r); return p; }
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>Points in the frame between the note edge and an inner card, spaced about `step` apart.</summary>
        static System.Collections.Generic.List<PointF> FramePoints(Rectangle outer, Rectangle card, float step)
        {
            var list = new System.Collections.Generic.List<PointF>();
            var mid = Rectangle.FromLTRB((outer.Left + card.Left) / 2, (outer.Top + card.Top) / 2, (outer.Right + card.Right) / 2, (outer.Bottom + card.Bottom) / 2);
            if (mid.Width < step || mid.Height < step) return list;
            for (float x = mid.Left; x < mid.Right; x += step) { list.Add(new PointF(x, mid.Top)); list.Add(new PointF(x, mid.Bottom)); }
            for (float y = mid.Top + step; y < mid.Bottom; y += step) { list.Add(new PointF(mid.Left, y)); list.Add(new PointF(mid.Right, y)); }
            return list;
        }

        /// <summary>Points along a border band `inset` px in, spaced `step` apart (for repeating edge motifs).</summary>
        static System.Collections.Generic.List<PointF> BorderRun(Rectangle r, float inset, float step)
        {
            var list = new System.Collections.Generic.List<PointF>();
            float l = inset, t = inset, rt = r.Width - inset, b = r.Height - inset;
            if (rt <= l || b <= t || step < 2) return list;
            for (float x = l; x < rt; x += step) list.Add(new PointF(x, t));
            for (float y = t + step; y < b; y += step) list.Add(new PointF(rt, y));
            for (float x = rt; x > l; x -= step) list.Add(new PointF(x, b));
            for (float y = b; y > t; y -= step) list.Add(new PointF(l, y));
            return list;
        }

        static void BlackCat(Graphics g, float x, float y, float s)
        {
            var ink = Hex(0x1A1620);
            using (var body = new SolidBrush(ink))
            {
                // Sitting body + head.
                g.FillEllipse(body, x + s * 0.18f, y + s * 0.42f, s * 0.64f, s * 0.58f);
                g.FillEllipse(body, x + s * 0.24f, y + s * 0.1f, s * 0.52f, s * 0.48f);
                foreach (int side in new[] { -1, 1 })
                {
                    float m = x + s * 0.5f;
                    g.FillPolygon(body, new[] { new PointF(m + side * s * 0.26f, y + s * 0.2f), new PointF(m + side * s * 0.34f, y - s * 0.04f), new PointF(m + side * s * 0.1f, y + s * 0.12f) });
                }
                // Curled tail.
                g.FillEllipse(body, x + s * 0.68f, y + s * 0.74f, s * 0.3f, s * 0.18f);
            }
            using (var eye = new SolidBrush(Hex(0xF2C94C)))
            {
                g.FillEllipse(eye, x + s * 0.36f, y + s * 0.26f, s * 0.08f, s * 0.1f);
                g.FillEllipse(eye, x + s * 0.56f, y + s * 0.26f, s * 0.08f, s * 0.1f);
            }
            using (var nose = new SolidBrush(Hex(0xF2A0A0))) g.FillPolygon(nose, new[] { new PointF(x + s * 0.47f, y + s * 0.38f), new PointF(x + s * 0.53f, y + s * 0.38f), new PointF(x + s * 0.5f, y + s * 0.42f) });
            // Little blue scarf.
            using (var scarf = new SolidBrush(Hex(0x4A90C4)))
            {
                g.FillRectangle(scarf, x + s * 0.24f, y + s * 0.5f, s * 0.52f, s * 0.12f);
                g.FillPolygon(scarf, new[] { new PointF(x + s * 0.3f, y + s * 0.58f), new PointF(x + s * 0.44f, y + s * 0.58f), new PointF(x + s * 0.36f, y + s * 0.82f), new PointF(x + s * 0.26f, y + s * 0.78f) });
            }
        }

        static void CapybaraMascot(Graphics g, float x, float y, float s)
        {
            var fur = Hex(0xB08A5A);
            using (var b = new SolidBrush(fur))
            using (var dark = new SolidBrush(Hex(0x8A6A42)))
            using (var ink = new SolidBrush(Hex(0x3A2A1A)))
            {
                g.FillEllipse(b, x + s * 0.06f, y + s * 0.2f, s * 0.9f, s * 0.78f);   // body
                g.FillEllipse(b, x + s * 0.2f, y, s * 0.62f, s * 0.56f);               // head
                g.FillEllipse(dark, x + s * 0.24f, y - s * 0.02f, s * 0.12f, s * 0.1f);
                g.FillEllipse(dark, x + s * 0.66f, y - s * 0.02f, s * 0.12f, s * 0.1f); // ears
                g.FillEllipse(ink, x + s * 0.2f, y + s * 0.16f, s * 0.14f, s * 0.2f);   // big nose/muzzle
                g.FillEllipse(ink, x + s * 0.4f, y + s * 0.14f, s * 0.05f, s * 0.07f);
                g.FillEllipse(ink, x + s * 0.56f, y + s * 0.14f, s * 0.05f, s * 0.07f); // eyes
                using (var cheek = new SolidBrush(Color.FromArgb(120, Hex(0xE98A7A)))) g.FillEllipse(cheek, x + s * 0.58f, y + s * 0.24f, s * 0.12f, s * 0.08f);
                using (var bow = new SolidBrush(Hex(0xD9552A))) g.FillPolygon(bow, new[] { new PointF(x + s * 0.44f, y + s * 0.5f), new PointF(x + s * 0.56f, y + s * 0.5f), new PointF(x + s * 0.5f, y + s * 0.56f) });
                g.FillEllipse(dark, x + s * 0.34f, y + s * 0.86f, s * 0.14f, s * 0.1f);
                g.FillEllipse(dark, x + s * 0.54f, y + s * 0.86f, s * 0.14f, s * 0.1f); // feet
            }
        }

        static void Sloth(Graphics g, float x, float y, float s)
        {
            var fur = Hex(0x9A8A6A);
            using (var arm = new Pen(fur, Math.Max(3f, s * 0.14f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(arm, x + s * 0.3f, y + s * 0.4f, x + s * 0.12f, y);   // arms up to branch
                g.DrawLine(arm, x + s * 0.7f, y + s * 0.4f, x + s * 0.88f, y);
            }
            using (var claw = new Pen(Hex(0x4A3A2A), Math.Max(1.5f, s * 0.04f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawArc(claw, x + s * 0.02f, y - s * 0.08f, s * 0.2f, s * 0.2f, 20, 180);
                g.DrawArc(claw, x + s * 0.78f, y - s * 0.08f, s * 0.2f, s * 0.2f, -20, 180);
            }
            using (var body = new SolidBrush(fur)) g.FillEllipse(body, x + s * 0.24f, y + s * 0.34f, s * 0.52f, s * 0.64f);
            using (var face = new SolidBrush(Hex(0xC8B89A))) g.FillEllipse(face, x + s * 0.26f, y + s * 0.3f, s * 0.48f, s * 0.44f);
            using (var mask = new SolidBrush(Hex(0x8A7458)))
            {
                g.FillEllipse(mask, x + s * 0.32f, y + s * 0.42f, s * 0.14f, s * 0.1f);
                g.FillEllipse(mask, x + s * 0.54f, y + s * 0.42f, s * 0.14f, s * 0.1f);
            }
            using (var ink = new SolidBrush(Hex(0x2A2016)))
            {
                g.FillEllipse(ink, x + s * 0.37f, y + s * 0.45f, s * 0.05f, s * 0.05f);
                g.FillEllipse(ink, x + s * 0.58f, y + s * 0.45f, s * 0.05f, s * 0.05f);
                g.FillEllipse(ink, x + s * 0.46f, y + s * 0.54f, s * 0.08f, s * 0.06f);  // nose
            }
            using (var smile = new Pen(Hex(0x2A2016), Math.Max(1f, s * 0.025f))) g.DrawArc(smile, x + s * 0.4f, y + s * 0.54f, s * 0.2f, s * 0.14f, 20, 140);
        }

        static void PalmTree(Graphics g, float baseX, float baseY, float h)
        {
            using (var trunk = new Pen(Hex(0x8A6A3A), Math.Max(3f, h * 0.05f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawBezier(trunk, baseX, baseY, baseX - h * 0.08f, baseY - h * 0.4f, baseX + h * 0.06f, baseY - h * 0.7f, baseX + h * 0.02f, baseY - h * 0.82f);
            float tx = baseX + h * 0.02f, ty = baseY - h * 0.82f;
            using (var frond = new SolidBrush(Hex(0x2A7A62)))
                for (int i = 0; i < 6; i++)
                {
                    double a = Math.PI + i * Math.PI / 5;
                    float ex = tx + (float)Math.Cos(a) * h * 0.4f, ey = ty + (float)Math.Sin(a) * h * 0.26f;
                    float mx = (tx + ex) / 2 - (ey - ty) * 0.2f, my = (ty + ey) / 2 + (ex - tx) * 0.2f;
                    using (var fp = new GraphicsPath())
                    {
                        fp.AddBezier(tx, ty, mx, my, ex, ey, mx + (ex - tx) * 0.1f, my + h * 0.05f);
                        fp.AddBezier(mx + (ex - tx) * 0.1f, my + h * 0.05f, mx, my, tx, ty + h * 0.03f, tx, ty);
                        fp.CloseFigure(); g.FillPath(frond, fp);
                    }
                }
            using (var coco = new SolidBrush(Hex(0x6B4A2A))) g.FillEllipse(coco, tx - h * 0.04f, ty + h * 0.02f, h * 0.08f, h * 0.08f);
        }

        static void MonsteraLeaf(Graphics g, float cx, float cy, float s, float angle, Color c)
        {
            var st = g.Save();
            g.TranslateTransform(cx, cy); g.RotateTransform(angle);
            using (var b = new SolidBrush(c))
            using (var leaf = new GraphicsPath())
            {
                leaf.AddBezier(0, s, -s * 0.9f, s * 0.4f, -s * 0.7f, -s * 0.8f, 0, -s);
                leaf.AddBezier(0, -s, s * 0.7f, -s * 0.8f, s * 0.9f, s * 0.4f, 0, s);
                leaf.CloseFigure();
                var rgn = new Region(leaf);
                // Split-leaf notches.
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 3; i++)
                    {
                        using (var notch = new GraphicsPath())
                        {
                            float ny = -s * 0.5f + i * s * 0.45f;
                            notch.AddPolygon(new[] { new PointF(side * s * 0.1f, ny), new PointF(side * s * 0.95f, ny - s * 0.08f), new PointF(side * s * 0.95f, ny + s * 0.08f) });
                            rgn.Exclude(notch);
                        }
                    }
                g.FillRegion(b, rgn); rgn.Dispose();
            }
            g.Restore(st);
        }

        static void PottedPlant(Graphics g, float x, float y, float s, int variant)
        {
            Color[] pots = { Hex(0xE8644A), Hex(0xF2C94C), Hex(0x5AB0C8), Hex(0xB57AC8) };
            using (var leaf = new SolidBrush(Hex(0x4E8A3E)))
            using (var leaf2 = new SolidBrush(Hex(0x6EA84E)))
            {
                // A little clump of leaves above the pot.
                for (int i = 0; i < 5; i++)
                {
                    double a = Math.PI + i * Math.PI / 5;
                    float lx = x + s * 0.5f + (float)Math.Cos(a) * s * 0.3f, ly = y + s * 0.3f + (float)Math.Sin(a) * s * 0.3f;
                    g.FillEllipse(i % 2 == 0 ? leaf : leaf2, lx - s * 0.1f, ly - s * 0.18f, s * 0.2f, s * 0.36f);
                }
            }
            using (var pot = new SolidBrush(pots[variant % pots.Length]))
            using (var pp = new GraphicsPath())
            {
                pp.AddPolygon(new[] { new PointF(x + s * 0.18f, y + s * 0.55f), new PointF(x + s * 0.82f, y + s * 0.55f), new PointF(x + s * 0.72f, y + s), new PointF(x + s * 0.28f, y + s) });
                g.FillPath(pot, pp);
            }
            using (var ink = new SolidBrush(Hex(0x3A2A1A)))
            {
                g.FillEllipse(ink, x + s * 0.36f, y + s * 0.68f, s * 0.06f, s * 0.06f);
                g.FillEllipse(ink, x + s * 0.58f, y + s * 0.68f, s * 0.06f, s * 0.06f);
                using (var smile = new Pen(Hex(0x3A2A1A), Math.Max(1f, s * 0.03f))) g.DrawArc(smile, x + s * 0.4f, y + s * 0.74f, s * 0.2f, s * 0.12f, 20, 140);
            }
        }

        static void SunflowerBloom(Graphics g, float x, float y, float s)
        {
            float cx = x + s * 0.5f, cy = y + s * 0.5f;
            using (var petal = new SolidBrush(Hex(0xF2B028)))
                for (int i = 0; i < 14; i++)
                {
                    double a = i * Math.PI * 2 / 14;
                    var st = g.Save(); g.TranslateTransform(cx, cy); g.RotateTransform((float)(a * 180 / Math.PI));
                    g.FillEllipse(petal, s * 0.22f, -s * 0.08f, s * 0.3f, s * 0.16f);
                    g.Restore(st);
                }
            using (var center = new SolidBrush(Hex(0x6B4A2A))) g.FillEllipse(center, cx - s * 0.24f, cy - s * 0.24f, s * 0.48f, s * 0.48f);
            using (var seeds = new SolidBrush(Color.FromArgb(120, Hex(0x3A2A14))))
            {
                var rnd = new Random((int)(x * 7 + y));
                for (int i = 0; i < 16; i++) g.FillEllipse(seeds, cx - s * 0.2f + (float)rnd.NextDouble() * s * 0.4f, cy - s * 0.2f + (float)rnd.NextDouble() * s * 0.4f, s * 0.04f, s * 0.04f);
            }
        }

        static void Football(Graphics g, float x, float y, float s)
        {
            float h = s * 0.62f;
            using (var ball = new SolidBrush(Hex(0x7A4A2A)))
            using (var shine = new SolidBrush(Color.FromArgb(50, 255, 255, 255)))
            {
                g.FillEllipse(ball, x, y, s, h);
                g.FillEllipse(shine, x + s * 0.12f, y + h * 0.14f, s * 0.5f, h * 0.3f);
            }
            using (var white = new Pen(Color.FromArgb(240, 255, 255, 255), Math.Max(2f, s * 0.04f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(white, x + s * 0.12f, y + h * 0.5f, x + s * 0.3f, y + h * 0.5f);
                g.DrawLine(white, x + s * 0.7f, y + h * 0.5f, x + s * 0.88f, y + h * 0.5f);
                float lx = x + s * 0.5f;
                g.DrawLine(white, lx, y + h * 0.28f, lx, y + h * 0.72f);
                for (int i = 0; i < 4; i++) { float ly = y + h * 0.34f + i * h * 0.12f; g.DrawLine(white, lx - s * 0.05f, ly, lx + s * 0.05f, ly); }
            }
        }

        static void Medallion(Graphics g, float cx, float cy, float s, Color ink)
        {
            using (var pen = new Pen(ink, 1.3f))
            {
                g.DrawEllipse(pen, cx - s / 2, cy - s / 2, s, s);
                g.DrawEllipse(pen, cx - s * 0.38f, cy - s * 0.38f, s * 0.76f, s * 0.76f);
                for (int i = 0; i < 16; i++)
                {
                    double a = i * Math.PI / 8;
                    g.DrawLine(pen, cx + (float)Math.Cos(a) * s * 0.38f, cy + (float)Math.Sin(a) * s * 0.38f, cx + (float)Math.Cos(a) * s * 0.5f, cy + (float)Math.Sin(a) * s * 0.5f);
                }
            }
            using (var f = new Font("Georgia", s * 0.42f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(ink))
            {
                var sz = g.MeasureString("1", f);
                g.DrawString("1", f, b, cx - sz.Width / 2, cy - sz.Height / 2);
            }
        }

        static void Mandala(Graphics g, float cx, float cy, float s, Color c)
        {
            using (var pen = new Pen(Color.FromArgb(200, c), 1.2f))
            {
                for (int ring = 1; ring <= 3; ring++)
                {
                    int sides = 3 + ring * 2;
                    float rad = s * ring / 3f;
                    for (int rot = 0; rot < 2; rot++)
                    {
                        var pts = new PointF[sides];
                        float off = rot * (float)Math.PI / sides;
                        for (int i = 0; i < sides; i++) { double a = off + i * Math.PI * 2 / sides - Math.PI / 2; pts[i] = new PointF(cx + (float)Math.Cos(a) * rad, cy + (float)Math.Sin(a) * rad); }
                        g.DrawPolygon(pen, pts);
                    }
                }
                g.DrawEllipse(pen, cx - s, cy - s, s * 2, s * 2);
                g.DrawEllipse(pen, cx - s * 0.12f, cy - s * 0.12f, s * 0.24f, s * 0.24f);
            }
        }

        static void Ufo(Graphics g, float cx, float cy, float w)
        {
            using (var dome = new SolidBrush(Hex(0xBFD8F0)))
            using (var hull = new SolidBrush(Hex(0x8A8AA8)))
            using (var hull2 = new SolidBrush(Hex(0x6A6A90)))
            using (var light = new SolidBrush(Hex(0xF2E04C)))
            {
                g.FillEllipse(hull2, cx - w * 0.5f, cy - w * 0.06f, w, w * 0.22f);
                g.FillEllipse(hull, cx - w * 0.5f, cy - w * 0.1f, w, w * 0.2f);
                g.FillPie(dome, cx - w * 0.26f, cy - w * 0.26f, w * 0.52f, w * 0.44f, 180, 180);
                for (int i = -2; i <= 2; i++) g.FillEllipse(light, cx + i * w * 0.16f - w * 0.03f, cy + w * 0.02f, w * 0.06f, w * 0.06f);
            }
        }

        static void BareTree(Graphics g, float x, float baseY, float h, Color c)
        {
            using (var pen = new Pen(c, Math.Max(2f, h * 0.03f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                g.DrawLine(pen, x, baseY, x - h * 0.05f, baseY - h * 0.5f);
                float tx = x - h * 0.05f, ty = baseY - h * 0.5f;
                g.DrawLine(pen, tx, ty, tx + h * 0.2f, ty - h * 0.2f);
                g.DrawLine(pen, tx + h * 0.2f, ty - h * 0.2f, tx + h * 0.34f, ty - h * 0.16f);
                g.DrawLine(pen, tx, ty, tx - h * 0.16f, ty - h * 0.24f);
                g.DrawLine(pen, tx - h * 0.16f, ty - h * 0.24f, tx - h * 0.26f, ty - h * 0.2f);
                g.DrawLine(pen, tx, ty, tx + h * 0.02f, ty - h * 0.3f);
                g.DrawLine(pen, tx + h * 0.02f, ty - h * 0.3f, tx + h * 0.12f, ty - h * 0.42f);
                g.DrawLine(pen, tx + h * 0.02f, ty - h * 0.3f, tx - h * 0.1f, ty - h * 0.4f);
            }
        }

        static Color Hex(int rgb) { return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF); }
    }
}
