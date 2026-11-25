// 사용: node unity/StairsOfPromotion/Tools~/rival_expect.js demo_api/game.js
// ParityTests의 경쟁 사원 기대값(Rival_*)을 웹 game.js에서 다시 뽑는다. Unity는 ~로 끝나는 폴더를 가져오지 않는다.
// 웹 game.js를 node에서 돌려 경쟁 사원 잠금 쪽 기대값을 뽑는다.
const fs = require('fs');
let src = fs.readFileSync(process.argv[2], 'utf8');
// 경쟁 사원을 강제로 낸다. 새 웹 판은 r.forceType이 있고, 예전 판은 spawnAttack 한 줄을 바꿔 끼운다
src = src.replace("const type = pickType(r), rival = type === 'rival';",
  "const type = (function (t0) { return globalThis.__force || t0; })(pickType(r)), rival = type === 'rival';");
src = src.replace("const picked = pickType(r), type = r.forceType || picked, rival = type === 'rival';",
  "const picked = pickType(r), type = globalThis.__force || r.forceType || picked, rival = type === 'rival';");
src = src.replace(/publish\(\);\s*requestAnimationFrame\(frame\);\s*\}\)\(\);?\s*$/,
  "globalThis.__api = { startRun, updatePlay, doStep, doParry, dirAt, stairX, get run() { return run; }, logArr, setScreen: s => { screen = s; } };\n})();");
const noop = () => {};
const ctxProxy = new Proxy({}, { get: (t, k) => (k === 'measureText' ? () => ({ width: 0 }) : noop), set: () => true });
globalThis.window = globalThis; globalThis.location = { search: '?seed=1&mute=1' };
globalThis.document = { getElementById: () => ({ getContext: () => ctxProxy, style: {}, addEventListener: noop, getBoundingClientRect: () => ({}) }),
  addEventListener: noop, createElement: () => ({ canPlayType: () => '', style: {} }) };
globalThis.addEventListener = noop; globalThis.innerWidth = 320; globalThis.innerHeight = 180;
globalThis.Image = function () { return {}; }; globalThis.Audio = function () { return { canPlayType: () => '', play: () => Promise.resolve(), pause: noop }; };
globalThis.requestAnimationFrame = noop;
eval(src);
const A = globalThis.__api;
function scenario(name, startSteps, stepTicks, parrySide, parryTick) {
  A.startRun('normal');            // seed 1 (runCount 0 → seed 1, 1 → 2...)
  const r = A.run;
  r.steps = startSteps; r.floor = 1 + Math.floor(startSteps / 6); r.hearts = 3; r.days = 30;
  r.pvx = r.camX = A.stairX(startSteps); r.pvy = r.camY = -startSteps * 12;
  r.nextAttack = 0; globalThis.__force = 'rival';
  const out = { name, seed: r.seed };
  for (let k = 1; k <= 400; k++) {
    if (stepTicks.indexOf(k) >= 0) A.doStep(A.dirAt(r.steps));
    if (k === parryTick && r.projs[0]) A.doParry(r.projs[0].side);
    A.updatePlay(1 / 60);
    if (k === 1) { r.nextAttack = 999; globalThis.__force = null; const p = r.projs[0]; out.spawnSide = p.side; out.floorStep = p.floorStep; out.wx0 = p.wx; out.impact0 = p.impactT; }
    const p = r.projs[0];
    if (p && p.locked && out.lockTick == null) { out.lockTick = k; out.lockSide = p.side; out.phase = p.phase; out.ri = p.ri; out.impactT = p.impactT; out.stepsAtLock = r.steps; }
    if (!r.projs.length && out.lockTick != null) { out.doneTick = k; break; }
    if (r.projs[0] && r.projs[0].done) { out.doneTick = k; }
  }
  out.dirBehind = -A.dirAt(out.stepsAtLock - 1); out.hearts = r.hearts; out.combo = r.combo; out.steps = r.steps;
  out.log = A.logArr.filter(s => /rival|heart|parry/.test(s)).slice(-4);
  return out;
}
const res = [
  scenario('floor_stand', 30, [], 0, 0),
  scenario('climb_chase', 30, [20, 32, 44, 56], 0, 0),
  scenario('climb_parry', 30, [20, 32, 44, 56], 0, 92),
  scenario('late_catchup', 30, [70, 71, 72, 73, 74, 75, 76, 77, 78], 0, 0),
];
res.forEach(o => { delete o.log; console.log(JSON.stringify(o)); });
