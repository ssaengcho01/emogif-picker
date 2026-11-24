// 사용법: node tools/build-data.js <emojibase-data 패키지 폴더>
// emojibase-data(유니코드 CLDR 기반)의 한국어·영어 라벨/태그로 data.js를 생성합니다.
const path = require('path');
const fs = require('fs');
const base = process.argv[2];
const ko = require(path.resolve(base, 'ko/data.json'));
const en = require(path.resolve(base, 'en/data.json'));
const enByHex = new Map(en.map(e => [e.hexcode, e]));

// CLDR에 없는 구어체/자주 쓰는 표현 보강
const EXTRA = {
  '😂': 'ㅋㅋ ㅋㅋㅋ 웃김 빵터짐 웃음', '🤣': 'ㅋㅋ ㅋㅋㅋ 웃김 빵터짐 웃음', '😆': 'ㅋㅋ 웃음 신남',
  '😄': '웃음 기쁨 행복', '😊': '미소 웃음 행복 기쁨', '🙂': '미소 웃음',
  '😭': 'ㅠㅠ ㅜㅜ 눈물 울음 울다 우는 오열 슬픔 슬퍼', '😢': 'ㅠㅠ ㅜㅜ 눈물 울음 울다 우는 슬픔 슬퍼', '🥲': 'ㅠㅠ 눈물 울음 웃픈 웃프다 웃는데 우는 웃으면서 울기 억지웃음 씁쓸',
  '🥹': 'ㅠㅠ 눈물 울음 감동 글썽',
  '🥺': 'ㅠㅠ 글썽 울먹 눈물 울음 제발 부탁', '😥': 'ㅠㅠ 눈물 울음 서운 슬퍼', '😔': '슬픔 슬퍼 우울 시무룩', '😞': '슬픔 슬퍼 실망 우울', '🙁': '슬픔 슬퍼', '☹️': '슬픔 슬퍼', '😓': '식은땀 땀', '😿': '눈물 울음 울다', '💧': '눈물 물방울',
  '😡': '화남 화 분노 짜증 빡침', '😠': '화남 화 분노 짜증', '🤬': '화남 분노 욕 빡침', '😤': '짜증 씩씩',
  '😴': '졸림 잠 수면 자다 꿀잠', '😪': '졸림 잠', '🥱': '하품 졸림 지루',
  '😍': '사랑 하트 반함 좋아', '🥰': '사랑 하트 좋아', '😘': '뽀뽀 키스 사랑', '❤️': '하트 사랑 좋아',
  '👍': '좋아 좋아요 굿 최고 엄지 따봉 ㅇㅋ 오케이', '👏': '박수 축하 짝짝', '🙏': '감사 부탁 기도 제발 고마워',
  '🎉': '축하 파티 생일 폭죽 대박', '🎂': '생일 케이크 축하', '🥳': '축하 파티 생일',
  '🔥': '불 대박 핫 열정', '💯': '최고 백점 완벽', '😱': '놀람 충격 공포 헐', '😮': '놀람 헐 오',
  '🤔': '생각 고민 흠', '😅': '민망 땀 머쓱', '🙇': '죄송 사과 꾸벅 감사', '👋': '안녕 인사 손인사 바이',
  '💰': '돈 부자', '💸': '돈 지출 텅장', '☕': '커피 카페 차', '🍺': '술 맥주', '🍻': '술 맥주 건배 짠',
  '🍷': '술 와인', '🍚': '밥 쌀', '🍜': '라면 국수', '🌧️': '비 날씨 우산', '☔': '비 우산 장마',
  '❄️': '눈 겨울 추움', '☃️': '눈사람 눈 겨울', '⛄': '눈사람 눈 겨울', '🌙': '달 밤 초승달', '🌕': '달 보름달',
  '⭐': '별', '🌟': '별 반짝', '✨': '반짝 반짝반짝 별', '🌈': '무지개', '☀️': '해 햇빛 맑음 날씨 태양',
  '🌞': '해 해님 햇빛 태양', '🌅': '해돋이 일출', '🌄': '해돋이 일출', '🌇': '해질녘 일몰 노을',
  '🐶': '강아지 개 멍멍', '🐕': '강아지 개', '🐱': '고양이 냥이 야옹', '🐈': '고양이 냥이',
  '💪': '힘 화이팅 파이팅 근육', '✅': '체크 완료 확인', '❌': '엑스 아니 틀림 취소', '⭕': '동그라미 정답 맞음',
  '🤮': '토 구역질 우웩', '🤒': '아픔 감기 열', '😷': '마스크 아픔 감기', '🫠': '녹음 더움 민망',
};

// 사용자 제보로 보강한 연관어 합치기
const EXTRA2 = require('./extra2.js');
for (const [k, v] of Object.entries(EXTRA2.strong)) EXTRA[k] = ((EXTRA[k] || '') + ' ' + v).trim().split(' ').filter((w, i, a) => a.indexOf(w) === i).join(' ');

const out = [];
for (const e of ko) {
  if (e.group === undefined || e.group === 2) continue;
  if ((e.version || 0) > 15) continue; // 구형 OS에서 깨지는 최신 이모지 제외
  const enE = enByHex.get(e.hexcode) || {};
  const extra = (EXTRA[e.emoji] || '').split(' ').filter(Boolean); // 직접 고른 대표 검색어 (우선순위 높음)
  const weak = (EXTRA2.weak[e.emoji] || '').split(' ').filter(Boolean); // 보조 검색어는 일반 태그로
  const words = new Set([e.label, ...(e.tags || []), ...weak].filter(w => !extra.includes(w)));
  const enWords = new Set([enE.label, ...(enE.tags || [])].filter(Boolean));
  out.push([e.emoji, e.group, e.label, [...words].join('|'), [...enWords].join('|'), extra.join('|')]);
}
const missing = Object.keys(EXTRA).filter(k => !out.some(o => o[0] === k));
if (missing.length) console.warn('EXTRA 매칭 실패:', missing.join(' '));

const groups = ['😀', '👋', null, '🐻', '🍔', '✈️', '⚽', '💡', '❤️', '🏳️'];
const groupNames = ['표정·감정', '사람·몸', null, '동물·자연', '음식', '여행·장소', '활동', '사물', '기호', '깃발'];
fs.writeFileSync(path.join(__dirname, '..', 'data.js'),
  '// 자동 생성 파일 (tools/build-data.js). 출처: emojibase-data / Unicode CLDR\n' +
  'window.__EMOJI_GROUPS__=' + JSON.stringify(groups.map((icon, i) => icon && { id: i, icon, name: groupNames[i] }).filter(Boolean)) + ';\n' +
  'window.__EMOJI_DATA__=' + JSON.stringify(out) + ';\n');
console.log('emoji count:', out.length);
