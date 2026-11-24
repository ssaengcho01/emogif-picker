// 휴대폰용 이모지 피커 (웹앱)
(() => {
  const $ = id => document.getElementById(id);
  const GROUPS = window.__EMOJI_GROUPS__;
  const RAW = window.__EMOJI_DATA__;
  const store = {
    get(k, d) { try { const v = localStorage.getItem(k); return v == null ? d : JSON.parse(v); } catch (_) { return d; } },
    set(k, v) { try { localStorage.setItem(k, JSON.stringify(v)); } catch (_) {} },
  };

  // ---------- 특수문자 (PC 버전과 같은 목록) ----------
  const VS15 = '︎';
  const SPECIALS = [
    ['느낌표·물음표', ['.ᐟ|느낌표', 'ᐟ|느낌표', 'ꜝ|느낌표', '‼' + VS15 + '|느낌표', '¡|느낌표', 'ˀ̣|물음표 궁금', 'ˀ|물음표 궁금', '¿|물음표 궁금', '⸮|물음표 궁금']],
    ['하트', ['♡|하트 사랑', '♥' + VS15 + '|하트 사랑', '❤' + VS15 + '|하트 사랑', '❥|하트 사랑', '❣' + VS15 + '|하트 사랑', 'ღ|하트 사랑', '❦|하트 꽃']],
    ['별', ['★|별', '☆|별', '✦|별 반짝', '✧|별 반짝', '⋆|별', '✩|별', '✪|별', '✫|별', '✬|별', '✭|별', '✮|별', '✯|별', '✶|별', '✷|별', '✸|별', '✹|별', '✺|별', '⭑|별', '⭒|별', '˚|반짝', '⁺|반짝', '₊|반짝']],
    ['꽃', ['✿|꽃', '❀|꽃', '❁|꽃', '✾|꽃', '❃|꽃', '❋|꽃', '✽|꽃', '❊|꽃', '✻|꽃', '✼|꽃', '✤|꽃', '✥|꽃', 'ꕤ|꽃', 'ꕥ|꽃', '⚘|꽃']],
    ['얼굴', ['◡̈|얼굴 웃음 스마일', 'ᵕ̈|얼굴 웃음 스마일', '☺' + VS15 + '|얼굴 웃음 스마일', '☻|얼굴 웃음 스마일', '☹' + VS15 + '|얼굴 슬픔', '⌒̈|얼굴 슬픔 우는', '◠̈|얼굴 슬픔 우는', '◠̋|얼굴 슬픔 우는 눈물', '㋡|얼굴 웃음 스마일', 'ツ|얼굴 웃음 스마일', 'ッ|얼굴 웃음 스마일']],
    ['나비·곰돌이·리본', ['ʚɞ|나비', 'ʚĭɞ|나비', 'ʚ♡ɞ|나비 하트', 'ʕ•ᴥ•ʔ|곰 곰돌이', 'ʕ·ᴥ·ʔ|곰 곰돌이', 'ʕᵔᴥᵔʔ|곰 곰돌이 웃음', 'ᴥ|곰 곰돌이', '୨୧|리본', '୨♡୧|리본 하트', '୨ৎ|리본', '𐙚|리본', 'ᡣ𐭩|리본', '୨⎯୧|리본', '꒰ა໒꒱|리본 괄호', '꒰ა|리본 괄호', '໒꒱|리본 괄호', 'ꔛ|리본']],
    ['까만 원 숫자', '❶❷❸❹❺❻❼❽❾❿⓫⓬⓭⓮⓯⓰⓱⓲⓳⓴'.split('').map((c, i) => c + '|숫자 번호 ' + (i + 1))],
    ['음표', ['♩|음표 음악 노래', '♪|음표 음악 노래', '♫|음표 음악 노래', '♬|음표 음악 노래', '♭|음표 음악', '♯|음표 음악', '𝄞|음표 음악 노래']],
  ].map(([name, items]) => ({ name, items: items.map(s => { const [ch, words] = s.split('|'); return { ch, words: words.split(' ') }; }) }));
  const isSym = ch => SPECIALS.some(g => g.items.some(i => i.ch === ch));

  // ---------- 위쪽 글쓰기 칸 ----------
  const msg = $('msg');
  const graphemes = s => {
    try { return [...new Intl.Segmenter('ko', { granularity: 'grapheme' }).segment(s)].map(x => x.segment); } catch (_) { return [...s]; }
  };
  let caret = 0;
  const saveCaret = () => { caret = msg.selectionStart ?? msg.value.length; };
  ['keyup', 'click', 'select', 'input', 'blur'].forEach(ev => msg.addEventListener(ev, saveCaret));
  function grow() { msg.style.height = 'auto'; msg.style.height = Math.min(Math.max(msg.scrollHeight, 96), 220) + 'px'; }
  async function copyText(t) {
    try { await navigator.clipboard.writeText(t); return true; } catch (_) {}
    try {
      const ta = document.createElement('textarea');
      ta.value = t; ta.setAttribute('readonly', ''); ta.style.position = 'fixed'; ta.style.opacity = '0';
      document.body.appendChild(ta); ta.select(); ta.setSelectionRange(0, t.length);
      const ok = document.execCommand('copy'); ta.remove(); return ok;
    } catch (_) { return false; }
  }
  let toastTimer;
  function toast(m) {
    const t = $('toast'); t.textContent = m; t.classList.add('on');
    clearTimeout(toastTimer); toastTimer = setTimeout(() => t.classList.remove('on'), 1600);
  }
  // 이모지를 누르면 글쓰기 칸의 커서 자리에 넣음 (자판은 다시 띄우지 않음)
  function pick(ch) {
    const v = msg.value, at = Math.min(caret, v.length);
    msg.value = v.slice(0, at) + ch + v.slice(at);
    caret = at + ch.length;
    grow();
    copyText(msg.value);
    let rec = store.get('recent', []).filter(x => x !== ch);
    rec.unshift(ch); store.set('recent', rec.slice(0, 32));
    if (navigator.vibrate) navigator.vibrate(8);
  }
  msg.addEventListener('input', () => {
    grow();
    clearTimeout(recTimer);
    recTimer = setTimeout(() => { if (mode === 'emoji' && !$('q').value.trim()) openTab(curTab, true); }, 250);
  });
  let recTimer;
  let beforeAuto = null, afterAuto = null;
  const autoBtn = $('auto');
  function resetAuto() { if (beforeAuto !== null && msg.value !== afterAuto) { beforeAuto = afterAuto = null; autoBtn.textContent = '✨ 자동으로 붙이기'; } }
  msg.addEventListener('input', resetAuto);
  autoBtn.onclick = async () => {
    if (beforeAuto !== null && msg.value === afterAuto) { // 한 번 더 누르면 되돌리기
      msg.value = beforeAuto; beforeAuto = afterAuto = null; autoBtn.textContent = '✨ 자동으로 붙이기'; grow(); openTab(curTab, true); return;
    }
    if (!msg.value.trim()) { toast('먼저 보낼 말을 써 주세요'); return; }
    let out = null;
    if (store.get('geminiKey', '')) {
      autoBtn.disabled = true; autoBtn.textContent = '✨ AI가 고르는 중…';
      try { out = await aiDecorate(msg.value); }
      catch (e) { toast(e.message + ' · 기본 사전으로 붙였어요'); }
      autoBtn.disabled = false; autoBtn.textContent = '✨ 자동으로 붙이기';
    }
    if (!out || out === msg.value) out = window.autoDecorate(msg.value);
    if (out === msg.value) { toast(store.get('geminiKey', '') ? '붙일 만한 이모지를 찾지 못했어요' : '사전에서 못 찾았어요 · ⚙에서 Gemini 키를 넣으면 더 잘 찾아요'); return; }
    beforeAuto = msg.value; afterAuto = out;
    msg.value = out; caret = out.length; grow(); copyText(out);
    autoBtn.textContent = '↩ 되돌리기';
    openTab(curTab, true);
  };
  // ---------- Gemini (무료 키, 선택) : 문장마다 끝에 붙일 이모지 하나 ----------
  const EMOJI_END = /\p{Extended_Pictographic}\uFE0F?\s*$/u;
  async function aiDecorate(text) {
    const parts = window.splitSentences(text), want = [];
    parts.forEach((part, i) => {
      const body = part.replace(/\s*$/, ''), plain = body.replace(/[.!?。！？~]+$/, '');
      if (plain.trim() && !EMOJI_END.test(body) && plain.replace(/\s/g, '').length >= 2) want.push(i);
    });
    if (!want.length) return text;
    const list = want.map((i, n) => (n + 1) + '. ' + parts[i].trim()).join('\n');
    const prompt = '카카오톡 메시지의 문장들이야. 각 문장 끝에 붙이기 가장 잘 어울리는 이모지를 하나씩 골라 줘. ' +
      '문장 수와 같은 ' + want.length + '개의 이모지를 순서대로 JSON 문자열 배열로만 답해. 예: ["😊","🙏"]\n' + list;
    const ctl = new AbortController(); const tm = setTimeout(() => ctl.abort(), 12000);
    let r;
    try {
      r = await fetch('https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-lite-latest:generateContent', {
        method: 'POST', signal: ctl.signal,
        headers: { 'Content-Type': 'application/json', 'x-goog-api-key': store.get('geminiKey', '') },
        body: JSON.stringify({ contents: [{ parts: [{ text: prompt }] }], generationConfig: { maxOutputTokens: 256, responseMimeType: 'application/json' } })
      });
    } catch (_) { throw new Error(navigator.onLine ? 'AI 응답이 늦어요' : '인터넷이 연결되어 있지 않아요'); }
    finally { clearTimeout(tm); }
    if (r.status === 400 || r.status === 401 || r.status === 403) throw new Error('Gemini 키가 맞지 않아요');
    if (r.status === 429) throw new Error('무료 사용 한도를 잠시 넘었어요');
    if (!r.ok) throw new Error('AI 서버 오류 (' + r.status + ')');
    const j = await r.json();
    let arr = [];
    try { arr = JSON.parse(j.candidates[0].content.parts.map(p => p.text).join('')); } catch (_) { throw new Error('AI 답을 이해하지 못했어요'); }
    let prev = null;
    want.forEach((i, n) => {
      const e = String(arr[n] || '').trim();
      if (!e || !/\p{Extended_Pictographic}/u.test(e) || e.length > 16 || e === prev || window.repeatsQuestionMark(parts[i], e)) return;
      const m = parts[i].match(/^([\s\S]*?)(\s*)$/);
      parts[i] = m[1] + e + m[2]; prev = e;
    });
    return parts.join('');
  }
  function aiSettings() {
    const s = plainSheet('AI 추천 설정 (선택)');
    const p = document.createElement('div'); p.className = 'tip';
    p.innerHTML = '키가 없어도 기본 사전으로 동작해요. Google Gemini 무료 키를 넣으면 "자동으로 붙이기"를 AI가 골라요. 요금은 나가지 않아요.<br>1) 아래 버튼으로 키 만드는 곳 열기 → "API 키 만들기"<br>2) 만든 키를 복사해서 아래 칸에 붙여넣기';
    s.appendChild(p);
    const open = document.createElement('a'); open.className = 'btn'; open.textContent = '키 만드는 곳 열기 (Google AI Studio)';
    open.href = 'https://aistudio.google.com/apikey'; open.target = '_blank'; open.rel = 'noopener'; open.style.textAlign = 'center'; open.style.textDecoration = 'none';
    s.appendChild(open);
    const inp = document.createElement('input'); inp.type = 'text'; inp.placeholder = 'AIza로 시작하는 키'; inp.value = store.get('geminiKey', ''); inp.autocomplete = 'off'; inp.spellcheck = false;
    s.appendChild(inp);
    sheetBtn(s, '저장', 'primary', () => {
      const v = inp.value.trim();
      if (v) store.set('geminiKey', v); else { try { localStorage.removeItem('geminiKey'); } catch (_) {} }
      closeSheet(); toast(v ? 'Gemini 키를 저장했어요' : 'AI 추천을 껐어요');
    });
    if (store.get('geminiKey', '')) sheetBtn(s, '키 지우기 (AI 끄기)', '', () => { try { localStorage.removeItem('geminiKey'); } catch (_) {} closeSheet(); toast('AI 추천을 껐어요'); });
    sheetBtn(s, '닫기', '', closeSheet);
    const n2 = document.createElement('div'); n2.className = 'tip'; n2.textContent = '키는 이 휴대폰 안에만 저장돼요.'; s.appendChild(n2);
  }
  $('clear').onclick = () => { msg.value = ''; resetAuto(); caret = 0; grow(); if (mode === 'emoji') openTab(curTab, true); };
  $('copy').onclick = async () => {
    if (!msg.value) { toast('먼저 보낼 말을 써 주세요'); return; }
    toast(await copyText(msg.value) ? '복사했어요. 카톡에서 길게 눌러 붙여넣기' : '복사하지 못했어요');
  };
  $('sendText').onclick = async () => {
    if (!msg.value) { toast('먼저 보낼 말을 써 주세요'); return; }
    if (navigator.share) { try { await navigator.share({ text: msg.value }); return; } catch (e) { if (e.name === 'AbortError') return; } }
    toast(await copyText(msg.value) ? '복사했어요. 카톡에서 붙여넣기 해 주세요' : '복사하지 못했어요');
  };

  // ---------- 이모지 목록 ----------
  const BY_GROUP = new Map();
  for (const r of RAW) { if (!BY_GROUP.has(r[1])) BY_GROUP.set(r[1], []); BY_GROUP.get(r[1]).push(r[0]); }

  function grid(chars) {
    const g = document.createElement('div'); g.className = 'grid';
    for (const ch of chars) {
      const b = document.createElement('button');
      b.className = 'e' + (isSym(ch) ? ' sym' + (graphemes(ch.replace(VS15, '')).length >= 3 ? ' long' : '') : '');
      b.textContent = ch;
      b.onclick = () => pick(ch);
      g.appendChild(b);
    }
    return g;
  }
  function sec(title) { const d = document.createElement('div'); d.className = 'sec'; d.textContent = title; return d; }

  let mode = 'emoji', curTab = 'recent';
  const body = $('body');
  const TABS = [{ id: 'recent', label: '🕘' }, ...GROUPS.map(g => ({ id: 'g' + g.id, label: g.icon, gid: g.id })),
    { id: 'sym', label: '★', txt: true }, { id: 'my', label: 'MY', txt: true }];

  function renderTabs() {
    const t = $('tabs'); t.innerHTML = '';
    for (const tab of TABS) {
      const b = document.createElement('button');
      b.className = 'tab' + (tab.txt ? ' txt' : '') + (tab.id === curTab ? ' on' : '');
      b.textContent = tab.label;
      b.onclick = () => openTab(tab.id);
      t.appendChild(b);
    }
    const g = document.createElement('button'); g.className = 'tab gear'; g.innerHTML = '<svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/></svg>'; g.setAttribute('aria-label', 'AI 추천 설정');
    g.onclick = aiSettings; t.appendChild(g);
  }

  function recBlock() {
    const list = window.recommend(msg.value);
    if (!list.length) return null;
    const box = document.createElement('div'); box.className = 'rec';
    box.appendChild(sec('✨ 쓴 말에 어울리는 이모지'));
    box.appendChild(grid(list.slice(0, 16)));
    return box;
  }

  function openTab(id, keepScroll) {
    curTab = id;
    renderTabs();
    const q = $('q');
    if (id === 'my') { mode = 'my'; q.placeholder = '이모지 검색'; q.value = ''; showMine(); return; }
    mode = 'emoji'; q.placeholder = '검색 (예: 웃음, 하트, 축하)';
    $('hint').textContent = '쓴 말에 맞춰 추천 · 누르면 글에 들어가요';
    body.innerHTML = '';
    if (q.value.trim()) { showSearch(q.value.trim()); return; }
    const rb = recBlock();
    if (rb) body.appendChild(rb);
    if (id === 'recent') {
      const rec = store.get('recent', []);
      body.appendChild(sec('최근 사용'));
      if (rec.length) body.appendChild(grid(rec));
      else { const e = document.createElement('div'); e.className = 'empty'; e.textContent = '아직 쓴 이모지가 없어요. 위 검색창에 찾고 싶은 말을 써 보세요.'; body.appendChild(e); }
      body.appendChild(sec(GROUPS[0].name));
      body.appendChild(grid(BY_GROUP.get(GROUPS[0].id) || []));
    } else if (id === 'sym') {
      for (const g of SPECIALS) { body.appendChild(sec(g.name)); body.appendChild(grid(g.items.map(i => i.ch))); }
    } else {
      const g = TABS.find(t => t.id === id);
      body.appendChild(sec(GROUPS.find(x => x.id === g.gid).name));
      body.appendChild(grid(BY_GROUP.get(g.gid) || []));
    }
    if (!keepScroll) body.scrollTop = 0;
  }

  function showSearch(q) {
    body.innerHTML = '';
    const res = window.EmojiPicker.search(q).map(e => e.ch);
    const terms = q.split(/\s+/);
    const sym = [];
    for (const g of SPECIALS) for (const i of g.items) if (terms.every(t => i.words.some(w => w.startsWith(t)))) sym.push(i.ch);
    if (!res.length && !sym.length) { const e = document.createElement('div'); e.className = 'empty'; e.textContent = '찾는 이모지가 없어요'; body.appendChild(e); return; }
    if (res.length) { body.appendChild(sec('"' + q + '" 이모지')); body.appendChild(grid(res)); }
    if (sym.length) { body.appendChild(sec('특수문자')); body.appendChild(grid(sym)); }
    body.scrollTop = 0;
  }

  let qTimer;
  $('q').addEventListener('input', () => {
    clearTimeout(qTimer);
    const v = $('q').value.trim();
    if (mode === 'my') { mode = 'emoji'; curTab = 'recent'; renderTabs(); }
    qTimer = setTimeout(() => (v ? showSearch(v) : openTab(curTab)), 120);
  });
  $('q').addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); $('q').blur(); } });

  // ---------- 보내기 창 (MY) ----------
  let onSheetClose = null;
  function closeSheet() { $('sheet').classList.remove('on'); const f = onSheetClose; onSheetClose = null; if (f) f(); }
  $('sheet').onclick = e => { if (e.target === $('sheet')) closeSheet(); };
  function openSheet(file, previewUrl, extra) {
    const s = $('sheetIn'); s.innerHTML = '';
    const img = document.createElement('img'); img.src = previewUrl; s.appendChild(img);
    const send = document.createElement('button'); send.className = 'btn kakao'; send.textContent = '카톡으로 보내기';
    send.onclick = async () => {
      if (navigator.canShare && navigator.canShare({ files: [file] })) {
        try { await navigator.share({ files: [file] }); closeSheet(); } catch (e) { if (e.name !== 'AbortError') toast('보내지 못했어요'); }
      } else save();
    };
    const save = () => {
      const a = document.createElement('a'); a.href = URL.createObjectURL(file); a.download = file.name;
      document.body.appendChild(a); a.click(); a.remove();
      toast('사진첩(다운로드)에 저장했어요');
    };
    const saveBtn = document.createElement('button'); saveBtn.className = 'btn'; saveBtn.textContent = '휴대폰에 저장'; saveBtn.onclick = save;
    s.appendChild(send); s.appendChild(saveBtn);
    if (extra) s.appendChild(extra);
    const cancel = document.createElement('button'); cancel.className = 'btn'; cancel.textContent = '닫기'; cancel.onclick = closeSheet;
    s.appendChild(cancel);
    $('sheet').classList.add('on');
  }

  // ---------- MY 이모티콘 (휴대폰 안에만 저장) ----------
  const DB = new Promise((ok, no) => {
    const r = indexedDB.open('emoji-my', 1);
    r.onupgradeneeded = () => r.result.createObjectStore('stickers', { keyPath: 'id' });
    r.onsuccess = () => ok(r.result); r.onerror = () => no(r.error);
  });
  async function tx(mode2, fn) {
    const db = await DB;
    return new Promise((ok, no) => { const t = db.transaction('stickers', mode2); const st = t.objectStore('stickers'); const res = fn(st); t.oncomplete = () => ok(res && res.result); t.onerror = () => no(t.error); });
  }
  const allMine = () => tx('readonly', st => st.getAll());

  // 내 세트: 기록마다 set(세트 이름)·ord(순서)를 둠. set이 없으면 '내 이모티콘'
  function myTile(s, list) {
    const b = document.createElement('button'); b.className = 'pic';
    const img = document.createElement('img'); img.src = URL.createObjectURL(s.png); b.appendChild(img);
    b.onclick = () => {
      const send = s.png; // 카톡은 PNG·GIF 모두 채팅창에선 흰 바탕, 누르면 투명 → 화질 좋은 PNG 그대로
      const ext = send.type === 'image/gif' ? '.gif' : send.type === 'image/jpeg' ? '.jpg' : '.png';
      const del = document.createElement('button'); del.className = 'btn'; del.textContent = '삭제'; del.style.color = '#d33';
      del.onclick = async () => { await tx('readwrite', st => st.delete(s.id)); closeSheet(); showMine(); };
      openSheet(new File([send], 'my-' + s.id + ext, { type: send.type }), img.src, del);
    };
    return b;
  }
  function addTile(html, fn) {
    const b = document.createElement('button'); b.className = 'pic add'; b.innerHTML = html; b.onclick = fn; return b;
  }
  async function showMine() {
    $('hint').textContent = '누르면 카톡으로 보내기';
    let list = [];
    try { list = (await allMine()) || []; } catch (_) {}
    if (mode !== 'my') return;
    const loose = list.filter(s => !s.set).sort((a, b) => b.id - a.id);
    const sets = new Map();
    for (const s of list.filter(s => s.set).sort((a, b) => (a.ord - b.ord) || (a.id - b.id))) {
      if (!sets.has(s.set)) sets.set(s.set, []);
      sets.get(s.set).push(s);
    }
    const setNames = [...sets.keys()].sort((x, y) => Math.min(...sets.get(x).map(s => s.made || s.id)) - Math.min(...sets.get(y).map(s => s.made || s.id)));
    knownSets = new Set(sets.keys());
    body.innerHTML = '';
    body.appendChild(sec(loose.length ? '내 이모티콘 ' + loose.length : '내 이모티콘 · 사진이나 그림을 등록해 보세요'));
    const g = document.createElement('div'); g.className = 'pics four';
    g.appendChild(addTile('<b>＋</b>추가', () => { pendingSet = null; $('file').click(); }));
    g.appendChild(addTile('<b>＋</b>세트 추가', newSetSheet));
    for (const s of loose) g.appendChild(myTile(s));
    body.appendChild(g);
    for (const name of setNames) {
      const h = sec(name + ' ' + sets.get(name).length);
      h.classList.add('sethead');
      const more = document.createElement('button'); more.className = 'more'; more.textContent = '⋯'; more.setAttribute('aria-label', name + ' 세트 메뉴');
      more.onclick = () => setMenu(name);
      h.appendChild(more);
      body.appendChild(h);
      const sg = document.createElement('div'); sg.className = 'pics four';
      for (const s of sets.get(name)) sg.appendChild(myTile(s));
      body.appendChild(sg);
    }
    // 기본으로 들어 있는 세트 (세트마다 소제목)
    for (const set of window.__MY_SETS__ || []) {
      const sh = sec(set.name + ' ' + set.files.length);
      if (set.credit) { const c = document.createElement('span'); c.className = 'credit'; c.textContent = set.credit; sh.appendChild(c); }
      body.appendChild(sh);
      const sg = document.createElement('div'); sg.className = 'pics four';
      set.files.forEach((f, i) => {
        const b = document.createElement('button'); b.className = 'pic';
        const img = document.createElement('img'); img.src = f.src; img.alt = f.title; img.loading = 'lazy'; b.appendChild(img);
        b.onclick = () => sendSetSticker(f, i);
        sg.appendChild(b);
      });
      body.appendChild(sg);
    }
  }

  // ---------- 세트 추가·고치기 ----------
  var pendingSet = null;
  var knownSets = new Set();
  function plainSheet(title) {
    const s = $('sheetIn'); s.innerHTML = '';
    const t = document.createElement('div'); t.className = 'sheettitle'; t.textContent = title; s.appendChild(t);
    $('sheet').classList.add('on');
    return s;
  }
  function sheetBtn(s, text, cls, fn) {
    const b = document.createElement('button'); b.className = 'btn' + (cls ? ' ' + cls : ''); b.textContent = text; b.onclick = fn; s.appendChild(b); return b;
  }
  async function setNamesNow() { return new Set(((await allMine()) || []).filter(s => s.set).map(s => s.set)); }
  function nameSheet(title, value, okText, done) {
    const s = plainSheet(title);
    const inp = document.createElement('input'); inp.type = 'text'; inp.value = value || ''; inp.placeholder = '예: 우리 강아지'; inp.maxLength = 30;
    s.appendChild(inp);
    sheetBtn(s, okText, 'primary', () => {
      const v = inp.value.trim().replace(/\s+/g, ' ');
      if (!v) { toast('세트 이름을 써 주세요'); inp.focus(); return; }
      done(v);
    });
    sheetBtn(s, '닫기', '', closeSheet);
    setTimeout(() => inp.focus(), 50);
  }
  function newSetSheet() {
    // 사진 고르기 창은 누른 바로 그 순간에 열어야 아이폰에서도 막히지 않아요 (기다리는 작업 없이)
    nameSheet('새 세트 이름', '', '사진 고르기', v => {
      const have = knownSets;
      let name = v, n = 2;
      while (have.has(name) || name === '내 이모티콘') name = v + ' (' + n++ + ')';
      pendingSet = name; closeSheet(); $('file').click();
    });
  }
  function setMenu(name) {
    const s = plainSheet(name);
    sheetBtn(s, '사진 더 넣기', '', () => { pendingSet = name; closeSheet(); $('file').click(); });
    sheetBtn(s, '이름 바꾸기', '', () => nameSheet('세트 이름 바꾸기', name, '바꾸기', async v => {
      if (v !== name && (await setNamesNow()).has(v)) { toast('같은 이름의 세트가 있어요'); return; }
      const items = ((await allMine()) || []).filter(x => x.set === name);
      await tx('readwrite', st => { for (const x of items) { x.set = v; st.put(x); } });
      closeSheet(); showMine();
    }));
    sheetBtn(s, '세트 지우기', '', () => {
      const c = plainSheet('"' + name + '" 세트를 지울까요? 안의 이모티콘도 모두 지워져요.');
      sheetBtn(c, '지우기', 'danger', async () => {
        const items = ((await allMine()) || []).filter(x => x.set === name);
        await tx('readwrite', st => { for (const x of items) st.delete(x.id); });
        closeSheet(); showMine();
      });
      sheetBtn(c, '닫기', '', closeSheet);
    });
    sheetBtn(s, '닫기', '', closeSheet);
  }
  // 세트 이모티콘 보내기: 카톡은 PNG의 투명한 곳을 흰색으로 바꾸므로 투명 GIF로 바꿔서 보냄
  async function sendSetSticker(f, i) {
    try {
      const blob = await (await fetch(f.src)).blob();
      openSheet(new File([blob], f.title + '.png', { type: 'image/png' }), f.src);
    } catch (_) { toast('이모티콘을 열지 못했어요'); }
  }
  $('file').onchange = async () => {
    const files = [...$('file').files].sort((x, y) => x.name.localeCompare(y.name, 'ko', { numeric: true })); $('file').value = '';
    const set = pendingSet; pendingSet = null; 
    if (!files.length) return;
    let ord = 0, made = Date.now();
    if (set) {
      const old = ((await allMine()) || []).filter(x => x.set === set);
      ord = old.reduce((m, x) => Math.max(m, x.ord || 0), 0);
      if (old.length) made = Math.min(...old.map(x => x.made || x.id));
    }
    let ok = 0, keepRest = null, tried = 0;
    for (let n = 0; n < files.length; n++) {
      const f = files[n];
      try {
        let opt = null;
        const anim = f.type === 'image/gif';
        if (!anim && !keepRest) {
          opt = await editPhoto(f, n, files.length);
          if (opt === null) break;          // 닫기: 남은 사진은 넣지 않음
          if (opt.rest) keepRest = {};
        } else if (keepRest) opt = keepRest;   // 나머지: 자르지 않고 같은 배경 설정만
        tried++;
        const rec = await makeSticker(f, opt);
        rec.id = Date.now() * 100 + ok;
        if (set) { rec.set = set; rec.ord = ++ord; rec.made = made; }
        await tx('readwrite', st => st.put(rec));
        ok++;
      } catch (_) {}
    }
    if (ok < tried) toast('일부 파일은 그림으로 열 수 없었어요');
    else if (!ok) { showMine(); return; }
    else if (set) toast('"' + set + '" 세트에 ' + ok + '개 넣었어요');
    showMine();
  };

  // 고른 영역(crop)만 잘라서 줄임. 투명한 그림은 투명 PNG(360px), 사진은 JPG(640px)
  async function makeSticker(f, opt) {
    const bmp = await loadImage(f);
    if (f.type === 'image/gif' && f.size < 3000000) return { png: f, gif: null }; // 움직이는 GIF는 그대로
    const c = (opt && opt.crop) || { x: 0, y: 0, w: bmp.width, h: bmp.height };
    const clear = hasClearEdge(bmp) || f.type === 'image/png' && hasAnyClear(bmp, c);
    const k = Math.min(1, (clear ? 360 : 640) / Math.max(c.w, c.h));
    const w = Math.max(1, Math.round(c.w * k)), h = Math.max(1, Math.round(c.h * k));
    const cv = document.createElement('canvas'); cv.width = w; cv.height = h;
    const ctx = cv.getContext('2d');
    ctx.imageSmoothingQuality = 'high';
    ctx.drawImage(bmp, c.x, c.y, c.w, c.h, 0, 0, w, h);
    const png = await new Promise(ok => cv.toBlob(ok, clear ? 'image/png' : 'image/jpeg', 0.9));
    return { png, gif: null };
  }
  function hasAnyClear(img, c) {
    const k = Math.min(1, 160 / Math.max(c.w, c.h));
    const w = Math.max(1, Math.round(c.w * k)), h = Math.max(1, Math.round(c.h * k));
    const cv = document.createElement('canvas'); cv.width = w; cv.height = h;
    const x = cv.getContext('2d'); x.drawImage(img, c.x, c.y, c.w, c.h, 0, 0, w, h);
    const d = x.getImageData(0, 0, w, h).data;
    for (let i = 3; i < d.length; i += 4) if (d[i] < 250) return true;
    return false;
  }
  function loadImage(f) {
    return new Promise((ok, no) => { const img = new Image(); img.onload = () => ok(img); img.onerror = no; img.src = URL.createObjectURL(f); });
  }
  // ---------- 사진 다듬기: 직접 자르기 + 배경 지우기 켜고 끄기 ----------
  // 이미 배경이 투명한 그림인지 (둘레의 10% 넘게 투명)
  function hasClearEdge(img) {
    const k = Math.min(1, 160 / Math.max(img.naturalWidth, img.naturalHeight));
    const w = Math.max(3, Math.round(img.naturalWidth * k)), h = Math.max(3, Math.round(img.naturalHeight * k));
    const cv = document.createElement('canvas'); cv.width = w; cv.height = h;
    const x = cv.getContext('2d'); x.drawImage(img, 0, 0, w, h);
    const d = x.getImageData(0, 0, w, h).data;
    let t = 0, m = 0;
    for (let X = 0; X < w; X++) { m += 2; if (d[X * 4 + 3] < 250) t++; if (d[((h - 1) * w + X) * 4 + 3] < 250) t++; }
    for (let Y = 0; Y < h; Y++) { m += 2; if (d[Y * w * 4 + 3] < 250) t++; if (d[(Y * w + w - 1) * 4 + 3] < 250) t++; }
    return t > m / 10;
  }
  function editPhoto(f, n, total) {
    return new Promise(async done => {
      let img; try { img = await loadImage(f); } catch (_) { done({ crop: null, bg: true }); return; }
      const W = img.naturalWidth, H = img.naturalHeight;
      const s = plainSheet(total > 1 ? '사진 다듬기 ' + (n + 1) + '/' + total : '사진 다듬기');
      const stage = document.createElement('div'); stage.className = 'crop';
      const pic = document.createElement('img'); pic.src = img.src; stage.appendChild(pic);
      const box = document.createElement('div'); box.className = 'cropbox';
      for (const k of ['nw', 'ne', 'sw', 'se']) { const hd = document.createElement('i'); hd.dataset.k = k; box.appendChild(hd); }
      stage.appendChild(box); s.appendChild(stage);
      // 자르기 비율
      const chips = document.createElement('div'); chips.className = 'chips';
      let ratio = 0; // 0 = 자유
      let r = { x: 0, y: 0, w: W, h: H }; // 원본 픽셀 기준
      const fit = q => {
        if (q) { const side = Math.min(r.w, r.h); r = { x: r.x + (r.w - side) / 2, y: r.y + (r.h - side) / 2, w: side, h: side }; }
      };
      for (const [t, q] of [['자유', 0], ['정사각형', 1], ['전체', -1]]) {
        const c = document.createElement('button'); c.className = 'chip' + (q === 0 ? ' on' : ''); c.textContent = t;
        c.onclick = () => {
          chips.querySelectorAll('.chip').forEach(x => x.classList.toggle('on', x === c));
          if (q === -1) { ratio = 0; r = { x: 0, y: 0, w: W, h: H }; } else { ratio = q; fit(q); }
          draw();
        };
        chips.appendChild(c);
      }
      s.appendChild(chips);
      if (hasClearEdge(img)) {
        const lab = document.createElement('div'); lab.className = 'tip'; lab.textContent = '배경이 투명한 그대로 들어가요'; s.appendChild(lab);
      }
      const finish = v => { onSheetClose = null; closeSheet(); done(v); };
      const crop = () => ({ x: Math.round(r.x), y: Math.round(r.y), w: Math.max(1, Math.round(r.w)), h: Math.max(1, Math.round(r.h)) });
      const okText = total > 1 ? '넣기' : 'MY에 넣기';
      sheetBtn(s, okText, 'primary', () => finish({ crop: crop() }));
      if (total > 1 && n < total - 1) sheetBtn(s, '나머지는 자르지 않고 모두 넣기', '', () => finish({ crop: crop(), rest: true }));
      sheetBtn(s, '닫기', '', () => finish(null));
      onSheetClose = () => done(null);
      // 화면 크기 ↔ 원본 크기
      const scale = () => pic.clientWidth / W;
      function draw() {
        const k = scale();
        box.style.left = r.x * k + 'px'; box.style.top = r.y * k + 'px';
        box.style.width = r.w * k + 'px'; box.style.height = r.h * k + 'px';
      }
      pic.onload = draw; if (pic.complete) requestAnimationFrame(draw);
      let drag = null;
      box.addEventListener('pointerdown', e => {
        e.preventDefault(); box.setPointerCapture(e.pointerId);
        drag = { k: e.target.dataset.k || 'move', sx: e.clientX, sy: e.clientY, r0: { ...r } };
      });
      box.addEventListener('pointermove', e => {
        if (!drag) return;
        const k = scale(), dx = (e.clientX - drag.sx) / k, dy = (e.clientY - drag.sy) / k, o = drag.r0, min = Math.max(16, Math.min(W, H) * 0.08);
        let { x, y, w, h } = o;
        if (drag.k === 'move') { x = Math.min(Math.max(0, o.x + dx), W - o.w); y = Math.min(Math.max(0, o.y + dy), H - o.h); }
        else {
          let L = o.x, T = o.y, R = o.x + o.w, B = o.y + o.h;
          if (drag.k.includes('w')) L = Math.min(Math.max(0, L + dx), R - min);
          if (drag.k.includes('e')) R = Math.max(Math.min(W, R + dx), L + min);
          if (drag.k.includes('n')) T = Math.min(Math.max(0, T + dy), B - min);
          if (drag.k.includes('s')) B = Math.max(Math.min(H, B + dy), T + min);
          if (ratio) { // 정사각형: 짧은 쪽에 맞추고, 잡은 모서리 반대쪽을 고정
            const side = Math.min(R - L, B - T);
            if (drag.k.includes('w')) L = R - side; else R = L + side;
            if (drag.k.includes('n')) T = B - side; else B = T + side;
          }
          x = L; y = T; w = R - L; h = B - T;
        }
        r = { x, y, w, h }; draw();
      });
      const end = () => { drag = null; };
      box.addEventListener('pointerup', end); box.addEventListener('pointercancel', end);
    });
  }

  // ---------- 시작 ----------
  openTab(store.get('recent', []).length ? 'recent' : 'g0');
  if ('serviceWorker' in navigator && location.protocol === 'https:') navigator.serviceWorker.register('sw.js').catch(() => {});
  window.__app = { openTab, showSearch, pick };
})();
