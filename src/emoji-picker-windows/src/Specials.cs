using System;
using System.Collections.Generic;
using System.Drawing;

namespace EmojiPicker
{
    // 특수문자 칸: 이모지 그림이 아니라 글자로 보이는 심플한 기호들
    // (그룹 번호 100~107, 탭은 100 하나만 보여 줌)
    static class Specials
    {
        public const int FirstGroup = 100;
        const string VS15 = "︎"; // 컬러 이모지가 아니라 글자 모양으로 보이게 하는 표시

        // 제목, [글자, 이름, 검색어]
        static readonly object[][] Sets =
        {
            new object[] { "느낌표·물음표", new[]
            {
                ".ᐟ|작은 느낌표|느낌표 느낌", "ᐟ|작은 느낌표|느낌표", "ꜝ|작은 느낌표|느낌표", "‼" + VS15 + "|느낌표 두 개|느낌표",
                "¡|뒤집힌 느낌표|느낌표", "ˀ̣|작은 물음표|물음표 궁금", "ˀ|작은 물음표|물음표 궁금", "¿|뒤집힌 물음표|물음표 궁금", "⸮|거꾸로 물음표|물음표 궁금",
            } },
            new object[] { "하트", new[]
            {
                "♡|빈 하트|하트 사랑", "♥" + VS15 + "|하트|하트 사랑", "❤" + VS15 + "|하트|하트 사랑", "❥|기울어진 하트|하트 사랑",
                "❣" + VS15 + "|느낌표 하트|하트 사랑", "ღ|하트|하트 사랑", "❦|꽃 하트|하트 꽃",
            } },
            new object[] { "별", new[]
            {
                "★|검은 별|별", "☆|흰 별|별", "✦|반짝이 별|별 반짝", "✧|흰 반짝이 별|별 반짝", "⋆|작은 별|별", "✩|별|별", "✪|원 안의 별|별",
                "✫|별|별", "✬|별|별", "✭|별|별", "✮|별|별", "✯|별|별", "✶|여섯 꼭지 별|별", "✷|별|별", "✸|별|별", "✹|별|별", "✺|별|별",
                "⭑|작은 별|별", "⭒|작은 흰 별|별", "˚|작은 동그라미|반짝", "⁺|작은 더하기|반짝", "₊|아래 작은 더하기|반짝",
            } },
            new object[] { "꽃", new[]
            {
                "✿|꽃|꽃", "❀|흰 꽃|꽃", "❁|꽃|꽃", "✾|꽃|꽃", "❃|꽃|꽃", "❋|꽃|꽃", "✽|꽃|꽃", "❊|꽃|꽃", "✻|꽃|꽃",
                "✼|꽃|꽃", "✤|꽃|꽃", "✥|꽃|꽃", "ꕤ|꽃|꽃", "ꕥ|꽃|꽃", "⚘|꽃 한 송이|꽃",
            } },
            new object[] { "얼굴", new[]
            {
                "◡̈|웃는 얼굴|얼굴 웃음 스마일", "ᵕ̈|웃는 얼굴|얼굴 웃음 스마일", "☺" + VS15 + "|웃는 얼굴|얼굴 웃음 스마일", "☻|검은 웃는 얼굴|얼굴 웃음 스마일",
                "☹" + VS15 + "|찡그린 얼굴|얼굴 슬픔", "⌒̈|시무룩한 얼굴|얼굴 슬픔 우는", "◠̈|시무룩한 얼굴|얼굴 슬픔 우는", "◠̋|우는 얼굴|얼굴 슬픔 우는 눈물", "㋡|원 안의 웃는 얼굴|얼굴 웃음 스마일", "ツ|웃는 얼굴 (츠)|얼굴 웃음 스마일", "ッ|웃는 얼굴 (작은 츠)|얼굴 웃음 스마일",
            } },
            new object[] { "나비·곰돌이·리본", new[]
            {
                "ʚɞ|나비|나비", "ʚĭɞ|나비|나비", "ʚ♡ɞ|하트 나비|나비 하트",
                "ʕ•ᴥ•ʔ|곰돌이|곰 곰돌이", "ʕ·ᴥ·ʔ|곰돌이|곰 곰돌이", "ʕᵔᴥᵔʔ|웃는 곰돌이|곰 곰돌이 웃음", "ᴥ|곰 코|곰 곰돌이",
                "୨୧|리본|리본", "୨♡୧|하트 리본|리본 하트", "୨ৎ|리본|리본", "𐙚|리본|리본", "ᡣ𐭩|리본|리본",
                "୨⎯୧|긴 리본|리본", "꒰ა໒꒱|리본 괄호|리본 괄호", "꒰ა|리본 괄호 왼쪽|리본 괄호", "໒꒱|리본 괄호 오른쪽|리본 괄호", "ꔛ|리본|리본",
            } },
            new object[] { "까만 원 숫자", new[]
            {
                "❶|숫자 1|숫자 1 번호", "❷|숫자 2|숫자 2 번호", "❸|숫자 3|숫자 3 번호", "❹|숫자 4|숫자 4 번호", "❺|숫자 5|숫자 5 번호",
                "❻|숫자 6|숫자 6 번호", "❼|숫자 7|숫자 7 번호", "❽|숫자 8|숫자 8 번호", "❾|숫자 9|숫자 9 번호", "❿|숫자 10|숫자 10 번호",
                "⓫|숫자 11|숫자 11 번호", "⓬|숫자 12|숫자 12 번호", "⓭|숫자 13|숫자 13 번호", "⓮|숫자 14|숫자 14 번호", "⓯|숫자 15|숫자 15 번호",
                "⓰|숫자 16|숫자 16 번호", "⓱|숫자 17|숫자 17 번호", "⓲|숫자 18|숫자 18 번호", "⓳|숫자 19|숫자 19 번호", "⓴|숫자 20|숫자 20 번호",
            } },
            new object[] { "음표", new[]
            {
                "♩|4분음표|음표 음악 노래", "♪|8분음표|음표 음악 노래", "♫|이어진 음표|음표 음악 노래", "♬|이어진 16분음표|음표 음악 노래",
                "♭|플랫|음표 음악", "♯|샤프|음표 음악", "𝄞|높은음자리표|음표 음악 노래",
            } },
        };

        public static void Add()
        {
            int idx = Data.All.Count;
            for (int i = 0; i < Sets.Length; i++)
            {
                int gid = FirstGroup + i;
                Data.Groups.Add(new EmojiGroup { Id = gid, Icon = i == 0 ? "★" : "", Name = (string)Sets[i][0] });
                foreach (var row in (string[])Sets[i][1])
                {
                    var f = row.Split('|');
                    if (Data.ByChar.ContainsKey(f[0])) continue;
                    var e = new Emoji
                    {
                        Ch = f[0], Label = f[1], Group = gid, Idx = idx++, IsText = true,
                        Ko = (f[1] + " " + f[2] + " 특수문자 기호").ToLowerInvariant().Split(' '), En = new string[0], Pri = new string[0],
                    };
                    Data.All.Add(e);
                    Data.ByChar[e.Ch] = e;
                }
            }
        }

        static Font font;
        static float fontPx;

        // 기호를 칸 가운데에 글자로 그리기 (컬러 이모지 글꼴이 아닌 기호 글꼴 사용)
        public static void Draw(Graphics g, Emoji e, RectangleF dest, Color color)
        {
            float px = dest.Height * 0.78f;
            if (font == null || fontPx != px)
            {
                if (font != null) font.Dispose();
                var name = "Segoe UI Symbol";
                using (var test = new Font(name, 10f)) if (test.Name != name) name = SystemFonts.MessageBoxFont.Name;
                font = new Font(name, px, GraphicsUnit.Pixel);
                fontPx = px;
            }
            var r = Rectangle.Round(RectangleF.Inflate(dest, dest.Width * 0.3f, dest.Height * 0.3f));
            var f = font;
            // ʕ•ᴥ•ʔ처럼 여러 글자는 칸에 들어가도록 글자 크기를 줄임
            var size = System.Windows.Forms.TextRenderer.MeasureText(g, e.Ch, font, Size.Empty, System.Windows.Forms.TextFormatFlags.NoPadding);
            float maxW = dest.Width * 1.45f;
            Font small = null;
            float scale = size.Width > maxW ? maxW / size.Width : 1f;
            int visible = 0;
            foreach (char c in e.Ch)
                if (!char.IsLowSurrogate(c) && System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) visible++;
            if (visible >= 4) scale = Math.Min(scale, 2.6f / visible); // 글꼴에 따라 폭 계산이 틀릴 때를 대비
            if (scale < 1f) f = small = new Font(font.FontFamily, Math.Max(6f, px * scale), GraphicsUnit.Pixel);
            System.Windows.Forms.TextRenderer.DrawText(g, e.Ch, f, r, color,
                System.Windows.Forms.TextFormatFlags.HorizontalCenter | System.Windows.Forms.TextFormatFlags.VerticalCenter |
                System.Windows.Forms.TextFormatFlags.NoPrefix | System.Windows.Forms.TextFormatFlags.NoPadding |
                System.Windows.Forms.TextFormatFlags.PreserveGraphicsTranslateTransform); // 스크롤 위치(이동 변환)를 따르게 (Windows에서는 이 표시가 없으면 무시됨)
            if (small != null) small.Dispose();
        }
    }
}
