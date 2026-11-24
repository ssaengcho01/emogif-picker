const toast = document.getElementById('toast');
let recent = [], buffer = '';

chrome.storage.local.get({ recent: [] }, v => {
  recent = v.recent;
  const picker = EmojiPicker.create({
    embedded: true,
    spriteUrl: 'emoji-sprite.webp',
    recent,
    getRecent: () => recent,
    saveRecent: list => { recent = list; chrome.storage.local.set({ recent: list }); },
    hint: '클릭하면 복사 · Shift+클릭: 이어서 복사',
    onPick: async (ch, keepOpen) => {
      buffer = keepOpen ? buffer + ch : ch;
      await navigator.clipboard.writeText(buffer);
      toast.textContent = `${buffer} 복사됨`;
      toast.classList.add('on');
      clearTimeout(toast._t); toast._t = setTimeout(() => toast.classList.remove('on'), 1200);
    },
    onClose: () => window.close(),
  });
  document.getElementById('picker').appendChild(picker.host);
  picker.focus();
});

chrome.storage.sync.get({ mode: 'menu' }, v => {
  for (const r of document.querySelectorAll('input[name=mode]')) {
    r.checked = r.value === v.mode;
    r.onchange = () => chrome.storage.sync.set({ mode: r.value });
  }
});
