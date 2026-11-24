using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace EmojiPicker
{
    // MY에 사진·그림 넣기 전: 사진마다 보낼 부분만 자르고, 배경 지우기를 켜거나 끔
    class AddImageForm : Form
    {
        public readonly List<KeyValuePair<string, Stickers.AddOpt>> Result = new List<KeyValuePair<string, Stickers.AddOpt>>();

        readonly string[] files;
        readonly Theme T;
        readonly float S;
        int index = -1;
        Bitmap work;          // 화면용으로 줄인 원본
        bool animated;
        const bool BgFeature = false; // 배경 지우기 기능 끔 (2026-11-24 사용자 결정: 카톡에선 어차피 흰 바탕)
        bool wantBg = true;   // 사용자가 고른 배경 지우기 (단색 배경일 때만 적용)
        RectangleF crop;      // 0~1 비율
        readonly CropBox box;
        readonly PictureBox preview;
        readonly CheckBox bg;
        readonly Label info, bgNote, title;
        readonly Button add, skip, rest;

        bool busy;
        int D(int v) { return (int)(v * S); }

        public AddImageForm(string[] files, Theme t, float s, Font font)
        {
            this.files = files; T = t; S = s;
            Text = "이모티콘 넣기";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = MaximizeBox = false; ShowInTaskbar = false; TopMost = true;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            Font = font; BackColor = T.Bg; ForeColor = T.Fg;
            ClientSize = new Size(D(580), D(470));

            title = new Label { Location = new Point(D(16), D(12)), Size = new Size(D(548), D(22)), Font = new Font(font, FontStyle.Bold) };
            var hint = new Label { Text = "네모를 끌어서 보낼 부분만 남기세요. 모서리를 끌면 크기가 바뀌어요.", Location = new Point(D(16), D(36)), Size = new Size(D(548), D(20)), ForeColor = T.Muted };
            box = new CropBox(this) { Location = new Point(D(16), D(62)), Size = new Size(D(360), D(340)) };

            var pl = new Label { Text = "보낼 모습", Location = new Point(D(394), D(62)), AutoSize = true, ForeColor = T.Muted };
            preview = new PictureBox { Location = new Point(D(394), D(84)), Size = new Size(D(170), D(170)), SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.None };
            preview.BackgroundImage = Checker(D(10)); preview.BackgroundImageLayout = ImageLayout.Tile;

            bg = new CheckBox { Text = "배경 지우기", Location = new Point(D(394), D(266)), AutoSize = true };
            bg.CheckedChanged += (o, e) => { if (busy) return; if (bg.Enabled) wantBg = bg.Checked; UpdatePreview(); };
            bgNote = new Label { Location = new Point(D(394), D(292)), Size = new Size(D(176), D(70)), ForeColor = T.Muted };
            var reset = new Button { Text = "자르기 처음대로", Location = new Point(D(394), D(368)), Size = new Size(D(170), D(30)), FlatStyle = FlatStyle.System };
            reset.Click += (o, e) => { crop = new RectangleF(0, 0, 1, 1); box.Invalidate(); UpdatePreview(); };

            info = new Label { Location = new Point(D(16), D(420)), Size = new Size(D(200), D(30)), ForeColor = T.Muted, TextAlign = ContentAlignment.MiddleLeft };
            rest = new Button { Text = "남은 사진은 그대로 넣기", Location = new Point(D(214), D(418)), Size = new Size(D(160), D(34)), FlatStyle = FlatStyle.System };
            skip = new Button { Text = "건너뛰기", Location = new Point(D(382), D(418)), Size = new Size(D(84), D(34)), FlatStyle = FlatStyle.System };
            add = new Button { Text = "넣기", Location = new Point(D(474), D(418)), Size = new Size(D(90), D(34)), FlatStyle = FlatStyle.System };
            add.Click += (o, e) => { Result.Add(new KeyValuePair<string, Stickers.AddOpt>(files[index], new Stickers.AddOpt { Crop = crop, RemoveBg = bg.Checked && bg.Enabled })); Next(); };
            skip.Click += (o, e) => Next();
            rest.Click += (o, e) =>
            {
                Result.Add(new KeyValuePair<string, Stickers.AddOpt>(files[index], new Stickers.AddOpt { Crop = crop, RemoveBg = bg.Checked && bg.Enabled }));
                for (int i = index + 1; i < files.Length; i++)
                    Result.Add(new KeyValuePair<string, Stickers.AddOpt>(files[i], new Stickers.AddOpt { RemoveBg = false }));
                Close();
            };
            Controls.AddRange(new Control[] { title, hint, box, pl, preview, bg, bgNote, reset, info, rest, skip, add });
            AcceptButton = add;
            Next();
        }

        static Bitmap Checker(int n)
        {
            var b = new Bitmap(n * 2, n * 2);
            using (var g = Graphics.FromImage(b))
            {
                g.Clear(Color.White);
                using (var br = new SolidBrush(Color.FromArgb(225, 228, 232))) { g.FillRectangle(br, 0, 0, n, n); g.FillRectangle(br, n, n, n, n); }
            }
            return b;
        }

        static bool SolidFile(string f)
        {
            try
            {
                using (var img = Image.FromStream(new MemoryStream(File.ReadAllBytes(f))))
                return !Stickers.IsAnimated(img) && Stickers.CanRemoveBg(img);
            }
            catch { return false; }
        }

        void Next()
        {
            for (index++; index < files.Length; index++)
            {
                try
                {
                    using (var img = Image.FromStream(new MemoryStream(File.ReadAllBytes(files[index]))))
                    {
                        animated = Stickers.IsAnimated(img);
                        if (work != null) work.Dispose();
                        work = Stickers.Resize(img, 1000);
                    }
                    break;
                }
                catch { } // 그림이 아니면 건너뜀
            }
            if (index >= files.Length) { Close(); return; }
            crop = new RectangleF(0, 0, 1, 1);
            title.Text = Path.GetFileName(files[index]);
            info.Text = files.Length > 1 ? (index + 1) + " / " + files.Length : "";
            rest.Visible = index < files.Length - 1;
            box.Enabled = !animated;
            wantBg = true;
            if (animated) { bg.Visible = bg.Enabled = false; bgNote.Text = "움직이는 GIF는 자르지 않고 그대로 넣어요."; }
            box.Invalidate();
            UpdatePreview();
        }

        const string NoteCan = "그림 둘레의 배경을 투명하게 해요. 끄면 사진 그대로 들어가요.";
        const string NoteClear = "이미 배경이 투명한 그림이라 투명한 그대로 들어가요.";
        const string NotePhoto = "배경이 복잡한 사진이라 사진 그대로 들어가요.";

        public void UpdatePreview()
        {
            if (work == null) return;
            var old = preview.Image;
            using (var cut = Stickers.CropTo(work, crop))
            {
                if (!animated)
                {
                    // 자른 뒤 둘레 기준으로 다시 판단. 쓸 수 없을 땐 체크박스를 숨기고 안내만
                    bool clear = Stickers.IsTransparent(cut);
                    bool can = BgFeature && !clear && Stickers.CanRemoveBg(cut);
                    busy = true; bg.Visible = bg.Enabled = can; bg.Checked = can && wantBg; busy = false;
                    bgNote.Text = can ? NoteCan : clear ? NoteClear : BgFeature ? NotePhoto : "";
                    bgNote.Top = can ? D(292) : D(266);
                }
                var small = Stickers.Resize(cut, D(170));
                if (bg.Checked && bg.Enabled) Stickers.RemoveBackground(small);
                preview.Image = small;
            }
            if (old != null) old.Dispose();
        }

        // 자르기 화면
        class CropBox : Control
        {
            readonly AddImageForm F;
            int drag = -1; // 0~3 모서리, 4 옮기기, 5 새로 그리기
            PointF start; RectangleF startCrop;

            public CropBox(AddImageForm f)
            {
                F = f;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }

            RectangleF ImageRect()
            {
                if (F.work == null) return RectangleF.Empty;
                float pad = 10 * F.S; // 모서리 손잡이가 잘리지 않게
                float k = Math.Min((Width - 2 * pad) / F.work.Width, (Height - 2 * pad) / F.work.Height);
                float w = F.work.Width * k, h = F.work.Height * k;
                return new RectangleF((Width - w) / 2, (Height - h) / 2, w, h);
            }
            RectangleF CropRect()
            {
                var r = ImageRect();
                return new RectangleF(r.X + F.crop.X * r.Width, r.Y + F.crop.Y * r.Height, F.crop.Width * r.Width, F.crop.Height * r.Height);
            }
            PointF[] Corners(RectangleF c) { return new[] { new PointF(c.Left, c.Top), new PointF(c.Right, c.Top), new PointF(c.Left, c.Bottom), new PointF(c.Right, c.Bottom) }; }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(F.T.Hover);
                if (F.work == null) return;
                var r = ImageRect();
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(F.work, r);
                if (!Enabled) return;
                var c = CropRect();
                using (var dim = new SolidBrush(Color.FromArgb(140, 0, 0, 0)))
                using (var region = new Region(r))
                {
                    region.Exclude(c);
                    g.FillRegion(dim, region);
                }
                float hs = 9 * F.S;
                using (var pen = new Pen(Color.White, 2 * F.S))
                using (var line = new Pen(Color.FromArgb(160, 255, 255, 255), 1))
                {
                    g.DrawRectangle(pen, c.X, c.Y, c.Width, c.Height);
                    for (int i = 1; i <= 2; i++)
                    {
                        g.DrawLine(line, c.X + c.Width * i / 3, c.Y, c.X + c.Width * i / 3, c.Bottom);
                        g.DrawLine(line, c.X, c.Y + c.Height * i / 3, c.Right, c.Y + c.Height * i / 3);
                    }
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    foreach (var p in Corners(c))
                    {
                        g.FillEllipse(Brushes.White, p.X - hs / 2, p.Y - hs / 2, hs, hs);
                        using (var ac = new Pen(F.T.Accent, 2 * F.S)) g.DrawEllipse(ac, p.X - hs / 2, p.Y - hs / 2, hs, hs);
                    }
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                if (F.work == null) return;
                var c = CropRect();
                var cs = Corners(c);
                drag = 5;
                for (int i = 0; i < 4; i++)
                    if (Math.Abs(cs[i].X - e.X) <= 12 * F.S && Math.Abs(cs[i].Y - e.Y) <= 12 * F.S) { drag = i; break; }
                bool full = F.crop.Width > 0.999f && F.crop.Height > 0.999f; // 처음엔 안쪽을 끌면 새로 그리기
                if (drag == 5 && c.Contains(e.Location) && !full) drag = 4;
                start = ToUnit(e.Location); startCrop = F.crop;
                Capture = true;
            }

            PointF ToUnit(Point p)
            {
                var r = ImageRect();
                return new PointF(Math.Max(0, Math.Min(1, (p.X - r.X) / r.Width)), Math.Max(0, Math.Min(1, (p.Y - r.Y) / r.Height)));
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (drag < 0)
                {
                    if (F.work == null) return;
                    var c = CropRect(); var cs = Corners(c);
                    Cursor = Cursors.Cross;
                    for (int i = 0; i < 4; i++)
                        if (Math.Abs(cs[i].X - e.X) <= 12 * F.S && Math.Abs(cs[i].Y - e.Y) <= 12 * F.S) { Cursor = i == 0 || i == 3 ? Cursors.SizeNWSE : Cursors.SizeNESW; return; }
                    if (c.Contains(e.Location)) Cursor = Cursors.SizeAll;
                    return;
                }
                var p = ToUnit(e.Location);
                var r = ImageRect();
                float minW = 16 / r.Width, minH = 16 / r.Height;
                var s0 = startCrop;
                float l = s0.Left, t = s0.Top, rr = s0.Right, b = s0.Bottom;
                if (drag == 4)
                {
                    float dx = p.X - start.X, dy = p.Y - start.Y;
                    dx = Math.Max(-l, Math.Min(1 - rr, dx)); dy = Math.Max(-t, Math.Min(1 - b, dy));
                    F.crop = new RectangleF(l + dx, t + dy, s0.Width, s0.Height);
                }
                else if (drag == 5)
                {
                    float x1 = Math.Min(start.X, p.X), y1 = Math.Min(start.Y, p.Y), x2 = Math.Max(start.X, p.X), y2 = Math.Max(start.Y, p.Y);
                    if (x2 - x1 >= minW && y2 - y1 >= minH) F.crop = RectangleF.FromLTRB(x1, y1, x2, y2);
                }
                else
                {
                    if (drag == 0 || drag == 2) l = Math.Min(p.X, rr - minW); else rr = Math.Max(p.X, l + minW);
                    if (drag == 0 || drag == 1) t = Math.Min(p.Y, b - minH); else b = Math.Max(p.Y, t + minH);
                    F.crop = RectangleF.FromLTRB(l, t, rr, b);
                }
                Invalidate();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                if (drag < 0) return;
                drag = -1; Capture = false;
                F.UpdatePreview();
            }
        }
    }
}
