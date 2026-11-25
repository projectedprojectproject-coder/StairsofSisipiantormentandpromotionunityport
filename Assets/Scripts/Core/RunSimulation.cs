// 한 판의 규칙. 웹 PoC의 startRun/doStep/doParry/updatePlay/spawnAttack을 옮겼다.
// 화면·소리는 모른다. 일이 생기면 Sfx/Log 이벤트로 알리고, 보이는 쪽(Game/)은 상태를 읽어 그린다.
// 경쟁 사원은 웹처럼 층 바닥을 걸어와 계단을 쫓아 오르는 대신, 같은 시각에 같은 쪽을 치는 단순한 형태다 (TODO: 쫓아오기).
using System;
using System.Collections.Generic;

namespace Stairs.Core
{
    public enum ThreatType { Clipboard, Rival, Phone, Paper, Monitor, Coffee, Sticky, Suitcase }

    public sealed class Threat
    {
        public ThreatType type;
        public int side;          // 맞는 쪽 = 막는 키 (-1 Q 왼쪽, +1 E 오른쪽)
        public int from;          // 날아오는 쪽 (서류는 반대쪽에서 머리 위로)
        public float warnT, spawnT, impactT;
        public int hp = 1;        // 포스트잇: 막아야 하는 남은 횟수 (색)
        public bool done, cued, shattered;
    }

    public sealed class RunResult
    {
        public bool promoted;
        public string cause;      // hearts, days, cracked, shock, floor100
        public string tip;        // 팁 키 (TIPS)
        public int floor, rankIdx, score, maxMult, maxCombo;
        public GameMode mode;
    }

    public sealed class RunSimulation
    {
        public readonly StairsConfig C;
        public readonly StairLayout Stairs;
        public readonly GameMode Mode;
        readonly Mulberry32 rng, decor;

        public float time;
        public int steps, floor = 1, rankIdx, hearts;
        public float days;
        public int score, combo, mult = 1, maxMult = 1, maxCombo, parries, hits;
        public float crackT, nextAttack, lastImpact = -9f, nextFlash;
        public int shockCycle = -1;
        public readonly float[] parryStart = { -9f, -9f }, parryEnd = { -9f, -9f }, parryCd = { 0f, 0f };
        public readonly List<Threat> threats = new List<Threat>();
        public string lastKind;
        public RunResult ended;
        public float endTimer;
        public bool flashing; float flashLeft;

        /// <summary>소리 ID (sfx_step, sfx_electric 등)를 틀어 달라는 알림.</summary>
        public event Action<string> Sfx;
        /// <summary>플레이테스트·디버그용 기록 (웹의 log와 같은 문구).</summary>
        public event Action<string> Log;
        /// <summary>화면에 띄울 짧은 문구 (예: "승급! 사원 → 대리").</summary>
        public event Action<string> Toast;

        public RunSimulation(StairsConfig config, int seed, GameMode mode)
        {
            C = config; Mode = mode;
            Stairs = new StairLayout(config, seed, mode);
            rng = new Mulberry32(seed);
            decor = new Mulberry32(unchecked((uint)seed ^ 0x5bd1e995u));
            hearts = C.maxHearts; days = C.startDays;
            nextAttack = C.firstAttackDelay; nextFlash = C.flashGap;
            if (mode == GameMode.Practice)
            {
                steps = (C.practiceFloor - 1) * C.stairsPerFloor;
                floor = C.practiceFloor;
            }
        }

        public bool Ended => ended != null;
        public string RankName => C.rankNames[rankIdx];
        public int NextDir => Stairs.DirAt(steps);
        public ShockState Shock => Stairs.Shock(time);

        float DiffT()
        {
            if (Mode == GameMode.Endless) return Clamp01((floor - 1) / (float)C.endlessRampFloors);
            if (Mode == GameMode.Practice) return C.practiceDiff;
            return Clamp01(((floor - 1) % 20) / 19f);   // 승급 구간(20층)마다 다시 쉬워진다
        }
        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        float DrainMult() => Lerp(C.drainStart, C.drainEnd, DiffT());
        float AttackInterval() => Lerp(C.attackIntervalStart, C.attackIntervalEnd, DiffT()) * (1f + ((float)rng.Next() * 2f - 1f) * C.attackJitter);

        // ------------------------------------------------------------ 입력
        public void Step(int dir)
        {
            if (Ended || Mode == GameMode.Practice) return;
            int need = Stairs.DirAt(steps);
            if (dir != need) { Log?.Invoke("wrong direction"); lastKind = "wrong"; LoseHeart("wrong"); return; }
            steps++; crackT = 0f;
            if (Stairs.IsCracked(steps)) { Sfx?.Invoke("sfx_stair_crack"); Log?.Invoke("crack step"); }
            days = Math.Min(C.maxDays, days + C.stepDays);
            score += C.stepPoints * mult;
            Sfx?.Invoke("sfx_step");
            int nf = Stairs.FloorOfSteps(steps);
            if (nf > floor)
            {
                floor = nf;
                score += C.floorPoints * mult;
                Log?.Invoke("floor " + nf);
                if (hearts < C.maxHearts) { hearts = Math.Min(C.maxHearts, hearts + C.floorHeal); Toast?.Invoke(nf + "F · 하트 +1"); }
                else Toast?.Invoke(nf + "F");
                if (Array.IndexOf(C.promoteFloors, nf) >= 0) Promote(nf);
                if (Mode == GameMode.Normal && nf >= C.finalFloor && !Ended) { Sfx?.Invoke("sfx_promote"); End(true, "floor" + nf); }
            }
        }

        public void Parry(int side)
        {
            if (Ended) return;
            int s = side < 0 ? 0 : 1;
            if (time < parryCd[s]) return;
            parryStart[s] = time;
            parryEnd[s] = time + C.parryWindow;
            parryCd[s] = parryEnd[s] + C.parryCooldown;
        }

        // ------------------------------------------------------------ 규칙
        void LoseHeart(string reason)
        {
            combo = 0; mult = 1;
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
            endTimer = C.endDelay;
            foreach (var t in threats) t.done = true;
            if (!promoted) Log?.Invoke("gameover " + cause);
        }

        ThreatType PickType()
        {
            int f = Mode == GameMode.Normal ? floor : 999;   // 무한·연습: 처음부터 모든 공격
            var w = new (ThreatType t, float w)[]
            {
                (ThreatType.Clipboard, 1f),
                (ThreatType.Rival, f >= C.rivalFromFloor ? C.weightRival : 0f),
                (ThreatType.Phone, f >= C.phoneFromFloor ? C.weightPhone : 0f),
                (ThreatType.Paper, f >= C.paperFromFloor ? C.weightPaper : 0f),
                (ThreatType.Monitor, f >= C.monitorFromFloor ? C.weightMonitor : 0f),
                (ThreatType.Coffee, f >= C.coffeeFromFloor ? C.weightCoffee : 0f),
                (ThreatType.Sticky, f >= C.stickyFromFloor ? C.weightSticky : 0f),
                (ThreatType.Suitcase, f >= C.suitcaseFromFloor ? C.weightSuitcase : 0f),
            };
            double sum = 0; foreach (var e in w) sum += e.w;
            double x = rng.Next() * sum;
            foreach (var e in w) { if (x < e.w) return e.t; x -= e.w; }
            return ThreatType.Clipboard;
        }

        void SpawnThreat()
        {
            int side = rng.Next() < 0.5 ? -1 : 1;
            var type = PickType();
            float impact = time + C.warnTime + (type == ThreatType.Rival ? C.rivalWalk : 0f) + (type == ThreatType.Monitor ? C.flickerTime : 0f);
            if (impact - lastImpact < C.minAttackGap) impact = lastImpact + C.minAttackGap;
            lastImpact = impact;
            var t = new Threat
            {
                type = type, side = side, from = type == ThreatType.Paper ? -side : side,
                warnT = time, spawnT = impact - C.warnTime, impactT = impact,
                hp = type == ThreatType.Sticky ? 1 + (int)Math.Floor(rng.Next() * C.stickyMax) : 1,
            };
            threats.Add(t);
            Log?.Invoke(type.ToString().ToLowerInvariant() + " spawn");
            if (type == ThreatType.Monitor) Sfx?.Invoke("sfx_light_flicker");
            else if (type != ThreatType.Phone && type != ThreatType.Coffee) Sfx?.Invoke("sfx_warning");
        }

        static readonly Dictionary<ThreatType, (string id, float lead)> Cues = new Dictionary<ThreatType, (string, float)>
        {
            { ThreatType.Clipboard, ("sfx_cue_clipboard", 0.40f) }, { ThreatType.Rival, ("sfx_cue_rival", 0.145f) },
            { ThreatType.Phone, ("sfx_phone_ring", 0.50f) }, { ThreatType.Paper, ("sfx_cue_paper", 0.35f) },
            { ThreatType.Coffee, ("sfx_cue_coffee", 0.135f) }, { ThreatType.Sticky, ("sfx_cue_clipboard", 0.40f) },
            { ThreatType.Suitcase, ("sfx_cue_suitcase", 0.46f) },
        };

        /// <summary>한 프레임 진행. 끝난 뒤 endDelay가 지나면 true를 돌려준다 (결과 화면으로 넘어갈 때).</summary>
        public bool Tick(float dt)
        {
            time += dt;
            if (flashing) { flashLeft -= dt; if (flashLeft <= 0) flashing = false; }
            if (Ended) { endTimer -= dt; return endTimer <= 0; }

            if (Mode != GameMode.Practice) days -= dt / C.dayDuration * DrainMult();

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
                if (crackT >= C.crackTime) { Log?.Invoke("crack fall"); Sfx?.Invoke("sfx_stair_crack"); Sfx?.Invoke("sfx_gameover"); End(false, "cracked"); return false; }
            }

            if (floor >= C.flashFromFloor || Mode != GameMode.Normal)   // 카메라 플래시: 막을 수 없지만 피해도 없다
            {
                nextFlash -= dt;
                if (nextFlash <= 0)
                {
                    flashing = true; flashLeft = C.flashTime;
                    nextFlash = C.flashGap * (0.6f + (float)decor.Next() * 0.8f);
                    Sfx?.Invoke("sfx_camera_flash");
                }
            }
            if (days <= 0) { days = 0; Sfx?.Invoke("sfx_gameover"); End(false, "days"); return false; }

            if (time >= nextAttack) { SpawnThreat(); nextAttack = time + AttackInterval(); }

            for (int i = 0; i < threats.Count; i++)
            {
                var p = threats[i];
                if (p.type == ThreatType.Monitor && !p.shattered && time >= p.spawnT) { p.shattered = true; Sfx?.Invoke("sfx_monitor_shatter"); }
                if (Cues.TryGetValue(p.type, out var cue) && !p.cued && !p.done && time >= p.impactT - cue.lead) { p.cued = true; Sfx?.Invoke(cue.id); }
                if (p.done || time < p.impactT) continue;
                p.done = true;
                int s = p.side < 0 ? 0 : 1;
                bool parried = parryStart[s] <= p.impactT && p.impactT <= parryEnd[s];
                if (p.type == ThreatType.Coffee)   // 커피: 막으면 하트 -1, 그냥 두면 지나간다
                {
                    if (parried) { lastKind = "coffee"; LoseHeart("coffee"); if (Ended) break; }
                    continue;
                }
                if (parried)
                {
                    parries++; combo++;
                    mult = Math.Min(C.maxMult, 1 + combo / C.comboPerMult);   // 3연속마다 ×1, 최대 ×10
                    maxMult = Math.Max(maxMult, mult);
                    maxCombo = Math.Max(maxCombo, combo);
                    days = Math.Min(C.maxDays, days + C.parryDays);
                    score += C.parryPoints * mult;
                    Sfx?.Invoke("sfx_parry");
                    Log?.Invoke("parry ok combo=" + combo + " x" + mult);
                    if (p.type == ThreatType.Sticky && p.hp > 1)   // 포스트잇: 튕겨 나갔다가 같은 쪽으로 돌아온다
                    {
                        p.hp--; p.done = false; p.cued = false; p.spawnT = time; p.impactT = time + C.warnTime;
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
        float botT;
        public void BotTick(float dt)
        {
            if (Ended) return;
            botT += dt;
            var sh = Shock;
            bool wetAhead = Stairs.IsWet(steps + 1) && (sh.live || sh.shockIn < 0.3f);
            if (botT >= C.botStepInterval)
            {
                if (wetAhead) botT = C.botStepInterval;
                else { botT -= C.botStepInterval; Step(NextDir); }
            }
            foreach (var p in threats)
            {
                if (p.done || p.type == ThreatType.Coffee) continue;
                float tti = p.impactT - time;
                int s = p.side < 0 ? 0 : 1;
                if (tti > 0 && tti <= C.botParryLead && time >= parryCd[s]) Parry(p.side);
            }
        }
    }
}
