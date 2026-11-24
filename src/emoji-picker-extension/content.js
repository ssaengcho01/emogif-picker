// 텍스트 입력창에서 우클릭하면 이모지 피커를 띄우고, 고른 이모지를 커서 위치에 넣습니다.
(() => {
  if (window.__emojiPickerLoaded) return;
  window.__emojiPickerLoaded = true;

  const TEXT_INPUT_TYPES = new Set(['text', 'search', 'email', 'url', 'tel', '']);
  let settings = { mode: 'menu' }; // 'rightclick' | 'menu'
  let recent = [];
  chrome.storage.sync.get({ mode: 'menu' }, v => { settings = v; });
  chrome.storage.local.get({ recent: [] }, v => { recent = v.recent; });
  chrome.storage.onChanged.addListener(ch => {
    if (ch.mode) settings.mode = ch.mode.newValue;
    if (ch.recent) recent = ch.recent.newValue;
  });

  function editableFrom(ev) {
    for (const el of ev.composedPath ? ev.composedPath() : [ev.target]) {
      if (!(el instanceof Element)) continue;
      if (el.tagName === 'TEXTAREA') return el.disabled || el.readOnly ? null : el;
      if (el.tagName === 'INPUT') {
        const ok = TEXT_INPUT_TYPES.has((el.getAttribute('type') || '').toLowerCase());
        return ok && !el.disabled && !el.readOnly ? el : null;
      }
      if (el.isContentEditable) {
        // contenteditable 최상위 요소를 대상으로
        let top = el;
        while (top.parentElement && top.parentElement.isContentEditable) top = top.parentElement;
        return top;
      }
    }
    return null;
  }

  // ---- 대상/커서 저장 ----
  let target = null, savedSel = null, lastPoint = null;

  function saveSelection(el) {
    target = el;
    if (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA') {
      savedSel = { start: el.selectionStart ?? el.value.length, end: el.selectionEnd ?? el.value.length };
    } else {
      const root = el.getRootNode();
      const sel = (root.getSelection ? root.getSelection() : null) || window.getSelection();
      savedSel = sel && sel.rangeCount && el.contains(sel.getRangeAt(0).startContainer)
        ? sel.getRangeAt(0).cloneRange() : null;
    }
  }

  function insert(text) {
    const el = target;
    if (!el || !el.isConnected) return;
    el.focus({ preventScroll: true });
    if (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA') {
      const { start, end } = savedSel || { start: el.value.length, end: el.value.length };
      try { el.setSelectionRange(start, end); } catch (_) {}
      // execCommand는 실행취소(Ctrl+Z)와 React 등 프레임워크 상태 갱신이 자연스럽게 됩니다
      if (!document.execCommand('insertText', false, text)) {
        el.setRangeText(text, start, end, 'end');
        el.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: text }));
      }
      savedSel = { start: el.selectionStart, end: el.selectionEnd };
    } else {
      const sel = window.getSelection();
      if (savedSel) { sel.removeAllRanges(); sel.addRange(savedSel); }
      else { // 커서 정보가 없으면 끝에 추가
        const r = document.createRange(); r.selectNodeContents(el); r.collapse(false);
        sel.removeAllRanges(); sel.addRange(r);
      }
      if (!document.execCommand('insertText', false, text)) {
        const r = sel.getRangeAt(0); r.deleteContents();
        const node = document.createTextNode(text); r.insertNode(node);
        r.setStartAfter(node); r.collapse(true); sel.removeAllRanges(); sel.addRange(r);
        el.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: text }));
      }
      savedSel = sel.rangeCount ? sel.getRangeAt(0).cloneRange() : null;
    }
  }

  // ---- 피커 ----
  let picker = null, wrap = null;

  function ensurePicker() {
    if (picker) return;
    picker = window.EmojiPicker.create({
      recent,
      getRecent: () => recent,
      spriteUrl: chrome.runtime.getURL('emoji-sprite.webp'),
      saveRecent: list => { recent = list; chrome.storage.local.set({ recent: list }); },
      hint: 'Enter: 넣기 · Shift+클릭: 여러 개 · Esc: 닫기',
      onPick: (ch, keepOpen) => {
        insert(ch);
        if (keepOpen) picker.focus(); else close();
      },
      onClose: () => close(true),
    });
    wrap = picker.host;
    Object.assign(wrap.style, { position: 'fixed', zIndex: '2147483647', top: '0', left: '0', display: 'none' });
  }

  function open(x, y) {
    ensurePicker();
    picker.reset();
    // 팝업 작성창(모달) 안에 넣어야 포커스 가두기·최상위 레이어에 가려지지 않음
    const modal = target && target.isConnected && target.closest('dialog[open], [role="dialog"], [aria-modal="true"]');
    (modal || document.body || document.documentElement).appendChild(wrap);
    wrap.style.display = 'block';
    const W = 354, H = 402, m = 8;
    const vw = window.innerWidth, vh = window.innerHeight;
    let left = x + W + m > vw ? Math.max(m, x - W) : x;
    let top = y + H + m > vh ? Math.max(m, y - H) : y;
    left = Math.max(m, Math.min(left, vw - W - m));
    top = Math.max(m, Math.min(top, vh - H - m));
    wrap.style.left = left + 'px';
    wrap.style.top = top + 'px';
    // 부모에 transform이 있으면 fixed 기준이 달라지므로 실제 위치를 재서 보정
    const r = wrap.getBoundingClientRect();
    if (Math.abs(r.left - left) > 1 || Math.abs(r.top - top) > 1) {
      wrap.style.left = (2 * left - r.left) + 'px';
      wrap.style.top = (2 * top - r.top) + 'px';
    }
    requestAnimationFrame(() => picker.focus());
  }

  function close(refocus) {
    if (!wrap || wrap.style.display === 'none') return;
    wrap.style.display = 'none';
    if (refocus && target && target.isConnected) {
      target.focus({ preventScroll: true });
      if (savedSel && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA')) {
        try { target.setSelectionRange(savedSel.start, savedSel.end); } catch (_) {}
      } else if (savedSel) {
        const s = window.getSelection(); s.removeAllRanges(); s.addRange(savedSel);
      }
    }
  }
  const isOpen = () => wrap && wrap.style.display !== 'none';

  // ---- 이벤트 ----
  window.addEventListener('contextmenu', ev => {
    if (isOpen() && ev.composedPath().includes(wrap)) { ev.preventDefault(); return; }
    const el = editableFrom(ev);
    if (!el) return;
    saveSelection(el);
    lastPoint = { x: ev.clientX, y: ev.clientY };
    // Shift+우클릭이면 원래 브라우저 메뉴(복사/붙여넣기 등)
    if (settings.mode !== 'rightclick' || ev.shiftKey) return;
    ev.preventDefault();
    ev.stopImmediatePropagation();
    open(ev.clientX, ev.clientY);
  }, true);

  window.addEventListener('mousedown', ev => {
    if (isOpen() && !ev.composedPath().includes(wrap)) close(false);
  }, true);
  window.addEventListener('keydown', ev => {
    if (ev.key === 'Escape' && isOpen()) close(true);
  }, true);
  window.addEventListener('resize', () => close(false));
  window.addEventListener('blur', () => {
    // 다른 창/프레임으로 포커스가 넘어가면 닫기 (피커 자신에게 간 경우는 제외)
    setTimeout(() => { if (isOpen() && !document.hasFocus()) close(false); }, 0);
  });

  // 메뉴 항목 클릭 / 단축키 → background가 보낸 메시지
  chrome.runtime.onMessage.addListener(msg => {
    if (msg.type !== 'open-from-menu' && msg.type !== 'open-from-shortcut') return;
    // 우클릭 시점에 대상을 못 잡았으면(설치 직후 등) 지금 포커스된 입력창을 사용
    if (msg.type === 'open-from-menu' && target && target.isConnected && lastPoint) {
      open(lastPoint.x, lastPoint.y);
      return;
    }
    const el = deepActiveElement();
    if (!el) return;
    const editable = editableFrom({ composedPath: () => [el], target: el });
    if (!editable) return;
    saveSelection(editable);
    const r = caretRect(editable);
    open(r.left, r.bottom + 4);
  });

  function deepActiveElement() {
    let a = document.activeElement;
    while (a && a.shadowRoot && a.shadowRoot.activeElement) a = a.shadowRoot.activeElement;
    return a;
  }
  function caretRect(el) {
    if (el.isContentEditable) {
      const s = window.getSelection();
      if (s.rangeCount) {
        const r = s.getRangeAt(0).getBoundingClientRect();
        if (r.width || r.height) return r;
      }
    }
    return el.getBoundingClientRect();
  }
})();
