// 확장 프로그램의 data.js / picker.js(같은 뜻 묶음)를 Windows 프로그램용 리소스로 변환
const fs = require('fs'), path = require('path');
const ext = path.join(__dirname, '..', 'emoji-picker-extension');
global.window = {}; require(path.join(ext, 'data.js'));
const lines = [];
for (const g of window.__EMOJI_GROUPS__) lines.push(['#group', g.id, g.icon, g.name].join('\t'));
for (const r of window.__EMOJI_DATA__) lines.push(r.join('\t'));
fs.writeFileSync(path.join(__dirname, 'res', 'emoji.tsv'), lines.join('\n') + '\n');
const src = fs.readFileSync(path.join(ext, 'picker.js'), 'utf8');
const syn = src.match(/const SYNONYMS = \[([\s\S]*?)\]\.map/)[1].match(/'([^']+)'/g).map(s => s.slice(1, -1));
fs.writeFileSync(path.join(__dirname, 'res', 'synonyms.txt'), syn.join('\n') + '\n');
console.log('emoji', window.__EMOJI_DATA__.length, 'synonym groups', syn.length);
