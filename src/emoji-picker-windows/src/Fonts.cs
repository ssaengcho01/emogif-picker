using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace EmojiPicker
{
    // 프로그램 글꼴: 프리텐다드(Pretendard, SIL OFL 1.1 무료 글꼴)를 exe 안에 넣어 두고 씀.
    // PC에 따로 설치하지 않아도 되고, 설치돼 있으면 설치된 것을 그대로 씀.
    static class AppFont
    {
        [DllImport("gdi32.dll")]
        static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, out uint pcFonts);

        static PrivateFontCollection pfc;
        static FontFamily family;

        public static FontFamily Family
        {
            get
            {
                if (family != null) return family;
                try
                {
                    using (var f = new Font("Pretendard", 10f)) if (f.Name == "Pretendard") return family = f.FontFamily;
                }
                catch { }
                try
                {
                    pfc = new PrivateFontCollection();
                    foreach (var name in new[] { "Pretendard-Regular.ttf.gz", "Pretendard-Bold.ttf.gz" })
                    {
                        byte[] data;
                        using (var s = typeof(AppFont).Assembly.GetManifestResourceStream(name))
                        using (var z = new GZipStream(s, CompressionMode.Decompress))
                        using (var ms = new MemoryStream()) { z.CopyTo(ms); data = ms.ToArray(); }
                        // 메모리에 고정해 두고 GDI+(그림 그리기)와 GDI(입력창·글자 그리기) 양쪽에 등록
                        var h = GCHandle.Alloc(data, GCHandleType.Pinned);
                        var p = h.AddrOfPinnedObject();
                        pfc.AddMemoryFont(p, data.Length);
                        try { uint n; AddFontMemResourceEx(p, (uint)data.Length, IntPtr.Zero, out n); } catch { }
                    }
                    if (pfc.Families.Length > 0 && Works(pfc.Families[0])) return family = pfc.Families[0];
                }
                catch { }
                string fallback = "Malgun Gothic";
                try { using (var f = new Font(fallback, 10f)) if (f.Name != fallback) fallback = SystemFonts.MessageBoxFont.Name; } catch { fallback = SystemFonts.MessageBoxFont.Name; }
                return family = new FontFamily(fallback);
            }
        }

        // 글꼴을 실제로 만들어 높이를 재 봄 (안 되면 기본 글꼴로)
        static bool Works(FontFamily f)
        {
            try { using (var x = new Font(f, 10f)) using (var b = new Font(f, 10f, FontStyle.Bold)) return x.Height > 0 && b.Height > 0; }
            catch { return false; }
        }

        public static Font Make(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(Family, size, style);
        }
    }
}
