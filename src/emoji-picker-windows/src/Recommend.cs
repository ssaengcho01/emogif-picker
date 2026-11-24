using System;
using System.Collections.Generic;
using System.Linq;

namespace EmojiPicker
{
    // 커서 앞에 쓴 글에 어울리는 이모지 추천 (인터넷 없이 동작하는 한국어 키워드 사전)
    static class Recommend
    {
        // "키워드들 = 이모지들". 키워드는 글 안에 들어 있기만 하면 맞는 것으로 봄 (오픈 → 오픈했어요도 맞음)
        // 앞쪽 이모지일수록 먼저 보여 줌
        static readonly string[] Rules =
        {
            "축하 오픈 런칭 출시 개업 개강 개원 합격 승진 당선 수상 우승 성공했 성공이 드디어 달성 돌파 기념 경축 개봉 완판 첫 방송 데뷔 = 🎉🥳🎊👏✨🍾🙌💐🏆",
            "생일 생신 생축 생일축하 = 🎂🥳🎁🎉🎈🕯️",
            "결혼 웨딩 신혼 = 💍👰🤵💒💐🥂",
            "출산 아기 임신 태어났 = 👶🍼🤱💕🎀",
            "감사 고마 고맙 땡큐 thank ㄱㅅ = 🙏🥰💕😊🤗💐",
            "사랑 좋아해 러브 love 보고싶 보고 싶 = ❤️😍🥰😘💕💖",
            "미안 죄송 sorry ㅈㅅ = 🙇🙏😢🥺😥",
            "안녕하세요 안녕 반가 반갑 hello = 👋😊🙌🤗",
            "수고 고생 파이팅 화이팅 홧팅 힘내 응원 = 💪🔥📣👍🙌✊",
            "잘자 잘 자 굿밤 주무 꿀잠 = 🌙😴💤🛌✨",
            "좋은 아침 굿모닝 아침 = ☀️🌞☕🌅😊",
            "점심 저녁 식사 밥 먹었 먹자 먹어 = 🍚🍽️😋🍜🍱",
            "맛있 맛집 냠냠 = 😋🤤👍🍽️",
            "배고 = 🤤🍔🍕🍗",
            "커피 카페 라떼 아아 = ☕🧋🍰",
            "맥주 소주 술한잔 술 한잔 술 마시 회식 건배 = 🍻🍺🥂🍶🍷",
            "강의 수업 클래스 교육 세미나 강좌 워크숍 특강 = 📚🎓✏️🧑‍🏫📝💡",
            "공부 시험 과제 숙제 = 📚✏️📝🤓💯",
            "업무 회의 출근 야근 미팅 일하 = 💼💻📊📈⏰",
            "퇴근 주말 금요일 불금 휴가 쉬는 휴식 = 🥳🎉🛋️🏖️😎🍻",
            "여행 비행기 공항 해외 제주 = ✈️🧳🏝️🌴📸🗺️",
            "바다 해변 수영 = 🏖️🌊🐚☀️",
            "운동 헬스 다이어트 러닝 요가 = 💪🏃🏋️🔥🥗🧘",
            "아프 아파 병원 감기 몸살 = 🤒😷💊🏥🤧",
            "피곤 힘들 지쳐 지친 졸려 졸리 = 😩😮‍💨😴🥱💤",
            "슬프 슬퍼 눈물 울었 울고 ㅠㅠ ㅜㅜ 속상 = 😭😢🥺💧😞",
            "웃기 웃겨 웃김 ㅋㅋ ㅎㅎ 하하 = 😂🤣😆😄😁",
            "화나 화난 짜증 빡치 열받 = 😡😤🤬💢😠",
            "헐 깜짝 놀랐 놀라 = 😮😲🤯😱👀",
            "걱정 불안 긴장 떨려 = 😟😰😬🙏🤞",
            "기대 설레 두근 = 🤩😆💓✨🥰",
            "최고 짱 대박 멋지 멋있 멋져 훌륭 좋아요 좋다 굿굿 = 👍👏😎🔥💯✨",
            "궁금 질문 어떻게 = 🤔❓🧐💭",
            "알겠 넵 네네 오케이 ㅇㅋ 확인했 = 👌👍✅🙆🫡",
            "월급 매출 수익 결제 입금 판매 = 💰💵💸📈🤑",
            "선물 택배 배송 = 🎁📦🚚💝",
            "비가 비 와 장마 우산 = ☔🌧️☂️💧",
            "눈이 눈 와 눈와 겨울 추워 춥다 = ❄️⛄🧣☃️🥶",
            "크리스마스 성탄 = 🎄🎅🎁✨⛄",
            "더워 덥다 여름 = 🥵☀️🍉🍦🌴",
            "벚꽃 꽃 봄 = 🌸🌷💐🌼",
            "가을 단풍 = 🍁🍂🌰",
            "새해 설날 명절 추석 = 🎍🧧🌕🙇✨🎊",
            "고양이 냥 = 🐱😺🐾",
            "강아지 멍멍 댕댕 = 🐶🐕🐾",
            "음악 노래 = 🎵🎶🎧🎤",
            "영상 유튜브 촬영 영화 = 🎬📹🎥🍿▶️",
            "사진 = 📸📷🖼️",
            "디자인 그림 = 🎨✍️🖌️💻",
            "게임 = 🎮🕹️👾",
            "이사 집들이 = 🏠📦🔑",
            "약속 만나 모임 = 🤝📅👫😊",
            "시작 도전 새로운 = 🚀✨🔥💪🌱",
            "완료 마감 마무리 끝났 끝냈 = ✅🎉😮‍💨🙌🏁",
            "지각 늦었 늦어 서둘 = 🏃⏰😅💦",
            "책 독서 읽었 = 📚📖🤓☕",
            "월요병 = 😩☕💼😮‍💨",
            "싫어 싫다 별로 = 😒🙅😑👎",
            "우울 외로 쓸쓸 = 😔😞🫂🌧️",
            "축구 야구 농구 경기 = ⚽⚾🏀🏆📣",
            "청소 빨래 = 🧹🧺✨🫧",
            "쇼핑 샀어 구매 = 🛍️💳🛒😍",
            "부탁 = 🙏🥺🙇",
            "칭찬 잘했 대단 = 👏👍🌟💯",
            "내일봐 또봐 이따봐 나중에봐 담에봐 = 👋😉😊",
            "와주세요 와 주세요 와줘 놀러와 참여 신청 많관부 관심 = 🙏🙌😊💌",
        };

        class Rule { public string[] Keys; public List<Emoji> Emojis; }
        static List<Rule> rules;

        static Emoji Find(string ch)
        {
            Emoji e;
            if (Data.ByChar.TryGetValue(ch, out e)) return e;
            if (Data.ByChar.TryGetValue(ch + "️", out e)) return e;
            if (Data.ByChar.TryGetValue(ch.Replace("️", ""), out e)) return e;
            return null;
        }

        // 이모지 여러 개가 붙어 있는 글에서 데이터에 있는 이모지만 순서대로 골라내기 (가장 긴 것부터 맞춤)
        public static List<Emoji> ParseEmojis(string s)
        {
            var list = new List<Emoji>();
            int i = 0;
            while (i < s.Length)
            {
                Emoji hit = null;
                int used = 0;
                for (int len = Math.Min(14, s.Length - i); len >= 1; len--)
                {
                    var e = Find(s.Substring(i, len));
                    if (e != null) { hit = e; used = len; break; }
                }
                if (hit != null) { if (!list.Contains(hit)) list.Add(hit); i += used; }
                else i++;
            }
            return list;
        }

        static List<Rule> Rules_
        {
            get
            {
                if (rules == null)
                {
                    rules = new List<Rule>();
                    foreach (var line in Rules)
                    {
                        var parts = line.Split(new[] { " = " }, StringSplitOptions.None);
                        var keys = new List<string>();
                        // "보고 싶"처럼 띄어 쓴 키워드는 붙여 쓴 글에서도 찾도록 공백을 없앤 형태로 비교
                        foreach (var k in SplitKeys(parts[0])) keys.Add(k);
                        rules.Add(new Rule { Keys = keys.ToArray(), Emojis = ParseEmojis(parts[1]) });
                    }
                }
                return rules;
            }
        }

        // "좋은 아침"처럼 두 단어짜리 키워드를 하나로 묶기
        static IEnumerable<string> SplitKeys(string s)
        {
            var w = s.Split(' ');
            var pairs = new HashSet<string> { "좋은 아침", "보고 싶", "잘 자", "첫 방송", "비 와", "눈 와", "술 한잔", "술 마시", "와 주세요" };
            for (int i = 0; i < w.Length; i++)
            {
                if (i + 1 < w.Length && pairs.Contains(w[i] + " " + w[i + 1])) { yield return (w[i] + w[i + 1]).ToLowerInvariant(); i++; }
                else if (w[i].Length > 0) yield return w[i].ToLowerInvariant();
            }
        }

        // 단어 끝의 조사·어미 떼기 (강의를 → 강의, 오픈했어요 → 오픈)
        static readonly string[] Endings =
        {
            "했습니다", "합니다", "했어요", "해요", "했다", "했어", "하는", "하고", "해서", "입니다", "이에요", "예요", "에요",
            "어요", "아요", "에서", "으로", "이랑", "랑", "까지", "부터", "에게", "한테", "께서", "님", "들",
            "을", "를", "이", "가", "은", "는", "에", "로", "도", "만", "과", "와", "의", "요", "다",
        };

        // 뜻이 너무 넓어서 이모지 검색에 쓰지 않는 말
        static readonly HashSet<string> Stop = new HashSet<string>
        {
            "오늘", "내일", "어제", "지금", "이번", "다음", "우리", "저희", "너무", "진짜", "정말", "완전", "그냥", "언제", "어디", "누구",
            "무엇", "뭐", "이제", "아직", "벌써", "같이", "함께", "많이", "조금", "좀", "다시", "계속", "혹시", "그럼", "그래", "근데",
            "그리고", "하지만", "제가", "내가", "너는", "당신", "여러분", "할래", "해볼", "있어", "없어", "있는", "없는", "했는데",
        };

        static string Stem(string w)
        {
            for (int round = 0; round < 2; round++)
                foreach (var e in Endings)
                    if (w.Length > e.Length + 1 && w.EndsWith(e, StringComparison.Ordinal)) { w = w.Substring(0, w.Length - e.Length); break; }
            return w;
        }

        public static List<Emoji> For(string text)
        {
            var result = new List<Emoji>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            var lower = text.ToLowerInvariant();
            var flat = lower.Replace(" ", "");
            var scores = new Dictionary<Emoji, double>();
            Action<Emoji, double> add = (e, s) =>
            {
                double old;
                if (!scores.TryGetValue(e, out old) || s > old) scores[e] = s;
            };

            // 1) 키워드 사전: 글 뒤쪽(최근에 쓴 말)에 나온 키워드일수록 앞에
            var matched = new List<string>();
            foreach (var r in Rules_)
            {
                double best = -1;
                foreach (var k in r.Keys)
                {
                    int p = flat.LastIndexOf(k, StringComparison.Ordinal);
                    if (p < 0) continue;
                    matched.Add(k);
                    // 키워드가 끝나는 위치가 뒤쪽일수록, 키워드가 길수록(더 구체적일수록) 앞에
                    best = Math.Max(best, 60.0 * (p + k.Length) / Math.Max(1, flat.Length) + k.Length);
                }
                if (best < 0) continue;
                double baseScore = 200 + best;
                for (int i = 0; i < r.Emojis.Count; i++) add(r.Emojis[i], baseScore - i * 2.5);
            }

            // 2) 사전에 없는 단어는 이모지 이름·태그 검색으로 보충 (예: "피자 먹자" → 🍕)
            var words = lower.Split(new[] { ' ', '\t', ',', '.', '!', '?', '~', '…', '"', '\'', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
            for (int wi = 0; wi < words.Length; wi++)
            {
                var w = Stem(words[wi]);
                if (w.Length < 2 || Stop.Contains(w) || matched.Any(k => w.Contains(k) || k.Contains(w))) continue;
                var found = Search.FindExact(w);
                if (found.Count == 0 || found.Count > 30) continue; // 너무 흔한 말은 건너뜀
                double baseScore = 200 + 60.0 * (lower.LastIndexOf(words[wi], StringComparison.Ordinal) + words[wi].Length) / Math.Max(1, lower.Length) - 5;
                for (int i = 0; i < Math.Min(3, found.Count); i++) add(found[i], baseScore - i * 3);
            }

            // 3) 물음표로 끝나면 생각하는 얼굴
            var trimmed = text.TrimEnd();
            if (trimmed.EndsWith("?") || trimmed.EndsWith("？")) { var e = Find("🤔"); if (e != null) add(e, 150); }

            return scores.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key.Idx).Select(kv => kv.Key).Take(24).ToList();
        }

        // ---------- 긴 글 자동 꾸미기: 문장마다 끝에 어울리는 이모지 하나씩 ----------
        const string Punct = ".!?。！？~";
        const string Jamo = "ㅋㅎㅠㅜ";
        static readonly HashSet<string> QuestionMarks = new HashSet<string> { "❓", "❔", "⁉", "⁈" };

        // 문장 단위로 나누기 (문장부호, ㅋㅋ·ㅠㅠ, 줄바꿈 기준). 나눈 조각을 이어 붙이면 원래 글과 같음
        public static List<string> SplitSentences(string text)
        {
            var parts = new List<string>();
            var cur = new System.Text.StringBuilder();
            Action flush = () => { if (cur.Length > 0) parts.Add(cur.ToString()); cur.Clear(); };
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '\n' || c == '\r')
                {
                    flush();
                    int j = i; while (j < text.Length && (text[j] == '\n' || text[j] == '\r')) j++;
                    parts.Add(text.Substring(i, j - i)); i = j; continue;
                }
                cur.Append(c); i++;
                bool end = false;
                if (Punct.IndexOf(c) >= 0 && !(i < text.Length && Punct.IndexOf(text[i]) >= 0)) end = true;
                // 점(.)은 뒤가 띄어쓰기·줄바꿈·글 끝일 때만 마침표로 봄 (4.5, www.a.com 같은 건 문장 끝이 아님)
                if (c == '.' && i < text.Length && !char.IsWhiteSpace(text[i])) end = false;
                if (Jamo.IndexOf(c) >= 0 && !(i < text.Length && Jamo.IndexOf(text[i]) >= 0))
                {
                    int k = cur.Length - 1; while (k >= 0 && Jamo.IndexOf(cur[k]) >= 0) k--;
                    if (cur.Length - 1 - k >= 2 && (i >= text.Length || char.IsWhiteSpace(text[i]))) end = true;
                }
                if (end) { while (i < text.Length && text[i] != '\n' && text[i] != '\r' && char.IsWhiteSpace(text[i])) cur.Append(text[i++]); flush(); }
            }
            flush();
            return parts;
        }

        // 글 끝이 이미 이모지인지 (그림 문자: 서로게이트 쌍이나 ☀ 같은 기호)
        static bool EndsWithEmoji(string s)
        {
            s = s.TrimEnd().TrimEnd('️', '‍');
            if (s.Length == 0) return false;
            char c = s[s.Length - 1];
            if (char.IsLowSurrogate(c)) return true;
            return c >= 0x2190 && c <= 0x2BFF || c == '©' || c == '®' || c >= 0x3290 && c <= 0x329F;
        }

        public static string AutoDecorate(string text) { int n; return AutoDecorate(text, out n); }

        public static string AutoDecorate(string text, out int added)
        {
            added = 0;
            var sb = new System.Text.StringBuilder();
            string prev = null;
            foreach (var part in SplitSentences(text ?? ""))
            {
                int e = part.Length; while (e > 0 && char.IsWhiteSpace(part[e - 1])) e--;
                string body = part.Substring(0, e), tail = part.Substring(e);
                string plain = body.TrimEnd(Punct.ToCharArray());
                if (plain.Trim().Length == 0 || EndsWithEmoji(plain) || EndsWithEmoji(body) || new string(plain.Where(ch => !char.IsWhiteSpace(ch)).ToArray()).Length < 3)
                { sb.Append(part); continue; }
                bool question = plain.Length < body.TrimEnd().Length && body.TrimEnd().IndexOfAny("?？".ToCharArray(), plain.Length) >= 0;
                var rec = For(body).Select(x => x.Ch)
                    .Where(x => !(question && QuestionMarks.Contains(x.Replace("\uFE0F", "")))) // 물음표 뒤에 물음표 이모지 금지
                    .Where(x => !(x.Length >= 2 && x[0] == '\uD83C' && x[1] >= '\uDDE6' && x[1] <= '\uDDFF')) // 국기는 자동으로 붙이지 않음
                    .FirstOrDefault(x => x != prev && !body.Contains(x));
                if (rec == null) { sb.Append(part); continue; }
                prev = rec; added++;
                sb.Append(body).Append(rec).Append(tail);
            }
            return sb.ToString();
        }
    }
}
