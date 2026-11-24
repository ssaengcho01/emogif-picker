#!/bin/bash
# Linux(Mono)에서 Windows용 EmojiPicker.exe 빌드. 결과물은 .NET Framework 4.x(Windows 10/11 기본 내장)에서 실행됩니다.
set -e
cd "$(dirname "$0")"
node export-data.js
# 기본으로 넣어 둘 이모티콘 세트 (builtin-sets/세트이름/01_제목.png) → exe 안에 넣고, 처음 켤 때 MY에 풀어 놓음
rm -rf res/builtin && mkdir -p res/builtin
BUILTIN=""
: > res/builtin/builtin.txt
si=0
for d in builtin-sets/*/; do
  [ -d "$d" ] || continue
  si=$((si+1)); fi=0
  for f in "$d"*; do
    fi=$((fi+1)); r="builtin-$si-$fi${f##*.}"
    r="builtin-$si-$fi.${f##*.}"
    cp "$f" "res/builtin/$r"
    printf '%s\t%s\t%s\n' "$(basename "$d")" "$(basename "$f")" "$r" >> res/builtin/builtin.txt
    BUILTIN="$BUILTIN -resource:res/builtin/$r,$r"
  done
done
BUILTIN="$BUILTIN -resource:res/builtin/builtin.txt,builtin.txt"
# Mono 자체 라이브러리가 아니라 실제 .NET Framework 4.5 API 목록으로 컴파일 (Windows에 없는 함수 사용 방지)
API=/usr/lib/mono/4.5-api
mcs -target:winexe -platform:anycpu -optimize+ -codepage:utf8 -langversion:6 -nostdlib -noconfig \
  -out:dist/EmojiPicker.exe -win32icon:res/app.ico \
  -r:$API/mscorlib.dll -r:$API/System.dll -r:$API/System.Core.dll \
  -r:$API/System.Windows.Forms.dll -r:$API/System.Security.dll -r:$API/System.Web.Extensions.dll -r:$API/System.Drawing.dll \
  -resource:res/emoji.tsv,emoji.tsv -resource:res/synonyms.txt,synonyms.txt \
  -resource:res/emoji-sprite.png,emoji-sprite.png -resource:res/app.ico,app.ico \
  -resource:res/Pretendard-Regular.ttf.gz,Pretendard-Regular.ttf.gz -resource:res/Pretendard-Bold.ttf.gz,Pretendard-Bold.ttf.gz -resource:res/Pretendard-LICENSE.txt,Pretendard-LICENSE.txt \
  $BUILTIN \
  src/*.cs

# 코드 서명 (게시자: SSAENGCHO 쌩초). signing/codesign.pfx 는 절대 공유하지 마세요.
if [ -z "$NOSIGN" ] && [ -f signing/codesign.pfx ]; then
  osslsigncode sign -pkcs12 signing/codesign.pfx -pass "" -h sha256 \
    -n "EmoGIF Picker" -t http://timestamp.digicert.com \
    -in dist/EmojiPicker.exe -out dist/EmojiPicker.signed.exe \
  || osslsigncode sign -pkcs12 signing/codesign.pfx -pass "" -h sha256 -n "EmoGIF Picker" \
    -in dist/EmojiPicker.exe -out dist/EmojiPicker.signed.exe
  mv dist/EmojiPicker.signed.exe dist/EmojiPicker.exe
  cp signing/SSAENGCHO.cer dist/
fi
ls -la dist/EmojiPicker.exe
