// Unity Core(C#: Mulberry32, StairLayout, RunSimulation)를 한 줄씩 옮긴 JS. float는 Math.fround로 흉내 낸다.
// Unity 없이 Core 규칙을 플레이테스트하고 웹 game.js와 기록을 맞대어 보려고 만들었다 (playtest.js가 쓴다).
// C# 쪽을 고치면 이 파일도 같이 고친다.
'use strict';
// C# Core는 double로 쌓는다 (2026-11-25). CS_FLOAT=1 이면 그 전(float) 동작을 흉내 내어 어긋남을 다시 볼 수 있다
const FLOAT = !!process.env.CS_FLOAT;
const f = FLOAT ? Math.fround : (x => x);

function Mulberry32(seed) {
  let a = seed >>> 0;
  return function () {
    a = (a + 0x6D2B79F5) >>> 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a) >>> 0;
    t = ((t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t) >>> 0;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function defaultConfig() {
  return {
    maxHearts: 3, maxDays: f(30), startDays: f(30), dayDuration: f(0.33), stepDays: f(1), parryDays: f(1.5),
    parryWindow: f(0.2), warnTime: f(1.0), promoteFloors: [20, 40, 60, 80], finalFloor: 100, practiceFloor: 11,
    practiceDiff: f(0.5), endlessRampFloors: 60, promoteHeal: 1, floorHeal: 1,
    crackFromFloor: 21, crackShare: f(0.16), crackTime: f(1.5), wetFromFloor: 30, wetShare: f(0.14),
    shockPeriod: f(3), shockTime: f(0.5), shockWarn: f(0.6), flashFromFloor: 61, flashGap: f(3.5), flashTime: f(0.28),
    rivalFromFloor: 11, coffeeFromFloor: 11, monitorFromFloor: 21, phoneFromFloor: 31, paperFromFloor: 41, stickyFromFloor: 61, suitcaseFromFloor: 71,
    weightRival: f(0.7), weightCoffee: f(0.5), weightMonitor: f(0.6), weightPhone: f(0.6), weightPaper: f(0.6), weightSticky: f(0.6), weightSuitcase: f(0.6),
    stickyMax: 5, rivalWalk: f(0.6), flickerTime: f(0.6), rivalLeap: f(0.3), rivalSpeed: f(240), rivalClimb: f(10), showRivalCameo: true,
    stairsPerFloor: 6, comboPerMult: 3, maxMult: 10, stepPoints: 10, parryPoints: 50, floorPoints: 100,
    drainStart: f(1.0), drainEnd: f(1.35), attackIntervalStart: f(2.4), attackIntervalEnd: f(1.1), attackJitter: f(0.2),
    minAttackGap: f(0.8), firstAttackDelay: f(3.0), parryCooldown: f(0.15), stairRunMax: 4, stairKeepProb: f(0.5), endDelay: f(1.0),
    botStepInterval: f(0.19), botParryLead: f(0.10), stepW: f(22), stepH: f(12), viewW: f(320), viewH: f(180),
    rankNames: ['사원', '대리', '과장', '간부', 'CEO'],
  };
}

class StairLayout {
  constructor(c, seed, mode) { this.c = c; this.mode = mode; this.rng = Mulberry32((seed ^ 0x9e3779b1) >>> 0); this.dirs = []; this.xs = [0]; this.runLen = 0; }
  ensure(n) {
    const c = this.c;
    while (this.dirs.length < n) {
      let d;
      if (this.dirs.length === 0) { d = this.rng() < 0.5 ? -1 : 1; this.runLen = 1; }
      else {
        const last = this.dirs[this.dirs.length - 1];
        if (this.runLen >= c.stairRunMax || this.rng() >= c.stairKeepProb) { d = -last; this.runLen = 1; } else { d = last; this.runLen++; }
      }
      this.dirs.push(d); this.xs.push(f(this.xs[this.xs.length - 1] + d * c.stepW));
    }
  }
  dirAt(i) { this.ensure(i + 1); return this.dirs[i]; }
  stairX(i) { this.ensure(i + 1); return this.xs[i]; }
  floorOfSteps(n) { return 1 + Math.trunc(n / this.c.stairsPerFloor); }
  static hashBelow(i, k, share) { return Number((BigInt(i) * BigInt(k)) & 0xFFFFFFFFn) % 1000 < Math.round(share * 1000); }
  isCracked(i) {
    if (this.mode === 'practice') return false;
    const from = this.mode === 'endless' ? 2 : this.c.crackFromFloor;
    if (this.floorOfSteps(i) < from || i % this.c.stairsPerFloor === 0) return false;
    return StairLayout.hashBelow(i, 2654435761, this.c.crackShare);
  }
  isWet(i) {
    if (this.mode === 'practice') return false;
    const from = this.mode === 'endless' ? 3 : this.c.wetFromFloor;
    if (this.floorOfSteps(i) < from || i % this.c.stairsPerFloor === 0 || this.isCracked(i)) return false;
    return StairLayout.hashBelow(i, 2246822519, this.c.wetShare);
  }
  shock(t) {
    const c = this.c, P = c.shockPeriod, ph = f(t % P), start = f(P - c.shockTime);
    return { live: ph >= start, warn: ph >= f(start - c.shockWarn) && ph < start, shockIn: ph >= start ? 0 : f(start - ph), cycle: Math.floor(t / P) };
  }
}

const TYPES = ['clipboard', 'rival', 'phone', 'paper', 'monitor', 'coffee', 'sticky', 'suitcase'];
const CUES = { clipboard: ['sfx_cue_clipboard', f(0.40)], rival: ['sfx_cue_rival', f(0.145)], phone: ['sfx_phone_ring', f(0.50)], paper: ['sfx_cue_paper', f(0.35)],
  coffee: ['sfx_cue_coffee', f(0.135)], sticky: ['sfx_cue_clipboard', f(0.40)], suitcase: ['sfx_cue_suitcase', f(0.46)] };

class RunSimulation {
  // opts.doubleTime: 고친 뒤(시간을 double로 쌓음) 동작. opts.doubleWeights: 가중치를 소수 6자리 double로
  constructor(c, seed, mode, opts) {
    this.o = Object.assign({ doubleTime: !FLOAT, doubleWeights: !FLOAT }, opts || {});
    this.C = c; this.mode = mode; this.stairs = new StairLayout(c, seed, mode);
    this.rng = Mulberry32(seed >>> 0); this.decor = Mulberry32((seed ^ 0x5bd1e995) >>> 0);
    Object.assign(this, { time: 0, timeD: 0, steps: 0, floor: 1, rankIdx: 0, hearts: c.maxHearts, days: c.startDays, score: 0, combo: 0, mult: 1, maxMult: 1, maxCombo: 0,
      parries: 0, hits: 0, crackT: 0, nextAttack: c.firstAttackDelay, lastImpact: f(-9), nextFlash: c.flashGap, shockCycle: -1,
      parryStart: [f(-9), f(-9)], parryEnd: [f(-9), f(-9)], parryCd: [0, 0], threats: [], sparks: [], leavers: [], lastKind: null, ended: null, endTimer: 0,
      flashing: false, flashLeft: 0, hitTimer: 0, stepTimer: 0, shake: 0, hurtFlash: 0, rivalIdx: 8, bot: false, botT: 0, forceType: null, log: [] });
    this.facing = this.stairs.dirAt(0);
    if (mode === 'practice') { this.steps = (c.practiceFloor - 1) * c.stairsPerFloor; this.floor = c.practiceFloor; this.facing = this.stairs.dirAt(this.steps); }
    this.snapCamera();
  }
  L(s) { this.log.push(this.time.toFixed(2) + ' ' + s); }
  snapCamera() { this.pvx = this.camX = this.stairs.stairX(this.steps); this.pvy = this.camY = f(-this.steps * this.C.stepH); }
  get endedB() { return this.ended !== null; }
  diffT() {
    const c = this.C, cl = v => v < 0 ? 0 : v > 1 ? 1 : v;
    if (this.mode === 'endless') return f(cl(f((this.floor - 1) / c.endlessRampFloors)));
    if (this.mode === 'practice') return c.practiceDiff;
    return f(cl(f(((this.floor - 1) % 20) / f(19))));
  }
  lerp(a, b, t) { return f(a + f(f(b - a) * t)); }
  drainMult() { return this.lerp(this.C.drainStart, this.C.drainEnd, this.diffT()); }
  attackInterval() { const c = this.C; return f(this.lerp(c.attackIntervalStart, c.attackIntervalEnd, this.diffT()) * f(1 + f(f(f(this.rng()) * 2 - 1) * c.attackJitter))); }

  step(dir) {
    const c = this.C;
    if (this.endedB || this.mode === 'practice') return;
    const need = this.stairs.dirAt(this.steps);
    if (dir !== need) { this.L('wrong direction'); this.facing = need; this.lastKind = 'wrong'; this.loseHeart('wrong'); return; }
    this.steps++; this.crackT = 0;
    if (this.stairs.isCracked(this.steps)) this.L('crack step');
    this.days = Math.min(c.maxDays, f(this.days + c.stepDays));
    this.score += c.stepPoints * this.mult;
    this.stepTimer = f(0.14); this.facing = this.stairs.dirAt(this.steps);
    const nf = this.stairs.floorOfSteps(this.steps);
    if (nf > this.floor) {
      this.floor = nf; this.score += c.floorPoints * this.mult; this.L('floor ' + nf);
      if (this.hearts < c.maxHearts) { this.hearts = Math.min(c.maxHearts, this.hearts + c.floorHeal); this.L('heart +1 (floor) hearts=' + this.hearts); }   // C#도 같은 기록
      if (c.promoteFloors.indexOf(nf) >= 0) this.promote(nf);
      if (this.mode === 'normal' && nf >= c.finalFloor && !this.endedB) { this.L('finish ' + nf + 'F'); this.end(true, 'floor' + nf); }
    }
  }
  parry(side) {
    if (this.endedB) return;
    const s = side < 0 ? 0 : 1;
    if (this.time < this.parryCd[s]) return;
    this.parryStart[s] = this.time; this.parryEnd[s] = f(this.time + this.C.parryWindow); this.parryCd[s] = f(this.parryEnd[s] + this.C.parryCooldown);
  }
  loseHeart(reason) {
    this.combo = 0; this.mult = 1; this.hitTimer = f(0.35); this.shake = f(0.25); this.hurtFlash = f(0.35);
    if (this.mode === 'practice') { this.hits++; this.L('hit (' + reason + ') practice'); return; }
    this.hearts--; this.L('heart -1 (' + reason + ') hearts=' + this.hearts);
    if (this.hearts <= 0) { this.hearts = 0; this.end(false, 'hearts'); }
  }
  promote(fl) {
    const c = this.C;
    this.hearts = Math.min(c.maxHearts, this.hearts + c.promoteHeal);
    this.rankIdx = Math.min(c.rankNames.length - 1, this.rankIdx + 1);
    this.L('promote -> ' + c.rankNames[this.rankIdx] + ' (' + fl + 'F)');
    if (fl >= c.finalFloor) this.end(true, 'floor' + fl);
  }
  end(promoted, cause) {
    this.ended = { promoted, cause, floor: this.floor, rankIdx: this.rankIdx, score: this.score, maxMult: this.maxMult, maxCombo: this.maxCombo,
      tip: cause === 'cracked' ? 'cracked' : cause === 'shock' ? 'shock' : cause === 'days' ? 'days' : (this.lastKind || 'clipboard') };
    this.endTimer = this.C.endDelay;
    this.threats.forEach(t => { t.done = true; });
    if (!promoted) this.L('gameover ' + cause);
  }
  pickType() {
    const c = this.C, fl = this.mode === 'normal' ? this.floor : 999;
    const W = v => this.o.doubleWeights ? Math.round(v * 1e6) / 1e6 : v;
    const w = [['clipboard', 1], ['rival', fl >= c.rivalFromFloor ? c.weightRival : 0], ['phone', fl >= c.phoneFromFloor ? c.weightPhone : 0],
      ['paper', fl >= c.paperFromFloor ? c.weightPaper : 0], ['monitor', fl >= c.monitorFromFloor ? c.weightMonitor : 0],
      ['coffee', fl >= c.coffeeFromFloor ? c.weightCoffee : 0], ['sticky', fl >= c.stickyFromFloor ? c.weightSticky : 0],
      ['suitcase', fl >= c.suitcaseFromFloor ? c.weightSuitcase : 0]].map(e => [e[0], W(e[1])]);
    let sum = 0; w.forEach(e => { sum += e[1]; });
    let x = this.rng() * sum;
    for (const e of w) { if (x < e[1]) return e[0]; x -= e[1]; }
    return 'clipboard';
  }
  spawn() {
    const c = this.C, side = this.rng() < 0.5 ? -1 : 1, picked = this.pickType(), type = this.forceType || picked, rival = type === 'rival';
    let impact = f(f(f(this.time + c.warnTime) + (rival ? c.rivalWalk : 0)) + (type === 'monitor' ? c.flickerTime : 0));
    if (f(impact - this.lastImpact) < c.minAttackGap) impact = f(this.lastImpact + c.minAttackGap);
    this.lastImpact = impact;
    const t = { type, side, from: type === 'paper' ? -side : side, enterT: f(f(impact - c.warnTime) - (rival ? c.rivalWalk : 0)), warnT: this.time,
      spawnT: f(impact - c.warnTime), impactT: impact, hp: 1, done: false, cued: false, shattered: false, bounce: false, locked: false };
    t.spin = (this.decor() < 0.5 ? -1 : 1) * f(4 + f(this.decor()) * 6);
    t.wob = f(1.5 + f(this.decor()) * 3); t.ph = f(f(this.decor()) * f(6.28));
    t.hp = type === 'sticky' ? 1 + Math.floor(this.rng() * c.stickyMax) : 1;
    t.debris = this.decor() < 0.5 ? 'document' : 'clipboard';
    if (rival) { t.phase = 'walk'; t.ri = 0; t.wx = f(this.camX + side * f(c.viewW / 2 + 16)); t.floorStep = Math.floor(this.steps / c.stairsPerFloor + 0.5) * c.stairsPerFloor; }
    this.threats.push(t); this.L(type + ' spawn');
  }
  rivalPos(p) {
    if (p.phase !== 'climb') return [p.wx, f(-p.floorStep * this.C.stepH)];
    const i = Math.floor(p.ri), fr = f(p.ri - i);
    return [this.lerp(this.stairs.stairX(i), this.stairs.stairX(i + 1), fr), f(-p.ri * this.C.stepH)];
  }
  updateRival(p, dt) {
    const c = this.C;
    if (p.done) return;
    const above = this.steps > p.floorStep;
    if (p.phase === 'walk' || p.phase === 'stand') {
      const tx = above ? this.stairs.stairX(p.floorStep) : f(this.stairs.stairX(this.steps) + p.side * 14), d = f(tx - p.wx);
      p.wx = f(p.wx + Math.sign(d) * Math.min(Math.abs(d), f(c.rivalSpeed * dt)));
      p.phase = Math.abs(f(tx - p.wx)) < 0.5 ? (above ? 'climb' : 'stand') : 'walk';
      if (p.phase === 'climb') p.ri = p.floorStep;
    } else if (p.ri < this.steps - 1) p.ri = Math.min(this.steps - 1, f(p.ri + f(c.rivalClimb * dt)));
    const ready = p.phase === 'stand' || (p.phase === 'climb' && p.ri >= f(this.steps - 1 - f(1e-6)));
    if (p.locked) return;
    if (p.phase === 'climb' && ready) p.side = -this.stairs.dirAt(this.steps - 1);
    if (this.time >= f(p.impactT - c.rivalLeap)) {
      if (ready) { p.locked = true; this.L('rival lock ' + (p.phase === 'climb' ? 'stairs' : 'floor')); }
      else p.impactT = f(this.time + c.rivalLeap);
    }
  }
  tick(dt) {
    const c = this.C;
    if (this.o.doubleTime) { this.timeD += dt; this.time = f(this.timeD); } else this.time = f(this.time + dt);
    this.hitTimer = Math.max(0, f(this.hitTimer - dt)); this.stepTimer = Math.max(0, f(this.stepTimer - dt));
    this.shake = Math.max(0, f(this.shake - dt)); this.hurtFlash = Math.max(0, f(this.hurtFlash - dt));
    const tx = this.stairs.stairX(this.steps), ty = f(-this.steps * c.stepH), k1 = Math.min(1, f(dt * 22));
    this.pvx = f(this.pvx + f(f(tx - this.pvx) * k1)); this.pvy = f(this.pvy + f(f(ty - this.pvy) * k1));
    this.camX = f(this.camX + f(f(this.pvx - this.camX) * Math.min(1, f(dt * 8)))); this.camY = this.pvy;
    this.sparks.forEach(s => { s.t = f(s.t + dt); }); this.sparks = this.sparks.filter(s => s.t < f(0.4));
    this.leavers = this.leavers.filter(l => f(this.time - l.t0) < f(0.5));
    if (c.showRivalCameo && this.rivalIdx < this.steps - 3) this.rivalIdx = this.steps + 8 + Math.floor(this.decor() * 6);
    if (this.endedB) { this.endTimer = f(this.endTimer - dt); return this.endTimer <= 0; }
    if (this.bot) { this.botTick(dt); if (this.endedB) return false; }
    if (this.mode !== 'practice') this.days = f(this.days - f(f(dt / c.dayDuration) * this.drainMult()));
    const sh = this.stairs.shock(this.time);
    if (sh.live && sh.cycle !== this.shockCycle) {
      this.shockCycle = sh.cycle; let near = false;
      for (let i = Math.max(0, this.steps - 8); i < this.steps + 14 && !near; i++) near = this.stairs.isWet(i);
      if (near) this.L('shock pulse');
    }
    if (sh.live && this.stairs.isWet(this.steps)) { this.L('shock hit'); this.end(false, 'shock'); return false; }
    if (this.stairs.isCracked(this.steps)) { this.crackT = f(this.crackT + dt); if (this.crackT >= c.crackTime) { this.L('crack fall'); this.end(false, 'cracked'); return false; } }
    if (this.floor >= c.flashFromFloor || this.mode !== 'normal') {
      this.nextFlash = f(this.nextFlash - dt);
      if (this.nextFlash <= 0) {
        this.flashing = true; this.flashLeft = c.flashTime; this.decor(); this.decor();
        this.nextFlash = f(c.flashGap * f(0.6 + f(this.decor()) * 0.8)); this.L('flash');
      }
    }
    if (this.flashing) { this.flashLeft = f(this.flashLeft - dt); if (this.flashLeft <= 0) this.flashing = false; }
    if (this.days <= 0) { this.days = 0; this.end(false, 'days'); return false; }
    if (this.time >= this.nextAttack) { this.spawn(); this.nextAttack = f(this.time + this.attackInterval()); }
    for (let i = 0; i < this.threats.length; i++) {
      const p = this.threats[i];
      if (p.type === 'rival') this.updateRival(p, dt);
      if (p.type === 'monitor' && !p.shattered && this.time >= p.spawnT) p.shattered = true;
      const cue = CUES[p.type], known = p.type !== 'rival' || p.locked;
      if (cue && !p.cued && !p.done && known && this.time >= f(p.impactT - cue[1])) { p.cued = true; this.L('cue ' + p.type); }
      if (p.done || this.time < p.impactT) continue;
      p.done = true;
      if (p.type === 'rival') { const rp = this.rivalPos(p); this.leavers.push({ side: p.side, t0: this.time, wx: rp[0], wy: rp[1] }); }
      const s = p.side < 0 ? 0 : 1, parried = this.parryStart[s] <= p.impactT && p.impactT <= this.parryEnd[s];
      if (p.type === 'coffee') {
        if (parried) { this.L('coffee parried'); this.lastKind = 'coffee'; this.loseHeart('coffee'); if (this.endedB) break; } else this.L('coffee dodged');
        continue;
      }
      if (parried) {
        this.parries++; this.combo++;
        this.mult = Math.min(c.maxMult, 1 + Math.trunc(this.combo / c.comboPerMult));
        this.maxMult = Math.max(this.maxMult, this.mult); this.maxCombo = Math.max(this.maxCombo, this.combo);
        this.days = Math.min(c.maxDays, f(this.days + c.parryDays)); this.score += c.parryPoints * this.mult;
        for (let k = 0; k < 8; k++) { this.decor(); this.sparks.push({ t: 0 }); }
        this.L('parry ok combo=' + this.combo + ' x' + this.mult);
        if (p.type === 'sticky' && p.hp > 1) { p.hp--; p.done = false; p.bounce = true; p.cued = false; p.spawnT = this.time; p.impactT = f(this.time + c.warnTime); this.L('sticky bounce hp=' + p.hp); }
      } else { this.lastKind = p.type; this.loseHeart('attack'); if (this.endedB) break; }
    }
    this.threats = this.threats.filter(t => !t.done);
    return false;
  }
  botTick(dt) {
    const c = this.C;
    if (this.endedB) return;
    if (this.o.doubleTime) this.botT += dt; else this.botT = f(this.botT + dt);
    const sh = this.stairs.shock(this.time), wetAhead = this.stairs.isWet(this.steps + 1) && (sh.live || sh.shockIn < f(0.3));
    if (this.botT >= c.botStepInterval) { if (wetAhead) this.botT = c.botStepInterval; else { this.botT = this.o.doubleTime ? this.botT - c.botStepInterval : f(this.botT - c.botStepInterval); this.step(this.stairs.dirAt(this.steps)); } }
    if (this.endedB) return;
    for (const p of this.threats) {
      if (p.done || p.type === 'coffee' || !(p.type !== 'rival' || p.locked)) continue;
      const tti = f(p.impactT - this.time), s = p.side < 0 ? 0 : 1;
      if (tti > 0 && tti <= c.botParryLead && this.time >= this.parryCd[s]) this.parry(p.side);
    }
  }
}

module.exports = { Mulberry32, StairLayout, RunSimulation, defaultConfig };
