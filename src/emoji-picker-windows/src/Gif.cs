using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EmojiPicker
{
    class GifItem
    {
        public string Id, Title, ThumbUrl, FullUrl, PageUrl;
        public int ThumbW, ThumbH;
    }

    // ---------- GIPHY (무료 키: 시간당 100번 검색) ----------
    // 키는 사용하는 사람이 직접 한 번 발급해서 붙여넣음 (앱에 키를 넣어 배포하지 않음)
    static class Giphy
    {
        static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EmojiPicker");
        // 2026-11-24: GIPHY가 새 키까지 거절해서 KLIPY(무료, Tenor 대신 나온 서비스)로 바꿈. 키는 klipy.txt
        static readonly string KeyFile = Path.Combine(Dir, "klipy.txt");
        public static readonly string CacheDir = Path.Combine(Path.GetTempPath(), "EmojiPicker-gif");
        public const string KeyPage = "https://partner.klipy.com/api-keys";

        static string key;
        public static string Key
        {
            get
            {
                if (key == null) { try { key = AiSettings.Unprotect(File.ReadAllText(KeyFile).Trim()); } catch { key = ""; } }
                return key;
            }
            set
            {
                key = (value ?? "").Trim();
                try { Directory.CreateDirectory(Dir); File.WriteAllText(KeyFile, AiSettings.Protect(key)); } catch { }
            }
        }

        // 브라우저 이름(User-Agent) 없이 요청하면 GIPHY 서버가 403으로 막음 → 이름을 붙여서 요청
        static WebClient Client()
        {
            var wc = new WebClient { Encoding = Encoding.UTF8 };
            wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36 EmoGIFPicker/1.0";
            wc.Headers[HttpRequestHeader.Accept] = "application/json, image/*, */*";
            return wc;
        }

        public static bool HasKey { get { return Key.Length > 0; } }
        public class BadKeyException : Exception { public BadKeyException(string detail) : base(detail) { } }

        // 붙여넣은 글에서 키만 골라냄 (GIPHY 키 = 영어·숫자 32자). "API Key: xxx"처럼 다른 글이 섞여도 됨
        public static string CleanKey(string s)
        {
            s = (s ?? "").Trim();
            // 가장 긴 영어·숫자 덩어리를 키로 봄 ("API Key: xxx"처럼 다른 글이 섞여도 됨)
            string best = s;
            int len = 0;
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(s, "[A-Za-z0-9_\\-]{16,}"))
                if (m.Value.Length > len) { best = m.Value; len = m.Value.Length; }
            return best;
        }

        // 자주 쓰는 한국어 감정·상황 말은 영어로 바꿔 검색 (GIPHY는 영어 태그가 훨씬 많음)
        static readonly string[] KoEn =
        {
            "기쁨 기뻐 기쁘 행복 신나 신남=happy", "슬픔 슬퍼 슬프 눈물 우는 울어 ㅠㅠ ㅜㅜ=sad crying", "축하=congratulations",
            "고마워 고맙 감사 땡큐=thank you", "사랑 좋아해 러브=love", "화나 화남 짜증 분노 빡=angry", "놀람 놀라 깜짝 헐=shocked",
            "웃김 웃겨 ㅋㅋ ㅎㅎ 웃음=lol laughing", "안녕 하이=hello", "바이 잘가=bye", "잘자 굿밤=good night", "굿모닝 좋은아침=good morning",
            "박수=applause", "춤=dance", "고양이 냥이=cat", "강아지 멍멍=dog", "생일=happy birthday", "화이팅 파이팅 힘내 응원=you can do it",
            "피곤 졸려 졸림=tired", "배고파 배고픔=hungry", "대박 와우=wow", "미안 죄송=sorry", "좋아 오케이 ㅇㅋ 굿=thumbs up yes",
            "싫어 아니=no", "멘붕 당황=confused", "부끄 수줍=shy", "최고 짱=awesome", "기다려 기다림=waiting", "출근 월요일=monday",
            "퇴근 금요일 불금=friday", "부탁 제발=please", "축하해 오픈 합격=congrats celebrate", "하트=heart", "윙크=wink", "멋져 멋짐 쿨=cool",
        };

        static string Translate(string q)
        {
            var flat = q.Replace(" ", "");
            foreach (var row in KoEn)
            {
                var kv = row.Split('=');
                foreach (var k in kv[0].Split(' '))
                    if (flat == k || (k.Length >= 2 && flat.StartsWith(k))) return kv[1];
            }
            return q;
        }

        // 같은 검색어는 다시 부르지 않음 (무료 한도 아끼기)
        static readonly Dictionary<string, List<GifItem>> cache = new Dictionary<string, List<GifItem>>();

        public static List<GifItem> Search(string q)
        {
            q = (q ?? "").Trim();
            List<GifItem> hit;
            lock (cache) if (cache.TryGetValue(q, out hit)) return hit;
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
            // KLIPY: 키가 주소 안에 들어감 https://api.klipy.com/api/v1/{키}/gifs/search
            string baseUrl = "https://api.klipy.com/api/v1/" + Uri.EscapeDataString(Key) + "/gifs/";
            string url = q.Length == 0
                ? baseUrl + "trending?per_page=30&page=1&rating=g&locale=ko_KR"
                : baseUrl + "search?q=" + Uri.EscapeDataString(Translate(q)) + "&per_page=30&page=1&rating=g&locale=ko_KR";
            string json;
            try
            {
                using (var wc = Client()) json = wc.DownloadString(url);
            }
            catch (WebException ex)
            {
                var hr = ex.Response as HttpWebResponse;
                if (hr != null && ((int)hr.StatusCode == 401 || (int)hr.StatusCode == 403 || (int)hr.StatusCode == 404))
                {
                    // GIPHY가 알려 준 이유를 그대로 보여 줌 (원인 찾기용)
                    string body = "";
                    try { using (var r = new StreamReader(hr.GetResponseStream(), Encoding.UTF8)) body = r.ReadToEnd(); } catch { }
                    var mm = System.Text.RegularExpressions.Regex.Match(body, "\"message\"\\s*:\\s*\"([^\"]*)\"");
                    string k = Key;
                    throw new BadKeyException((int)hr.StatusCode + (mm.Success ? " " + mm.Groups[1].Value : (body.Trim().Length > 0 ? " " + System.Text.RegularExpressions.Regex.Replace(body, "<[^>]+>|\\s+", " ").Trim().Substring(0, Math.Min(60, System.Text.RegularExpressions.Regex.Replace(body, "<[^>]+>|\\s+", " ").Trim().Length)) : "")) + " · 저장된 키: " + (k.Length >= 6 ? k.Substring(0, 4) + "…" + k.Substring(k.Length - 2) : "(비어 있음)") + " (" + k.Length + "자)");
                }
                if (hr != null && (int)hr.StatusCode == 429) throw new Exception("검색을 너무 자주 했어요. 1분쯤 뒤에 다시 써 주세요.");
                throw new Exception("인터넷 연결을 확인해 주세요.");
            }
            var list = Parse(json);
            lock (cache) cache[q] = list;
            return list;
        }

        // KLIPY 답: { result, data: { data: [ { id, slug, title, type, file: { hd/md/sm/xs: { gif: { url, width, height, size } } } } ] } }
        public static List<GifItem> ParseForTest(string json) { return Parse(json); }

        static List<GifItem> Parse(string json)
        {
            var list = new List<GifItem>();
            var root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<Dictionary<string, object>>(json);
            var outer = Get(root, "data") as Dictionary<string, object>;
            if (outer == null) return ParseGiphy(json);
            var items = Get(outer, "data") as System.Collections.IEnumerable;
            if (items == null) return list;
            foreach (var o in items)
            {
                var d = o as Dictionary<string, object>;
                if (d == null) continue;
                if (Convert.ToString(Get(d, "type")) == "ad") continue; // 광고 칸은 건너뜀
                var file = Get(d, "file") as Dictionary<string, object>;
                if (file == null) continue;
                Func<string, Dictionary<string, object>> gifOf = sz =>
                {
                    var f = Get(file, sz) as Dictionary<string, object>;
                    var g = f == null ? null : Get(f, "gif") as Dictionary<string, object>;
                    return g != null && !string.IsNullOrEmpty(Convert.ToString(Get(g, "url"))) ? g : null;
                };
                var thumb = gifOf("sm") ?? gifOf("xs") ?? gifOf("md");
                // 카톡에서 바로 재생되도록 1MB 이하 중 가장 큰 것
                Dictionary<string, object> full = null;
                foreach (var sz in new[] { "md", "sm", "hd", "xs" })
                {
                    var g = gifOf(sz);
                    if (g == null) continue;
                    long size; long.TryParse(Convert.ToString(Get(g, "size")), out size);
                    if (full == null) full = g;
                    if (size > 0 && size <= 1000000) { full = g; break; }
                }
                if (thumb == null || full == null) continue;
                int w, h;
                int.TryParse(Convert.ToString(Get(thumb, "width")), out w);
                int.TryParse(Convert.ToString(Get(thumb, "height")), out h);
                list.Add(new GifItem
                {
                    Id = "k" + Convert.ToString(Get(d, "id")),
                    Title = Convert.ToString(Get(d, "title")),
                    PageUrl = "https://klipy.com/gifs/" + Convert.ToString(Get(d, "slug")),
                    ThumbUrl = Convert.ToString(Get(thumb, "url")),
                    FullUrl = Convert.ToString(Get(full, "url")),
                    ThumbW = w, ThumbH = h,
                });
            }
            return list;
        }

        static List<GifItem> ParseGiphy(string json)
        {
            var list = new List<GifItem>();
            var root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<Dictionary<string, object>>(json);
            object data;
            if (!root.TryGetValue("data", out data) || !(data is object[] || data is System.Collections.ArrayList)) return list;
            foreach (var o in (System.Collections.IEnumerable)data)
            {
                var d = o as Dictionary<string, object>;
                if (d == null) continue;
                var images = Get(d, "images") as Dictionary<string, object>;
                if (images == null) continue;
                var thumb = Get(images, "fixed_width_small") as Dictionary<string, object> ?? Get(images, "fixed_width") as Dictionary<string, object>;
                // 카톡에서 바로 재생되도록 작고 가벼운 GIF를 고름 (1MB 이하 우선)
                Dictionary<string, object> full = null;
                foreach (var name in new[] { "fixed_width", "fixed_height", "downsized", "fixed_width_small" })
                {
                    var r = Get(images, name) as Dictionary<string, object>;
                    if (r == null || string.IsNullOrEmpty(Convert.ToString(Get(r, "url")))) continue;
                    long size; long.TryParse(Convert.ToString(Get(r, "size")), out size);
                    if (full == null) full = r;
                    if (size > 0 && size <= 1000000) { full = r; break; }
                }
                if (full == null) full = Get(images, "original") as Dictionary<string, object>;
                if (thumb == null || full == null) continue;
                int w, h;
                int.TryParse(Convert.ToString(Get(thumb, "width")), out w);
                int.TryParse(Convert.ToString(Get(thumb, "height")), out h);
                list.Add(new GifItem
                {
                    Id = Convert.ToString(Get(d, "id")),
                    Title = Convert.ToString(Get(d, "title")),
                    PageUrl = Convert.ToString(Get(d, "url")),
                    ThumbUrl = Convert.ToString(Get(thumb, "url")),
                    FullUrl = Convert.ToString(Get(full, "url")),
                    ThumbW = w, ThumbH = h,
                });
            }
            return list;
        }

        static object Get(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) ? v : null;
        }

        public static byte[] Download(string url)
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
            using (var wc = Client()) return wc.DownloadData(url);
        }

        // 넣을 GIF 파일을 받아서 임시 폴더에 저장
        public static string DownloadFull(GifItem g)
        {
            Directory.CreateDirectory(CacheDir);
            var safe = new string((g.Id ?? "gif").Where(char.IsLetterOrDigit).ToArray());
            var path = Path.Combine(CacheDir, (safe.Length > 0 ? safe : "gif") + "-s.gif");
            if (!File.Exists(path)) File.WriteAllBytes(path, Download(g.FullUrl));
            return path;
        }

        // 하루 지난 임시 GIF 지우기
        public static void CleanCache()
        {
            try
            {
                if (!Directory.Exists(CacheDir)) return;
                foreach (var f in Directory.GetFiles(CacheDir))
                    if (File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-1)) File.Delete(f);
            }
            catch { }
        }

        // 클립보드에 GIF를 "그림"으로 올림 (카카오톡은 파일 붙여넣기를 받지 않고 그림만 받음)
        // 움직이는 GIF로 보내려면 칸을 카톡 창으로 끌어다 놓으면 됨
        public static void PutOnClipboard(string path, GifItem g)
        {
            Stickers.PutImageOnClipboard(path);
        }

        // Windows 클립보드 HTML 형식 (머리말에 위치값이 필요함)
        static string HtmlClip(string fragment)
        {
            const string header = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
            string pre = "<html><body>\r\n<!--StartFragment-->", post = "<!--EndFragment-->\r\n</body></html>";
            int hl = string.Format(header, 0, 0, 0, 0).Length;
            var enc = Encoding.UTF8;
            int startHtml = hl;
            int startFrag = startHtml + enc.GetByteCount(pre);
            int endFrag = startFrag + enc.GetByteCount(fragment);
            int endHtml = endFrag + enc.GetByteCount(post);
            return string.Format(header, startHtml, endHtml, startFrag, endFrag) + pre + fragment + post;
        }
    }

    // ---------- 이모지 창 안의 GIF 칸 ----------
    class GifPanel : Panel
    {
        readonly Theme T;
        readonly float S;
        readonly FlowLayoutPanel flow;
        readonly Panel keyPanel;
        readonly Label status;
        readonly TextBox keyBox;
        readonly Label keyTitle;
        readonly Label keyNote;
        const string KeyNoteText = "무료 키예요. 요금은 나가지 않아요.";
        int searchSeq;
        public event Action<GifItem> Picked;
        public event Action Dropped;
        public Action KeepOpen; // 브라우저를 여는 동안 창이 닫히지 않게

        public GifPanel(Theme t, float s, Font font)
        {
            T = t; S = s;
            BackColor = T.Bg;
            Font = font;

            status = new Label { Dock = DockStyle.Top, Height = (int)(24 * S), ForeColor = T.Muted, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding((int)(8 * S), 0, 0, 0), Font = new Font(font.FontFamily, 8.5f) };
            flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = T.Bg, Padding = new Padding((int)(4 * S)) };

            // 키가 없을 때: 한 번만 하면 되는 안내
            keyPanel = new Panel { Dock = DockStyle.Fill, BackColor = T.Bg, Padding = new Padding((int)(14 * S)) };
            int y = (int)(10 * S), w = (int)(310 * S);
            var title = keyTitle = new Label { Text = "GIF를 쓰려면 무료 KLIPY 키가 한 번 필요해요.", Location = new Point((int)(14 * S), y), Size = new Size(w, (int)(22 * S)), ForeColor = T.Fg };
            var s1 = new Label { Text = "① 버튼을 눌러 KLIPY에 가입·로그인한 뒤\n    API Keys에서 키 만들기 (앱 이름만 입력)", Location = new Point((int)(14 * S), y + (int)(30 * S)), Size = new Size(w, (int)(38 * S)), ForeColor = T.Muted };
            var open = new Button { Text = "키 발급 페이지 열기", Location = new Point((int)(14 * S), y + (int)(72 * S)), Size = new Size((int)(150 * S), (int)(28 * S)), FlatStyle = FlatStyle.System };
            open.Click += (o, e) => { if (KeepOpen != null) KeepOpen(); try { Process.Start(Giphy.KeyPage); } catch { } };
            var s2 = new Label { Text = "② 만들어진 키를 복사해서 붙여넣기", Location = new Point((int)(14 * S), y + (int)(112 * S)), Size = new Size(w, (int)(20 * S)), ForeColor = T.Muted };
            keyBox = new TextBox { Location = new Point((int)(14 * S), y + (int)(136 * S)), Size = new Size((int)(220 * S), (int)(24 * S)) };
            var paste = new Button { Text = "붙여넣기", Location = new Point((int)(240 * S), y + (int)(135 * S)), Size = new Size((int)(84 * S), (int)(26 * S)), FlatStyle = FlatStyle.System };
            paste.Click += (o, e) =>
            {
                try { if (Clipboard.ContainsText()) keyBox.Text = Clipboard.GetText().Trim(); } catch { }
                if (keyBox.Text.Trim().Length > 0) { keyBox.Text = Giphy.CleanKey(keyBox.Text); Giphy.Key = keyBox.Text; Show(""); }
            };
            keyBox.KeyDown += (o, e) => { if (e.KeyCode == Keys.Enter && keyBox.Text.Trim().Length > 0) { e.SuppressKeyPress = true; keyBox.Text = Giphy.CleanKey(keyBox.Text); Giphy.Key = keyBox.Text; Show(""); } };
            var note = keyNote = new Label
            {
                Text = KeyNoteText,
                Location = new Point((int)(14 * S), y + (int)(172 * S)), Size = new Size(w, (int)(36 * S)), ForeColor = T.Muted, Font = new Font(font.FontFamily, 8.5f),
            };
            keyPanel.Controls.AddRange(new Control[] { title, s1, open, s2, keyBox, paste, note });

            Controls.Add(flow);
            Controls.Add(status);
            Controls.Add(keyPanel);
        }

        // 검색어로 GIF 보여 주기 (빈 칸이면 인기 GIF)
        public void Show(string q)
        {
            bool has = Giphy.HasKey;
            keyPanel.Visible = !has;
            flow.Visible = status.Visible = has;
            if (!has) return;
            int seq = ++searchSeq;
            status.Text = q.Length == 0 ? "인기 GIF 불러오는 중…" : "\"" + q + "\" 찾는 중…";
            ClearThumbs();
            var th = new Thread(() =>
            {
                List<GifItem> list = null;
                string err = null;
                bool bad = false;
                try { list = Giphy.Search(q); } catch (Giphy.BadKeyException bk) { bad = true; err = bk.Message; } catch (Exception ex) { err = ex.Message; }
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        if (seq != searchSeq) return;
                        if (bad) { ShowKeyHelp(true, err); return; }
                        if (err != null) { status.Text = err; return; }
                        status.Text = list.Count == 0 ? "찾는 GIF가 없어요" : (q.Length == 0 ? "지금 인기 있는 GIF" : "\"" + q + "\" GIF") + " · 카톡 창으로 드래그해서 넣기";
                        Fill(list, seq);
                    }));
                }
                catch { }
            });
            th.IsBackground = true;
            th.Start();
        }

        void ClearThumbs()
        {
            var old = flow.Controls.Cast<Control>().ToList();
            flow.Controls.Clear();
            foreach (var c in old)
            {
                var pb = c as PictureBox;
                if (pb != null && pb.Image != null) { var img = pb.Image; pb.Image = null; img.Dispose(); }
                c.Dispose();
            }
        }

        public void ShowSamples(List<Image> imgs, string label) // 화면 점검용
        {
            keyPanel.Visible = false; flow.Visible = status.Visible = true;
            status.Text = label;
            ClearThumbs();
            foreach (var img in imgs) { var pb = Thumb(null); pb.Image = img; flow.Controls.Add(pb); }
        }

        // 키가 틀렸을 때도 같은 안내 화면을 다시 보여 줌 (새 키를 붙여넣을 수 있게)
        public void ShowKeyHelp(bool wrong = false, string detail = null)
        {
            keyNote.Text = detail != null ? "KLIPY 답: " + detail + "\n키를 복사 버튼으로 다시 복사해서 붙여넣어 주세요." : KeyNoteText;
            keyNote.ForeColor = detail != null ? Color.FromArgb(220, 60, 60) : T.Muted;
            keyNote.Height = (int)(54 * S);
            keyTitle.Text = wrong ? "KLIPY 키가 맞지 않아요. 새 키를 붙여넣어 주세요." : "GIF를 쓰려면 무료 KLIPY 키가 한 번 필요해요.";
            keyTitle.ForeColor = wrong ? Color.FromArgb(220, 60, 60) : T.Fg;
            if (wrong) keyBox.Text = "";
            keyPanel.Visible = true; flow.Visible = status.Visible = false;
        }

        PictureBox Thumb(GifItem g)
        {
            int m = (int)(2 * S);
            int avail = flow.ClientSize.Width - flow.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 2;
            int n = Math.Max(3, avail / (int)(104 * S)); // 창을 넓히면 한 줄에 더 많이
            int cw = (avail - 2 * n * m) / n;
            var pb = new PictureBox
            {
                Size = new Size(cw, (int)(cw * 0.8f)), SizeMode = PictureBoxSizeMode.Zoom, BackColor = T.Hover,
                Margin = new Padding(m), Cursor = Cursors.Hand, Tag = g,
            };
            if (g != null)
            {
                pb.Click += (o, e) => { if (!Drag.JustDragged(pb) && Picked != null) Picked(g); };
                Drag.Enable(pb, () => Giphy.DownloadFull(g), () => { if (Dropped != null) Dropped(); });
                new ToolTip().SetToolTip(pb, string.IsNullOrEmpty(g.Title) ? "GIF" : g.Title);
            }
            return pb;
        }

        void Fill(List<GifItem> list, int seq)
        {
            flow.SuspendLayout();
            var boxes = new List<PictureBox>();
            foreach (var g in list) { var pb = Thumb(g); flow.Controls.Add(pb); boxes.Add(pb); }
            flow.ResumeLayout();
            // 미리보기는 4개씩 나눠서 받기
            var queue = new Queue<PictureBox>(boxes);
            for (int k = 0; k < 4; k++)
            {
                var th = new Thread(() =>
                {
                    while (true)
                    {
                        PictureBox pb;
                        lock (queue) { if (queue.Count == 0 || seq != searchSeq) return; pb = queue.Dequeue(); }
                        try
                        {
                            var bytes = Giphy.Download(((GifItem)pb.Tag).ThumbUrl);
                            var img = Image.FromStream(new MemoryStream(bytes)); // 움직이는 GIF는 스트림을 열어 둬야 함
                            pb.BeginInvoke((Action)(() => { if (!pb.IsDisposed) pb.Image = img; else img.Dispose(); }));
                        }
                        catch { }
                    }
                });
                th.IsBackground = true;
                th.Start();
            }
        }
    }
}
