// 이모지 피커 UI + 한국어 검색. content script와 팝업이 함께 씁니다.
(() => {
  if (window.EmojiPicker) return;

  const GROUPS = window.__EMOJI_GROUPS__;
  const RAW = window.__EMOJI_DATA__;

  // [emoji, group, koLabel, koWords, enWords, priorityWords]
  const split = s => (s ? s.toLowerCase().split('|') : []);
  const EMOJIS = RAW.map(([ch, group, label, ko, en, pri], idx) => ({
    ch, group, label, ko: split(ko), en: split(en), pri: split(pri), idx, img: false,
  }));
  const HANGUL = /^[가-힣]+$/;

  // 이 컴퓨터의 글꼴이 그리지 못하는 이모지(□ 상자, 글자 두 개로 쪼개짐)를 찾아 숨김
  const EMOJI_FONT = '"Apple Color Emoji","Segoe UI Emoji","Noto Color Emoji",sans-serif';
  function findUnsupported() {
    const bad = new Set();
    try {
      const S = 24;
      const cv = document.createElement('canvas'); cv.width = cv.height = S;
      const ctx = cv.getContext('2d', { willReadFrequently: true });
      ctx.font = `20px ${EMOJI_FONT}`; ctx.textBaseline = 'top';
      const draw = ch => { ctx.clearRect(0, 0, S, S); ctx.fillText(ch, 0, 0); return ctx.getImageData(0, 0, S, S).data; };
      const tofu = draw('\u{10FFFD}'), tofu2 = draw('\uFFFE');
      const same = (a, b) => { for (let i = 3; i < a.length; i += 16) if (a[i] !== b[i]) return false; return true; };
      const base = ctx.measureText('😀').width;
      for (const e of EMOJIS) {
        if (ctx.measureText(e.ch).width > base * 1.4) { bad.add(e.ch); continue; } // 조합이 깨져 여러 글자로 나옴
        const d = draw(e.ch);
        if (same(d, tofu) || same(d, tofu2)) bad.add(e.ch);
      }
    } catch (_) {}
    return bad;
  }
  const UNSUPPORTED = findUnsupported();
  for (const ch of window.__EMOJI_FORCE_IMG__ || []) UNSUPPORTED.add(ch); // 테스트용
  // 이 PC 글꼴로 못 그리는 이모지는 숨기지 않고 그림(emoji-sprite.webp)으로 대신 보여줌.
  // 넣을 때는 실제 이모지 글자가 들어가므로 받는 사람·사이트에서는 정상 표시됨
  if (UNSUPPORTED.size < EMOJIS.length / 2) { // 감지가 이상하게 동작하면 그대로 둠
    for (const e of EMOJIS) if (UNSUPPORTED.has(e.ch)) e.img = true;
  }
  const BY_CHAR = new Map(EMOJIS.map(e => [e.ch, e]));
  const SPRITE_COLS = 48;
  let spriteUrl = null;
  // 이모지를 요소에 표시 (글꼴로 못 그리면 스프라이트 그림)
  function glyph(el, e, px) {
    if (e && e.img && spriteUrl) {
      const span = document.createElement('span');
      const col = e.idx % SPRITE_COLS, row = Math.floor(e.idx / SPRITE_COLS);
      Object.assign(span.style, {
        display: 'inline-block', width: px + 'px', height: px + 'px', verticalAlign: 'middle',
        backgroundImage: `url("${spriteUrl}")`, backgroundSize: `${SPRITE_COLS * px}px auto`,
        backgroundPosition: `-${col * px}px -${row * px}px`,
      });
      el.textContent = ''; el.appendChild(span);
    } else el.textContent = e ? e.ch : '';
  }

  // 한 단어(term)에 대한 점수. stem이 있으면 "슬퍼"→"슬"처럼 앞부분만으로도 약하게 매칭
  function scoreTerm(e, term, stem) {
    let best = 0;
    for (const w of e.pri) {
      if (w === term) return 120;
      if (w.startsWith(term)) best = Math.max(best, 80);
    }
    for (const w of e.ko) {
      if (w === term) best = Math.max(best, 100);
      else if (w.startsWith(term)) best = Math.max(best, 70);
      else if (w.includes(' ' + term)) best = Math.max(best, 45); // 띄어쓴 단어의 시작만 (기울어가는 ≠ 울어)
    }
    if (best < 70) {
      for (const w of e.en) {
        if (w === term) best = Math.max(best, 90);
        else if (w.startsWith(term)) best = Math.max(best, 60);
        else if (w.includes(' ' + term)) best = Math.max(best, 35);
      }
    }
    if (!best && stem) {
      for (const w of [...e.pri, ...e.ko]) if (w.length > stem.length && w.startsWith(stem)) { best = 20; break; }
    }
    return best;
  }

  // 같은 뜻 묶음: 묶음 안의 어떤 단어로 검색해도 같은 결과가 나옴
  const SYNONYMS = [
    '울음 울다 우는 울어 울고 눈물 슬픔 슬퍼 슬프다 슬픈 슬퍼요 ㅠㅠ ㅜㅜ ㅠ ㅜ 오열 엉엉 훌쩍 서러움 서러워',
    '웃음 웃다 웃는 웃겨 웃김 웃긴 ㅋㅋ ㅋㅋㅋ ㅎㅎ 하하 빵터짐 폭소',
    '화 화남 화나 화난 화났어 분노 짜증 빡침 열받음 열받아',
    '사랑 사랑해 하트 좋아해 러브',
    '좋아 좋아요 최고 굿 따봉 엄지 ㅇㅋ 오케이',
    '감사 감사해 고마워 고맙습니다 땡큐',
    '축하 축하해 생일 파티',
    '놀람 놀라 놀란 깜짝 헐 충격',
    '졸림 졸려 잠 피곤 피곤해 수면',
    '해 태양 햇빛 햇살 해님',
    '비 장마 우산',
    '무서워 무서움 공포 겁',
  ].map(g => g.split(' '));
  const SYN_MAP = new Map();
  for (const g of SYNONYMS) for (const w of g) SYN_MAP.set(w, g);

  function run(alts, useStem) {
    const results = [];
    EMOJIS.forEach((e, i) => {
      let total = 0;
      for (const terms of alts) {          // OR: 여러 대안에 걸리면 점수 합산 → 더 관련 높은 게 위로
        let sum = 0;
        for (const { t, words, stem } of terms) { // AND: 모든 단어가 맞아야 함
          let s = 0;
          let hits = 0;
          for (const w of words) {
            // 묶음에서 딸려 온 한 글자 단어(화, ㅠ)는 정확히 같을 때만 (화 ≠ 화이팅)
            const v = w !== t && w.length === 1
              ? (e.pri.includes(w) || e.ko.includes(w) ? 100 : 0)
              : scoreTerm(e, w, useStem && stem);
            if (v >= 90) hits++;
            s = Math.max(s, v);
          }
          if (hits > 1) s += Math.min(hits - 1, 8) * 6; // 묶음 단어에 많이 맞을수록 더 관련 높음
          if (!s) { sum = 0; break; }
          sum += s;
        }
        total += sum / terms.length;
      }
      if (total) results.push({ e, total, i });
    });
    results.sort((a, b) => b.total - a.total || a.i - b.i);
    return results.map(r => r.e);
  }

  // 공백 = 모두 포함(AND), "/" 또는 "," = 하나라도 포함(OR)
  function search(query) {
    const alts = query.toLowerCase().split(/[\/,|]/).map(s => s.trim()).filter(Boolean)
      .map(s => s.split(/\s+/).map(t => ({
        t,
        words: SYN_MAP.get(t) || [t],
        // 활용형 대응: 2글자 이상 한글이면 마지막 글자를 뗀 어간 (울음→울, 슬퍼→슬, 배고파→배고)
        stem: !SYN_MAP.has(t) && HANGUL.test(t) && t.length >= 2 ? t.slice(0, -1) : null,
      })));
    if (!alts.length) return [];
    const seenAlt = new Set();
    for (let i = alts.length - 1; i >= 0; i--) {
      const key = alts[i].map(x => x.words.join(',')).join(' ');
      if (seenAlt.has(key)) alts.splice(i, 1); else seenAlt.add(key);
    }
    const exact = run(alts, false);
    if (exact.length >= 1) return exact; // 정확히 맞는 게 하나라도 있으면 느슨한 검색은 하지 않음 (피자 → 피… 방지)
    const seen = new Set(exact);
    return exact.concat(run(alts, true).filter(e => !seen.has(e)));
  }

  const CSS = `
  :host { all: initial; }
  .picker {
    --bg: #ffffff; --fg: #1f2328; --muted: #6b7280; --line: #e5e7eb; --hover: #f1f3f5; --accent: #3b82f6; --input: #f6f7f9;
    width: 352px; height: 400px; display: flex; flex-direction: column;
    background: var(--bg); color: var(--fg); border: 1px solid var(--line); border-radius: 12px;
    box-shadow: 0 12px 32px rgba(0,0,0,.18), 0 2px 6px rgba(0,0,0,.08);
    font: 13px/1.4 -apple-system, BlinkMacSystemFont, "Segoe UI", "Apple SD Gothic Neo", "Malgun Gothic", sans-serif;
    overflow: hidden; box-sizing: border-box;
  }
  .picker.embedded { box-shadow: none; border: 0; border-radius: 0; width: 100%; height: 100%; }
  @media (prefers-color-scheme: dark) {
    .picker { --bg: #1e1f22; --fg: #e6e6e6; --muted: #9aa0a6; --line: #33353a; --hover: #2b2d31; --input: #2b2d31; }
  }
  .top { padding: 10px 10px 6px; }
  input {
    width: 100%; box-sizing: border-box; padding: 8px 10px; border: 1px solid var(--line); border-radius: 8px;
    background: var(--input); color: var(--fg); font: inherit; font-size: 14px; outline: none;
  }
  input:focus { border-color: var(--accent); }
  .tabs { display: flex; gap: 2px; padding: 0 8px 6px; border-bottom: 1px solid var(--line); }
  .tab { flex: 1; border: 0; background: none; border-radius: 6px; padding: 4px 0; cursor: pointer; font-size: 17px; line-height: 1; opacity: .55; filter: grayscale(.6); }
  .tab:hover { background: var(--hover); opacity: 1; filter: none; }
  .tab.on { opacity: 1; filter: none; box-shadow: inset 0 -2px 0 var(--accent); }
  .body { flex: 1; overflow-y: auto; padding: 4px 8px 8px; overscroll-behavior: contain; }
  .sec { font-size: 12px; color: var(--muted); padding: 8px 4px 4px; position: sticky; top: -4px; background: var(--bg); }
  .grid { display: grid; grid-template-columns: repeat(8, 1fr); }
  .e {
    border: 0; background: none; cursor: pointer; border-radius: 8px; aspect-ratio: 1; padding: 0;
    font-size: 24px; line-height: 1; font-family: "Apple Color Emoji","Segoe UI Emoji","Noto Color Emoji",sans-serif;
  }
  .e:hover, .e.sel { background: var(--hover); }
  .e.sel { outline: 2px solid var(--accent); outline-offset: -2px; }
  .empty { color: var(--muted); text-align: center; padding: 40px 0; }
  .foot { display: flex; align-items: center; gap: 8px; padding: 6px 12px; border-top: 1px solid var(--line); min-height: 34px; color: var(--muted); font-size: 12px; }
  .foot .big { font-size: 22px; font-family: "Apple Color Emoji","Segoe UI Emoji","Noto Color Emoji",sans-serif; }
  .foot .name { color: var(--fg); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  `;

  /**
   * opts.onPick(emoji, keepOpen)  선택 시 호출
   * opts.recent / opts.saveRecent(list)
   * opts.hint  하단 기본 안내 문구
   * opts.embedded  팝업 안에 꽉 채워 표시
   */
  function create(opts) {
    if (opts.spriteUrl) spriteUrl = opts.spriteUrl;
    const host = document.createElement('div');
    const root = host.attachShadow({ mode: 'open' });
    root.innerHTML = `<style>${CSS}</style>
      <div class="picker ${opts.embedded ? 'embedded' : ''}">
        <div class="top"><input type="text" placeholder="이모지 검색 (예: 해, 눈물/울음, 하트)" spellcheck="false" autocomplete="off"></div>
        <div class="tabs"></div>
        <div class="body"></div>
        <div class="foot"></div>
      </div>`;
    const $ = s => root.querySelector(s);
    const input = $('input'), body = $('.body'), tabs = $('.tabs'), foot = $('.foot');
    let recent = (opts.recent || []).filter(c => BY_CHAR.has(c));
    let buttons = [], selIdx = -1;

    // 페이지 단축키가 피커 입력을 가로채지 않게
    for (const t of ['keydown', 'keyup', 'keypress', 'input', 'paste', 'copy', 'cut', 'mousedown', 'mouseup', 'click', 'contextmenu', 'wheel'])
      root.addEventListener(t, ev => ev.stopPropagation());

    const setFoot = e => {
      foot.innerHTML = '';
      if (e) {
        const big = document.createElement('span'); big.className = 'big'; glyph(big, e, 24);
        const name = document.createElement('span'); name.className = 'name'; name.textContent = e.label;
        foot.append(big, name);
      } else foot.textContent = opts.hint || '';
    };

    function makeGrid(list) {
      const grid = document.createElement('div'); grid.className = 'grid';
      for (const e of list) {
        const b = document.createElement('button');
        b.className = 'e'; glyph(b, e, 26); b.title = e.label; b._e = e;
        grid.appendChild(b); buttons.push(b);
      }
      return grid;
    }
    function section(title, list, id) {
      const h = document.createElement('div'); h.className = 'sec'; h.textContent = title;
      if (id !== undefined) h.dataset.g = id;
      body.append(h, makeGrid(list));
    }

    function render() {
      const q = input.value.trim();
      body.innerHTML = ''; buttons = []; selIdx = -1;
      if (q) {
        const res = search(q);
        if (res.length) section(`"${q}" 검색 결과 ${res.length}개`, res);
        else body.innerHTML = '<div class="empty">찾는 이모지가 없어요 🥲</div>';
        if (res.length) select(0);
      } else {
        if (recent.length) section('최근 사용', recent.map(c => BY_CHAR.get(c)), 'r');
        for (const g of GROUPS) { const l = EMOJIS.filter(e => e.group === g.id); if (l.length) section(g.name, l, g.id); }
        body.scrollTop = 0;
        updateTabs();
      }
      tabs.style.display = q ? 'none' : '';
      setFoot(null);
    }

    function select(i) {
      if (buttons[selIdx]) buttons[selIdx].classList.remove('sel');
      selIdx = Math.max(0, Math.min(buttons.length - 1, i));
      const b = buttons[selIdx]; if (!b) return;
      b.classList.add('sel'); b.scrollIntoView({ block: 'nearest' }); setFoot(b._e);
    }

    function pick(e, keepOpen) {
      recent = [e.ch, ...recent.filter(c => c !== e.ch)].slice(0, 24);
      opts.saveRecent && opts.saveRecent(recent);
      opts.onPick(e.ch, keepOpen);
    }

    // 탭
    const tabDefs = [{ id: 'r', icon: '🕘', name: '최근 사용' }, ...GROUPS];
    for (const g of tabDefs) {
      if (g.id !== 'r' && !EMOJIS.some(e => e.group === g.id)) continue;
      const t = document.createElement('button');
      t.className = 'tab'; t.title = g.name;
      const ge = BY_CHAR.get(g.icon); if (ge) glyph(t, ge, 18); else t.textContent = g.icon;
      t.onclick = () => {
        const h = body.querySelector(`.sec[data-g="${g.id}"]`);
        if (h) body.scrollTop = h.offsetTop - body.offsetTop;
      };
      tabs.appendChild(t); t._g = g.id;
    }
    function updateTabs() {
      let cur = null;
      for (const h of body.querySelectorAll('.sec[data-g]')) if (h.offsetTop - body.offsetTop <= body.scrollTop + 8) cur = h.dataset.g;
      for (const t of tabs.children) t.classList.toggle('on', String(t._g) === cur);
    }
    body.addEventListener('scroll', updateTabs);

    body.addEventListener('click', ev => {
      const b = ev.target.closest('.e'); if (b) pick(b._e, ev.shiftKey || ev.ctrlKey || ev.metaKey);
    });
    body.addEventListener('mouseover', ev => { const b = ev.target.closest('.e'); if (b) setFoot(b._e); });

    let timer;
    input.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(render, 60); });
    input.addEventListener('keydown', ev => {
      if (ev.isComposing) return;
      const cols = 8;
      if (ev.key === 'ArrowDown') { ev.preventDefault(); select(selIdx < 0 ? 0 : selIdx + cols); }
      else if (ev.key === 'ArrowUp') { ev.preventDefault(); select(selIdx - cols); }
      else if (ev.key === 'ArrowRight' && selIdx >= 0 && input.selectionStart === input.value.length) { ev.preventDefault(); select(selIdx + 1); }
      else if (ev.key === 'ArrowLeft' && selIdx > 0 && input.selectionStart === input.value.length) { ev.preventDefault(); select(selIdx - 1); }
      else if (ev.key === 'Enter') {
        ev.preventDefault();
        clearTimeout(timer); if (selIdx < 0) render();
        const b = buttons[selIdx] || buttons[0]; if (b) pick(b._e, ev.shiftKey);
      } else if (ev.key === 'Escape') { ev.preventDefault(); opts.onClose && opts.onClose(); }
    });

    render();
    return {
      host,
      focus: () => input.focus({ preventScroll: true }),
      reset: () => {
        if (opts.getRecent) recent = opts.getRecent().filter(c => BY_CHAR.has(c));
        input.value = ''; render();
      },
      contains: node => node === host || host.contains(node),
    };
  }

  window.EmojiPicker = { create, search };
})();
