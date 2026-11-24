using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace EmojiPicker
{
    // 전역 단축키를 받는 보이지 않는 창
    class HotkeyWindow : NativeWindow, IDisposable
    {
        public event Action Pressed;

        public HotkeyWindow() { CreateHandle(new CreateParams()); }

        public bool Register()
        {
            return Native.RegisterHotKey(Handle, 1, Native.MOD_CONTROL | Native.MOD_WIN | Native.MOD_NOREPEAT, Native.VK_OEM_PERIOD);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && Pressed != null) Pressed();
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            Native.UnregisterHotKey(Handle, 1);
            DestroyHandle();
        }
    }

    class Settings
    {
        static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EmojiPicker");
        static readonly string RecentFile = Path.Combine(Dir, "recent.txt");
        static readonly string ModeFile = Path.Combine(Dir, "paste-mode.txt");
        static readonly string NoSmartFile = Path.Combine(Dir, "no-smart.txt");

        // 문장 맞춤 추천 (기본: 켜짐)
        public static bool SmartRecommend
        {
            get { return !File.Exists(NoSmartFile); }
            set
            {
                try
                {
                    Directory.CreateDirectory(Dir);
                    if (!value) File.WriteAllText(NoSmartFile, "1"); else File.Delete(NoSmartFile);
                }
                catch { }
            }
        }

        static readonly string SizeFile = Path.Combine(Dir, "window-size.txt");

        // 사용자가 늘린 창 크기 기억 (기본보다 작게는 안 됨)
        public static Size LoadSize(Size min)
        {
            try
            {
                var p = File.ReadAllText(SizeFile).Trim().Split(',');
                var wa = Screen.PrimaryScreen.WorkingArea;
                return new Size(Math.Max(min.Width, Math.Min(wa.Width - 16, int.Parse(p[0]))), Math.Max(min.Height, Math.Min(wa.Height - 16, int.Parse(p[1]))));
            }
            catch { return min; }
        }

        public static void SaveSize(Size s)
        {
            try { Directory.CreateDirectory(Dir); File.WriteAllText(SizeFile, s.Width + "," + s.Height); } catch { }
        }

        public static List<string> LoadRecent()
        {
            try { return File.ReadAllLines(RecentFile, Encoding.UTF8).Where(l => l.Length > 0).ToList(); }
            catch { return new List<string>(); }
        }

        public static void SaveRecent(List<string> list)
        {
            try { Directory.CreateDirectory(Dir); File.WriteAllLines(RecentFile, list, Encoding.UTF8); } catch { }
        }

        public static bool PasteMode
        {
            get { return File.Exists(ModeFile); }
            set
            {
                try
                {
                    Directory.CreateDirectory(Dir);
                    if (value) File.WriteAllText(ModeFile, "1"); else File.Delete(ModeFile);
                }
                catch { }
            }
        }

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool AutoStart
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue("EmojiPicker") != null;
            }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value) k.SetValue("EmojiPicker", "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue("EmojiPicker", false);
                }
            }
        }
    }

    class App : ApplicationContext
    {
        const string HotkeyText = "Ctrl+Win+.";
        readonly NotifyIcon tray = new NotifyIcon();
        readonly HotkeyWindow hotkey = new HotkeyWindow();
        readonly PickerForm picker = new PickerForm();
        List<string> recent = Settings.LoadRecent();
        IntPtr target = IntPtr.Zero;
        bool copyOnly; // 트레이에서 열었을 때는 입력 대신 클립보드 복사

        public App()
        {
            picker.GetRecent = () => recent;
            picker.Picked = OnPicked;
            picker.PickedGif = OnPickedGif;
            picker.AutoRequested = OpenAuto;
            picker.PickedSticker = path => { if (replaceWith != null) { replaceWith = null; return; } PasteFile(() => Stickers.PutOnClipboard(path), "이모티콘"); };
            Giphy.CleanCache();
            picker.CreateControl();
            var h = picker.Handle; // 창 미리 만들어 두기 (첫 실행 지연 방지)

            tray.Icon = new Icon(typeof(App).Assembly.GetManifestResourceStream("app.ico"), 16, 16);
            tray.Text = "EmoGIF Picker (" + HotkeyText + ")";
            tray.Visible = true;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) OpenFromTray(); };

            var menu = new ContextMenuStrip { Font = AppFont.Make(9f) };
            menu.Items.Add("EmoGIF Picker 열기 (복사용)", null, (s, e) => OpenFromTray());
            menu.Items.Add(new ToolStripSeparator());
            var paste = new ToolStripMenuItem("붙여넣기 방식으로 입력 (입력이 안 되는 프로그램용)") { Checked = Settings.PasteMode, CheckOnClick = true };
            paste.CheckedChanged += (s, e) => Settings.PasteMode = paste.Checked;
            menu.Items.Add(paste);
            var smart = new ToolStripMenuItem("쓰고 있는 문장에 맞는 이모지 추천") { Checked = Settings.SmartRecommend, CheckOnClick = true };
            smart.CheckedChanged += (s, e) => Settings.SmartRecommend = smart.Checked;
            menu.Items.Add(smart);
            menu.Items.Add("AI 추천 설정 (선택)…", null, (s, e) => ShowAiSettings());
            var auto = new ToolStripMenuItem("Windows 시작 시 자동 실행") { CheckOnClick = true };
            try { auto.Checked = Settings.AutoStart; } catch { }
            auto.CheckedChanged += (s, e) => { try { Settings.AutoStart = auto.Checked; } catch { } };
            menu.Items.Add(auto);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("종료", null, (s, e) => ExitThread());
            tray.ContextMenuStrip = menu;

            hotkey.Pressed += OpenFromHotkey;
            if (!hotkey.Register())
            {
                tray.ShowBalloonTip(6000, "EmoGIF Picker",
                    HotkeyText + " 단축키를 다른 프로그램이 쓰고 있어요. 트레이 아이콘을 눌러 사용하세요.", ToolTipIcon.Warning);
            }
            else
            {
                tray.ShowBalloonTip(4000, "EmoGIF Picker 실행 중",
                    "입력창에서 " + HotkeyText + " 를 누르면 이모지를 고를 수 있어요.", ToolTipIcon.Info);
            }
        }

        void OpenFromHotkey()
        {
            replaceWith = null;
            target = Native.GetForegroundWindow();
            if (target == picker.Handle) { picker.Reactivate(); return; }
            copyOnly = false;
            var caret = Native.CaretPosition(target);
            string ctx = Settings.SmartRecommend ? Context.ReadBeforeCaret(target, 350) : null;
            picker.Open(caret ?? Cursor.Position, ctx);
            if (ctx != null && AiSettings.Enabled) AskAi(ctx, picker.OpenCount);
        }

        // AI 추천은 창을 먼저 띄운 뒤 뒤에서 받아 옴 (느려도 창이 늦게 뜨지 않게)
        void AskAi(string ctx, int openCount)
        {
            var th = new Thread(() =>
            {
                List<Emoji> list = null;
                try { list = AiClient.Suggest(ctx, 8000); } catch { }
                if (list == null || list.Count == 0) return;
                try { picker.BeginInvoke((Action)(() => picker.SetAiRecs(openCount, list))); } catch { }
            });
            th.IsBackground = true;
            th.Start();
        }

        void OpenFromTray()
        {
            replaceWith = null;
            target = IntPtr.Zero;
            copyOnly = true;
            var wa = Screen.PrimaryScreen.WorkingArea;
            picker.Open(new Point(wa.Right - picker.Width - 16, wa.Bottom - picker.Height - 16));
        }

        AutoForm autoForm;
        Action<string> replaceWith; // 자동 창에서 이모지를 바꾸려고 피커를 열었을 때

        // 긴 글 자동 꾸미기 창 (입력하던 창의 글을 가져와서 바로 꾸며 보여 줌)
        void OpenAuto(string fromBox)
        {
            picker.Hide();
            if (autoForm != null && !autoForm.IsDisposed) autoForm.Close();
            bool canPut = !copyOnly && target != IntPtr.Zero;
            // 피커 입력칸에 쓴 글이 있으면 그 글, 없으면 입력하던 창의 글
            string initial = fromBox ?? (canPut ? Context.LastFull : null);
            bool replace = fromBox == null; // 입력하던 창의 글을 가져왔을 때만 그 글을 통째로 바꿈
            if (initial != null) initial = initial.Replace("\n", "\r\n");
            var keep = target;
            autoForm = new AutoForm(initial, canPut, Theme.Current(), picker.DpiScale, AppFont.Make(9.5f));
            autoForm.Put = t =>
            {
                SetClipboard(t);
                Native.FocusWindow(keep);
                var tm = new System.Windows.Forms.Timer { Interval = 150 };
                tm.Tick += (s, e) => { tm.Stop(); tm.Dispose(); if (replace) Native.PressCtrlA(); Native.PressCtrlV(); };
                tm.Start();
            };
            autoForm.OpenPicker = (ctx, pt, done) =>
            {
                replaceWith = done;
                picker.Open(pt, ctx.Length > 0 ? ctx : null); picker.BringToFront();
            };
            autoForm.FormClosed += (s, e) => { autoForm = null; replaceWith = null; };
            autoForm.Show();
            autoForm.Activate();
        }

        AiSettingsForm aiForm;

        void ShowAiSettings()
        {
            if (aiForm != null && !aiForm.IsDisposed) { aiForm.Activate(); return; }
            aiForm = new AiSettingsForm();
            aiForm.FormClosed += (s, e) => aiForm = null;
            aiForm.Show();
            aiForm.Activate();
        }

        // GIF: 받아서 클립보드에 올린 뒤 붙여넣기 (GIF는 글자가 아니라서 입력창이 받아 줘야 들어감)
        void OnPickedGif(GifItem g)
        {
            if (replaceWith != null) { replaceWith = null; return; }
            var th = new Thread(() =>
            {
                string path = null;
                try { path = Giphy.DownloadFull(g); } catch { }
                try
                {
                    picker.BeginInvoke((Action)(() =>
                    {
                        if (path == null) { tray.ShowBalloonTip(3000, "GIF", "GIF를 받지 못했어요. 인터넷 연결을 확인해 주세요.", ToolTipIcon.Warning); return; }
                        PasteFile(() => Giphy.PutOnClipboard(path, g), "GIF");
                    }));
                }
                catch { }
            });
            th.IsBackground = true;
            th.Start();
        }

        // 그림을 클립보드에 올리고 입력하던 창에 붙여넣기
        void PasteFile(Action putOnClipboard, string what)
        {
            putOnClipboard();
            if (copyOnly || target == IntPtr.Zero)
            {
                tray.ShowBalloonTip(2500, what + " 복사됨", "보낼 곳에서 Ctrl+V로 붙여넣으세요.", ToolTipIcon.None);
                return;
            }
            Native.FocusWindow(target);
            var t = new System.Windows.Forms.Timer { Interval = 120 };
            t.Tick += (s, e) => { t.Stop(); t.Dispose(); Native.PressCtrlV(); };
            t.Start();
        }

        void OnPicked(string ch, bool keepOpen)
        {
            if (replaceWith != null) // 자동 창의 이모지 바꾸기
            {
                var done = replaceWith; replaceWith = null;
                picker.Hide();
                recent.Remove(ch); recent.Insert(0, ch); Settings.SaveRecent(recent);
                done(ch);
                return;
            }
            recent.Remove(ch);
            recent.Insert(0, ch);
            if (recent.Count > 24) recent.RemoveRange(24, recent.Count - 24);
            Settings.SaveRecent(recent);

            if (copyOnly || target == IntPtr.Zero)
            {
                string text = ch;
                if (keepOpen)
                {
                    try { if (Clipboard.ContainsText() && lastCopy != null && Clipboard.GetText() == lastCopy) text = lastCopy + ch; } catch { }
                }
                SetClipboard(text);
                lastCopy = text;
                tray.ShowBalloonTip(1500, "복사됨", text + "  — Ctrl+V로 붙여넣으세요", ToolTipIcon.None);
                return;
            }

            picker.KeepAlive = keepOpen;
            Native.FocusWindow(target);
            // 포커스가 넘어갈 시간을 조금 준 뒤 입력
            var t = new System.Windows.Forms.Timer { Interval = 60 };
            t.Tick += (s, e) =>
            {
                t.Stop();
                t.Dispose();
                Insert(ch);
                if (keepOpen)
                {
                    var t2 = new System.Windows.Forms.Timer { Interval = 80 };
                    t2.Tick += (s2, e2) => { t2.Stop(); t2.Dispose(); picker.Reactivate(); picker.KeepAlive = false; };
                    t2.Start();
                }
            };
            t.Start();
        }

        string lastCopy;

        void Insert(string ch)
        {
            if (!Settings.PasteMode && Native.TypeText(ch)) return;
            // 붙여넣기 방식: 클립보드 내용을 잠깐 바꿨다가 되돌림
            string old = null;
            try { if (Clipboard.ContainsText()) old = Clipboard.GetText(); } catch { }
            SetClipboard(ch);
            Native.PressCtrlV();
            if (old != null)
            {
                var t = new System.Windows.Forms.Timer { Interval = 400 };
                t.Tick += (s, e) => { t.Stop(); t.Dispose(); SetClipboard(old); };
                t.Start();
            }
        }

        static void SetClipboard(string text)
        {
            for (int i = 0; i < 5; i++)
            {
                try { Clipboard.SetText(text); return; }
                catch { Thread.Sleep(30); }
            }
        }

        protected override void ExitThreadCore()
        {
            tray.Visible = false;
            tray.Dispose();
            hotkey.Dispose();
            picker.Dispose();
            base.ExitThreadCore();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--search") // 검색 점검용
            {
                Data.Load();
                Console.OutputEncoding = Encoding.UTF8;
                foreach (var q in args.Skip(1))
                    Console.WriteLine(q + " → " + string.Concat(Search.Find(q).Take(16).Select(e => e.Ch)));
                return;
            }

            if (args.Length >= 2 && args[0] == "--recommend") // 추천 점검용
            {
                Data.Load();
                Console.OutputEncoding = Encoding.UTF8;
                foreach (var q in args.Skip(1))
                    Console.WriteLine(q + " → " + string.Concat(Recommend.For(q).Take(14).Select(e => e.Ch)));
                return;
            }

            if (args.Length >= 2 && args[0] == "--parse-gif") // GIF 답 해석 점검용
            {
                foreach (var g in Giphy.ParseForTest(File.ReadAllText(args[1]))) Console.WriteLine(g.Id + " " + g.Title + " " + g.ThumbW + "x" + g.ThumbH + " " + g.ThumbUrl + " | " + g.FullUrl);
                return;
            }

            if (args.Length >= 2 && args[0] == "--auto") // 자동 꾸미기 점검용
            {
                Data.Load();
                Console.OutputEncoding = Encoding.UTF8;
                foreach (var q in args.Skip(1)) Console.WriteLine(Recommend.AutoDecorate(q.Replace("\\n", "\n")) + "\n---");
                return;
            }

            bool created;
            using (var mutex = new Mutex(true, "EmojiPicker_SingleInstance_v1", out created))
            {
                if (!created)
                {
                    MessageBox.Show("EmoGIF Picker가 이미 실행 중이에요.\n입력창에서 Ctrl+Win+. 을 눌러 보세요.", "EmoGIF Picker");
                    return;
                }
                try { Native.SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AiSettings.Load();
                try { Data.Load(); }
                catch (Exception ex)
                {
                    MessageBox.Show("데이터를 불러오지 못했어요: " + ex.Message, "EmoGIF Picker");
                    return;
                }
                if (args.Length >= 1 && args[0] == "--ai-settings") { Application.Run(new AiSettingsForm()); return; } // 화면 점검용
                if (args.Length >= 1 && args[0] == "--show") // 화면 점검용
                {
                    var f = new PickerForm { GetRecent = () => new List<string> { "😂", "🥲", "❤️" } };
                    f.Picked = (c, k) => { };
                    if (args.Length > 2 && args[1] == "--context") f.Open(new Point(20, 20), args[2]);
                    else if (args.Length > 1 && args[1] == "--mine") { f.Open(new Point(20, 20)); f.ShowMine(); }
                    else if (args.Length > 2 && args[1] == "--for-send") { Console.WriteLine(Stickers.ForSend(args[2])); return; }
                    else if (args.Length > 2 && args[1] == "--add-dialog") { var d = new AddImageForm(args.Skip(2).ToArray(), Theme.Current(), 1f, AppFont.Make(9.5f)); Application.Run(d); foreach (var kv in d.Result) { Console.WriteLine(kv.Key + " " + kv.Value.Crop + " bg=" + kv.Value.RemoveBg); Stickers.Add(kv.Key, kv.Value); } return; }
                    else if (args.Length > 2 && args[0] == "--show" && args[1] == "--add-sticker") { foreach (var x in args.Skip(2)) Stickers.Add(x); return; }
                    else if (args.Length > 1 && args[1] == "--gif-key") { f.Open(new Point(20, 20)); f.ShowGifKeyHelp(); }
                    else if (args.Length > 2 && args[1] == "--auto-dialog") { var af = new AutoForm(args[2].Replace("\\n", "\r\n"), true, Theme.Current(), 1f, AppFont.Make(9.5f)); Action<string> rw = null; f.Picked = (c, k) => { f.Hide(); if (rw != null) { var d = rw; rw = null; d(c); } }; af.OpenPicker = (ctx, pt, done) => { rw = done; f.Open(pt, ctx.Length > 0 ? ctx : null); f.BringToFront(); }; Application.Run(af); return; }
                    else if (args.Length > 1 && args[1] == "--gif-badkey") { f.Open(new Point(20, 20)); f.ShowGifKeyHelp(true); }
                    else if (args.Length > 2 && args[1] == "--gif-samples")
                    {
                        f.Open(new Point(20, 20));
                        var imgs = System.IO.Directory.GetFiles(args[2], "*.gif").OrderBy(x => x).Select(x => (Image)Image.FromFile(x)).ToList();
                        f.ShowGifSamples(imgs, args.Length > 3 ? args[3] : "샘플");
                    }
                    else if (args.Length > 2 && args[1] == "--group") { f.Open(new Point(20, 20)); f.ShowGroup(int.Parse(args[2])); }
                    else
                    {
                        f.Open(new Point(20, 20));
                        if (args.Length > 1) f.SetQuery(args[1]);
                    }
                    Application.Run(f);
                    return;
                }
                Application.Run(new App());
            }
        }
    }
}
