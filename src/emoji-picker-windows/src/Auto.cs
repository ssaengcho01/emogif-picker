using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;

namespace EmojiPicker
{
    // 긴 글 자동 꾸미기: 글을 붙여넣으면 문장 끝마다 어울리는 이모지를 하나씩 붙여 줌
    class AutoForm : Form
    {
        readonly TextBox text;
        readonly ColorView view;
        readonly Button run, copy, put;
        readonly Label note;
        string before, after; // 되돌리기용
        public Action<string> Put; // 입력창 글을 이 글로 바꾸기 (입력하던 창이 있을 때만)

        int D(int v) { return (int)(v * S); }
        readonly float S;

        public AutoForm(string initial, bool canPut, Theme T, float s, Font font)
        {
            S = s;
            Text = "✨ 이모지 자동으로 붙이기";
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = MaximizeBox = false; ShowInTaskbar = true; TopMost = true;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            Font = font; BackColor = T.Bg; ForeColor = T.Fg;
            ClientSize = new Size(D(460), D(380));
            MinimumSize = Size;

            var hint = new Label
            {
                Text = "긴 글을 붙여넣으면 문장 끝마다 어울리는 이모지를 하나씩 붙여 줘요.",
                Location = new Point(D(16), D(12)), Size = new Size(D(428), D(22)), ForeColor = T.Muted,
            };
            text = new TextBox
            {
                Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, WordWrap = true,
                Location = new Point(D(16), D(38)), Size = new Size(D(428), D(250)),
                BackColor = T.Input, ForeColor = T.Fg, Font = new Font(font.FontFamily, 10.5f),
            };
            // 꾸민 결과는 컬러 이모지로 보여 주고, 누르면 고칠 수 있는 입력칸으로 바뀜
            view = new ColorView(T, S) { Location = text.Location, Size = text.Size, Font = text.Font, Visible = false };
            view.Click += (o, e) => Edit();
            view.EmojiClicked += (pos, len, pt, right) => ChangeEmoji(pos, len, pt, right);
            text.LostFocus += (o, e) => { if (ActiveControl != text) ShowView(); };
            note = new Label { Location = new Point(D(16), D(294)), Size = new Size(D(428), D(22)), ForeColor = T.Muted };

            run = new Button { Text = "✨ 자동으로 붙이기", Location = new Point(D(16), D(326)), Size = new Size(D(150), D(36)), FlatStyle = FlatStyle.System };
            copy = new Button { Text = "복사", Location = new Point(D(250), D(326)), Size = new Size(D(80), D(36)), FlatStyle = FlatStyle.System };
            put = new Button { Text = "입력창에 넣기", Location = new Point(D(338), D(326)), Size = new Size(D(106), D(36)), FlatStyle = FlatStyle.System, Visible = canPut };
            if (!canPut) copy.Location = new Point(D(364), D(326));

            run.Click += (o, e) => Toggle();
            copy.Click += (o, e) =>
            {
                try { Clipboard.SetText(text.Text); note.Text = "복사했어요. 보낼 곳에서 Ctrl+V로 붙여넣으세요."; } catch { note.Text = "복사하지 못했어요. 다시 눌러 주세요."; }
            };
            put.Click += (o, e) => { var t = text.Text; Close(); if (Put != null) Put(t); };
            text.TextChanged += (o, e) =>
            {
                // 꾸민 뒤 글을 고치면 되돌리기는 없앰
                if (before != null && text.Text != after) { before = after = null; run.Text = "✨ 자동으로 붙이기"; }
            };

            // 창을 키우면 글 칸도 같이 커지게
            hint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            text.Anchor = view.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            note.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            run.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            copy.Anchor = put.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            Controls.AddRange(new Control[] { hint, view, text, note, run, copy, put });
            view.BringToFront();

            text.Text = initial ?? "";
            if (text.Text.Trim().Length > 0) Toggle();
            else note.Text = "여기에 글을 붙여넣고 ✨ 버튼을 눌러 주세요.";
            Shown += (o, e) => { if (view.Visible) run.Focus(); else { text.Focus(); text.SelectionStart = text.TextLength; text.SelectionLength = 0; } };
        }

        // 이모지가 들어 있으면 컬러 보기로
        void ShowView()
        {
            if (text.Text.Trim().Length == 0 || Recommend.ParseEmojis(text.Text).Count == 0) { view.Visible = false; return; }
            view.Content = text.Text;
            view.Visible = true;
        }

        // 이모지를 누르면: 그 문장에 어울리는 다른 이모지로 바꾸거나 지우기
        // 이모지를 누르면 평소 피커 창(검색·추천 그대로)을 열어서 고른 것으로 바꿈
        public Action<string, Point, Action<string>> OpenPicker;

        void ChangeEmoji(int pos, int len, Point screen, bool right)
        {
            string flat = text.Text.Replace("\r\n", "\n");
            if (pos < 0 || pos + len > flat.Length) return;
            string cur = flat.Substring(pos, len);
            string sentence = "";
            int at = 0;
            foreach (var part in Recommend.SplitSentences(flat))
            {
                if (pos > at && pos <= at + part.Length) { sentence = part; break; } // 이모지 바로 앞 글자가 들어 있는 문장
                at += part.Length;
            }
            var plain = sentence.Replace(cur, "");
            Action<string> apply0 = rep =>
            {
                string next = flat.Substring(0, pos) + rep + flat.Substring(pos + len);
                bool wasAuto = before != null;
                var keepBefore = before;
                text.Text = next.Replace("\n", "\r\n");
                if (wasAuto) { before = keepBefore; after = text.Text; run.Text = "↩ 되돌리기"; }
                note.Text = rep.Length == 0 ? "이모지를 지웠어요." : "이모지를 바꿨어요. 오른쪽 클릭하면 지워요.";
                ShowView();
                Activate();
            };
            if (right) { apply0(""); return; }
            if (OpenPicker != null) { OpenPicker(plain.Trim(), screen, apply0); return; }
            var alts = Recommend.For(plain).Where(x => !x.IsText && x.Ch != cur && x.Ch != cur.TrimEnd('\uFE0F')).Take(12).ToList();
            var menu = new ContextMenuStrip { Font = Font, ShowImageMargin = true, ImageScalingSize = new Size(D(22), D(22)) };
            Action<string> apply = rep =>
            {
                string next = flat.Substring(0, pos) + rep + flat.Substring(pos + len);
                bool wasAuto = before != null;
                var keepBefore = before;
                text.Text = next.Replace("\n", "\r\n");
                if (wasAuto) { before = keepBefore; after = text.Text; run.Text = "↩ 되돌리기"; }
                note.Text = rep.Length == 0 ? "이모지를 지웠어요." : "이모지를 바꿨어요.";
                ShowView();
            };
            foreach (var a in alts)
            {
                var bmp = new Bitmap(D(22), D(22));
                using (var g = Graphics.FromImage(bmp)) Draw.Emoji(g, a, new RectangleF(0, 0, bmp.Width, bmp.Height));
                var ch = a.Ch;
                menu.Items.Add(a.Label, bmp, (o, e) => apply(ch));
            }
            if (alts.Count > 0) menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("이 이모지 지우기", null, (o, e) => apply(""));
            menu.Items.Add("다른 이모지 직접 고르기…", null, (o, e) =>
            {
                // 직접 고치기: 이모지를 선택해 둔 채 입력칸으로 (Ctrl+Win+. 으로 피커를 열어 바꿀 수 있음)
                view.Visible = false;
                string t = text.Text;
                int idx = 0, flatIdx = 0;
                while (flatIdx < pos && idx < t.Length) { if (t[idx] == '\r') idx++; idx++; flatIdx++; }
                text.Focus();
                text.SelectionStart = idx; text.SelectionLength = len;
                note.Text = "선택한 이모지를 Ctrl+Win+. 으로 피커를 열어 바꿔 보세요.";
            });
            menu.Show(screen);
        }

        void Edit()
        {
            view.Visible = false;
            text.Focus();
            text.SelectionStart = text.TextLength; text.SelectionLength = 0;
        }

        void Toggle()
        {
            if (before != null) // 되돌리기
            {
                var b = before; before = after = null;
                text.Text = b; run.Text = "✨ 자동으로 붙이기"; note.Text = "원래 글로 되돌렸어요."; ShowView();
                return;
            }
            var src = text.Text;
            if (src.Trim().Length == 0) { note.Text = "먼저 글을 붙여넣어 주세요."; return; }
            int added;
            var res = Recommend.AutoDecorate(src.Replace("\r\n", "\n"), out added).Replace("\n", "\r\n");
            if (res == text.Text) { note.Text = "붙일 만한 이모지를 찾지 못했어요."; return; }
            text.Text = res;
            before = src; after = res;
            run.Text = "↩ 되돌리기";
            note.Text = "이모지 " + added + "개를 붙였어요. 이모지를 누르면 바꾸고, 오른쪽 클릭하면 지워요.";
            ShowView();
        }
    }
}

namespace EmojiPicker
{
    // 글 + 컬러 이모지 보기 (Windows 10 입력칸은 이모지를 흑백으로만 그려서, 결과는 직접 그려서 보여 줌)
    class ColorView : Control
    {
        readonly Theme T;
        readonly float S;
        string text = "";
        int scroll, contentH;

        class Piece { public string Text; public Emoji E; public RectangleF R; public int Pos; }
        public event Action<int, int, Point, bool> EmojiClicked; // (글 안 위치, 길이, 화면 좌표, 오른쪽 클릭)
        List<Piece> pieces = new List<Piece>();

        public ColorView(Theme t, float s)
        {
            T = t; S = s;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = T.Input; ForeColor = T.Fg; Cursor = Cursors.IBeam;
        }

        public string Content { get { return text; } set { text = value ?? ""; scroll = 0; Relayout(); } }

        // 글자 하나씩 보면서 이모지(가장 긴 것부터)와 일반 글자를 나눔
        static List<Piece> Tokenize(string s)
        {
            var list = new List<Piece>();
            int i = 0;
            while (i < s.Length)
            {
                Emoji hit = null; int used = 0;
                for (int len = Math.Min(14, s.Length - i); len >= 1; len--)
                {
                    var sub = s.Substring(i, len);
                    Emoji e;
                    if ((Data.ByChar.TryGetValue(sub, out e) || Data.ByChar.TryGetValue(sub + "️", out e)) && !e.IsText) { hit = e; used = len; break; }
                }
                if (hit != null)
                {
                    if (i + used < s.Length && s[i + used] == '️') used++;
                    list.Add(new Piece { E = hit, Text = s.Substring(i, used), Pos = i }); i += used; continue;
                }
                int n = char.IsHighSurrogate(s[i]) && i + 1 < s.Length ? 2 : 1;
                string c = s.Substring(i, n);
                if (c == "️" || c == "‍") { i += n; continue; }
                list.Add(new Piece { Text = c }); i += n;
            }
            return list;
        }

        void Relayout()
        {
            pieces = new List<Piece>();
            float pad = 6 * S, maxW = Width - pad * 2 - 8 * S;
            float lineH = Font.Height * 1.25f, emo = Font.Height * 1.1f;
            float x = pad, y = pad;
            var toks = Tokenize(text.Replace("\r\n", "\n"));
            using (var g = CreateGraphics())
            {
                Func<string, float> W = str => TextRenderer.MeasureText(g, str, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
                int k = 0;
                while (k < toks.Count)
                {
                    var p = toks[k];
                    if (p.E == null && p.Text == "\n") { x = pad; y += lineH; k++; continue; }
                    if (p.E != null)
                    {
                        if (x + emo > pad + maxW && x > pad) { x = pad; y += lineH; }
                        p.R = new RectangleF(x, y + (lineH - emo) / 2, emo, emo); pieces.Add(p); x += emo + 1 * S; k++; continue;
                    }
                    // 낱말 단위로 묶어서 줄바꿈 (띄어쓰기 기준)
                    int j = k; var sb = new System.Text.StringBuilder();
                    bool space = char.IsWhiteSpace(p.Text[0]);
                    while (j < toks.Count && toks[j].E == null && toks[j].Text != "\n" && char.IsWhiteSpace(toks[j].Text[0]) == space) { sb.Append(toks[j].Text); j++; }
                    string word = sb.ToString();
                    float w = W(word);
                    if (space) { if (x > pad) { pieces.Add(new Piece { Text = word, R = new RectangleF(x, y, w, lineH) }); x += w; } k = j; continue; }
                    if (x + w > pad + maxW && x > pad) { x = pad; y += lineH; }
                    if (w > maxW) // 너무 긴 낱말은 글자 단위로
                    {
                        foreach (var t in toks.GetRange(k, j - k))
                        {
                            float cw = W(t.Text);
                            if (x + cw > pad + maxW) { x = pad; y += lineH; }
                            pieces.Add(new Piece { Text = t.Text, R = new RectangleF(x, y, cw, lineH) }); x += cw;
                        }
                    }
                    else { pieces.Add(new Piece { Text = word, R = new RectangleF(x, y, w, lineH) }); x += w; }
                    k = j;
                }
            }
            contentH = (int)(y + lineH + pad);
            scroll = Math.Max(0, Math.Min(scroll, contentH - Height));
            Invalidate();
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); if (IsHandleCreated) Relayout(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (IsHandleCreated) Relayout(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Relayout(); }

        Piece EmojiAt(Point p)
        {
            foreach (var x in pieces) if (x.E != null && RectangleF.Inflate(x.R, 2 * S, 2 * S).Contains(p.X, p.Y + scroll)) return x;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); Cursor = EmojiAt(e.Location) != null ? Cursors.Hand : Cursors.IBeam; }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            var hit = EmojiAt(e.Location);
            if (hit != null && EmojiClicked != null)
            {
                EmojiClicked(hit.Pos, hit.Text.Length, PointToScreen(new Point((int)hit.R.X, (int)(hit.R.Bottom - scroll + 2 * S))), e.Button == MouseButtons.Right);
                return;
            }
            base.OnClick(e); // 글을 누르면 고치기
        }

        protected override void OnClick(EventArgs e) { } // OnMouseClick에서 처리

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int step = (int)(Font.Height * 1.25f) * Math.Max(1, SystemInformation.MouseWheelScrollLines);
            scroll = Math.Max(0, Math.Min(Math.Max(0, contentH - Height), scroll - Math.Sign(e.Delta) * step));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.TranslateTransform(0, -scroll);
            foreach (var p in pieces)
            {
                if (p.R.Bottom < scroll || p.R.Top > scroll + Height) continue;
                if (p.E != null) Draw.Emoji(g, p.E, p.R);
                else
                {
                    var pt = new Point((int)p.R.X, (int)(p.R.Y + (p.R.Height - Font.Height) / 2));
                    TextRenderer.DrawText(g, p.Text, Font, pt, ForeColor, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                }
            }
            g.ResetTransform();
            using (var pen = new Pen(T.Line)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            if (contentH > Height) // 얇은 스크롤 표시
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                float h = Math.Max(14 * S, Height * (float)Height / contentH);
                float y = (Height - h) * scroll / Math.Max(1f, contentH - Height);
                var r = new RectangleF(Width - 7 * S, y, 4 * S, h);
                using (var b = new SolidBrush(Color.FromArgb(150, T.Muted)))
                using (var path = Draw.Round(r, r.Width / 2)) g.FillPath(b, path);
            }
        }
    }
}
