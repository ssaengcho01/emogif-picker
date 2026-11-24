using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace EmojiPicker
{
    // ---------- AI 추천 설정 (선택 기능, 기본은 꺼짐) ----------
    // Google Gemini 무료 API 키만 지원 (Google AI Studio에서 발급, 결제 정보 등록 없이 무료 한도 안에서 사용)
    static class AiSettings
    {
        static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EmojiPicker");
        static readonly string FileName = Path.Combine(Dir, "ai.txt");

        public const string Off = "off", Gemini = "gemini", Ollama = "ollama";
        public const string DefaultGeminiModel = "gemini-flash-lite-latest", DefaultOllamaModel = "gemma3";

        public static string Provider = Off;
        public static string GeminiKey = "";
        public static string GeminiModel = DefaultGeminiModel;
        public static string OllamaModel = DefaultOllamaModel;

        public static bool Enabled
        {
            get { return Provider == Gemini && GeminiKey.Length > 0; }
        }

        public static void Load()
        {
            try
            {
                foreach (var line in File.ReadAllLines(FileName, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i < 0) continue;
                    string k = line.Substring(0, i), v = line.Substring(i + 1);
                    if (k == "provider") Provider = v;
                    else if (k == "gemini_model" && v.Length > 0) GeminiModel = v;
                    else if (k == "ollama_model" && v.Length > 0) OllamaModel = v;
                    else if (k == "gemini_key") GeminiKey = Unprotect(v);
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllLines(FileName, new[]
                {
                    "provider=" + Provider,
                    "gemini_model=" + GeminiModel,
                    "ollama_model=" + OllamaModel,
                    "gemini_key=" + Protect(GeminiKey),
                }, Encoding.UTF8);
            }
            catch { }
        }

        // 키는 Windows 계정으로 암호화해서 저장 (같은 PC·같은 사용자만 풀 수 있음)
        public static string Protect(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            try { return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s), null, DataProtectionScope.CurrentUser)); }
            catch { return ""; }
        }

        public static string Unprotect(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s), null, DataProtectionScope.CurrentUser)); }
            catch { return ""; }
        }
    }

    static class AiClient
    {
        static string Prompt(string text)
        {
            return "아래는 사용자가 메시지 입력창에 쓰고 있는 글이야. 이 글 바로 뒤에 붙이기 좋은 이모지 12개를 어울리는 순서대로 골라 줘. " +
                   "이모지만 공백으로 구분해서 한 줄로 답하고, 다른 말은 쓰지 마.\n글: " + text;
        }

        // 결과 이모지 목록. 실패하면 예외 (메시지는 사용자에게 보여 줄 수 있는 한국어)
        public static List<Emoji> Suggest(string text, int timeoutMs)
        {
            return Recommend.ParseEmojis(Ask(AiSettings.Provider, AiSettings.GeminiKey, AiSettings.GeminiModel, AiSettings.OllamaModel, Prompt(text), timeoutMs));
        }

        public static string Ask(string provider, string key, string geminiModel, string ollamaModel, string prompt, int timeoutMs)
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { } // TLS 1.2
            string body, url;
            var headers = new Dictionary<string, string>();
            if (provider == AiSettings.Gemini)
            {
                url = "https://generativelanguage.googleapis.com/v1beta/models/" + Uri.EscapeDataString(geminiModel) + ":generateContent";
                headers["x-goog-api-key"] = key;
                body = "{\"contents\":[{\"parts\":[{\"text\":" + Json(prompt) + "}]}]," +
                       "\"generationConfig\":{\"maxOutputTokens\":1024}}";
            }
            else if (provider == AiSettings.Ollama)
            {
                url = "http://localhost:11434/api/generate";
                body = "{\"model\":" + Json(ollamaModel) + ",\"prompt\":" + Json(prompt) + ",\"stream\":false}";
            }
            else throw new Exception("AI 추천이 꺼져 있어요.");

            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json; charset=utf-8";
            req.Timeout = req.ReadWriteTimeout = timeoutMs;
            foreach (var h in headers) req.Headers[h.Key] = h.Value;
            var bytes = Encoding.UTF8.GetBytes(body);
            try
            {
                using (var s = req.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    var json = r.ReadToEnd();
                    // 응답에서 AI가 쓴 글 부분만 꺼내기
                    var m = Regex.Match(json, provider == AiSettings.Gemini ? "\"text\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"" : "\"response\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                    if (!m.Success) throw new Exception("AI 답을 이해하지 못했어요.");
                    return Unjson(m.Groups[1].Value);
                }
            }
            catch (WebException ex)
            {
                var hr = ex.Response as HttpWebResponse;
                if (provider == AiSettings.Ollama && hr == null)
                    throw new Exception("Ollama에 연결하지 못했어요. Ollama가 실행 중인지 확인해 주세요.");
                if (hr != null)
                {
                    int code = (int)hr.StatusCode;
                    string err = "";
                    try { using (var er = new StreamReader(hr.GetResponseStream(), Encoding.UTF8)) err = er.ReadToEnd(); } catch { }
                    if (provider == AiSettings.Gemini && (code == 401 || code == 403 || (code == 400 && err.Contains("API_KEY"))))
                        throw new Exception("키가 맞지 않아요. 복사한 키를 다시 붙여넣어 주세요.");
                    if (code == 429) throw new Exception("무료 사용 한도를 잠시 넘었어요. 조금 뒤에 다시 써 주세요. (요금은 청구되지 않아요)");
                    if (code == 404) throw new Exception(provider == AiSettings.Ollama
                        ? "Ollama에 '" + ollamaModel + "' 모델이 없어요. 명령 창에서 ollama pull " + ollamaModel + " 을 실행해 주세요."
                        : "모델 이름 '" + geminiModel + "'을(를) 찾지 못했어요.");
                    throw new Exception("AI 서버 오류 (" + code + ")");
                }
                if (ex.Status == WebExceptionStatus.Timeout) throw new Exception("AI 응답이 너무 늦어요.");
                throw new Exception("인터넷 연결을 확인해 주세요.");
            }
        }

        static string Json(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s)
            {
                if (c == '"') sb.Append("\\\"");
                else if (c == '\\') sb.Append("\\\\");
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c);
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        static string Unjson(string s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '\\' || i + 1 >= s.Length) { sb.Append(c); continue; }
                char n = s[++i];
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 < s.Length) { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                        break;
                    default: sb.Append(n); break;
                }
            }
            return sb.ToString();
        }
    }

    // ---------- 트레이 메뉴 → "AI 추천 설정" 창 ----------
    class AiSettingsForm : Form
    {
        readonly RadioButton rOff, rGemini;
        readonly TextBox key;
        readonly Panel geminiPanel;
        readonly Label status;

        public AiSettingsForm()
        {
            float S;
            using (var g = CreateGraphics()) S = g.DpiX / 96f;
            Func<int, int> D = v => (int)(v * S);
            Text = "AI 추천 설정 (선택)";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            Font = AppFont.Make(9.5f);
            ClientSize = new Size(D(440), D(414));
            BackColor = Color.White;
            TopMost = true;
            try { Icon = new Icon(typeof(AiSettingsForm).Assembly.GetManifestResourceStream("app.ico")); } catch { }

            int y = D(14);
            var intro = new Label
            {
                Text = "AI를 켜지 않아도 내장 사전으로 문장에 맞는 이모지를 추천해요.\nAI를 켜면 문장을 더 잘 이해해요. 무료로 쓸 수 있어요.",
                Location = new Point(D(14), y), Size = new Size(D(412), D(60)), ForeColor = Color.FromArgb(70, 70, 70),
            };
            Controls.Add(intro);
            y += D(68);

            rOff = new RadioButton { Text = "사용 안 함 (기본)", Location = new Point(D(14), y), AutoSize = true };
            rGemini = new RadioButton { Text = "Google Gemini (무료 키)", Location = new Point(D(14), y + D(26)), AutoSize = true };
            Controls.AddRange(new Control[] { rOff, rGemini });
            y += D(58);

            // Gemini: 1) 키 발급 페이지 열기 2) 붙여넣기
            geminiPanel = new Panel { Location = new Point(D(14), y), Size = new Size(D(412), D(190)) };
            var step1 = new Label { Text = "① 버튼을 눌러 Google 계정으로 로그인한 뒤\n    [API 키 만들기]를 누르세요.", Location = new Point(0, 0), Size = new Size(D(412), D(38)) };
            var open = new Button { Text = "키 발급 페이지 열기", Location = new Point(0, D(40)), Size = new Size(D(160), D(28)) };
            open.Click += (s, e) => { try { Process.Start("https://aistudio.google.com/app/apikey"); } catch { } };
            var step2 = new Label { Text = "② 만든 키를 복사해서 여기에 붙여넣으세요.", Location = new Point(0, D(78)), Size = new Size(D(412), D(20)) };
            key = new TextBox { Location = new Point(0, D(100)), Size = new Size(D(330), D(24)), UseSystemPasswordChar = true, Text = AiSettings.GeminiKey };
            var paste = new Button { Text = "붙여넣기", Location = new Point(D(336), D(99)), Size = new Size(D(76), D(26)) };
            paste.Click += (s, e) => { try { if (Clipboard.ContainsText()) key.Text = Clipboard.GetText().Trim(); } catch { } };
            var note = new Label
            {
                Text = "결제 정보를 등록하지 않으면 요금이 나가지 않아요.\n무료 한도를 넘으면 잠시 멈출 뿐이에요.\n무료로 쓰면 보낸 글이 Google 서비스 개선에 쓰일 수 있어요.",
                Location = new Point(0, D(132)), Size = new Size(D(412), D(56)), ForeColor = Color.FromArgb(110, 110, 110),
                Font = new Font(Font.FontFamily, 8.5f),
            };
            geminiPanel.Controls.AddRange(new Control[] { step1, open, step2, key, paste, note });
            Controls.Add(geminiPanel);


            status = new Label { Location = new Point(D(14), D(334)), Size = new Size(D(412), D(34)), ForeColor = Color.FromArgb(60, 60, 60) };
            Controls.Add(status);

            var test = new Button { Text = "연결 확인", Location = new Point(D(14), D(374)), Size = new Size(D(90), D(28)) };
            var save = new Button { Text = "저장", Location = new Point(D(254), D(374)), Size = new Size(D(84), D(28)) };
            var cancel = new Button { Text = "취소", Location = new Point(D(342), D(374)), Size = new Size(D(84), D(28)), DialogResult = DialogResult.Cancel };
            Controls.AddRange(new Control[] { test, save, cancel });
            AcceptButton = save;
            CancelButton = cancel;

            rOff.CheckedChanged += (s, e) => Sync();
            rGemini.CheckedChanged += (s, e) => Sync();
            if (AiSettings.Provider == AiSettings.Gemini) rGemini.Checked = true;
            else rOff.Checked = true;
            Sync();

            test.Click += (s, e) =>
            {
                if (rOff.Checked) { status.Text = "AI 추천이 꺼져 있어요. 내장 사전으로 추천해요."; return; }
                if (rGemini.Checked && key.Text.Trim().Length == 0) { status.Text = "키를 먼저 붙여넣어 주세요."; return; }
                status.Text = "확인 중…";
                status.Refresh();
                Cursor = Cursors.WaitCursor;
                try
                {
                    var ans = AiClient.Ask(Provider, key.Text.Trim(), AiSettings.GeminiModel, AiSettings.OllamaModel,
                        "\"이번에 강의 오픈했어요!\" 뒤에 붙일 이모지 5개만 공백으로 구분해서 답해.", 20000);
                    var list = Recommend.ParseEmojis(ans);
                    status.Text = list.Count > 0 ? "연결됐어요! 예시 추천: " + string.Concat(list.Take(6).Select(x => x.Ch)) : "연결은 됐지만 이모지를 받지 못했어요: " + ans;
                }
                catch (Exception ex) { status.Text = ex.Message; }
                finally { Cursor = Cursors.Default; }
            };
            save.Click += (s, e) =>
            {
                AiSettings.Provider = Provider;
                AiSettings.GeminiKey = key.Text.Trim();
                AiSettings.Save();
                DialogResult = DialogResult.OK;
                Close();
            };
        }

        string Provider { get { return rGemini.Checked ? AiSettings.Gemini : AiSettings.Off; } }

        void Sync()
        {
            geminiPanel.Visible = rGemini.Checked;
            status.Text = "";
        }

        static string FontExistsOr(string name)
        {
            using (var f = new Font(name, 10f)) return f.Name == name ? name : SystemFonts.MessageBoxFont.Name;
        }
    }
}
