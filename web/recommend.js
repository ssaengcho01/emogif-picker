// 쓴 문장에 어울리는 이모지 추천 (PC 버전 Recommend.cs와 같은 사전, 인터넷 없이 동작)
(() => {
  const RULES = [
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
"와주세요 와 주세요 와줘 놀러와 참여 신청 많관부 관심 = 🙏🙌😊💌"
];
  const PAIRS = new Set(["좋은 아침", "보고 싶", "잘 자", "첫 방송", "비 와", "눈 와", "술 한잔", "술 마시", "와 주세요"]);
  const ENDINGS = ["했습니다", "합니다", "했어요", "해요", "했다", "했어", "하는", "하고", "해서", "입니다", "이에요", "예요", "에요",
    "어요", "아요", "에서", "으로", "이랑", "랑", "까지", "부터", "에게", "한테", "께서", "님", "들",
    "을", "를", "이", "가", "은", "는", "에", "로", "도", "만", "과", "와", "의", "요", "다"];
  const STOP = new Set("오늘 내일 어제 지금 이번 다음 우리 저희 너무 진짜 정말 완전 그냥 언제 어디 누구 무엇 뭐 이제 아직 벌써 같이 함께 많이 조금 좀 다시 계속 혹시 그럼 그래 근데 그리고 하지만 제가 내가 너는 당신 여러분 할래 해볼 있어 없어 있는 없는 했는데".split(" "));
  const seg = s => { try { return [...new Intl.Segmenter("ko", { granularity: "grapheme" }).segment(s)].map(x => x.segment); } catch (_) { return [...s]; } };
  const rules = RULES.map(line => {
    const [k, e] = line.split(" = ");
    const w = k.split(" "), keys = [];
    for (let i = 0; i < w.length; i++) {
      if (i + 1 < w.length && PAIRS.has(w[i] + " " + w[i + 1])) { keys.push((w[i] + w[i + 1]).toLowerCase()); i++; }
      else if (w[i]) keys.push(w[i].toLowerCase());
    }
    return { keys, emojis: seg(e) };
  });
  function stem(w) {
    for (let r = 0; r < 2; r++) for (const e of ENDINGS) if (w.length > e.length + 1 && w.endsWith(e)) { w = w.slice(0, -e.length); break; }
    return w;
  }
  function repeatsQuestionMark(text, emoji) {
    return /[?？]\s*$/u.test(text) && /[?？❓❔⁉]/u.test(emoji);
  }
  function recommend(text) {
    if (!text || !text.trim()) return [];
    const lower = text.toLowerCase(), flat = lower.replace(/ /g, "");
    const scores = new Map(), order = new Map();
    const add = (e, s) => { if (!scores.has(e) || s > scores.get(e)) scores.set(e, s); if (!order.has(e)) order.set(e, order.size); };
    const matched = [];
    for (const r of rules) {
      let best = -1;
      for (const k of r.keys) {
        const p = flat.lastIndexOf(k);
        if (p < 0) continue;
        matched.push(k);
        best = Math.max(best, 60 * (p + k.length) / Math.max(1, flat.length) + k.length);
      }
      if (best < 0) continue;
      r.emojis.forEach((e, i) => add(e, 200 + best - i * 2.5));
    }
    const words = lower.split(/[\s,.!?~…"'()]+/).filter(Boolean);
    for (const raw of words) {
      const w = stem(raw);
      if (w.length < 2 || STOP.has(w) || matched.some(k => w.includes(k) || k.includes(w))) continue;
      const found = window.EmojiPicker.searchExact(w);
      if (!found.length || found.length > 30) continue;
      const base = 200 + 60 * (lower.lastIndexOf(raw) + raw.length) / Math.max(1, lower.length) - 5;
      found.slice(0, 3).forEach((e, i) => add(e.ch, base - i * 3));
    }
    const t = text.trimEnd();
    if (t.endsWith("?") || t.endsWith("？")) add("🤔", 150);
    return [...scores.entries()].filter(([emoji]) => !repeatsQuestionMark(text, emoji)).sort((a, b) => b[1] - a[1] || order.get(a[0]) - order.get(b[0])).map(x => x[0]).slice(0, 24);
  }
  // 긴 글: 문장마다 끝에 가장 잘 맞는 이모지 하나씩 붙이기
  const EMOJI_END = /\p{Extended_Pictographic}\uFE0F?\s*$/u;
  // 문장 나누기: 마침표·느낌표·물음표·물결, ㅋㅋ·ㅠㅠ 같은 끝맺음, 줄바꿈에서 끊음 (글자는 하나도 빠지지 않음)
  function splitSentences(text) {
    const P = ".!?。！？~", J = "ㅋㅎㅠㅜ";
    const parts = []; let cur = "", i = 0;
    const flush = () => { if (cur) parts.push(cur); cur = ""; };
    while (i < text.length) {
      const c = text[i];
      if (c === "\n") { flush(); let j = i; while (j < text.length && text[j] === "\n") j++; parts.push(text.slice(i, j)); i = j; continue; }
      cur += c; i++;
      if (c === "." && /\d/.test(text[i - 2]) && /\d/.test(text[i])) continue;
      let end = false;
      if (P.includes(c) && !(i < text.length && P.includes(text[i]))) end = true;
      if (J.includes(c) && !(i < text.length && J.includes(text[i]))) {
        let k = cur.length - 1; while (k >= 0 && J.includes(cur[k])) k--;
        if (cur.length - 1 - k >= 2 && (i >= text.length || /\s/.test(text[i]))) end = true;
      }
      if (end) { while (i < text.length && text[i] !== "\n" && /\s/.test(text[i])) cur += text[i++]; flush(); }
    }
    flush();
    return parts;
  }
  function autoDecorate(text) {
    const parts = splitSentences(text);
    let prev = null, out = "";
    for (const part of parts) {
      const m = part.match(/^([\s\S]*?)(\s*)$/);
      const body = m[1], tail = m[2];
      const plain = body.replace(/[.!?。！？~]+$/, "");
      if (!plain.trim() || EMOJI_END.test(plain) || EMOJI_END.test(body) || plain.replace(/\s/g, "").length < 3) { out += part; continue; }
      const recs = recommend(body).filter(e => e !== prev && !body.includes(e));
      if (!recs.length) { out += part; continue; }
      prev = recs[0];
      out += body + recs[0] + tail;
    }
    return out;
  }
  window.repeatsQuestionMark = repeatsQuestionMark;
  window.recommend = recommend;
  window.autoDecorate = autoDecorate;
  window.splitSentences = splitSentences;
})();
