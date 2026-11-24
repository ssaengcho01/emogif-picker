<div align="center">

<a href="../../releases/latest"><img src="docs/images/overview-v4.png" width="100%" alt="EmoGIF Picker: 톡방·SNS 글을 이모지로 더 예쁘게"></a>

## [⬇️ Windows용 무료 다운로드](../../releases/latest)

받은 exe를 더블클릭하고, 글 쓰는 칸에서 `Ctrl` + `Win` + `.`

<sub>"Windows의 PC 보호" 창이 뜨면 <b>[추가 정보] → [실행]</b> · 📱 <a href="https://emoji-picker-mobile.soldaeng-guri.chatgpt.site">휴대폰 웹앱</a></sub>

</div>

<br>

<details>
<summary>개발자용 · 직접 빌드하기</summary>

```bash
sudo apt install mono-complete nodejs
cd src/emoji-picker-windows
mkdir -p dist && NOSIGN=1 bash build.sh   # → dist/EmojiPicker.exe
```

`src/emoji-picker-windows` Windows 프로그램 · `src/emoji-picker-extension` 크롬 확장 · `web` 휴대폰 웹앱

이모지: Noto Color Emoji (Apache 2.0) · 글꼴: Pretendard (OFL 1.1) · GIF: KLIPY · 기본 이모티콘 「크리에이터의 하루」 font: @aiollcut
</details>
