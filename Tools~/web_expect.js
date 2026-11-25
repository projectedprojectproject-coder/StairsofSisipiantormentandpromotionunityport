// 웹 판(game.js)을 node에서 돌려 사건 기록을 뽑아 Assets/Tests/EditMode/WebRuns.txt로 쓴다.
// Unity EditMode 테스트 WebRuns_MatchFrameForFrame이 같은 입력을 C# Core에 넣어 한 줄씩 맞대어 본다.
// 사용: node unity/StairsOfPromotion/Tools~/web_expect.js unity/StairsOfPromotion/WebReference/game.js unity/StairsOfPromotion/Assets/Tests/EditMode/WebRuns.txt
'use strict';
const fs = require('fs');
let src = fs.readFileSync(process.argv[2], 'utf8');
src = src.replace('if (logArr.length > 500) logArr.shift();', '');                       // 기록을 다 남긴다
src = src.replace("const BOT = params.get('bot') === '1';", "let BOT = params.get('bot') === '1';");
src = src.replace(/publish\(\);\s*requestAnimationFrame\(frame\);\s*\}\)\(\);?\s*$/,
  "globalThis.__api = { startRun, updatePlay, doStep, dirAt: needDir, setBot: v => { BOT = v; }, scene: (seed, at, ph) => { run = makeRun(seed, 'normal'); run.steps = at; run.floor = floorOfSteps(at); run.facing = dirAt(at); run.pvx = run.camX = stairX(at); run.pvy = run.camY = -at * CONFIG.stepH; run.nextAttack = 0.6; run.forceType = 'rival'; run.vanishPhase = ph; }, get run() { return run; }, logArr };\n})();");
const noop = () => {};
const ctxProxy = new Proxy({}, { get: (t, k) => (k === 'measureText' ? () => ({ width: 0 }) : noop), set: () => true });
Object.assign(globalThis, { window: globalThis, location: { search: '?seed=1&mute=1' }, addEventListener: noop, innerWidth: 320, innerHeight: 180,
  requestAnimationFrame: noop, Image: function () { return {}; }, Audio: function () { return { canPlayType: () => '', play: () => Promise.resolve(), pause: noop }; } });
globalThis.document = { getElementById: () => ({ getContext: () => ctxProxy, style: {}, addEventListener: noop, getBoundingClientRect: () => ({}) }),
  addEventListener: noop, createElement: () => ({ canPlayType: () => '', style: {} }) };
eval(src);
const A = globalThis.__api;
// 맞대어 볼 사건 (시각 앞머리는 뺀다). C# RunSimulation.Log와 같은 문구여야 한다
const KEEP = /spawn$|rival lock|parry ok|heart|^floor \d|promote|gameover|shock hit|crack fall|finish|coffee|sticky bounce|wrong direction|stapler return|airplane miss|escalator push|vanish fall|gaze (caught|start|warn|sweep)|rival (wait|fall|shock|pushed|gaze)|chair off|envelope miss|hit \(/;
// 사람 흉내: climb = 0.3초마다 오르기만, idle = 아무것도 안 함, wrong = 0.5초마다 틀린 쪽, slow = 1.67초마다
function policy(name, k, steps) {
  if (name === 'climb' && k % 18 === 0) return A.dirAt(steps);
  if (name === 'wrong' && k % 30 === 0) return -A.dirAt(steps);
  if (name === 'slow' && k % 100 === 0) return A.dirAt(steps);
  return 0;
}
const RUNS = [
  ['normal', 'bot', 60 * 300], ['normal', 'bot', 60 * 300], ['normal', 'bot', 60 * 300], ['endless', 'bot', 60 * 240], ['practice', 'bot', 60 * 60],
  ['normal', 'climb', 60 * 120], ['endless', 'climb', 60 * 120], ['normal', 'idle', 60 * 60], ['normal', 'wrong', 60 * 30], ['endless', 'slow', 60 * 60],
];
// 경쟁 사원 × 계단 장애물 장면 (장애물 보기와 같은 판: seed 7, 그 칸에 서서 경쟁 사원 1명, 아무것도 누르지 않음)
// climb > 0: 그 프레임마다 맞는 쪽으로 오른다 (61층에서 상사의 눈 레이저에 들키는지)
const SCENES = [[7, 356, 3.6, 60 * 5, 0], [7, 212, 0, 60 * 4, 0], [7, 326, 0, 60 * 4, 0], [7, 366, 0, 60 * 30, 20]];
const out = ['# 웹 판 사건 기록 — Tools~/web_expect.js가 만든다. 고치지 말고 다시 만든다', '# run <모드> <seed> <손놀림> <프레임 수> / 사건 줄들 / end <끝난 까닭|-> <층> <점수> <하트> <패링> <칸>'];
for (const [mode, pol, ticks] of RUNS) {
  A.setBot(pol === 'bot'); A.logArr.length = 0; A.startRun(mode); const r = A.run;
  for (let k = 1; k <= ticks && !r.ended; k++) { const d = policy(pol, k, r.steps); if (d) A.doStep(d); A.updatePlay(1 / 60); }
  out.push(['run', mode, r.seed, pol, ticks].join(' '));
  A.logArr.map(s => s.replace(/^\S+ /, '')).filter(s => KEEP.test(s)).forEach(s => out.push(s));
  out.push(['end', r.ended ? r.ended.cause : '-', r.floor, r.score, r.hearts, r.parries, r.steps].join(' '));
}
for (const [seed, at, ph, ticks, climb] of SCENES) {
  A.setBot(false); A.logArr.length = 0; A.scene(seed, at, ph); const r = A.run;
  for (let k = 1; k <= ticks && !r.ended; k++) { if (climb && k % climb === 0) A.doStep(A.dirAt(r.steps)); A.updatePlay(1 / 60); if (r.forceType && r.projs.length) { r.forceType = null; r.nextAttack = 1e9; } }
  out.push(['run', 'scene', seed, at + ':' + ph + ':' + climb, ticks].join(' '));
  A.logArr.map(s => s.replace(/^\S+ /, '')).filter(s => KEEP.test(s)).forEach(s => out.push(s));
  out.push(['end', r.ended ? r.ended.cause : '-', r.floor, r.score, r.hearts, r.parries, r.steps].join(' '));
}
fs.writeFileSync(process.argv[3], out.join('\n') + '\n', 'utf8');
console.log('wrote', process.argv[3], out.length, 'lines');
out.filter(s => /^(run|end)/.test(s)).forEach(s => console.log(' ', s));
