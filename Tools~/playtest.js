// 플레이테스트: 웹 game.js(봇)와 Unity Core의 JS 옮김(cs_core.js)을 같은 seed·모드로 1/60초씩 돌려 사건 기록을 맞대어 본다.
// 사용: node unity/StairsOfPromotion/Tools~/playtest.js demo_api/game.js [--context]
//   --context: 처음 어긋난 곳 앞뒤 기록을 보여 준다. CS_FLOAT=1 을 주면 고치기 전(float) Core로 돌린다
'use strict';
const fs = require('fs');
const { RunSimulation, defaultConfig } = require('./cs_core.js');
let src = fs.readFileSync(process.argv[2], 'utf8');
src = src.replace('if (logArr.length > 500) logArr.shift();', '');
src = src.replace("const BOT = params.get('bot') === '1';", "let BOT = params.get('bot') === '1';");   // 사람 흉내(봇 끔)도 돌린다   // 웹은 기록을 500줄만 남긴다: 맞대어 보려고 다 남긴다
const ctxArg = process.argv.indexOf('--context');
src = src.replace(/publish\(\);\s*requestAnimationFrame\(frame\);\s*\}\)\(\);?\s*$/,
  "globalThis.__api = { startRun, updatePlay, doStep, dirAt, setBot: v => { BOT = v; }, get run() { return run; }, logArr };\n})();");
const noop = () => {};
const ctxProxy = new Proxy({}, { get: (t, k) => (k === 'measureText' ? () => ({ width: 0 }) : noop), set: () => true });
Object.assign(globalThis, { window: globalThis, location: { search: '?seed=1&mute=1&bot=1' }, addEventListener: noop, innerWidth: 320, innerHeight: 180,
  requestAnimationFrame: noop, Image: function () { return {}; }, Audio: function () { return { canPlayType: () => '', play: () => Promise.resolve(), pause: noop }; } });
globalThis.document = { getElementById: () => ({ getContext: () => ctxProxy, style: {}, addEventListener: noop, getBoundingClientRect: () => ({}) }),
  addEventListener: noop, createElement: () => ({ canPlayType: () => '', style: {} }) };
eval(src);
const A = globalThis.__api;
const KEEP = /spawn|rival lock|parry ok|heart|floor \d|promote|gameover|shock hit|crack fall|finish|coffee|sticky bounce|wrong/;
const norm = s => s.replace(/^\S+ /, '');
function webRun(mode, maxTicks) {
  A.logArr.length = 0; A.startRun(mode); const r = A.run;
  for (let k = 0; k < maxTicks && !r.ended; k++) A.updatePlay(1 / 60);
  return { seed: r.seed, log: A.logArr.filter(s => KEEP.test(s)), end: r.ended && r.ended.cause, floor: r.floor, score: r.score, hearts: r.hearts, hits: r.hits, parries: r.parries, t: r.time };
}
function csRun(mode, seed, maxTicks) {
  const sim = new RunSimulation(defaultConfig(), seed, mode); sim.bot = true;
  for (let k = 0; k < maxTicks && !sim.endedB; k++) sim.tick(1 / 60);
  return { log: sim.log.filter(s => KEEP.test(s)), end: sim.ended && sim.ended.cause, floor: sim.floor, score: sim.score, hearts: sim.hearts, hits: sim.hits, parries: sim.parries, t: sim.time };
}
let bad = 0;
for (const [mode, n, ticks] of [['normal', 6, 60 * 300], ['endless', 3, 60 * 240], ['practice', 2, 60 * 90]]) {
  for (let i = 0; i < n; i++) {
    const w = webRun(mode, ticks), c = csRun(mode, w.seed, ticks);
    let div = -1; const L = Math.max(w.log.length, c.log.length);
    for (let j = 0; j < L; j++) if (norm(w.log[j] || '') !== norm(c.log[j] || '')) { div = j; break; }
    const same = div < 0 && w.end === c.end && w.floor === c.floor && w.score === c.score;
    if (!same) bad++;
    if (!same && ctxArg > 0 && div >= 0) {
      const a = Math.max(0, div - 4), b = div + 4;
      console.log('  web  : ' + w.log.slice(a, b).join(' | '));
      console.log('  unity: ' + c.log.slice(a, b).join(' | '));
    }
    console.log(`${mode.padEnd(8)} seed ${String(w.seed).padStart(2)} | web: ${w.end || '-'} ${w.floor}F score ${w.score} hearts ${w.hearts} parries ${w.parries} hits ${w.hits} t=${w.t.toFixed(1)}`
      + ` | unity: ${c.end || '-'} ${c.floor}F score ${c.score} hearts ${c.hearts} parries ${c.parries} hits ${c.hits} t=${c.t.toFixed(1)} | ${same ? 'SAME' : 'DIFF at #' + div + ' web "' + (w.log[div] || '(end)') + '" unity "' + (c.log[div] || '(end)') + '"'}`);
  }
}
// 지는 판: 봇 대신 정해진 손놀림. climb = 0.3초마다 오르기만 (막지 않음), idle = 아무것도 안 함, wrong = 0.5초마다 틀린 쪽, slow = 1.67초마다 (금 간 계단)
function policy(name, k, dirAt, steps) { if (name === 'climb' && k % 18 === 0) return dirAt(steps); if (name === 'wrong' && k % 30 === 0) return -dirAt(steps); if (name === 'slow' && k % 100 === 0) return dirAt(steps); return 0; }
A.setBot(false);
for (const pol of ['climb', 'idle', 'wrong', 'slow']) {
  for (const mode of ['normal', 'endless']) {
    A.logArr.length = 0; A.startRun(mode); const r = A.run;
    const sim = new RunSimulation(defaultConfig(), r.seed, mode);
    for (let k = 1; k < 60 * 120 && !(r.ended && sim.endedB); k++) {
      if (!r.ended) { const d = policy(pol, k, A.dirAt, r.steps); if (d) A.doStep(d); A.updatePlay(1 / 60); }
      if (!sim.endedB) { const d = policy(pol, k, i => sim.stairs.dirAt(i), sim.steps); if (d) sim.step(d); sim.tick(1 / 60); }
    }
    const wl = A.logArr.filter(x => KEEP.test(x)).map(norm), cl = sim.log.filter(x => KEEP.test(x)).map(norm);
    const same = JSON.stringify(wl) === JSON.stringify(cl) && r.score === sim.score;
    if (!same) bad++;
    console.log(`${pol.padEnd(5)} ${mode.padEnd(8)} seed ${String(r.seed).padStart(2)} | web: ${r.ended && r.ended.cause} ${r.floor}F score ${r.score} t=${r.time.toFixed(2)} | unity: ${sim.ended && sim.ended.cause} ${sim.floor}F score ${sim.score} t=${sim.time.toFixed(2)} tip=${sim.ended && sim.ended.tip} | ${same ? 'SAME' : 'DIFF'}`);
  }
}
console.log(bad ? bad + ' run(s) differ' : 'all runs match');
