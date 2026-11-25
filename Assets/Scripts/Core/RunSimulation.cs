// 한 판의 규칙. 웹 PoC의 startRun/doStep/doParry/updatePlay/spawnAttack/updateRival/rivalPos/botTick을 옮겼다.
// 화면·소리는 모른다. 일이 생기면 Sfx/Log 이벤트로 알리고, 보이는 쪽(Game/)은 상태를 읽어 그린다.
// 좌표는 웹의 월드 좌표(논리 px)다: x는 오른쪽이 +, y는 아래가 + (i번째 칸의 y = -i * stepH).
// Unity로 그릴 때는 y 부호를 뒤집는다 (GameController.W2U).
// 시간·위치 같은 상태는 모두 double이고 수치는 StairsConfig.D로 읽는다: 웹(JS double)과 프레임 단위까지 같게 하려고.
// float로 쌓으면 2분 동안 몇 프레임씩 어긋나 경쟁 사원이 잠기는 때, 점수가 달라졌다 (2026-11-25 node 플레이테스트, Tools~/playtest.js).
using System;
using System.Collections.Generic;

namespace Stairs.Core
{
    public enum ThreatType { Clipboard, Rival, Phone, Paper, Monitor, Coffee, Sticky, Suitcase }
    public enum RivalPhase { Walk, Stand, Climb }

    public sealed class Threat
    {
        public ThreatType type;
        public int side;          // 맞는 쪽 = 막는 키 (-1 Q 왼쪽, +1 E 오른쪽). 경쟁 사원은 치기 직전에 바뀔 수 있다
        public int from;          // 날아오는 쪽 (서류는 반대쪽에서 머리 위로)
        public double warnT, enterT, spawnT, impactT;
        public int hp = 1;        // 포스트잇: 막아야 하는 남은 횟수 (색)
        public bool done, cued, shattered, bounce;
        // 모니터의 불규칙한 회전 값과 서류가방에서 쏟아질 것 (웹과 같은 decor 난수 순서를 지키려고 Core에서 뽑는다)
        public double spin, wob, ph;
        public string debris;
        // ---- 경쟁 사원만 ----
        public bool locked;       // 막을 쪽이 정해졌다 (치기 rivalLeap초 전)
        public RivalPhase phase;
        public double wx;         // 걷기·서 있기: 층 바닥 위 가로 위치(월드 px)
        public double ri;         // 오르기: 계단 칸 번호(소수)
        public int floorStep;     // 걸어 들어오는 층 바닥의 칸 번호 (가장 가까운 층 바닥, 위 또는 아래)

        public bool IsRival => type == ThreatType.Rival;
        /// <summary>웹의 p.locked !== false: 경쟁 사원이 아니면 늘 true, 경쟁 사원은 막을 쪽이 정해진 뒤에만 true.</summary>
        public bool SideKnown => !IsRival || locked;
    }

    /// <summary>패링 불꽃 (웹의 sparks). 월드 좌표.</summary>
    public sealed class Spark { public double x, y, vx, vy, t; public bool olive; }
    /// <summary>치고 물러나는 경쟁 사원 (웹의 leavers). 월드 좌표.</summary>
    public sealed class Leaver { public int side; public double t0, wx, wy; }

    public sealed class RunResult
    {
        public bool promoted;
        public string cause;      // hearts, days, cracked, shock, floor100
        public string tip;        // 팁 키 (TIPS)
        public int floor, rankIdx, score, maxMult, maxCombo;
        public GameMode mode;
        public bool newBest;      // 저장 쪽(Game/SaveData)이 채운다
    }

    public sealed class RunSimulation
    {
        /// <summary>웹과 같은 한 프레임 (웹 STEP = 1/60). Game은 이 간격으로 Tick을 부른다.</summary>
        public const double Step60 = 1.0 / 60.0;

        public readonly StairsConfig C;
        public readonly StairLayout Stairs;
        public readonly GameMode Mode;
        readonly Mulberry32 rng, decor;

        public double time;
        public int steps, floor = 1, rankIdx, hearts;
        public double days;
        public int score, combo, mult = 1, maxMult = 1, maxCombo, parries, hits;
        public double crackT, nextAttack, lastImpact = -9, nextFlash;
        public int shockCycle = -1;
        public readonly double[] parryStart = { -9, -9 }, parryEnd = { -9, -9 }, parryCd = { 0, 0 };
        public readonly List<Threat> threats = new List<Threat>();
        public readonly List<Spark> sparks = new List<Spark>();
        public readonly List<Leaver> leavers = new List<Leaver>();
        public string lastKind;
        public RunResult ended;
        public double endTimer;
        public bool flashing; double flashLeft;
        public double flashX, flashY;              // 카메라 플래시가 터진 화면 자리 (논리 px)
        /// <summary>카메라 플래시의 남은 세기 1 → 0 (그리기용).</summary>
        public double FlashK => flashing ? Math.Max(0, flashLeft / D(C.flashTime)) : 0;
        // 웹의 pvx/pvy(플레이어가 보이는 자리, 칸으로 미끄러진다)와 camX/camY. 경쟁 사원이 들어오는 자리가 camX에 기대므로 Core에서 움직인다
        public double pvx, pvy, camX, camY;
        public double hitTimer, stepTimer, shake, hurtFlash;   // 연출용 타이머 (웹과 같은 값)
        public int facing;
        public int rivalIdx = 8;                  // 경쟁 사원 장식이 서 있는 칸
        /// <summary>켜면 Tick 안에서 웹과 같은 자리(시간을 더한 뒤)에 봇이 돈다 (웹의 ?bot=1).</summary>
        public bool Bot;

        /// <summary>소리 ID (sfx_step, sfx_electric 등)를 틀어 달라는 알림.</summary>
        public event Action<string> Sfx;
        /// <summary>플레이테스트·디버그용 기록 (웹의 log와 같은 문구).</summary>
        public event Action<string> Log;
        /// <summary>화면에 띄울 짧은 문구 (예: "승급! 사원 → 대리").</summary>
        public event Action<string> Toast;

        /// <summary>테스트용: 다음 공격의 종류를 정한다 (난수는 웹처럼 그대로 쓴다).</summary>
        public ThreatType? forceType;

        static double D(float v) => StairsConfig.D(v);

        public RunSimulation(StairsConfig config, int seed, GameMode mode)
        {
            C = config; Mode = mode;
            Stairs = new StairLayout(config, seed, mode);
            rng = new Mulberry32(seed);
            decor = new Mulberry32(unchecked((uint)seed ^ 0x5bd1e995u));
            hearts = C.maxHearts; days = D(C.startDays);
            nextAttack = D(C.firstAttackDelay); nextFlash = D(C.flashGap);
            facing = Stairs.DirAt(0);
            if (mode == GameMode.Practice)
            {
                steps = (C.practiceFloor - 1) * C.stairsPerFloor;
                floor = C.practiceFloor;
                facing = Stairs.DirAt(steps);
            }
            SnapCamera();
        }

        /// <summary>플레이어 자리와 카메라를 지금 칸으로 바로 옮긴다 (시작할 때, 테스트에서 칸을 바꿨을 때).</summary>
        public void SnapCamera()
        {
            pvx = camX = Stairs.StairX(steps);
            pvy = camY = -steps * D(C.stepH);
        }

        public bool Ended => ended != null;
        public string RankName => C.rankNames[rankIdx];
        public int NextDir => Stairs.DirAt(steps);
        public ShockState Shock => Stairs.Shock(time);

        double DiffT()
        {
            if (Mode == GameMode.Endless) return Clamp01((floor - 1) / (double)C.endlessRampFloors);
            if (Mode == GameMode.Practice) return D(C.practiceDiff);
            return Clamp01(((floor - 1) % 20) / 19.0);   // 승급 구간(20층)마다 다시 쉬워진다
        }
        static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
        static double Lerp(double a, double b, double t) => a + (b - a) * t;
        double DrainMult() => Lerp(D(C.drainStart), D(C.drainEnd), DiffT());
        double AttackInterval() => Lerp(D(C.attackIntervalStart), D(C.attackIntervalEnd), DiffT()) * (1 + (rng.Next() * 2 - 1) * D(C.attackJitter));

        // ------------------------------------------------------------ 입력
        public void Step(int dir)
        {
            if (Ended || Mode == GameMode.Practice) return;
            int need = Stairs.DirAt(steps);
            if (dir != need) { Log?.Invoke("wrong direction"); facing = need; lastKind = "wrong"; LoseHeart("wrong"); return; }
            steps++; crackT = 0;
            if (Stairs.IsCracked(steps)) { Sfx?.Invoke("sfx_stair_crack"); Log?.Invoke("crack step"); }
            days = Math.Min(D(C.maxDays), days + D(C.stepDays));
            score += C.stepPoints * mult;
            stepTimer = 0.14;
            facing = Stairs.DirAt(steps);
            Sfx?.Invoke("sfx_step");
            int nf = Stairs.FloorOfSteps(steps);
            if (nf > floor)
            {
                floor = nf;
                score += C.floorPoints * mult;
                Log?.Invoke("floor " + nf);
                if (hearts < C.maxHearts)
                {
                    hearts = Math.Min(C.maxHearts, hearts + C.floorHeal);
                    Toast?.Invoke(nf + "F · 하트 +1");
                    Log?.Invoke("heart +1 (floor) hearts=" + hearts);
                }
                else Toast?.Invoke(nf + "F");
                if (Array.IndexOf(C.promoteFloors, nf) >= 0) Promote(nf);
                if (Mode == GameMode.Normal && nf >= C.finalFloor && !Ended) { Sfx?.Invoke("sfx_promote"); Log?.Invoke("finish " + nf + "F"); End(true, "floor" + nf); }
            }
        }

        public void Parry(int side)
        {
            if (Ended) return;
            int s = side < 0 ? 0 : 1;
            if (time < parryCd[s]) return;
            parryStart[s] = time;
            parryEnd[s] = time + D(C.parryWindow);
            parryCd[s] = parryEnd[s] + D(C.parryCooldown);
        }

        // ------------------------------------------------------------ 규칙
        void LoseHeart(string reason)
        {
            combo = 0; mult = 1;
            hitTimer = 0.35; shake = 0.25; hurtFlash = 0.35;
            Sfx?.Invoke("sfx_hit");
            if (Mode == GameMode.Practice) { hits++; Log?.Invoke("hit (" + reason + ") practice"); return; }
            hearts--;
            Log?.Invoke("heart -1 (" + reason + ") hearts=" + hearts);
            if (hearts <= 0) { hearts = 0; Sfx?.Invoke("sfx_gameover"); End(false, "hearts"); }
        }

        void Promote(int f)
        {
            hearts = Math.Min(C.maxHearts, hearts + C.promoteHeal);
            rankIdx = Math.Min(C.rankNames.Length - 1, rankIdx + 1);
            Sfx?.Invoke("sfx_promote");
            Log?.Invoke("promote -> " + RankName + " (" + f + "F)");
            if (f >= C.finalFloor) { End(true, "floor" + f); return; }
            Toast?.Invoke("승급! " + C.rankNames[rankIdx - 1] + " → " + RankName);   // 장애물은 그대로 둔다 (2026-11-20)
        }

        void End(bool promoted, string cause)
        {
            ended = new RunResult
            {
                promoted = promoted, cause = cause, floor = floor, rankIdx = rankIdx, score = score,
                maxMult = maxMult, maxCombo = maxCombo, mode = Mode,
                tip = cause == "cracked" ? "cracked" : cause == "shock" ? "shock" : cause == "days" ? "days" : (lastKind ?? "clipboard"),
            };
            endTimer = D(C.endDelay);
            foreach (var t in threats) t.done = true;
            if (!promoted) Log?.Invoke("gameover " + cause);
        }

        ThreatType PickType()
        {
            int f = Mode == GameMode.Normal ? floor : 999;   // 무한·연습: 처음부터 모든 공격
            var w = new (ThreatType t, double w)[]
            {
                (ThreatType.Clipboard, 1.0),
                (ThreatType.Rival, f >= C.rivalFromFloor ? D(C.weightRival) : 0),
                (ThreatType.Phone, f >= C.phoneFromFloor ? D(C.weightPhone) : 0),
                (ThreatType.Paper, f >= C.paperFromFloor ? D(C.weightPaper) : 0),
                (ThreatType.Monitor, f >= C.monitorFromFloor ? D(C.weightMonitor) : 0),
                (ThreatType.Coffee, f >= C.coffeeFromFloor ? D(C.weightCoffee) : 0),
                (ThreatType.Sticky, f >= C.stickyFromFloor ? D(C.weightSticky) : 0),
                (ThreatType.Suitcase, f >= C.suitcaseFromFloor ? D(C.weightSuitcase) : 0),
            };
            double sum = 0; foreach (var e in w) sum += e.w;
            double x = rng.Next() * sum;
            foreach (var e in w) { if (x < e.w) return e.t; x -= e.w; }
            return ThreatType.Clipboard;
        }

        // JS Math.round (0.5는 올림). C# Math.Round는 기본이 짝수 맞춤이라 다르다
        static int JsRound(double v) => (int)Math.Floor(v + 0.5);

        void SpawnThreat()
        {
            int side = rng.Next() < 0.5 ? -1 : 1;
            var picked = PickType();
            var type = forceType ?? picked;
            bool rival = type == ThreatType.Rival;
            double warn = D(C.warnTime), walk = rival ? D(C.rivalWalk) : 0;
            double impact = time + warn + walk + (type == ThreatType.Monitor ? D(C.flickerTime) : 0);
            if (impact - lastImpact < D(C.minAttackGap)) impact = lastImpact + D(C.minAttackGap);
            lastImpact = impact;
            var t = new Threat
            {
                type = type, side = side, from = type == ThreatType.Paper ? -side : side,
                enterT = impact - warn - walk,
                warnT = time, spawnT = impact - warn, impactT = impact,
            };
            // 웹과 같은 순서: spin(decor 2번), wob, ph, hp(rng, 포스트잇만), debris
            t.spin = (decor.Next() < 0.5 ? -1 : 1) * (4 + decor.Next() * 6);
            t.wob = 1.5 + decor.Next() * 3;
            t.ph = decor.Next() * 6.28;
            t.hp = type == ThreatType.Sticky ? 1 + (int)Math.Floor(rng.Next() * C.stickyMax) : 1;
            t.debris = decor.Next() < 0.5 ? "document" : "clipboard";
            if (rival)
            {
                t.locked = false; t.phase = RivalPhase.Walk; t.ri = 0;
                t.wx = camX + side * (D(C.viewW) / 2 + 16);   // 화면 끝에서 들어온다
                t.floorStep = JsRound(steps / (double)C.stairsPerFloor) * C.stairsPerFloor;   // 가장 가까운 층 바닥(위 또는 아래)
            }
            threats.Add(t);
            Log?.Invoke(type.ToString().ToLowerInvariant() + " spawn");
            if (type == ThreatType.Monitor) Sfx?.Invoke("sfx_light_flicker");
            else if (type != ThreatType.Phone && type != ThreatType.Coffee) Sfx?.Invoke("sfx_warning");
        }

        // ------------------------------------------------------------ 경쟁 사원 (웹의 rivalPos/updateRival)
        /// <summary>경쟁 사원 발 위치(월드): 걷기·서 있기는 층 바닥 위(wx), 오르기는 계단 칸(ri, 소수).</summary>
        public (double x, double y) RivalPos(Threat p)
        {
            if (p.phase != RivalPhase.Climb) return (p.wx, -p.floorStep * D(C.stepH));
            int i = (int)Math.Floor(p.ri); double f = p.ri - i;
            return (Lerp(Stairs.StairX(i), Stairs.StairX(i + 1), f), -p.ri * D(C.stepH));
        }

        void UpdateRival(Threat p, double dt)
        {
            if (p.done) return;
            bool above = steps > p.floorStep;
            if (p.phase == RivalPhase.Walk || p.phase == RivalPhase.Stand)
            {
                // 플레이어가 바닥보다 위면 계단 아래 끝으로, 아니면 플레이어 옆으로 걷는다
                double tx = above ? Stairs.StairX(p.floorStep) : Stairs.StairX(steps) + p.side * 14, d = tx - p.wx;
                p.wx += Math.Sign(d) * Math.Min(Math.Abs(d), D(C.rivalSpeed) * dt);
                p.phase = Math.Abs(tx - p.wx) < 0.5 ? (above ? RivalPhase.Climb : RivalPhase.Stand) : RivalPhase.Walk;
                if (p.phase == RivalPhase.Climb) p.ri = p.floorStep;
            }
            else if (p.ri < steps - 1)
            {
                p.ri = Math.Min(steps - 1, p.ri + D(C.rivalClimb) * dt);   // 계단을 따라 쫓아 오른다
            }
            bool ready = p.phase == RivalPhase.Stand || (p.phase == RivalPhase.Climb && p.ri >= steps - 1 - 1e-6);
            if (p.locked) return;
            if (p.phase == RivalPhase.Climb && ready) p.side = -Stairs.DirAt(steps - 1);   // 바로 뒤 칸: 플레이어가 온 쪽
            if (time >= p.impactT - D(C.rivalLeap))
            {
                if (ready) { p.locked = true; Log?.Invoke("rival lock " + (p.phase == RivalPhase.Climb ? "stairs" : "floor")); }
                else p.impactT = time + D(C.rivalLeap);                  // 아직 따라잡지 못했다: 붙을 때까지 치지 않는다
            }
        }

        static readonly Dictionary<ThreatType, (string id, double lead)> Cues = new Dictionary<ThreatType, (string id, double lead)>
        {
            { ThreatType.Clipboard, ("sfx_cue_clipboard", 0.40) }, { ThreatType.Rival, ("sfx_cue_rival", 0.145) },
            { ThreatType.Phone, ("sfx_phone_ring", 0.50) }, { ThreatType.Paper, ("sfx_cue_paper", 0.35) },
            { ThreatType.Coffee, ("sfx_cue_coffee", 0.135) }, { ThreatType.Sticky, ("sfx_cue_clipboard", 0.40) },
            { ThreatType.Suitcase, ("sfx_cue_suitcase", 0.46) },
        };

        /// <summary>한 프레임 진행 (웹의 updatePlay). 웹과 같게 하려면 dt = Step60. 끝난 뒤 endDelay가 지나면 true를 돌려준다.</summary>
        public bool Tick(double dt)
        {
            time += dt;
            hitTimer = Math.Max(0, hitTimer - dt); stepTimer = Math.Max(0, stepTimer - dt);
            shake = Math.Max(0, shake - dt); hurtFlash = Math.Max(0, hurtFlash - dt);
            double tx = Stairs.StairX(steps), ty = -steps * D(C.stepH), k1 = Math.Min(1, dt * 22);
            pvx += (tx - pvx) * k1; pvy += (ty - pvy) * k1;
            camX += (pvx - camX) * Math.Min(1, dt * 8); camY = pvy;
            foreach (var s in sparks) { s.x += s.vx * dt; s.y += s.vy * dt; s.vy += 120 * dt; s.t += dt; }
            sparks.RemoveAll(s => s.t >= 0.4);
            leavers.RemoveAll(l => time - l.t0 >= 0.5);
            if (C.showRivalCameo && rivalIdx < steps - 3) rivalIdx = steps + 8 + (int)Math.Floor(decor.Next() * 6);

            if (Ended) { endTimer -= dt; return endTimer <= 0; }
            if (Bot) { BotTick(dt); if (Ended) return false; }

            if (Mode != GameMode.Practice) days -= dt / D(C.dayDuration) * DrainMult();

            var sh = Stairs.Shock(time);   // 젖은 계단: 전기가 흐르는 순간 그 칸에 있으면 감전
            if (sh.live && sh.cycle != shockCycle)
            {
                shockCycle = sh.cycle;
                bool near = false;
                for (int i = Math.Max(0, steps - 8); i < steps + 14 && !near; i++) near = Stairs.IsWet(i);
                if (near) { Sfx?.Invoke("sfx_electric"); Log?.Invoke("shock pulse"); }
            }
            if (sh.live && Stairs.IsWet(steps)) { Log?.Invoke("shock hit"); Sfx?.Invoke("sfx_electric"); Sfx?.Invoke("sfx_gameover"); End(false, "shock"); return false; }

            if (Stairs.IsCracked(steps))
            {
                crackT += dt;
                if (crackT >= D(C.crackTime)) { Log?.Invoke("crack fall"); Sfx?.Invoke("sfx_stair_crack"); Sfx?.Invoke("sfx_gameover"); End(false, "cracked"); return false; }
            }

            if (floor >= C.flashFromFloor || Mode != GameMode.Normal)   // 카메라 플래시: 막을 수 없지만 피해도 없다
            {
                nextFlash -= dt;
                if (nextFlash <= 0)
                {
                    flashing = true; flashLeft = D(C.flashTime);
                    flashX = 20 + decor.Next() * (D(C.viewW) - 40); flashY = 20 + decor.Next() * 60;
                    nextFlash = D(C.flashGap) * (0.6 + decor.Next() * 0.8);
                    Sfx?.Invoke("sfx_camera_flash"); Log?.Invoke("flash");
                }
            }
            if (flashing) { flashLeft -= dt; if (flashLeft <= 0) flashing = false; }   // 웹 camFlash.t: 터진 프레임부터 줄어든다
            if (days <= 0) { days = 0; Sfx?.Invoke("sfx_gameover"); End(false, "days"); return false; }

            if (time >= nextAttack) { SpawnThreat(); nextAttack = time + AttackInterval(); }

            for (int i = 0; i < threats.Count; i++)
            {
                var p = threats[i];
                if (p.IsRival) UpdateRival(p, dt);
                if (p.type == ThreatType.Monitor && !p.shattered && time >= p.spawnT) { p.shattered = true; Sfx?.Invoke("sfx_monitor_shatter"); }
                // 패링 타이밍 소리. 경쟁 사원은 막을 쪽이 정해진 뒤에만
                if (Cues.TryGetValue(p.type, out var cue) && !p.cued && !p.done && p.SideKnown && time >= p.impactT - cue.lead) { p.cued = true; Sfx?.Invoke(cue.id); Log?.Invoke("cue " + p.type.ToString().ToLowerInvariant()); }
                if (p.done || time < p.impactT) continue;
                p.done = true;
                if (p.IsRival) { var rp = RivalPos(p); leavers.Add(new Leaver { side = p.side, t0 = time, wx = rp.x, wy = rp.y }); }
                int s = p.side < 0 ? 0 : 1;
                bool parried = parryStart[s] <= p.impactT && p.impactT <= parryEnd[s];
                if (p.type == ThreatType.Coffee)   // 커피: 막으면 하트 -1, 그냥 두면 지나간다
                {
                    if (parried) { Log?.Invoke("coffee parried"); lastKind = "coffee"; LoseHeart("coffee"); if (Ended) break; }
                    else Log?.Invoke("coffee dodged");
                    continue;
                }
                if (parried)
                {
                    parries++; combo++;
                    mult = Math.Min(C.maxMult, 1 + combo / C.comboPerMult);   // 3연속마다 ×1, 최대 ×10
                    maxMult = Math.Max(maxMult, mult);
                    maxCombo = Math.Max(maxCombo, combo);
                    days = Math.Min(D(C.maxDays), days + D(C.parryDays));
                    score += C.parryPoints * mult;
                    Sfx?.Invoke("sfx_parry");
                    for (int k = 0; k < 8; k++)   // 패링 불꽃 (웹과 같이 decor 8번)
                    {
                        double a = decor.Next() * Math.PI * 2;
                        sparks.Add(new Spark { x = pvx + p.side * 10, y = pvy - 14, vx = Math.Cos(a) * 50, vy = Math.Sin(a) * 50 - 20, olive = k % 2 == 0 });
                    }
                    Log?.Invoke("parry ok combo=" + combo + " x" + mult);
                    if (p.type == ThreatType.Sticky && p.hp > 1)   // 포스트잇: 튕겨 나갔다가 같은 쪽으로 돌아온다
                    {
                        p.hp--; p.done = false; p.bounce = true; p.cued = false; p.spawnT = time; p.impactT = time + D(C.warnTime);
                        Log?.Invoke("sticky bounce hp=" + p.hp);
                    }
                }
                else
                {
                    lastKind = p.type.ToString().ToLowerInvariant();
                    LoseHeart("attack");
                    if (Ended) break;
                }
            }
            threats.RemoveAll(t => t.done);
            return false;
        }

        // ------------------------------------------------------------ 봇 (웹의 ?bot=1)
        double botT;
        /// <summary>봇 한 프레임. Bot을 켜면 Tick이 부르고, 직접 불러도 된다.</summary>
        public void BotTick(double dt)
        {
            if (Ended) return;
            botT += dt;
            double interval = D(C.botStepInterval);
            var sh = Shock;   // 다음 칸이 젖었고 전기가 흐르는 중이거나 곧 흐르면 건너지 않고 기다린다
            bool wetAhead = Stairs.IsWet(steps + 1) && (sh.live || sh.shockIn < 0.3);
            if (botT >= interval)
            {
                if (wetAhead) botT = interval;
                else { botT -= interval; Step(NextDir); }
            }
            if (Ended) return;
            foreach (var p in threats)
            {
                if (p.done || p.type == ThreatType.Coffee || !p.SideKnown) continue;
                double tti = p.impactT - time;
                int s = p.side < 0 ? 0 : 1;
                if (tti > 0 && tti <= D(C.botParryLead) && time >= parryCd[s]) Parry(p.side);
            }
        }
    }
}
