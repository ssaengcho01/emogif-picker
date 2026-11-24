const MENU_ID = 'emoji-picker-open';
const FILES = ['data.js', 'picker.js', 'content.js'];

chrome.runtime.onInstalled.addListener(async () => {
  chrome.contextMenus.removeAll(() =>
    chrome.contextMenus.create({ id: MENU_ID, title: '😀 이모지 넣기', contexts: ['editable'] }));
  // 설치/업데이트 전에 열려 있던 탭에도 바로 적용 (새로고침 불필요)
  for (const tab of await chrome.tabs.query({})) {
    if (!/^(https?|file):/.test(tab.url || '')) continue;
    chrome.scripting.executeScript({ target: { tabId: tab.id, allFrames: true }, files: FILES }).catch(() => {});
  }
});

// 메시지를 보내고, 페이지에 스크립트가 없으면 넣은 뒤 다시 보냄
async function openPicker(tabId, frameId, type) {
  const opts = frameId != null ? { frameId } : {};
  try {
    await chrome.tabs.sendMessage(tabId, { type }, opts);
  } catch (_) {
    try {
      await chrome.scripting.executeScript({
        target: frameId != null ? { tabId, frameIds: [frameId] } : { tabId }, files: FILES,
      });
      await chrome.tabs.sendMessage(tabId, { type }, opts);
    } catch (e) { console.warn('이모지 피커를 열 수 없는 페이지:', e.message); }
  }
}
self.openPicker = openPicker;

chrome.contextMenus.onClicked.addListener((info, tab) => {
  if (info.menuItemId === MENU_ID && tab) openPicker(tab.id, info.frameId, 'open-from-menu');
});

chrome.commands.onCommand.addListener(async (cmd, tab) => {
  if (cmd !== 'open-picker') return;
  tab = tab || (await chrome.tabs.query({ active: true, currentWindow: true }))[0];
  if (tab) openPicker(tab.id, undefined, 'open-from-shortcut');
});
