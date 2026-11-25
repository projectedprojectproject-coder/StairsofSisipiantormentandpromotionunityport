// 승급의 계단 Unity 판의 진입점. 빈 씬에 GameObject 하나를 만들고 이 컴포넌트를 붙이면 된다
// (Stairs > Create Game Scene 메뉴가 대신 해 준다).
// 화면: 타이틀 → 로비(연습·보통·무한) → 플레이 ⇄ 일시정지(조작법, 소리) → 결과. 규칙은 Core/RunSimulation,
// 여기는 입력·월드 그리기·소리·저장, HUD와 메뉴는 GameUi(uGUI)가 맡는다.
using System.Collections.Generic;
using Stairs.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stairs.Game
{
    public class GameController : MonoBehaviour
    {
        public enum Screen { Title, Lobby, Play, Pause, Result }

        public GameConfigAsset configAsset;
        public SpriteCatalog catalog;
        public AudioDirector audioDirector;
        [Tooltip("HUD·메뉴의 한글 글꼴 (NeoDunggeunmo 또는 Noto Sans KR TTF). 비우면 OS 글꼴에서 찾는다")]
        public Font uiFont;
        public int seed = 1;
        public bool bot;                       // 웹의 ?bot=1 (자동 플레이)

        public Screen screen { get; private set; } = Screen.Title;
        public RunSimulation run { get; private set; }
        public RunResult result { get; private set; }
        public int focus { get; private set; }
        public float screenTime { get; private set; }
        public string toast { get; private set; }
        public float toastUntil { get; private set; }
        public readonly float[] padUntil = new float[4];   // 조작 키 아이콘이 켜져 있는 시각 (← → Q E)
        public bool Muted => audioDirector && audioDirector.muted;

        const float LogicalPerUnit = 16f;      // 논리 px 16 = 1 유닛 (카메라 높이 180px = 11.25 유닛)
        const float PlayerY = 128f;            // 웹 PLAYER_Y: 플레이어 발이 화면 위에서 128px
        const double MaxFrame = 0.1;           // 웹 frame(): 한 번에 0.1초까지만 따라잡는다
        static readonly string[] StickyColors = { "white", "white", "yellow", "green", "blue", "pink" };
        static readonly Color SlabColor = UiKit.Hex(0x4E5259), SlabTop = UiKit.Hex(0x7B8088), PracticeFloor = UiKit.Hex(0x3A3D42);

        int runCount;
        double acc;                            // 고정 1/60초 진행에 남은 시간 (웹 frame()의 acc)
        GameUi ui;
        Camera cam;
        SpriteRenderer bg, player, flash, hurt, cameo;
        readonly List<SpriteRenderer> stairPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> slabPool = new List<SpriteRenderer>(), slabTopPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> threatPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> warnPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> fxPool = new List<SpriteRenderer>();
        readonly List<LineRenderer> arcPool = new List<LineRenderer>();
        Vector2 playerPos;                     // Unity 쪽 논리 px (위가 +y)
        string animState; double animStart;
        StairsConfig C => configAsset ? configAsset.config : (fallback ??= new StairsConfig());
        StairsConfig fallback;

        void Start()
        {
            cam = Camera.main;
            if (!cam) { cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; }
            cam.orthographic = true;
            cam.orthographicSize = 180f / 2f / LogicalPerUnit;
            cam.backgroundColor = UiKit.Bg;
            if (!audioDirector) audioDirector = gameObject.AddComponent<AudioDirector>();
            if (!audioDirector.catalog) audioDirector.catalog = catalog;
            audioDirector.SetMuted(SaveData.Muted);
            bg = NewRenderer("Background", -100);
            cameo = NewRenderer("RivalCameo", 8);
            player = NewRenderer("Player", 10);
            hurt = NewRenderer("HurtFlash", 99);
            flash = NewRenderer("CameraFlash", 100);
            hurt.sprite = flash.sprite = WhiteSprite();
            ui = new GameObject("UI").AddComponent<GameUi>();
            ui.transform.SetParent(transform, false);
            ui.Build(this, catalog, C, uiFont);
            audioDirector.SetBgm("bgm_loop");
            audioDirector.SetAmbience("amb_office");
            Go(Screen.Title);
        }

        SpriteRenderer NewRenderer(string name, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sortingOrder = order;
            return sr;
        }

        static Vector3 ToWorld(double lx, double ly) => new Vector3((float)(lx / LogicalPerUnit), (float)(ly / LogicalPerUnit), 0);
        /// <summary>Core(웹) 월드 좌표(아래가 +y) → Unity 월드.</summary>
        static Vector3 W2U(double wx, double wy) => ToWorld(wx, -wy);
        double StairUY(int i) => i * StairsConfig.D(C.stepH);   // i번째 칸 윗면의 Unity 쪽 논리 y (위로 오른다)

        // ------------------------------------------------------------ 입력과 화면 전환
        void Update()
        {
            var kb = Keyboard.current;
            screenTime += Time.deltaTime;
            if (kb != null) HandleKeys(kb);
            if (screen == Screen.Play)
            {
                // 웹과 같이 1/60초씩 고정으로 진행한다 (Unity의 들쭉날쭉한 deltaTime 대신). 그래야 같은 seed면 웹과 같은 판이 된다
                run.Bot = bot;
                acc += System.Math.Min(Time.deltaTime, MaxFrame);
                bool finished = false;
                while (acc >= RunSimulation.Step60 && !finished) { acc -= RunSimulation.Step60; finished = run.Tick(RunSimulation.Step60); }
                if (finished) FinishRun();
                else audioDirector.SetBgm(audioDirector.StageBgm(run.floor));
            }
            Draw();
            ui.Tick(screen, screenTime, focus);
        }

        void HandleKeys(Keyboard kb)
        {
            if (screen == Screen.Play)
            {
                // 끝난 뒤 결과로 넘어가기 전(endDelay)에는 일시정지하지 않는다: 멈추면 결과로 못 넘어가 갇혔다
                if (kb.escapeKey.wasPressedThisFrame && !run.Ended) { Go(Screen.Pause); return; }
                if (kb.leftArrowKey.wasPressedThisFrame) { Pad(0); run.Step(-1); }
                if (kb.rightArrowKey.wasPressedThisFrame) { Pad(1); run.Step(1); }
                if (kb.qKey.wasPressedThisFrame) { Pad(2); run.Parry(-1); }
                if (kb.eKey.wasPressedThisFrame) { Pad(3); run.Parry(1); }
                return;
            }
            var bs = ui.Buttons(screen);
            if (bs != null && bs.Count > 0)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) { focus = (focus + bs.Count - 1) % bs.Count; PlaySfx("sfx_click", 0.5f); }
                if (kb.rightArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) { focus = (focus + 1) % bs.Count; PlaySfx("sfx_click", 0.5f); }
                bool locked = screen == Screen.Result && screenTime < 0.5f;   // 웹 resultLock: 결과가 뜨자마자 눌려 넘어가지 않게
                if (!locked && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                {
                    PlaySfx("sfx_click");
                    bs[Mathf.Clamp(focus, 0, bs.Count - 1)].onClick?.Invoke();
                    return;
                }
            }
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (ui.HelpOpen) ui.ShowHelp(false);
                else if (screen == Screen.Pause) Resume();
                else if (screen == Screen.Lobby) Go(Screen.Title);
                else if (screen == Screen.Result && screenTime > 0.5f) QuitToLobby();
            }
        }

        void Pad(int i) => padUntil[i] = Time.time + 0.12f;

        // 창이 가려지면 일시정지 (웹의 visibilitychange)
        void OnApplicationFocus(bool has) { if (!has && screen == Screen.Play && run != null && !run.Ended) Go(Screen.Pause); }
        void OnApplicationPause(bool paused) { if (paused && screen == Screen.Play && run != null && !run.Ended) Go(Screen.Pause); }

        void Go(Screen s)
        {
            screen = s; screenTime = 0; focus = s == Screen.Lobby ? 1 : 0;   // 로비는 가운데 "업무 시작"
            if (s == Screen.Lobby || s == Screen.Title) audioDirector.SetBgm("bgm_loop");
            if (s == Screen.Result && result != null) audioDirector.Play(result.promoted ? "sfx_promote" : "sfx_click");
            ui.Show(s);
        }

        public void ResetFocus() => focus = 0;
        public void GoLobby() => Go(Screen.Lobby);
        public void Resume() { if (run != null) { acc = 0; Go(Screen.Play); } }
        public void QuitToLobby() { run = null; Go(Screen.Lobby); }
        public void PlaySfx(string id, float vol = 1f) => audioDirector.Play(id, vol);
        public void ToggleMute() { audioDirector.SetMuted(!audioDirector.muted); SaveData.Muted = audioDirector.muted; }

        public void StartRun(GameMode mode)
        {
            run = new RunSimulation(C, seed + runCount++, mode) { Bot = bot };
            run.Sfx += id => audioDirector.Play(id);
            run.Toast += s => { toast = s; toastUntil = Time.time + 0.9f; };
            run.Log += s => { if (bot) Debug.Log("[stairs] " + s); };
            animState = null; acc = 0;
            Go(Screen.Play);
        }

        void FinishRun()
        {
            result = run.ended;
            SaveData.Record(result);   // 최고 기록 (result.newBest)
            audioDirector.SetBgm(null);
            Go(Screen.Result);
        }

        // ------------------------------------------------------------ 월드 그리기
        void Draw()
        {
            bool inRun = run != null && (screen == Screen.Play || screen == Screen.Pause);
            player.enabled = bg.enabled = inRun;
            flash.enabled = hurt.enabled = cameo.enabled = false;
            foreach (var s in stairPool) s.enabled = false;
            foreach (var s in slabPool) s.enabled = false;
            foreach (var s in slabTopPool) s.enabled = false;
            foreach (var s in threatPool) s.enabled = false;
            foreach (var s in warnPool) s.enabled = false;
            foreach (var s in fxPool) s.enabled = false;
            foreach (var a in arcPool) a.enabled = false;
            if (!inRun) return;   // 타이틀·로비·결과의 배경은 GameUi가 그린다

            playerPos = new Vector2((float)run.pvx, (float)-run.pvy);
            // 플레이어 발이 화면 위에서 128px(웹 PLAYER_Y)에 오도록 카메라를 올린다. 맞으면 잠깐 흔들린다
            Vector2 shake = run.shake > 0 && screen == Screen.Play ? new Vector2(Mathf.Round(Random.Range(-2f, 2f)), Mathf.Round(Random.Range(-2f, 2f))) : Vector2.zero;
            cam.transform.position = ToWorld(run.camX + shake.x, -run.camY + (PlayerY - 90) + shake.y) + Vector3.back * 10;
            Vector3 camCenter = new Vector3(cam.transform.position.x, cam.transform.position.y, 0);

            bool practice = run.Mode == GameMode.Practice;
            int zone = StairLayout.ZoneOf(run.floor);
            bg.sprite = catalog ? catalog.GetImage(practice ? "bg_practice" : "bg_zone" + zone) : null;
            bg.transform.position = camCenter + Vector3.forward * 10;
            int slabs = 0;

            if (practice)
            {
                // 연습 모드: 계단 없이 옆에서 본 사무실 바닥에 선다 (웹 drawPlayWorld). 배경이 없으면 바닥 띠만
                if (!bg.sprite) Slab(slabs++, playerPos.y - 2, 60, PracticeFloor, PracticeFloor);
            }
            else
            {
                var sh = run.Shock;
                int spf = C.stairsPerFloor;
                // 층 바닥(콘크리트 슬래브): 층이 바뀌는 칸 높이에 화면 끝까지. 경쟁 사원이 이 위를 걸어온다
                for (int i = (int)System.Math.Floor((run.steps - 6) / (double)spf) * spf; i <= run.steps + 14; i += spf)
                    if (i > 0) Slab(slabs++, (float)StairUY(i), 7, SlabColor, SlabTop);
                int n = 0;
                for (int i = Mathf.Max(0, run.steps - 6); i <= run.steps + 14; i++)
                {
                    bool cracked = run.Stairs.IsCracked(i), wet = run.Stairs.IsWet(i);
                    string anim = cracked && catalog && catalog.HasAnim("stairs", "cracked") ? "cracked" : wet && catalog && catalog.HasAnim("stairs", "wet") ? "wet" : "tile";
                    var sr = Pooled(stairPool, n++, 0, "Stair");
                    sr.sprite = catalog ? catalog.Get("stairs", anim, "zone" + zone, Time.time) : null;
                    // 금 간 계단: 서 있으면 시간이 갈수록 흔들리고 붉어진다
                    double crack = cracked && i == run.steps ? run.crackT / StairsConfig.D(C.crackTime) : 0;
                    sr.color = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.55f), (float)crack);
                    double jx = crack > 0 ? System.Math.Sin(run.time * 70) * crack * 1.5 : 0;
                    sr.transform.position = ToWorld(run.Stairs.StairX(i) + jx, StairUY(i) - 6);   // 시트 가운데 기준: 디딤판 윗면이 발 높이
                    if (wet && (sh.live || sh.warn)) DrawArc(i, sh.live);
                }
            }

            // 경쟁 사원 장식 (경쟁 사원이 공격하기 전 층)
            if (!practice && C.showRivalCameo && run.floor < C.rivalFromFloor && run.rivalIdx >= run.steps - 3 && run.rivalIdx <= run.steps + 14 && catalog)
            {
                cameo.enabled = true;
                cameo.sprite = catalog.Get("rival", "walk", null, Time.time);
                cameo.color = new Color(1, 1, 1, 0.75f);
                cameo.flipX = run.Stairs.DirAt(run.rivalIdx) < 0;
                cameo.transform.position = ToWorld(run.Stairs.StairX(run.rivalIdx), StairUY(run.rivalIdx) - 1);
            }

            string pa = PlayerAnim();
            player.sprite = catalog ? catalog.Get("player", pa, run.RankName, run.time - animStart) : null;
            player.flipX = run.facing < 0 && pa != "parry_left" && pa != "parry_right";
            player.transform.position = ToWorld(playerPos.x, playerPos.y - 2);

            int k = 0, w = 0;
            foreach (var l in run.leavers)   // 치고 물러나는 경쟁 사원
            {
                double u = (run.time - l.t0) / 0.5;
                var sr = Pooled(threatPool, k++, 20, "Threat");
                sr.sprite = catalog ? catalog.Get("rival", "leave", null, run.time - l.t0) : null;
                sr.color = new Color(1, 1, 1, Mathf.Clamp01((float)(1 - u)));
                sr.flipX = l.side < 0;
                sr.transform.rotation = Quaternion.identity;
                sr.transform.position = W2U(l.wx + l.side * u * 90, l.wy - 1);
            }
            foreach (var p in run.threats)
            {
                if (p.IsRival) { DrawRival(p, k++, ref w); continue; }
                if (run.time < p.spawnT) continue;   // 모니터: 아직 형광등이 깜박이는 중
                var sr = Pooled(threatPool, k++, 20, "Threat");
                sr.sprite = ThreatSprite(p);
                sr.color = Color.white; sr.flipX = p.side > 0;
                Vector3 pos = ThreatPos(p);
                sr.transform.position = pos;
                double tt = run.time - p.spawnT;
                bool spins = p.type == ThreatType.Monitor || p.type == ThreatType.Phone || p.type == ThreatType.Suitcase;
                sr.transform.rotation = spins ? Quaternion.Euler(0, 0, -(float)((p.ph + p.spin * tt + 0.8 * System.Math.Sin(p.wob * tt * 3)) * Mathf.Rad2Deg)) : Quaternion.identity;
                // 예고: 커피는 "막지 말라"는 빨간 ×, 나머지는 "!" (웹: 맞는 자리 위 16px, 화면 안으로)
                Vector3 mark = pos + ToWorld(0, 16);
                if (p.type == ThreatType.Coffee) Cross(ref w, mark);
                else WarnMark(ref w, mark, tt);
            }

            int f = 0;
            foreach (var s in run.sparks)   // 패링 불꽃 (2px, 0.4초에 걸쳐 사라짐)
            {
                var sr = Pooled(fxPool, f++, 40, "Spark");
                sr.sprite = WhiteSprite();
                sr.color = UiKit.A(s.olive ? UiKit.Green : UiKit.Ink, Mathf.Clamp01((float)(1 - s.t / 0.4)));
                sr.transform.localScale = Vector3.one * (2f / LogicalPerUnit);
                sr.transform.rotation = Quaternion.identity;
                sr.transform.position = W2U(s.x, s.y);
            }

            if (run.hurtFlash > 0)   // 맞으면 화면이 붉게 (웹 r.flash)
            {
                hurt.enabled = true;
                hurt.color = UiKit.A(UiKit.Red, Mathf.Clamp01((float)(run.hurtFlash / 0.35)) * 0.35f);
                hurt.transform.position = camCenter + Vector3.forward * 2;
                hurt.transform.localScale = new Vector3(40, 24, 1);
            }
            if (run.flashing)   // 카메라 플래시 (무해): 하얗게 번쩍이고 사라진다
            {
                flash.enabled = true;
                flash.color = new Color(0.96f, 0.96f, 0.94f, 0.85f * (float)run.FlashK);
                flash.transform.position = camCenter + Vector3.forward * 1;
                flash.transform.localScale = new Vector3(40, 24, 1);
            }
        }

        /// <summary>층 바닥 띠: 화면 폭 전체, 윗면이 topY(Unity 논리 y), 두께 h px.</summary>
        void Slab(int i, float topY, float h, Color body, Color top)
        {
            var sr = Pooled(slabPool, i, -2, "FloorSlab");
            sr.sprite = WhiteSprite(); sr.color = body;
            sr.transform.localScale = new Vector3(22f, h / LogicalPerUnit, 1);   // 320px보다 넓게 (흔들려도 끝이 안 보이게)
            sr.transform.position = new Vector3(cam.transform.position.x, (topY - h / 2) / LogicalPerUnit, 0);
            var line = Pooled(slabTopPool, i, -1, "FloorSlabTop");
            line.sprite = WhiteSprite(); line.color = top;
            line.transform.localScale = new Vector3(22f, 1f / LogicalPerUnit, 1);
            line.transform.position = new Vector3(cam.transform.position.x, (topY - 0.5f) / LogicalPerUnit, 0);
        }

        void WarnMark(ref int w, Vector3 at, double t)
        {
            var sr = Pooled(warnPool, w++, 30, "Warning");
            sr.transform.rotation = Quaternion.identity;
            sr.sprite = catalog ? catalog.Get("warning", "blink", null, t) : null;
            if (!sr.sprite)
            {
                sr.sprite = WhiteSprite();
                sr.color = Mathf.FloorToInt(Time.time * 8) % 2 == 0 ? UiKit.Red : Color.clear;
                sr.transform.localScale = new Vector3(10f / LogicalPerUnit, 14f / LogicalPerUnit, 1);
            }
            else { sr.color = Color.white; sr.transform.localScale = Vector3.one; }
            sr.transform.position = ClampToView(at);
        }

        void Cross(ref int w, Vector3 at)   // 커피 위 빨간 × (막지 마세요)
        {
            for (int j = 0; j < 2; j++)
            {
                var sr = Pooled(warnPool, w++, 30, "Cross");
                sr.sprite = WhiteSprite(); sr.color = UiKit.Red;
                sr.transform.localScale = new Vector3(9f / LogicalPerUnit, 2f / LogicalPerUnit, 1);
                sr.transform.rotation = Quaternion.Euler(0, 0, j == 0 ? 45 : -45);
                sr.transform.position = ClampToView(at);
            }
        }

        Vector3 ClampToView(Vector3 p)   // 웹: clamp(x, 8, W - 8)
        {
            float half = (160f - 8f) / LogicalPerUnit, cx = cam.transform.position.x;
            p.x = Mathf.Clamp(p.x, cx - half, cx + half);
            return p;
        }

        void DrawRival(Threat p, int k, ref int w)
        {
            // 층 바닥 위를 걸어 들어오고, 플레이어가 더 높이 있으면 계단을 따라 쫓아 오른다. 붙으면 플레이어 칸으로 덤벼 친다
            var rp = run.RivalPos(p);
            float left = (float)(p.impactT - run.time), leap = (float)StairsConfig.D(C.rivalLeap), x = (float)rp.x, y = (float)-rp.y;
            string anim = "walk"; double at = p.phase == RivalPhase.Stand ? 0 : run.time - p.enterT;
            if (p.locked && left < leap)
            {
                float u = 1 - left / leap;
                x = Mathf.Lerp(x, playerPos.x + p.side * 12, u);
                y = Mathf.Lerp(y, playerPos.y, u) + Mathf.Sin(u * Mathf.PI) * 8;
                anim = "attack"; at = u * leap;
            }
            var sr = Pooled(threatPool, k, 20, "Threat");
            sr.sprite = catalog ? catalog.Get("rival", anim, null, at) : null;
            sr.color = Color.white;
            sr.flipX = x > playerPos.x;
            sr.transform.rotation = Quaternion.identity;
            sr.transform.position = ToWorld(x, y - 1);
            if (run.time >= p.spawnT) WarnMark(ref w, ToWorld(x, y + 35), run.time - p.spawnT);   // "!" 예고 (머리 위)
        }

        string PlayerAnim()
        {
            string s = "idle";
            if (run.Ended) s = run.ended.promoted ? "proud" : "hit";
            else if (run.hitTimer > 0) s = "hit";
            else if (run.time >= run.parryStart[0] && run.time < run.parryEnd[0]) s = "parry_left";
            else if (run.time >= run.parryStart[1] && run.time < run.parryEnd[1]) s = "parry_right";
            else if (run.stepTimer > 0) s = "step";
            if (catalog && !catalog.HasAnim("player", s)) s = "idle";   // 시트에 없는 동작은 서 있기로
            if (animState != s) { animState = s; animStart = run.time; }
            return s;
        }

        SpriteRenderer Pooled(List<SpriteRenderer> pool, int i, int order, string name)
        {
            while (pool.Count <= i) pool.Add(NewRenderer(name, order));
            pool[i].enabled = true;
            return pool[i];
        }

        Sprite ThreatSprite(Threat p)
        {
            if (!catalog) return WhiteSprite();
            switch (p.type)
            {
                case ThreatType.Sticky: return catalog.Get("sticky", "fly", StickyColors[Mathf.Clamp(p.hp, 0, 5)], 0);
                case ThreatType.Paper: return catalog.Get("projectiles", "document", null, run.time - p.spawnT);
                default: return catalog.Get("projectiles", p.type.ToString().ToLowerInvariant(), null, 0);
            }
        }

        // 날아오는 길: 결재판·커피·포스트잇은 옆에서 곧게, 서류는 반대쪽에서 머리 위로, 전화기·서류가방은 아래에서 포물선, 모니터는 위에서 70도로
        Vector3 ThreatPos(Threat p)
        {
            float u = Mathf.Clamp01((float)((run.time - p.spawnT) / System.Math.Max(0.01, p.impactT - p.spawnT)));
            Vector2 hit = playerPos + new Vector2(p.side * 8, 16);
            Vector2 start; float lift = 0;
            switch (p.type)
            {
                case ThreatType.Paper: start = hit + new Vector2(p.from * 180, 10); lift = 60; break;
                case ThreatType.Phone: case ThreatType.Suitcase: start = hit + new Vector2(p.side * 120, -110); lift = 70; break;
                case ThreatType.Monitor: start = hit + new Vector2(p.side * 40, 110); break;
                default: start = hit + new Vector2(p.side * 180, 0); break;
            }
            if (p.bounce) start = hit + new Vector2(p.side * 70, 20);   // 포스트잇: 튕겨 나갔다가 같은 쪽으로 돌아온다
            Vector2 at = Vector2.Lerp(start, hit, u) + Vector2.up * (lift * 4 * u * (1 - u));
            return ToWorld(at.x, at.y);
        }

        void DrawArc(int i, bool live)
        {
            int k = 0; while (k < arcPool.Count && arcPool[k].enabled) k++;
            if (k == arcPool.Count)
            {
                var lr = new GameObject("ShockArc").AddComponent<LineRenderer>();
                lr.transform.SetParent(transform, false);
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.positionCount = 6; lr.sortingOrder = 5; lr.useWorldSpace = true;
                arcPool.Add(lr);
            }
            var a = arcPool[k];
            a.enabled = true;
            a.startWidth = a.endWidth = live ? 0.08f : 0.04f;
            a.startColor = a.endColor = live ? new Color(0.85f, 0.95f, 1f) : new Color(1f, 0.75f, 0.3f);
            double x0 = run.Stairs.StairX(i) - 10, y = StairUY(i) - 2;
            for (int j = 0; j < 6; j++)
                a.SetPosition(j, ToWorld(x0 + j * 4, y + (live ? Random.Range(-3f, 3f) : (j % 2) * 2)));
        }

        static Sprite white;
        static Sprite WhiteSprite()
        {
            if (white) return white;
            var t = new Texture2D(4, 4); var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white; t.SetPixels(px); t.Apply();
            return white = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        }
    }
}
