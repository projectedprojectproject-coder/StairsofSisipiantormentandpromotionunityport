// 승급의 계단 Unity 판의 진입점. 빈 씬에 GameObject 하나를 만들고 이 컴포넌트를 붙이면 된다
// (Stairs > Create Game Scene 메뉴가 대신 해 준다).
// 화면: 타이틀 → 로비(보통·무한·연습) → 플레이 ⇄ 일시정지 → 결과. 규칙은 Core/RunSimulation, 여기는 입력·그리기·소리만.
// HUD와 메뉴는 첫 단계라 IMGUI로 그린다. 다음 단계에서 uGUI/UI Toolkit과 웹의 HUD 시트(hud_icons)로 바꾼다.
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
        public int seed = 1;
        public bool bot;                       // 웹의 ?bot=1 (자동 플레이)

        public Screen screen { get; private set; } = Screen.Title;
        public RunSimulation run { get; private set; }
        public RunResult result { get; private set; }

        const float LogicalPerUnit = 16f;      // 논리 px 16 = 1 유닛 (카메라 높이 180px = 11.25 유닛)
        static readonly string[] ModeNames = { "보통 모드 (1~100층)", "무한 모드", "연습 모드" };
        static readonly Dictionary<string, string> Tips = new Dictionary<string, string>
        {
            { "clipboard", "결재판은 \"딱… 딱!\"의 두 번째 딱에 맞춰 막으세요" },
            { "rival", "경쟁 사원이 계단을 쫓아오면 내가 올라온 쪽(뒤쪽) 키로 막으세요" },
            { "coffee", "커피(× 표시)는 막지 마세요. 그냥 두면 지나갑니다" },
            { "monitor", "형광등이 깜박이면 모니터! 유리 깨지는 소리 1초 뒤에 떨어집니다" },
            { "phone", "전화기는 두 번째 벨이 울리는 순간에 막으세요" },
            { "paper", "서류는 머리 위로 넘어가 반대쪽에서 맞습니다. 맞는 쪽 키로!" },
            { "sticky", "포스트잇 색은 남은 횟수: 분홍5 파랑4 초록3 노랑2 흰색1" },
            { "suitcase", "서류가방은 \"펑!\" 하고 터지는 순간에 막으세요" },
            { "wrong", "다음 계단이 어느 쪽인지 먼저 보고 누르세요. 틀리면 하트 -1" },
            { "cracked", "금 간 계단에서는 멈추지 마세요. 1.5초면 무너집니다" },
            { "shock", "젖은 계단은 3초마다 감전! 불꽃이 튀면 건너기 전에 잠깐 기다리세요" },
            { "days", "오르고 막을수록 남은 일수가 늘어납니다. 쉬지 말고 오르세요" },
        };
        static readonly string[] StickyColors = { "white", "white", "yellow", "green", "blue", "pink" };

        int modeFocus, runCount;
        float screenTime, toastUntil;
        string toast;
        Camera cam;
        SpriteRenderer bg, player, flash;
        readonly List<SpriteRenderer> stairPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> threatPool = new List<SpriteRenderer>();
        readonly List<LineRenderer> arcPool = new List<LineRenderer>();
        Vector2 playerPos, camPos;
        StairsConfig C => configAsset ? configAsset.config : (fallback ??= new StairsConfig());
        StairsConfig fallback;

        void Start()
        {
            cam = Camera.main;
            if (!cam) { cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; }
            cam.orthographic = true;
            cam.orthographicSize = 180f / 2f / LogicalPerUnit;
            cam.backgroundColor = new Color32(0x1E, 0x1F, 0x22, 0xFF);
            if (!audioDirector) audioDirector = gameObject.AddComponent<AudioDirector>();
            if (!audioDirector.catalog) audioDirector.catalog = catalog;
            bg = NewRenderer("Background", -100);
            player = NewRenderer("Player", 10);
            flash = NewRenderer("CameraFlash", 100);
            flash.color = new Color(1, 1, 1, 0);
            audioDirector.SetBgm("bgm_loop");
            audioDirector.SetAmbience("amb_office");
        }

        SpriteRenderer NewRenderer(string name, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sortingOrder = order;
            return sr;
        }

        static Vector3 ToWorld(float lx, float ly) => new Vector3(lx / LogicalPerUnit, ly / LogicalPerUnit, 0);

        // ------------------------------------------------------------ 입력과 화면 전환
        void Update()
        {
            var kb = Keyboard.current;
            screenTime += Time.deltaTime;
            if (kb == null) return;
            switch (screen)
            {
                case Screen.Title:
                    if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Go(Screen.Lobby);
                    break;
                case Screen.Lobby:
                    if (kb.upArrowKey.wasPressedThisFrame) modeFocus = (modeFocus + 2) % 3;
                    if (kb.downArrowKey.wasPressedThisFrame) modeFocus = (modeFocus + 1) % 3;
                    if (kb.enterKey.wasPressedThisFrame) StartRun((GameMode)modeFocus);
                    if (kb.escapeKey.wasPressedThisFrame) Go(Screen.Title);
                    break;
                case Screen.Play:
                    if (kb.escapeKey.wasPressedThisFrame) { Go(Screen.Pause); break; }
                    if (kb.leftArrowKey.wasPressedThisFrame) run.Step(-1);
                    if (kb.rightArrowKey.wasPressedThisFrame) run.Step(1);
                    if (kb.qKey.wasPressedThisFrame) run.Parry(-1);
                    if (kb.eKey.wasPressedThisFrame) run.Parry(1);
                    if (bot) run.BotTick(Time.deltaTime);
                    if (run.Tick(Time.deltaTime)) { result = run.ended; Go(Screen.Result); audioDirector.SetBgm(null); }
                    else audioDirector.SetBgm(audioDirector.StageBgm(run.floor));
                    break;
                case Screen.Pause:
                    if (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Go(Screen.Play);
                    if (kb.backspaceKey.wasPressedThisFrame) Go(Screen.Lobby);
                    break;
                case Screen.Result:
                    if (screenTime > 1.2f && kb.enterKey.wasPressedThisFrame) StartRun(result.mode);
                    if (screenTime > 1.2f && kb.escapeKey.wasPressedThisFrame) Go(Screen.Lobby);
                    break;
            }
            Draw();
        }

        void Go(Screen s)
        {
            screen = s; screenTime = 0;
            if (s == Screen.Lobby || s == Screen.Title) audioDirector.SetBgm("bgm_loop");
            if (s == Screen.Result && result != null) audioDirector.Play(result.promoted ? "sfx_promote" : "sfx_click");
        }

        void StartRun(GameMode mode)
        {
            run = new RunSimulation(C, seed + runCount++, mode);
            run.Sfx += id => audioDirector.Play(id);
            run.Toast += s => { toast = s; toastUntil = Time.time + 0.9f; };
            run.Log += s => { if (bot) Debug.Log("[stairs] " + s); };
            playerPos = camPos = new Vector2(run.Stairs.StairX(run.steps), -run.steps * C.stepH);
            Go(Screen.Play);
        }

        // ------------------------------------------------------------ 월드 그리기
        void Draw()
        {
            bool inRun = run != null && (screen == Screen.Play || screen == Screen.Pause);
            player.enabled = inRun;
            if (!inRun) flash.enabled = false;
            foreach (var s in stairPool) s.enabled = false;
            foreach (var s in threatPool) s.enabled = false;
            foreach (var a in arcPool) a.enabled = false;
            if (!inRun)
            {
                bg.sprite = catalog ? catalog.GetImage(screen == Screen.Result ? "bg_result" : screen == Screen.Lobby ? "bg_lobby" : "title_bg") : null;
                bg.transform.position = cam.transform.position + Vector3.forward * 10;
                return;
            }
            float dt = screen == Screen.Play ? Time.deltaTime : 0;
            var target = new Vector2(run.Stairs.StairX(run.steps), -run.steps * C.stepH);
            playerPos = Vector2.Lerp(playerPos, target, Mathf.Min(1, dt * 22));
            camPos = new Vector2(Mathf.Lerp(camPos.x, playerPos.x, Mathf.Min(1, dt * 8)), playerPos.y);
            // 플레이어는 화면 아래쪽(웹의 PLAYER_Y = 128)에 서도록 카메라를 올린다
            cam.transform.position = ToWorld(camPos.x, camPos.y + (128 - 90)) + Vector3.back * 10;

            int zone = StairLayout.ZoneOf(run.floor);
            bg.sprite = catalog ? catalog.GetImage(run.Mode == GameMode.Practice ? "bg_practice" : "bg_zone" + zone) : null;
            bg.transform.position = cam.transform.position + Vector3.forward * 10;

            var sh = run.Shock;
            int n = 0;
            for (int i = Mathf.Max(0, run.steps - 10); i < run.steps + 16; i++)
            {
                bool cracked = run.Stairs.IsCracked(i), wet = run.Stairs.IsWet(i);
                string anim = cracked && catalog && catalog.HasAnim("stairs", "cracked") ? "cracked" : wet && catalog && catalog.HasAnim("stairs", "wet") ? "wet" : "tile";
                var sr = Pooled(stairPool, n++, 0);
                sr.sprite = catalog ? catalog.Get("stairs", anim, "zone" + StairLayout.ZoneOf(run.Stairs.FloorOfSteps(i)), run.time) : null;
                sr.color = Color.white;
                float jx = cracked && i == run.steps ? Mathf.Sin(run.time * 60) * run.crackT : 0;   // 금 간 계단: 서 있으면 흔들림
                sr.transform.position = ToWorld(run.Stairs.StairX(i) + jx, -i * C.stepH - 6);   // 시트 가운데 기준: 디딤판 윗면이 발 높이
                if (wet && (sh.live || sh.warn)) DrawArc(i, sh.live);
            }

            player.sprite = catalog ? catalog.Get("player", PlayerAnim(), run.RankName, run.time) : null;
            player.flipX = run.NextDir < 0;
            player.transform.position = ToWorld(playerPos.x, playerPos.y);

            int k = 0;
            foreach (var p in run.threats)
            {
                if (run.time < p.spawnT) continue;
                var sr = Pooled(threatPool, k++, 20);
                sr.sprite = ThreatSprite(p);
                sr.transform.position = ThreatPos(p);
            }
            flash.enabled = run.flashing;
            if (run.flashing) { flash.color = new Color(1, 1, 1, 0.8f); flash.transform.position = cam.transform.position + Vector3.forward * 1; flash.transform.localScale = new Vector3(40, 24, 1); flash.sprite = WhiteSprite(); }
        }

        string PlayerAnim()
        {
            int s = run.time <= run.parryEnd[0] ? -1 : run.time <= run.parryEnd[1] ? 1 : 0;
            return s < 0 ? "parry_left" : s > 0 ? "parry_right" : "idle";
        }

        SpriteRenderer Pooled(List<SpriteRenderer> pool, int i, int order)
        {
            while (pool.Count <= i) pool.Add(NewRenderer(order == 0 ? "Stair" : "Threat", order));
            pool[i].enabled = true;
            return pool[i];
        }

        Sprite ThreatSprite(Threat p)
        {
            if (!catalog) return null;
            switch (p.type)
            {
                case ThreatType.Rival: return catalog.Get("rival", "walk", null, run.time);
                case ThreatType.Sticky: return catalog.Get("sticky", "fly", StickyColors[Mathf.Clamp(p.hp, 0, 5)], run.time);
                case ThreatType.Paper: return catalog.Get("projectiles", "document", null, run.time);
                default: return catalog.Get("projectiles", p.type.ToString().ToLowerInvariant(), null, run.time);
            }
        }

        // 날아오는 길: 결재판·커피·포스트잇·경쟁 사원은 옆에서 곧게, 서류는 반대쪽에서 머리 위로, 전화기·서류가방은 아래에서 포물선, 모니터는 위에서 70도로
        Vector3 ThreatPos(Threat p)
        {
            float u = Mathf.Clamp01((run.time - p.spawnT) / Mathf.Max(0.01f, p.impactT - p.spawnT));
            Vector2 hit = playerPos + new Vector2(p.side * 10, 14);
            Vector2 start; float lift = 0;
            switch (p.type)
            {
                case ThreatType.Paper: start = hit + new Vector2(p.from * 180, 10); lift = 60; break;
                case ThreatType.Phone: case ThreatType.Suitcase: start = hit + new Vector2(p.side * 120, -110); lift = 70; break;
                case ThreatType.Monitor: start = hit + new Vector2(p.side * 40, 110); break;
                default: start = hit + new Vector2(p.side * 180, 0); break;
            }
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
            float x0 = run.Stairs.StairX(i) - 10, y = -i * C.stepH - 2;
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

        // ------------------------------------------------------------ HUD와 메뉴 (임시: IMGUI)
        void OnGUI()
        {
            float s = UnityEngine.Screen.height / 180f;   // 논리 1px
            var big = new GUIStyle(GUI.skin.label) { fontSize = (int)(14 * s), alignment = TextAnchor.MiddleCenter, richText = true };
            var small = new GUIStyle(big) { fontSize = (int)(7 * s) };
            float W = UnityEngine.Screen.width, H = UnityEngine.Screen.height;
            switch (screen)
            {
                case Screen.Title:
                    GUI.Label(new Rect(0, H * 0.3f, W, 20 * s), "승급의 계단", big);
                    GUI.Label(new Rect(0, H * 0.6f, W, 10 * s), "Enter 를 눌러 출근", small);
                    break;
                case Screen.Lobby:
                    for (int i = 0; i < 3; i++)
                        GUI.Label(new Rect(0, H * 0.35f + i * 14 * s, W, 12 * s), (i == modeFocus ? "▶ " : "") + ModeNames[i], small);
                    GUI.Label(new Rect(0, H * 0.85f, W, 10 * s), "↑↓ 선택 · Enter 시작 · ← → 오르기 · Q E 막기", small);
                    break;
                case Screen.Play:
                case Screen.Pause:
                    var hud = new GUIStyle(small) { alignment = TextAnchor.UpperLeft };
                    string hearts = new string('♥', run.hearts) + new string('♡', Mathf.Max(0, C.maxHearts - run.hearts));
                    GUI.Label(new Rect(8 * s, 6 * s, 160 * s, 10 * s), $"{run.floor}F · {run.RankName}", hud);
                    GUI.Label(new Rect(W - 90 * s, 6 * s, 90 * s, 10 * s), run.Mode == GameMode.Practice ? $"맞은 수 {run.hits}" : $"D-{Mathf.CeilToInt(run.days)}  {hearts}", hud);
                    GUI.Label(new Rect(8 * s, 18 * s, 160 * s, 10 * s), $"점수 {run.score}", hud);
                    if (run.combo > 0) GUI.Label(new Rect(8 * s, 28 * s, 160 * s, 10 * s), $"{run.combo} COMBO · 점수 ×{run.mult}", hud);
                    if (run.Shock.warn && run.Stairs.IsWet(run.steps + 1)) GUI.Label(new Rect(0, H * 0.25f, W, 10 * s), "⚡ 곧 전기가 흐릅니다", small);
                    if (toast != null && Time.time < toastUntil) GUI.Label(new Rect(0, H * 0.35f, W, 12 * s), toast, small);
                    if (screen == Screen.Pause) GUI.Label(new Rect(0, H * 0.45f, W, 20 * s), "일시정지\n<size=" + (int)(6 * s) + ">Esc 계속 · Backspace 로비</size>", big);
                    break;
                case Screen.Result:
                    if (result == null) break;
                    string head = result.promoted ? "승급 성공" : "구조조정 대상";
                    string cause = result.promoted ? $"{result.floor}층 도달" :
                        result.cause == "shock" ? "젖은 계단에서 감전" : result.cause == "cracked" ? "금 간 계단이 무너짐" : result.cause == "days" ? "남은 일수 소진" : "하트 소진";
                    // 도장 애니메이션: 0.35초 동안 크게 찍혀 내려앉는다
                    float u = Mathf.Clamp01(screenTime / 0.35f), scale = Mathf.Lerp(2.2f, 1f, u * u);
                    var stamp = new GUIStyle(big) { fontSize = (int)(14 * s * scale) };
                    stamp.normal.textColor = result.promoted ? new Color(0.54f, 0.6f, 0.36f) : new Color(0.63f, 0.24f, 0.24f);
                    GUI.Label(new Rect(0, H * 0.12f, W, 24 * s), head, stamp);
                    GUI.Label(new Rect(0, H * 0.3f, W, 10 * s), cause, small);
                    GUI.Label(new Rect(0, H * 0.42f, W, 40 * s),
                        $"도달 층수 {result.floor}F\n직급 {C.rankNames[result.rankIdx]}\n점수 {result.score}\n최고 콤보 {result.maxCombo}회 ×{result.maxMult}", small);
                    if (!result.promoted && Tips.TryGetValue(result.tip, out var tip) && screenTime > 0.5f)
                        GUI.Label(new Rect(0, H * 0.75f, W, 10 * s), "TIP  " + tip, small);
                    GUI.Label(new Rect(0, H * 0.88f, W, 10 * s), "Enter 다시 하기 · Esc 로비", small);
                    break;
            }
        }
    }
}
