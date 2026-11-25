// 컷신 QA: game.js의 CUTSCENES 대본을 읽어 규칙으로 확인한다 (화면은 tools/cutscenes/cs_gallery.py로 눈으로 본다).
// - 쓰는 그림·시트·소리가 sprite_definition에 있는가, 배우 프레임 번호가 시트 안인가
// - 키·카메라·자막·효과 시각이 장면 길이 안이고 차례대로인가
// - 카메라가 배경 밖(검은 빈 곳)을 보지 않는가 (0.05초마다)
// - 배우가 등장하는 동안 화면 근처에 있는가, 자막이 화면 폭(300px) 안에 드는가
// - 소리 효과를 자막으로 쓰지 않았는가 (의성어 글자: 쾅·콰광·삐·딸깍·쿵·Zzz 등, 자막 종류 'big')
// - 길이: 인트로 ≤ 24초, 새 판 시작 ≤ 8초 (건너뛰기 가능), 엔딩 ≤ 22초
// 사용: node cutscene_qa.js <game.js> <sprite_definition.json>
'use strict';
const fs = require('fs');
let src = fs.readFileSync(process.argv[2], 'utf8');
const def = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
src = src.replace(/if \(params\.has\('cs'\)\) \{[\s\S]*?playCutscene\('intro', toTitle\);[^\n]*\n/, '');
src = src.replace(/publish\(\);\s*requestAnimationFrame\(frame\);\s*\}\)\(\);?\s*$/, "globalThis.__api = { CUTSCENES, csCam, csKeys, SPRITE_IDS, IMAGE_IDS, SOUND_IDS };\n})();");
const noop = () => {};
const ctxProxy = new Proxy({}, { get: (t, k) => (k === 'measureText' ? () => ({ width: 0 }) : noop), set: () => true });
Object.assign(globalThis, { window: globalThis, location: { search: '?mute=1&nointro=1' }, addEventListener: noop, innerWidth: 320, innerHeight: 180,
  requestAnimationFrame: noop, Image: function () { return {}; }, Audio: function () { return { canPlayType: () => '', play: () => Promise.resolve(), pause: noop }; } });
globalThis.document = { getElementById: () => ({ getContext: () => ctxProxy, style: {}, addEventListener: noop, getBoundingClientRect: () => ({}) }),
  addEventListener: noop, createElement: () => ({ canPlayType: () => '', style: {} }) };
eval(src);
const A = globalThis.__api;
const issues = []; const flag = (m) => issues.push(m);
const LIMIT = { intro: 24, runstart: 8, ending: 22 };
const ONOMATOPOEIA = /쾅|콰|광!|삐|딸깍|쿵|펑|휙|철컥|띠링|zzz|Zzz|zz/;
for (const [id, cs] of Object.entries(A.CUTSCENES)) {
  let total = 0;
  cs.shots.forEach((sh, si) => {
    const tag = `${id}#${si + 1}`; total += sh.d;
    if (sh.bg) {
      const img = def.images[sh.bg[0]];
      if (!img) flag(`${tag}: 배경 ${sh.bg[0]}이 정의에 없음`);
      else if (Math.abs(img.w / 2 - sh.bg[1]) > 0.5 || Math.abs(img.h / 2 - sh.bg[2]) > 0.5) flag(`${tag}: 배경 크기 ${sh.bg[1]}x${sh.bg[2]} ≠ 그림 ${img.w / 2}x${img.h / 2}`);
      if (!A.IMAGE_IDS.includes(sh.bg[0])) flag(`${tag}: 배경 ${sh.bg[0]}을 게임이 불러오지 않음 (IMAGE_IDS)`);
      if (sh.fg && !def.images[sh.fg]) flag(`${tag}: 앞 배경 ${sh.fg}이 정의에 없음`);
      for (let t = 0; t <= sh.d + 1e-9; t += 0.05) {   // 카메라가 배경 밖을 보지 않는다
        const c = A.csCam(sh.cam, t), hw = 160 / c.z, top = c.y + (14 - 90) / c.z, bot = c.y + (154 - 90) / c.z;   // 위아래 검은 띠(14px·26px) 사이만 보인다
        if (c.x - hw < -0.5 || c.x + hw > sh.bg[1] + 0.5 || top < -0.5 || bot > sh.bg[2] + 0.5) { flag(`${tag} t=${t.toFixed(2)}: 카메라가 배경 밖을 봄 (가운데 ${c.x.toFixed(0)},${c.y.toFixed(0)} 배율 ${c.z.toFixed(2)})`); break; }
      }
      sh.cam.forEach((k, i) => { if (k[0] > sh.d + 1e-6 || (i && k[0] < sh.cam[i - 1][0])) flag(`${tag}: 카메라 키 ${i} 시각 ${k[0]}이 이상함`); });
    } else if (!sh.card) flag(`${tag}: 배경도 제목 카드도 없음`);
    (sh.act || []).forEach((a, ai) => {
      const sp = def.sprites[a.s];
      if (!sp) { flag(`${tag}: 배우 시트 ${a.s}가 정의에 없음`); return; }
      if (!A.SPRITE_IDS.includes(a.s)) flag(`${tag}: 배우 시트 ${a.s}를 게임이 불러오지 않음 (SPRITE_IDS)`);
      const n = Object.keys(sp.animations).filter(k => /^f\d+$/.test(k)).length;
      a.k.forEach((k, i) => {
        const fs_ = Array.isArray(k[1]) ? k[1] : [k[1]];
        fs_.forEach(f => { if (f >= n) flag(`${tag} 배우 ${ai}(${a.s}): 프레임 ${f}가 시트(${n}칸) 밖`); });
        if (k[0] > sh.d + 1e-6) flag(`${tag} 배우 ${ai}: 키 시각 ${k[0]}이 장면 길이 ${sh.d}를 넘음`);
        if (i && k[0] < a.k[i - 1][0]) flag(`${tag} 배우 ${ai}: 키 시각이 거꾸로`);
        if (k[7] && !(k[7] in { l: 1, o: 1, i: 1, io: 1, b: 1 })) flag(`${tag} 배우 ${ai}: 모르는 이징 ${k[7]}`);
      });
      if (sh.bg) {   // 화면 밖에서 들어오는 것은 괜찮다. 한 번도 화면(띠 사이)에 보이지 않으면 문제
        let seen = false;
        for (let t = a.k[0][0]; t <= sh.d && !seen; t += 0.05) {
          const k = A.csKeys(a.k, t); if (!k || (!Array.isArray(k.f) && k.f < 0)) continue;
          const c = A.csCam(sh.cam, t), z = (a.z || 1) * c.z, sx = 160 + (k.x - c.x) * c.z, sy = 90 + (k.y - c.y) * c.z, w = sp.frame_w / 4 * z, h = sp.frame_h / 2 * z;
          if (sx + w > 0 && sx - w < 320 && sy > 14 && sy - h < 154) seen = true;
        }
        if (!seen) flag(`${tag} 배우 ${ai}(${a.s}): 한 번도 화면에 보이지 않음`);
      }
    });
    (sh.cap || []).forEach((c) => {
      if (c[1] > sh.d + 1e-6 || c[0] >= c[1]) flag(`${tag}: 자막 시각 ${c[0]}~${c[1]} 이상함`);
      if (c[3] === 'big' || ONOMATOPOEIA.test(c[2])) flag(`${tag}: 소리 효과를 자막으로 씀 "${c[2]}"`);
      const wpx = [...c[2]].reduce((s, ch) => s + (/[가-힣]/.test(ch) ? 8 : 4.6), 0);
      if (wpx > 300) flag(`${tag}: 자막이 화면 폭을 넘음 (${wpx.toFixed(0)}px) "${c[2]}"`);
      if ((c[1] - c[0]) < [...c[2]].length / 26 + 0.6) flag(`${tag}: 자막을 읽을 시간이 모자람 "${c[2]}"`);
    });
    (sh.fx || []).forEach((f) => {
      if (f[0] > sh.d + 1e-6) flag(`${tag}: 효과 시각 ${f[0]}이 장면 밖`);
      if (f[1] === 'sfx' && !(def.sounds[f[2]] && A.SOUND_IDS.includes(f[2]))) flag(`${tag}: 소리 ${f[2]}가 없거나 불러오지 않음`);
      if (!['sfx', 'shake', 'flash', 'lines', 'debris'].includes(f[1])) flag(`${tag}: 모르는 효과 ${f[1]}`);
    });
  });
  if (total > LIMIT[id]) flag(`${id}: 길이 ${total.toFixed(1)}초가 한도 ${LIMIT[id]}초를 넘음`);
  console.log(`${id}: 장면 ${cs.shots.length}개, ${total.toFixed(1)}초`);
}
console.log(`\n문제 ${issues.length}개`);
console.log(issues.length ? issues.join('\n') : '없음');
process.exit(issues.length ? 1 : 0);
