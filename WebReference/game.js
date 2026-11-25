(function () {
'use strict';

/* =====================================================================
 * CONFIG - 모든 수치는 여기에 모은다.
 * "기획서 초안" = 기획서 04 주요 수치에 있는 값 (초안)
 * "기획서 미정" = 기획서에 수치가 없어 임시로 정한 값
 * ===================================================================== */
const CONFIG = {
  // ---- 기획서 초안 (04 주요 수치) ----
  maxHearts: 3,            // 하트 3
  maxDays: 30,             // 남은 일수 최대 30일
  startDays: 30,           // 시작 D-30 (05-2 요소 2)
  dayDuration: 0.33,       // 1일 = 약 0.33초
  stepDays: 1,             // 계단 1칸 +1일
  parryDays: 1.5,          // 패링 성공 +1.5일
  parryWindow: 0.20,       // 패링 판정 0.20초
  parryPerfect: 0.035,     // 결정(2026-11-25): 판정 창 한가운데에서 이만큼(초, 약 2프레임) 안에 막으면 '완벽' (새 막기 소리). 그 밖은 예전 막기 소리
  warnTime: 1.0,           // 공격 예고 1.0초 전 표시 (서류 비행 시간과 같음)
  promoteFloors: [20, 40, 60, 80], // 승급 심사 층. 20·40·60층은 승급 뒤 계속 오른다 (결정 2026-11-20: PoC 1~80층)
  practiceFloor: 11,       // 결정(2026-11-20): 연습 모드에서 서 있는 층 (층 바닥 위, 계단을 오르지 않는다)
  practiceDiff: 0.5,       // 기획서 미정: 연습 모드 공격 간격의 난이도 (구간 가운데 값)
  endlessRampFloors: 60,   // 기획서 미정: 무한 모드에서 난이도가 끝까지 오르는 층 수
  finalFloor: 100,         // 결정(2026-11-20): 80층에서 CEO로 승급한 뒤 100층(CEO실)에 오르면 PoC 마무리
  crackFromFloor: 21,      // 결정(2026-11-20): 21층부터 금 간 계단
  crackShare: 0.16,        // 기획서 미정: 금 간 계단 비율 (층이 바뀌는 칸은 빼고)
  crackTime: 1.5,          // 결정: 금 간 계단에 이만큼(초) 서 있으면 무너져 바로 구조조정
  wetFromFloor: 30,        // 결정(2026-11-25): 30층부터 젖은 계단(WET HAZARD). 물 위에 전선이 올라 있다
  wetShare: 0.14,          // 기획서 미정: 젖은 계단 비율 (층이 바뀌는 칸, 금 간 칸은 빼고)
  shockPeriod: 3.0,        // 결정: 전기가 흐르는 주기(초)
  shockTime: 0.5,          // 기획서 미정: 한 번 흐르는 시간(초). 이때 젖은 칸에 있으면 바로 구조조정
  shockWarn: 0.6,          // 기획서 미정: 흐르기 전 불꽃이 튀는 예고 시간(초)
  flashFromFloor: 61,      // 결정(2026-11-20): 61층부터 카메라 플래시. 막을 수 없지만 피해도 없다 (눈부심만)
  flashGap: 3.5,           // 기획서 미정: 플래시 평균 간격(초, ±40%)
  flashTime: 0.28,         // 기획서 미정: 플래시가 번쩍이는 시간(초)
  suitcaseFromFloor: 71,   // 결정(2026-11-20): 71층부터 서류가방. 전화기처럼 아래에서 솟다가 도중에 터져 서류·결재판이 쏟아진다
  weightSuitcase: 0.6,     // 기획서 미정
  // ---- 새 장애물 (2026-11-25)
  staplerFromFloor: 15,    // 결정: 15층부터 스테이플러. 막으면 부메랑처럼 반대쪽에서 돌아와 한 번 더 막아야 한다
  weightStapler: 0.6,      // 기획서 미정
  staplerReturn: 0.9,      // 기획서 미정: 막은 뒤 반대쪽에서 돌아와 맞기까지(초)
  airplaneFromFloor: 20,   // 결정: 20층부터 종이비행기 편대. 날아오는 쪽 막기 키를 꾹 누르고 있어야 한다
  weightAirplane: 0.5,     // 기획서 미정
  airplaneCount: 4,        // 기획서 미정: 한 편대의 비행기 수
  airplaneGap: 0.1,        // 결정(2026-11-25): 비행기 사이 간격(초). 바짝 붙은 편대. 누르고 있어야 하는 시간 = (수-1) x 간격
  envelopeFromFloor: 25,   // 결정(2026-11-25): 25층부터 편지 3연발. 같은 쪽에서 박자에 맞춰 날아오는 편지를 한 장씩 막는다
  weightEnvelope: 0.6,     // 기획서 미정
  envelopeCount: 3,        // 결정: 세 장
  envelopeGap: 0.4,        // 기획서 미정: 편지 사이 박자(초). 판정 0.2초 + 재입력 대기 0.15초보다 길어야 세 번 누를 수 있다
  chairFromFloor: 30,      // 결정(2026-11-25): 30층부터 튀어 내려오는 의자. 위 계단에서 두 칸씩 튀며 내려온다
  weightChair: 0.6,        // 기획서 미정
  chairHop: 0.34,          // 결정(2026-11-25): 의자가 두 칸을 한 번에 튀는 시간(초). 생길 때 플레이어 칸 6칸 위에서 출발해 세 번째에 그 칸에 내려앉는다
  reverseFromFloor: 35,    // 기획서 미정: 35층부터 거꾸로 칸. 그 칸에서는 반대쪽 화살표를 눌러야 오른다
  reverseShare: 0.10,      // 기획서 미정: 거꾸로 칸 비율 (층이 바뀌는 칸, 다른 계단 장애물 칸은 빼고)
  escalatorFromFloor: 20,  // 결정: 20층부터 내려가는 에스컬레이터 칸. 가만히 서 있으면 한 칸씩 밀려 내려간다
  escalatorShare: 0.12,    // 기획서 미정: 에스컬레이터 칸 비율 (층이 바뀌는 칸, 금 간·젖은 칸은 빼고)
  escalatorPush: 0.6,      // 기획서 미정: 이만큼(초) 서 있으면 한 칸 밀린다
  vanishFromFloor: 41,     // 기획서 미정: 41층부터 사라지는 칸
  vanishShare: 0.10,       // 기획서 미정: 사라지는 칸 비율 (층이 바뀌는 칸, 다른 장애물 칸은 빼고)
  vanishShow: 5.0,         // 결정: 5초 동안 있다가
  vanishHide: 1.0,         // 결정: 1초 동안 없어진다 (모든 사라지는 칸이 같은 박자)
  vanishWarn: 1.0,         // 기획서 미정: 없어지기 전 깜박이는 시간(초)
  gazeFromFloor: 61,       // 결정: 61층부터(보통·무한) 상사의 눈. 멀리서 쏜 빨간 레이저가 화면을 위에서 아래로 훑을 때 오르면 하트 -1
  gazeWarn: 5.0,           // 결정: 훑기 전 5초 예고 (화면이 붉어지고 위쪽 끝에 레이저가 차오름, 숫자). 눈은 보이지 않는다
  gazeSweep: 0.6,          // 결정(2026-11-25): 0.6초 만에 화면 위 끝에서 아래 끝까지 쓸어내린다 (순식간에 훑는 감시 레이저)
  gazeRest: 6.0,           // 기획서 미정: 다음 예고까지 쉬는 시간(초)
  gazeWidth: 40,           // 기획서 미정: 레이저 띠의 두께(px). 띠가 플레이어 몸(28px)에 걸쳐 있는 동안 오르면 들킨다
  rivalCrackTime: 0.8,     // 기획서 미정: 경쟁 사원이 금 간 칸에 이만큼(초) 서 있으면 무너져 떨어진다 (플레이어보다 무겁다)
  rivalVanishWait: 0.5,    // 기획서 미정: 경쟁 사원은 사라지기까지 이만큼(초)도 안 남은 칸에는 올라서지 않고 기다린다
  suitcaseBurst: 0.1,      // 결정: 도착 몇 초 전에 터지는지. 판정 창(도착 전 0.2초)의 한가운데 = 터지는 순간
  coffeeFromFloor: 11,     // 결정(2026-11-20): 11층부터 커피. 결재판처럼 날아오지만 막으면 안 된다(막으면 하트 -1, 그냥 두면 지나감)
  weightCoffee: 0.5,       // 기획서 미정
  stickyFromFloor: 61,     // 결정(2026-11-20): 61층부터 포스트잇. 막으면 튕겨 나갔다 돌아와 1~5번 막아야 없어진다
  weightSticky: 0.6,       // 기획서 미정
  stickyMax: 5,            // 결정: 포스트잇을 막아야 하는 최대 횟수 (색: 분홍 5, 파랑 4, 초록 3, 노랑 2, 흰색 1)
  promoteHeal: 1,          // 승급 시 하트 1 회복
  floorHeal: 1,            // 플레이테스트 반영: 새 층에 오를 때마다 하트 1 회복 (최대 maxHearts)

  // ---- 기획서 미정 ----
  stairsPerFloor: 6,       // 기획서 미정: 한 층당 계단 수
  comboPerMult: 3,         // 기획서 미정: 점수 배수가 한 단계 오르는 연속 패링 횟수 (결정 2026-11-20: COMBO는 연속 패링 횟수)
  maxMult: 10,             // 결정(2026-11-20): 점수 배수 최대 ×10
  stepPoints: 10,          // 기획서 미정: 계단 1칸 점수 (콤보 배수 적용)
  parryPoints: 50,         // 기획서 미정: 패링 성공 점수 (콤보 배수 적용)
  floorPoints: 100,        // 기획서 미정: 층 도달 점수 (콤보 배수 적용)
  drainStart: 1.0,         // 기획서 미정: 1층 일수 감소 배속
  drainEnd: 1.35,          // 기획서 미정: 구간(20층) 끝 일수 감소 배속 (구간마다 1층 값부터 다시 증가)
  attackIntervalStart: 2.4,// 기획서 미정: 구간 첫 층 공격 평균 간격(초)
  attackIntervalEnd: 1.1,  // 기획서 미정: 구간 끝 층 공격 평균 간격(초)
  attackJitter: 0.2,       // 기획서 미정: 간격 무작위 폭(±비율)
  minAttackGap: 0.8,       // 기획서 미정: 공격 도착 사이 최소 간격(초)
  firstAttackDelay: 3.0,   // 기획서 미정: 첫 공격까지 유예(초). 튜토리얼 대신 숨 돌릴 시간
  parryCooldown: 0.15,     // 기획서 미정: 패링 종료 후 재입력 대기(연타 방지)
  tenseFloor: 11,          // 기획서 미정: bgm_loop -> bgm_loop_tense(불협화음) 전환 층
  stairRunMax: 4,          // 기획서 미정: 같은 방향 계단 최대 연속 수
  stairKeepProb: 0.5,      // 기획서 미정: 같은 방향이 이어질 확률
  endDelay: 1.0,           // 기획서 미정: 종료 연출 후 결과 화면까지(초)
  botStepInterval: 0.19,   // bot=1 자동 이동 간격(초)
  botParryLead: 0.10,      // bot=1 도착 몇 초 전에 패링을 누를지

  // ---- 표현 ----
  stepW: 22,               // 계단 한 칸의 가로 이동(px)
  stepH: 12,               // 계단 한 칸의 세로 이동(px)
  showRivalCameo: true,    // 05-3 요소 5 경쟁 사원 장식: 경쟁 사원이 공격하기 전 층(rivalFromFloor 미만)에서만 배치
  rivalFromFloor: 11,      // 결정(2026-11-19): 기획서상 21층 위협인 경쟁 사원을 PoC 11층부터 미리 등장
  rivalShare: 0.4,         // 기획서 미정: 11층부터 공격 중 경쟁 사원 비율
  rivalWalk: 0.6,          // 기획서 미정: 화면 끝에서 층 바닥을 걸어 들어오는 시간(초). 이후 예고 1.0초 뒤 근접 공격
  rivalLeap: 0.3,          // 기획서 미정: 공격 직전 층 바닥에서 플레이어 칸으로 뛰어오르는 시간(초)
  phoneFromFloor: 31,      // 결정(2026-11-20): 31층부터 전화기. 화면 아래에서 솟아 포물선으로 날아온다
  paperFromFloor: 41,      // 결정(2026-11-20): 41층부터 서류. 머리 위로 넘어가 반대쪽에서 맞는다
  weightRival: 0.7,        // 기획서 미정: 공격 종류 비중 (결재판 1 기준)
  weightPhone: 0.6,        // 기획서 미정
  weightPaper: 0.6,        // 기획서 미정
  monitorFromFloor: 21,    // 결정(2026-11-20): 21층부터 컴퓨터 모니터. 위층 창이 깨지며 플레이어 옆으로 떨어진다
  weightMonitor: 0.6,      // 기획서 미정
  monitorAngle: 70,        // 결정(2026-11-20): 모니터가 떨어지는 사선의 각도(수평에서, 도)
  flickerTime: 0.6,        // 결정(2026-11-20): 모니터 예고. 형광등이 깜박이는 시간(초), 그 뒤 유리가 깨지며 1.0초 동안 떨어진다
  // 패링 타이밍 소리: [소리 ID, 도착 몇 초 전에 틀지]. 각 소리의 박(두 번째 딱·"헤이!"·두 번째 벨·종이 접는 소리)이
  // 판정 창(도착 전 0.2초)의 한가운데, 곧 도착 0.1초 전에 오도록 박 위치 + 0.1초 앞에서 튼다 (결정 2026-11-20)
  cues: { clipboard: ['sfx_cue_clipboard', 0.40], rival: ['sfx_cue_rival', 0.145], stapler: ['sfx_cue_stapler', 0.40], airplane: ['sfx_cue_airplane', 0.35], chair: ['sfx_cue_chair', 0.40],
          phone: ['sfx_phone_ring', 0.50], paper: ['sfx_cue_paper', 0.35],
          coffee: ['sfx_cue_coffee', 0.135], sticky: ['sfx_cue_sticky', 0.40],
          suitcase: ['sfx_cue_suitcase', 0.46] },   // 서류가방: "똑, 똑, 펑!"의 펑(0.36초)이 터지는 순간   // 커피: 높은 "헤이!"(막지 말라는 신호)
  rivalSpeed: 240,         // 기획서 미정: 경쟁 사원이 층 바닥을 걷는 속도(px/초)
  rivalClimb: 10,          // 기획서 미정: 경쟁 사원이 계단을 따라 오르는 속도(칸/초). 플레이어보다 빠르다
  commuters: 5,            // 기획서 미정: 타이틀 거리를 걷는 사원 수 (05-1 요소 4)
  bgmVolume: 0.45,
  ambVolume: 0.15,         // 환경음은 낮은 볼륨 (03)
  sfxVolume: 0.8,
  rankNames: ['사원', '대리', '과장', '간부', 'CEO'],
  zoneNames: ['파티션 사무실', '개방형 오피스', '텅 빈 회의실', '임원층', 'CEO실'],
};

// 구조조정 결과 화면의 팁: 마지막으로 하트를 잃은 원인(장애물 종류, 발판, 금 간 계단, 일수)에 맞춰 하나를 보여 준다
const TIPS = {
    clipboard: "결재판은 \"딱… 딱!\"의 두 번째 딱에 맞춰 막으세요",
    stapler: "스테이플러는 막으면 반대쪽에서 돌아와요. 반대쪽 키로 한 번 더 막으세요",
    airplane: "종이비행기 편대는 날아오는 쪽 키를 꾹 누르고 있으세요",
    envelope: "편지 3장은 같은 쪽에서 박자에 맞춰 와요. 한 장씩 세 번 막으세요",
    chair: "튀어 내려오는 의자는 내 칸에 내려앉을 때만 맞아요. 가만히 서서 오는 쪽 키로 막거나, 다른 칸으로 피하세요",
    reverse: "보라색 거꾸로 칸에서는 반대쪽 화살표를 눌러야 올라가요",
    vanish: "사라지는 칸은 5초 있다가 1초 없어져요. 깜박이면 기다렸다가 건너세요",
    gaze: "빨간 레이저(상사의 눈)가 내 위를 지나갈 때는 오르지 마세요. 가만히 있으면 안전해요",
    rival: "경쟁 사원이 계단을 쫓아오면 내가 올라온 쪽(뒤쪽) 키로 막으세요",
    coffee: "커피(× 표시)는 막지 마세요. 그냥 두면 지나갑니다",
    monitor: "형광등이 깜박이면 모니터! 유리 깨지는 소리 1초 뒤에 떨어집니다",
    phone: "전화기는 두 번째 벨이 울리는 순간에 막으세요",
    paper: "서류는 머리 위로 넘어가 반대쪽에서 맞습니다. 맞는 쪽 키로!",
    sticky: "포스트잇 색은 남은 횟수: 분홍5 파랑4 초록3 노랑2 흰색1",
    suitcase: "서류가방은 \"펑!\" 하고 터지는 순간에 막으세요",
    wrong: "다음 계단이 어느 쪽인지 먼저 보고 누르세요. 틀리면 하트 -1",
    cracked: "금 간 계단에서는 멈추지 마세요. 1.5초면 무너집니다",
    shock: "젖은 계단은 3초마다 감전! 불꽃이 튀면 건너기 전에 잠깐 기다리세요",
    days: "오르고 막을수록 남은 일수가 늘어납니다. 쉬지 말고 오르세요",
};
const PAL = { gray: '#5A5F66', green: '#8A9A5B', red: '#A13D3D', bg: '#1E1F22', text: '#E8E6DF' };
const STICKY_COLORS = ['white', 'white', 'yellow', 'green', 'blue', 'pink'];   // 포스트잇 남은 횟수 -> 색 (sticky 시트 변형)
const RANK_COLORS = [PAL.gray, PAL.green, PAL.red, PAL.text, PAL.text];
const W = 320, H = 180;
const STEP = 1 / 60;
const ONCE = new Set(['step', 'climb_r', 'climb_l', 'down', 'parry_hit_left', 'parry_hit_right', 'parry_hit_left2', 'parry_hit_right2', 'parry_hit_left3', 'parry_hit_right3', 'parry_left', 'parry_right', 'hit', 'box_exit', 'proud', 'attack', 'leave']);

const params = new URLSearchParams(location.search);
const BOT = params.get('bot') === '1';
const MUTE_PARAM = params.get('mute') === '1';
let SEED = params.has('seed') ? parseInt(params.get('seed'), 10) : NaN;
if (!isFinite(SEED)) SEED = (Date.now() & 0x7fffffff);

function mulberry32(a) {
  return function () {
    a |= 0; a = a + 0x6D2B79F5 | 0;
    let t = Math.imul(a ^ a >>> 15, 1 | a);
    t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const lerp = (a, b, t) => a + (b - a) * t;

/* ---------------------------------------------------------------------
 * 시험용 장치
 * ------------------------------------------------------------------- */
const assetErrors = [];
const logArr = [];
const S = window.__stairs = {
  ready: false, screen: 'title', floor: 1, rank: CONFIG.rankNames[0], hearts: CONFIG.maxHearts,
  days: CONFIG.startDays, score: 0, maxMult: 1, steps: 0,
  assetErrors: assetErrors, audioFormat: 'none', log: logArr,
  finalFloor: CONFIG.finalFloor, finalRank: CONFIG.rankNames[CONFIG.promoteFloors.length],
};
function log(msg) {
  logArr.push((run ? run.time.toFixed(2) : '0.00') + ' ' + msg);
  if (logArr.length > 500) logArr.shift();
}

/* ---------------------------------------------------------------------
 * 캔버스
 * ------------------------------------------------------------------- */
// 에셋 밀도: 2면 논리 해상도(320x180)의 2배로 그린 픽셀 아트를 절반 크기로 그린다 (sprite_definition.pixel_scale)
const PX = (window.SPRITE_DEF && window.SPRITE_DEF.pixel_scale) || 1;
const canvas = document.getElementById('game');
let ctx = canvas.getContext('2d');
let scale = 1;
function resize() {
  // 아트 픽셀이 화면 픽셀에 딱 맞도록 배율을 PX의 배수로 고른다 (창이 너무 작으면 1배)
  const k = Math.floor(Math.min(window.innerWidth / (W * PX), window.innerHeight / (H * PX)));
  scale = k >= 1 ? k * PX : Math.max(1, Math.floor(Math.min(window.innerWidth / W, window.innerHeight / H)));
  canvas.width = W * scale; canvas.height = H * scale;
  canvas.style.width = (W * scale) + 'px'; canvas.style.height = (H * scale) + 'px';
  ctx.setTransform(scale, 0, 0, scale, 0, 0);
  ctx.imageSmoothingEnabled = false;
}
window.addEventListener('resize', resize);
resize();

/* ---------------------------------------------------------------------
 * 에셋 로딩 (실패해도 멈추지 않음)
 * ------------------------------------------------------------------- */
const DEF = window.SPRITE_DEF || null;
if (!DEF) assetErrors.push('SPRITE_DEF:missing');
const D = DEF || { sprites: {}, images: {}, sounds: {} };
const ASSET_DIR = (D.assets_dir || 'assets').replace(/\/$/, '');
const SPRITE_IDS = ['player', 'rival', 'projectiles', 'warning', 'stairs', 'hud_icons', 'props', 'commuter', 'sticky', 'staff', 'obstacles'];
const IMAGE_IDS = ['bg_zone1', 'bg_zone2', 'bg_zone3', 'bg_zone4', 'bg_zone5', 'title_bg', 'logo', 'bg_lobby', 'bg_result', 'bg_practice'];
const SOUND_IDS = ['bgm_loop', 'bgm_loop_tense', 'amb_office', 'sfx_step', 'sfx_parry', 'sfx_parry_perfect', 'sfx_hit', 'sfx_warning', 'sfx_promote', 'sfx_gameover', 'sfx_click', 'sfx_phone_ring', 'sfx_cue_clipboard', 'sfx_cue_rival', 'sfx_cue_paper', 'sfx_monitor_shatter', 'sfx_light_flicker', 'sfx_cue_coffee', 'sfx_stair_crack', 'sfx_camera_flash', 'sfx_cue_suitcase', 'bgm_stage1', 'bgm_stage2', 'bgm_stage3', 'bgm_stage4', 'bgm_stage5', 'sfx_electric',
                   'sfx_cue_stapler', 'sfx_cue_sticky', 'sfx_cue_airplane', 'sfx_gaze_warn', 'sfx_gaze_laser', 'sfx_vanish', 'sfx_escalator',
                   'sfx_cue_chair', 'sfx_chair_hop', 'sfx_cue_envelope'];   // 새 장애물마다 다른 소리 (2026-11-25)
const A = { sprites: {}, images: {} };
let pending = 0;
let assetsReady = false;
function assetDone() {
  pending--;
  if (pending <= 0) { assetsReady = true; }
}
function loadImage(kind, id, def, store) {
  if (!def || !def.file) { assetErrors.push(kind + ':' + id + (def ? ':no-file' : ':no-def')); return; }
  const rec = { img: new Image(), ok: false, def: def, fw: def.frame_w || 0, fh: def.frame_h || 0 };
  store[id] = rec;
  pending++;
  rec.img.onload = function () {
    rec.ok = true;
    if (kind === 'sprite' && !(rec.fw > 0 && rec.fh > 0)) { rec.ok = false; assetErrors.push('sprite:' + id + ':no-frame-size'); }
    assetDone();
  };
  rec.img.onerror = function () { assetErrors.push(kind + ':' + id + ':' + def.file); assetDone(); };
  rec.img.src = ASSET_DIR + '/' + def.file;
}
SPRITE_IDS.forEach(function (id) { loadImage('sprite', id, D.sprites && D.sprites[id], A.sprites); });
IMAGE_IDS.forEach(function (id) { loadImage('image', id, D.images && D.images[id], A.images); });
if (pending === 0) assetsReady = true;
setTimeout(function () { assetsReady = true; }, 8000);

function rowOf(def, variant) {
  const v = def.variants;
  if (!v) return 0;
  if (Array.isArray(v)) { const i = v.indexOf(variant); return i < 0 ? 0 : i; }
  return (variant in v) ? v[variant] : 0;
}

/* drawSprite: 로드 실패 시 색 사각형(fallback) */
function drawSprite(id, variant, anim, t, x, y, o) {
  o = o || {};
  const sc = o.scale || 1;
  const rec = A.sprites[id];
  const usable = !!(rec && rec.ok && rec.fw > 0 && rec.fh > 0);
  let dw, dh;
  if (usable) { dw = o.dw || rec.fw * sc / PX; dh = o.dh || rec.fh * sc / PX; }
  else { dw = o.dw || (o.fw || 10) * sc; dh = o.dh || (o.fh || 10) * sc; }
  let dx = x, dy = y;
  const anc = o.anchor || 'bc';
  if (anc === 'bc') { dx = x - dw / 2; dy = y - dh; }
  else if (anc === 'c') { dx = x - dw / 2; dy = y - dh / 2; }
  else if (anc === 'tc') { dx = x - dw / 2; }
  dx = Math.round(dx * PX) / PX; dy = Math.round(dy * PX) / PX;
  if (!usable) {
    if (!o.nofb) { ctx.fillStyle = o.color || PAL.gray; ctx.fillRect(dx, dy, Math.round(dw), Math.round(dh)); }
    return false;
  }
  const row = rowOf(rec.def, variant);
  const an = rec.def.animations && rec.def.animations[anim];
  let col = 0;
  if (an && an.frames && an.frames.length) {
    let n = Math.floor(Math.max(0, t) * (an.fps || 1));
    if (ONCE.has(anim)) n = Math.min(n, an.frames.length - 1); else n = n % an.frames.length;
    col = an.frames[n];
  }
  if (o.rot || o.sx !== undefined || o.sy !== undefined || o.filter) {
    // 회전·눌림·필터: 그림 가운데를 기준으로 돌리고 줄인다
    ctx.save(); ctx.translate(dx + dw / 2, dy + dh / 2); ctx.rotate(o.rot || 0);
    ctx.scale((o.flip ? -1 : 1) * (o.sx === undefined ? 1 : o.sx), o.sy === undefined ? 1 : o.sy);
    if (o.filter) ctx.filter = o.filter;
    ctx.drawImage(rec.img, col * rec.fw, row * rec.fh, rec.fw, rec.fh, -dw / 2, -dh / 2, dw, dh);
    ctx.restore();
    return true;
  }
  if (o.flip) {
    ctx.save();
    ctx.translate(dx + dw, dy); ctx.scale(-1, 1);
    ctx.drawImage(rec.img, col * rec.fw, row * rec.fh, rec.fw, rec.fh, 0, 0, dw, dh);
    ctx.restore();
  } else {
    ctx.drawImage(rec.img, col * rec.fw, row * rec.fh, rec.fw, rec.fh, dx, dy, dw, dh);
  }
  return true;
}
function drawImg(id, x, y, w, h, fallback) {
  const rec = A.images[id];
  if (rec && rec.ok) { ctx.drawImage(rec.img, x, y, w, h); return true; }
  if (fallback) { ctx.fillStyle = fallback; ctx.fillRect(x, y, w, h); }
  return false;
}
function spScale(id, maxH) {
  const rec = A.sprites[id];
  if (!rec || !rec.ok) return 2;
  return Math.max(1, Math.floor(maxH / (rec.fh / PX)));
}

/* ---------------------------------------------------------------------
 * 사운드 (첫 입력 이후 재생, ogg 불가 시 mp3)
 * ------------------------------------------------------------------- */
const Snd = {
  unlocked: false, muted: MUTE_PARAM, vol: 1, format: 'none', loops: {}, pools: {}, rr: {},
};
(function detectFormat() {
  try {
    const a = document.createElement('audio');
    if (a.canPlayType('audio/ogg; codecs="vorbis"')) Snd.format = 'ogg';
    else if (a.canPlayType('audio/mpeg')) Snd.format = 'mp3';
  } catch (e) { Snd.format = 'none'; }
  S.audioFormat = Snd.format;
})();
function makeAudio(id) {
  if (Snd.format === 'none') return null;
  const d = D.sounds && D.sounds[id];
  if (!d || !d.files) { assetErrors.push('sound:' + id + (d ? ':no-files' : ':no-def')); return null; }
  const order = Snd.format === 'mp3' ? ['mp3', 'ogg'] : ['ogg', 'mp3'];
  const srcs = [];
  order.forEach(function (k) { if (d.files[k]) srcs.push(ASSET_DIR + '/' + d.files[k]); });
  if (!srcs.length) { assetErrors.push('sound:' + id + ':no-files'); return null; }
  const a = new Audio();
  a.preload = 'auto'; a.loop = !!d.loop;
  let i = 0;
  a.addEventListener('error', function () {
    if (i + 1 < srcs.length) { i++; a.src = srcs[i]; }
    else { a._failed = true; assetErrors.push('sound:' + id + ':' + srcs[i]); }
  });
  a.src = srcs[0];
  return a;
}
function loopEl(id) {
  if (!(id in Snd.loops)) Snd.loops[id] = makeAudio(id);
  return Snd.loops[id];
}
function sfx(id) {
  if (!Snd.unlocked || Snd.muted) return;
  try {
    if (!Snd.pools[id]) { Snd.pools[id] = [makeAudio(id), makeAudio(id), makeAudio(id)]; Snd.rr[id] = 0; }
    const pool = Snd.pools[id];
    const a = pool[Snd.rr[id] = (Snd.rr[id] + 1) % pool.length];
    if (!a || a._failed) return;
    a.volume = clamp(CONFIG.sfxVolume * Snd.vol, 0, 1);
    try { a.currentTime = 0; } catch (e) {}
    const p = a.play(); if (p && p.catch) p.catch(function () {});
  } catch (e) {}
}
let audioKey = null, audioSyncT = 0;
// 구간(20층)마다 다른 음악: 1~20 사무실, 21~40 개방형, 41~60 회의실, 61~80 임원층, 81~100 CEO실 (없으면 기존 루프)
const STAGE_BGM = ['bgm_stage1', 'bgm_stage2', 'bgm_stage3', 'bgm_stage4', 'bgm_stage5'];
function desiredBgm() {
  if (screen === 'result') return null;
  if ((screen === 'play' || screen === 'pause') && run) {
    const id = STAGE_BGM[Math.min(4, Math.floor((run.floor - 1) / 20))];   // 배경 구간(61~99층 한 구간)과 달리 음악은 20층마다
    if (D.sounds && D.sounds[id]) return id;
    if (run.floor >= CONFIG.tenseFloor) return 'bgm_loop_tense';
  }
  return 'bgm_loop';
}
function syncAudio(force) {
  if (!Snd.unlocked) return;
  const key = desiredBgm() + '|' + Snd.muted + '|' + Snd.vol + '|' + (screen === 'pause');
  if (!force && key === audioKey && time - audioSyncT < 1) return;
  audioKey = key; audioSyncT = time;
  const want = {};
  if (!Snd.muted) {
    const b = desiredBgm();
    if (b) want[b] = CONFIG.bgmVolume * (screen === 'pause' ? 0.4 : 1);
    want.amb_office = CONFIG.ambVolume;
  }
  ['bgm_loop', 'bgm_loop_tense', 'amb_office'].concat(STAGE_BGM).forEach(function (id) {
    if (id.indexOf('bgm_stage') === 0 && !(id in want) && !(id in Snd.loops)) return;   // 안 쓴 구간 음악은 만들지 않는다
    const a = loopEl(id);
    if (!a || a._failed) return;
    if (id in want) {
      a.volume = clamp(want[id] * Snd.vol, 0, 1);
      if (a.paused) { const p = a.play(); if (p && p.catch) p.catch(function () {}); }
    } else if (!a.paused) a.pause();
  });
}
function unlockAudio() {
  if (Snd.unlocked) return;
  Snd.unlocked = true;
  syncAudio(true);
}

/* ---------------------------------------------------------------------
 * 저장
 * ------------------------------------------------------------------- */
const save = { floor: 1, score: 0, endless: 0 };
try {
  save.endless = parseInt(localStorage.getItem('stairs_best_endless'), 10) || 0;   // 무한 모드 최고 층은 따로
  save.floor = parseInt(localStorage.getItem('stairs_best_floor'), 10) || 1;
  save.score = parseInt(localStorage.getItem('stairs_best_score'), 10) || 0;
} catch (e) {}
function writeSave() {
  try {
    localStorage.setItem('stairs_best_floor', String(save.floor));
    localStorage.setItem('stairs_best_score', String(save.score));
    localStorage.setItem('stairs_best_endless', String(save.endless));
  } catch (e) {}
}

/* ---------------------------------------------------------------------
 * 상태
 * ------------------------------------------------------------------- */
let screen = 'title';
let time = 0;
let focus = 0;
let run = null;
let result = null;
let resultLock = 0;
let settingsOpen = false;
let helpOpen = false; // 조작법 화면
let QA_HOOK = null;   // QA: 그리기 자리를 알린다 (평소에는 null. tools/qa_physics.js가 쓴다)
let demo = null;      // 장애물 보기 (조작법·결과 화면에서 연다)
let runCount = 0;
let toastText = '', toastUntil = 0;
const padUntil = [0, 0, 0, 0]; // ←, →, Q, E 키 아이콘 발광
let st = null; // 계단 생성기

function newStairs(seed) {
  st = { rng: mulberry32(seed ^ 0x9e3779b1), dirs: [], x: [0], runLen: 0 };
}
function ensureStairs(n) {
  while (st.dirs.length < n) {
    let d;
    if (st.dirs.length === 0) { d = st.rng() < 0.5 ? -1 : 1; st.runLen = 1; }
    else {
      const last = st.dirs[st.dirs.length - 1];
      if (st.runLen >= CONFIG.stairRunMax || st.rng() >= CONFIG.stairKeepProb) { d = -last; st.runLen = 1; }
      else { d = last; st.runLen++; }
    }
    st.dirs.push(d);
    st.x.push(st.x[st.x.length - 1] + d * CONFIG.stepW);
  }
}
function dirAt(i) { ensureStairs(i + 1); return st.dirs[i]; }
function stairX(i) { ensureStairs(i + 1); return st.x[i]; }
function floorOfSteps(n) { return 1 + Math.floor(n / CONFIG.stairsPerFloor); }
function zoneOf(f) { return f <= 20 ? 1 : f <= 40 ? 2 : f <= 60 ? 3 : f <= 99 ? 4 : 5; }

// mode: 'normal'(1~100층), 'endless'(무한: 1층부터 모든 장애물, 끝없음), 'practice'(연습: 제자리에서 막기만, 시간 제한·게임 오버 없음)
// 한 판의 상태를 새로 만든다 (계단 생성기 st도 새로). startRun과 장애물 보기(demo)가 같이 쓴다
function makeRun(seed, mode) {
  newStairs(seed);
  const r = {
    seed: seed, rng: mulberry32(seed), decor: mulberry32(seed ^ 0x5bd1e995),
    time: 0, steps: 0, floor: 1, rankIdx: 0,
    hearts: CONFIG.maxHearts, days: CONFIG.startDays,
    score: 0, combo: 0, mult: 1, maxMult: 1, maxCombo: 0, parries: 0, lastKind: null,
    nextAttack: CONFIG.firstAttackDelay, lastImpact: -9,
    projs: [], sparks: [], texts: [], leavers: [],
    parryStart: [-9, -9], parryEnd: [-9, -9], parryCd: [0, 0],
    hitTimer: 0, stepTimer: 0, lastStepT: -9, downT: -9, parryHitT: -9, parryHitSide: 0, parryVar: 0, shake: 0, flash: 0,   // lastStepT: 마지막으로 오른 때 (그리기용)
    pvx: 0, pvy: 0, camX: 0, camY: 0,
    facing: dirAt(0), animState: '', animStart: 0,
    botT: 0, ended: null, endTimer: 0, rivalIdx: 8,
    crackT: 0, nextFlash: CONFIG.flashGap, camFlash: null, shockCycle: -1,
    mode: mode, hits: 0, forceType: null,
    escT: 0, gazeT0: null, gazeHitCycle: -1, gazeKey: -1, vanishPhase: 0, held: [false, false], fallers: [],   // fallers: 계단 장애물에 당한 경쟁 사원   // 새 장애물 상태, held = Q·E를 누르고 있는지
  };
  if (mode === 'practice') {                   // 연습: 층 바닥이 있는 칸에 서서 시작, 카메라도 바로 그 자리
    r.steps = (CONFIG.practiceFloor - 1) * CONFIG.stairsPerFloor; r.floor = CONFIG.practiceFloor;
    r.pvx = r.camX = stairX(r.steps); r.pvy = r.camY = -r.steps * CONFIG.stepH; r.facing = dirAt(r.steps);
  }
  return r;
}
function startRun(mode) {
  if (typeof mode !== 'string') mode = 'normal';
  const seed = SEED + runCount; runCount++;
  run = makeRun(seed, mode);
  screen = 'play'; focus = 0; settingsOpen = false;
  log('run start seed=' + seed + ' mode=' + mode);
}

/* ---------------------------------------------------------------------
 * 게임 규칙
 * ------------------------------------------------------------------- */
function diffT(r) {   // 보통은 20층 구간마다, 무한은 60층에 걸쳐 끝까지, 연습은 가운데 값으로 고정
  if (r.mode === 'endless') return clamp((r.floor - 1) / CONFIG.endlessRampFloors, 0, 1);
  if (r.mode === 'practice') return CONFIG.practiceDiff;
  return clamp(((r.floor - 1) % 20) / 19, 0, 1);
}   // 승급 구간(20층)마다 다시 쉬워진다
function drainMult(r) { return lerp(CONFIG.drainStart, CONFIG.drainEnd, diffT(r)); }
function attackInterval(r) {
  const base = lerp(CONFIG.attackIntervalStart, CONFIG.attackIntervalEnd, diffT(r));
  return base * (1 + (r.rng() * 2 - 1) * CONFIG.attackJitter);
}
function addText(s, x, y, color) { run.texts.push({ s: s, x: x, y: y, t: 0, color: color || PAL.text }); }
function playerScreenX() { return Math.round(160 + (run.pvx - run.camX)); }
const PLAYER_Y = 128;

function keyStep(dir) {
  padUntil[dir < 0 ? 0 : 1] = time + 0.12;
  doStep(dir);
}
function keyParry(side) {
  padUntil[side < 0 ? 2 : 3] = time + 0.12;
  doParry(side);
}
// 금 간 계단: 21층부터 일부 칸 (층이 바뀌는 칸은 빼고, 칸 번호로 정해져 매번 같다)
// 젖은 계단(WET HAZARD): 30층부터 일부 칸 (층이 바뀌는 칸, 금 간 칸은 빼고, 칸 번호로 정해져 매번 같다)
function isWet(i) {
  if (run && run.mode === 'practice') return false;   // 연습 모드에는 계단 장애물이 없다
  const from = run && run.mode === 'endless' ? 3 : CONFIG.wetFromFloor;
  if (floorOfSteps(i) < from || i % CONFIG.stairsPerFloor === 0 || isCracked(i)) return false;
  return (((i * 2246822519) >>> 0) % 1000) / 1000 < CONFIG.wetShare;
}
// 전기 상태: live(지금 흐름), warn(곧 흐름, 불꽃), shockIn(다음에 흐르기까지 초)
function shockState(t) {
  const P = CONFIG.shockPeriod, ph = t % P, start = P - CONFIG.shockTime;
  return { live: ph >= start, warn: ph >= start - CONFIG.shockWarn && ph < start, shockIn: ph >= start ? 0 : start - ph, cycle: Math.floor(t / P) };
}
function isCracked(i) {
  if (run && run.mode === 'practice') return false;   // 연습 모드에는 계단 장애물이 없다
  const from = run && run.mode === 'endless' ? 2 : CONFIG.crackFromFloor;   // 무한 모드는 2층부터
  if (floorOfSteps(i) < from || i % CONFIG.stairsPerFloor === 0) return false;
  return (((i * 2654435761) >>> 0) % 1000) / 1000 < CONFIG.crackShare;
}
// 내려가는 에스컬레이터: 20층부터 일부 칸 (층이 바뀌는 칸, 금 간·젖은 칸은 빼고, 칸 번호로 정해진다)
function isEscalator(i) {
  if (run && run.mode === 'practice') return false;
  const from = run && run.mode === 'endless' ? 4 : CONFIG.escalatorFromFloor;
  if (floorOfSteps(i) < from || i % CONFIG.stairsPerFloor === 0 || isCracked(i) || isWet(i)) return false;
  return (((i * 3266489917) >>> 0) % 1000) / 1000 < CONFIG.escalatorShare;
}
// 사라지는 칸: 41층부터 일부 칸 (층이 바뀌는 칸, 다른 장애물 칸은 빼고). 모두 같은 박자로 5초 있고 1초 없어진다
function isVanish(i) {
  if (run && run.mode === 'practice') return false;
  const from = run && run.mode === 'endless' ? 5 : CONFIG.vanishFromFloor;
  if (floorOfSteps(i) < from || i % CONFIG.stairsPerFloor === 0 || isCracked(i) || isWet(i) || isEscalator(i)) return false;
  return (((i * 668265263) >>> 0) % 1000) / 1000 < CONFIG.vanishShare;
}
// 거꾸로 칸: 35층부터 일부 칸 (층이 바뀌는 칸, 다른 계단 장애물 칸은 빼고). 그 칸에서는 반대쪽 화살표를 눌러야 오른다
function isReverse(i) {
  if (run && run.mode === 'practice') return false;
  const from = run && run.mode === 'endless' ? 6 : CONFIG.reverseFromFloor;
  if (floorOfSteps(i) < from || i % CONFIG.stairsPerFloor === 0 || isCracked(i) || isWet(i) || isEscalator(i) || isVanish(i)) return false;
  return (((i * 1597334677) >>> 0) % 1000) / 1000 < CONFIG.reverseShare;
}
function needDir(i) { return isReverse(i) ? -dirAt(i) : dirAt(i); }   // i번 칸에서 오르려면 눌러야 하는 쪽
function vanishState(r) {
  const show = CONFIG.vanishShow, P = show + CONFIG.vanishHide, ph = (r.time + r.vanishPhase) % P;
  return { gone: ph >= show, warn: ph >= show - CONFIG.vanishWarn && ph < show, goneIn: ph < show ? show - ph : 0, backIn: ph >= show ? P - ph : 0 };
}
// 상사의 눈: 61층에 처음 닿은 때부터 (예고 5초 → 훑기 1.5초 → 쉬기)를 되풀이. 멀리서 쏜 레이저 띠가 화면 위에서 아래로 내려온다 (y = 띠 가운데, 화면 좌표)
const GAZE_BODY_Y = PLAYER_Y - 14, GAZE_BODY_H = 14;   // 플레이어 몸 가운데와 반 높이 (레이저가 몸에 걸쳤는지)
const GAZE_SRC_Y = 50;                                 // 레이저가 시작되는 먼 불꽃의 높이 (화면 좌표). 번갈아 왼쪽·오른쪽
function gazeSrcX(cycle) { return cycle % 2 ? 252 : 68; }
function gazeState(r) {
  if (r.mode === 'practice' || r.gazeT0 === null) return null;
  const P = CONFIG.gazeWarn + CONFIG.gazeSweep + CONFIG.gazeRest, e = r.time - r.gazeT0;
  const ph = e % P, cycle = Math.floor(e / P);
  if (ph < CONFIG.gazeWarn) return { warn: true, left: CONFIG.gazeWarn - ph, cycle: cycle };
  if (ph < CONFIG.gazeWarn + CONFIG.gazeSweep) {
    const u = (ph - CONFIG.gazeWarn) / CONFIG.gazeSweep, half = CONFIG.gazeWidth / 2;
    return { sweep: true, u: u, y: lerp(GAZE_SRC_Y, H + half, u), cycle: cycle };   // 불꽃 높이에서 화면 아래 끝까지
  }
  return { rest: true, cycle: cycle };
}
function gazeOnPlayer(r) { const g = gazeState(r); return !!(g && g.sweep && Math.abs(g.y - GAZE_BODY_Y) < CONFIG.gazeWidth / 2 + GAZE_BODY_H); }
function doStep(dir) {
  const r = run;
  if (!r || r.ended || screen !== 'play' || r.mode === 'practice') return;   // 연습: 계단을 오르지 않는다
  const need = needDir(r.steps);                 // 거꾸로 칸에서는 반대쪽
  if (dir !== need) {
    log('wrong direction');
    r.facing = dirAt(r.steps);
    r.lastKind = isReverse(r.steps) ? 'reverse' : 'wrong'; loseHeart('wrong');
    return;
  }
  if (gazeOnPlayer(r) && r.gazeHitCycle !== gazeState(r).cycle) {   // 상사의 눈이 나를 비추는 동안 움직였다
    r.gazeHitCycle = gazeState(r).cycle;
    log('gaze caught'); r.lastKind = 'gaze';
    addText('들켰다!', playerScreenX(), PLAYER_Y - 30, PAL.red);
    loseHeart('gaze');
    if (r.ended) return;
  }
  r.steps++; r.crackT = 0; r.escT = 0;
  if (isCracked(r.steps)) { sfx('sfx_stair_crack'); log('crack step'); }   // 금 간 계단에 올라섰다
  r.days = Math.min(CONFIG.maxDays, r.days + CONFIG.stepDays);
  r.score += CONFIG.stepPoints * r.mult;
  r.stepTimer = 0.14; r.lastStepT = r.time;
  r.facing = dirAt(r.steps);
  sfx('sfx_step');
  const nf = floorOfSteps(r.steps);
  if (nf > r.floor) {
    r.floor = nf;
    r.score += CONFIG.floorPoints * r.mult;
    log('floor ' + nf);
    if (r.hearts < CONFIG.maxHearts) {  // 새 층 도착: 하트 회복 (층 표시와 한 줄로 보여 겹치지 않게)
      r.hearts = Math.min(CONFIG.maxHearts, r.hearts + CONFIG.floorHeal);
      addText(nf + 'F · 하트 +1', 160, 70, PAL.green);
      log('heart +1 (floor) hearts=' + r.hearts);
    } else {
      addText(nf + 'F', 160, 70, PAL.text);
    }
    if (CONFIG.promoteFloors.indexOf(nf) >= 0) promote(nf);
    if (r.mode === 'normal' && nf >= CONFIG.finalFloor && !r.ended) { sfx('sfx_promote'); log('finish ' + nf + 'F'); endRun('promote', 'floor' + nf); }   // 90층: PoC 마무리
  }
}
function doParry(side) {
  const r = run;
  if (!r || r.ended || screen !== 'play') return;
  const s = side < 0 ? 0 : 1;
  if (r.time < r.parryCd[s]) return;
  r.parryStart[s] = r.time;
  r.parryEnd[s] = r.time + CONFIG.parryWindow;
  r.parryCd[s] = r.parryEnd[s] + CONFIG.parryCooldown;
  r.parryVar = (r.parryVar + 1) % 3;   // 막기 동작: 누를 때마다 곧게 막기 → 두 손 밀기 → 위로 쳐내기 (그리기용)
}
function loseHeart(reason) {
  const r = run;
  if (r.mode === 'practice') {                 // 연습: 하트를 잃지 않고 맞은 수만 센다
    r.hits++; r.combo = 0; r.mult = 1; r.hitTimer = 0.35; r.shake = 0.25; r.flash = 0.35;
    sfx('sfx_hit'); log('hit (' + reason + ') practice'); return;
  }
  r.hearts--; r.combo = 0; r.mult = 1;
  r.hitTimer = 0.35; r.shake = 0.25; r.flash = 0.35;
  sfx('sfx_hit');
  log('heart -1 (' + reason + ') hearts=' + r.hearts);
  if (r.hearts <= 0) { r.hearts = 0; sfx('sfx_gameover'); endRun('gameover', 'hearts'); }
}
function promote(f) {
  const r = run;
  r.hearts = Math.min(CONFIG.maxHearts, r.hearts + CONFIG.promoteHeal);
  r.rankIdx = Math.min(CONFIG.rankNames.length - 1, r.rankIdx + 1);
  sfx('sfx_promote');
  log('promote -> ' + CONFIG.rankNames[r.rankIdx] + ' (' + f + 'F)');
  if (f >= CONFIG.finalFloor) { endRun('promote', 'floor' + f); return; }
  // 20·40·60·80층: 승급 문구를 띄우고 계속 오른다. 날아오던 장애물은 그대로 둔다 (결정 2026-11-20)
  addText('승급! ' + CONFIG.rankNames[r.rankIdx - 1] + ' → ' + CONFIG.rankNames[r.rankIdx], 160, 56, PAL.green);
}
function endRun(kind, cause) {
  const r = run;
  r.ended = { kind: kind, cause: cause };
  r.endTimer = CONFIG.endDelay;
  r.projs.forEach(function (p) { p.done = true; });
  if (kind === 'gameover') log('gameover ' + cause);
}
// 공격 종류: 결재판(기본), 경쟁 사원(11층~), 전화기(31층~), 서류(41층~)
function pickType(r) {
  r = r.mode === 'normal' ? r : Object.assign({}, r, { floor: 999 });   // 무한·연습: 처음부터 모든 공격
  const w = [['clipboard', 1], ['rival', r.floor >= CONFIG.rivalFromFloor ? CONFIG.weightRival : 0],
             ['phone', r.floor >= CONFIG.phoneFromFloor ? CONFIG.weightPhone : 0], ['paper', r.floor >= CONFIG.paperFromFloor ? CONFIG.weightPaper : 0],
             ['monitor', r.floor >= CONFIG.monitorFromFloor ? CONFIG.weightMonitor : 0],
             ['coffee', r.floor >= CONFIG.coffeeFromFloor ? CONFIG.weightCoffee : 0], ['sticky', r.floor >= CONFIG.stickyFromFloor ? CONFIG.weightSticky : 0],
             ['suitcase', r.floor >= CONFIG.suitcaseFromFloor ? CONFIG.weightSuitcase : 0],
             ['stapler', r.floor >= CONFIG.staplerFromFloor ? CONFIG.weightStapler : 0], ['airplane', r.floor >= CONFIG.airplaneFromFloor ? CONFIG.weightAirplane : 0],
             ['envelope', r.floor >= CONFIG.envelopeFromFloor ? CONFIG.weightEnvelope : 0], ['chair', r.floor >= CONFIG.chairFromFloor ? CONFIG.weightChair : 0]];
  let x = r.rng() * w.reduce(function (s, e) { return s + e[1]; }, 0);
  for (let i = 0; i < w.length; i++) { if (x < w[i][1]) return w[i][0]; x -= w[i][1]; }
  return 'clipboard';
}
function spawnAttack() {
  const r = run;
  const side = r.rng() < 0.5 ? -1 : 1;
  const picked = pickType(r), type = r.forceType || picked, rival = type === 'rival';   // forceType: 장애물 보기에서만 쓴다
  let impact = r.time + CONFIG.warnTime + (rival ? CONFIG.rivalWalk : 0) + (type === 'monitor' ? CONFIG.flickerTime : 0);
  if (impact - r.lastImpact < CONFIG.minAttackGap) impact = r.lastImpact + CONFIG.minAttackGap;
  r.lastImpact = impact;
  // side는 맞는 쪽(패링 키). 경쟁 사원은 가장 가까운 층 바닥(floorStep)을 걸어 들어와 멈춘 뒤 "!" 1.0초 후 뛰어올라(내려와) 근접 공격.
  // 서류는 반대쪽(from)에서 던져져 머리 위로 넘어온다
  // 경쟁 사원은 플레이어가 그 바닥보다 높이 올라가 있으면(또는 올라가면) 계단 아래 끝에서 계단을 따라 쫓아 올라와 바로 뒤 칸에서 친다.
  // 이때 막는 쪽은 경쟁 사원이 있는 쪽 = 플레이어가 마지막으로 오른 방향의 반대쪽이다 (치기 직전 0.3초에 정해짐)
  const p = { side: side, from: type === 'paper' ? -side : side, type: type,
              enterT: impact - CONFIG.warnTime - (rival ? CONFIG.rivalWalk : 0), spawnT: impact - CONFIG.warnTime, impactT: impact, done: false,
              // 모니터는 형광등이 먼저 깜박인 뒤(warnT~spawnT) 유리가 깨지며 나타난다. spin·wob·ph는 불규칙한 회전 값
              warnT: r.time, spin: (r.decor() < 0.5 ? -1 : 1) * (4 + r.decor() * 6), wob: 1.5 + r.decor() * 3, ph: r.decor() * 6.28,
              hp: type === 'sticky' ? 1 + Math.floor(r.rng() * CONFIG.stickyMax) : 1,
              debris: r.decor() < 0.5 ? 'document' : 'clipboard',   // 서류가방에서 쏟아질 것
              returned: false, k: 0,                                // 스테이플러: 돌아왔는지, 종이비행기·편지: 지나간 수
              passed: false, hopped: false, cueK: 0, fallT: null,
              ax: stairX(r.steps), ay: -r.steps * CONFIG.stepH, s0: r.steps };   // 정해진 길: 생길 때 플레이어가 서 있던 자리(world)로 날아간다 (따라가지 않는다)   // 의자: 머리 위로 넘어가는지
  if (type === 'chair') {   // 의자: 위 계단(오를 쪽)에서 생길 때 플레이어 칸의 계단 줄을 따라 내려온다 (정해진 길)
    p.side = p.from = dirAt(r.steps); p.spawnT = p.impactT - 3 * CONFIG.chairHop;
    p.up = 0; while (p.up < 6 && dirAt(r.steps + p.up) === p.side) p.up++;                          // 위로 같은 줄에 이어진 칸 수
    p.down = 0; while (p.down < 8 && r.steps - 1 - p.down >= 0 && dirAt(r.steps - 1 - p.down) === p.side) p.down++;   // 아래로
  }
  if (rival) Object.assign(p, { locked: false, phase: 'walk', wx: r.camX + side * (W / 2 + 16), ri: 0,
                                floorStep: Math.round(r.steps / CONFIG.stairsPerFloor) * CONFIG.stairsPerFloor });   // 가장 가까운 층 바닥(위 또는 아래)
  r.projs.push(p);
  log(type + ' spawn');
  if (type === 'monitor') sfx('sfx_light_flicker');     // 형광등 깜박이는 소리 (유리 깨짐은 spawnT에)
  else if (type !== 'phone' && type !== 'coffee') sfx('sfx_warning');   // 커피는 경고음 없이 높은 "헤이!"만
}
// 경쟁 사원 위치(월드): 걷기·서 있기는 층 바닥 위(wx), 오르기는 계단 칸(ri, 소수)
function rivalPos(p) {
  if (p.phase !== 'climb') return { x: p.wx, y: -p.floorStep * CONFIG.stepH };
  const i = Math.floor(p.ri), f = p.ri - i;
  return { x: lerp(stairX(i), stairX(i + 1), f), y: -p.ri * CONFIG.stepH };
}
function updateRival(p, dt) {
  const r = run;
  if (p.done) return;
  const above = r.steps > p.floorStep;
  if (p.phase === 'walk' || p.phase === 'stand') {
    // 플레이어가 바닥보다 위면 계단 아래 끝으로, 아니면 플레이어 옆으로 걷는다
    const tx = above ? stairX(p.floorStep) : stairX(r.steps) + p.side * 14, d = tx - p.wx;
    p.wx += Math.sign(d) * Math.min(Math.abs(d), CONFIG.rivalSpeed * dt);
    p.phase = Math.abs(tx - p.wx) < 0.5 ? (above ? 'climb' : 'stand') : 'walk';
    if (p.phase === 'climb') p.ri = p.floorStep;
  } else if (p.ri < r.steps - 1) {
    // 계단을 따라 쫓아 오른다. 다음 칸이 사라졌거나(곧 사라지거나) 전기가 흐르면(곧 흐르면) 그 앞 칸에서 기다린다
    let nr = Math.min(r.steps - 1, p.ri + CONFIG.rivalClimb * dt);
    const c = Math.floor(nr + 1e-6);
    const was = p.waiting;
    p.waiting = false;
    if (c > Math.floor(p.ri + 1e-6) && nr > c + 1e-9 && rivalBlocked(r, c + 1)) { nr = c; p.waiting = true; }
    else if (Math.abs(p.ri - Math.round(p.ri)) < 1e-6 && rivalBlocked(r, Math.round(p.ri) + 1)) { nr = p.ri; p.waiting = true; }
    if (p.waiting && !was) log('rival wait ' + (Math.round(nr) + 1));
    p.ri = nr;
  } else p.waiting = false;
  if (p.phase === 'climb' && rivalTileHazards(p, dt)) return;      // 계단 장애물에 당했다 (떨어짐·감전)
  const ready = p.phase === 'stand' || (p.phase === 'climb' && p.ri >= r.steps - 1 - 1e-6);
  if (p.locked) return;
  if (p.phase === 'climb' && ready) p.side = -dirAt(r.steps - 1);   // 바로 뒤 칸: 플레이어가 온 쪽
  if (r.time >= p.impactT - CONFIG.rivalLeap) {
    if (ready) { p.locked = true; log('rival lock ' + (p.phase === 'climb' ? 'stairs' : 'floor')); }
    else p.impactT = r.time + CONFIG.rivalLeap;                    // 아직 따라잡지 못했다: 붙을 때까지 치지 않는다
  }
}
// 경쟁 사원 × 계단 장애물
function rivalBlocked(r, n) {   // 경쟁 사원이 n번 칸에 올라서지 않고 기다리는지
  const vs = vanishState(r), sh = shockState(r.time);
  return (isVanish(n) && (vs.gone || vs.goneIn < CONFIG.rivalVanishWait)) || (isWet(n) && (sh.live || sh.warn));
}
function rivalTileHazards(p, dt) {   // 경쟁 사원이 서 있는 칸의 장애물. 당하면 true (그 경쟁 사원은 치지 못하고 사라진다)
  const r = run, i = Math.round(p.ri);
  if (Math.abs(p.ri - i) > 1e-6) { p.crackT = 0; p.escT = 0; return false; }   // 칸 사이를 지나는 중
  let kind = null;
  if (isWet(i) && shockState(r.time).live) kind = 'shock';
  else if (isVanish(i) && vanishState(r).gone) kind = 'vanish';
  if (isCracked(i)) { p.crackT = (p.crackT || 0) + dt; if (!kind && p.crackT >= CONFIG.rivalCrackTime) kind = 'cracked'; } else p.crackT = 0;
  if (isEscalator(i)) {                                    // 에스컬레이터: 서 있으면 경쟁 사원도 밀려 내려간다
    p.escT = (p.escT || 0) + dt;
    if (p.escT >= CONFIG.escalatorPush && i > p.floorStep) { p.escT = 0; p.ri = i - 1; log('rival pushed'); sfx('sfx_escalator'); p.downT = r.time; }
  } else p.escT = 0;
  if (!kind) return false;
  p.done = true;
  const rp = rivalPos(p);
  r.fallers.push({ wx: rp.x, wy: rp.y, t0: r.time, kind: kind, side: p.side });
  r.score += CONFIG.parryPoints;                           // 덤 점수 (콤보는 그대로)
  log(kind === 'shock' ? 'rival shock' : 'rival fall ' + kind);
  sfx(kind === 'shock' ? 'sfx_electric' : kind === 'vanish' ? 'sfx_vanish' : 'sfx_stair_crack');
  addText(kind === 'shock' ? '경쟁 사원 감전!' : '경쟁 사원 추락!', 160 + (rp.x - r.camX), PLAYER_Y + (rp.y - r.camY) - 30, PAL.green);
  return true;
}
function botTick(dt) {
  const r = run;
  r.botT += dt;
  // 봇: 다음 칸이 젖었고 전기가 흐르는 중이거나 곧 흐르면 건너지 않고 기다린다
  const sh = shockState(r.time), wetAhead = isWet(r.steps + 1) && (sh.live || sh.shockIn < 0.3);
  // 기다릴지 정하기 (끝나는 것 > 하트 -1 > 기다림 순으로 피한다):
  //   다음 칸이 끝나는 칸(전기가 흐르거나 곧 흐르는 젖은 칸)이면 기다린다
  //   지금 칸이 끝나는 칸(곧 전기가 흐르는 젖은 칸, 무너지기 직전의 금 간 칸)이면 오른다
  //   다음 사라지는 칸들(이어진 것까지)을 다 건널 시간이 없으면 기다린다
  //   상사의 레이저가 내 몸 위쪽 가까이 내려왔거나 몸에 걸쳐 있으면 기다린다 (지금 칸이 곧 사라지면 빼고)
  const gz = gazeState(r), vs = vanishState(r);
  let vrun = 0; while (vrun < 6 && isVanish(r.steps + 1 + vrun)) vrun++;
  const vanishBad = vrun > 0 && (vs.gone || vs.goneIn < 0.2 + vrun * CONFIG.botStepInterval);
  const hereFatal = (isWet(r.steps) && !sh.live && sh.shockIn < 0.45) || (isCracked(r.steps) && r.crackT > 1.0);
  const hereVanishSoon = isVanish(r.steps) && vs.goneIn < 0.45;
  const gazeNear = !!(gz && gz.sweep && gz.y < GAZE_BODY_Y + CONFIG.gazeWidth / 2 + GAZE_BODY_H + 4);   // 빨라서 훑는 동안은 몸을 지나갈 때까지 기다린다
  const wait = wetAhead || (!hereFatal && (vanishBad || (gazeNear && !hereVanishSoon)));
  if (r.botT >= CONFIG.botStepInterval) { if (wait) r.botT = CONFIG.botStepInterval; else { r.botT -= CONFIG.botStepInterval; keyStep(needDir(r.steps)); } }
  if (r.ended) return;
  // 종이비행기 편대: 첫 비행기 도착 botParryLead 전부터 날아오는 쪽 키를 누르고 있는다
  r.held = [false, false];
  r.projs.forEach(function (p) { if (p.type === 'airplane' && !p.done && r.time >= p.impactT - CONFIG.botParryLead) r.held[p.side < 0 ? 0 : 1] = true; });
  r.projs.forEach(function (p) {
    if (p.done || p.type === 'airplane') return;
    const ch = p.type === 'chair' ? chairHitT(r, p) : null; if (p.type === 'chair' && !ch) return;   // 의자: 내 칸에 내려앉을 때만 막는다
    const tti = (ch ? ch.t : p.type === 'envelope' ? p.impactT + p.k * CONFIG.envelopeGap : p.impactT) - r.time;
    const s = p.side < 0 ? 0 : 1;
    if (p.type !== 'coffee' && p.locked !== false && tti > 0 && tti <= CONFIG.botParryLead && r.time >= r.parryCd[s]) keyParry(p.side);
  });
}
function updatePlay(dt) {
  const r = run;
  r.time += dt;
  r.hitTimer = Math.max(0, r.hitTimer - dt);
  r.stepTimer = Math.max(0, r.stepTimer - dt);
  r.shake = Math.max(0, r.shake - dt);
  r.flash = Math.max(0, r.flash - dt);
  const tx = stairX(r.steps), ty = -r.steps * CONFIG.stepH;
  const k1 = Math.min(1, dt * 22);
  r.pvx += (tx - r.pvx) * k1; r.pvy += (ty - r.pvy) * k1;
  r.camX += (r.pvx - r.camX) * Math.min(1, dt * 8); r.camY = r.pvy;
  r.sparks.forEach(function (s) { s.x += s.vx * dt; s.y += s.vy * dt; s.vy += 120 * dt; s.t += dt; });
  r.sparks = r.sparks.filter(function (s) { return s.t < 0.4; });
  r.texts.forEach(function (t) { t.t += dt; t.y -= 14 * dt; });
  r.texts = r.texts.filter(function (t) { return t.t < 0.8; });
  r.leavers = r.leavers.filter(function (l) { return r.time - l.t0 < 0.5; });
  r.fallers = r.fallers.filter(function (f) { return r.time - f.t0 < 0.8; });
  if (CONFIG.showRivalCameo && r.rivalIdx < r.steps - 3) r.rivalIdx = r.steps + 8 + Math.floor(r.decor() * 6);

  if (r.ended) {
    r.endTimer -= dt;
    if (r.endTimer <= 0) finishRun();
    return;
  }
  if (BOT && !demo) botTick(dt);   // 장애물 보기는 자기 자동 조작을 쓴다
  if (r.ended) return;

  if (r.mode !== 'practice') r.days -= dt / CONFIG.dayDuration * drainMult(r);   // 연습: 시간 제한 없음
  const sh = shockState(r.time);                // 젖은 계단: 전기가 흐르는 순간 그 칸에 있으면 감전
  if (sh.live && sh.cycle !== r.shockCycle) {
    r.shockCycle = sh.cycle;
    let near = false;
    for (let i = Math.max(0, r.steps - 8); i < r.steps + 14 && !near; i++) near = isWet(i);
    if (near) { sfx('sfx_electric'); log('shock pulse'); }
  }
  if (sh.live && isWet(r.steps)) { log('shock hit'); sfx('sfx_electric'); sfx('sfx_gameover'); endRun('gameover', 'shock'); return; }
  if (isCracked(r.steps)) {                    // 금 간 계단에 너무 오래 서 있으면 무너진다
    r.crackT += dt;
    if (r.crackT >= CONFIG.crackTime) { log('crack fall'); sfx('sfx_stair_crack'); sfx('sfx_gameover'); endRun('gameover', 'cracked'); return; }
  }
  if (isEscalator(r.steps)) {                  // 내려가는 에스컬레이터: 가만히 서 있으면 한 칸씩 밀려 내려간다
    r.escT += dt;
    if (r.escT >= CONFIG.escalatorPush) { r.escT = 0; r.steps = Math.max(0, r.steps - 1); r.crackT = 0; r.facing = dirAt(r.steps); log('escalator push'); sfx('sfx_escalator'); r.downT = r.time; }
  } else r.escT = 0;
  if (isVanish(r.steps) && vanishState(r).gone) {   // 사라지는 칸: 없어지면 그 아래 칸으로 떨어진다
    log('vanish fall'); sfx('sfx_vanish'); r.lastKind = 'vanish'; r.downT = r.time;
    while (r.steps > 0 && isVanish(r.steps)) r.steps--;
    r.crackT = 0; r.escT = 0; r.facing = dirAt(r.steps);
    addText('떨어짐!', playerScreenX(), PLAYER_Y - 30, PAL.red);
    loseHeart('vanish');
    if (r.ended) return;
  }
  if (r.mode !== 'practice' && r.gazeT0 === null && r.floor >= CONFIG.gazeFromFloor) { r.gazeT0 = r.time; log('gaze start'); }
  const gz = gazeState(r);                     // 상사의 눈: 예고·훑기가 시작될 때 소리
  if (gz && !gz.rest) {
    const key = gz.cycle * 2 + (gz.sweep ? 1 : 0);
    if (key !== r.gazeKey) { r.gazeKey = key; if (gz.warn) { sfx('sfx_gaze_warn'); log('gaze warn'); } else { sfx('sfx_gaze_laser'); log('gaze sweep'); r.shake = Math.max(r.shake, 0.18); } }
  }
  if (r.floor >= CONFIG.flashFromFloor || r.mode !== 'normal') {      // 카메라 플래시 (무한·연습은 처음부터): 막을 수 없지만 피해도 없다
    r.nextFlash -= dt;
    if (r.nextFlash <= 0) {
      r.camFlash = { t: CONFIG.flashTime, x: 20 + r.decor() * (W - 40), y: 20 + r.decor() * 60 };
      r.nextFlash = CONFIG.flashGap * (0.6 + r.decor() * 0.8);
      sfx('sfx_camera_flash'); log('flash');
    }
  }
  if (r.camFlash) { r.camFlash.t -= dt; if (r.camFlash.t <= 0) r.camFlash = null; }
  if (r.days <= 0) { r.days = 0; sfx('sfx_gameover'); endRun('gameover', 'days'); return; }

  if (r.time >= r.nextAttack) { spawnAttack(); r.nextAttack = r.time + attackInterval(r); }

  for (let i = 0; i < r.projs.length; i++) {
    const p = r.projs[i];
    if (p.type === 'rival') updateRival(p, dt);
    if (p.type === 'monitor' && !p.shattered && r.time >= p.spawnT) { p.shattered = true; sfx('sfx_monitor_shatter'); }   // 유리 깨짐 (도착 1.0초 전)
    // 패링 타이밍 소리: 소리의 박이 패링하기 가장 좋은 순간(도착 0.1초 전)에 온다. 경쟁 사원은 막을 쪽이 정해진 뒤에만
    const cue = CONFIG.cues[p.type];
    if (cue && p.type !== 'chair' && !p.cued && !p.done && p.locked !== false && r.time >= p.impactT - cue[1]) { p.cued = true; sfx(cue[0]); log('cue ' + p.type); }
    if (p.type === 'chair') {                  // 튀어 내려오는 의자: 두 칸씩 튀며 내려오고, 플레이어 몸에 닿는 순간 맞는다 (그 순간 오는 쪽 막기가 켜져 있으면 막힌다)
      if (p.done) continue;
      if (p.fallT !== null) { if (r.time >= p.fallT + 0.6) p.done = true; continue; }   // 계단 밖으로 떨어지는 중
      const c = chairPos(p, r.time);
      if (c.phase === 'fall') { p.fallT = c.tPeak; log('chair off'); continue; }       // 앞에 칸이 없다
      const nh = chairHitT(r, p);
      if (nh && p.cueK !== nh.k && r.time >= nh.t - CONFIG.cues.chair[1]) { p.cueK = nh.k; sfx('sfx_cue_chair'); log('cue chair'); }
      if (c.phase !== 'pre' && chairTouches(r, c)) {                                    // 몸에 닿았다
        const cs = p.side < 0 ? 0 : 1; p.done = true;
        if (r.parryStart[cs] <= r.time && r.time <= r.parryEnd[cs]) gainParry(r, p, r.time);
        else { r.lastKind = 'chair'; loseHeart('attack'); }
        if (r.ended) break;
      }
      continue;
    }
    if (p.type === 'envelope') {               // 편지 3연발: 한 장씩 도착할 때 그 쪽 막기가 켜져 있어야 한다
      if (p.done) continue;
      const es = p.side < 0 ? 0 : 1;
      while (p.cueK < CONFIG.envelopeCount && r.time >= p.impactT + p.cueK * CONFIG.envelopeGap - 0.35) { p.cueK++; sfx('sfx_cue_envelope'); }
      while (!p.done && p.k < CONFIG.envelopeCount && r.time >= p.impactT + p.k * CONFIG.envelopeGap) {
        const th = p.impactT + p.k * CONFIG.envelopeGap;
        if (r.parryStart[es] <= th && th <= r.parryEnd[es]) { p.k++; gainParry(r, p, th); }
        else { p.done = true; log('envelope miss ' + p.k); r.lastKind = 'envelope'; loseHeart('attack'); }
      }
      if (r.ended) break;
      if (p.k >= CONFIG.envelopeCount) p.done = true;
      continue;
    }
    if (p.type === 'airplane') {               // 종이비행기 편대: 비행기마다 도착할 때 날아오는 쪽 키를 누르고 있어야 한다
      if (p.done) continue;
      const hs = p.side < 0 ? 0 : 1;
      while (!p.done && p.k < CONFIG.airplaneCount && r.time >= p.impactT + p.k * CONFIG.airplaneGap) {
        if (r.held[hs]) { p.k++; sfx('sfx_click'); }
        else { p.done = true; log('airplane miss ' + p.k); r.lastKind = 'airplane'; loseHeart('attack'); }
      }
      if (r.ended) break;
      if (!p.done && p.k >= CONFIG.airplaneCount) { p.done = true; gainParry(r, p); }
      continue;
    }
    if (p.done || r.time < p.impactT) continue;
    p.done = true;
    if (p.type === 'rival') { const rp = rivalPos(p); r.leavers.push({ side: p.side, t0: r.time, wx: rp.x, wy: rp.y }); }
    const s = p.side < 0 ? 0 : 1, parried = r.parryStart[s] <= p.impactT && p.impactT <= r.parryEnd[s];
    if (p.type === 'coffee') {   // 커피: 막으면 하트 -1, 그냥 두면 지나간다
      if (parried) { log('coffee parried'); r.lastKind = 'coffee'; addText('커피!', playerScreenX() + p.side * 10, PLAYER_Y - 30, PAL.red); loseHeart('coffee'); if (r.ended) break; }
      else log('coffee dodged');
      continue;
    }
    if (parried) {
      gainParry(r, p);
      if (p.type === 'stapler' && !p.returned) {   // 스테이플러: 부메랑처럼 반대쪽에서 돌아온다
        p.returned = true; p.side = -p.side; p.from = p.side; p.done = false; p.cued = false; p.spawnT = r.time; p.impactT = r.time + CONFIG.staplerReturn; p.ax = stairX(r.steps); p.ay = -r.steps * CONFIG.stepH;
        log('stapler return');
      }
      if (p.type === 'sticky' && p.hp > 1) {   // 포스트잇: 튕겨 나갔다가 같은 쪽으로 돌아온다. 남은 횟수(색)가 하나 준다
        p.hp--; p.done = false; p.bounce = true; p.cued = false; p.spawnT = r.time; p.impactT = r.time + CONFIG.warnTime; p.ax = stairX(r.steps); p.ay = -r.steps * CONFIG.stepH;
        log('sticky bounce hp=' + p.hp);
      }
    } else {
      r.lastKind = p.type; loseHeart('attack');
      if (r.ended) break;
    }
  }
  r.projs = r.projs.filter(function (p) { return !p.done; });
}
// 막기에 성공했을 때 (점수·콤보·일수·불꽃·글자). 종이비행기 편대는 다 막은 뒤 한 번
// 의자가 다음에 플레이어 칸에 내려앉는 때 {k, t} (내려앉지 않으면 null). 의자는 생길 때 칸(s0)에서 6, 4, 2, 0, -2 ... 칸에 내려앉는다
// 의자 자리(world, 의자 가운데). 생길 때 칸(s0)의 계단 줄을 따라: 첫 실제 칸(j = 6, 4, 2, 0 중 up 이하로 가장 높은 칸)까지는 층참에서 떨어져 들어오고,
// 두 칸씩 30px 아치로 튀며(칸 윗면에 내려앉는다), 앞에 칸이 없는 튀기는 꼭대기부터 그대로 떨어진다. phase: pre·drop·hop·fall
const CHAIR_ARC = 30;
function chairPos(p, T) {
  const s = p.side, n0 = p.s0, hop = CONFIG.chairHop, dx = Math.abs(stairX(n0 + 1) - stairX(n0)) || CONFIG.stepW;
  const L = function (j) { return [stairX(n0) + s * j * dx, -(n0 + j) * CONFIG.stepH - 7]; };
  const kF = Math.max(0, Math.ceil((6 - p.up) / 2)), jF = 6 - 2 * kF, kOff = Math.floor((6 + p.down) / 2) + 1, tPeak = p.spawnT + (kOff - 0.5) * hop;
  const t = T - p.spawnT;
  if (T >= tPeak) { const a = L(8 - 2 * kOff), b = L(6 - 2 * kOff), e = T - tPeak; return { x: (a[0] + b[0]) / 2 + (b[0] - a[0]) / hop * e, y: (a[1] + b[1]) / 2 - CHAIR_ARC + 350 * e * e, phase: 'fall', e: e, tPeak: tPeak }; }
  if (t < 0) { const q = L(6); return { x: q[0], y: q[1] - (p.up < 6 ? 40 : 0), phase: 'pre', tPeak: tPeak }; }
  if (t < kF * hop) { const u = t / (kF * hop), a = L(6), b = L(jF); return { x: lerp(a[0], b[0], u), y: lerp(a[1] - 40, b[1], u * u), phase: 'drop', u: u, tPeak: tPeak }; }
  const f = t / hop, k = Math.floor(f), u = f - k, a = L(6 - 2 * k), b = L(4 - 2 * k);
  return { x: lerp(a[0], b[0], u), y: lerp(a[1], b[1], u) - Math.sin(u * Math.PI) * CHAIR_ARC, phase: 'hop', u: u, tPeak: tPeak };
}
// 의자 상자(가운데 ±7, ±8)가 플레이어 몸(발, 가로 ±6, 높이 26)에 닿는가. 플레이어 자리는 보이는 자리(pvx, pvy: 칸 사이를 미끄러지는 중이면 그 자리)
function chairTouches(r, c) {
  const fx = r.pvx, fy = r.pvy;
  return Math.abs(c.x - fx) < 13 && c.y + 8 > fy - 26 && c.y - 8 < fy;
}
// 의자가 다음에 플레이어 몸에 닿을 때 {k, t} (1.5초 안에, 플레이어가 지금 자리에 그대로 있다면). 없으면 null
function chairHitT(r, p) {
  if (p.fallT !== null) return null;
  for (let i = 1; i <= 90; i++) {
    const T = r.time + i / 60, c = chairPos(p, T);
    if (c.phase === 'fall') return null;
    if (c.phase !== 'pre' && chairTouches(r, c)) return { k: Math.round(T * 60), t: T };
  }
  return null;
}
function gainParry(r, p, th) {   // th: 맞는 순간 (편지는 장마다 다르다)
  r.parryHitT = r.time; r.parryHitSide = p.side;   // 막기 성공 프레임 (그리기용)
  r.parries++; r.combo++;
  r.mult = Math.min(CONFIG.maxMult, 1 + Math.floor(r.combo / CONFIG.comboPerMult));   // 3연속마다 ×1, 최대 ×10
  r.maxMult = Math.max(r.maxMult, r.mult);
  r.maxCombo = Math.max(r.maxCombo, r.combo);
  r.days = Math.min(CONFIG.maxDays, r.days + CONFIG.parryDays);
  r.score += CONFIG.parryPoints * r.mult;
  const s = p.side < 0 ? 0 : 1, perfect = p.type === 'airplane' || Math.abs((th === undefined ? p.impactT : th) - r.parryStart[s] - CONFIG.parryWindow / 2) <= CONFIG.parryPerfect;
  sfx(perfect ? 'sfx_parry_perfect' : 'sfx_parry');   // 완벽하게 막으면 더 짜릿한 소리
  const px = playerScreenX() + p.side * 10;
  for (let k = 0; k < 8; k++) {
    const a = r.decor() * Math.PI * 2;
    r.sparks.push({ x: px, y: PLAYER_Y - 14, vx: Math.cos(a) * 50, vy: Math.sin(a) * 50 - 20, t: 0, color: k % 2 ? PAL.text : PAL.green });
  }
  addText('+' + CONFIG.parryDays, px, PLAYER_Y - 42, PAL.green);
  log('parry ok combo=' + r.combo + ' x' + r.mult);
}
function finishRun() {
  const r = run;
  const kind = r.ended.kind;
  let newBest;
  if (r.mode === 'endless') { newBest = r.floor > save.endless; save.endless = Math.max(save.endless, r.floor); }   // 무한 모드 기록은 따로
  else {
    newBest = r.floor > save.floor || r.score > save.score;
    if (r.floor > save.floor) save.floor = r.floor;
    if (r.score > save.score) save.score = r.score;
  }
  writeSave();
  result = {
    kind: kind, cause: r.ended.cause, floor: r.floor, rankIdx: r.rankIdx, mode: r.mode,
    score: r.score, maxMult: r.maxMult, maxCombo: r.maxCombo, newBest: newBest,
    tip: r.ended.cause === 'cracked' ? 'cracked' : r.ended.cause === 'shock' ? 'shock' : r.ended.cause === 'days' ? 'days' : (r.lastKind || 'clipboard'), t0: time, stamped: false,
  };
  screen = 'result'; focus = 0; resultLock = 0.5;
  log('result ' + kind);
}

/* ---------------------------------------------------------------------
 * UI 보조
 * ------------------------------------------------------------------- */
function rect(x, y, w, h, c, a) {
  if (a !== undefined) ctx.globalAlpha = a;
  ctx.fillStyle = c; ctx.fillRect(Math.round(x), Math.round(y), Math.round(w), Math.round(h));
  if (a !== undefined) ctx.globalAlpha = 1;
}
function frameRect(x, y, w, h, c) {
  rect(x, y, w, 1, c); rect(x, y + h - 1, w, 1, c); rect(x, y, 1, h, c); rect(x + w - 1, y, 1, h, c);
}
function txt(s, x, y, o) {
  o = o || {};
  ctx.font = (o.bold ? 'bold ' : '') + (o.size || 8) + 'px "Noto Sans KR", sans-serif';
  ctx.textAlign = o.align || 'left';
  ctx.textBaseline = o.base || 'top';
  x = Math.round(x); y = Math.round(y);
  if (o.shadow !== false) { ctx.fillStyle = PAL.bg; ctx.fillText(s, x + 1, y + 1); }
  ctx.fillStyle = o.color || PAL.text;
  ctx.fillText(s, x, y);
}
function txtO(s, x, y, o) {   // 테두리 글자: 어떤 배경에서도 읽히게 (짙은 1px 테두리)
  const d = Object.assign({}, o, { color: '#0E0F12', shadow: false });
  txt(s, x - 1, y, d); txt(s, x + 1, y, d); txt(s, x, y - 1, d); txt(s, x, y + 1, d);
  txt(s, x, y, Object.assign({}, o, { shadow: false }));
}
function btn(id, label, x, y, w, h, fn, size) { return { id: id, label: label, x: x, y: y, w: w, h: h, fn: fn, size: size }; }

function openSettings() { settingsOpen = true; focus = 0; }
function closeSettings() { settingsOpen = false; focus = 0; }
function openHelp() { helpOpen = true; focus = 0; }
function closeHelp() { helpOpen = false; focus = 0; }
function toast(s) { toastText = s; toastUntil = time + 2.5; }
function toTitle() { screen = 'title'; focus = 0; settingsOpen = false; helpOpen = false; }

function getButtons() {
  if (demo) {
    return [
      btn('dprev', '◀ 이전', 8, 158, 48, 16, function () { demoGo(demo.idx - 1); }, 8),
      btn('dnext', '다음 ▶', 60, 158, 48, 16, function () { demoGo(demo.idx + 1); }, 8),
      btn('dclose', '닫기', 264, 158, 48, 16, closeDemo, 8),
    ];
  }
  if (helpOpen) return [btn('demo', '장애물 보기', 60, 150, 96, 14, function () { openDemo('climb'); }), btn('close', '닫기', 164, 150, 96, 14, closeHelp)];
  if (settingsOpen) {
    return [
      Object.assign(btn('snd', Snd.muted ? '꺼짐' : '켜짐', 170, 34, 88, 14, function () { Snd.muted = !Snd.muted; syncAudio(true); }), { off: Snd.muted }),
      btn('vdn', '-', 170, 52, 24, 14, function () { Snd.vol = clamp(Math.round((Snd.vol - 0.1) * 10) / 10, 0, 1); syncAudio(true); }),
      btn('vup', '+', 234, 52, 24, 14, function () { Snd.vol = clamp(Math.round((Snd.vol + 0.1) * 10) / 10, 0, 1); syncAudio(true); }),
      btn('fs', '전체화면', 170, 70, 88, 14, function () {
        try {
          if (document.fullscreenElement) document.exitFullscreen();
          else { const p = document.documentElement.requestFullscreen(); if (p && p.catch) p.catch(function () {}); }
        } catch (e) {}
      }),
      btn('help', '보기', 170, 96, 88, 14, openHelp),
      btn('demo', '보기', 170, 114, 88, 14, function () { openDemo('climb'); }),   // 장애물 보기 (장애물마다 대처법 장면)
      btn('close', '닫기', 110, 144, 100, 16, closeSettings),
    ];
  }
  switch (screen) {
    case 'title':
      return [
        Object.assign(btn('start', '시작하기', 122, 98, 76, 18, function () { screen = 'lobby'; focus = 0; }, 10), { tone: 'red' }),
        Object.assign(btn('settings', '', 298, 4, 18, 18, openSettings), { icon: 'gear' }),
      ];
    case 'lobby':
      return [
        Object.assign(btn('work', '업무 시작', 104, 150, 112, 18, function () { startRun('normal'); }, 10), { tone: 'red' }),   // 05-2 요소 4 (ESC: 타이틀로)
        btn('endless', '무한 모드', 224, 150, 84, 18, function () { startRun('endless'); }, 8),
        btn('practice', '연습 모드', 12, 150, 84, 18, function () { startRun('practice'); }, 8),
        Object.assign(btn('settings', '', 226, 5, 18, 18, openSettings), { icon: 'gear' }),   // 설정 (층 명판과 D-30 사이)
      ];
    case 'pause':
      return [
        btn('resume', '계속하기', 100, 66, 120, 14, function () { screen = 'play'; focus = 0; }),
        btn('help', '조작법', 100, 84, 120, 14, openHelp),
        btn('settings', '설정', 100, 102, 120, 14, openSettings),
        btn('main', '메인으로', 100, 120, 120, 14, function () { log('quit to title'); toTitle(); }),
      ];
    case 'result':
      return [
        Object.assign(btn('again', result && result.kind === 'promote' ? '1층부터 다시' : '다시 하기', 96, 155, 62, 16, function () { startRun(result ? result.mode : 'normal'); }, 8), { tone: 'red' }),   // 05-5 요소 5
        btn('share', '기록 공유', 166, 155, 62, 16, shareResult, 8),                                                                                     // 요소 2
        Object.assign(btn('settings', '', 298, 4, 18, 18, openSettings), { icon: 'gear' }),   // 설정 (장애물 보기도 여기서)
      ];
  }
  return [];
}
function activate(b) {
  if (!b) return;
  sfx('sfx_click');
  b.fn();
}

/* ---------------------------------------------------------------------
 * 입력
 * ------------------------------------------------------------------- */
function menuMode() { return screen !== 'play' || settingsOpen || helpOpen || !!demo; }
window.addEventListener('keydown', function (e) {
  unlockAudio();
  const c = e.code;
  if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Space', 'Tab'].indexOf(c) >= 0) e.preventDefault();
  if (e.repeat) return;

  if (c === 'Escape') {
    if (demo) { closeDemo(); return; }
    if (helpOpen) { closeHelp(); return; }
    if (settingsOpen) { closeSettings(); return; }
    if (screen === 'play') { screen = 'pause'; focus = 0; return; }
    if (screen === 'pause') { screen = 'play'; focus = 0; return; }
    if (screen === 'lobby') { toTitle(); return; }
    if (screen === 'result') { screen = 'lobby'; focus = 0; return; }
    return;
  }
  if (!menuMode()) {
    if (c === 'ArrowLeft') keyStep(-1);
    else if (c === 'ArrowRight') keyStep(1);
    else if (c === 'KeyQ') { keyParry(-1); if (run) run.held[0] = true; }   // held: 종이비행기 편대는 누르고 있어야 한다
    else if (c === 'KeyE') { keyParry(1); if (run) run.held[1] = true; }
    return;
  }
  const bs = getButtons();
  if (!bs.length) return;
  if (c === 'ArrowUp' || c === 'ArrowLeft') focus = (focus + bs.length - 1) % bs.length;
  else if (c === 'ArrowDown' || c === 'ArrowRight' || c === 'Tab') focus = (focus + 1) % bs.length;
  else if (c === 'Enter' || c === 'NumpadEnter' || c === 'Space') {
    if (screen === 'result' && resultLock > 0) return;
    activate(bs[focus % bs.length]);
  }
});
function logicalPos(e) {
  const r = canvas.getBoundingClientRect();
  return { x: (e.clientX - r.left) / r.width * W, y: (e.clientY - r.top) / r.height * H };
}
function hitButton(p) {
  const bs = getButtons();
  for (let i = 0; i < bs.length; i++) {
    const b = bs[i];
    if (p.x >= b.x && p.x <= b.x + b.w && p.y >= b.y && p.y <= b.y + b.h) return i;
  }
  return -1;
}
canvas.addEventListener('mousemove', function (e) {
  if (!menuMode()) return;
  const i = hitButton(logicalPos(e));
  if (i >= 0) focus = i;
});
canvas.addEventListener('mousedown', function (e) {
  unlockAudio();
  canvas.focus();
  if (!menuMode()) return;
  const i = hitButton(logicalPos(e));
  if (i >= 0) {
    focus = i;
    if (screen === 'result' && resultLock > 0) return;
    activate(getButtons()[i]);
  }
});
window.addEventListener('keyup', function (e) {
  if (!run) return;
  if (e.code === 'KeyQ') run.held[0] = false;
  else if (e.code === 'KeyE') run.held[1] = false;
});
window.addEventListener('blur', function () { if (run) run.held = [false, false]; });
document.addEventListener('visibilitychange', function () {
  if (document.hidden && screen === 'play') { screen = 'pause'; focus = 0; }
});

/* ---------------------------------------------------------------------
 * 그리기 - 공통
 * ------------------------------------------------------------------- */
function drawButtons() {
  const bs = getButtons();
  if (focus >= bs.length) focus = 0;
  bs.forEach(function (b, i) {
    const f = i === focus;
    if (b.icon) {   // 아이콘만 있는 버튼 (타이틀의 톱니바퀴)
      rect(b.x, b.y, b.w, b.h, PAL.bg, f ? 0.9 : 0.6);
      frameRect(b.x, b.y, b.w, b.h, f ? PAL.text : PAL.gray);
      if (!drawSprite('hud_icons', 'default', b.icon, 0, b.x + (b.w - 12) / 2, b.y + (b.h - 12) / 2, { anchor: 'tl', dw: 12, dh: 12, nofb: true })) txt('설정', b.x + b.w / 2, b.y + b.h / 2, { align: 'center', base: 'middle', size: 6 });
      return;
    }
    const red = b.tone === 'red';
    rect(b.x, b.y, b.w, b.h, red ? (f ? '#B84545' : PAL.red) : b.off ? '#4A4E55' : (f ? PAL.green : PAL.gray));   // 꺼짐은 고른 때도 회색 (테두리로 고른 것을 보인다)
    frameRect(b.x, b.y, b.w, b.h, f ? PAL.text : PAL.bg);
    if (red) frameRect(b.x + 1, b.y + 1, b.w - 2, b.h - 2, '#6E2626');
    txt(b.label, b.x + b.w / 2, b.y + b.h / 2 + 0.5, { align: 'center', base: 'middle', size: b.size || 8, color: b.off ? '#E8E6DF' : f && !red ? PAL.bg : PAL.text, shadow: !f || red });
  });
}
function drawHeart(x, y, full) {
  const ok = drawSprite('hud_icons', 'default', full ? 'heart' : 'heart_empty', 0, x, y, { anchor: 'tl', dw: 12, dh: 12, nofb: true });
  if (!ok) { rect(x, y, 10, 10, full ? PAL.red : PAL.gray, full ? 1 : 0.5); }
}
function drawBadgeIcon(x, y, size) {
  const ok = drawSprite('hud_icons', 'default', 'badge', 0, x, y, { anchor: 'tl', dw: size, dh: size, nofb: true });
  if (!ok) rect(x, y, size, size, PAL.green);
}

/* ---------------------------------------------------------------------
 * 05-1 타이틀
 * ------------------------------------------------------------------- */
// 타이틀 거리의 사원들: 서로 다른 속도로 좌우로 걷고, 화면 밖으로 나가면 반대편에서 다시 들어온다
function drawCommuters() {
  for (let i = 0; i < CONFIG.commuters; i++) {
    const dir = i % 3 === 1 ? -1 : 1, v = 9 + ((i * 7) % 5) * 1.6, span = W + 28;
    const x = (((20 + i * 64 + (i % 2) * 17 + dir * v * time) % span) + span) % span - 14;
    const at = time * (v / 11) + i * 0.37;
    drawSprite('commuter', 'default', 'walk', at, x, 179 - Math.abs(Math.sin(at * 7)) * 1.5, { fw: 10, fh: 20, nofb: true, flip: dir < 0 });   // 걸으며 들썩
  }
}
function drawTitle() {
  const titleOk = drawImg('title_bg', 0, 0, W, H, PAL.bg);
  drawCommuters();
  if (!titleOk) {
    // 어두운 빌딩 숲 + 가운데 본사 꼭대기만 불빛
    const hs = [60, 90, 50, 110, 70, 130, 55, 95, 65];
    for (let i = 0; i < hs.length; i++) rect(i * 36 - 4, H - hs[i], 30, hs[i], PAL.gray, 0.35);
    rect(136, 40, 48, 140, PAL.gray, 0.6);
    rect(150, 52, 20, 10, PAL.text);
  }
  const lg = A.images.logo;
  if (lg && lg.ok) {
    const iw = lg.img.naturalWidth, ih = lg.img.naturalHeight;
    const s = Math.min(1 / PX, 280 / iw, 70 / ih);
    ctx.drawImage(lg.img, Math.round((W - iw * s) / 2), 34, iw * s, ih * s); // 05-1 컨셉: 본사 앞 가운데 명판
  } else {
    rect(50, 20, 220, 48, PAL.bg, 0.8);
    frameRect(50, 20, 220, 48, PAL.red);
    txt('승급의 계단', 160, 30, { align: 'center', size: 26, bold: true });
    txt('구조조정까지 D-30', 160, 58, { align: 'center', size: 8, color: PAL.green });
  }
  drawButtons();
  txt('v0.1 PoC · 승급의 계단 제작팀', 160, 172, { align: 'center', size: 6, color: PAL.gray });
}

/* ---------------------------------------------------------------------
 * 05-2 로비
 * ------------------------------------------------------------------- */
// 05-2 요소 3: 목걸이(올리브 끈)에 걸린 사원증 카드
function drawIdCard(x, y, rIdx) {
  const rank = CONFIG.rankNames[rIdx];
  rect(x + 27, 0, 4, y - 6, '#6E7A49'); rect(x + 43, 0, 4, y - 6, '#6E7A49');             // 목걸이 끈
  rect(x + 28, 0, 1, y - 6, PAL.green); rect(x + 44, 0, 1, y - 6, PAL.green);
  rect(x + 29, y - 9, 16, 12, '#9AA0A8'); frameRect(x + 29, y - 9, 16, 12, '#44484E');    // 집게
  rect(x, y, 74, 108, PAL.text); frameRect(x, y, 74, 108, PAL.gray);
  rect(x + 1, y + 1, 72, 13, PAL.green); rect(x + 30, y + 5, 14, 3, '#4E5733');           // 올리브 머리띠, 줄 구멍
  rect(x + 15, y + 19, 44, 48, '#3A3D43'); rect(x + 18, y + 22, 38, 42, '#9AA0A8');        // 사진
  drawSprite('player', rank, 'idle', time, x + 37, y + 64, { scale: 1.6, fw: 16, fh: 28, color: RANK_COLORS[rIdx] });
  [['NAME', '○○○'], ['TITLE', rank], ['DEPARTMENT', '총무팀']].forEach(function (row, i) {
    const yy = y + 73 + i * 11;
    txt(row[0], x + 5, yy, { size: 5, bold: true, color: '#2A2C31', shadow: false });
    txt(row[1], x + (row[0].length > 5 ? 44 : 28), yy, { size: 5, color: PAL.gray, shadow: false });
    rect(x + 4, yy + 8, 66, 1, '#9AA0A8', 0.6);
  });
}
// 로비 사원: 왼쪽 끝 ↔ 오른쪽 끝을 걷고, 끝에서 pause초 머문다 (커피를 마시다 서 있기)
function lobbyWalker(t, x0, x1, speed, pause) {
  const walk = (x1 - x0) / speed, P = 2 * (walk + pause), ph = ((t % P) + P) % P;
  if (ph < walk) return { x: x0 + speed * ph, dir: 1, anim: 'walk', at: ph };
  if (ph < walk + pause) return { x: x1, dir: -1, anim: ph - walk < pause * 0.6 ? 'coffee' : 'idle', at: ph - walk };
  if (ph < 2 * walk + pause) { const q = ph - walk - pause; return { x: x1 - speed * q, dir: -1, anim: 'walk', at: q }; }
  return { x: x0, dir: 1, anim: 'idle', at: ph - 2 * walk - pause };
}
function drawLobbyStaff() {
  const o = { fw: 16, fh: 24, nofb: true };
  drawSprite('staff', 'b', 'type', time, 172, 129, o);                                       // 의자에 앉아 타자 (위몸만)
  drawSprite('staff', 'c', 'phone', time, 229, 132, o);                                      // 서류함 옆에서 전화
  drawSprite('staff', 'd', time % 6 < 3.5 ? 'coffee' : 'idle', time, 113, 132, Object.assign({ flip: false }, o));   // 화분 옆에서 커피
  const w = lobbyWalker(time + 2, 96, 236, 14, 3);                                           // 바닥을 오가는 사원
  drawSprite('staff', 'a', w.anim, w.at, Math.round(w.x), 148, Object.assign({ flip: w.dir < 0 }, o));
}
function drawLobby() {
  // 컨셉 05-2: 형광등 천장의 파티션 사무실(배경 bg_lobby) 위에 층 표시, D-30, 사원증, 화이트보드 기록, 업무 시작
  if (!drawImg('bg_lobby', 0, 0, W, H, null)) { drawImg('bg_zone1', 0, 0, W, H, PAL.bg); rect(0, 0, W, H, PAL.bg, 0.5); }
  drawLobbyStaff();                                                                         // 사무실 사원들 (움직이는 로비)
  const rIdx = CONFIG.promoteFloors.filter(function (f) { return save.floor >= f; }).length;
  rect(124, 3, 72, 13, PAL.bg, 0.92); frameRect(124, 3, 72, 13, '#9AA0A8');                 // 1 층·직급
  txt('1F', 148, 9.5, { align: 'center', base: 'middle', size: 8, bold: true });
  txt(CONFIG.rankNames[0], 172, 9.5, { align: 'center', base: 'middle', size: 6, color: '#9AA0A8' });
  rect(248, 5, 64, 18, PAL.red); frameRect(248, 5, 64, 18, PAL.text); frameRect(249, 6, 62, 16, '#6E2626');   // 2 D-30
  txt('D-' + CONFIG.startDays, 280, 14.5, { align: 'center', base: 'middle', size: 11, bold: true });
  drawIdCard(12, 38, rIdx);                                                                 // 3 사원증
  rect(244, 80, 68, 22, PAL.bg, 0.9); frameRect(244, 80, 68, 22, PAL.gray);                // 5 화이트보드 위 기록
  txt('최고 ' + save.floor + 'F', 278, 87, { align: 'center', base: 'middle', size: 7, bold: true });
  txt('무한 최고 ' + save.endless + 'F · 랭킹 오프라인', 278, 96, { align: 'center', base: 'middle', size: 5, color: '#9AA0A8' });
  drawButtons();                                                                            // 4 업무 시작
}

/* ---------------------------------------------------------------------
 * 05-3 플레이
 * ------------------------------------------------------------------- */
// 사람 그림 크기 (1 = 원래 크기)
const MAN_SCALE = 1;
// 계단을 달려 오르는 몸짓. a = 한 칸 오르기 진행(0 → 1, 0.14초), run = 계속 오르는 중인 정도(1 → 멈추면 0.4초에 걸쳐 0).
// 한 칸마다 낮고 빠르게 들썩이고(발을 딛는 순간 살짝 눌림), 오르는 쪽으로 앞으로 기운다
function runFx(a, run, dir, h) {
  const s = a > 0 && a < 1 ? Math.sin(a * Math.PI) : 0;
  const sy = 1 + 0.05 * s - (a > 0.75 && a < 1 ? 0.06 * Math.sin((a - 0.75) / 0.25 * Math.PI) : 0);
  return { dy: -s * 2.5 + (1 - sy) * h / 2, sx: 1 + (1 - sy) * 0.8, sy: sy, rot: dir * (0.07 + 0.08 * run) * Math.max(run, s) };
}
// 위협 위 표시(!, ×, ↺, 꾹!, …)를 보일지: 10층까지만. 연습 모드와 장애물 보기에서는 늘 보인다 (결정 2026-11-25)
function showMarks() { return !!demo || run.mode === 'practice' || run.floor <= 10; }
const PARRY_VAR = ['', '2', '3'];   // 막기 동작 이름 뒤에 붙는 것 (곧게 막기, 두 손 밀기, 위로 쳐내기)
function playerAnim(r) {
  let s = 'idle';
  if (r.ended) s = r.ended.kind === 'promote' ? 'proud' : 'hit';
  else if (r.hitTimer > 0) s = 'hit';
  else if (r.time - r.parryHitT < 0.17) s = (r.parryHitSide < 0 ? 'parry_hit_left' : 'parry_hit_right') + PARRY_VAR[r.parryVar];   // 막기 성공: 손이 번쩍 → 몸이 밀림
  else if (r.time >= r.parryStart[0] && r.time < r.parryEnd[0]) s = 'parry_left' + PARRY_VAR[r.parryVar];
  else if (r.time >= r.parryStart[1] && r.time < r.parryEnd[1]) s = 'parry_right' + PARRY_VAR[r.parryVar];
  else if (r.time - r.downT < 0.3) s = 'down';                          // 밀려 내려가기·떨어지기: 웅크려 내려 딛기
  else if (r.stepTimer > 0) s = r.steps % 2 ? 'climb_r' : 'climb_l';    // 칸마다 무릎을 번갈아 든다
  if (r.animState !== s) { r.animState = s; r.animStart = r.time; }
  return s;
}
function drawPlayWorld() {
  const r = run;
  const zone = zoneOf(r.floor), practice = r.mode === 'practice';
  // 연습 모드: 계단 없는 평범한 사무실(로비 배경), 플레이어는 사무실 바닥에 선다
  if (practice) {   // 옆에서 본 사무실(bg_practice): 가구는 플레이어 크기, 발 높이에 바닥이 있어 경쟁 사원도 그 위를 걷는다
    if (!drawImg('bg_practice', 0, 0, W, H, null)) { drawImg('bg_lobby', 0, 0, W, H, PAL.bg); rect(0, PLAYER_Y + 2, W, H, '#3A3D42'); }
    [[30, 'cooler'], [96, 'plant'], [228, 'boxes'], [296, 'cabinet']].forEach(function (pr) {
      drawSprite('props', 'default', pr[1], 0, pr[0], PLAYER_Y + 2, { anchor: 'bc', fw: 16, fh: 24, nofb: true });
    });
  }
  else drawImg('bg_zone' + zone, 0, 0, W, H, PAL.bg);
  if (!practice && (!A.images['bg_zone' + zone] || !A.images['bg_zone' + zone].ok)) {
    // 아래로 흐르는 가로줄로 위로 오르는 느낌
    const off = (-r.camY) % 24;
    for (let y = -24; y < H + 24; y += 24) rect(0, y + off, W, 1, PAL.gray, 0.25);
  }
  rect(0, 0, W, H, PAL.bg, 0.15);
  // 상사의 눈 (배경): 눈은 보이지 않는다. 예고 5초 동안 화면이 붉어지고 먼 배경에 불꽃 하나가 천천히 나타났다 사라졌다 하며
  // (상사의 눈인지 아닌지 모르게), 훑을 때는 그 불꽃에서 거대한 레이저가 부채꼴로 퍼져 화면 전체를 위에서 아래로 쓸어내린다
  const gz = gazeState(r);
  if (gz) drawDecoyGlints();                                            // 61층부터: 배경 곳곳의 가짜 불꽃 (진짜와 같은 모양·박자)
  if (gz && (gz.warn || gz.sweep)) {
    const sx0 = gazeSrcX(gz.cycle), sy0 = GAZE_SRC_Y;
    if (gz.warn) {
      const k = 1 - gz.left / CONFIG.gazeWarn, late = gz.left < 1.5;
      rect(0, 0, W, H, '#C0302E', 0.12 * k);                            // 예고 동안 화면이 조금씩 붉어진다
      const e = CONFIG.gazeWarn - gz.left, fade = 0.5 - 0.5 * Math.cos(e * 2 * Math.PI / 1.25);   // 불꽃 하나: 1.25초마다 천천히 나타났다 사라짐
      drawGlint(sx0, sy0, 2 + (late ? 2 : 0) + fade * 1.5, fade * Math.min(1, e / 0.6));
    } else {
      const half = CONFIG.gazeWidth / 2, top = gz.y - half;
      rect(0, 0, W, H, '#C0302E', 0.08);                                // 훑는 동안 화면 전체가 붉게 번쩍
      for (let yy = sy0; yy < top; yy += 3) {                           // 불꽃에서 띠까지 부채꼴로 퍼지는 빛
        const t = (yy - sy0) / Math.max(1, top - sy0), w = lerp(14, W * 2.4, t);
        rect(sx0 - w / 2, yy, w, 3, '#C0302E', 0.2); rect(sx0 - w * 0.12, yy, w * 0.24, 3, '#FF5A4E', 0.3);
      }
      rect(0, top - 6, W, 6, '#C0302E', 0.14); rect(0, top + CONFIG.gazeWidth, W, 6, '#C0302E', 0.14);   // 바깥 번짐
      rect(0, top, W, CONFIG.gazeWidth, '#C0302E', 0.36);
      rect(0, gz.y - 6, W, 12, '#FF5A4E', 0.45); rect(0, gz.y - 3, W, 6, '#FFB0A0', 0.6); rect(0, gz.y - 1, W, 2, '#FFFFFF', 0.9);   // 뜨거운 하얀 심
      drawGlint(sx0, sy0, 10, 1);
    }
  }

  if (!practice) {   // ---- 계단, 층 바닥, 소품, 난간 (연습 모드에는 없다)
  const spf = CONFIG.stairsPerFloor;
  const RAIL = ['#4E5733', '#44484E', '#5C4F3B', '#5E2323', '#8A6A2E'][zone - 1];
  const PROPS = ['cooler', 'boxes', 'plant', 'cabinet'];
  // 층 바닥(콘크리트 슬래브)과 그 위 소품: 층이 바뀌는 칸 높이에 화면 끝까지 깐다
  for (let i = Math.floor((r.steps - 6) / spf) * spf; i <= r.steps + 14; i += spf) {
    if (i <= 0) continue;
    const sy = PLAYER_Y + (-i * CONFIG.stepH - r.camY);
    if (sy > H + 10 || sy < -40) continue;
    const h = (i * 2654435761) >>> 0, cx = 160 + (stairX(i) - r.camX);
    [-1, 1].forEach(function (side, k) {
      const px = cx + side * (64 + ((h >>> (k * 8)) % 50));
      drawSprite('props', 'default', PROPS[(h >>> (16 + k * 4)) % PROPS.length], 0, px, sy, { anchor: 'bc', fw: 16, fh: 24, nofb: true });
    });
    rect(0, sy, W, 7, '#4E5259'); rect(0, sy, W, 1, '#7B8088'); rect(0, sy + 7, W, 2, '#141518', 0.55);
  }
  // 계단 받침(stringer): 디딤판 아래를 계단 기울기대로 비스듬히 깎아, 아래가 뚫린 계단처럼 보이게 한다 (컨셉 05-3)
  const tileW = CONFIG.stepW * 2, tileH = CONFIG.stepH;
  const STR = ["#3A4226", "#33363C", "#463A2B", "#461A1A", "#5E4820"], STR_EDGE = ["#2C3320", "#26282C", "#352C20", "#341313", "#463618"];
  for (let i = r.steps + 14; i >= Math.max(0, r.steps - 6); i--) {
    const sx = 160 + (stairX(i) - r.camX);
    const sy = PLAYER_Y + (-i * CONFIG.stepH - r.camY);
    if (sy > H + 10 || sy < -30) continue;
    if (isVanish(i) && vanishState(r).gone) continue;   // 사라진 칸은 받침도 없다
    const d = Math.sign(stairX(i + 1) - stairX(i)) || 1, half = CONFIG.stepW / 2;
    for (let c = 0; c < half * 2 * PX; c++) {
      const x = sx - half + c / PX, u = Math.round((tileH * 0.6 - d * (x + 0.5 / PX - sx) * (tileH / (half * 2))) * PX) / PX;
      rect(x, sy + tileH, 1 / PX, u, STR[zone - 1]);
      rect(x, sy + tileH + u - 1 / PX, 1 / PX, 1 / PX, STR_EDGE[zone - 1]);
    }
  }
  // 계단 디딤판
  const floorLabels = [];
  for (let i = r.steps + 14; i >= Math.max(0, r.steps - 6); i--) {
    const sx = 160 + (stairX(i) - r.camX);
    const sy = PLAYER_Y + (-i * CONFIG.stepH - r.camY);
    if (sy > H + 10 || sy < -10) continue;
    const landing = i % spf === 0;
    // 금 간 계단: 위에 서 있으면 시간이 갈수록 흔들리고 붉어지며 부스러기가 떨어진다
    const cracked = isCracked(i), shake = cracked && i === r.steps ? r.crackT / CONFIG.crackTime : 0;
    const jx = shake ? Math.round(Math.sin(r.time * 70) * shake * 1.5 * PX) / PX : 0;
    const sdef = A.sprites.stairs && A.sprites.stairs.def, hasCrack = !!(sdef && sdef.animations && sdef.animations.cracked);
    const wet = isWet(i), hasWet = !!(sdef && sdef.animations && sdef.animations.wet);
    // 사라지는 칸: 없어진 동안은 윤곽만, 없어지기 1초 전부터 깜박인다. 윗면의 하늘색 선이 표시
    const van = isVanish(i), vst = vanishState(r);
    if (van && vst.gone) {
      rect(sx - CONFIG.stepW / 2, sy, CONFIG.stepW, tileH, '#9FE0FF', 0.12);   // 없어진 칸: 옅게 채운 유령
      rect(sx - CONFIG.stepW / 2, sy, CONFIG.stepW, 1, '#9FE0FF', 0.5); rect(sx - CONFIG.stepW / 2, sy + tileH - 1, CONFIG.stepW, 1, '#9FE0FF', 0.25);
      continue;
    }
    if (van && vst.warn && Math.floor(time * 12) % 2 === 0) ctx.globalAlpha = 0.3;
    const ok = drawSprite('stairs', 'zone' + zone, cracked && hasCrack ? 'cracked' : wet && hasWet ? 'wet' : 'tile', time, sx + jx, sy, { anchor: 'tc', dw: tileW, dh: tileH, nofb: true });
    ctx.globalAlpha = 1;
    if (van) rect(sx - CONFIG.stepW / 2, sy, CONFIG.stepW, 1, '#9FE0FF', 0.8);
    if (isReverse(i)) {     // 거꾸로 칸: 보라색 윗면과 엇갈린 화살표
      const x0 = sx - CONFIG.stepW / 2, w = CONFIG.stepW, cx = sx, cy = sy + 6;
      rect(x0, sy, w, 2, '#B07CE0', 0.9); rect(x0, sy + 2, w, 1, '#6E4A99', 0.7);
      rect(cx - 5, cy - 2, 7, 1, '#D4A5FF'); rect(cx - 5, cy - 3, 1, 3, '#D4A5FF');      // ← 위
      rect(cx - 2, cy + 1, 7, 1, '#D4A5FF'); rect(cx + 4, cy, 1, 3, '#D4A5FF');          // → 아래
    }
    if (isEscalator(i)) {   // 내려가는 에스컬레이터: 쇠빛 디딤판 위로 홈이 내려가는 쪽으로 흐른다
      const x0 = sx - CONFIG.stepW / 2, w = CONFIG.stepW, down = -(Math.sign(stairX(i) - stairX(i - 1)) || 1);
      rect(x0, sy, w, 3, '#9AA0A8', 0.6);
      for (let g = 0; g < 5; g++) { const gx = ((g * 4.4 + down * r.time * 14) % w + w) % w; rect(x0 + gx, sy, 1, 3, '#2A2C31', 0.85); }
      rect(x0, sy + 3, w, 1, '#E3B65C', 0.7);
      for (const ax of [x0 + w * 0.3, x0 + w * 0.7]) { rect(ax - 2, sy + 6, 5, 1, '#E3B65C', 0.9); rect(ax - 1, sy + 7, 3, 1, '#E3B65C', 0.9); rect(ax, sy + 8, 1, 1, '#E3B65C', 0.9); }   // 아래 화살표
    }
    if (wet) {   // 전기: 예고 때는 전선 끝에서 불꽃이 튀고, 흐를 때는 물 위로 번개가 친다
      const sh = shockState(r.time), x0 = sx - CONFIG.stepW / 2, w = CONFIG.stepW;
      if (sh.live) {
        rect(x0, sy - 2, w, 4, '#F2E678', 0.35);
        const c = Math.floor(r.time * 30) % 2 ? '#FFF6B0' : '#9FE0FF';
        for (let x = 0; x < w; x += 1 / PX) rect(x0 + x, sy - 1 + Math.round(Math.sin((x + r.time * 40) * 3.1) * 1.5 * PX) / PX, 1 / PX, 1 / PX, c);
        rect(x0 + ((r.time * 97) % w), sy - 6, 1 / PX, 5, c); rect(x0 + ((r.time * 53 + 7) % w), sy - 5, 1 / PX, 4, c);
      } else if (sh.warn && Math.floor(r.time * 20) % 2) {
        rect(x0 + w * 0.82, sy - 3, 1 / PX, 1 / PX, '#FFE680'); rect(x0 + w * 0.86, sy - 4, 1 / PX, 1 / PX, '#FFE680'); rect(x0 + w * 0.78, sy - 2, 1 / PX, 1 / PX, '#FFE680');
      }
    }
    if (shake) {
      rect(sx - CONFIG.stepW / 2 + jx, sy, CONFIG.stepW, tileH, PAL.red, 0.45 * shake);
      for (let d = 0; d < 4; d++) rect(sx - CONFIG.stepW / 2 + ((d * 37 + r.steps * 11) % CONFIG.stepW) + jx, sy + tileH + ((r.time * 60 + d * 9) % 16), 1, 1, '#3A3D43');
    }
    if (!ok) {
      rect(sx - tileW / 2, sy, tileW, tileH, landing ? PAL.red : PAL.gray);
      rect(sx - tileW / 2, sy, tileW, 2, landing ? PAL.text : PAL.green);
    }
    if (landing && i > 0) floorLabels.push([(i / spf + 1) + 'F', sx, sy + 8]);
  }
  floorLabels.forEach(function (l) { rect(l[1] - 8, l[2] - 1, 16, 9, '#141518', 0.55); txt(l[0], l[1], l[2], { size: 6, align: 'center', color: PAL.text }); });   // 층 표시는 칸 위에, 짙은 바탕
  // 난간: 칸마다 기둥, 이웃 칸 기둥끼리 손잡이로 잇는다 (아트 픽셀 단위로 그려 계단처럼 각지게)
  for (let i = Math.max(0, r.steps - 6); i < r.steps + 14; i++) {
    const ax = 160 + (stairX(i) - r.camX), ay = PLAYER_Y + (-i * CONFIG.stepH - r.camY);
    const bx = 160 + (stairX(i + 1) - r.camX), by = ay - CONFIG.stepH;
    if (ay > H + 20 || ay < -20) continue;
    rect(ax - 0.5, ay - 12, 1, 12, RAIL, 0.9);
    const n = Math.max(1, Math.round(Math.max(Math.abs(bx - ax), Math.abs(by - ay)) * PX));
    for (let s = 0; s <= n; s++) rect(Math.round((ax + (bx - ax) * s / n) * PX) / PX - 0.5, Math.round((ay - 12 + (by - ay) * s / n) * PX) / PX, 1, 1 / PX * 2, RAIL);
  }
  } else {           // 연습 모드: 발밑 그림자
    rect(playerScreenX() - 9, PLAYER_Y + 1, 18, 2, '#0A0B0D', 0.45);   // 발밑 그림자
  }
  // 경쟁 사원 (장식)
  if (CONFIG.showRivalCameo && r.floor < CONFIG.rivalFromFloor && r.rivalIdx >= r.steps - 3 && r.rivalIdx <= r.steps + 14) {
    const sx = 160 + (stairX(r.rivalIdx) - r.camX);
    const sy = PLAYER_Y + (-r.rivalIdx * CONFIG.stepH - r.camY);
    ctx.globalAlpha = 0.75;
    drawSprite('rival', 'default', 'idle', time, sx, sy + 1, { scale: MAN_SCALE, fw: 14 * MAN_SCALE, fh: 26 * MAN_SCALE, color: PAL.red, flip: dirAt(r.rivalIdx) < 0 });   // 계단 아래 끝에 서 있는 경쟁 사원 (서 있기)
    ctx.globalAlpha = 1;
  }
  // 플레이어
  const px = playerScreenX();
  const anim = playerAnim(r);
  const rc = RANK_COLORS[r.rankIdx];
  const po = { scale: MAN_SCALE, fw: 16 * MAN_SCALE, fh: 28 * MAN_SCALE, color: rc, flip: r.facing < 0 && anim !== 'parry_left' && anim !== 'parry_right' };
  let pdy = 0;
  if (anim === 'climb_r' || anim === 'climb_l' || anim === 'idle') {                                  // 계단을 달려 오르는 몸짓 (막기·맞기 동작은 그대로)
    const since = r.time - r.lastStepT, run = clamp(1 - (since - 0.14) / 0.4, 0, 1), mdir = dirAt(Math.max(0, r.steps - 1));
    const fx = runFx(r.stepTimer > 0 ? 1 - r.stepTimer / 0.14 : 0, run, mdir, 28 * MAN_SCALE);
    po.sx = fx.sx; po.sy = fx.sy; po.rot = fx.rot; pdy = fx.dy;
    if (r.stepTimer > 0) {                                                  // 발밑 먼지: 디딘 칸 뒤쪽으로 흩어진다
      const k = 1 - r.stepTimer / 0.14;
      for (let j = 0; j < 3; j++) rect(px - mdir * (5 + k * (6 + j * 4)), PLAYER_Y + 1 - k * (2 + j * 2), 2, 2, '#C9CCD1', 0.6 * (1 - k));
    }
  }
  for (let dy = -30; dy <= 0; dy += 2) { const k = (dy + 15) / 17, w = Math.sqrt(Math.max(0, 1 - k * k)) * 13; rect(px - w, PLAYER_Y + dy, w * 2, 2, '#F2E6D0', 0.06); }   // 플레이어 뒤 은은한 빛
  if (QA_HOOK) QA_HOOK('player', null, px, PLAYER_Y + 2 + pdy);
  drawSprite('player', CONFIG.rankNames[r.rankIdx], anim, r.time - r.animStart, px, PLAYER_Y + 2 + pdy, po);
  // 투척물 + 예고
  // 월드 좌표 -> 화면 좌표 (경쟁 사원 발 위치)
  const sx = function (wx) { return 160 + (wx - r.camX); };
  const sy = function (wy) { return PLAYER_Y + (wy - r.camY) + 1; };
  r.leavers.forEach(function (l) {
    const k = (r.time - l.t0) / 0.5;
    ctx.globalAlpha = clamp(1 - k, 0, 1);
    drawSprite('rival', 'default', 'leave', r.time - l.t0, sx(l.wx) + l.side * k * 90, sy(l.wy), { scale: MAN_SCALE, fw: 14 * MAN_SCALE, fh: 26 * MAN_SCALE, color: PAL.red, flip: l.side < 0 });
    ctx.globalAlpha = 1;
  });
  r.fallers.forEach(function (f) {   // 계단 장애물에 당한 경쟁 사원: 감전은 하얗게 떨다 사라지고, 떨어지면 돌며 아래로
    const t = r.time - f.t0, k = t / 0.8, o = { scale: MAN_SCALE, fw: 14 * MAN_SCALE, fh: 26 * MAN_SCALE, color: PAL.red, flip: f.side < 0 };
    if (f.kind === 'shock') {
      ctx.globalAlpha = clamp(1 - k, 0, 1);
      drawSprite('rival', 'default', 'walk', 0, sx(f.wx) + Math.sin(t * 80) * 1.5, sy(f.wy), o);
      ctx.globalAlpha = 1;
      if (Math.floor(t * 20) % 2) rect(sx(f.wx) - 8, sy(f.wy) - 26, 16, 26, '#9FE0FF', 0.5 * (1 - k));
    } else {
      ctx.globalAlpha = clamp(1 - k * 0.8, 0, 1);
      o.rot = f.side * t * 6;
      drawSprite('rival', 'default', 'down', t, sx(f.wx) + f.side * 20 * t, sy(f.wy) + 160 * t * t, o);
      ctx.globalAlpha = 1;
    }
  });
  r.projs.forEach(function (p) {
    if (p.type === 'rival') {
      // 층 바닥 위를 걸어 들어오고, 플레이어가 더 높이 있으면 계단을 따라 쫓아 오른다. 붙으면 플레이어 칸으로 덤벼 친다
      const rp = rivalPos(p), left = p.impactT - r.time;
      let x = sx(rp.x), y = sy(rp.y), anim = 'walk', at = p.phase === 'stand' ? 0 : r.time - p.enterT;
      if (p.locked && left < CONFIG.rivalLeap) {
        const k = 1 - left / CONFIG.rivalLeap;
        x = lerp(x, px + p.side * 12, k); y = lerp(y, PLAYER_Y + 1, k) - Math.sin(k * Math.PI) * 8; anim = 'attack';
      }
      const ro = { scale: MAN_SCALE, fw: 14 * MAN_SCALE, fh: 26 * MAN_SCALE, color: PAL.red, flip: x > px };
      if (!p.locked && (p.phase === 'stand' || p.waiting || (p.phase === 'climb' && p.ri >= r.steps - 1 - 1e-6))) { anim = 'idle'; at = r.time; }   // 서 있기: 바닥에 섰을 때, 기다릴 때, 바로 뒤 칸에서 덤비기 전
      if (p.downT !== undefined && r.time - p.downT < 0.3) { anim = 'down'; at = r.time - p.downT; }   // 밀려 내려가는 중
      if (anim === 'walk' && p.phase === 'climb' && !p.waiting && p.ri < r.steps - 1 - 1e-6) {   // 계단을 달려 오를 때: 앞으로 기울고 다리를 빨리, 칸마다 낮게 들썩
        const f = p.ri - Math.floor(p.ri);
        y -= Math.sin(f * Math.PI) * 2; ro.rot = dirAt(Math.floor(p.ri)) * 0.15; at *= 2; anim = 'climb';
      } else if (anim === 'walk' && p.phase === 'walk') {                       // 층 바닥을 달려올 때: 들썩이며 앞으로 기운다
        y -= Math.abs(Math.sin(at * 16)) * 2; ro.rot = (x > px ? -1 : 1) * 0.1; at *= 1.6;
      }
      if (QA_HOOK) QA_HOOK('rival', p, x, y);
      drawSprite('rival', 'default', anim, at, x, y, ro);
      if (p.waiting && showMarks()) txt('…', x + 10, y - 36, { align: 'center', base: 'middle', size: 9, bold: true, color: '#E3B65C' });   // 계단 장애물 앞에서 기다리는 중
      if (r.time >= p.spawnT && showMarks()) {
        const wk = drawSprite('warning', 'default', 'blink', r.time - p.spawnT, x, y - 35, { anchor: 'c', fw: 8, fh: 12, nofb: true });
        if (!wk && Math.floor(time * 8) % 2 === 0) { rect(x - 5, y - 42, 10, 14, PAL.red); txt('!', x, y - 35, { align: 'center', base: 'middle', size: 10, bold: true, shadow: false }); }
      }
      return;
    }
    const ax = px, ay = PLAYER_Y;   // 투척물은 플레이어를 향한다 (의자만 생길 때의 계단 줄을 따라 정해진 길로)
    if (p.type === 'airplane') { drawAirplanes(p, ax, ay); return; }
    if (p.type === 'envelope') { drawEnvelopes(p, ax, ay); return; }
    if (p.type === 'chair') { drawRollingChair(p, px); return; }
    if (r.time < p.spawnT) return;                           // 모니터: 아직 형광등이 깜박이는 중
    const prog = clamp((r.time - p.spawnT) / Math.max(0.01, p.impactT - p.spawnT), 0, 1), tt = r.time - p.spawnT;   // 날아오는 시간(돌아오는 스테이플러는 0.9초)
    const tx = ax + p.side * 8, ty = ay - 16;                // 맞는 자리 (정해진 자리)
    let x, y;
    if (p.bounce) {                                          // 포스트잇: 튕겨 나갔다가 같은 쪽으로 돌아온다
      x = tx + p.side * 64 * Math.sin(prog * Math.PI); y = ty - 26 * Math.sin(prog * Math.PI);
    } else if (p.type === 'suitcase') {                      // 아래에서 솟아 날아오다가 도중에 터진다
      const x0 = ax + p.side * 80, y0 = H + 14, bx = ax + p.side * 40, by = ay - 34;
      const u = Math.min(1, prog / (1 - CONFIG.suitcaseBurst / CONFIG.warnTime));
      x = lerp(x0, bx, u); y = lerp(y0, by, u) - Math.sin(Math.pow(u, 0.7) * Math.PI) * 56;
    } else if (p.type === 'phone') {                         // 화면 아래에서 솟아 꼭대기에서 잠깐 멈칫한 뒤 중력처럼 빨라지며 떨어진다
      const x0 = ax + p.side * 80, y0 = H + 14;
      x = lerp(x0, tx, prog); y = lerp(y0, ty, prog) - Math.sin(Math.pow(prog, 0.7) * Math.PI) * 84;
    } else if (p.type === 'monitor') {                       // 위층 창이 깨지며 70도 사선으로 떨어진다 (처음엔 느리고 중력처럼 빨라짐)
      const y0 = ty - 96, x0 = tx + p.side * (ty - y0) / Math.tan(CONFIG.monitorAngle * Math.PI / 180), e = prog * (0.25 + 0.75 * prog);
      x = lerp(x0, tx, e); y = lerp(y0, ty, e);
    } else if (p.type === 'paper') {                         // 던진 쪽에서 머리 위로 넘어가, 반대쪽에서 돌아와 맞는다
      const x0 = p.from < 0 ? -16 : W + 16, mx = ax + p.side * 36, my = ay - 36;
      if (prog < 0.65) { const u = prog / 0.65; x = lerp(x0, mx, u); y = lerp(ty, my, u) - Math.sin(u * Math.PI) * 14; }
      else { const v = (prog - 0.65) / 0.35; x = lerp(mx, tx, v); y = lerp(my, ty, v); }
    } else if (p.type === 'stapler' && p.returned) {         // 막힌 스테이플러: 막은 자리에서 뒤로 튕겨 나갔다가 머리 위로 휘어 반대쪽에서 맞는다
      const b = bezier3(prog, [ax - p.side * 8, ty], [ax - p.side * 96, ty - 40], [ax + p.side * 84, ty - 76], [tx, ty]);
      x = b[0]; y = b[1];
    } else {                                                 // 결재판: 옆에서 날아온다
      const x0 = p.side < 0 ? -16 : W + 16;
      x = lerp(x0, tx, prog); y = ty - Math.sin(prog * Math.PI) * 6;
    }
    if (QA_HOOK) QA_HOOK('proj', p, x, y);
    // 회전: 모니터·전화기는 불규칙하게 빙글빙글, 결재판은 납작한 판이 수평으로 도는 것처럼 폭이 줄었다 늘었다 한다
    const so = { anchor: 'c', fw: 12, fh: 9, color: PAL.text, flip: p.side > 0 };
    let at = r.time - p.spawnT;
    if (p.type === 'suitcase' && r.time >= p.impactT - CONFIG.suitcaseBurst) {   // 터졌다: 서류·결재판이 사방으로, 두 장은 플레이어에게
      const t0 = r.time - (p.impactT - CONFIG.suitcaseBurst);
      // 열린 가방은 덮개가 젖혀진 채 돌며 아래로 떨어진다
      drawSprite('projectiles', 'default', 'suitcase_open', 0, x + p.side * 30 * t0, y - 30 * t0 + 160 * t0 * t0, { anchor: 'c', scale: 1.35, rot: p.ph + t0 * 5, fw: 12, fh: 9, color: '#2E2A28' });
      for (let j = 0; j < 7; j++) {
        const ang = p.ph + j * 0.9, toMe = j < 2;
        const vx = toMe ? (tx - x) / CONFIG.suitcaseBurst : Math.cos(ang) * 120, vy = toMe ? (ty - y) / CONFIG.suitcaseBurst : Math.sin(ang) * 90 - 40;
        drawSprite('projectiles', 'default', p.debris, 0, x + vx * t0, y + vy * t0 + (toMe ? 0 : 120 * t0 * t0), { anchor: 'c', scale: 0.7, rot: ang + t0 * 9, fw: 8, fh: 6, color: PAL.text });
      }
      if (t0 < 0.08) rect(x - 10, y - 10, 20, 20, PAL.text, 0.8 - t0 * 10);   // 터지는 빛
      return;
    }
    if (p.type === 'monitor' || p.type === 'phone' || p.type === 'suitcase' || p.type === 'stapler') { so.rot = p.ph + p.spin * tt + 0.8 * Math.sin(p.wob * tt * 3); at = 0; }
    if (p.type === 'suitcase') so.scale = 1.35;             // 서류가방은 크게 (서류·결재판과 헷갈리지 않게)
    else if (p.type === 'clipboard' || p.type === 'coffee' || p.type === 'sticky') { const c = Math.cos(tt * 13 + p.ph); so.sx = (c < 0 ? -1 : 1) * Math.max(0.14, Math.abs(c)); so.sy = 0.82; so.rot = 0.12 * p.side; at = 0; }
    if (p.type === 'stapler') drawStapler(x, y, so.rot || 0, tt, p.side > 0);                                // 스테이플러 (그림 없이 그린다)
    else if (p.type === 'sticky') drawSprite('sticky', STICKY_COLORS[p.hp], 'fly', at, x, y, so);   // 색 = 남은 횟수
    else drawSprite('projectiles', 'default', p.type === 'paper' ? 'document' : p.type, at, x, y, so);
    const wx = clamp(x, 8, W - 8), wy = y - 16;
    if (p.type === 'coffee') { if (showMarks()) txtO('×', wx, wy, { align: 'center', base: 'middle', size: 13, bold: true, color: '#FF5A4E' }); return; }   // 막지 말라는 표시
    if (!showMarks()) return;                                // 10층 위: 위협 위 표시 없음
    const wk = drawSprite('warning', 'default', 'blink', r.time - p.spawnT, wx, wy, { anchor: 'c', fw: 8, fh: 12, nofb: true });
    if (!wk && Math.floor(time * 8) % 2 === 0) {
      rect(wx - 5, wy - 7, 10, 14, PAL.red);
      txt('!', wx, wy, { align: 'center', base: 'middle', size: 10, bold: true, shadow: false });
    }
    if (p.type === 'stapler' && p.returned) txt('↺', wx + 9, wy, { align: 'center', base: 'middle', size: 8, bold: true, color: '#E3B65C' });   // 돌아오는 중
  });
  // 효과
  r.sparks.forEach(function (s) { rect(s.x, s.y, 2, 2, s.color, 1 - s.t / 0.4); });
  r.texts.forEach(function (t) {   // 떠오르는 글자: 0.3초 동안 또렷하게, 밝은 색 + 테두리
    ctx.globalAlpha = t.t < 0.3 ? 1 : clamp(1 - (t.t - 0.3) / 0.5, 0, 1);
    txtO(t.s, t.x, t.y, { align: 'center', size: 9, bold: true, color: t.color === PAL.green ? '#C8F06A' : t.color === PAL.red ? '#FF6A5E' : t.color });
    ctx.globalAlpha = 1;
  });
  // 모니터 예고: 형광등이 불규칙하게 깜박인다 (화면이 어두워졌다 밝아짐, 떨어질 쪽 위가 더 깜박임)
  r.projs.forEach(function (p) {
    if (p.type !== 'monitor' || p.done || r.time > p.spawnT + 0.12) return;
    const f = [0.5, 0.05, 0.38, 0, 0.55, 0.12, 0.3, 0][Math.floor((r.time - p.warnT) * 24) % 8];
    rect(0, 0, W, H, '#08090B', f);
    rect(px + p.side * 52 - 30, 0, 60, 3, PAL.text, Math.max(0, 0.22 - f * 0.35));
  });
  // 상사의 눈 (앞): 레이저가 플레이어 위로도 보이게 옅게 한 번 더, 예고 숫자
  if (gz && gz.sweep) { rect(0, gz.y - CONFIG.gazeWidth / 2, W, CONFIG.gazeWidth, '#C0302E', 0.14); txtO('멈추기!', 160, 40, { align: 'center', base: 'middle', size: 8, bold: true, color: '#FF8A7A' }); }
  if (r.flash > 0) rect(0, 0, W, H, PAL.red, clamp(r.flash / 0.35, 0, 1) * 0.35);
  // 카메라 플래시: 화면이 하얗게 번쩍이고, 터진 자리에 빛줄기가 남는다 (막을 수 없고 피해도 없다)
  if (r.camFlash) {
    const f = r.camFlash, k = f.t / CONFIG.flashTime;
    rect(0, 0, W, H, '#F5F5F0', 0.85 * k);
    rect(f.x - 14 * k - 1, f.y, 28 * k + 2, 1, '#FFFFFF', k); rect(f.x, f.y - 14 * k - 1, 1, 28 * k + 2, '#FFFFFF', k);
    rect(f.x - 3, f.y - 3, 6, 6, '#FFFFFF', k);
  }
}
// 새 장애물 그림 (스프라이트 없이 칸 단위로 그린다)
function decoyHash(a, b) { const v = Math.sin(a * 12.9898 + b * 78.233) * 43758.5453; return v - Math.floor(v); }
function drawDecoyGlints() {   // 가짜 불꽃 5개: 저마다 다른 박자로 나타났다 사라지고, 사라질 때마다 자리를 옮긴다
  for (let i = 0; i < 5; i++) {
    const P = 1.9 + i * 0.43, e = time + i * 0.71, n = Math.floor(e / P), u = (e - n * P) / P, fade = Math.pow(Math.sin(u * Math.PI), 2);
    drawGlint(Math.round(16 + decoyHash(i, n) * 288), Math.round(14 + decoyHash(i + 7, n) * 80), 2 + fade * 1.5, fade * 0.9);
  }
}
function drawGlint(x, y, s, a) {   // 반짝임 (네 갈래 빛줄기와 가운데 점)
  const S = Math.round(s);
  rect(x - S, y, S * 2 + 1, 1, '#FFD0C8', a); rect(x, y - S, 1, S * 2 + 1, '#FFD0C8', a);
  rect(x - 1, y - 1, 3, 3, '#FFFFFF', a); rect(x - 2, y - 2, 5, 5, '#FF5A4E', a * 0.35);
}
function bezier3(u, a, b, c, d) {   // 3차 베지어 곡선 위의 점
  const v = 1 - u, w0 = v * v * v, w1 = 3 * v * v * u, w2 = 3 * v * u * u, w3 = u * u * u;
  return [w0 * a[0] + w1 * b[0] + w2 * c[0] + w3 * d[0], w0 * a[1] + w1 * b[1] + w2 * c[1] + w3 * d[1]];
}
function drawStapler(x, y, rot, t, flip) {   // 스테이플러 (obstacles.png stapler: 입이 딱딱). 그림이 없으면 사각형으로
  if (drawSprite('obstacles', 'stapler', 'snap', t || 0, x, y, { anchor: 'c', fw: 12, fh: 12, nofb: true, rot: rot, flip: flip })) return;
  ctx.save(); ctx.translate(Math.round(x), Math.round(y)); ctx.rotate(rot);
  rect(-6, -1, 12, 3, '#3A3D43'); rect(-6, -3, 10, 2, '#9AA0A8'); rect(3, -3, 3, 1, '#A13D3D'); rect(-6, 2, 12, 1, '#2A2C31');
  ctx.restore();
}
function drawChair(x, y, rot, o) {   // 사무실 의자 (obstacles.png chair, 바퀴가 돈다). 그림이 없으면 사각형으로
  o = o || {};
  if (drawSprite('obstacles', 'chair', 'roll', o.t || 0, x, y - 0.5, { anchor: 'c', fw: 16, fh: 16, nofb: true, rot: rot, sx: o.sx, sy: o.sy, flip: o.flip })) return;
  ctx.save(); ctx.translate(Math.round(x), Math.round(y)); ctx.rotate(rot);
  rect(-5, -11, 3, 9, '#3A4A6A'); rect(-5, -3, 10, 3, '#3A4A6A'); rect(-4, -2, 8, 1, '#4E6290');
  rect(-1, 0, 2, 4, '#9AA0A8'); rect(-5, 4, 10, 1, '#2A2C31'); rect(-5, 5, 2, 2, '#1E1F22'); rect(3, 5, 2, 2, '#1E1F22');
  ctx.restore();
}
function drawRollingChair(p, px) {   // 튀어 내려오는 의자: 자리는 chairPos(규칙과 같다). 내려앉을 때 눌리고, 공중에서는 아치를 따라 기운다
  const r = run, s = p.side;
  const c = chairPos(p, r.time);                                           // 규칙과 같은 자리
  const x = 160 + (c.x - r.camX), y = PLAYER_Y + (c.y - r.camY);
  let rot = -s * 0.3, ground = false;
  if (c.phase === 'fall') rot = -s * (0.3 + c.e * 10);
  else if (c.phase === 'drop') rot = -s * (0.3 + c.u * 0.6);
  else if (c.phase === 'hop') { ground = c.u < 0.1 || c.u > 0.94; rot = -s * (0.3 + (c.u - 0.5) * 0.7); }
  if (QA_HOOK) QA_HOOK('chair', p, x, y, ground);
  drawChair(x, y, rot, { t: r.time - p.spawnT, flip: s > 0, sx: ground ? 1.16 : 0.95, sy: ground ? 0.82 : 1.06 });
  if (showMarks() && p.fallT === null && chairHitT(r, p)) {               // 나에게 닿을 의자에만 "!"
    const wx = clamp(x, 8, W - 8), wy = y - 20;
    const wk = drawSprite('warning', 'default', 'blink', r.time - p.spawnT, wx, wy, { anchor: 'c', fw: 8, fh: 12, nofb: true });
    if (!wk && Math.floor(time * 8) % 2 === 0) { rect(wx - 5, wy - 7, 10, 14, PAL.red); txt('!', wx, wy, { align: 'center', base: 'middle', size: 10, bold: true, shadow: false }); }
  }
}
function drawEnvelope(x, y) {   // 편지 봉투: 흰 봉투, 접힌 날개
  const X = Math.round(x), Y = Math.round(y);
  rect(X - 5, Y - 3, 10, 7, '#5A5F66'); rect(X - 4, Y - 2, 8, 5, '#E8E6DF');
  rect(X - 4, Y - 2, 2, 1, '#9AA0A8'); rect(X - 2, Y - 1, 2, 1, '#9AA0A8'); rect(X, Y - 1, 2, 1, '#9AA0A8'); rect(X + 2, Y - 2, 2, 1, '#9AA0A8'); rect(X - 1, Y, 2, 1, '#A13D3D');
}
function drawEnvelopes(p, px, py) {   // px, py: 정해진 자리   // 편지 3연발: 같은 쪽에서 envelopeGap 박자로 한 장씩
  const r = run, dur = Math.max(0.01, p.impactT - p.spawnT), tx = px + p.side * 8, ty = py - 16, x0 = p.side < 0 ? -16 : W + 16;
  for (let k = p.k; k < CONFIG.envelopeCount; k++) {
    const t0 = p.spawnT + k * CONFIG.envelopeGap;
    if (r.time < t0) continue;
    const u = clamp((r.time - t0) / dur, 0, 1);
    const ex = lerp(x0, tx, u), ey = ty - Math.sin(u * Math.PI) * 14 + (k % 2) * 3, et = r.time - t0;
    for (let j = 1; j <= 3; j++) rect(ex + p.side * (4 + j * 4) - 1, ey - 1 + (j % 2) * 2, 4, 1, '#E8E6DF', 0.42 - j * 0.12);   // 뒤에 남는 바람 줄
    if (QA_HOOK) QA_HOOK('envelope', p, ex, ey, k);
    if (!drawSprite('obstacles', 'envelope', 'fly', et + k * 0.13, ex, ey, { anchor: 'c', fw: 12, fh: 12, nofb: true, rot: Math.sin(et * 14 + k) * 0.22, flip: p.side > 0 }))
      drawEnvelope(ex, ey);                                                // 돌며 펄럭이고 흔들린다
  }
  if (r.time >= p.spawnT && showMarks()) {
    const u = clamp((r.time - p.spawnT) / dur, 0, 1), wx = clamp(lerp(x0, tx, u), 8, W - 8), wy = ty - 28;
    const wk = drawSprite('warning', 'default', 'blink', r.time - p.spawnT, wx, wy, { anchor: 'c', fw: 8, fh: 12, nofb: true });
    if (!wk && Math.floor(time * 8) % 2 === 0) { rect(wx - 5, wy - 7, 10, 14, PAL.red); txt('!', wx, wy, { align: 'center', base: 'middle', size: 10, bold: true, shadow: false }); }
    txt('×' + (CONFIG.envelopeCount - p.k), wx + 10, wy, { align: 'center', base: 'middle', size: 7, bold: true, color: '#E3B65C' });
  }
}
function drawPlane(x, y, side, t) {   // 종이비행기 (obstacles.png plane: 날개가 파닥, 코가 날아가는 쪽). 그림이 없으면 사각형으로
  if (drawSprite('obstacles', 'plane', 'flap', t || 0, x, y, { anchor: 'c', fw: 10, fh: 10, nofb: true, flip: side > 0 })) return;
  const f = -side, X = Math.round(x), Y = Math.round(y);
  rect(X - 4, Y, 9, 1, '#E8E6DF'); rect(f > 0 ? X - 3 : X - 2, Y - 1, 6, 1, '#E8E6DF'); rect(f > 0 ? X - 4 : X + 1, Y - 2, 4, 1, '#C9CCD1');
  rect(f > 0 ? X - 2 : X - 1, Y + 1, 4, 1, '#9AA0A8');
}
function drawAirplanes(p, px, py) {   // px, py: 정해진 자리   // 종이비행기 편대: 비행기마다 airplaneGap만큼 늦게 같은 쪽에서
  const r = run, dur = Math.max(0.01, p.impactT - p.spawnT), tx = px + p.side * 8, ty = py - 16, x0 = p.side < 0 ? -16 : W + 16;
  for (let k = p.k; k < CONFIG.airplaneCount; k++) {
    const t0 = p.spawnT + k * CONFIG.airplaneGap;
    if (r.time < t0) continue;
    const u = clamp((r.time - t0) / dur, 0, 1);
    const qx = lerp(x0, tx, u), qy = ty - 6 + (k % 2) * 3 - Math.sin(u * Math.PI) * 10;
    if (QA_HOOK) QA_HOOK('plane', p, qx, qy, k);
    drawPlane(qx, qy, p.side, r.time - t0 + k * 0.07);
  }
  if (r.time >= p.spawnT && showMarks()) {
    const u = clamp((r.time - p.spawnT) / dur, 0, 1), wx = clamp(lerp(x0, tx, u), 8, W - 8), wy = ty - 26;
    const wk = drawSprite('warning', 'default', 'blink', r.time - p.spawnT, wx, wy, { anchor: 'c', fw: 8, fh: 12, nofb: true });
    if (!wk && Math.floor(time * 8) % 2 === 0) { rect(wx - 5, wy - 7, 10, 14, PAL.red); txt('!', wx, wy, { align: 'center', base: 'middle', size: 10, bold: true, shadow: false }); }
    txt('꾹!', wx, wy - 12, { align: 'center', base: 'middle', size: 7, bold: true, color: '#E3B65C' });
  }
}
function plate(x, y, w, h) {   // 05-3 컨셉의 이중 테두리 명판
  rect(x, y, w, h, PAL.bg, 0.92); frameRect(x, y, w, h, '#9AA0A8'); frameRect(x + 2, y + 2, w - 4, h - 4, '#5A5F66');
}
function drawKeyCap(x, y, idx, iconName, label) {
  const lit = time < padUntil[idx] || (idx >= 2 && !demo && screen === 'play' && run && run.held[idx - 2]);   // 누르고 있는 막기 키도 켠다
  rect(x - 1, y - 1, 20, 20, '#121317', 0.92);                          // 컨셉 키 아이콘: 짙은 바깥 테두리
  rect(x, y, 18, 18, lit ? PAL.green : '#2B2E35');
  frameRect(x + 1, y + 1, 16, 16, lit ? PAL.text : '#C9CCD1');          // 밝은 안쪽 테두리
  const ok = drawSprite('hud_icons', 'default', iconName, time, x + 3, y + 3, { anchor: 'tl', dw: 12, dh: 12, nofb: true });
  if (!ok) txt(label, x + 9, y + 9, { align: 'center', base: 'middle', size: 9, bold: true, color: lit ? PAL.bg : PAL.text, shadow: false });
  rect(x + 12, y + 12, 9, 8, '#121317'); rect(x + 13, y + 13, 7, 6, '#C24A64');   // 오른쪽 아래 빨간 딱지: 키 이름
  txt(label, x + 16.5, y + 16, { align: 'center', base: 'middle', size: 5, bold: true, color: '#FFFFFF', shadow: false });
}
function drawHud() {
  const r = run;
  // 1 층수·직급 명판
  plate(6, 5, 86, 19);
  drawBadgeIcon(10, 9, 12);
  if (r.mode === 'practice') {                 // 연습: 층 대신 모드 이름, 일수·하트 대신 막은 수와 맞은 수
    txt('연습 모드', 56, 14.5, { align: 'center', base: 'middle', size: 9, bold: true });
    plate(W - 92, 5, 86, 19);
    txt('막음 ' + r.parries + ' · 맞음 ' + r.hits, W - 49, 14.5, { align: 'center', base: 'middle', size: 8, bold: true });
    txt('ESC → 메인으로', W - 49, 31, { align: 'center', base: 'middle', size: 6, color: '#9AA0A8' });
  } else {
    txt((r.mode === 'endless' ? '∞ ' : '') + r.floor + 'F · ' + CONFIG.rankNames[r.rankIdx], 56, 14.5, { align: 'center', base: 'middle', size: r.mode === 'endless' ? 8 : 9, bold: true });
    // 2 남은 일수 명판 + 하트
    const dayInt = Math.ceil(r.days);
    const low = r.days <= 7;
    plate(W - 92, 5, 86, 19);
    const ok = drawSprite('hud_icons', 'default', 'calendar', 0, W - 88, 9, { anchor: 'tl', dw: 12, dh: 12, nofb: true });
    if (!ok) rect(W - 88, 9, 12, 12, PAL.gray);
    txt('D-' + dayInt, W - 44, 14.5, { align: 'center', base: 'middle', size: 10, bold: true, color: low ? PAL.red : PAL.text });
    rect(W - 89, 21, 80 * clamp(r.days / CONFIG.maxDays, 0, 1), 1, low ? PAL.red : PAL.green);   // 일수 게이지
    for (let i = 0; i < CONFIG.maxHearts; i++) drawHeart(W - 8 - (CONFIG.maxHearts - i) * 13, 27, i < r.hearts);
  }
  // 콤보 (가운데 위, 금색)
  if (r.combo > 0) {
    // COMBO = 연속으로 막은 횟수, 그 아래 점수 배수 (3연속마다 ×1, 최대 ×10)
    txt(r.combo + ' COMBO', 160, 8, { align: 'center', size: 10, bold: true, color: '#E3B65C' });
    txtO('점수 ×' + r.mult, 160, 21, { align: 'center', size: 6, bold: true, color: r.mult >= CONFIG.maxMult ? '#FF8A7A' : '#E8E6DF' });
  }
  // 4 조작 키 (아래 가운데)
  drawKeyCap(114, 158, 0, 'arrow_left', '←');
  drawKeyCap(136, 158, 1, 'arrow_right', '→');
  drawKeyCap(166, 158, 2, 'shield_q', 'Q');
  drawKeyCap(188, 158, 3, 'shield_e', 'E');
}
function drawPlay() {
  const r = run;
  let ox = 0, oy = 0;
  if (r.shake > 0) { ox = Math.round((Math.random() * 2 - 1) * 2); oy = Math.round((Math.random() * 2 - 1) * 2); }
  ctx.save(); ctx.translate(ox, oy);
  drawPlayWorld();
  ctx.restore();
  drawHud();
}

/* ---------------------------------------------------------------------
 * 05-4 일시정지 / 설정
 * ------------------------------------------------------------------- */
function drawPause() {
  drawPlay();
  rect(0, 0, W, H, PAL.bg, 0.65);                       // 5 반투명 오버레이
  rect(80, 38, 160, 100, PAL.bg, 0.95); frameRect(80, 38, 160, 100, PAL.gray);
  txt('일시정지', 160, 48, { align: 'center', size: 12, bold: true });   // 1 제목
  drawButtons();
}
function drawSettings() {
  rect(0, 0, W, H, PAL.bg, 0.6);
  rect(50, 14, 220, 152, PAL.bg, 0.97); frameRect(50, 14, 220, 152, PAL.gray);
  txt('설정', 160, 19, { align: 'center', size: 10, bold: true });
  txt('사운드', 62, 37, { size: 8 });
  txt('볼륨', 62, 55, { size: 8 });
  txt(Math.round(Snd.vol * 100) + '%', 214, 55, { align: 'center', size: 8 });
  txt('화면', 62, 73, { size: 8 });
  txt('배율 ×' + scale + ' (창 크기에 맞춰 자동)', 62, 86, { size: 7, color: PAL.green });
  txt('조작법', 62, 99, { size: 8 });
  txt('장애물', 62, 117, { size: 8 });
  txt('← → 이동 · Q E 패링 · ESC 일시정지 (키 변경 불가)', 62, 132, { size: 6, color: PAL.gray });
  drawButtons();
}

function drawHelp() {
  rect(0, 0, W, H, PAL.bg, 0.7);
  rect(50, 16, 220, 150, PAL.bg, 0.97); frameRect(50, 16, 220, 150, PAL.gray);
  txt('조작법', 160, 21, { align: 'center', size: 10, bold: true });
  const rows = [
    ['← →', '다음 계단 방향으로 한 칸 오르기'],
    ['Q', '왼쪽 패링'],
    ['E', '오른쪽 패링'],
    ['ESC', '일시정지'],
    ['Enter', '메뉴 결정 (방향키 선택·클릭)'],
  ];
  rows.forEach(function (row, i) {
    const y = 34 + i * 13;
    rect(60, y, 36, 13, PAL.gray); frameRect(60, y, 36, 13, PAL.text);
    txt(row[0], 78, y + 7, { align: 'center', base: 'middle', size: 7, bold: true });
    txt(row[1], 104, y + 7, { base: 'middle', size: 7 });
  });
  txt('"!" 1초 뒤 도착 · 도착 직전 0.2초 안에 맞는 쪽 키 (소리의 박)', 160, 100, { align: 'center', size: 6, color: PAL.green });
  txt('11층~ 경쟁 사원 · 커피(× 표시: 막으면 하트 -1) · 15층~ 스테이플러(반대쪽)', 160, 106, { align: 'center', size: 6, color: '#D9DCE0' });
  txt('20층~ 종이비행기(키 꾹) · 에스컬레이터 · 21층~ 모니터 · 금 간 계단', 160, 112, { align: 'center', size: 6, color: '#D9DCE0' });
  txt('25층~ 편지 3장(박자) · 30층~ 젖은 계단 · 튀어 내려오는 의자(내 칸이면 막기)', 160, 118, { align: 'center', size: 6, color: '#D9DCE0' });
  txt('31층~ 전화기 · 35층~ 거꾸로 칸(반대 키) · 41층~ 서류 · 사라지는 칸', 160, 124, { align: 'center', size: 6, color: '#D9DCE0' });
  txt('61층~ 상사의 눈 · 포스트잇(색=횟수) · 카메라 플래시(무해)', 160, 130, { align: 'center', size: 6, color: '#D9DCE0' });
  txt('71층~ 서류가방(터질 때 막기)', 160, 136, { align: 'center', size: 6, color: '#D9DCE0' });
  txt('틀린 방향·놓친 공격은 하트 -1, 새 층마다 하트 +1', 160, 142, { align: 'center', size: 6, color: PAL.text });
  drawButtons();
}

/* ---------------------------------------------------------------------
 * 장애물 보기: 장애물마다 짧은 장면을 실제 규칙(updatePlay)으로 돌리고, 자동으로 맞는 키를 맞는 순간에 눌러 보여 준다.
 * 설정 화면의 "장애물 · 보기"와 조작법 화면의 "장애물 보기"에서 연다 (설정은 타이틀·로비·일시정지·결과에서). 기록·점수에는 남지 않는다.
 * 장면은 따로 만든 판(demo.run, demo.st)에서 돌고, 돌리거나 그릴 때만 run/st/screen을 잠깐 바꿔 끼운다.
 * 칸 번호: 계단 장애물은 칸 번호로 정해진다 (171 금 감, 189 젖음, 205 에스컬레이터, 262 사라짐. 156~161, 372, 402~407 근처는 안전)
 * ------------------------------------------------------------------- */
const DEMO_SEED = 7;
const DEMOS = [
  { id: 'climb', name: '계단 오르기', how: '다음 계단이 놓인 쪽 화살표(← →). 틀리면 하트 -1, 오르고 막을수록 남은 일수가 늘어요', at: 0, climb: [0.5, 0.85, 1.2, 1.55, 1.9, 2.25, 2.6], dur: 3.4 },
  { id: 'clipboard', name: '결재판', how: '"딱… 딱!"의 두 번째 딱에 날아오는 쪽 키 (Q 왼쪽 · E 오른쪽)', at: 156, threat: 'clipboard' },
  { id: 'rival', name: '경쟁 사원', how: '층 바닥을 걸어와 계단을 쫓아와요. 덤비는 순간 내가 올라온 쪽(뒤쪽) 키', at: 156, threat: 'rival', climb: [0.9, 1.25, 1.6, 1.95] },
  { id: 'coffee', name: '커피', how: '× 표시는 막지 마세요! 막으면 하트 -1, 그냥 두면 지나갑니다', at: 156, threat: 'coffee' },
  { id: 'monitor', name: '모니터', how: '형광등이 깜박이면 위에서 떨어져요. 유리 깨지는 소리 1초 뒤에 막기', at: 156, threat: 'monitor' },
  { id: 'phone', name: '전화기', how: '아래에서 솟아올라요. 두 번째 벨이 울리는 순간에 막기', at: 156, threat: 'phone' },
  { id: 'paper', name: '서류', how: '던진 쪽 반대편에서 맞아요. "!"가 뜬 쪽(맞는 쪽) 키로', at: 156, threat: 'paper' },
  { id: 'sticky', name: '포스트잇', how: '막아도 튕겨 돌아와요. 색 = 남은 횟수 (분홍5 파랑4 초록3 노랑2 흰색1)', at: 156, threat: 'sticky', hp: 3 },
  { id: 'suitcase', name: '서류가방', how: '"똑, 똑, 펑!" 터지는 순간에 막기', at: 156, threat: 'suitcase' },
  { id: 'stapler', name: '스테이플러', how: '막으면 뒤로 튕겼다가 머리 위로 휘어 반대쪽에서 돌아와요. 반대쪽 키로 한 번 더 (↺)', at: 156, threat: 'stapler' },
  { id: 'airplane', name: '종이비행기 편대', how: '날아오는 쪽 막기 키를 꾹 누르고 있기. 마지막 비행기까지 떼지 마세요', at: 156, threat: 'airplane' },
  { id: 'envelope', name: '편지 3연발', how: '같은 쪽에서 박자에 맞춰 세 장이 와요. 한 장씩 "착, 착, 착" 세 번 막기', at: 156, threat: 'envelope' },
  { id: 'chair', name: '튀어 내려오는 의자', how: '계단을 두 칸씩 튀며 내려와요. 내 칸에 내려앉을 때 맞으니 가만히 서서 오는 쪽 키로 막기', at: 186, threat: 'chair' },
  { id: 'chairdodge', name: '의자 피하기', how: '의자가 내려앉는 칸에 없으면 맞지 않아요. 한 칸 올라 피하기', at: 186, threat: 'chair', climb: [1.0] },
  { id: 'reverse', name: '거꾸로 칸', how: '보라색 칸에서는 반대쪽 화살표를 눌러야 올라가요 ("반대로!")', at: 236, climb: [0.5, 0.85, 1.2, 1.55], dur: 2.4 },
  { id: 'cracked', name: '금 간 계단', how: '서 있으면 흔들리다 1.5초면 무너져요. 멈추지 말고 다음 칸으로', at: 170, climb: [0.5, 1.5, 1.85, 2.2], dur: 3.2 },
  { id: 'escalator', name: '내려가는 에스컬레이터', how: '쇠빛 칸에 가만히 서 있으면 아래로 밀려요. 쉬지 말고 지나가기', at: 205, climb: [1.6, 1.95, 2.3, 2.65], dur: 3.4 },
  { id: 'shock', name: '젖은 계단', how: '불꽃이 튀면 멈추고, 전기가 흐르고 난 뒤에 건너기 (3초마다 감전)', at: 188, wait: 'wet' },
  { id: 'vanish', name: '사라지는 칸', how: '하늘색 선 칸은 5초 있다가 1초 없어져요. 깜박이면 기다렸다가 다시 생기면 건너기', at: 261, wait: 'vanish', vanishPhase: 3.8 },
  { id: 'rivalwait', name: '경쟁 사원과 사라지는 칸', how: '경쟁 사원도 사라지는 칸·전기가 흐르는 칸 앞에서는 멈춰 기다려요', at: 356, threat: 'rival', vanishPhase: 3.6 },
  { id: 'rivalcrack', name: '경쟁 사원과 금 간 칸', how: '경쟁 사원은 금 간 칸에 0.8초만 서 있어도 떨어지고, 젖은 칸에서 전기가 흐르면 감전돼요', at: 212, threat: 'rival' },
  { id: 'gaze', name: '상사의 눈', how: '먼 곳에서 불꽃이 깜박이고 5초 뒤 거대한 레이저가 순식간에(0.6초) 화면을 쓸어내려요. 레이저가 내 위를 지날 때는 오르지 않기', at: 402, wait: 'gaze', gazeT0: -3.6 },
  { id: 'flash', name: '카메라 플래시', how: '막을 수 없지만 피해도 없어요. 눈이 부셔도 계속 오르세요', at: 372, flash: 0.8, climb: [1.5, 1.85, 2.2], dur: 3.2 },
];
const DEMO_KEY_X = [114, 136, 166, 188];

function openDemo(id) {
  const i = DEMOS.findIndex(function (d) { return d.id === id; });
  demo = { idx: 0, run: null, st: null };
  demoGo(i < 0 ? 0 : i);
  focus = 1;   // Enter = 다음
}
function closeDemo() { demo = null; focus = 0; }
// 장면 하나를 돌리는 동안만 run/st/screen을 장면 것으로 바꿔 끼운다 (진행 중인 판은 그대로 남는다)
function withDemo(fn) {
  const sr = run, sst = st, ss = screen;
  run = demo.run; st = demo.st; screen = 'play';
  try { fn(); } finally { demo.st = st; run = sr; st = sst; screen = ss; }
}
function demoGo(i) { withDemo(function () { demoStart(i); }); }
function demoStart(i) {
  const d = demo;
  d.idx = (i + DEMOS.length) % DEMOS.length;
  const sc = DEMOS[d.idx];
  const r = makeRun(DEMO_SEED, 'normal');
  run = d.run = r;
  r.steps = sc.at; r.floor = floorOfSteps(sc.at); r.facing = dirAt(r.steps);
  r.rankIdx = Math.min(CONFIG.rankNames.length - 1, CONFIG.promoteFloors.filter(function (f) { return r.floor >= f; }).length);
  r.pvx = r.camX = stairX(r.steps); r.pvy = r.camY = -r.steps * CONFIG.stepH;
  r.nextAttack = sc.threat ? 0.6 : 1e9; r.forceType = sc.threat || null;
  r.nextFlash = sc.flash || 1e9;
  r.vanishPhase = sc.vanishPhase || 0;
  if (sc.gazeT0 !== undefined) r.gazeT0 = sc.gazeT0;
  Object.assign(d, { ci: 0, doneAt: sc.dur || null, spawned: false, crossed: null, sawLive: false, waiting: false, nowKey: 0, nowUntil: 0, nowText: '지금!', holdUntil: 0 });
}
function demoPress(d, k, text) { d.nowKey = k; d.nowUntil = time + 0.35; d.nowText = text || '지금!'; }
function demoUpdate(dt) {
  withDemo(function () {
    const d = demo, sc = DEMOS[d.idx], r = run;
    if (r.ended) { demoStart(d.idx); return; }                       // 혹시 끝나면 처음부터
    // 정해진 때에 오르기
    while (sc.climb && d.ci < sc.climb.length && r.time >= sc.climb[d.ci]) { const rev = isReverse(r.steps), dir = needDir(r.steps); keyStep(dir); demoPress(d, dir < 0 ? 0 : 1, rev ? '반대로!' : undefined); d.ci++; }
    // 기다렸다 건너기: 젖은 계단(전기), 사라지는 칸(없어짐), 상사의 눈(훑기)이 지나가면 건넌다
    if (sc.wait) {
      const sh = shockState(r.time), vs = vanishState(r), gz = gazeState(r);
      const danger = sc.wait === 'wet' ? sh.live : sc.wait === 'vanish' ? vs.gone : !!(gz && gz.sweep);
      const soon = sc.wait === 'wet' ? sh.warn : sc.wait === 'vanish' ? vs.warn : !!(gz && gz.warn);
      if (d.crossed === null) {
        if (danger) d.sawLive = true;
        d.waiting = danger || soon;
        if (d.sawLive && !danger) { const rev = isReverse(r.steps), dir = needDir(r.steps); keyStep(dir); demoPress(d, dir < 0 ? 0 : 1, rev ? '반대로!' : undefined); d.crossed = r.time; d.waiting = false; }
      } else {
        const k = d.crossed;
        if (d.ci < 2 && r.time >= k + 0.35 * (d.ci + 1)) { const rev = isReverse(r.steps), dir = needDir(r.steps); keyStep(dir); demoPress(d, dir < 0 ? 0 : 1, rev ? '반대로!' : undefined); d.ci++; }
        if (d.doneAt === null) d.doneAt = k + 1.8;
      }
    }
    // 막기: 막을 쪽이 정해진 위협은 도착 botParryLead 전에 (커피는 누르지 않는다). 종이비행기는 꾹 누르고 있기
    r.held = [false, false];
    r.projs.forEach(function (p) {
      if (p.type !== 'airplane' || p.done || r.time < p.impactT - CONFIG.botParryLead) return;
      const s = p.side < 0 ? 0 : 1; r.held[s] = true; padUntil[s + 2] = time + 0.05; demoPress(d, s + 2, '꾹!');
    });
    r.projs.forEach(function (p) {
      if (p.done || p.type === 'airplane') return;
      const ch = p.type === 'chair' ? chairHitT(r, p) : null; if (p.type === 'chair' && !ch) return;
      const tti = (ch ? ch.t : p.type === 'envelope' ? p.impactT + p.k * CONFIG.envelopeGap : p.impactT) - r.time, s = p.side < 0 ? 0 : 1;
      if (p.type === 'coffee') { if (tti > 0 && tti <= 0.35) d.holdUntil = time + 0.45; return; }
      if (p.locked !== false && tti > 0 && tti <= CONFIG.botParryLead && r.time >= r.parryCd[s]) { keyParry(p.side); demoPress(d, s + 2); }
    });
    updatePlay(dt);
    if (r.forceType && r.projs.length) {                              // 한 번만 내보낸다
      r.forceType = null; r.nextAttack = 1e9; d.spawned = true;
      if (sc.hp) r.projs[0].hp = sc.hp;
    }
    if (sc.flash && r.camFlash) r.nextFlash = 1e9;
    d.rivalWait = r.projs.some(function (p) { return p.type === 'rival' && p.waiting; });   // 경쟁 사원이 계단 장애물 앞에서 기다리는 중
    if (sc.threat && d.spawned && !r.projs.length && d.doneAt === null) d.doneAt = r.time + 1.0;
    r.hearts = CONFIG.maxHearts; r.days = CONFIG.maxDays;            // 장면은 끝나지 않는다
    if (d.doneAt !== null && r.time >= d.doneAt) demoStart(d.idx + 1);
  });
}
function drawDemo() {
  const d = demo, sc = DEMOS[d.idx];
  withDemo(function () { drawPlayWorld(); });
  rect(0, 0, W, 36, PAL.bg, 0.88); rect(0, 36, W, 1, '#9AA0A8', 0.6);
  txt('장애물 보기 ' + (d.idx + 1) + '/' + DEMOS.length, 6, 4, { size: 6, color: '#9AA0A8' });
  txt(sc.name, 160, 4, { align: 'center', size: 11, bold: true, color: '#E3B65C' });
  txt(sc.how, 160, 22, { align: 'center', size: 6 });
  for (let i = 0; i < 4; i++) drawKeyCap(DEMO_KEY_X[i], 158, i, ['arrow_left', 'arrow_right', 'shield_q', 'shield_e'][i], ['←', '→', 'Q', 'E'][i]);
  if (time < d.nowUntil) {                                          // 지금 누른 키
    const kx = DEMO_KEY_X[d.nowKey] + 9;
    txtO(d.nowText, kx, 134, { align: 'center', base: 'middle', size: 7, bold: true, color: '#F2C46A' });   // 층 표시와 겹치지 않게 위로
    rect(kx - 1, 140, 2, 16, '#E3B65C');
  }
  if (time < d.holdUntil) txtO('누르지 마세요!', 160, 146, { align: 'center', base: 'middle', size: 7, bold: true, color: PAL.red });
  if (d.waiting) txt('기다리기…', 160, 146, { align: 'center', base: 'middle', size: 7, bold: true, color: '#E3B65C' });
  else if (d.rivalWait && time >= d.nowUntil) txt('경쟁 사원도 기다려요', 160, 146, { align: 'center', base: 'middle', size: 7, bold: true, color: '#E3B65C' });
  drawButtons();
}

/* ---------------------------------------------------------------------
 * 05-5 결과
 * ------------------------------------------------------------------- */
function drawVerdict(x, y, w, h, promo, subtext) {
  ctx.save();
  ctx.translate(x + w / 2, y + h / 2); ctx.rotate(-0.04);
  rect(-w / 2, -h / 2, w, h, promo ? PAL.green : PAL.red);
  frameRect(-w / 2 + 2, -h / 2 + 2, w - 4, h - 4, promo ? PAL.bg : PAL.text);
  txt(promo ? '승급' : '구조조정 대상', 0, -6, { align: 'center', base: 'middle', size: 18, bold: true, color: promo ? PAL.bg : PAL.text, shadow: false });
  txt(subtext, 0, 12, { align: 'center', base: 'middle', size: 7, color: promo ? PAL.bg : PAL.text, shadow: false });
  ctx.restore();
}
function resultRows() {
  const r = result;
  return [
    ['도달 층수', r.floor + 'F'],
    ['직급', r.kind === 'promote' ? CONFIG.rankNames[r.rankIdx - 1] + ' → ' + CONFIG.rankNames[r.rankIdx] : CONFIG.rankNames[r.rankIdx]],
    ['점수', String(r.score)],
    ['최고 콤보', r.maxCombo + '회 (점수 ×' + r.maxMult + ')'],
    ['전체 랭킹', '#-- (오프라인)'],
  ];
}
function drawResult() {
  // 컨셉 05-5: 어둡게 가라앉은 사무실(bg_result), 위 가운데 판정 판, 왼쪽 큰 캐릭터, 가운데 어두운 인사 평가 표
  const r = result, promo = r.kind === 'promote';
  if (!drawImg('bg_result', 0, 0, W, H, null)) { drawImg('bg_zone1', 0, 0, W, H, PAL.bg); rect(0, 0, W, H, PAL.bg, 0.75); }
  // 1 상자를 들고 걸어 나가는 큰 모습 (구조조정은 회색 실루엣, 승급은 제 색으로 뿌듯한 자세)
  const rk = CONFIG.rankNames[r.rankIdx];
  drawSprite('player', rk, promo ? 'proud' : 'box_exit', time, 62, 176,
             { scale: 4, fw: 16, fh: 28, color: RANK_COLORS[r.rankIdx], flip: !promo, filter: promo ? undefined : 'grayscale(1) brightness(0.85) contrast(0.85)' });
  // 3 위 가운데 판정 판. 구조조정은 도장처럼 크게 떨어져 쾅 찍힌다 (화면 흔들림, 잉크 튐)
  // 승급도 구조조정도 판정 판이 도장처럼 찍힌다
  const rt = time - (r.t0 || 0), k = clamp((rt - 0.15) / 0.22, 0, 1);
  if (k >= 1 && !r.stamped) { r.stamped = true; sfx('sfx_hit'); }
  const shake = rt > 0.37 && rt < 0.62 ? Math.sin(rt * 90) * (0.62 - rt) * 10 : 0;
  if (k > 0) {
    ctx.save(); ctx.translate(160 + shake, 11); const s = 1 + (1 - k) * (1 - k) * 1.8; ctx.scale(s, s); ctx.rotate((1 - k) * -0.12); ctx.translate(-160, -11);
    ctx.globalAlpha = k;
    rect(64, 0, 192, 22, promo ? PAL.green : PAL.red); frameRect(64, 0, 192, 22, PAL.text); frameRect(66, 2, 188, 18, promo ? '#4E5733' : '#6E2626');
    txt(promo ? '승급' : '구조조정 대상', 160, 11, { align: 'center', base: 'middle', size: 13, bold: true });
    ctx.restore(); ctx.globalAlpha = 1;
  }
  if (rt > 0.37 && rt < 1.1) {   // 잉크 튐 (승급은 올리브)
    const f = 1 - (rt - 0.37) / 0.73;
    [[58, 6], [262, 14], [70, 24], [250, 2], [150, 26], [184, 25]].forEach(function (d, i) { rect(d[0] + (i % 2 ? 1 : -1) * (1 - f) * 6, d[1], 2, 2, promo ? PAL.green : PAL.red, 0.8 * f); });
  }
  txt(promo ? CONFIG.rankNames[r.rankIdx - 1] + ' → ' + rk + ' · ' + r.floor + '층 도달 (PoC 종료)' : ((r.mode === 'endless' ? '무한 모드 · ' + r.floor + 'F 도달 (최고 ' + save.endless + 'F) · ' : '') + (r.cause === 'days' ? '남은 일수 소진' : r.cause === 'cracked' ? '금 간 계단이 무너짐' : r.cause === 'shock' ? '젖은 계단에서 감전' : '하트 소진')),
      160, 28, { align: 'center', base: 'middle', size: 6, color: '#9AA0A8' });
  // 4 인사 평가 결과 표 (칸마다 테두리, 값은 올리브 글자, 최고 콤보는 흰 테두리)
  rect(112, 34, 140, 100, '#18191C', 0.95); frameRect(112, 34, 140, 100, '#9AA0A8'); frameRect(114, 36, 136, 96, PAL.gray);
  rect(120, 41, 124, 12, '#141518'); frameRect(120, 41, 124, 12, '#9AA0A8');
  txt('인사 평가 결과', 182, 47.5, { align: 'center', base: 'middle', size: 7, bold: true });
  const rows = [['도달 층수', r.floor + 'F'], ['직급', promo ? CONFIG.rankNames[r.rankIdx - 1] + '→' + rk : rk], ['점수', String(r.score)],
                ['최고 콤보', r.maxCombo + '회 ×' + r.maxMult, true], ['전체 랭킹', '#--']];
  rows.forEach(function (row, i) {
    const y = 57 + i * 15;
    rect(120, y, 76, 12, '#141518'); frameRect(120, y, 76, 12, '#6E737A');
    txt(row[0], 124, y + 6.5, { base: 'middle', size: 6, color: '#C9CCD1', shadow: false });
    rect(200, y, 44, 12, '#141518'); frameRect(200, y, 44, 12, row[2] ? PAL.text : PAL.green);
    txt(row[1], 222, y + 6.5, { align: 'center', base: 'middle', size: 6, bold: true, color: row[2] ? PAL.text : '#A9B57C', shadow: false });
  });
  if (r.newBest) txtO('NEW', 262, 49, { size: 6, bold: true, color: '#FF6A5E', align: 'right' });
  // 구조조정 팁: 도장이 찍힌 뒤 떠오른다
  if (!promo) {
    const a = clamp((rt - 0.5) / 0.3, 0, 1);
    rect(40, 137, 240, 14, '#141518', 0.94 * a);   // rect는 알파를 받으면 끝에 1로 되돌리므로 먼저 그린다
    ctx.globalAlpha = a; frameRect(40, 137, 240, 14, '#E3B65C');
    txt('TIP', 50, 144, { align: 'center', base: 'middle', size: 6, bold: true, color: '#E3B65C' });
    txt(TIPS[r.tip] || TIPS.clipboard, 164, 144, { align: 'center', base: 'middle', size: 6, color: PAL.text });
    ctx.globalAlpha = 1;
  }
  // 2, 5 버튼
  drawButtons();
  if (time < toastUntil) txt(toastText, 197, 174, { size: 6, align: 'center', color: PAL.green });
}
function shareResult() {
  if (!result) return;
  const prev = ctx;
  const c = document.createElement('canvas');
  c.width = W * 2; c.height = H * 2;
  const g = c.getContext('2d');
  g.scale(2, 2); g.imageSmoothingEnabled = false;
  ctx = g;
  try {
    const promo = result.kind === 'promote';
    rect(0, 0, W, H, PAL.bg);
    txt('승급의 계단', 160, 12, { align: 'center', size: 16, bold: true });
    drawVerdict(60, 38, 200, 42, promo, promo ? CONFIG.rankNames[result.rankIdx - 1] + ' → ' + CONFIG.rankNames[result.rankIdx] + ' · ' + result.floor + '층 승급' : (result.cause === 'days' ? '남은 일수 소진' : '하트 소진'));
    rect(60, 92, 200, 72, PAL.bg); frameRect(60, 92, 200, 72, PAL.gray);
    txt('인사 평가표', 68, 95, { size: 8, bold: true, color: PAL.green });
    resultRows().forEach(function (row, i) {
      txt(row[0], 68, 108 + i * 10, { size: 8 });
      txt(row[1], 252, 108 + i * 10, { size: 8, align: 'right' });
    });
    txt('v0.1 PoC', 160, 168, { align: 'center', size: 7, color: PAL.gray });
  } finally { ctx = prev; }
  try {
    const url = c.toDataURL('image/png');
    const a = document.createElement('a');
    a.href = url; a.download = 'stairs_result.png';
    document.body.appendChild(a); a.click(); a.remove();
    toast('결과 이미지를 저장했습니다');
  } catch (e) { toast('이미지 저장에 실패했습니다'); }
}

/* ---------------------------------------------------------------------
 * 메인 루프
 * ------------------------------------------------------------------- */
function update(dt) {
  time += dt;
  if (resultLock > 0) resultLock -= dt;
  if (demo) demoUpdate(dt);
  else if (screen === 'play' && run && !settingsOpen) updatePlay(dt);
  syncAudio(false);
}
function draw() {
  ctx.setTransform(scale, 0, 0, scale, 0, 0);
  ctx.imageSmoothingEnabled = false;
  ctx.globalAlpha = 1;
  rect(0, 0, W, H, PAL.bg);
  switch (screen) {
    case 'title': drawTitle(); break;
    case 'lobby': drawLobby(); break;
    case 'play': if (run) drawPlay(); break;
    case 'pause': if (run) drawPause(); break;
    case 'result': if (result) drawResult(); break;
  }
  if (settingsOpen) {
    if (screen === 'pause' || screen === 'play') { /* 뒤 화면은 이미 그려짐 */ }
    drawSettings();
  }
  if (helpOpen) drawHelp();
  if (demo) drawDemo();
}
function publish() {
  S.ready = assetsReady;
  S.screen = screen;
  S.audioFormat = Snd.format;
  if (run) {
    S.floor = run.floor;
    S.rank = CONFIG.rankNames[run.rankIdx];
    S.hearts = run.hearts;
    S.days = Math.round(run.days * 100) / 100;
    S.score = run.score;
    S.maxMult = run.maxMult;
    S.steps = run.steps;
    const sh = shockState(run.time);
    S.hazard = { nextWet: isWet(run.steps + 1), onWet: isWet(run.steps), live: sh.live, shockIn: sh.shockIn };
  }
}
let last = performance.now(), acc = 0;
function frame(now) {
  let dt = (now - last) / 1000; last = now;
  if (dt > 0.1) dt = 0.1;
  acc += dt;
  while (acc >= STEP) { update(STEP); acc -= STEP; }
  draw();
  publish();
  requestAnimationFrame(frame);
}
publish();
requestAnimationFrame(frame);
})();