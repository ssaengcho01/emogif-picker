using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace EmojiPicker
{
    class Emoji
    {
        public string Ch, Label;
        public int Group, Idx;
        public string[] Ko, En, Pri;
        public bool IsText; // 특수문자 (그림이 아니라 글자로 그림)
    }

    class EmojiGroup
    {
        public int Id;
        public string Icon, Name;
    }

    static class Data
    {
        public const int SpriteCols = 48, Tile = 48;
        public static readonly List<Emoji> All = new List<Emoji>();
        public static readonly List<EmojiGroup> Groups = new List<EmojiGroup>();
        public static readonly Dictionary<string, Emoji> ByChar = new Dictionary<string, Emoji>();
        public static readonly List<string[]> Synonyms = new List<string[]>();
        public static Bitmap Sprite;

        static string[] Split(string s)
        {
            return string.IsNullOrEmpty(s) ? new string[0] : s.ToLowerInvariant().Split('|');
        }

        static Stream Res(string name)
        {
            var s = typeof(Data).Assembly.GetManifestResourceStream(name);
            if (s == null) throw new Exception("리소스 없음: " + name);
            return s;
        }

        public static void Load()
        {
            using (var r = new StreamReader(Res("emoji.tsv"), Encoding.UTF8))
            {
                string line;
                int idx = 0;
                while ((line = r.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;
                    var f = line.Split('\t');
                    if (f[0] == "#group")
                    {
                        Groups.Add(new EmojiGroup { Id = int.Parse(f[1]), Icon = f[2], Name = f[3] });
                        continue;
                    }
                    var e = new Emoji
                    {
                        Ch = f[0], Group = int.Parse(f[1]), Label = f[2],
                        Ko = Split(f[3]), En = Split(f[4]), Pri = Split(f.Length > 5 ? f[5] : ""),
                        Idx = idx++,
                    };
                    All.Add(e);
                    ByChar[e.Ch] = e;
                }
            }
            using (var r = new StreamReader(Res("synonyms.txt"), Encoding.UTF8))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                    if (line.Trim().Length > 0) Synonyms.Add(line.Trim().Split(' '));
            }
            Sprite = new Bitmap(Res("emoji-sprite.png"));
            Specials.Add();
        }

        public static Rectangle SpriteRect(Emoji e)
        {
            return new Rectangle(e.Idx % SpriteCols * Tile, e.Idx / SpriteCols * Tile, Tile, Tile);
        }
    }

    // 확장 프로그램(picker.js)과 같은 검색 규칙
    static class Search
    {
        class Term
        {
            public string Typed;
            public string[] Words;
            public string Stem;
        }

        static Dictionary<string, string[]> synMap;

        static Dictionary<string, string[]> SynMap
        {
            get
            {
                if (synMap == null)
                {
                    synMap = new Dictionary<string, string[]>();
                    foreach (var g in Data.Synonyms) foreach (var w in g) synMap[w] = g;
                }
                return synMap;
            }
        }

        static bool IsHangul(string t)
        {
            if (t.Length == 0) return false;
            foreach (var c in t) if (c < '가' || c > '힣') return false;
            return true;
        }

        static bool Starts(string w, string t) { return w.StartsWith(t, StringComparison.Ordinal); }

        static int ScoreTerm(Emoji e, string term, string stem)
        {
            int best = 0;
            foreach (var w in e.Pri)
            {
                if (w == term) return 120;
                if (Starts(w, term)) best = Math.Max(best, 80);
            }
            foreach (var w in e.Ko)
            {
                if (w == term) best = Math.Max(best, 100);
                else if (Starts(w, term)) best = Math.Max(best, 70);
                else if (w.Contains(" " + term)) best = Math.Max(best, 45); // 띄어쓴 단어의 시작만 (기울어가는 ≠ 울어)
            }
            if (best < 70)
            {
                foreach (var w in e.En)
                {
                    if (w == term) best = Math.Max(best, 90);
                    else if (Starts(w, term)) best = Math.Max(best, 60);
                    else if (w.Contains(" " + term)) best = Math.Max(best, 35);
                }
            }
            if (best == 0 && stem != null)
            {
                foreach (var w in e.Pri.Concat(e.Ko))
                    if (w.Length > stem.Length && Starts(w, stem)) { best = 20; break; }
            }
            return best;
        }

        static List<Emoji> Run(List<List<Term>> alts, bool useStem)
        {
            var results = new List<Tuple<Emoji, double>>();
            foreach (var e in Data.All)
            {
                double total = 0;
                foreach (var terms in alts)
                {
                    double sum = 0;
                    foreach (var term in terms)
                    {
                        double s = 0;
                        int hits = 0;
                        foreach (var w in term.Words)
                        {
                            // 묶음에서 딸려 온 한 글자 단어(화, ㅠ)는 정확히 같을 때만 (화 ≠ 화이팅)
                            int v = w != term.Typed && w.Length == 1
                                ? (e.Pri.Contains(w) || e.Ko.Contains(w) ? 100 : 0)
                                : ScoreTerm(e, w, useStem ? term.Stem : null);
                            if (v >= 90) hits++;
                            s = Math.Max(s, v);
                        }
                        if (hits > 1) s += Math.Min(hits - 1, 8) * 6;
                        if (s == 0) { sum = 0; break; }
                        sum += s;
                    }
                    total += sum / terms.Count;
                }
                if (total > 0) results.Add(Tuple.Create(e, total));
            }
            return results.OrderByDescending(r => r.Item2).ThenBy(r => r.Item1.Idx).Select(r => r.Item1).ToList();
        }

        // 한 단어를 이모지 이름·태그와 정확히(앞부분 일치까지) 맞춰 보기. 어미를 잘라 내는 느슨한 검색은 하지 않음
        public static List<Emoji> FindExact(string word)
        {
            string[] syn;
            var t = word.ToLowerInvariant();
            var term = new Term { Typed = t, Words = SynMap.TryGetValue(t, out syn) ? syn : new[] { t } };
            return Run(new List<List<Term>> { new List<Term> { term } }, false);
        }

        // 공백 = 모두 포함(AND), "/" 또는 "," = 하나라도 포함(OR)
        public static List<Emoji> Find(string query)
        {
            var alts = new List<List<Term>>();
            var seen = new HashSet<string>();
            foreach (var part in query.ToLowerInvariant().Split('/', ',', '|'))
            {
                var p = part.Trim();
                if (p.Length == 0) continue;
                var terms = new List<Term>();
                foreach (var t in p.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] syn;
                    bool isSyn = SynMap.TryGetValue(t, out syn);
                    terms.Add(new Term
                    {
                        Typed = t,
                        Words = isSyn ? syn : new[] { t },
                        Stem = !isSyn && IsHangul(t) && t.Length >= 2 ? t.Substring(0, t.Length - 1) : null,
                    });
                }
                var key = string.Join(" ", terms.Select(x => string.Join(",", x.Words)));
                if (seen.Add(key)) alts.Add(terms);
            }
            if (alts.Count == 0) return new List<Emoji>();
            var exact = Run(alts, false);
            if (exact.Count >= 1) return exact; // 정확히 맞는 게 하나라도 있으면 느슨한 검색은 하지 않음 (피자 → 피… 방지)
            var have = new HashSet<Emoji>(exact);
            exact.AddRange(Run(alts, true).Where(e => !have.Contains(e)));
            return exact;
        }
    }
}
