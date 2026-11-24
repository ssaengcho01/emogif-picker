using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EmojiPicker
{
    // ---------- 내 이모티콘: 내가 등록한 사진·그림 ----------
    // 등록하면 작게 줄이고, 단색 배경이면 투명하게 바꿔서 %APPDATA%\EmojiPicker\stickers 에 저장
    static class Stickers
    {
        public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EmojiPicker", "stickers");
        public const int MaxSize = 720; // 카톡에서 눌러 크게 봐도 깨지지 않는 크기

        public static List<string> All()
        {
            try
            {
                if (!Directory.Exists(Dir)) return new List<string>();
                return Directory.GetFiles(Dir).Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => File.GetCreationTimeUtc(f)).ToList();
            }
            catch { return new List<string>(); }
        }

        static bool IsPic(string f) { return f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase); }

        // 세트별로 묶기: stickers 바로 안의 파일 = "내 이모티콘"(최근 등록순), 하위 폴더 하나 = 세트 하나(파일 이름순)
        // 프로그램에 들어 있는 기본 세트를 처음 한 번만 MY에 풀어 놓음 (지운 세트는 다시 안 생김)
        static bool builtinDone;
        public static void InstallBuiltIn()
        {
            if (builtinDone) return;
            builtinDone = true;
            try
            {
                var asm = typeof(Stickers).Assembly;
                string list;
                using (var s = asm.GetManifestResourceStream("builtin.txt"))
                {
                    if (s == null) return;
                    using (var r = new StreamReader(s, Encoding.UTF8)) list = r.ReadToEnd();
                }
                var mark = Path.Combine(Path.GetDirectoryName(Dir), "builtin-installed.txt");
                var done = new HashSet<string>(File.Exists(mark) ? File.ReadAllLines(mark, Encoding.UTF8) : new string[0]);
                var added = new List<string>();
                foreach (var line in list.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = line.TrimEnd('\r').Split('\t');
                    if (p.Length < 3 || done.Contains(p[0])) continue;
                    var dir = SetDir(p[0]);
                    Directory.CreateDirectory(dir);
                    var dst = Path.Combine(dir, p[1]);
                    if (!File.Exists(dst))
                        using (var s = asm.GetManifestResourceStream(p[2]))
                        using (var o = File.Create(dst)) s.CopyTo(o);
                    if (!added.Contains(p[0])) added.Add(p[0]);
                }
                if (added.Count > 0) File.AppendAllLines(mark, added, new UTF8Encoding(false));
            }
            catch { }
        }

        public static List<KeyValuePair<string, List<string>>> Sets()
        {
            InstallBuiltIn();
            var sets = new List<KeyValuePair<string, List<string>>>();
            sets.Add(new KeyValuePair<string, List<string>>("내 이모티콘", All()));
            try
            {
                if (Directory.Exists(Dir))
                    foreach (var d in Directory.GetDirectories(Dir).OrderBy(x => Path.GetFileName(x), StringComparer.CurrentCulture))
                    {
                        var files = Directory.GetFiles(d).Where(IsPic).OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal).ToList();
                        if (files.Count > 0) sets.Add(new KeyValuePair<string, List<string>>(Path.GetFileName(d), files));
                    }
            }
            catch { }
            return sets;
        }

        // 파일 이름에서 "01_" 같은 번호를 뗀 이름 (마우스를 올리면 보여 줌)
        public static string Title(string path)
        {
            var n = Path.GetFileNameWithoutExtension(path);
            int i = 0; while (i < n.Length && char.IsDigit(n[i])) i++;
            if (i > 0 && i < n.Length && (n[i] == '_' || n[i] == '-' || n[i] == ' ')) n = n.Substring(i + 1);
            return n.Replace('_', ' ');
        }

        // 넣을 때 고른 설정: 자를 부분(0~1 비율)과 배경 지우기
        public class AddOpt
        {
            public RectangleF Crop = new RectangleF(0, 0, 1, 1);
            public bool RemoveBg = false;
        }
        public const int PhotoMax = 720; // 배경을 안 지우면 사진으로 보낼 수 있게 크게 저장

        // 사진·그림 등록. 움직이는 GIF는 그대로, 나머지는 자르고 줄여서 PNG로
        public static string Add(string src, AddOpt o = null)
        {
            Directory.CreateDirectory(Dir);
            return Save(src, Path.Combine(Dir, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")), o);
        }

        public static bool IsAnimated(Image img)
        {
            try { return img.FrameDimensionsList.Length > 0 && img.GetFrameCount(new FrameDimension(img.FrameDimensionsList[0])) > 1; } catch { return false; }
        }

        // 자른 그림 (비율 좌표). 전체면 그대로 복사
        public static Bitmap CropTo(Image img, RectangleF c)
        {
            int x = (int)Math.Round(c.X * img.Width), y = (int)Math.Round(c.Y * img.Height);
            int w = Math.Max(1, Math.Min(img.Width - x, (int)Math.Round(c.Width * img.Width)));
            int h = Math.Max(1, Math.Min(img.Height - y, (int)Math.Round(c.Height * img.Height)));
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) g.DrawImage(img, new Rectangle(0, 0, w, h), new Rectangle(x, y, w, h), GraphicsUnit.Pixel);
            return bmp;
        }

        // 그림 하나를 이모티콘으로 저장 (확장자는 여기서 붙임)
        static string Save(string src, string dstNoExt, AddOpt o)
        {
            if (o == null) o = new AddOpt();
            using (var img = Image.FromStream(new MemoryStream(File.ReadAllBytes(src))))
            {
                if (IsAnimated(img))
                {
                    var dst = dstNoExt + ".gif";
                    File.Copy(src, dst);
                    return dst;
                }
                using (var cut = CropTo(img, o.Crop))
                using (var small = Resize(cut, o.RemoveBg || IsTransparent(cut) ? MaxSize : PhotoMax))
                {
                    if (o.RemoveBg) RemoveBackground(small);
                    var dst = dstNoExt + ".png";
                    small.Save(dst, ImageFormat.Png);
                    return dst;
                }
            }
        }

        // ---------- 세트 (stickers 안의 폴더 하나 = 세트 하나) ----------
        static string SafeName(string name)
        {
            var bad = Path.GetInvalidFileNameChars();
            var s = new string((name ?? "").Trim().Select(c => bad.Contains(c) ? ' ' : c).ToArray()).Trim().TrimEnd('.');
            return s.Length == 0 ? "새 세트" : (s.Length > 40 ? s.Substring(0, 40) : s);
        }

        public static string SetDir(string name) { return Path.Combine(Dir, name); }

        // 새 세트 만들기. 같은 이름이 있으면 "이름 (2)"
        public static int AddSet(string name, IEnumerable<KeyValuePair<string, AddOpt>> files)
        {
            var baseName = SafeName(name);
            var n = baseName;
            for (int k = 2; Directory.Exists(SetDir(n)); k++) n = baseName + " (" + k + ")";
            Directory.CreateDirectory(SetDir(n));
            return AddToSet(n, files);
        }

        // 세트에 그림 더 넣기: 파일 이름순으로, 이미 있는 번호 다음부터 01_이름 형태로 저장
        public static int AddToSet(string setName, IEnumerable<KeyValuePair<string, AddOpt>> files)
        {
            var dir = SetDir(setName);
            Directory.CreateDirectory(dir);
            int next = 1;
            foreach (var f in Directory.GetFiles(dir))
            {
                int num; var head = Path.GetFileName(f).Split('_')[0];
                if (int.TryParse(head, out num) && num >= next) next = num + 1;
            }
            int ok = 0;
            foreach (var kv in files)
            {
                var f = kv.Key;
                try
                {
                    var title = SafeName(Path.GetFileNameWithoutExtension(f)).Replace('_', ' ');
                    Save(f, Path.Combine(dir, next.ToString("00") + "_" + title), kv.Value);
                    next++; ok++;
                }
                catch { }
            }
            return ok;
        }

        public static bool RenameSet(string oldName, string newName)
        {
            try
            {
                var n = SafeName(newName);
                if (n == oldName) return true;
                if (Directory.Exists(SetDir(n))) return false;
                Directory.Move(SetDir(oldName), SetDir(n));
                return true;
            }
            catch { return false; }
        }

        public static void DeleteSet(string name)
        {
            try { Directory.Delete(SetDir(name), true); } catch { }
        }

        // 카톡으로 보낼 파일: 카톡은 PNG 사진의 투명한 곳을 흰색으로 바꾸지만 GIF는 그대로 둠
        // 그래서 투명한 곳이 있는 PNG는 투명 GIF로 바꿔서 보냄
        public static bool PngAsGif = false;

        // 투명한 곳이 있는 PNG를 흰 바탕으로 채운 복사본 (없으면 원본 그대로)
        public const int SendMax = 240; // 카톡으로 보낼 때 긴 쪽 크기
        static string WhitePng(string path)
        {
            try
            {
                var dir = Path.Combine(Path.GetTempPath(), "EmojiPicker-send");
                Directory.CreateDirectory(dir);
                var rel = path.StartsWith(Dir, StringComparison.OrdinalIgnoreCase) ? path.Substring(Dir.Length) : path;
                var name = Path.GetFileName(path);
                var safe = new string(Path.GetDirectoryName(rel).Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
                var sub = Path.Combine(dir, safe.Length > 0 ? safe : "my");
                Directory.CreateDirectory(sub);
                var dst = Path.Combine(sub, name); // 받는 쪽에 보이는 파일 이름은 원래 이름 그대로
                if (File.Exists(dst) && File.GetLastWriteTimeUtc(dst) >= File.GetLastWriteTimeUtc(path)) return dst;
                using (var src = new Bitmap(new MemoryStream(File.ReadAllBytes(path))))
                {
                    // 카톡 채팅창에서 너무 크게 보이지 않게 긴 쪽을 SendMax로 줄이고, 투명한 곳은 흰색으로
                    float k = Math.Min(1f, (float)SendMax / Math.Max(src.Width, src.Height));
                    int w = Math.Max(1, (int)Math.Round(src.Width * k)), h = Math.Max(1, (int)Math.Round(src.Height * k));
                    using (var flat = new Bitmap(w, h, PixelFormat.Format24bppRgb))
                    {
                        using (var g = Graphics.FromImage(flat))
                        {
                            g.Clear(Color.White);
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                            g.DrawImage(src, 0, 0, w, h);
                        }
                        flat.Save(dst, ImageFormat.Png);
                    }
                }
                return dst;
            }
            catch { return path; }
        }
        public static string ForSend(string path)
        {
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return path;
            if (!PngAsGif) return WhitePng(path); // 투명한 곳은 흰색으로 채운 PNG로 보냄 (2026-11-24 사용자 결정: 카톡에서 눌렀을 때도 흰 바탕)
            try
            {
                var dir = Path.Combine(Path.GetTempPath(), "EmojiPicker-gif");
                Directory.CreateDirectory(dir);
                var rel = path.StartsWith(Dir, StringComparison.OrdinalIgnoreCase) ? path.Substring(Dir.Length) : path;
                var safe = new string(rel.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
                var dst = Path.Combine(dir, "my-" + safe + ".gif"); // 세트마다 같은 파일 이름이 있어도 겹치지 않게
                if (File.Exists(dst) && File.GetLastWriteTimeUtc(dst) >= File.GetLastWriteTimeUtc(path)) return dst;
                using (var src = new Bitmap(new MemoryStream(File.ReadAllBytes(path))))
                {
                    var gif = ToTransparentGif(src);
                    if (gif == null) return path;
                    File.WriteAllBytes(dst, gif);
                }
                return dst;
            }
            catch { return path; }
        }

        // GIF는 반투명을 못 담아서, 테두리의 반투명 부분은 이 색과 섞음 (카톡 기본 채팅방 배경색이라 테두리가 티 나지 않음)
        public static Color Matte = Color.FromArgb(0xBA, 0xCE, 0xE0);

        // 256색 + 투명색 1개로 줄이기. 투명한 곳이 없으면 null
        public static byte[] ToTransparentGif(Bitmap src)
        {
            int w = src.Width, h = src.Height;
            var px = new int[w * h];
            using (var b = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(b)) { g.Clear(Color.Transparent); g.DrawImage(src, 0, 0, w, h); }
                var bd = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, px, 0, px.Length);
                b.UnlockBits(bd);
            }
            if (!px.Any(c => ((c >> 24) & 255) < 128)) return null;
            // 반쯤 투명한 테두리는 흰색과 섞어서 불투명하게
            for (int i = 0; i < px.Length; i++)
            {
                int c = px[i], a = (c >> 24) & 255;
                if (a < 128) { px[i] = 0; continue; }
                int r = (((c >> 16) & 255) * a + Matte.R * (255 - a)) / 255, gg = (((c >> 8) & 255) * a + Matte.G * (255 - a)) / 255, bb = ((c & 255) * a + Matte.B * (255 - a)) / 255;
                px[i] = unchecked((int)0xFF000000) | (r << 16) | (gg << 8) | bb;
            }
            // 많이 쓰인 색 255개 고르기 (색을 32단계로 묶어서 셈)
            var count = new Dictionary<int, int>();
            var sum = new Dictionary<int, long[]>();
            foreach (var c in px)
            {
                if (c == 0) continue;
                int r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
                int k = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
                int n; count.TryGetValue(k, out n); count[k] = n + 1;
                long[] acc; if (!sum.TryGetValue(k, out acc)) sum[k] = acc = new long[3];
                acc[0] += r; acc[1] += g; acc[2] += b;
            }
            var top = count.OrderByDescending(kv => kv.Value).Take(255).Select(kv => kv.Key).ToList();
            var pal = top.Select(k => { var a = sum[k]; int n = count[k]; return Color.FromArgb(255, (int)(a[0] / n), (int)(a[1] / n), (int)(a[2] / n)); }).ToList();
            var map = new Dictionary<int, byte>();
            var idxs = new byte[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                int c = px[i];
                if (c == 0) continue;
                int r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
                int k = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
                byte idx;
                if (!map.TryGetValue(k, out idx))
                {
                    int best = 0, bd2 = int.MaxValue;
                    for (int j = 0; j < pal.Count; j++)
                    {
                        int dr = pal[j].R - r, dg = pal[j].G - g, db = pal[j].B - b, d2 = dr * dr + dg * dg + db * db;
                        if (d2 < bd2) { bd2 = d2; best = j; }
                    }
                    idx = (byte)(best + 1);
                    map[k] = idx;
                }
                idxs[i] = idx;
            }
            return GifBytes(w, h, pal, idxs);
        }

        // 아주 작은 GIF 만들기 (256색 팔레트, 0번 = 투명)
        static byte[] GifBytes(int w, int h, List<Color> pal, byte[] idxs)
        {
            var ms = new MemoryStream();
            var o = new BinaryWriter(ms);
            o.Write(Encoding.ASCII.GetBytes("GIF89a"));
            o.Write((short)w); o.Write((short)h);
            o.Write((byte)0xF7); o.Write((byte)0); o.Write((byte)0); // 전역 팔레트 256색
            for (int i = 0; i < 256; i++)
            {
                if (i == 0) { o.Write((byte)255); o.Write((byte)255); o.Write((byte)255); }
                else if (i - 1 < pal.Count) { o.Write(pal[i - 1].R); o.Write(pal[i - 1].G); o.Write(pal[i - 1].B); }
                else { o.Write((byte)0); o.Write((byte)0); o.Write((byte)0); }
            }
            o.Write(new byte[] { 0x21, 0xF9, 4, 0x01, 0, 0, 0, 0 }); // 투명색 = 0번
            o.Write((byte)0x2C); o.Write((short)0); o.Write((short)0); o.Write((short)w); o.Write((short)h); o.Write((byte)0);
            o.Write((byte)8);
            var lzw = Lzw(idxs);
            for (int p = 0; p < lzw.Length; p += 255)
            {
                int n = Math.Min(255, lzw.Length - p);
                o.Write((byte)n); o.Write(lzw, p, n);
            }
            o.Write((byte)0); o.Write((byte)0x3B);
            o.Flush();
            return ms.ToArray();
        }

        static byte[] Lzw(byte[] data)
        {
            const int clear = 256, eoi = 257;
            var outp = new List<byte>();
            int bitBuf = 0, bitCnt = 0, size = 9, next = 258;
            Action<int> emit = code =>
            {
                bitBuf |= code << bitCnt; bitCnt += size;
                while (bitCnt >= 8) { outp.Add((byte)(bitBuf & 255)); bitBuf >>= 8; bitCnt -= 8; }
            };
            var dict = new Dictionary<int, int>();
            emit(clear);
            int cur = data.Length > 0 ? data[0] : 0;
            for (int i = 1; i < data.Length; i++)
            {
                int k = (cur << 8) | data[i], v;
                if (dict.TryGetValue(k, out v)) { cur = v; continue; }
                emit(cur);
                if (next < 4096)
                {
                    dict[k] = next++;
                    if (next > (1 << size) && size < 12) size++;
                }
                else { emit(clear); dict.Clear(); size = 9; next = 258; }
                cur = data[i];
            }
            emit(cur);
            emit(eoi);
            if (bitCnt > 0) outp.Add((byte)(bitBuf & 255));
            return outp.ToArray();
        }

        public static Bitmap Resize(Image img, int max = MaxSize)
        {
            float k = Math.Min(1f, (float)max / Math.Max(img.Width, img.Height));
            int w = Math.Max(1, (int)Math.Round(img.Width * k)), h = Math.Max(1, (int)Math.Round(img.Height * k));
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                // 가장자리가 반투명해지지 않도록 (GDI+ 확대·축소 특성)
                using (var ia = new ImageAttributes())
                {
                    ia.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                    g.DrawImage(img, new Rectangle(0, 0, w, h), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
                }
            }
            return bmp;
        }

        // 네 모서리가 같은 색(흰 배경 등)이면 가장자리에서부터 그 색을 지워 투명하게
        // 사진처럼 배경이 복잡하면 그대로 둠
        // 배경 지우기: 그림 둘레에서 가장 많은 색을 배경으로 보고, 이웃끼리 비슷하게 이어지는 곳(그라데이션·살짝 얼룩진 배경 포함)을 지움.
        // 테두리 2칸은 옆 배경색이 섞인 만큼 반투명하게 + 배경색을 빼냄. 지울 배경이 없으면 false (그림은 그대로)
        public static bool RemoveBackground(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            if (w < 3 || h < 3) return false;
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var px = new int[w * h];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, px, 0, px.Length);
            bool changed = false;
            try
            {
                var orig = (int[])px.Clone();
                var border = new List<int>();
                for (int x = 0; x < w; x++) { border.Add(x); border.Add((h - 1) * w + x); }
                for (int y = 1; y < h - 1; y++) { border.Add(y * w); border.Add(y * w + w - 1); }
                if (border.Count(i => ((px[i] >> 24) & 255) < 250) > border.Count / 10) return false; // 이미 투명한 그림
                // 둘레에서 가장 많은 색
                var hist = new Dictionary<int, int>();
                foreach (int i in border)
                {
                    int c = px[i], key = (((c >> 20) & 15) << 8) | (((c >> 12) & 15) << 4) | ((c >> 4) & 15);
                    int n; hist.TryGetValue(key, out n); hist[key] = n + 1;
                }
                int top = hist.OrderByDescending(kv => kv.Value).First().Key;
                long sr = 0, sg = 0, sb = 0; int cnt = 0;
                foreach (int i in border)
                {
                    int c = px[i];
                    if (((((c >> 20) & 15) << 8) | (((c >> 12) & 15) << 4) | ((c >> 4) & 15)) != top) continue;
                    sr += (c >> 16) & 255; sg += (c >> 8) & 255; sb += c & 255; cnt++;
                }
                int bg = (int)(0xFF000000u | (uint)((sr / cnt) << 16) | (uint)((sg / cnt) << 8) | (uint)(sb / cnt));
                var seen = new bool[w * h];
                var q = new Queue<int>();
                int seeds = 0;
                foreach (int i in border) if (!seen[i] && Dist(px[i], bg) <= 48) { seen[i] = true; q.Enqueue(i); seeds++; }
                if (seeds < border.Count / 2) return false; // 둘레가 복잡한 사진
                int removed = 0;
                Action<int, int> push = (from, j) =>
                {
                    if (seen[j]) return;
                    if (Dist(orig[j], orig[from]) <= 14 && Dist(orig[j], bg) <= 110) { seen[j] = true; q.Enqueue(j); }
                };
                while (q.Count > 0)
                {
                    int i = q.Dequeue(), x = i % w, y = i / w;
                    removed++;
                    if (x > 0) push(i, i - 1);
                    if (x < w - 1) push(i, i + 1);
                    if (y > 0) push(i, i - w);
                    if (y < h - 1) push(i, i + w);
                }
                if (removed > px.Length * 97 / 100) return false; // 그림까지 다 지워지면 하지 않음
                for (int i = 0; i < px.Length; i++) if (seen[i]) px[i] = 0;
                changed = true;
                // 테두리: 가장 가까운 지운 칸의 색을 그 자리 배경색으로 보고 섞인 만큼 투명하게
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (seen[i]) continue;
                        int local = 0, best = int.MaxValue;
                        for (int dy = -2; dy <= 2; dy++)
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= w || ny >= h || !seen[ny * w + nx]) continue;
                                int d2 = dx * dx + dy * dy;
                                if (d2 < best) { best = d2; local = orig[ny * w + nx]; }
                            }
                        if (best == int.MaxValue) continue;
                        int c = px[i];
                        int r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
                        int br = (local >> 16) & 255, bgG = (local >> 8) & 255, bb = local & 255;
                        double a = Math.Max(Mix(r, br), Math.Max(Mix(g, bgG), Mix(b, bb)));
                        a = Math.Min(1, a * 1.15);
                        if (a < 0.04) { px[i] = 0; continue; }
                        int nr = Clamp((r - br * (1 - a)) / a), ng = Clamp((g - bgG * (1 - a)) / a), nb = Clamp((b - bb * (1 - a)) / a);
                        px[i] = ((int)Math.Round(a * 255) << 24) | (nr << 16) | (ng << 8) | nb;
                    }
                return true;
            }
            finally
            {
                if (changed) System.Runtime.InteropServices.Marshal.Copy(px, 0, data.Scan0, px.Length);
                bmp.UnlockBits(data);
            }
        }

        // 배경 지우기를 할 수 있는 그림인지 (작게 줄여서 미리 해 봄)
        public static bool CanRemoveBg(Image img)
        {
            try { using (var b = Resize(img, 160)) return RemoveBackground(b); } catch { return false; }
        }

        // 이미 배경이 투명한 그림인지 (둘레의 10% 넘게 투명)
        public static bool IsTransparent(Image img)
        {
            try
            {
                using (var b = Resize(img, 160))
                {
                    int t = 0, n = 0;
                    for (int x = 0; x < b.Width; x++) { n += 2; if (b.GetPixel(x, 0).A < 250) t++; if (b.GetPixel(x, b.Height - 1).A < 250) t++; }
                    for (int y = 0; y < b.Height; y++) { n += 2; if (b.GetPixel(0, y).A < 250) t++; if (b.GetPixel(b.Width - 1, y).A < 250) t++; }
                    return t > n / 10;
                }
            }
            catch { return false; }
        }

        // 이 색이 배경색에서 얼마나 멀리 있는지 (0 = 배경색 그대로, 1 = 완전히 다른 색)
        static double Mix(int c, int bgc) { return c < bgc ? (bgc - c) / (double)Math.Max(1, bgc) : (c - bgc) / (double)Math.Max(1, 255 - bgc); }
        static int Clamp(double v) { return v < 0 ? 0 : v > 255 ? 255 : (int)Math.Round(v); }

        static int Dist(int a, int b)
        {
            int dr = ((a >> 16) & 255) - ((b >> 16) & 255), dg = ((a >> 8) & 255) - ((b >> 8) & 255), db = (a & 255) - (b & 255);
            return Math.Max(Math.Abs(dr), Math.Max(Math.Abs(dg), Math.Abs(db)));
        }

        public static void Delete(string path)
        {
            try { File.Delete(path); } catch { }
        }

        // 클립보드에 "그림"으로 올림. 카카오톡은 파일 붙여넣기를 받지 않고 그림(비트맵)만 받음
        //  - 비트맵: 카톡·그림판 등 거의 모든 프로그램 (투명한 곳은 흰색으로)
        //  - PNG: 투명 배경을 그대로 받는 프로그램용
        // 투명도가 들어 있는 비트맵(BITMAPV5HEADER, 32비트, 아래→위). 투명한 곳의 색은 흰색으로 둬서 투명도를 못 읽는 곳에서도 흰 배경
        static MemoryStream DibV5(Image src)
        {
            int w = src.Width, h = src.Height;
            var px = new int[w * h];
            using (var b = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(b)) { g.Clear(Color.Transparent); g.DrawImage(src, 0, 0, w, h); }
                var data = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, px, 0, px.Length);
                b.UnlockBits(data);
            }
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            bw.Write(124); bw.Write(w); bw.Write(h); bw.Write((short)1); bw.Write((short)32);
            bw.Write(3); bw.Write(w * h * 4); bw.Write(2835); bw.Write(2835); bw.Write(0); bw.Write(0);
            bw.Write(0x00FF0000); bw.Write(0x0000FF00); bw.Write(0x000000FF); unchecked { bw.Write((int)0xFF000000); }
            bw.Write(0x73524742); // 'sRGB'
            for (int i = 0; i < 9; i++) bw.Write(0); // 끝점
            bw.Write(0); bw.Write(0); bw.Write(0); // 감마
            bw.Write(4); bw.Write(0); bw.Write(0); bw.Write(0); // intent, profile, reserved
            for (int y = h - 1; y >= 0; y--)
                for (int x = 0; x < w; x++)
                {
                    int c = px[y * w + x];
                    if (((c >> 24) & 255) == 0) c = 0x00FFFFFF;
                    bw.Write(c);
                }
            bw.Flush();
            ms.Position = 0;
            return ms;
        }

        public static void PutOnClipboard(string path) { PutImageOnClipboard(path); }

        public static void PutImageOnClipboard(string path)
        {
            path = ForSend(path); // 클릭으로 넣을 때도 흰 바탕
            var d = new DataObject();
            Bitmap flat = null;
            try
            {
                using (var src = Image.FromStream(new MemoryStream(File.ReadAllBytes(path)))) // GIF는 첫 장면
                {
                    flat = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
                    using (var g = Graphics.FromImage(flat))
                    {
                        g.Clear(Color.White);
                        g.DrawImage(src, 0, 0, src.Width, src.Height);
                    }
                    d.SetData(DataFormats.Bitmap, true, flat);
                    try { d.SetData(DataFormats.GetFormat(17).Name, false, DibV5(src)); } catch { } // CF_DIBV5: 투명 배경을 읽는 프로그램용
                    using (var ms = new MemoryStream())
                    {
                        src.Save(ms, ImageFormat.Png);
                        d.SetData("PNG", false, new MemoryStream(ms.ToArray()));
                    }
                }
            }
            catch { }
            for (int i = 0; i < 5; i++)
            {
                try { Clipboard.SetDataObject(d, true); return; }
                catch { Thread.Sleep(40); }
            }
        }
    }

    // ---------- 칸을 다른 창(카톡 등)으로 끌어다 놓기: 파일로 보냄 → 움직이는 GIF·투명 배경 유지 ----------
    static class Drag
    {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> dragged = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();

        public static bool JustDragged(Control c)
        {
            object o;
            return dragged.TryGetValue(c, out o);
        }

        [System.Runtime.InteropServices.ComImport, System.Runtime.InteropServices.Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"),
         System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItem
        {
            void BindToHandler(IntPtr pbc, [System.Runtime.InteropServices.In] ref Guid bhid, [System.Runtime.InteropServices.In] ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, out IntPtr ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, PreserveSig = false)]
        static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, [System.Runtime.InteropServices.In] ref Guid riid, out IShellItem item);

        static object ShellData(string path)
        {
            try
            {
                var iidItem = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
                var bhid = new Guid("B8C0BD9F-ED24-455c-83E6-D5390C4FE8C4"); // BHID_DataObject
                var iidData = new Guid("0000010e-0000-0000-C000-000000000046");
                IShellItem item;
                SHCreateItemFromParsingName(Path.GetFullPath(path), IntPtr.Zero, ref iidItem, out item);
                IntPtr p;
                item.BindToHandler(IntPtr.Zero, ref bhid, ref iidData, out p);
                var o = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(p);
                System.Runtime.InteropServices.Marshal.Release(p);
                return o as System.Runtime.InteropServices.ComTypes.IDataObject;
            }
            catch { return null; }
        }

        public static void Enable(Control c, Func<string> getFile, Action dropped)
        {
            Point down = Point.Empty;
            bool pressed = false;
            c.MouseDown += (o, e) => { dragged.Remove(c); if (e.Button == MouseButtons.Left) { pressed = true; down = e.Location; } };
            c.MouseUp += (o, e) => pressed = false;
            c.MouseMove += (o, e) =>
            {
                if (!pressed || e.Button != MouseButtons.Left) return;
                var ds = SystemInformation.DragSize;
                if (Math.Abs(e.X - down.X) < ds.Width && Math.Abs(e.Y - down.Y) < ds.Height) return;
                pressed = false;
                dragged.Remove(c); dragged.Add(c, null);
                string path = null;
                var old = Cursor.Current;
                Cursor.Current = Cursors.WaitCursor;
                try { path = getFile(); } catch { }
                Cursor.Current = old;
                if (path == null || !File.Exists(path)) return;
                // 탐색기가 쓰는 것과 똑같은 끌기 데이터 (카톡은 이것만 받음)
                object d = ShellData(path);
                if (d == null)
                {
                    var wd = new DataObject();
                    wd.SetFileDropList(new StringCollection { path });
                    d = wd;
                }
                try { c.DoDragDrop(d, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link); } catch { }
                if (dropped != null && !c.ClientRectangle.Contains(c.PointToClient(Control.MousePosition))) dropped();
            };
        }
    }

    // ---------- 이모지 창 안의 "내 이모티콘" 칸 ----------
    class StickerPanel : Panel
    {
        readonly Theme T;
        readonly float S;
        readonly FlowLayoutPanel flow;
        readonly Label status;
        public event Action<string> Picked;
        public event Action Dropped;
        public Action KeepOpen; // 파일 고르는 창을 여는 동안 이모지 창 유지

        public StickerPanel(Theme t, float s, Font font)
        {
            T = t; S = s;
            BackColor = T.Bg;
            Font = font;
            status = new Label { Dock = DockStyle.Top, Height = (int)(24 * S), ForeColor = T.Muted, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding((int)(8 * S), 0, 0, 0), Font = new Font(font.FontFamily, 8.5f) };
            flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = T.Bg, Padding = new Padding((int)(4 * S)) };
            Controls.Add(flow);
            Controls.Add(status);
        }

        int CellW
        {
            get
            {
                int m = (int)(2 * S);
                int avail = flow.ClientSize.Width - flow.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 2;
                int n = Math.Max(4, avail / (int)(78 * S)); // 창을 넓히면 한 줄에 더 많이
                return (avail - 2 * n * m) / n;
            }
        }

        public void Reload()
        {
            var old = flow.Controls.Cast<Control>().ToList();
            flow.Controls.Clear();
            foreach (var c in old)
            {
                var pb = c as PictureBox;
                if (pb != null && pb.Image != null) { var img = pb.Image; pb.Image = null; img.Dispose(); }
                c.Dispose();
            }
            int cw = CellW, m = (int)(2 * S);

            int inner = flow.ClientSize.Width - flow.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 2;
            var sets = Stickers.Sets();
            int total = 0;
            var tip = new ToolTip();
            foreach (var set in sets)
            {
                bool mine = set.Key == "내 이모티콘";
                if (sets.Count > 1)
                {
                    var head = new Label
                    {
                        Text = set.Key + (set.Value.Count > 0 ? "  " + set.Value.Count : ""), AutoSize = false, Size = new Size(inner - 2 * m, (int)(24 * S)),
                        Margin = new Padding(m, total == 0 && mine ? m : (int)(8 * S), m, 0), ForeColor = T.Muted, TextAlign = ContentAlignment.BottomLeft,
                        Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold),
                    };
                    if (!mine)
                    {
                        var setName = set.Key;
                        head.Text += "   ⋯";
                        head.Cursor = Cursors.Hand;
                        head.MouseClick += (o, e) => SetMenu(setName, head, e.Location);
                        tip.SetToolTip(head, "눌러서 세트 이름 바꾸기·사진 더 넣기·지우기");
                        if (setName.StartsWith("크리에이터의 하루"))
                        {
                            // 폰트 출처 표기: 소제목 바로 옆에 작은 회색 글씨
                            var small = new Font(Font.FontFamily, 7.5f, FontStyle.Regular);
                            head.Paint += (o, e) =>
                            {
                                var fl = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsTranslateTransform;
                                int w = TextRenderer.MeasureText(e.Graphics, head.Text, head.Font, new Size(9999, 99), fl).Width;
                                var sz = TextRenderer.MeasureText(e.Graphics, "font: @aiollcut", small, new Size(9999, 99), fl);
                                int y = head.Height - sz.Height - 1;
                                TextRenderer.DrawText(e.Graphics, "font: @aiollcut", small, new Point(w + (int)(10 * S), y), T.Muted, fl);
                            };
                        }
                    }
                    flow.Controls.Add(head);
                    flow.SetFlowBreak(head, true);
                }
                if (mine)
                {
                    var add = new Label
                    {
                        Text = "＋\n추가", TextAlign = ContentAlignment.MiddleCenter, Size = new Size(cw, cw), Margin = new Padding(m),
                        BackColor = T.Hover, ForeColor = T.Muted, Cursor = Cursors.Hand,
                    };
                    add.Click += (o, e) => AddFromFile();
                    flow.Controls.Add(add);
                    var addSet = new Label
                    {
                        Text = "＋\n세트 추가", TextAlign = ContentAlignment.MiddleCenter, Size = new Size(cw, cw), Margin = new Padding(m),
                        BackColor = T.Hover, ForeColor = T.Muted, Cursor = Cursors.Hand,
                    };
                    addSet.Click += (o, e) => NewSet();
                    flow.Controls.Add(addSet);
                }
                Control last = null;
                foreach (var f in set.Value)
                {
                    Image img = null;
                    try { img = Image.FromStream(new MemoryStream(File.ReadAllBytes(f))); } catch { continue; }
                    var pb = new PictureBox
                    {
                        Size = new Size(cw, cw), SizeMode = PictureBoxSizeMode.Zoom, Image = img, Margin = new Padding(m),
                        BackColor = T.Bg, Cursor = Cursors.Hand, Tag = f,
                    };
                    var path = f;
                    if (!mine) tip.SetToolTip(pb, Stickers.Title(f));
                    pb.MouseEnter += (o, e) => pb.BackColor = T.Hover;
                    pb.MouseLeave += (o, e) => pb.BackColor = T.Bg;
                    pb.MouseClick += (o, e) =>
                    {
                        if (e.Button == MouseButtons.Left) { if (!Drag.JustDragged(pb) && Picked != null) Picked(path); }
                        else if (e.Button == MouseButtons.Right)
                        {
                            var menu = new ContextMenuStrip { Font = AppFont.Make(9f) };
                            menu.Items.Add("삭제", null, (o2, e2) => { Stickers.Delete(path); Reload(); });
                            menu.Show(pb, e.Location);
                        }
                    };
                    Drag.Enable(pb, () => Stickers.ForSend(path), () => { if (Dropped != null) Dropped(); });
                    flow.Controls.Add(pb);
                    last = pb;
                    total++;
                }
                if (mine && last == null) last = flow.Controls[flow.Controls.Count - 1];
                if (last != null) flow.SetFlowBreak(last, true);
            }
            var files = sets.SelectMany(x => x.Value).ToList();
            status.Text = files.Count == 0
                ? "사진이나 그림을 등록하면 이모티콘처럼 보낼 수 있어요"
                : "카톡 창으로 드래그해서 넣기";
        }

        string[] PickFiles(string title)
        {
            if (KeepOpen != null) KeepOpen();
            using (var dlg = new OpenFileDialog
            {
                Title = title,
                Filter = "그림 파일|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|모든 파일|*.*",
                Multiselect = true,
            })
                return dlg.ShowDialog(FindForm()) == DialogResult.OK ? dlg.FileNames : null;
        }

        // 세트 만들기: 이름 입력 → 그림 여러 장 고르기
        void NewSet()
        {
            if (KeepOpen != null) KeepOpen();
            var name = Ask("세트 추가", "세트 이름을 써 주세요 (예: 우리집 고양이)", "");
            if (string.IsNullOrWhiteSpace(name)) return;
            var files = PickFiles("'" + name.Trim() + "' 세트에 넣을 그림 고르기 (여러 장 선택 가능)");
            if (files == null) return;
            var list = Prepare(files);
            if (list.Count == 0) return;
            int ok = Stickers.AddSet(name, list);
            Reload();
            if (ok < list.Count) status.Text = "일부 파일은 그림으로 열 수 없었어요";
        }

        void SetMenu(string setName, Control at, Point p)
        {
            if (KeepOpen != null) KeepOpen();
            var menu = new ContextMenuStrip { Font = AppFont.Make(9f) };
            menu.Items.Add("사진 더 넣기", null, (o, e) =>
            {
                var files = PickFiles("'" + setName + "' 세트에 더 넣을 그림 고르기");
                if (files == null) return;
                var list = Prepare(files);
                if (list.Count == 0) return;
                Stickers.AddToSet(setName, list);
                Reload();
            });
            menu.Items.Add("이름 바꾸기", null, (o, e) =>
            {
                if (KeepOpen != null) KeepOpen();
                var n = Ask("이름 바꾸기", "새 세트 이름", setName);
                if (string.IsNullOrWhiteSpace(n)) return;
                if (!Stickers.RenameSet(setName, n)) status.Text = "같은 이름의 세트가 있거나 바꿀 수 없었어요";
                Reload();
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("세트 지우기", null, (o, e) =>
            {
                if (KeepOpen != null) KeepOpen();
                if (MessageBox.Show(FindForm(), "'" + setName + "' 세트를 지울까요?\n안에 있는 이모티콘도 모두 지워져요.", "세트 지우기",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                Stickers.DeleteSet(setName);
                Reload();
            });
            menu.Show(at, p);
        }

        // 한 줄 입력 창
        string Ask(string title, string prompt, string value)
        {
            using (var f = new Form
            {
                Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false,
                StartPosition = FormStartPosition.CenterParent, ClientSize = new Size((int)(320 * S), (int)(118 * S)), Font = Font, TopMost = true,
            })
            {
                var l = new Label { Text = prompt, Location = new Point((int)(12 * S), (int)(12 * S)), AutoSize = true };
                var t = new TextBox { Text = value, Location = new Point((int)(12 * S), (int)(38 * S)), Width = (int)(296 * S) };
                var ok = new Button { Text = "확인", DialogResult = DialogResult.OK, Location = new Point((int)(146 * S), (int)(76 * S)), Size = new Size((int)(78 * S), (int)(28 * S)) };
                var no = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Location = new Point((int)(230 * S), (int)(76 * S)), Size = new Size((int)(78 * S), (int)(28 * S)) };
                f.Controls.AddRange(new Control[] { l, t, ok, no });
                f.AcceptButton = ok; f.CancelButton = no;
                return f.ShowDialog(FindForm()) == DialogResult.OK ? t.Text.Trim() : null;
            }
        }

        void AddFromFile()
        {
            var files = PickFiles("이모티콘으로 쓸 사진·그림 고르기");
            if (files == null) return;
            var list = Prepare(files);
            int ok = 0;
            foreach (var kv in list)
            {
                try { Stickers.Add(kv.Key, kv.Value); ok++; } catch { }
            }
            Reload();
            if (ok < list.Count) status.Text = "일부 파일은 그림으로 열 수 없었어요";
        }

        // 사진마다 자르기·배경 지우기를 고르는 창 (파일 이름순)
        List<KeyValuePair<string, Stickers.AddOpt>> Prepare(string[] files)
        {
            if (KeepOpen != null) KeepOpen();
            var sorted = files.OrderBy(x => Path.GetFileName(x), StringComparer.CurrentCulture).ToArray();
            using (var f = new AddImageForm(sorted, T, S, Font)) { f.ShowDialog(FindForm()); return f.Result; }
        }
    }
}
