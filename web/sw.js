// 한 번 열면 인터넷이 없어도 이모지·특수문자는 쓸 수 있게 저장
const C = 'emoji-v13';
const FILES = ['./', 'index.html', 'app.js', 'data.js', 'picker.js', 'recommend.js', 'sets.js', 'manifest.webmanifest', 'icon-192.png', 'PretendardVariable.woff2'];
self.addEventListener('install', e => { e.waitUntil(caches.open(C).then(c => c.addAll(FILES))); self.skipWaiting(); });
self.addEventListener('activate', e => { e.waitUntil(caches.keys().then(ks => Promise.all(ks.filter(k => k !== C).map(k => caches.delete(k))))); self.clients.claim(); });
self.addEventListener('fetch', e => {
  const u = new URL(e.request.url);
  if (u.origin !== location.origin) return; // 다른 사이트 파일은 저장하지 않음
  e.respondWith(fetch(e.request).then(r => { const cp = r.clone(); caches.open(C).then(c => c.put(e.request, cp)); return r; }).catch(() => caches.match(e.request)));
});
