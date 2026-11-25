// 플레이어 움직임·상호작용 QA: 웹 game.js를 node에서 돌리며(봇·사람 흉내) 프레임마다 그리기 자리를 기록하고, 물리적으로 말이 되는지 본다.
// - 맞음·막음이 일어날 때 그 물체가 플레이어에 닿아 있는가 (투척물·경쟁 사원·의자·편지·종이비행기)
// - 물체가 플레이어 몸을 사건 없이 지나가지 않는가 (특히 의자)
// - 의자가 실제 계단 칸 위에만 내려앉는가 (허공에서 튀지 않는가)
// - 순간이동(한 프레임에 너무 많이 움직임), 떨어지는 것이 위로 가지 않는가
// 자리는 모두 world 좌표로 바꿔 비교한다 (카메라 움직임과 무관). 사용: node qa_physics.js <game.js>
'use strict';
const fs = require('fs');
let src = fs.readFileSync(process.argv[2], 'utf8');
src = src.replace('if (logArr.length > 500) logArr.shift();', '');
src = src.replace("const BOT = params.get('bot') === '1';", "let BOT = params.get('bot') === '1';");
src = src.replace(/publish\(\);\s*requestAnimationFrame\(frame\);\s*\}\)\(\);?\s*$/,
  "globalThis.__api = { startRun, updatePlay, doStep, needDir, drawPlayWorld, stairX, CONFIG, setBot: v => { BOT = v; }, setHook: f => { QA_HOOK = f; }, get run() { return run; }, logArr, PLAYER_Y };\n})();");
const noop = () => {};
const ctxProxy = new Proxy({}, { get: (t, k) => (k === 'measureText' ? () => ({ width: 0 }) : noop), set: () => true });
Object.assign(globalThis, { window: globalThis, location: { search: '?seed=1&mute=1' }, addEventListener: noop, innerWidth: 320, innerHeight: 180,
  requestAnimationFrame: noop, Image: function () { return {}; }, Audio: function () { return { canPlayType: () => '', play: () => Promise.resolve(), pause: noop }; } });
globalThis.document = { getElementById: () => ({ getContext: () => ctxProxy, style: {}, addEventListener: noop, getBoundingClientRect: () => ({}) }),
  addEventListener: noop, createElement: () => ({ canPlayType: () => '', style: {} }) };
eval(src);
const A = globalThis.__api, PY = A.PLAYER_Y;
const RUNS = [['normal', 'bot', 60 * 300], ['normal', 'bot', 60 * 300], ['endless', 'bot', 60 * 200], ['normal', 'climb', 60 * 120], ['normal', 'still', 60 * 60], ['endless', 'still', 60 * 60]];
const issues = {}, counts = {};
function flag(kind, msg) { (issues[kind] = issues[kind] || []).push(msg); }
function cnt(k) { counts[k] = (counts[k] || 0) + 1; }
for (const [mode, pol, ticks] of RUNS) {
  A.setBot(pol === 'bot'); A.logArr.length = 0; A.startRun(mode); const r = A.run;
  let prev = new Map(), player = null, prevPlayer = null;
  for (let k = 1; k <= ticks && !r.ended; k++) {
    if (pol === 'climb' && k % 18 === 0) A.doStep(A.needDir(r.steps));
    const before = new Set(r.projs);
    const logBefore = A.logArr.length;
    A.updatePlay(1 / 60);
    const ev = A.logArr.slice(logBefore).map(s => s.replace(/^\S+ /, ''));
    const cur = new Map(); player = null;
    A.setHook(function (kind, p, x, y, extra) {
      const sy0 = y; x = x - 160 + r.camX; y = y - PY + r.camY;   // 화면 → world (카메라가 움직여도 비교가 흔들리지 않게). 모니터는 화면을 따라 떨어지므로 화면 y로 본다
      if (kind === 'player') { player = { x, y }; return; }
      const key = kind === 'envelope' || kind === 'plane' ? p : p;   // 편대·편지는 장마다 따로
      const id = (kind === 'envelope' || kind === 'plane') ? p : p;
      let m = cur.get(p); if (!m) { cur.set(p, m = { kind, items: {} }); const pm = prev.get(p); if (pm) m.prevItem = pm.items[0]; }
      m.items[extra === undefined || typeof extra === 'boolean' ? 0 : extra] = { x, y, sy: sy0, ground: extra === true };
    });
    A.drawPlayWorld(); A.setHook(null);
    if (!player) continue;
    const tag = `${mode}/${pol}/${r.seed} t=${r.time.toFixed(2)} ${r.floor}F`;
    // 플레이어 순간이동
    if (prevPlayer && Math.hypot(player.x - prevPlayer.x, player.y - prevPlayer.y) > 30) flag('player teleport', `${tag} moved ${Math.hypot(player.x - prevPlayer.x, player.y - prevPlayer.y).toFixed(1)}px`);
    const body = { x0: player.x - 7, x1: player.x + 7, y0: player.y - 26, y1: player.y };   // 플레이어 몸 (발 = y)
    const hitEv = ev.some(s => /^heart -1 \(attack\)|^hit \(attack\)/.test(s)), parryEv = ev.some(s => /^parry ok/.test(s));
    // 사라진 것: 맞거나 막힌 순간의 마지막 자리가 플레이어에 닿아 있는가
    for (const [p, m] of prev) {
      if (cur.has(p) || !before.has(p)) continue;
      if (r.projs.includes(p)) continue;
      const it = m.items[Math.min(...Object.keys(m.items).map(Number))];   // 편지·종이비행기는 도착한(가장 앞선) 장
      const resolved = (hitEv || parryEv);
      if (!resolved || r.ended) continue;                                   // 판이 끝나 모두 치워진 프레임은 빼고
      if (m.kind === 'proj' && (p.type === 'suitcase')) continue;           // 서류가방은 터져서 서류가 날아간다
      if (m.kind === 'chair' && p.fallT !== null) continue;
      const tx = player.x + p.side * 8, ty = player.y - 18;
      let d = Math.hypot(it.x - tx, it.y - ty), lim = m.kind === 'rival' ? 26 : 16;
      if (m.kind === 'chair') {   // 규칙과 같은 상자 겹침. 사라진 프레임에 닿았으니 마지막 자리를 한 프레임 앞으로 (속도만큼) 옮겨 본다
        const fy = player.y - 2, pv = m.prevItem || it, ex = it.x + (it.x - pv.x), ey = it.y + (it.y - pv.y);
        d = Math.abs(ex - player.x) < 15 && ey + 8 > fy - 28 && ey - 8 < fy + 2 ? 0 : 99; lim = 1;
      }   // 의자: 규칙과 같은 상자 겹침 (프레임 사이 이동만큼 여유)
      cnt('resolved ' + m.kind);
      if (d > lim) flag('resolved away from player', `${tag} ${m.kind}/${p.type} ${d.toFixed(1)}px from the player when ${hitEv ? 'hit' : 'parried'}` + (m.kind === 'chair' ? ` (chair ${it.x.toFixed(1)},${it.y.toFixed(1)} prev ${(m.prevItem || it).x.toFixed(1)},${(m.prevItem || it).y.toFixed(1)} player ${player.x.toFixed(1)},${player.y.toFixed(1)})` : ''));
    }
    for (const [p, m] of cur) {
      // 순간이동
      const pm = prev.get(p);
      for (const key of Object.keys(m.items)) {
        const it = m.items[key], pi = pm && pm.items[key];
        if (pi) {
          const dd = Math.hypot(it.x - pi.x, it.y - pi.y);
          const lim = m.kind === 'rival' ? 30 : 40;
          if (dd > lim && !(p.type === 'stapler' && p.returned && r.time - p.spawnT < 0.05) && !(p.bounce && r.time - p.spawnT < 0.05)) flag('object teleport', `${tag} ${m.kind}/${p.type} jumped ${dd.toFixed(1)}px`);
          if (m.kind === 'proj' && p.type === 'monitor' && it.sy < pi.sy - 0.5) flag('falling object goes up', `${tag} monitor`);
        }
        // 의자: 내려앉는 순간은 실제 계단 칸 위
        if (m.kind === 'chair') {
          cnt('chair frames');
          const fy = player.y - 2, inBody = Math.abs(it.x - player.x) < 12 && it.y + 8 > fy - 25 && it.y - 8 < fy - 1;   // 규칙과 같은 상자 (1px 안쪽)
          if (inBody && !hitEv && !parryEv && p.fallT === null) flag('chair passes through player', `${tag} chair overlaps the player body without a hit/parry (k=${p.k})`);
          if (it.ground && p.fallT === null && r.time >= p.spawnT) {
            cnt('chair landings');
            const j = 6 - 2 * Math.round((r.time - p.spawnT) / A.CONFIG.chairHop);
            if (j > p.up || j < -p.down) flag('chair lands in the air', `${tag} chair touches down at j=${j} (line has up=${p.up} down=${p.down})`);
          }
        }
      }
    }
    prev = cur; prevPlayer = player;
  }
}
console.log('checked', Object.entries(counts).map(([k, v]) => `${k} ${v}`).join(', '));
const kinds = Object.keys(issues);
console.log(`\nproblems: ${kinds.reduce((a, k) => a + issues[k].length, 0)}`);
for (const k of kinds) { console.log(`- ${k}: ${issues[k].length}`); issues[k].slice(0, 4).forEach(m => console.log('    ' + m)); }
