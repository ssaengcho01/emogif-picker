using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace EmojiPicker
{
    class Theme
    {
        public Color Bg, Fg, Muted, Line, Hover, Accent, Input;

        public static Theme Current()
        {
            bool dark = false;
            try
            {
                var v = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
                dark = v is int && (int)v == 0;
            }
            catch { }
            return dark
                ? new Theme { Bg = C(0x1e1f22), Fg = C(0xe6e6e6), Muted = C(0x9aa0a6), Line = C(0x3a3c42), Hover = C(0x2e3035), Accent = C(0x4f8ff7), Input = C(0x2b2d31) }
                : new Theme { Bg = C(0xffffff), Fg = C(0x1f2328), Muted = C(0x6b7280), Line = C(0xe3e5e8), Hover = C(0xf1f3f5), Accent = C(0x3b82f6), Input = C(0xf6f7f9) };
        }

        static Color C(int rgb) { return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }
    }

    static class Draw
    {
        public static Color TextColor = Color.Black; // 특수문자 글자색 (테마에 맞춤)

        public static void Emoji(Graphics g, Emoji e, RectangleF dest) { Emoji(g, e, dest, TextColor); }

        public static void Emoji(Graphics g, Emoji e, RectangleF dest, Color textColor)
        {
            if (e.IsText) { Specials.Draw(g, e, dest, textColor); return; }
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(Data.Sprite, dest, Data.SpriteRect(e), GraphicsUnit.Pixel);
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ---------- 이모지 격자 ----------
    class EmojiGrid : Control
    {
        class Item
        {
            public bool Header;
            public string Text;
            public Emoji E;
            public int GroupId = int.MinValue;
            public RectangleF R;
        }

        // 창을 넓히면 한 줄에 더 많이 (기본 8칸, 칸 크기는 거의 그대로)
        public int Cols { get { return Math.Max(8, (int)((Width - bar.Width - 12 * S) / (38 * S))); } }
        readonly Theme T;
        readonly float S;
        readonly VScrollBar bar = new VScrollBar();
        List<Item> items = new List<Item>();
        List<Item> cells = new List<Item>();
        string emptyText;
        int sel = -1, hover = -1;
        float contentH;

        public event Action<Emoji, bool> Picked;
        public event Action<Emoji> Hovered;
        public event Action<int> GroupInView;

        public EmojiGrid(Theme t, float scale)
        {
            T = t; S = scale;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = T.Bg;
            bar.Dock = DockStyle.Right;
            bar.Scroll += (s, e) => { Invalidate(); ReportGroup(); };
            bar.ValueChanged += (s, e) => { Invalidate(); ReportGroup(); };
            Controls.Add(bar);
        }

        public Emoji Selected { get { return sel >= 0 && sel < cells.Count ? cells[sel].E : null; } }
        public Emoji First { get { return cells.Count > 0 ? cells[0].E : null; } }

        // sections: (제목, 그룹ID, 이모지들)
        public void SetSections(List<Tuple<string, int, List<Emoji>>> sections, string empty, bool selectFirst)
        {
            items = new List<Item>();
            foreach (var s in sections)
            {
                items.Add(new Item { Header = true, Text = s.Item1, GroupId = s.Item2 });
                foreach (var e in s.Item3) items.Add(new Item { E = e, GroupId = s.Item2 });
            }
            cells = items.Where(i => !i.Header).ToList();
            emptyText = empty;
            sel = selectFirst && cells.Count > 0 ? 0 : -1;
            hover = -1;
            DoLayout();
            bar.Value = 0;
            Invalidate();
            ReportGroup();
            if (sel >= 0 && Hovered != null) Hovered(cells[sel].E);
        }

        float Cell { get { return (Width - bar.Width - 12 * S) / Cols; } }

        void DoLayout()
        {
            float pad = 6 * S, x0 = pad, y = 2 * S, cell = Cell, headerH = 26 * S;
            int col = 0;
            foreach (var it in items)
            {
                if (it.Header)
                {
                    if (col != 0) { y += cell; col = 0; }
                    it.R = new RectangleF(x0, y, Cols * cell, headerH);
                    y += headerH;
                }
                else
                {
                    it.R = new RectangleF(x0 + col * cell, y, cell, cell);
                    if (++col == Cols) { col = 0; y += cell; }
                }
            }
            if (col != 0) y += cell;
            contentH = y + pad;
            int max = Math.Max(0, (int)(contentH - Height));
            bar.Minimum = 0;
            bar.LargeChange = Math.Max(1, Height);
            bar.SmallChange = Math.Max(1, (int)cell);
            bar.Maximum = max + bar.LargeChange - 1;
            bar.Enabled = max > 0;
            if (bar.Value > max) bar.Value = max;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            DoLayout();
        }

        int Scroll { get { return bar.Value; } }

        void ScrollTo(int v)
        {
            int max = Math.Max(0, (int)(contentH - Height));
            bar.Value = Math.Max(0, Math.Min(max, v));
        }

        public void ScrollToGroup(int id)
        {
            var h = items.FirstOrDefault(i => i.Header && i.GroupId == id);
            if (h != null) ScrollTo((int)h.R.Y);
        }

        void ReportGroup()
        {
            if (GroupInView == null) return;
            int cur = int.MinValue;
            foreach (var h in items.Where(i => i.Header))
                if (h.R.Y <= Scroll + 8 * S) cur = h.GroupId;
            GroupInView(cur);
        }

        public void MoveSel(int delta)
        {
            if (cells.Count == 0) return;
            sel = sel < 0 ? 0 : Math.Max(0, Math.Min(cells.Count - 1, sel + delta));
            var r = cells[sel].R;
            if (r.Y < Scroll) ScrollTo((int)(r.Y - 30 * S));
            else if (r.Bottom > Scroll + Height) ScrollTo((int)(r.Bottom - Height + 4 * S));
            Invalidate();
            if (Hovered != null) Hovered(cells[sel].E);
        }

        public bool HasSelection { get { return sel >= 0; } }

        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics;
            g.Clear(T.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(0, -Scroll);
            if (items.Count == 0 && emptyText != null)
            {
                TextRenderer.DrawText(g, emptyText, Font, new Rectangle(0, Scroll + (int)(40 * S), Width - bar.Width, (int)(30 * S)), T.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.PreserveGraphicsTranslateTransform);
                return;
            }
            using (var hov = new SolidBrush(T.Hover))
            using (var acc = new Pen(T.Accent, 2 * S))
            using (var small = new Font(Font.FontFamily, 8.5f))
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    if (it.R.Bottom < Scroll || it.R.Y > Scroll + Height) continue;
                    if (it.Header)
                    {
                        TextRenderer.DrawText(g, it.Text, small, Rectangle.Round(new RectangleF(it.R.X + 2 * S, it.R.Y + 7 * S, it.R.Width, it.R.Height)), T.Muted, TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsTranslateTransform);
                        continue;
                    }
                    int ci = cells.IndexOf(it);
                    var box = RectangleF.Inflate(it.R, -1.5f * S, -1.5f * S);
                    if (ci == sel || ci == hover)
                        using (var path = Draw.Round(box, 7 * S)) g.FillPath(hov, path);
                    if (ci == sel)
                        using (var path = Draw.Round(RectangleF.Inflate(box, -1 * S, -1 * S), 6 * S)) g.DrawPath(acc, path);
                    float pic = it.R.Width * 0.66f;
                    Draw.Emoji(g, it.E, new RectangleF(it.R.X + (it.R.Width - pic) / 2, it.R.Y + (it.R.Height - pic) / 2, pic, pic));
                }
            }
        }

        int HitTest(Point p)
        {
            var pt = new PointF(p.X, p.Y + Scroll);
            for (int i = 0; i < cells.Count; i++) if (cells[i].R.Contains(pt)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h != hover)
            {
                hover = h;
                Invalidate();
                if (h >= 0 && Hovered != null) Hovered(cells[h].E);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h >= 0 && e.Button == MouseButtons.Left && Picked != null)
                Picked(cells[h].E, (ModifierKeys & (Keys.Shift | Keys.Control)) != 0);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollTo(Scroll - e.Delta / 120 * (int)(Cell * 2));
        }
    }

    // ---------- 분류 탭 ----------
    class TabBar : Control
    {
        readonly Theme T;
        readonly float S;
        List<Tuple<int, Emoji, string>> tabs = new List<Tuple<int, Emoji, string>>();
        int current = int.MinValue, hover = -1;
        readonly ToolTip tip = new ToolTip();
        public event Action<int> Clicked;

        public TabBar(Theme t, float s)
        {
            T = t; S = s;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = T.Bg;
        }

        public void SetTabs(List<Tuple<int, Emoji, string>> t) { tabs = t; Invalidate(); }
        public void SetCurrent(int id)
        {
            if (id > Specials.FirstGroup) id = Specials.FirstGroup; // 특수문자 묶음들은 탭 하나
            if (id != current) { current = id; Invalidate(); }
        }

        RectangleF TabRect(int i)
        {
            float w = (Width - 12 * S) / Math.Max(1, tabs.Count);
            return new RectangleF(6 * S + i * w, 0, w, Height - 1);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(T.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var line = new Pen(T.Line)) g.DrawLine(line, 0, Height - 1, Width, Height - 1);
            for (int i = 0; i < tabs.Count; i++)
            {
                var r = TabRect(i);
                bool on = tabs[i].Item1 == current;
                if (i == hover) using (var b = new SolidBrush(T.Hover)) using (var p = Draw.Round(RectangleF.Inflate(r, -2 * S, -3 * S), 5 * S)) g.FillPath(b, p);
                float pic = Math.Min(r.Width, r.Height) * 0.62f;
                var dest = new RectangleF(r.X + (r.Width - pic) / 2, r.Y + (r.Height - pic) / 2 - S, pic, pic);
                if (on || i == hover) Draw.Emoji(g, tabs[i].Item2, dest);
                else if (tabs[i].Item2.IsText) Draw.Emoji(g, tabs[i].Item2, dest, T.Muted);
                else
                {
                    var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.45f };
                    using (var ia = new System.Drawing.Imaging.ImageAttributes())
                    {
                        ia.SetColorMatrix(cm);
                        var src = Data.SpriteRect(tabs[i].Item2);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(Data.Sprite, Rectangle.Round(dest), src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, ia);
                    }
                }
                if (on) using (var b = new SolidBrush(T.Accent)) g.FillRectangle(b, r.X + r.Width * 0.2f, Height - 3 * S, r.Width * 0.6f, 2 * S);
            }
        }

        int Hit(Point p)
        {
            for (int i = 0; i < tabs.Count; i++) if (TabRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = Hit(e.Location);
            if (h != hover)
            {
                hover = h;
                Invalidate();
                tip.SetToolTip(this, h >= 0 ? tabs[h].Item3 : "");
            }
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int h = Hit(e.Location);
            if (h >= 0 && Clicked != null) Clicked(tabs[h].Item1);
        }
    }

    // ---------- 하단 안내줄 ----------
    class FootBar : Control
    {
        readonly Theme T;
        readonly float S;
        Emoji emoji;
        public string Hint = "";
        public string Credit = "@SSAENGCHO";

        public FootBar(Theme t, float s)
        {
            T = t; S = s;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = T.Bg;
        }

        public void SetEmoji(Emoji e) { emoji = e; Invalidate(); }

        public bool InGrip(Point p) { return p.X >= Width - 16 * S && p.Y >= Height - 18 * S; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(T.Bg);
            using (var line = new Pen(T.Line)) g.DrawLine(line, 0, 0, Width, 0);
            int x = (int)(12 * S);
            // 맨 오른쪽 아래: 창 크기 조절 손잡이 (점 6개)
            using (var b = new SolidBrush(T.Muted))
            {
                float d = 2 * S, step = 4 * S, gx = Width - 4 * S, gy = Height - 4 * S;
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j <= i; j++)
                        g.FillRectangle(b, gx - (i - j) * step - d, gy - j * step - d, d, d);
            }
            x = (int)(18 * S);
            // 오른쪽 아래 작은 서명
            int creditW;
            using (var tiny = new Font(Font.FontFamily, 7f))
            {
                creditW = TextRenderer.MeasureText(g, Credit, tiny, Size.Empty, TextFormatFlags.NoPadding).Width;
                TextRenderer.DrawText(g, Credit, tiny, new Rectangle(Width - x - creditW, 0, creditW, Height), T.Muted,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
            int right = Width - x - creditW - (int)(10 * S); // 왼쪽 글자가 서명을 덮지 않도록
            x = (int)(12 * S);
            if (emoji != null)
            {
                float pic = 22 * S;
                Draw.Emoji(g, emoji, new RectangleF(x, (Height - pic) / 2, pic, pic));
                int tx = x + (int)(30 * S);
                TextRenderer.DrawText(g, emoji.Label, Font, new Rectangle(tx, 0, Math.Max(0, right - tx), Height), T.Fg,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            else
            {
                using (var small = new Font(Font.FontFamily, 8.5f))
                    TextRenderer.DrawText(g, Hint, small, new Rectangle(x, 0, Math.Max(0, right - x), Height), T.Muted,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }
    }

    // ---------- 피커 창 ----------
    class PickerForm : Form
    {
        readonly Theme T = Theme.Current();
        readonly float S;
        public float DpiScale { get { return S; } }
        readonly TextBox box;
        readonly Panel top, topGrip;
        readonly SlimScroll slim;
        readonly Label placeholder;
        void SetPlaceholder(string t) { if (placeholder != null) { placeholder.Text = t; placeholder.Visible = box.TextLength == 0; } }
        readonly Size baseSize;
        int boxExtra; // 손잡이로 직접 늘린 높이

        // 입력칸 높이: 1~4줄은 저절로, 그 이상은 스크롤 + 손잡이
        void FitBox()
        {
            if (box == null || top == null) return;
            int lineH = box.Font.Height;
            int lines = box.TextLength == 0 ? 1 : box.GetLineFromCharIndex(box.TextLength) + 1;
            int auto = Math.Min(4, Math.Max(1, lines));
            if (box.TextLength == 0) boxExtra = 0;
            int h = Math.Max((int)(48 * S), (int)(26 * S) + auto * lineH) + boxExtra;
            if (top.Height != h) top.Height = h;
            bool more = lines > 4 || boxExtra > 0;
            topGrip.Visible = more;
            if (slim != null) slim.Invalidate(); // 기본 스크롤바 대신 얇은 막대
        }
        readonly EmojiGrid grid;
        readonly TabBar tabs;
        readonly FootBar foot;
        readonly GifPanel gif;
        readonly Timer debounce = new Timer { Interval = 60 };
        const int GifTab = -10, MyTab = -11;
        const string EmojiHint = "Enter 줄바꿈 · Ctrl+Enter 넣기 · Esc 닫기";
        bool gifMode, myMode;
        readonly StickerPanel mine;
        static readonly Emoji MyIcon = new Emoji { Ch = "MY", Label = "내 이모티콘", IsText = true, Ko = new string[0], En = new string[0], Pri = new string[0], Group = -11 };
        public Action<string> PickedSticker;
        static readonly Emoji GifIcon = new Emoji { Ch = "GIF", Label = "GIF", IsText = true, Ko = new string[0], En = new string[0], Pri = new string[0], Group = GifTab };
        public Action<GifItem> PickedGif;
        public Func<List<string>> GetRecent;
        public Action<string, bool> Picked;
        public Action<string> AutoRequested; // ✨ 자동 버튼 (입력칸에 글이 있으면 그 글) // (이모지, 계속 열어두기)
        public bool KeepAlive; // 이모지를 넣는 동안 잠깐 포커스를 잃어도 닫지 않기
        string context;              // 커서 앞에 쓴 글 (읽지 못하면 null)
        List<Emoji> recs = new List<Emoji>();
        bool recsFromAi;

        public PickerForm()
        {
            using (var g = CreateGraphics()) S = g.DpiX / 96f;
            Draw.TextColor = T.Fg;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            Text = "EmoGIF Picker";
            BackColor = T.Line; // 1px 테두리
            Padding = new Padding(1);
            baseSize = new Size((int)(352 * S), (int)(412 * S));
            ClientSize = Settings.LoadSize(baseSize);
            Font = AppFont.Make(9.5f);

            var inner = new Panel { Dock = DockStyle.Fill, BackColor = T.Bg };
            Controls.Add(inner);

            top = new Panel { Dock = DockStyle.Top, Height = (int)(48 * S), BackColor = T.Bg, Padding = new Padding((int)(10 * S), (int)(10 * S), (int)(10 * S), (int)(4 * S)) };
            var boxWrap = new Panel { Dock = DockStyle.Fill, BackColor = T.Input, Padding = new Padding((int)(8 * S), (int)(6 * S), (int)(2 * S), (int)(4 * S)) };
            // 여러 줄 입력칸: 긴 글을 붙여넣으면 4줄까지 저절로 길어지고, 그 이상은 아래 손잡이로 직접 늘림
            box = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = T.Input, ForeColor = T.Fg, Font = new Font(Font.FontFamily, 11f), Multiline = true, WordWrap = true, AcceptsReturn = false };
            boxWrap.Controls.Add(box);
            // 여러 줄 입력칸은 Windows 안내 문구(cue banner)가 안 나와서 직접 그림
            placeholder = new Label { AutoSize = false, Location = new Point((int)(3 * S), 0), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, ForeColor = T.Muted, BackColor = T.Input, Font = box.Font, Cursor = Cursors.IBeam, UseMnemonic = false };
            placeholder.Click += (s, e) => box.Focus();
            box.Controls.Add(placeholder);
            box.Resize += (s, e) => placeholder.Size = new Size(Math.Max(1, box.ClientSize.Width - (int)(3 * S)), box.Font.Height + 2);
            box.TextChanged += (s, e) => placeholder.Visible = box.TextLength == 0;
            slim = new SlimScroll(box, T, S) { Dock = DockStyle.Right, Width = (int)(10 * S) };
            boxWrap.Controls.Add(slim);
            box.BringToFront();
            boxWrap.Paint += (s, e) =>
            {
                using (var p = new Pen(box.Focused ? T.Accent : T.Line)) e.Graphics.DrawRectangle(p, 0, 0, boxWrap.Width - 1, boxWrap.Height - 1);
            };
            box.GotFocus += (s, e) => boxWrap.Invalidate();
            box.LostFocus += (s, e) => boxWrap.Invalidate();
            top.Controls.Add(boxWrap);
            // 긴 글 자동 꾸미기 버튼 (검색칸 오른쪽)
            var autoBtn = new Button
            {
                Text = "✨ 자동", Dock = DockStyle.Right, Width = (int)(64 * S), FlatStyle = FlatStyle.Flat,
                BackColor = T.Input, ForeColor = T.Fg, Cursor = Cursors.Hand, TabStop = false, Font = new Font(Font.FontFamily, 9f),
            };
            autoBtn.FlatAppearance.BorderColor = T.Line;
            autoBtn.FlatAppearance.MouseOverBackColor = T.Hover;
            new ToolTip().SetToolTip(autoBtn, "긴 글의 문장 끝마다 이모지 자동으로 붙이기");
            autoBtn.Click += (s, e) => { if (AutoRequested != null) AutoRequested(box.Text.Trim().Length > 0 ? box.Text : null); };
            autoBtn.Dock = DockStyle.Top; autoBtn.Height = (int)(34 * S);
            var autoWrap = new Panel { Dock = DockStyle.Right, Width = (int)(64 * S), BackColor = T.Bg };
            autoWrap.Controls.Add(autoBtn);
            var gap = new Panel { Dock = DockStyle.Right, Width = (int)(6 * S), BackColor = T.Bg };
            top.Controls.Add(gap);
            top.Controls.Add(autoWrap);
            boxWrap.BringToFront();

            // 입력칸 아래 손잡이: 4줄보다 길게 보고 싶을 때 끌어서 늘림
            topGrip = new Panel { Dock = DockStyle.Top, Height = (int)(8 * S), BackColor = T.Bg, Cursor = Cursors.SizeNS, Visible = false };
            topGrip.Paint += (s, e) =>
            {
                using (var p = new Pen(T.Muted, Math.Max(1f, S)))
                {
                    int cx = topGrip.Width / 2, w = (int)(14 * S), y0 = (int)(2 * S);
                    e.Graphics.DrawLine(p, cx - w, y0, cx + w, y0);
                    e.Graphics.DrawLine(p, cx - w, y0 + (int)(3 * S), cx + w, y0 + (int)(3 * S));
                }
            };
            int dragY = 0, dragExtra = 0;
            topGrip.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { dragY = Cursor.Position.Y; dragExtra = boxExtra; } };
            topGrip.MouseMove += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                int max = Math.Max(0, ClientSize.Height - (int)(220 * S)) ;
                boxExtra = Math.Max(0, Math.Min(max, dragExtra + Cursor.Position.Y - dragY));
                FitBox();
            };
            box.TextChanged += (s, e) => FitBox();
            box.SizeChanged += (s, e) => FitBox();

            tabs = new TabBar(T, S) { Dock = DockStyle.Top, Height = (int)(32 * S) };
            foot = new FootBar(T, S) { Dock = DockStyle.Bottom, Height = (int)(34 * S), Font = Font, Hint = EmojiHint };
            grid = new EmojiGrid(T, S) { Dock = DockStyle.Fill, Font = Font };
            gif = new GifPanel(T, S, Font) { Dock = DockStyle.Fill, Visible = false };
            gif.Picked += g => { Hide(); if (PickedGif != null) PickedGif(g); };
            gif.Dropped += () => Hide();
            gif.KeepOpen = () => KeepAlive = true; // 키 발급 페이지를 여는 동안 창 유지

            mine = new StickerPanel(T, S, Font) { Dock = DockStyle.Fill, Visible = false };
            mine.Picked += f => { Hide(); if (PickedSticker != null) PickedSticker(f); };
            mine.Dropped += () => Hide();
            mine.KeepOpen = () => KeepAlive = true;
            inner.Controls.Add(mine);
            inner.Controls.Add(gif);
            inner.Controls.Add(grid);
            inner.Controls.Add(foot);
            inner.Controls.Add(tabs);
            inner.Controls.Add(topGrip);
            inner.Controls.Add(top);

            // 오른쪽 아래 손잡이를 끌어서 창 크기 조절 (이모지를 더 많이 보기)
            Point rs0 = Point.Empty; Size sz0 = Size.Empty; bool sizing = false;
            foot.MouseMove += (s, e) =>
            {
                foot.Cursor = sizing || foot.InGrip(e.Location) ? Cursors.SizeNWSE : Cursors.Default;
                if (!sizing) return;
                var wa = Screen.FromControl(this).WorkingArea;
                int w = Math.Max(baseSize.Width, Math.Min(wa.Right - Left, sz0.Width + Cursor.Position.X - rs0.X));
                int h = Math.Max(baseSize.Height, Math.Min(wa.Bottom - Top, sz0.Height + Cursor.Position.Y - rs0.Y));
                ClientSize = new Size(w, h);
            };
            foot.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left && foot.InGrip(e.Location)) { sizing = true; rs0 = Cursor.Position; sz0 = ClientSize; } };
            foot.MouseUp += (s, e) =>
            {
                if (!sizing) return;
                sizing = false;
                Settings.SaveSize(ClientSize);
                if (myMode) mine.Reload();
                if (gifMode) gif.Show(box.Text.Trim());
            };
            foot.DoubleClick += (s, e) =>
            {
                // 손잡이 두 번 누르면 처음 크기로
                if (!foot.InGrip(foot.PointToClient(Cursor.Position))) return;
                ClientSize = baseSize; Settings.SaveSize(ClientSize);
                if (myMode) mine.Reload();
                if (gifMode) gif.Show(box.Text.Trim());
            };

            SetPlaceholder("이모지 검색 (예: 해, 눈물/울음, 하트)"); // 안내 문구

            debounce.Tick += (s, e) => { debounce.Stop(); Render(); };
            box.TextChanged += (s, e) => { debounce.Stop(); debounce.Start(); };
            grid.Picked += (e, keep) => DoPick(e, keep);
            grid.Hovered += e => foot.SetEmoji(e);
            grid.GroupInView += id => tabs.SetCurrent(id);
            tabs.Clicked += id =>
            {
                if (id == GifTab) { if (myMode) ExitMine(); EnterGif(); return; }
                if (id == MyTab) { if (gifMode) ExitGif(); EnterMine(); return; }
                if (gifMode) ExitGif();
                if (myMode) ExitMine();
                grid.ScrollToGroup(id);
            };
            Activated += (s, e) => { if (gifMode || myMode) KeepAlive = false; };
        }

        static bool FontExists(string name)
        {
            using (var f = new Font(name, 10f)) return f.Name == name;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(Handle);
        }

        // ---------- GIF 칸 ----------
        void EnterGif()
        {
            gifMode = true;
            grid.Visible = false;
            gif.Visible = true;
            tabs.Visible = true;
            tabs.SetCurrent(GifTab);
            debounce.Interval = 600; // 검색은 입력을 멈췄을 때만 (무료 한도 아끼기)
            foot.Hint = "Powered by KLIPY · 드래그해서 넣기";
            foot.SetEmoji(null);
            SetPlaceholder("KLIPY에서 GIF 검색 (예: 기쁨, 축하)");
            box.Text = "";
            debounce.Stop();
            gif.Show("");
            box.Focus();
        }

        void ExitGif()
        {
            gifMode = false;
            gif.Visible = false;
            grid.Visible = true;
            debounce.Interval = 60;
            foot.Hint = EmojiHint;
            SetPlaceholder("이모지 검색 (예: 해, 눈물/울음, 하트)");
            if (box.Text.Length > 0) box.Text = ""; else Render();
            debounce.Stop();
            Render();
        }

        // ---------- 내 이모티콘 칸 ----------
        void EnterMine()
        {
            myMode = true;
            grid.Visible = false;
            mine.Visible = true;
            tabs.Visible = true;
            tabs.SetCurrent(MyTab);
            foot.Hint = "드래그해서 넣기 · 오른쪽 클릭: 삭제";
            foot.SetEmoji(null);
            mine.Reload();
        }

        void ExitMine()
        {
            myMode = false;
            mine.Visible = false;
            grid.Visible = true;
            foot.Hint = EmojiHint;
            Render();
        }

        public void ShowMine() { EnterMine(); }

        public void ShowGifSamples(List<Image> imgs, string label) { EnterGif(); gif.ShowSamples(imgs, label); }
        public void ShowGifKeyHelp(bool wrong = false) { EnterGif(); gif.ShowKeyHelp(wrong, wrong ? "403 Invalid authentication credentials · 저장된 키: abcd…yz (32자)" : null); }

        void Render()
        {
            if (gifMode) { gif.Show(box.Text.Trim()); return; }
            if (myMode && box.Text.Trim().Length > 0) ExitMine(); // 검색하면 이모지 검색으로
            var q = box.Text.Trim();
            var sections = new List<Tuple<string, int, List<Emoji>>>();
            bool sentence = q.Length >= 8 && (q.IndexOf(' ') >= 0 || q.IndexOf('\n') >= 0);
            if (q.Length > 0 && sentence)
            {
                // 긴 글을 넣으면 검색 대신 그 글에 맞는 추천
                var flat = q.Replace("\r", " ").Replace("\n", " ");
                var rec = Recommend.For(flat);
                if (rec.Count > 0) sections.Add(Tuple.Create("추천 · ✨ 자동을 누르면 문장마다 붙여요", -3, rec));
                var res2 = Search.Find(q);
                if (res2.Count > 0) sections.Add(Tuple.Create(string.Format("검색 결과 {0}개", res2.Count), -2, res2));
                grid.SetSections(sections, "어울리는 이모지를 찾지 못했어요", true);
                tabs.Visible = false;
            }
            else if (q.Length > 0)
            {
                var res = Search.Find(q);
                if (res.Count > 0) sections.Add(Tuple.Create(string.Format("\"{0}\" 검색 결과 {1}개", q, res.Count), -2, res));
                grid.SetSections(sections, "찾는 이모지가 없어요", true);
                tabs.Visible = false;
            }
            else
            {
                if (recs.Count > 0) sections.Add(Tuple.Create((recsFromAi ? "AI 추천 · " : "추천 · ") + Snippet(context), -3, recs));
                var recent = (GetRecent != null ? GetRecent() : new List<string>())
                    .Where(c => Data.ByChar.ContainsKey(c)).Select(c => Data.ByChar[c]).ToList();
                if (recent.Count > 0) sections.Add(Tuple.Create("최근 사용", -1, recent));
                foreach (var g in Data.Groups)
                    sections.Add(Tuple.Create(g.Name, g.Id, Data.All.Where(e => e.Group == g.Id).ToList()));
                tabs.Visible = true;
                grid.SetSections(sections, null, recs.Count > 0); // 추천이 있으면 첫 번째를 골라 두기 (Enter로 바로 넣기)
                if (recs.Count == 0) foot.SetEmoji(null);
            }
        }

        void BuildTabs()
        {
            var list = new List<Tuple<int, Emoji, string>>();
            Emoji spark, clock;
            if (recs.Count > 0 && Data.ByChar.TryGetValue("✨", out spark)) list.Add(Tuple.Create(-3, spark, "추천"));
            if (Data.ByChar.TryGetValue("🕘", out clock)) list.Add(Tuple.Create(-1, clock, "최근 사용"));
            foreach (var g in Data.Groups)
            {
                Emoji e;
                if (g.Id > Specials.FirstGroup) continue;
                if (Data.ByChar.TryGetValue(g.Icon, out e)) list.Add(Tuple.Create(g.Id, e, g.Id == Specials.FirstGroup ? "특수문자" : g.Name));
            }
            list.Add(Tuple.Create(GifTab, GifIcon, "GIF (움직이는 그림)"));
            list.Add(Tuple.Create(MyTab, MyIcon, "내 이모티콘 (내 사진·그림)"));
            tabs.SetTabs(list);
        }

        static string Snippet(string t)
        {
            if (string.IsNullOrEmpty(t)) return "";
            t = t.Trim();
            return "\u201C" + (t.Length > 18 ? "…" + t.Substring(t.Length - 18) : t) + "\u201D";
        }

        public int OpenCount; // AI 답이 늦게 왔을 때 이미 닫혔다 다시 열린 창인지 구분

        public void Open(Point near) { Open(near, null); }

        public void Open(Point near, string ctx)
        {
            OpenCount++;
            if (gifMode) ExitGif();
            if (myMode) ExitMine();
            context = ctx;
            recsFromAi = false;
            recs = ctx != null ? Recommend.For(ctx) : new List<Emoji>();
            BuildTabs();
            box.Text = "";
            debounce.Stop();
            Render();
            var wa = Screen.FromPoint(near).WorkingArea;
            int m = (int)(8 * S);
            int x = near.X, y = near.Y + (int)(4 * S);
            if (x + Width + m > wa.Right) x = wa.Right - Width - m;
            if (y + Height + m > wa.Bottom) y = Math.Max(wa.Top + m, near.Y - Height - (int)(24 * S));
            Location = new Point(Math.Max(wa.Left + m, x), Math.Max(wa.Top + m, y));
            Show();
            Activate();
            try { Native.SetForegroundWindow(Handle); } catch { }
            box.Focus();
        }

        void DoPick(Emoji e, bool keepOpen)
        {
            if (!keepOpen) Hide();
            if (Picked != null) Picked(e.Ch, keepOpen);
        }

        public void SetQuery(string q) { box.Text = q; debounce.Stop(); Render(); }

        public void ShowGroup(int id) { grid.ScrollToGroup(id); }

        // AI 추천이 도착하면 맨 앞에 넣고, 사전 추천은 그 뒤에 이어 붙임
        public void SetAiRecs(int openCount, List<Emoji> ai)
        {
            if (openCount != OpenCount || !Visible || ai == null || ai.Count == 0) return;
            var merged = ai.ToList();
            foreach (var e in recs) if (!merged.Contains(e)) merged.Add(e);
            recs = merged.Take(32).ToList();
            recsFromAi = true;
            BuildTabs();
            if (box.Text.Trim().Length == 0) Render();
        }

        public void Reactivate()
        {
            Activate();
            try { Native.SetForegroundWindow(Handle); } catch { }
            box.Focus();
            box.SelectionStart = box.TextLength;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (gifMode)
            {
                if (e.KeyCode == Keys.Escape) { Hide(); e.Handled = e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Enter) { debounce.Stop(); Render(); e.Handled = e.SuppressKeyPress = true; }
                return;
            }
            bool atEnd = box.SelectionStart == box.TextLength && box.SelectionLength == 0;
            switch (e.KeyCode)
            {
                case Keys.Down: grid.MoveSel(grid.HasSelection ? grid.Cols : 0); break;
                case Keys.Up: grid.MoveSel(-grid.Cols); break;
                case Keys.PageDown: grid.MoveSel(grid.Cols * 5); break;
                case Keys.PageUp: grid.MoveSel(-grid.Cols * 5); break;
                case Keys.Right:
                    if (!grid.HasSelection || !atEnd) return;
                    grid.MoveSel(1); break;
                case Keys.Left:
                    if (!grid.HasSelection || !atEnd) return;
                    grid.MoveSel(-1); break;
                case Keys.Enter:
                    // 글이 있으면 Enter = 줄바꿈 (이모지 넣기는 Ctrl+Enter 또는 클릭). 빈 칸이면 Enter = 추천 이모지 넣기
                    if (box.TextLength > 0 && !e.Control)
                    {
                        box.SelectedText = "\r\n";
                        box.ScrollToCaret();
                        break;
                    }
                    if (debounce.Enabled) { debounce.Stop(); Render(); }
                    var pick = grid.Selected ?? grid.First;
                    if (pick != null) DoPick(pick, e.Shift);
                    break;
                case Keys.Escape: Hide(); break;
                default: return;
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (!KeepAlive) Hide();
        }
    }
}

namespace EmojiPicker
{
    // 여러 줄 입력칸용 얇은 스크롤 막대 (기본 스크롤바 대신, 글을 가리지 않음)
    class SlimScroll : Control
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW")]
        static extern IntPtr SendMsg(IntPtr h, int msg, IntPtr w, IntPtr l);

        readonly TextBox box;
        readonly Theme T;
        readonly float S;
        bool drag; int dragY, dragFirst;

        public SlimScroll(TextBox box, Theme t, float s)
        {
            this.box = box; T = t; S = s;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = T.Input;
            box.TextChanged += (o, e) => Invalidate();
            box.KeyUp += (o, e) => Invalidate();
            box.MouseUp += (o, e) => Invalidate();
            // 기본 스크롤바가 없으면 Windows 입력칸이 휠을 무시하므로 직접 줄을 내림
            box.MouseWheel += (o, e) => { Wheel(e.Delta); var he = e as HandledMouseEventArgs; if (he != null) he.Handled = true; };
            box.Resize += (o, e) => Invalidate();
        }

        int LineH { get { return Math.Max(1, box.Font.Height); } }
        int Total { get { return box.TextLength == 0 ? 1 : box.GetLineFromCharIndex(box.TextLength) + 1; } }
        int Visible { get { return Math.Max(1, box.ClientSize.Height / LineH); } }
        int First
        {
            get
            {
                try { return box.GetLineFromCharIndex(box.GetCharIndexFromPosition(new Point(1, 1))); } catch { return 0; }
            }
        }

        public bool Needed { get { return Total > Visible; } }

        RectangleF Thumb()
        {
            int total = Total, vis = Visible;
            float h = Math.Max(14 * S, Height * (float)vis / total);
            float maxFirst = Math.Max(1, total - vis);
            float y = (Height - h) * Math.Min(1f, First / maxFirst);
            return new RectangleF(Width / 2f - 2 * S, y, 4 * S, h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(T.Input);
            if (!Needed) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var r = Thumb();
            using (var b = new SolidBrush(drag ? T.Muted : Color.FromArgb(150, T.Muted)))
            using (var p = Draw.Round(r, r.Width / 2)) e.Graphics.FillPath(b, p);
        }

        void ScrollToLine(int line)
        {
            line = Math.Max(0, Math.Min(Total - Visible, line));
            int delta = line - First;
            if (delta == 0) return;
            try { SendMsg(box.Handle, 0x00B6, IntPtr.Zero, (IntPtr)delta); } // EM_LINESCROLL
            catch { }
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!Needed || e.Button != MouseButtons.Left) return;
            var r = Thumb();
            if (e.Y >= r.Top && e.Y <= r.Bottom) { drag = true; dragY = e.Y; dragFirst = First; Invalidate(); }
            else ScrollToLine(First + (e.Y < r.Top ? -Visible : Visible));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!drag) return;
            var r = Thumb();
            float track = Math.Max(1, Height - r.Height);
            ScrollToLine(dragFirst + (int)Math.Round((e.Y - dragY) / track * (Total - Visible)));
        }

        protected override void OnMouseUp(MouseEventArgs e) { drag = false; Invalidate(); }

        protected override void OnMouseWheel(MouseEventArgs e) { Wheel(e.Delta); }

        public void Wheel(int delta)
        {
            if (!Needed) return;
            int lines = Math.Max(1, SystemInformation.MouseWheelScrollLines);
            ScrollToLine(First - Math.Sign(delta) * Math.Max(1, Math.Abs(delta) / 120) * lines);
        }
    }
}
