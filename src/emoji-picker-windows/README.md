# 이모지 피커 — Windows 프로그램

어떤 프로그램(카톡, 메모장, 한글, 워드, 브라우저 …)의 입력창에서든 `Ctrl+Win+.` → 한국어로 검색 → 커서 위치에 입력.
사용법은 `dist/사용법.txt` 참고.

## 빌드 (Linux + Mono)
```bash
./build.sh
```
- 검색 데이터·같은 뜻 묶음은 `../emoji-picker-extension`(data.js, picker.js)에서 가져옵니다 (`export-data.js`).
- 이모지 그림은 `res/emoji-sprite.png` (Noto Color Emoji로 렌더링, 48px 타일 48열).
- 점검: `mono dist/EmojiPicker.exe --search 울음 해`
