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
    public enum ThreatType { Clipboard, Rival, Phone, Paper, Monitor, Coffee, Sticky, Suitcase, Stapler, Airplane, Envelope, Chair }

    /// <summary>사라지는 칸의 박자 (웹 vanishState).</summary>
    public struct VanishState { public bool gone, warn; public double goneIn, backIn; }
    /// <summary>상사의 눈 (웹 gazeState). active가 false면 아직 61층 전(또는 연습). y = 위에서 아래로 내려오는 레이저 띠의 가운데(화면 좌표).</summary>
    public struct GazeState { public bool active, warn, sweep, rest; public double left, u, y; public int cycle; }
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
        public bool returned;     // 스테이플러: 반대쪽에서 돌아오는 중
        public int k;             // 종이비행기 편대·편지: 지나간 수
        public int up, down; public double? fallT;   // 의자: 생길 때 칸에서 위·아래로 같은 계단 줄에 이어진 칸 수, 계단 밖으로 떨어진 때
        public double ax, ay; public int s0;   // 정해진 길: 생길 때(또는 튕겨 돌아올 때) 플레이어가 서 있던 자리(world)와 칸 (그리기용)
        public int cueK;          // 편지: 박자 소리를 낸 수
        // ---- 경쟁 사원만 ----
        public bool locked;       // 막을 쪽이 정해졌다 (치기 rivalLeap초 전)
        public RivalPhase phase;
        public double wx;         // 걷기·서 있기: 층 바닥 위 가로 위치(월드 px)
        public double ri;         // 오르기: 계단 칸 번호(소수)
        public int floorStep;     // 걸어 들어오는 층 바닥의 칸 번호 (가장 가까운 층 바닥, 위 또는 아래)
        public bool waiting;      // 다음 칸의 계단 장애물 앞에서 기다리는 중 (머리 위 "…")
        public double downT = -9; // 에스컬레이터에 밀려 내려간 때 (그리기용)
        public double crackT, escT;   // 지금 칸(금 간 칸·에스컬레이터)에 서 있은 시간

        public bool IsRival => type == ThreatType.Rival;
        /// <summary>웹의 p.locked !== false: 경쟁 사원이 아니면 늘 true, 경쟁 사원은 막을 쪽이 정해진 뒤에만 true.</summary>
        public bool SideKnown => !IsRival || locked;
    }

    /// <summary>패링 불꽃 (웹의 sparks). 웹과 같이 화면 좌표(논리 px)다.</summary>
    public sealed class Spark { public double x, y, vx, vy, t; public bool olive; }
    /// <summary>떠오르는 글자 (웹의 texts/addText). 화면 좌표, color는 "green"·"red"·"text" (웹 PAL).</summary>
    public sealed class FloatText { public string s, color; public double x, y, t; }
    /// <summary>치고 물러나는 경쟁 사원 (웹의 leavers). 월드 좌표.</summary>
    public sealed class Leaver { public int side; public double t0, wx, wy; }
    /// <summary>계단 장애물에 당한 경쟁 사원 (웹의 fallers). kind = shock·vanish·cracked. 월드 좌표.</summary>
    public sealed class Faller { public int side; public double t0, wx, wy; public string kind; }

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
        public readonly List<Faller> fallers = new List<Faller>();
        public readonly List<FloatText> texts = new List<FloatText>();
        /// <summary>웹 PLAYER_Y: 플레이어 발이 화면 위에서 128px.</summary>
        public const double PlayerY = 128;
        /// <summary>웹 playerScreenX(): Math.round(160 + (pvx - camX)).</summary>
        public int PlayerScreenX => JsRound(160 + (pvx - camX));
        void AddText(string s, double x, double y, string color) => texts.Add(new FloatText { s = s, x = x, y = y, color = color });
        public string lastKind;
        public RunResult ended;
        public double endTimer;
        public bool flashing; double flashLeft;
        public double flashX, flashY;              // 카메라 플래시가 터진 화면 자리 (논리 px)
        /// <summary>카메라 플래시의 남은 세기 1 → 0 (그리기용).</summary>
        public double FlashK => flashing ? Math.Max(0, flashLeft / D(C.flashTime)) : 0;
        // 웹의 pvx/pvy(플레이어가 보이는 자리, 칸으로 미끄러진다)와 camX/camY. 경쟁 사원이 들어오는 자리가 camX에 기대므로 Core에서 움직인다
        public double pvx, pvy, camX, camY;
        public double parryHitT = -9; public int parryHitSide;   // 막기에 성공한 때와 쪽 (그리기용, 웹 run.parryHitT)
        public double lastStepT = -9, downT = -9;  // 마지막으로 오른 때, 밀려 내려가거나 떨어진 때 (그리기용, 웹 run.lastStepT·downT)
        public double hitTimer, stepTimer, shake, hurtFlash;   // 연출용 타이머 (웹과 같은 값)
        public int facing;
        public int rivalIdx = 8;                  // 경쟁 사원 장식이 서 있는 칸
        // ---- 새 장애물 상태 (웹 run.escT, gazeT0, gazeHitCycle, gazeKey, vanishPhase, held)
        public double escT;                       // 에스컬레이터 칸에 서 있은 시간
        public double? gazeT0;                    // 상사의 눈이 시작된 시각 (61층에 처음 닿은 때)
        public int gazeHitCycle = -1, gazeKey = -1;
        public double vanishPhase;                // 사라지는 칸 박자 옮김 (장애물 보기에서만)
        /// <summary>Q·E를 누르고 있는지 (종이비행기 편대). 보이는 쪽이 매 프레임 넣는다. 봇은 스스로 정한다.</summary>
        public readonly bool[] held = { false, false };
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
        public int NextDir => Stairs.NeedDir(steps);   // 거꾸로 칸에서는 반대쪽
        public ShockState Shock => Stairs.Shock(time);

        public VanishState Vanish()
        {
            double show = D(C.vanishShow), P = show + D(C.vanishHide), ph = (time + vanishPhase) % P;
            return new VanishState { gone = ph >= show, warn = ph >= show - D(C.vanishWarn) && ph < show, goneIn = ph < show ? show - ph : 0, backIn = ph >= show ? P - ph : 0 };
        }

        public GazeState Gaze()
        {
            if (Mode == GameMode.Practice || gazeT0 == null) return default;
            double warn = D(C.gazeWarn), sweep = D(C.gazeSweep), P = warn + sweep + D(C.gazeRest), e = time - gazeT0.Value;
            double ph = e % P; int cycle = (int)Math.Floor(e / P);
            var g = new GazeState { active = true, cycle = cycle };
            if (ph < warn) { g.warn = true; g.left = warn - ph; return g; }
            if (ph < warn + sweep)
            {
                double u = (ph - warn) / sweep, half = D(C.gazeWidth) / 2, H = D(C.viewH);
                g.sweep = true; g.u = u; g.y = Lerp(GazeSrcY, H + half, u);   // 불꽃 높이에서 화면 아래 끝까지
                return g;
            }
            g.rest = true; return g;
        }

        /// <summary>웹 GAZE_BODY_Y/H: 플레이어 몸 가운데와 반 높이 (레이저가 몸에 걸쳤는지).</summary>
        public const double GazeBodyY = PlayerY - 14, GazeBodyH = 14;
        /// <summary>웹 GAZE_SRC_Y/gazeSrcX: 레이저가 시작되는 먼 불꽃 (화면 좌표). 번갈아 왼쪽·오른쪽.</summary>
        public const double GazeSrcY = 50;
        public static double GazeSrcX(int cycle) => cycle % 2 != 0 ? 252 : 68;
        bool GazeOnPlayer() { var g = Gaze(); return g.sweep && Math.Abs(g.y - GazeBodyY) < D(C.gazeWidth) / 2 + GazeBodyH; }

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
            int need = Stairs.NeedDir(steps);   // 거꾸로 칸에서는 반대쪽
            if (dir != need) { Log?.Invoke("wrong direction"); facing = Stairs.DirAt(steps); lastKind = Stairs.IsReverse(steps) ? "reverse" : "wrong"; LoseHeart("wrong"); return; }
            if (GazeOnPlayer() && gazeHitCycle != Gaze().cycle)   // 상사의 눈이 나를 비추는 동안 움직였다
            {
                gazeHitCycle = Gaze().cycle;
                Log?.Invoke("gaze caught"); lastKind = "gaze";
                AddText("들켰다!", PlayerScreenX, PlayerY - 30, "red");
                LoseHeart("gaze");
                if (Ended) return;
            }
            steps++; crackT = 0; escT = 0;
            if (Stairs.IsCracked(steps)) { Sfx?.Invoke("sfx_stair_crack"); Log?.Invoke("crack step"); }
            days = Math.Min(D(C.maxDays), days + D(C.stepDays));
            score += C.stepPoints * mult;
            stepTimer = 0.14; lastStepT = time;
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
                    AddText(nf + "F · 하트 +1", 160, 70, "green");
                    Toast?.Invoke(nf + "F · 하트 +1");
                    Log?.Invoke("heart +1 (floor) hearts=" + hearts);
                }
                else { AddText(nf + "F", 160, 70, "text"); Toast?.Invoke(nf + "F"); }
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
            parryVar = (parryVar + 1) % 3;   // 막기 동작: 누를 때마다 곧게 막기 → 두 손 밀기 → 위로 쳐내기 (그리기용)
        }
        public int parryVar;

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
            AddText("승급! " + C.rankNames[rankIdx - 1] + " → " + RankName, 160, 56, "green");
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
                (ThreatType.Stapler, f >= C.staplerFromFloor ? D(C.weightStapler) : 0),
                (ThreatType.Airplane, f >= C.airplaneFromFloor ? D(C.weightAirplane) : 0),
                (ThreatType.Envelope, f >= C.envelopeFromFloor ? D(C.weightEnvelope) : 0),
                (ThreatType.Chair, f >= C.chairFromFloor ? D(C.weightChair) : 0),
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
            if (type == ThreatType.Chair)
            {   // 의자: 위 계단(오를 쪽)에서 생길 때 플레이어 칸의 계단 줄을 따라 내려온다 (정해진 길)
                t.side = t.from = Stairs.DirAt(steps); t.spawnT = t.impactT - 3 * D(C.chairHop);
                t.up = 0; while (t.up < 6 && Stairs.DirAt(steps + t.up) == t.side) t.up++;
                t.down = 0; while (t.down < 8 && steps - 1 - t.down >= 0 && Stairs.DirAt(steps - 1 - t.down) == t.side) t.down++;
            }
            t.ax = Stairs.StairX(steps); t.ay = -steps * D(C.stepH); t.s0 = steps;       // 정해진 길 (따라가지 않는다)
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
                // 계단을 따라 쫓아 오른다. 다음 칸이 사라졌거나(곧 사라지거나) 전기가 흐르면(곧 흐르면) 그 앞 칸에서 기다린다
                double nr = Math.Min(steps - 1, p.ri + D(C.rivalClimb) * dt);
                int c = (int)Math.Floor(nr + 1e-6);
                bool was = p.waiting;
                p.waiting = false;
                if (c > (int)Math.Floor(p.ri + 1e-6) && nr > c + 1e-9 && RivalBlocked(c + 1)) { nr = c; p.waiting = true; }
                else if (Math.Abs(p.ri - JsRound(p.ri)) < 1e-6 && RivalBlocked(JsRound(p.ri) + 1)) { nr = p.ri; p.waiting = true; }
                if (p.waiting && !was) Log?.Invoke("rival wait " + (JsRound(nr) + 1));
                p.ri = nr;
            }
            else p.waiting = false;
            if (p.phase == RivalPhase.Climb && RivalTileHazards(p, dt)) return;   // 계단 장애물에 당했다 (떨어짐·감전)
            bool ready = p.phase == RivalPhase.Stand || (p.phase == RivalPhase.Climb && p.ri >= steps - 1 - 1e-6);
            if (p.locked) return;
            if (p.phase == RivalPhase.Climb && ready) p.side = -Stairs.DirAt(steps - 1);   // 바로 뒤 칸: 플레이어가 온 쪽
            if (time >= p.impactT - D(C.rivalLeap))
            {
                if (ready) { p.locked = true; Log?.Invoke("rival lock " + (p.phase == RivalPhase.Climb ? "stairs" : "floor")); }
                else p.impactT = time + D(C.rivalLeap);                  // 아직 따라잡지 못했다: 붙을 때까지 치지 않는다
            }
        }

        // ------------------------------------------------------------ 경쟁 사원 × 계단 장애물 (웹 rivalBlocked/rivalTileHazards)
        /// <summary>경쟁 사원이 n번 칸에 올라서지 않고 기다리는지.</summary>
        bool RivalBlocked(int n)
        {
            var vs = Vanish(); var sh = Stairs.Shock(time);
            return (Stairs.IsVanish(n) && (vs.gone || vs.goneIn < D(C.rivalVanishWait))) || (Stairs.IsWet(n) && (sh.live || sh.warn));
        }

        /// <summary>경쟁 사원이 서 있는 칸의 장애물. 당하면 true (그 경쟁 사원은 치지 못하고 사라진다).</summary>
        bool RivalTileHazards(Threat p, double dt)
        {
            int i = JsRound(p.ri);
            if (Math.Abs(p.ri - i) > 1e-6) { p.crackT = 0; p.escT = 0; return false; }   // 칸 사이를 지나는 중
            string kind = null;
            if (Stairs.IsWet(i) && Stairs.Shock(time).live) kind = "shock";
            else if (Stairs.IsVanish(i) && Vanish().gone) kind = "vanish";
            if (Stairs.IsCracked(i)) { p.crackT += dt; if (kind == null && p.crackT >= D(C.rivalCrackTime)) kind = "cracked"; } else p.crackT = 0;
            if (Stairs.IsEscalator(i))                                 // 에스컬레이터: 서 있으면 경쟁 사원도 밀려 내려간다
            {
                p.escT += dt;
                if (p.escT >= D(C.escalatorPush) && i > p.floorStep) { p.escT = 0; p.ri = i - 1; Log?.Invoke("rival pushed"); Sfx?.Invoke("sfx_escalator"); p.downT = time; }
            }
            else p.escT = 0;
            if (kind == null) return false;
            p.done = true;
            var rp = RivalPos(p);
            fallers.Add(new Faller { wx = rp.x, wy = rp.y, t0 = time, kind = kind, side = p.side });
            score += C.parryPoints;                                    // 덤 점수 (콤보는 그대로)
            Log?.Invoke(kind == "shock" ? "rival shock" : "rival fall " + kind);
            Sfx?.Invoke(kind == "shock" ? "sfx_electric" : kind == "vanish" ? "sfx_vanish" : "sfx_stair_crack");
            AddText(kind == "shock" ? "경쟁 사원 감전!" : "경쟁 사원 추락!", 160 + (rp.x - camX), PlayerY + (rp.y - camY) - 30, "green");
            return true;
        }

        static readonly Dictionary<ThreatType, (string id, double lead)> Cues = new Dictionary<ThreatType, (string id, double lead)>
        {
            { ThreatType.Clipboard, ("sfx_cue_clipboard", 0.40) }, { ThreatType.Rival, ("sfx_cue_rival", 0.145) },
            { ThreatType.Phone, ("sfx_phone_ring", 0.50) }, { ThreatType.Paper, ("sfx_cue_paper", 0.35) },
            { ThreatType.Coffee, ("sfx_cue_coffee", 0.135) }, { ThreatType.Sticky, ("sfx_cue_sticky", 0.40) },
            { ThreatType.Suitcase, ("sfx_cue_suitcase", 0.46) },
            { ThreatType.Stapler, ("sfx_cue_stapler", 0.40) }, { ThreatType.Airplane, ("sfx_cue_airplane", 0.35) }, { ThreatType.Chair, ("sfx_cue_chair", 0.40) },
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
            foreach (var x in texts) { x.t += dt; x.y -= 14 * dt; }
            texts.RemoveAll(x => x.t >= 0.8);
            leavers.RemoveAll(l => time - l.t0 >= 0.5);
            fallers.RemoveAll(f => time - f.t0 >= 0.8);
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

            if (Stairs.IsEscalator(steps))   // 내려가는 에스컬레이터: 가만히 서 있으면 한 칸씩 밀려 내려간다
            {
                escT += dt;
                if (escT >= D(C.escalatorPush)) { escT = 0; steps = Math.Max(0, steps - 1); crackT = 0; facing = Stairs.DirAt(steps); Log?.Invoke("escalator push"); Sfx?.Invoke("sfx_escalator"); downT = time; }
            }
            else escT = 0;
            if (Stairs.IsVanish(steps) && Vanish().gone)   // 사라지는 칸: 없어지면 그 아래 칸으로 떨어진다
            {
                Log?.Invoke("vanish fall"); Sfx?.Invoke("sfx_vanish"); lastKind = "vanish"; downT = time;
                while (steps > 0 && Stairs.IsVanish(steps)) steps--;
                crackT = 0; escT = 0; facing = Stairs.DirAt(steps);
                AddText("떨어짐!", PlayerScreenX, PlayerY - 30, "red");
                LoseHeart("vanish");
                if (Ended) return false;
            }
            if (Mode != GameMode.Practice && gazeT0 == null && floor >= C.gazeFromFloor) { gazeT0 = time; Log?.Invoke("gaze start"); }
            var gz = Gaze();   // 상사의 눈: 예고·훑기가 시작될 때 소리
            if (gz.active && !gz.rest)
            {
                int key = gz.cycle * 2 + (gz.sweep ? 1 : 0);
                if (key != gazeKey) { gazeKey = key; if (gz.warn) { Sfx?.Invoke("sfx_gaze_warn"); Log?.Invoke("gaze warn"); } else { Sfx?.Invoke("sfx_gaze_laser"); Log?.Invoke("gaze sweep"); shake = Math.Max(shake, 0.18); } }
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
                if (Cues.TryGetValue(p.type, out var cue) && !p.cued && !p.done && p.SideKnown && p.type != ThreatType.Chair && time >= p.impactT - cue.lead) { p.cued = true; Sfx?.Invoke(cue.id); Log?.Invoke("cue " + p.type.ToString().ToLowerInvariant()); }
                if (p.type == ThreatType.Chair)   // 튀어 내려오는 의자: 두 칸씩 튀며 내려오고, 플레이어 몸에 닿는 순간 맞는다 (그 순간 오는 쪽 막기가 켜져 있으면 막힌다)
                {
                    if (p.done) continue;
                    if (p.fallT.HasValue) { if (time >= p.fallT.Value + 0.6) p.done = true; continue; }   // 계단 밖으로 떨어지는 중
                    var c = ChairPos(p, time);
                    if (c.phase == ChairPhase.Fall) { p.fallT = c.tPeak; Log?.Invoke("chair off"); continue; }   // 앞에 칸이 없다
                    var nh = ChairHitT(p);
                    if (nh.HasValue && p.cueK != nh.Value.k && time >= nh.Value.t - Cues[ThreatType.Chair].lead) { p.cueK = nh.Value.k; Sfx?.Invoke("sfx_cue_chair"); Log?.Invoke("cue chair"); }
                    if (c.phase != ChairPhase.Pre && ChairTouches(c))
                    {   // 몸에 닿았다
                        int cs = p.side < 0 ? 0 : 1; p.done = true;
                        if (parryStart[cs] <= time && time <= parryEnd[cs]) GainParry(p, time);
                        else { lastKind = "chair"; LoseHeart("attack"); }
                        if (Ended) break;
                    }
                    continue;
                }
                if (p.type == ThreatType.Envelope)   // 편지 3연발: 한 장씩 도착할 때 그 쪽 막기가 켜져 있어야 한다
                {
                    if (p.done) continue;
                    int es = p.side < 0 ? 0 : 1; double gap = D(C.envelopeGap);
                    while (p.cueK < C.envelopeCount && time >= p.impactT + p.cueK * gap - 0.35) { p.cueK++; Sfx?.Invoke("sfx_cue_envelope"); }
                    while (!p.done && p.k < C.envelopeCount && time >= p.impactT + p.k * gap)
                    {
                        double th = p.impactT + p.k * gap;
                        if (parryStart[es] <= th && th <= parryEnd[es]) { p.k++; GainParry(p, th); }
                        else { p.done = true; Log?.Invoke("envelope miss " + p.k); lastKind = "envelope"; LoseHeart("attack"); }
                    }
                    if (Ended) break;
                    if (p.k >= C.envelopeCount) p.done = true;
                    continue;
                }
                if (p.type == ThreatType.Airplane)   // 종이비행기 편대: 비행기마다 도착할 때 날아오는 쪽 키를 누르고 있어야 한다
                {
                    if (p.done) continue;
                    int hs = p.side < 0 ? 0 : 1;
                    while (!p.done && p.k < C.airplaneCount && time >= p.impactT + p.k * D(C.airplaneGap))
                    {
                        if (held[hs]) { p.k++; Sfx?.Invoke("sfx_click"); }
                        else { p.done = true; Log?.Invoke("airplane miss " + p.k); lastKind = "airplane"; LoseHeart("attack"); }
                    }
                    if (Ended) break;
                    if (!p.done && p.k >= C.airplaneCount) { p.done = true; GainParry(p); }
                    continue;
                }
                if (p.done || time < p.impactT) continue;
                p.done = true;
                if (p.IsRival) { var rp = RivalPos(p); leavers.Add(new Leaver { side = p.side, t0 = time, wx = rp.x, wy = rp.y }); }
                int s = p.side < 0 ? 0 : 1;
                bool parried = parryStart[s] <= p.impactT && p.impactT <= parryEnd[s];
                if (p.type == ThreatType.Coffee)   // 커피: 막으면 하트 -1, 그냥 두면 지나간다
                {
                    if (parried) { Log?.Invoke("coffee parried"); lastKind = "coffee"; AddText("커피!", PlayerScreenX + p.side * 10, PlayerY - 30, "red"); LoseHeart("coffee"); if (Ended) break; }
                    else Log?.Invoke("coffee dodged");
                    continue;
                }
                if (parried)
                {
                    GainParry(p);
                    if (p.type == ThreatType.Stapler && !p.returned)   // 스테이플러: 부메랑처럼 반대쪽에서 돌아온다
                    {
                        p.returned = true; p.side = -p.side; p.from = p.side; p.done = false; p.cued = false; p.spawnT = time; p.impactT = time + D(C.staplerReturn); p.ax = Stairs.StairX(steps); p.ay = -steps * D(C.stepH);
                        Log?.Invoke("stapler return");
                    }
                    if (p.type == ThreatType.Sticky && p.hp > 1)   // 포스트잇: 튕겨 나갔다가 같은 쪽으로 돌아온다
                    {
                        p.hp--; p.done = false; p.bounce = true; p.cued = false; p.spawnT = time; p.impactT = time + D(C.warnTime); p.ax = Stairs.StairX(steps); p.ay = -steps * D(C.stepH);
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

        public enum ChairPhase { Pre, Drop, Hop, Fall }
        public struct ChairState { public double x, y, u, e, tPeak; public ChairPhase phase; }
        public const double ChairArc = 30;
        /// <summary>의자 자리(world, 의자 가운데) (웹 chairPos). 생길 때 칸(s0)의 계단 줄을 따라: 첫 실제 칸(j = 6, 4, 2, 0 중 up 이하로 가장 높은 칸)까지는
        /// 층참에서 떨어져 들어오고, 두 칸씩 30px 아치로 튀며(칸 윗면에 내려앉는다), 앞에 칸이 없는 튀기는 꼭대기부터 그대로 떨어진다. 규칙과 그리기가 같이 쓴다.</summary>
        public ChairState ChairPos(Threat p, double T)
        {
            int s = p.side, n0 = p.s0; double hop = D(C.chairHop), stepH = D(C.stepH);
            double dx = Math.Abs(Stairs.StairX(n0 + 1) - Stairs.StairX(n0)); if (dx == 0) dx = D(C.stepW);
            double Lx(int j) => Stairs.StairX(n0) + s * j * dx;
            double Ly(int j) => -(n0 + j) * stepH - 7;
            int kF = Math.Max(0, (int)Math.Ceiling((6 - p.up) / 2.0)), jF = 6 - 2 * kF, kOff = (int)Math.Floor((6 + p.down) / 2.0) + 1;
            double tPeak = p.spawnT + (kOff - 0.5) * hop, t = T - p.spawnT;
            if (T >= tPeak)
            {
                double ax = Lx(8 - 2 * kOff), ay = Ly(8 - 2 * kOff), bx = Lx(6 - 2 * kOff), by = Ly(6 - 2 * kOff), e = T - tPeak;
                return new ChairState { x = (ax + bx) / 2 + (bx - ax) / hop * e, y = (ay + by) / 2 - ChairArc + 350 * e * e, phase = ChairPhase.Fall, e = e, tPeak = tPeak };
            }
            if (t < 0) return new ChairState { x = Lx(6), y = Ly(6) - (p.up < 6 ? 40 : 0), phase = ChairPhase.Pre, tPeak = tPeak };
            if (t < kF * hop)
            {
                double u = t / (kF * hop);
                return new ChairState { x = Lerp(Lx(6), Lx(jF), u), y = Lerp(Ly(6) - 40, Ly(jF), u * u), phase = ChairPhase.Drop, u = u, tPeak = tPeak };
            }
            double f = t / hop; int k = (int)Math.Floor(f); double uu = f - k;
            return new ChairState { x = Lerp(Lx(6 - 2 * k), Lx(4 - 2 * k), uu), y = Lerp(Ly(6 - 2 * k), Ly(4 - 2 * k), uu) - Math.Sin(uu * Math.PI) * ChairArc, phase = ChairPhase.Hop, u = uu, tPeak = tPeak };
        }
        /// <summary>의자 상자(가운데 ±7, ±8)가 플레이어 몸(발, 가로 ±6, 높이 26)에 닿는가 (웹 chairTouches). 플레이어 자리는 보이는 자리 pvx, pvy.</summary>
        public bool ChairTouches(ChairState c) => Math.Abs(c.x - pvx) < 13 && c.y + 8 > pvy - 26 && c.y - 8 < pvy;
        /// <summary>의자가 다음에 플레이어 몸에 닿을 때 (웹 chairHitT, 1.5초 안에, 플레이어가 지금 자리에 그대로 있다면). 없으면 null.</summary>
        public (int k, double t)? ChairHitT(Threat p)
        {
            if (p.fallT.HasValue) return null;
            for (int i = 1; i <= 90; i++)
            {
                double T = time + i / 60.0; var c = ChairPos(p, T);
                if (c.phase == ChairPhase.Fall) return null;
                if (c.phase != ChairPhase.Pre && ChairTouches(c)) return (JsRound(T * 60), T);
            }
            return null;
        }
        /// <summary>막기에 성공했을 때 (웹 gainParry): 점수·콤보·일수·불꽃·글자. 종이비행기 편대는 다 막은 뒤 한 번.</summary>
        void GainParry(Threat p, double? th = null)   // th: 맞는 순간 (편지는 장마다 다르다)
        {
            parryHitT = time; parryHitSide = p.side;   // 막기 성공 프레임 (그리기용)
            parries++; combo++;
            mult = Math.Min(C.maxMult, 1 + combo / C.comboPerMult);   // 3연속마다 ×1, 최대 ×10
            maxMult = Math.Max(maxMult, mult);
            maxCombo = Math.Max(maxCombo, combo);
            days = Math.Min(D(C.maxDays), days + D(C.parryDays));
            score += C.parryPoints * mult;
            int ps = p.side < 0 ? 0 : 1;   // 완벽하게(판정 창 한가운데에서) 막으면 더 짜릿한 소리, 조금 이르거나 늦으면 예전 소리
            bool perfect = p.type == ThreatType.Airplane || Math.Abs((th ?? p.impactT) - parryStart[ps] - D(C.parryWindow) / 2) <= D(C.parryPerfect);
            Sfx?.Invoke(perfect ? "sfx_parry_perfect" : "sfx_parry");
            int px = PlayerScreenX + p.side * 10;
            for (int k = 0; k < 8; k++)   // 패링 불꽃 (웹과 같이 decor 8번, 화면 좌표)
            {
                double a = decor.Next() * Math.PI * 2;
                sparks.Add(new Spark { x = px, y = PlayerY - 14, vx = Math.Cos(a) * 50, vy = Math.Sin(a) * 50 - 20, olive = k % 2 == 0 });
            }
            AddText("+" + D(C.parryDays).ToString(System.Globalization.CultureInfo.InvariantCulture), px, PlayerY - 42, "green");
            Log?.Invoke("parry ok combo=" + combo + " x" + mult);
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
            // 웹 botTick과 같은 순서: 끝나는 것 > 하트 -1 > 기다림
            var gz = Gaze(); var vs = Vanish();
            int vrun = 0; while (vrun < 6 && Stairs.IsVanish(steps + 1 + vrun)) vrun++;
            bool vanishBad = vrun > 0 && (vs.gone || vs.goneIn < 0.2 + vrun * interval);
            bool hereFatal = (Stairs.IsWet(steps) && !sh.live && sh.shockIn < 0.45) || (Stairs.IsCracked(steps) && crackT > 1.0);
            bool hereVanishSoon = Stairs.IsVanish(steps) && vs.goneIn < 0.45;
            double gh = D(C.gazeWidth) / 2;   // 레이저가 아직 내 몸을 다 지나가지 않았으면
            bool gazeNear = gz.sweep && gz.y < GazeBodyY + gh + GazeBodyH + 4;   // 빨라서 훑는 동안은 몸을 지나갈 때까지 기다린다
            bool wait = wetAhead || (!hereFatal && (vanishBad || (gazeNear && !hereVanishSoon)));
            if (botT >= interval)
            {
                if (wait) botT = interval;
                else { botT -= interval; Step(NextDir); }
            }
            if (Ended) return;
            // 종이비행기 편대: 첫 비행기 도착 botParryLead 전부터 날아오는 쪽 키를 누르고 있는다
            held[0] = held[1] = false;
            foreach (var p in threats) if (p.type == ThreatType.Airplane && !p.done && time >= p.impactT - D(C.botParryLead)) held[p.side < 0 ? 0 : 1] = true;
            foreach (var p in threats)
            {
                if (p.done || p.type == ThreatType.Coffee || p.type == ThreatType.Airplane || !p.SideKnown) continue;
                var ch = p.type == ThreatType.Chair ? ChairHitT(p) : null; if (p.type == ThreatType.Chair && !ch.HasValue) continue;   // 의자: 내 칸에 내려앉을 때만 막는다
                double tti = (ch.HasValue ? ch.Value.t : p.type == ThreatType.Envelope ? p.impactT + p.k * D(C.envelopeGap) : p.impactT) - time;
                int s = p.side < 0 ? 0 : 1;
                if (tti > 0 && tti <= D(C.botParryLead) && time >= parryCd[s]) Parry(p.side);
            }
        }
    }
}
