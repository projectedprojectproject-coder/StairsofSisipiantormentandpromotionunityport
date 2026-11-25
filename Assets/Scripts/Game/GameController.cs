// 승급의 계단 Unity 판. 웹 PoC(demo_api/game.js)를 그대로 옮겼다:
//   규칙   → Core/RunSimulation (웹과 프레임 단위로 같다, ParityTests)
//   그리기 → 이 파일의 Draw* 함수 = game.js의 drawTitle/drawLobby/drawPlayWorld/drawHud/drawPause/drawSettings/drawHelp/drawResult를
//            한 줄씩 옮긴 것. WebCanvas가 웹 캔버스(rect/txt/drawSprite/drawImg, save/translate/rotate)를 흉내 낸다.
//   입력·메뉴·소리 → getButtons/keydown/mousedown/syncAudio를 옮긴 것.
// 빈 씬에 GameObject 하나를 만들고 이 컴포넌트를 붙이면 된다 (Stairs > Create Game Scene).
// 명령줄: -seed N (웹 ?seed=N), -bot (웹 ?bot=1), -mute (웹 ?mute=1).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Stairs.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Stairs.Game
{
    public class GameController : MonoBehaviour
    {
        public enum Screen { Title, Lobby, Play, Pause, Result }

        public GameConfigAsset configAsset;
        public SpriteCatalog catalog;
        public AudioDirector audioDirector;
        [Tooltip("글꼴. 비우면 Resources/Fonts의 Noto Sans KR(웹 txt의 글꼴), 그것도 없으면 OS 한글 글꼴")]
        public Font uiFont, uiFontBold;
        [Tooltip("-1이면 웹처럼 시각으로 정한다 (Date.now() & 0x7fffffff)")]
        public int seed = -1;
        public bool bot;                       // 웹 ?bot=1
        public bool ignoreInput;               // 자동 플레이테스트: 키보드·마우스를 무시한다 (창에 눌린 키가 판을 바꾸지 않게)
        public bool mute;                      // 웹 ?mute=1

        // ---- 웹의 전역 상태 ----
        public Screen screen { get; private set; } = Screen.Title;
        public RunSimulation run { get; private set; }
        public RunResult result { get; private set; }
        public string FontName => font ? font.name : "없음";
        int focus;
        Demo demo;   // 장애물 보기 (조작법·결과 화면에서 연다)
        double resultLock, resultT0;
        bool resultStamped, settingsOpen, helpOpen;
        string toastText = ""; double toastUntil;
        readonly double[] padUntil = new double[4];   // ←, →, Q, E 키 아이콘 발광
        double time, acc;
        int runCount, SEED;
        string animState = ""; double animStart;
        const double STEP = 1.0 / 60.0;
        const float W = WebCanvas.W, H = WebCanvas.H;
        const float PLAYER_Y = 128;

        StairsConfig C => configAsset ? configAsset.config : (fallback ??= new StairsConfig());
        StairsConfig fallback;
        static double D(float v) => StairsConfig.D(v);

        Camera cam;
        WebCanvas cv;
        Font font;
        float scale = 1; Rect viewport;
        Vector2 lastMouse = new Vector2(-1, -1);

        static readonly Color PalGray = WebCanvas.Gray, PalGreen = WebCanvas.Green, PalRed = WebCanvas.Red, PalBg = WebCanvas.Bg, PalText = WebCanvas.Text;
        static Color Hex(string s) => WebCanvas.Hex(s);
        static readonly string[] StickyColors = { "white", "white", "yellow", "green", "blue", "pink" };
        static readonly Color[] RankColors = { WebCanvas.Gray, WebCanvas.Green, WebCanvas.Red, WebCanvas.Text, WebCanvas.Text };
        static readonly string[] StageBgm = { "bgm_stage1", "bgm_stage2", "bgm_stage3", "bgm_stage4", "bgm_stage5" };

        public static readonly Dictionary<string, string> Tips = new Dictionary<string, string>
        {
            { "clipboard", "결재판은 \"딱… 딱!\"의 두 번째 딱에 맞춰 막으세요" },
            { "stapler", "스테이플러는 막으면 반대쪽에서 돌아와요. 반대쪽 키로 한 번 더 막으세요" },
            { "airplane", "종이비행기 편대는 날아오는 쪽 키를 꾹 누르고 있으세요" },
            { "envelope", "편지 3장은 같은 쪽에서 박자에 맞춰 와요. 한 장씩 세 번 막으세요" },
            { "chair", "튀어 내려오는 의자는 내 칸에 내려앉을 때만 맞아요. 가만히 서서 오는 쪽 키로 막거나, 다른 칸으로 피하세요" },
            { "reverse", "보라색 거꾸로 칸에서는 반대쪽 화살표를 눌러야 올라가요" },
            { "vanish", "사라지는 칸은 5초 있다가 1초 없어져요. 깜박이면 기다렸다가 건너세요" },
            { "gaze", "빨간 레이저(상사의 눈)가 내 위를 지나갈 때는 오르지 마세요. 가만히 있으면 안전해요" },
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

        int PX => catalog ? Mathf.Max(1, catalog.pixelScale) : 1;

        // ================================================================ 시작
        void Awake()
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == "-bot") bot = true;
                else if (a[i] == "-mute") mute = true;
                else if (a[i] == "-seed" && i + 1 < a.Length && int.TryParse(a[i + 1], out var s)) seed = s;
            }
            SEED = seed >= 0 ? seed : (int)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond & 0x7fffffff);
            Application.runInBackground = true;   // 웹 탭처럼 초점이 없어도 돈다
        }

        void Start()
        {
            cam = Camera.main;
            if (!cam) { cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = PalBg;          // 웹 body/canvas 배경 #1E1F22
            cam.cullingMask = 0;                  // 월드에는 아무것도 없다. 모두 WebCanvas로 그린다
            if (!audioDirector) audioDirector = gameObject.AddComponent<AudioDirector>();
            if (!audioDirector.catalog) audioDirector.catalog = catalog;
            audioDirector.muted = mute;
            // 웹 txt: (bold) size px "Noto Sans KR", sans-serif. 설치된 Noto Sans KR은 가변 글꼴이라 Unity가 굵기를 고르지 못해
            // 가는(100) 굵기로 나왔다. 그래서 보통(400)·굵게(700)로 고정한 글꼴 파일을 함께 넣었다 (Resources/Fonts, SIL OFL)
            font = uiFont ? uiFont : Resources.Load<Font>("Fonts/NotoSansKR-Regular");
            var bold = uiFontBold ? uiFontBold : Resources.Load<Font>("Fonts/NotoSansKR-Bold");
            if (!font) { font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR" }, 16); bold = null; }
            cv = new WebCanvas(font, bold);
            Camera.onPostRender += OnCamPostRender;
            Resize();
            cv.Warm("0123456789FDx×-+·.,:()→∞%#!?/ ", new[] { 5, 6, 7, 8, 9, 10, 11, 12, 13, 16, 18, 26 });
            SyncAudio(true);
        }

        void OnDestroy() { Camera.onPostRender -= OnCamPostRender; cv?.Dispose(); }

        // 웹 resize(): 아트 픽셀이 화면 픽셀에 딱 맞도록 배율을 PX의 배수로 (창이 너무 작으면 1배). 캔버스는 가운데
        void Resize()
        {
            int sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height, px = PX;
            int k = Mathf.FloorToInt(Mathf.Min(sw / (W * px), sh / (H * px)));
            scale = k >= 1 ? k * px : Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(sw / W, sh / H)));
            viewport = new Rect(Mathf.Floor((sw - W * scale) / 2), Mathf.Floor((sh - H * scale) / 2), W * scale, H * scale);
            cv.SetScale(scale);
        }

        // ================================================================ 메인 루프 (웹 frame/update)
        void Update()
        {
            Resize();
            if (!ignoreInput) { HandleKeys(); HandleMouse(); }   // 자동 플레이테스트 중에는 사람 입력을 받지 않는다
            // 종이비행기 편대: Q·E를 누르고 있는지 (웹 keydown/keyup의 held). 봇이면 봇이 정한다
            var kbd = Keyboard.current;
            if (run != null && !run.Bot && kbd != null && !ignoreInput)
            {
                bool live = screen == Screen.Play && !MenuMode();
                run.held[0] = live && kbd.qKey.isPressed; run.held[1] = live && kbd.eKey.isPressed;
            }
            acc += Math.Min(Time.deltaTime, 0.1);
            while (acc >= STEP) { acc -= STEP; StepFrame(STEP); }
        }

        void StepFrame(double dt)
        {
            time += dt;
            if (resultLock > 0) resultLock -= dt;
            if (demo != null) DemoUpdate(dt);
            else if (screen == Screen.Play && run != null && !settingsOpen)
            {
                run.Bot = bot;
                if (run.Tick(dt)) FinishRun();
            }
            SyncAudio(false);
        }

        void OnCamPostRender(Camera c)
        {
            if (c != cam || cv == null) return;
            cv.Begin(viewport, scale);
            Draw();
            cv.End();
        }

        void Draw()
        {
            cv.globalAlpha = 1;
            Rect(0, 0, W, H, PalBg);
            switch (screen)
            {
                case Screen.Title: DrawTitle(); break;
                case Screen.Lobby: DrawLobby(); break;
                case Screen.Play: if (run != null) DrawPlay(); break;
                case Screen.Pause: if (run != null) DrawPause(); break;
                case Screen.Result: if (result != null) DrawResult(); break;
            }
            if (settingsOpen) DrawSettings();
            if (helpOpen) DrawHelp();
            if (demo != null) DrawDemo();
        }

        // ================================================================ 소리 (웹 sfx/desiredBgm/syncAudio)
        void Sfx(string id) => audioDirector.Sfx(id, C.sfxVolume);
        string audioKey; double audioSyncT;
        string DesiredBgm()
        {
            if (screen == Screen.Result) return null;
            if ((screen == Screen.Play || screen == Screen.Pause) && run != null)
            {
                string id = StageBgm[Math.Min(4, (run.floor - 1) / 20)];   // 음악은 20층마다
                if (audioDirector.Has(id)) return id;
                if (run.floor >= C.tenseFloor) return "bgm_loop_tense";
            }
            return "bgm_loop";
        }
        void SyncAudio(bool force)
        {
            string key = DesiredBgm() + "|" + audioDirector.muted + "|" + audioDirector.vol + "|" + (screen == Screen.Pause);
            if (!force && key == audioKey && time - audioSyncT < 1) return;
            audioKey = key; audioSyncT = time;
            var want = new Dictionary<string, float>();
            if (!audioDirector.muted)
            {
                string b = DesiredBgm();
                if (b != null) want[b] = C.bgmVolume * (screen == Screen.Pause ? 0.4f : 1f);
                want["amb_office"] = C.ambVolume;
            }
            var ids = new List<string> { "bgm_loop", "bgm_loop_tense", "amb_office" }; ids.AddRange(StageBgm);
            audioDirector.SyncLoops(ids, want);
        }

        // ================================================================ 한 판 (웹 startRun/finishRun)
        public void StartRun(GameMode mode)
        {
            int s = SEED + runCount; runCount++;
            run = new RunSimulation(C, s, mode) { Bot = bot };
            run.Sfx += Sfx;
            run.Log += m => { if (bot) Debug.Log("[stairs] " + m); };
            animState = ""; animStart = 0;
            screen = Screen.Play; focus = 0; settingsOpen = false;
            Debug.Log("[stairs] run start seed=" + s + " mode=" + mode.ToString().ToLowerInvariant());
        }

        void FinishRun()
        {
            result = run.ended;
            SaveData.Record(result);   // 무한은 최고 층만, 보통은 최고 층·점수 (웹 save와 같은 키)
            resultT0 = time; resultStamped = false;
            screen = Screen.Result; focus = 0; resultLock = 0.5;
        }

        // PlaytestDriver와 외부에서 쓰는 조작 (웹 버튼과 같은 동작)
        public void GoLobby() { screen = Screen.Lobby; focus = 0; }
        public void PauseGame() { if (screen == Screen.Play) { screen = Screen.Pause; focus = 0; } }
        public void Resume() { if (screen == Screen.Pause) { screen = Screen.Play; focus = 0; } }
        public void QuitToLobby() { screen = Screen.Lobby; focus = 0; settingsOpen = false; helpOpen = false; }
        public void OpenSettings() { settingsOpen = true; focus = 0; }
        public void CloseSettings() { settingsOpen = false; focus = 0; }
        public void OpenHelp() { helpOpen = true; focus = 0; }
        public void CloseHelp() { helpOpen = false; focus = 0; }
        public string ShareResult() => DoShare();
        void Toast(string s) { toastText = s; toastUntil = time + 2.5; }
        void ToTitle() { screen = Screen.Title; focus = 0; settingsOpen = false; helpOpen = false; }

        // ================================================================ 버튼 (웹 btn/getButtons/activate)
        sealed class Btn
        {
            public string id, label, tone, icon; public float x, y, w, h; public int size; public Action fn; public bool off;
        }
        static Btn Off(Btn b, bool off) { b.off = off; return b; }
        static Btn B(string id, string label, float x, float y, float w, float h, Action fn, int size = 0) => new Btn { id = id, label = label, x = x, y = y, w = w, h = h, fn = fn, size = size };

        List<Btn> GetButtons()
        {
            if (demo != null)
            {
                return new List<Btn>
                {
                    B("dprev", "◀ 이전", 8, 158, 48, 16, () => DemoGo(demo.idx - 1), 8),
                    B("dnext", "다음 ▶", 60, 158, 48, 16, () => DemoGo(demo.idx + 1), 8),
                    B("dclose", "닫기", 264, 158, 48, 16, CloseDemo, 8),
                };
            }
            if (helpOpen) return new List<Btn> { B("demo", "장애물 보기", 60, 150, 96, 14, () => OpenDemo("climb")), B("close", "닫기", 164, 150, 96, 14, CloseHelp) };
            if (settingsOpen)
            {
                return new List<Btn>
                {
                    Off(B("snd", audioDirector.muted ? "꺼짐" : "켜짐", 170, 34, 88, 14, () => { audioDirector.muted = !audioDirector.muted; SyncAudio(true); }), audioDirector.muted),
                    B("vdn", "-", 170, 52, 24, 14, () => { audioDirector.vol = Mathf.Clamp01(Mathf.Round((audioDirector.vol - 0.1f) * 10) / 10); SyncAudio(true); }),
                    B("vup", "+", 234, 52, 24, 14, () => { audioDirector.vol = Mathf.Clamp01(Mathf.Round((audioDirector.vol + 0.1f) * 10) / 10); SyncAudio(true); }),
                    B("fs", "전체화면", 170, 70, 88, 14, () => { UnityEngine.Screen.fullScreen = !UnityEngine.Screen.fullScreen; }),
                    B("help", "보기", 170, 96, 88, 14, OpenHelp),
                    B("demo", "보기", 170, 114, 88, 14, () => OpenDemo("climb")),   // 장애물 보기 (장애물마다 대처법 장면)
                    B("close", "닫기", 110, 144, 100, 16, CloseSettings),
                };
            }
            switch (screen)
            {
                case Screen.Title:
                    var start = B("start", "시작하기", 122, 98, 76, 18, () => { screen = Screen.Lobby; focus = 0; }, 10); start.tone = "red";
                    var gear = B("settings", "", 298, 4, 18, 18, OpenSettings); gear.icon = "gear";
                    return new List<Btn> { start, gear };
                case Screen.Lobby:
                    var work = B("work", "업무 시작", 104, 150, 112, 18, () => StartRun(GameMode.Normal), 10); work.tone = "red";   // 05-2 요소 4 (ESC: 타이틀로)
                    var lobbyGear = B("settings", "", 226, 5, 18, 18, OpenSettings); lobbyGear.icon = "gear";   // 설정 (층 명판과 D-30 사이)
                    return new List<Btn> { work, B("endless", "무한 모드", 224, 150, 84, 18, () => StartRun(GameMode.Endless), 8), B("practice", "연습 모드", 12, 150, 84, 18, () => StartRun(GameMode.Practice), 8), lobbyGear };
                case Screen.Pause:
                    return new List<Btn>
                    {
                        B("resume", "계속하기", 100, 66, 120, 14, () => { screen = Screen.Play; focus = 0; }),
                        B("help", "조작법", 100, 84, 120, 14, OpenHelp),
                        B("settings", "설정", 100, 102, 120, 14, OpenSettings),
                        B("main", "메인으로", 100, 120, 120, 14, () => { Debug.Log("[stairs] quit to title"); ToTitle(); }),
                    };
                case Screen.Result:
                    var again = B("again", result != null && result.promoted ? "1층부터 다시" : "다시 하기", 96, 155, 62, 16, () => StartRun(result != null ? result.mode : GameMode.Normal), 8); again.tone = "red";   // 05-5 요소 5
                    var resultGear = B("settings", "", 298, 4, 18, 18, OpenSettings); resultGear.icon = "gear";   // 설정 (장애물 보기도 여기서)
                    return new List<Btn> { again, B("share", "기록 공유", 166, 155, 62, 16, () => DoShare(), 8), resultGear };   // 요소 2
            }
            return new List<Btn>();
        }
        void Activate(Btn b) { if (b == null) return; Sfx("sfx_click"); b.fn(); }

        // ================================================================ 입력 (웹 keydown/mousemove/mousedown/visibilitychange)
        bool MenuMode() => screen != Screen.Play || settingsOpen || helpOpen || demo != null;

        void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) KeyDown("Escape");
            if (kb.leftArrowKey.wasPressedThisFrame) KeyDown("ArrowLeft");
            if (kb.rightArrowKey.wasPressedThisFrame) KeyDown("ArrowRight");
            if (kb.upArrowKey.wasPressedThisFrame) KeyDown("ArrowUp");
            if (kb.downArrowKey.wasPressedThisFrame) KeyDown("ArrowDown");
            if (kb.qKey.wasPressedThisFrame) KeyDown("KeyQ");
            if (kb.eKey.wasPressedThisFrame) KeyDown("KeyE");
            if (kb.tabKey.wasPressedThisFrame) KeyDown("Tab");
            if (kb.enterKey.wasPressedThisFrame) KeyDown("Enter");
            if (kb.numpadEnterKey.wasPressedThisFrame) KeyDown("NumpadEnter");
            if (kb.spaceKey.wasPressedThisFrame) KeyDown("Space");
        }

        void KeyDown(string c)
        {
            if (c == "Escape")
            {
                if (demo != null) { CloseDemo(); return; }
                if (helpOpen) { CloseHelp(); return; }
                if (settingsOpen) { CloseSettings(); return; }
                if (screen == Screen.Play) { screen = Screen.Pause; focus = 0; return; }
                if (screen == Screen.Pause) { screen = Screen.Play; focus = 0; return; }
                if (screen == Screen.Lobby) { ToTitle(); return; }
                if (screen == Screen.Result) { screen = Screen.Lobby; focus = 0; return; }
                return;
            }
            if (!MenuMode())
            {
                if (c == "ArrowLeft") KeyStep(-1);
                else if (c == "ArrowRight") KeyStep(1);
                else if (c == "KeyQ") KeyParry(-1);
                else if (c == "KeyE") KeyParry(1);
                return;
            }
            var bs = GetButtons();
            if (bs.Count == 0) return;
            if (c == "ArrowUp" || c == "ArrowLeft") focus = (focus + bs.Count - 1) % bs.Count;
            else if (c == "ArrowDown" || c == "ArrowRight" || c == "Tab") focus = (focus + 1) % bs.Count;
            else if (c == "Enter" || c == "NumpadEnter" || c == "Space")
            {
                if (screen == Screen.Result && resultLock > 0) return;
                Activate(bs[focus % bs.Count]);
            }
        }

        void KeyStep(int dir) { padUntil[dir < 0 ? 0 : 1] = time + 0.12; if (run != null && !run.Ended && screen == Screen.Play) run.Step(dir); }
        void KeyParry(int side) { padUntil[side < 0 ? 2 : 3] = time + 0.12; if (run != null && !run.Ended && screen == Screen.Play) run.Parry(side); }

        Vector2 LogicalPos(Vector2 m) => new Vector2((m.x - viewport.x) / scale, (viewport.yMax - m.y) / scale);
        int HitButton(Vector2 p)
        {
            var bs = GetButtons();
            for (int i = 0; i < bs.Count; i++) { var b = bs[i]; if (p.x >= b.x && p.x <= b.x + b.w && p.y >= b.y && p.y <= b.y + b.h) return i; }
            return -1;
        }
        void HandleMouse()
        {
            var ms = Mouse.current;
            if (ms == null) return;
            var pos = ms.position.ReadValue();
            if (pos != lastMouse)
            {
                lastMouse = pos;
                if (MenuMode()) { int i = HitButton(LogicalPos(pos)); if (i >= 0) focus = i; }
            }
            if (ms.leftButton.wasPressedThisFrame && MenuMode())
            {
                int i = HitButton(LogicalPos(pos));
                if (i >= 0)
                {
                    focus = i;
                    if (screen == Screen.Result && resultLock > 0) return;
                    Activate(GetButtons()[i]);
                }
            }
        }
        // 웹 visibilitychange: 탭이 숨겨질 때(창을 내렸을 때)만 일시정지한다. 다른 창을 눌러 초점만 잃으면 웹처럼 계속 돈다
        void OnApplicationPause(bool p) { if (p && screen == Screen.Play) { screen = Screen.Pause; focus = 0; } }
        void OnApplicationFocus(bool has) { if (!has && run != null && !run.Bot) run.held[0] = run.held[1] = false; }   // 웹 blur: 누른 키를 뗀 것으로

        // ================================================================ 그리기 도구 (웹 rect/frameRect/txt/drawSprite/drawImg)
        static float JsRound(double v) => (float)Math.Floor(v + 0.5);
        static double Clamp(double v, double a, double b) => Math.Max(a, Math.Min(b, v));
        static double Lerp(double a, double b, double t) => a + (b - a) * t;
        static string Num(double v) => v.ToString("0.##########", CultureInfo.InvariantCulture);   // JS 숫자 → 글자

        void Rect(double x, double y, double w, double h, Color c, double? a = null) => cv.Rect((float)x, (float)y, (float)w, (float)h, c, a.HasValue ? (float?)a.Value : null);
        void Rect(double x, double y, double w, double h, string c, double? a = null) => Rect(x, y, w, h, Hex(c), a);
        void FrameRect(double x, double y, double w, double h, Color c) => cv.FrameRect((float)x, (float)y, (float)w, (float)h, c);
        void FrameRect(double x, double y, double w, double h, string c) => FrameRect(x, y, w, h, Hex(c));

        struct TO { public int size; public bool bold, noShadow; public WebCanvas.Align align; public WebCanvas.Base bas; public Color? color; }
        static TO T(int size, string align = "left", string bas = "top", bool bold = false, Color? color = null, bool shadow = true) => new TO
        {
            size = size, bold = bold, noShadow = !shadow, color = color,
            align = align == "center" ? WebCanvas.Align.Center : align == "right" ? WebCanvas.Align.Right : WebCanvas.Align.Left,
            bas = bas == "middle" ? WebCanvas.Base.Middle : WebCanvas.Base.Top,
        };
        void TxtO(string s, double x, double y, TO o)
        {   // 테두리 글자 (웹 txtO): 짙은 1px 테두리
            var d = o; d.noShadow = true; d.color = Hex("#0E0F12");
            Txt(s, x - 1, y, d); Txt(s, x + 1, y, d); Txt(s, x, y - 1, d); Txt(s, x, y + 1, d);
            var m = o; m.noShadow = true; Txt(s, x, y, m);
        }
        void Txt(string s, double x, double y, TO o)
        {
            float rx = JsRound(x), ry = JsRound(y);
            int size = o.size > 0 ? o.size : 8;
            if (!o.noShadow) cv.FillText(s, rx + 1, ry + 1, size, o.bold, o.align, o.bas, PalBg);
            cv.FillText(s, rx, ry, size, o.bold, o.align, o.bas, o.color ?? PalText);
        }

        sealed class SO
        {
            public double scale = 1; public double? dw, dh, fw, fh, rot, sx, sy; public string anchor = "bc"; public Color? color; public bool nofb, flip, gray;
        }
        bool DrawSprite(string id, string variant, string anim, double t, double x, double y, SO o)
        {
            o = o ?? new SO();
            double sc = o.scale;
            var sheet = catalog ? catalog.GetSheet(id) : null;
            bool usable = sheet != null && sheet.frameW > 0 && sheet.frameH > 0;
            double dw, dh;
            if (usable) { dw = o.dw ?? sheet.frameW * sc / PX; dh = o.dh ?? sheet.frameH * sc / PX; }
            else { dw = o.dw ?? (o.fw ?? 10) * sc; dh = o.dh ?? (o.fh ?? 10) * sc; }
            double dx = x, dy = y;
            if (o.anchor == "bc") { dx = x - dw / 2; dy = y - dh; }
            else if (o.anchor == "c") { dx = x - dw / 2; dy = y - dh / 2; }
            else if (o.anchor == "tc") { dx = x - dw / 2; }
            dx = JsRound(dx * PX) / PX; dy = JsRound(dy * PX) / PX;
            if (!usable)
            {
                if (!o.nofb) cv.Fill((float)dx, (float)dy, JsRound(dw), JsRound(dh), o.color ?? PalGray);
                return false;
            }
            var spr = catalog.Get(id, anim, variant, t);
            if ((o.rot.HasValue && o.rot.Value != 0) || o.sx.HasValue || o.sy.HasValue || o.gray)
            {
                // 회전·눌림·필터: 그림 가운데를 기준으로 돌리고 줄인다
                cv.Save(); cv.Translate((float)(dx + dw / 2), (float)(dy + dh / 2)); cv.Rotate((float)(o.rot ?? 0));
                cv.Scale((float)((o.flip ? -1 : 1) * (o.sx ?? 1)), (float)(o.sy ?? 1));
                if (o.gray) cv.grayFilter = true;
                cv.DrawImage(spr, (float)(-dw / 2), (float)(-dh / 2), (float)dw, (float)dh);
                cv.Restore();
                return true;
            }
            if (o.flip)
            {
                cv.Save(); cv.Translate((float)(dx + dw), (float)dy); cv.Scale(-1, 1);
                cv.DrawImage(spr, 0, 0, (float)dw, (float)dh);
                cv.Restore();
            }
            else cv.DrawImage(spr, (float)dx, (float)dy, (float)dw, (float)dh);
            return true;
        }
        bool DrawImg(string id, double x, double y, double w, double h, Color? fallback)
        {
            var s = catalog ? catalog.GetImage(id) : null;
            if (s) { cv.DrawImage(s, (float)x, (float)y, (float)w, (float)h); return true; }
            if (fallback.HasValue) cv.Fill((float)x, (float)y, (float)w, (float)h, fallback.Value);
            return false;
        }

        void DrawButtons()
        {
            var bs = GetButtons();
            if (focus >= bs.Count) focus = 0;
            for (int i = 0; i < bs.Count; i++)
            {
                var b = bs[i]; bool f = i == focus;
                if (b.icon != null)   // 아이콘만 있는 버튼 (타이틀의 톱니바퀴)
                {
                    Rect(b.x, b.y, b.w, b.h, PalBg, f ? 0.9 : 0.6);
                    FrameRect(b.x, b.y, b.w, b.h, f ? PalText : PalGray);
                    if (!DrawSprite("hud_icons", "default", b.icon, 0, b.x + (b.w - 12) / 2, b.y + (b.h - 12) / 2, new SO { anchor = "tl", dw = 12, dh = 12, nofb = true }))
                        Txt("설정", b.x + b.w / 2, b.y + b.h / 2, T(6, "center", "middle"));
                    continue;
                }
                bool red = b.tone == "red";
                Rect(b.x, b.y, b.w, b.h, red ? (f ? Hex("#B84545") : PalRed) : b.off ? Hex("#4A4E55") : (f ? PalGreen : PalGray));   // 꺼짐은 고른 때도 회색
                FrameRect(b.x, b.y, b.w, b.h, f ? PalText : PalBg);
                if (red) FrameRect(b.x + 1, b.y + 1, b.w - 2, b.h - 2, "#6E2626");
                Txt(b.label, b.x + b.w / 2, b.y + b.h / 2 + 0.5, T(b.size > 0 ? b.size : 8, "center", "middle", false, b.off ? Hex("#E8E6DF") : f && !red ? PalBg : PalText, !f || red));
            }
        }
        void DrawHeart(double x, double y, bool full)
        {
            bool ok = DrawSprite("hud_icons", "default", full ? "heart" : "heart_empty", 0, x, y, new SO { anchor = "tl", dw = 12, dh = 12, nofb = true });
            if (!ok) Rect(x, y, 10, 10, full ? PalRed : PalGray, full ? 1 : 0.5);
        }
        void DrawBadgeIcon(double x, double y, double size)
        {
            bool ok = DrawSprite("hud_icons", "default", "badge", 0, x, y, new SO { anchor = "tl", dw = size, dh = size, nofb = true });
            if (!ok) Rect(x, y, size, size, PalGreen);
        }

        // ================================================================ 05-1 타이틀
        void DrawCommuters()
        {
            for (int i = 0; i < C.commuters; i++)
            {
                int dir = i % 3 == 1 ? -1 : 1; double v = 9 + ((i * 7) % 5) * 1.6, span = W + 28;
                double x = (((20 + i * 64 + (i % 2) * 17 + dir * v * time) % span) + span) % span - 14;
                double cat = time * (v / 11) + i * 0.37;
                DrawSprite("commuter", "default", "walk", cat, x, 179 - Math.Abs(Math.Sin(cat * 7)) * 1.5, new SO { fw = 10, fh = 20, nofb = true, flip = dir < 0 });   // 걸으며 들썩
            }
        }
        void DrawTitle()
        {
            bool titleOk = DrawImg("title_bg", 0, 0, W, H, PalBg);
            DrawCommuters();
            if (!titleOk)
            {
                // 어두운 빌딩 숲 + 가운데 본사 꼭대기만 불빛
                int[] hs = { 60, 90, 50, 110, 70, 130, 55, 95, 65 };
                for (int i = 0; i < hs.Length; i++) Rect(i * 36 - 4, H - hs[i], 30, hs[i], PalGray, 0.35);
                Rect(136, 40, 48, 140, PalGray, 0.6);
                Rect(150, 52, 20, 10, PalText);
            }
            var lg = catalog ? catalog.GetImage("logo") : null;
            if (lg)
            {
                double iw = lg.rect.width, ih = lg.rect.height;
                double s = Math.Min(1.0 / PX, Math.Min(280 / iw, 70 / ih));
                cv.DrawImage(lg, JsRound((W - iw * s) / 2), 34, (float)(iw * s), (float)(ih * s));   // 05-1 컨셉: 본사 앞 가운데 명판
            }
            else
            {
                Rect(50, 20, 220, 48, PalBg, 0.8);
                FrameRect(50, 20, 220, 48, PalRed);
                Txt("승급의 계단", 160, 30, T(26, "center", bold: true));
                Txt("구조조정까지 D-30", 160, 58, T(8, "center", color: PalGreen));
            }
            DrawButtons();
            Txt("v0.1 PoC · 승급의 계단 제작팀", 160, 172, T(6, "center", color: PalGray));
        }

        // ================================================================ 05-2 로비
        void DrawIdCard(double x, double y, int rIdx)   // 05-2 요소 3: 목걸이(올리브 끈)에 걸린 사원증 카드
        {
            string rank = C.rankNames[rIdx];
            Rect(x + 27, 0, 4, y - 6, "#6E7A49"); Rect(x + 43, 0, 4, y - 6, "#6E7A49");             // 목걸이 끈
            Rect(x + 28, 0, 1, y - 6, PalGreen); Rect(x + 44, 0, 1, y - 6, PalGreen);
            Rect(x + 29, y - 9, 16, 12, "#9AA0A8"); FrameRect(x + 29, y - 9, 16, 12, "#44484E");    // 집게
            Rect(x, y, 74, 108, PalText); FrameRect(x, y, 74, 108, PalGray);
            Rect(x + 1, y + 1, 72, 13, PalGreen); Rect(x + 30, y + 5, 14, 3, "#4E5733");             // 올리브 머리띠, 줄 구멍
            Rect(x + 15, y + 19, 44, 48, "#3A3D43"); Rect(x + 18, y + 22, 38, 42, "#9AA0A8");        // 사진
            DrawSprite("player", rank, "idle", time, x + 37, y + 64, new SO { scale = 1.6, fw = 16, fh = 28, color = RankColors[rIdx] });
            string[,] rows = { { "NAME", "○○○" }, { "TITLE", rank }, { "DEPARTMENT", "총무팀" } };
            for (int i = 0; i < 3; i++)
            {
                double yy = y + 73 + i * 11;
                Txt(rows[i, 0], x + 5, yy, T(5, bold: true, color: Hex("#2A2C31"), shadow: false));
                Txt(rows[i, 1], x + (rows[i, 0].Length > 5 ? 44 : 28), yy, T(5, color: PalGray, shadow: false));
                Rect(x + 4, yy + 8, 66, 1, "#9AA0A8", 0.6);
            }
        }
        // 로비 사원 (웹 lobbyWalker·drawLobbyStaff): 왼쪽 끝 ↔ 오른쪽 끝을 걷고, 끝에서 pause초 머문다
        static (double x, int dir, string anim, double at) LobbyWalker(double t, double x0, double x1, double speed, double pause)
        {
            double walk = (x1 - x0) / speed, P = 2 * (walk + pause), ph = ((t % P) + P) % P;
            if (ph < walk) return (x0 + speed * ph, 1, "walk", ph);
            if (ph < walk + pause) return (x1, -1, ph - walk < pause * 0.6 ? "coffee" : "idle", ph - walk);
            if (ph < 2 * walk + pause) { double q = ph - walk - pause; return (x1 - speed * q, -1, "walk", q); }
            return (x0, 1, "idle", ph - 2 * walk - pause);
        }
        void DrawLobbyStaff()
        {
            DrawSprite("staff", "b", "type", time, 172, 129, new SO { fw = 16, fh = 24, nofb = true });                     // 의자에 앉아 타자 (위몸만)
            DrawSprite("staff", "c", "phone", time, 229, 132, new SO { fw = 16, fh = 24, nofb = true });                    // 서류함 옆에서 전화
            DrawSprite("staff", "d", time % 6 < 3.5 ? "coffee" : "idle", time, 113, 132, new SO { fw = 16, fh = 24, nofb = true });   // 화분 옆에서 커피
            var w = LobbyWalker(time + 2, 96, 236, 14, 3);                                                                  // 바닥을 오가는 사원
            DrawSprite("staff", "a", w.anim, w.at, JsRound(w.x), 148, new SO { fw = 16, fh = 24, nofb = true, flip = w.dir < 0 });
        }
        void DrawLobby()
        {
            // 컨셉 05-2: 형광등 천장의 파티션 사무실(배경 bg_lobby) 위에 층 표시, D-30, 사원증, 화이트보드 기록, 업무 시작
            if (!DrawImg("bg_lobby", 0, 0, W, H, null)) { DrawImg("bg_zone1", 0, 0, W, H, PalBg); Rect(0, 0, W, H, PalBg, 0.5); }
            DrawLobbyStaff();                                                          // 사무실 사원들 (움직이는 로비)
            int bestFloor = SaveData.BestFloor, rIdx = 0;
            foreach (var f in C.promoteFloors) if (bestFloor >= f) rIdx++;
            rIdx = Math.Min(rIdx, C.rankNames.Length - 1);
            Rect(124, 3, 72, 13, PalBg, 0.92); FrameRect(124, 3, 72, 13, "#9AA0A8");                 // 1 층·직급
            Txt("1F", 148, 9.5, T(8, "center", "middle", true));
            Txt(C.rankNames[0], 172, 9.5, T(6, "center", "middle", color: Hex("#9AA0A8")));
            Rect(248, 5, 64, 18, PalRed); FrameRect(248, 5, 64, 18, PalText); FrameRect(249, 6, 62, 16, "#6E2626");   // 2 D-30
            Txt("D-" + Num(D(C.startDays)), 280, 14.5, T(11, "center", "middle", true));
            DrawIdCard(12, 38, rIdx);                                                                // 3 사원증
            Rect(244, 80, 68, 22, PalBg, 0.9); FrameRect(244, 80, 68, 22, PalGray);                  // 5 화이트보드 위 기록
            Txt("최고 " + bestFloor + "F", 278, 87, T(7, "center", "middle", true));
            Txt("무한 최고 " + SaveData.BestEndless + "F · 랭킹 오프라인", 278, 96, T(5, "center", "middle", color: Hex("#9AA0A8")));
            DrawButtons();                                                                           // 4 업무 시작
        }

        // ================================================================ 05-3 플레이
        // 위협 위 표시(!, ×, ↺, 꾹!, …)를 보일지: 10층까지만. 연습 모드와 장애물 보기에서는 늘 보인다 (웹 showMarks)
        bool ShowMarks() => demo != null || run.Mode == GameMode.Practice || run.floor <= 10;
        static readonly string[] ParryVar = { "", "2", "3" };   // 막기 동작: 곧게 막기, 두 손 밀기, 위로 쳐내기 (웹 PARRY_VAR)
        string PlayerAnim()
        {
            var r = run; string s = "idle";
            if (r.Ended) s = r.ended.promoted ? "proud" : "hit";
            else if (r.hitTimer > 0) s = "hit";
            else if (r.time - r.parryHitT < 0.17) s = (r.parryHitSide < 0 ? "parry_hit_left" : "parry_hit_right") + ParryVar[r.parryVar];   // 막기 성공: 손이 번쩍 → 몸이 밀림
            else if (r.time >= r.parryStart[0] && r.time < r.parryEnd[0]) s = "parry_left" + ParryVar[r.parryVar];
            else if (r.time >= r.parryStart[1] && r.time < r.parryEnd[1]) s = "parry_right" + ParryVar[r.parryVar];
            else if (r.time - r.downT < 0.3) s = "down";                       // 밀려 내려가기·떨어지기: 웅크려 내려 딛기
            else if (r.stepTimer > 0) s = r.steps % 2 != 0 ? "climb_r" : "climb_l";   // 칸마다 무릎을 번갈아 든다
            if (animState != s) { animState = s; animStart = r.time; }
            return s;
        }

        double PlayerScreenX() => run.PlayerScreenX;

        void DrawPlayWorld()
        {
            var r = run;
            int zone = StairLayout.ZoneOf(r.floor); bool practice = r.Mode == GameMode.Practice;
            double stepH = D(C.stepH), stepW = D(C.stepW);
            // 연습 모드: 계단 없는 평범한 사무실(로비 배경), 플레이어는 사무실 바닥에 선다
            if (practice)
            {   // 옆에서 본 사무실(bg_practice): 가구는 플레이어 크기, 발 높이에 바닥이 있어 경쟁 사원도 그 위를 걷는다
                if (!DrawImg("bg_practice", 0, 0, W, H, null)) { DrawImg("bg_lobby", 0, 0, W, H, PalBg); Rect(0, PLAYER_Y + 2, W, H, "#3A3D42"); }
                (int x, string n)[] props = { (30, "cooler"), (96, "plant"), (228, "boxes"), (296, "cabinet") };
                foreach (var pr in props) DrawSprite("props", "default", pr.n, 0, pr.x, PLAYER_Y + 2, new SO { anchor = "bc", fw = 16, fh = 24, nofb = true });
            }
            else DrawImg("bg_zone" + zone, 0, 0, W, H, PalBg);
            if (!practice && !(catalog && catalog.GetImage("bg_zone" + zone)))
            {
                // 아래로 흐르는 가로줄로 위로 오르는 느낌
                double off = (-r.camY) % 24;
                for (double y = -24; y < H + 24; y += 24) Rect(0, y + off, W, 1, PalGray, 0.25);
            }
            Rect(0, 0, W, H, PalBg, 0.15);
            // 상사의 눈 (배경): 눈은 보이지 않는다. 예고 5초 동안 화면이 붉어지고 먼 배경에 불꽃 하나가 천천히 나타났다 사라졌다 하며
            // (상사의 눈인지 아닌지 모르게), 훑을 때는 그 불꽃에서 거대한 레이저가 부채꼴로 퍼져 화면 전체를 위에서 아래로 쓸어내린다
            var gz = r.Gaze(); double gw = D(C.gazeWidth);
            if (gz.active) DrawDecoyGlints();                                   // 61층부터: 배경 곳곳의 가짜 불꽃 (진짜와 같은 모양·박자)
            if (gz.warn || gz.sweep)
            {
                double sx0 = RunSimulation.GazeSrcX(gz.cycle), sy0 = RunSimulation.GazeSrcY;
                if (gz.warn)
                {
                    double k = 1 - gz.left / D(C.gazeWarn); bool late = gz.left < 1.5;
                    Rect(0, 0, W, H, "#C0302E", 0.12 * k);
                    double e = D(C.gazeWarn) - gz.left, fade = 0.5 - 0.5 * Math.Cos(e * 2 * Math.PI / 1.25);   // 불꽃 하나: 1.25초마다 천천히 나타났다 사라짐
                    DrawGlint(sx0, sy0, 2 + (late ? 2 : 0) + fade * 1.5, fade * Math.Min(1, e / 0.6));
                }
                else
                {
                    double half = gw / 2, top = gz.y - half;
                    Rect(0, 0, W, H, "#C0302E", 0.08);
                    for (double yy = sy0; yy < top; yy += 3)
                    {   // 불꽃에서 띠까지 부채꼴로 퍼지는 빛
                        double t = (yy - sy0) / Math.Max(1, top - sy0), w = Lerp(14, W * 2.4, t);
                        Rect(sx0 - w / 2, yy, w, 3, "#C0302E", 0.2); Rect(sx0 - w * 0.12, yy, w * 0.24, 3, "#FF5A4E", 0.3);
                    }
                    Rect(0, top - 6, W, 6, "#C0302E", 0.14); Rect(0, top + gw, W, 6, "#C0302E", 0.14);   // 바깥 번짐
                    Rect(0, top, W, gw, "#C0302E", 0.36);
                    Rect(0, gz.y - 6, W, 12, "#FF5A4E", 0.45); Rect(0, gz.y - 3, W, 6, "#FFB0A0", 0.6); Rect(0, gz.y - 1, W, 2, "#FFFFFF", 0.9);   // 뜨거운 하얀 심
                    DrawGlint(sx0, sy0, 10, 1);
                }
            }

            if (!practice)
            {   // ---- 계단, 층 바닥, 소품, 난간 (연습 모드에는 없다)
                int spf = C.stairsPerFloor;
                string RAIL = new[] { "#4E5733", "#44484E", "#5C4F3B", "#5E2323", "#8A6A2E" }[zone - 1];
                string[] PROPS = { "cooler", "boxes", "plant", "cabinet" };
                // 층 바닥(콘크리트 슬래브)과 그 위 소품: 층이 바뀌는 칸 높이에 화면 끝까지 깐다
                for (int i = (int)Math.Floor((r.steps - 6) / (double)spf) * spf; i <= r.steps + 14; i += spf)
                {
                    if (i <= 0) continue;
                    double sy = PLAYER_Y + (-i * stepH - r.camY);
                    if (sy > H + 10 || sy < -40) continue;
                    uint h = (uint)((ulong)i * 2654435761UL);
                    double cx = 160 + (r.Stairs.StairX(i) - r.camX);
                    for (int k = 0; k < 2; k++)
                    {
                        int side = k == 0 ? -1 : 1;
                        double px = cx + side * (64 + ((h >> (k * 8)) % 50));
                        DrawSprite("props", "default", PROPS[(h >> (16 + k * 4)) % PROPS.Length], 0, px, sy, new SO { anchor = "bc", fw = 16, fh = 24, nofb = true });
                    }
                    Rect(0, sy, W, 7, "#4E5259"); Rect(0, sy, W, 1, "#7B8088"); Rect(0, sy + 7, W, 2, "#141518", 0.55);
                }
                // 계단 받침(stringer): 디딤판 아래를 계단 기울기대로 비스듬히 깎아, 아래가 뚫린 계단처럼 보이게 한다 (컨셉 05-3)
                double tileW = stepW * 2, tileH = stepH;
                string[] STR = { "#3A4226", "#33363C", "#463A2B", "#461A1A", "#5E4820" }, STR_EDGE = { "#2C3320", "#26282C", "#352C20", "#341313", "#463618" };
                for (int i = r.steps + 14; i >= Math.Max(0, r.steps - 6); i--)
                {
                    double sx = 160 + (r.Stairs.StairX(i) - r.camX), sy = PLAYER_Y + (-i * stepH - r.camY);
                    if (sy > H + 10 || sy < -30) continue;
                    if (r.Stairs.IsVanish(i) && r.Vanish().gone) continue;   // 사라진 칸은 받침도 없다
                    int d = Math.Sign(r.Stairs.StairX(i + 1) - r.Stairs.StairX(i)); if (d == 0) d = 1;
                    double half = stepW / 2;
                    for (int c = 0; c < half * 2 * PX; c++)
                    {
                        double x = sx - half + c / (double)PX, u = JsRound((tileH * 0.6 - d * (x + 0.5 / PX - sx) * (tileH / (half * 2))) * PX) / PX;
                        Rect(x, sy + tileH, 1.0 / PX, u, STR[zone - 1]);
                        Rect(x, sy + tileH + u - 1.0 / PX, 1.0 / PX, 1.0 / PX, STR_EDGE[zone - 1]);
                    }
                }
                // 계단 디딤판
                var sdef = catalog ? catalog.GetSheet("stairs") : null;
                bool hasCrack = sdef != null && sdef.anims.Exists(a => a.name == "cracked"), hasWet = sdef != null && sdef.anims.Exists(a => a.name == "wet");
                var floorLabels = new List<(string, double, double)>();
                for (int i = r.steps + 14; i >= Math.Max(0, r.steps - 6); i--)
                {
                    double sx = 160 + (r.Stairs.StairX(i) - r.camX), sy = PLAYER_Y + (-i * stepH - r.camY);
                    if (sy > H + 10 || sy < -10) continue;
                    bool landing = i % spf == 0;
                    // 금 간 계단: 위에 서 있으면 시간이 갈수록 흔들리고 붉어지며 부스러기가 떨어진다
                    bool cracked = r.Stairs.IsCracked(i);
                    double shake = cracked && i == r.steps ? r.crackT / D(C.crackTime) : 0;
                    double jx = shake != 0 ? JsRound(Math.Sin(r.time * 70) * shake * 1.5 * PX) / PX : 0;
                    bool wet = r.Stairs.IsWet(i);
                    // 사라지는 칸: 없어진 동안은 윤곽만, 없어지기 1초 전부터 깜박인다. 윗면의 하늘색 선이 표시
                    bool van = r.Stairs.IsVanish(i); var vst = r.Vanish();
                    if (van && vst.gone)
                    {
                        Rect(sx - stepW / 2, sy, stepW, tileH, "#9FE0FF", 0.12);   // 없어진 칸: 옅게 채운 유령
                        Rect(sx - stepW / 2, sy, stepW, 1, "#9FE0FF", 0.5); Rect(sx - stepW / 2, sy + tileH - 1, stepW, 1, "#9FE0FF", 0.25);
                        continue;
                    }
                    if (van && vst.warn && Math.Floor(time * 12) % 2 == 0) cv.globalAlpha = 0.3f;
                    bool ok = DrawSprite("stairs", "zone" + zone, cracked && hasCrack ? "cracked" : wet && hasWet ? "wet" : "tile", time, sx + jx, sy, new SO { anchor = "tc", dw = tileW, dh = tileH, nofb = true });
                    cv.globalAlpha = 1;
                    if (van) Rect(sx - stepW / 2, sy, stepW, 1, "#9FE0FF", 0.8);
                    if (r.Stairs.IsReverse(i))
                    {   // 거꾸로 칸: 보라색 윗면과 엇갈린 화살표 (웹과 같다)
                        double rx0 = sx - stepW / 2, rcx = sx, rcy = sy + 6;
                        Rect(rx0, sy, stepW, 2, "#B07CE0", 0.9); Rect(rx0, sy + 2, stepW, 1, "#6E4A99", 0.7);
                        Rect(rcx - 5, rcy - 2, 7, 1, "#D4A5FF"); Rect(rcx - 5, rcy - 3, 1, 3, "#D4A5FF");
                        Rect(rcx - 2, rcy + 1, 7, 1, "#D4A5FF"); Rect(rcx + 4, rcy, 1, 3, "#D4A5FF");
                    }
                    if (r.Stairs.IsEscalator(i))
                    {   // 내려가는 에스컬레이터: 쇠빛 디딤판 위로 홈이 내려가는 쪽으로 흐르고, 아래 화살표
                        double ex0 = sx - stepW / 2, ew = stepW; int down = -Math.Sign(r.Stairs.StairX(i) - r.Stairs.StairX(i - 1)); if (down == 0) down = -1;
                        Rect(ex0, sy, ew, 3, "#9AA0A8", 0.6);
                        for (int g = 0; g < 5; g++) { double gx = ((g * 4.4 + down * r.time * 14) % ew + ew) % ew; Rect(ex0 + gx, sy, 1, 3, "#2A2C31", 0.85); }
                        Rect(ex0, sy + 3, ew, 1, "#E3B65C", 0.7);
                        foreach (double ax in new[] { ex0 + ew * 0.3, ex0 + ew * 0.7 }) { Rect(ax - 2, sy + 6, 5, 1, "#E3B65C", 0.9); Rect(ax - 1, sy + 7, 3, 1, "#E3B65C", 0.9); Rect(ax, sy + 8, 1, 1, "#E3B65C", 0.9); }
                    }
                    if (wet)
                    {   // 전기: 예고 때는 전선 끝에서 불꽃이 튀고, 흐를 때는 물 위로 번개가 친다
                        var shs = r.Stairs.Shock(r.time); double x0 = sx - stepW / 2, w = stepW;
                        if (shs.live)
                        {
                            Rect(x0, sy - 2, w, 4, "#F2E678", 0.35);
                            string c = Math.Floor(r.time * 30) % 2 != 0 ? "#FFF6B0" : "#9FE0FF";
                            for (double x = 0; x < w; x += 1.0 / PX) Rect(x0 + x, sy - 1 + JsRound(Math.Sin((x + r.time * 40) * 3.1) * 1.5 * PX) / PX, 1.0 / PX, 1.0 / PX, c);
                            Rect(x0 + ((r.time * 97) % w), sy - 6, 1.0 / PX, 5, c); Rect(x0 + ((r.time * 53 + 7) % w), sy - 5, 1.0 / PX, 4, c);
                        }
                        else if (shs.warn && Math.Floor(r.time * 20) % 2 != 0)
                        {
                            Rect(x0 + w * 0.82, sy - 3, 1.0 / PX, 1.0 / PX, "#FFE680"); Rect(x0 + w * 0.86, sy - 4, 1.0 / PX, 1.0 / PX, "#FFE680"); Rect(x0 + w * 0.78, sy - 2, 1.0 / PX, 1.0 / PX, "#FFE680");
                        }
                    }
                    if (shake != 0)
                    {
                        Rect(sx - stepW / 2 + jx, sy, stepW, tileH, PalRed, 0.45 * shake);
                        for (int d = 0; d < 4; d++) Rect(sx - stepW / 2 + ((d * 37 + r.steps * 11) % stepW) + jx, sy + tileH + ((r.time * 60 + d * 9) % 16), 1, 1, "#3A3D43");
                    }
                    if (!ok)
                    {
                        Rect(sx - tileW / 2, sy, tileW, tileH, landing ? PalRed : PalGray);
                        Rect(sx - tileW / 2, sy, tileW, 2, landing ? PalText : PalGreen);
                    }
                    if (landing && i > 0) floorLabels.Add(((i / spf + 1) + "F", sx, sy + 8));
                }
                foreach (var l in floorLabels) { Rect(l.Item2 - 8, l.Item3 - 1, 16, 9, "#141518", 0.55); Txt(l.Item1, l.Item2, l.Item3, T(6, "center", color: PalText)); }   // 층 표시는 칸 위에, 짙은 바탕
                // 난간: 칸마다 기둥, 이웃 칸 기둥끼리 손잡이로 잇는다 (아트 픽셀 단위로 그려 계단처럼 각지게)
                for (int i = Math.Max(0, r.steps - 6); i < r.steps + 14; i++)
                {
                    double ax = 160 + (r.Stairs.StairX(i) - r.camX), ay = PLAYER_Y + (-i * stepH - r.camY);
                    double bx = 160 + (r.Stairs.StairX(i + 1) - r.camX), by = ay - stepH;
                    if (ay > H + 20 || ay < -20) continue;
                    Rect(ax - 0.5, ay - 12, 1, 12, RAIL, 0.9);
                    int n = Math.Max(1, (int)JsRound(Math.Max(Math.Abs(bx - ax), Math.Abs(by - ay)) * PX));
                    for (int s = 0; s <= n; s++) Rect(JsRound((ax + (bx - ax) * s / n) * PX) / PX - 0.5, JsRound((ay - 12 + (by - ay) * s / n) * PX) / PX, 1, 1.0 / PX * 2, RAIL);
                }
            }
            else
            {   // 연습 모드: 발밑 그림자
                Rect(PlayerScreenX() - 9, PLAYER_Y + 1, 18, 2, "#0A0B0D", 0.45);
            }
            // 경쟁 사원 (장식)
            if (C.showRivalCameo && r.floor < C.rivalFromFloor && r.rivalIdx >= r.steps - 3 && r.rivalIdx <= r.steps + 14)
            {
                double sx = 160 + (r.Stairs.StairX(r.rivalIdx) - r.camX);
                double sy = PLAYER_Y + (-r.rivalIdx * stepH - r.camY);
                cv.globalAlpha = 0.75f;
                DrawSprite("rival", "default", "idle", time, sx, sy + 1, new SO { scale = ManScale, fw = 14 * ManScale, fh = 26 * ManScale, color = PalRed, flip = r.Stairs.DirAt(r.rivalIdx) < 0 });   // 계단 아래 끝에 서 있는 경쟁 사원 (서 있기)
                cv.globalAlpha = 1;
            }
            // 플레이어
            double ppx = PlayerScreenX();
            string anim = PlayerAnim();
            var rc = RankColors[r.rankIdx];
            var po = new SO { scale = ManScale, fw = 16 * ManScale, fh = 28 * ManScale, color = rc, flip = r.facing < 0 && anim != "parry_left" && anim != "parry_right" };
            double pdy = 0;
            if (anim == "climb_r" || anim == "climb_l" || anim == "idle")
            {   // 계단을 달려 오르는 몸짓 (막기·맞기 동작은 그대로)
                double since = r.time - r.lastStepT, run = Clamp(1 - (since - 0.14) / 0.4, 0, 1); int mdir = r.Stairs.DirAt(Math.Max(0, r.steps - 1));
                var fx = RunFx(r.stepTimer > 0 ? 1 - r.stepTimer / 0.14 : 0, run, mdir, 28 * ManScale);
                po.sx = fx[1]; po.sy = fx[2]; po.rot = fx[3]; pdy = fx[0];
                if (r.stepTimer > 0)
                {   // 발밑 먼지: 디딘 칸 뒤쪽으로 흩어진다
                    double k = 1 - r.stepTimer / 0.14;
                    for (int j = 0; j < 3; j++) Rect(ppx - mdir * (5 + k * (6 + j * 4)), PLAYER_Y + 1 - k * (2 + j * 2), 2, 2, "#C9CCD1", 0.6 * (1 - k));
                }
            }
            for (int dy = -30; dy <= 0; dy += 2) { double k = (dy + 15) / 17.0, w = Math.Sqrt(Math.Max(0, 1 - k * k)) * 13; Rect(ppx - w, PLAYER_Y + dy, w * 2, 2, "#F2E6D0", 0.06); }   // 플레이어 뒤 은은한 빛
            DrawSprite("player", C.rankNames[r.rankIdx], anim, r.time - animStart, ppx, PLAYER_Y + 2 + pdy, po);
            // 투척물 + 예고. 월드 좌표 -> 화면 좌표 (경쟁 사원 발 위치)
            Func<double, double> SX = qx => 160 + (qx - r.camX);
            Func<double, double> SY = qy => PLAYER_Y + (qy - r.camY) + 1;
            foreach (var l in r.leavers)
            {
                double k = (r.time - l.t0) / 0.5;
                cv.globalAlpha = (float)Clamp(1 - k, 0, 1);
                DrawSprite("rival", "default", "leave", r.time - l.t0, SX(l.wx) + l.side * k * 90, SY(l.wy), new SO { scale = ManScale, fw = 14 * ManScale, fh = 26 * ManScale, color = PalRed, flip = l.side < 0 });
                cv.globalAlpha = 1;
            }
            foreach (var f in r.fallers)   // 계단 장애물에 당한 경쟁 사원: 감전은 하얗게 떨다 사라지고, 떨어지면 돌며 아래로
            {
                double t = r.time - f.t0, k = t / 0.8;
                var o = new SO { scale = ManScale, fw = 14 * ManScale, fh = 26 * ManScale, color = PalRed, flip = f.side < 0 };
                if (f.kind == "shock")
                {
                    cv.globalAlpha = (float)Clamp(1 - k, 0, 1);
                    DrawSprite("rival", "default", "walk", 0, SX(f.wx) + Math.Sin(t * 80) * 1.5, SY(f.wy), o);
                    cv.globalAlpha = 1;
                    if (Math.Floor(t * 20) % 2 != 0) Rect(SX(f.wx) - 8, SY(f.wy) - 26, 16, 26, "#9FE0FF", 0.5 * (1 - k));
                }
                else if (f.kind == "gaze")
                {   // 상사의 눈에 들켰다: 그 자리에 굳어 붉게 깜박이며 사라진다
                    cv.globalAlpha = (float)Clamp(1 - k, 0, 1);
                    DrawSprite("rival", "default", "idle", 0, SX(f.wx), SY(f.wy), o);
                    cv.globalAlpha = 1;
                    if (Math.Floor(t * 12) % 2 == 0) Rect(SX(f.wx) - 8, SY(f.wy) - 26, 16, 26, "#FF5A4A", 0.45 * (1 - k));
                }
                else
                {
                    cv.globalAlpha = (float)Clamp(1 - k * 0.8, 0, 1);
                    o.rot = f.side * t * 6;
                    DrawSprite("rival", "default", "down", t, SX(f.wx) + f.side * 20 * t, SY(f.wy) + 160 * t * t, o);
                    cv.globalAlpha = 1;
                }
            }
            double burst = D(C.suitcaseBurst), warnTime = D(C.warnTime), leap = D(C.rivalLeap);
            foreach (var p in r.threats)
            {
                if (p.IsRival)
                {
                    // 층 바닥 위를 걸어 들어오고, 플레이어가 더 높이 있으면 계단을 따라 쫓아 오른다. 붙으면 플레이어 칸으로 덤벼 친다
                    var rp = r.RivalPos(p); double left = p.impactT - r.time;
                    double rx = SX(rp.x), ry = SY(rp.y), at0 = p.phase == RivalPhase.Stand ? 0 : r.time - p.enterT; string ranim = "walk";
                    if (p.locked && left < leap)
                    {
                        double k = 1 - left / leap;
                        rx = Lerp(rx, ppx + p.side * 12, k); ry = Lerp(ry, PLAYER_Y + 1, k) - Math.Sin(k * Math.PI) * 8; ranim = "attack";
                    }
                    var ro = new SO { scale = ManScale, fw = 14 * ManScale, fh = 26 * ManScale, color = PalRed, flip = rx > ppx };
                    if (!p.locked && (p.phase == RivalPhase.Stand || p.waiting || (p.phase == RivalPhase.Climb && p.ri >= r.steps - 1 - 1e-6))) { ranim = "idle"; at0 = r.time; }   // 서 있기
                    if (r.time - p.downT < 0.3) { ranim = "down"; at0 = r.time - p.downT; }   // 밀려 내려가는 중
                    if (ranim == "walk" && p.phase == RivalPhase.Climb && !p.waiting && p.ri < r.steps - 1 - 1e-6)
                    {   // 계단을 달려 오를 때: 앞으로 기울고 다리를 빨리, 칸마다 낮게 들썩
                        double f = p.ri - Math.Floor(p.ri);
                        ry -= Math.Sin(f * Math.PI) * 2; ro.rot = r.Stairs.DirAt((int)Math.Floor(p.ri)) * 0.15; at0 *= 2; ranim = "climb";
                    }
                    else if (ranim == "walk" && p.phase == RivalPhase.Walk)
                    {   // 층 바닥을 달려올 때: 들썩이며 앞으로 기운다
                        ry -= Math.Abs(Math.Sin(at0 * 16)) * 2; ro.rot = (rx > ppx ? -1 : 1) * 0.1; at0 *= 1.6;
                    }
                    DrawSprite("rival", "default", ranim, at0, rx, ry, ro);
                    if (p.waiting && ShowMarks()) Txt("…", rx + 10, ry - 36, T(9, "center", "middle", true, Hex("#E3B65C")));   // 계단 장애물 앞에서 기다리는 중
                    if (r.time >= p.spawnT && ShowMarks())
                    {
                        bool wk = DrawSprite("warning", "default", "blink", r.time - p.spawnT, rx, ry - 35, new SO { anchor = "c", fw = 8, fh = 12, nofb = true });
                        if (!wk && Math.Floor(time * 8) % 2 == 0) { Rect(rx - 5, ry - 42, 10, 14, PalRed); Txt("!", rx, ry - 35, T(10, "center", "middle", true, shadow: false)); }
                    }
                    continue;
                }
                double ax = ppx, ay = PLAYER_Y;   // 투척물은 플레이어를 향한다 (의자만 생길 때의 계단 줄을 따라 정해진 길로)
                if (p.type == ThreatType.Airplane) { DrawAirplanes(p, ax, ay); continue; }
                if (p.type == ThreatType.Envelope) { DrawEnvelopes(p, ax, ay); continue; }
                if (p.type == ThreatType.Chair) { DrawRollingChair(p, ppx); continue; }
                if (r.time < p.spawnT) continue;                           // 모니터: 아직 형광등이 깜박이는 중
                double prog = Clamp((r.time - p.spawnT) / Math.Max(0.01, p.impactT - p.spawnT), 0, 1), tt = r.time - p.spawnT;   // 날아오는 시간(돌아오는 스테이플러는 0.9초)
                double tx = ax + p.side * 8, ty = ay - 16;                 // 맞는 자리 (정해진 자리)
                double x, y;
                if (p.bounce)
                {   // 포스트잇: 튕겨 나갔다가 같은 쪽으로 돌아온다
                    x = tx + p.side * 64 * Math.Sin(prog * Math.PI); y = ty - 26 * Math.Sin(prog * Math.PI);
                }
                else if (p.type == ThreatType.Suitcase)
                {   // 아래에서 솟아 날아오다가 도중에 터진다
                    double x0 = ax + p.side * 80, y0 = H + 14, bx = ax + p.side * 40, by = ay - 34;
                    double u = Math.Min(1, prog / (1 - burst / warnTime));
                    x = Lerp(x0, bx, u); y = Lerp(y0, by, u) - Math.Sin(Math.Pow(u, 0.7) * Math.PI) * 56;
                }
                else if (p.type == ThreatType.Phone)
                {   // 화면 아래에서 솟아 꼭대기에서 잠깐 멈칫한 뒤 중력처럼 빨라지며 떨어진다
                    double x0 = ax + p.side * 80, y0 = H + 14;
                    x = Lerp(x0, tx, prog); y = Lerp(y0, ty, prog) - Math.Sin(Math.Pow(prog, 0.7) * Math.PI) * 84;
                }
                else if (p.type == ThreatType.Monitor)
                {   // 위층 창이 깨지며 70도 사선으로 떨어진다 (처음엔 느리고 중력처럼 빨라짐)
                    double y0 = ty - 96, x0 = tx + p.side * (ty - y0) / Math.Tan(D(C.monitorAngle) * Math.PI / 180), e = prog * (0.25 + 0.75 * prog);
                    x = Lerp(x0, tx, e); y = Lerp(y0, ty, e);
                }
                else if (p.type == ThreatType.Paper)
                {   // 던진 쪽에서 머리 위로 넘어가, 반대쪽에서 돌아와 맞는다
                    double x0 = p.from < 0 ? -16 : W + 16, mx = ax + p.side * 36, my = ay - 36;
                    if (prog < 0.65) { double u = prog / 0.65; x = Lerp(x0, mx, u); y = Lerp(ty, my, u) - Math.Sin(u * Math.PI) * 14; }
                    else { double v = (prog - 0.65) / 0.35; x = Lerp(mx, tx, v); y = Lerp(my, ty, v); }
                }
                else if (p.type == ThreatType.Stapler && p.returned)
                {   // 막힌 스테이플러: 막은 자리에서 뒤로 튕겨 나갔다가 머리 위로 휘어 반대쪽에서 맞는다 (3차 베지어)
                    double u = prog, v = 1 - u, w0 = v * v * v, w1 = 3 * v * v * u, w2 = 3 * v * u * u, w3 = u * u * u;
                    x = w0 * (ax - p.side * 8) + w1 * (ax - p.side * 96) + w2 * (ax + p.side * 84) + w3 * tx;
                    y = w0 * ty + w1 * (ty - 40) + w2 * (ty - 76) + w3 * ty;
                }
                else
                {   // 결재판: 옆에서 날아온다
                    double x0 = p.side < 0 ? -16 : W + 16;
                    x = Lerp(x0, tx, prog); y = ty - Math.Sin(prog * Math.PI) * 6;
                }
                // 회전: 모니터·전화기는 불규칙하게 빙글빙글, 결재판은 납작한 판이 수평으로 도는 것처럼 폭이 줄었다 늘었다 한다
                var so = new SO { anchor = "c", fw = 12, fh = 9, color = PalText, flip = p.side > 0 };
                double at = r.time - p.spawnT;
                if (p.type == ThreatType.Suitcase && r.time >= p.impactT - burst)
                {   // 터졌다: 서류·결재판이 사방으로, 두 장은 플레이어에게
                    double t0 = r.time - (p.impactT - burst);
                    // 열린 가방은 덮개가 젖혀진 채 돌며 아래로 떨어진다
                    DrawSprite("projectiles", "default", "suitcase_open", 0, x + p.side * 30 * t0, y - 30 * t0 + 160 * t0 * t0, new SO { anchor = "c", scale = 1.35, rot = p.ph + t0 * 5, fw = 12, fh = 9, color = Hex("#2E2A28") });
                    for (int j = 0; j < 7; j++)
                    {
                        double ang = p.ph + j * 0.9; bool toMe = j < 2;
                        double vx = toMe ? (tx - x) / burst : Math.Cos(ang) * 120, vy = toMe ? (ty - y) / burst : Math.Sin(ang) * 90 - 40;
                        DrawSprite("projectiles", "default", p.debris, 0, x + vx * t0, y + vy * t0 + (toMe ? 0 : 120 * t0 * t0), new SO { anchor = "c", scale = 0.7, rot = ang + t0 * 9, fw = 8, fh = 6, color = PalText });
                    }
                    if (t0 < 0.08) Rect(x - 10, y - 10, 20, 20, PalText, 0.8 - t0 * 10);   // 터지는 빛
                    continue;
                }
                if (p.type == ThreatType.Monitor || p.type == ThreatType.Phone || p.type == ThreatType.Suitcase || p.type == ThreatType.Stapler) { so.rot = p.ph + p.spin * tt + 0.8 * Math.Sin(p.wob * tt * 3); at = 0; }
                if (p.type == ThreatType.Suitcase) so.scale = 1.35;             // 서류가방은 크게 (서류·결재판과 헷갈리지 않게)
                else if (p.type == ThreatType.Clipboard || p.type == ThreatType.Coffee || p.type == ThreatType.Sticky)
                {
                    double c = Math.Cos(tt * 13 + p.ph); so.sx = (c < 0 ? -1 : 1) * Math.Max(0.14, Math.Abs(c)); so.sy = 0.82; so.rot = 0.12 * p.side; at = 0;
                }
                if (p.type == ThreatType.Stapler) DrawStapler(x, y, so.rot ?? 0, tt, p.side > 0);                                // 스테이플러 (그림 없이 그린다)
                else if (p.type == ThreatType.Sticky) DrawSprite("sticky", StickyColors[Mathf.Clamp(p.hp, 0, 5)], "fly", at, x, y, so);   // 색 = 남은 횟수
                else DrawSprite("projectiles", "default", p.type == ThreatType.Paper ? "document" : p.type.ToString().ToLowerInvariant(), at, x, y, so);
                double wx = Clamp(x, 8, W - 8), wy = y - 16;
                if (p.type == ThreatType.Coffee) { if (ShowMarks()) TxtO("×", wx, wy, T(13, "center", "middle", true, Hex("#FF5A4E"))); continue; }   // 막지 말라는 표시
                if (!ShowMarks()) continue;                                // 10층 위: 위협 위 표시 없음
                bool wk2 = DrawSprite("warning", "default", "blink", r.time - p.spawnT, wx, wy, new SO { anchor = "c", fw = 8, fh = 12, nofb = true });
                if (!wk2 && Math.Floor(time * 8) % 2 == 0)
                {
                    Rect(wx - 5, wy - 7, 10, 14, PalRed);
                    Txt("!", wx, wy, T(10, "center", "middle", true, shadow: false));
                }
                if (p.type == ThreatType.Stapler && p.returned) Txt("↺", wx + 9, wy, T(8, "center", "middle", true, Hex("#E3B65C")));   // 돌아오는 중
            }
            // 효과
            foreach (var s in r.sparks) Rect(s.x, s.y, 2, 2, s.olive ? PalGreen : PalText, 1 - s.t / 0.4);
            foreach (var t in r.texts)
            {
                cv.globalAlpha = (float)(t.t < 0.3 ? 1 : Clamp(1 - (t.t - 0.3) / 0.5, 0, 1));   // 떠오르는 글자: 0.3초 동안 또렷하게, 밝은 색 + 테두리
                TxtO(t.s, t.x, t.y, T(9, "center", bold: true, color: t.color == "green" ? Hex("#C8F06A") : t.color == "red" ? Hex("#FF6A5E") : PalText));
                cv.globalAlpha = 1;
            }
            // 모니터 예고: 형광등이 불규칙하게 깜박인다 (화면이 어두워졌다 밝아짐, 떨어질 쪽 위가 더 깜박임)
            double[] fl = { 0.5, 0.05, 0.38, 0, 0.55, 0.12, 0.3, 0 };
            foreach (var p in r.threats)
            {
                if (p.type != ThreatType.Monitor || p.done || r.time > p.spawnT + 0.12) continue;
                double f = fl[(int)Math.Floor((r.time - p.warnT) * 24) % 8];
                Rect(0, 0, W, H, "#08090B", f);
                Rect(ppx + p.side * 52 - 30, 0, 60, 3, PalText, Math.Max(0, 0.22 - f * 0.35));
            }
            // 상사의 눈 (앞): 시선이 플레이어 위로도 보이게 옅게 한 번 더, 예고 숫자
            if (gz.sweep) { Rect(0, gz.y - gw / 2, W, gw, "#C0302E", 0.14); TxtO("멈추기!", 160, 40, T(8, "center", "middle", true, Hex("#FF8A7A"))); }
            if (r.hurtFlash > 0) Rect(0, 0, W, H, PalRed, Clamp(r.hurtFlash / 0.35, 0, 1) * 0.35);
            // 카메라 플래시: 화면이 하얗게 번쩍이고, 터진 자리에 빛줄기가 남는다 (막을 수 없고 피해도 없다)
            if (r.flashing)
            {
                double k = r.FlashK, fx = r.flashX, fy = r.flashY;
                Rect(0, 0, W, H, "#F5F5F0", 0.85 * k);
                Rect(fx - 14 * k - 1, fy, 28 * k + 2, 1, "#FFFFFF", k); Rect(fx, fy - 14 * k - 1, 1, 28 * k + 2, "#FFFFFF", k);
                Rect(fx - 3, fy - 3, 6, 6, "#FFFFFF", k);
            }
        }

        // 새 장애물 그림 (스프라이트 없이 칸 단위로, 웹 drawGazeEye/drawStapler/drawPlane/drawAirplanes)
        // 웹 MAN_SCALE: 사람 그림 1.5배 (원본 한 픽셀이 화면에서 고르게 3픽셀)
        const double ManScale = 1;   // 사람 그림 크기 (1 = 원래 크기)
        // 웹 runFx: 계단을 달려 오르는 몸짓. [dy, sx, sy, rot]
        static double[] RunFx(double a, double run, int dir, double h)
        {
            double s = a > 0 && a < 1 ? Math.Sin(a * Math.PI) : 0;
            double sy = 1 + 0.05 * s - (a > 0.75 && a < 1 ? 0.06 * Math.Sin((a - 0.75) / 0.25 * Math.PI) : 0);
            return new[] { -s * 2.5 + (1 - sy) * h / 2, 1 + (1 - sy) * 0.8, sy, dir * (0.07 + 0.08 * run) * Math.Max(run, s) };
        }
        static double DecoyHash(double a, double b) { double v = Math.Sin(a * 12.9898 + b * 78.233) * 43758.5453; return v - Math.Floor(v); }
        void DrawDecoyGlints()
        {   // 가짜 불꽃 5개: 저마다 다른 박자로 나타났다 사라지고, 사라질 때마다 자리를 옮긴다 (웹 drawDecoyGlints)
            for (int i = 0; i < 5; i++)
            {
                double P = 1.9 + i * 0.43, e = time + i * 0.71, n = Math.Floor(e / P), u = (e - n * P) / P, fade = Math.Pow(Math.Sin(u * Math.PI), 2);
                DrawGlint(JsRound(16 + DecoyHash(i, n) * 288), JsRound(14 + DecoyHash(i + 7, n) * 80), 2 + fade * 1.5, fade * 0.9);
            }
        }
        void DrawGlint(double x, double y, double sz, double a)
        {   // 반짝임 (네 갈래 빛줄기와 가운데 점)
            double S = JsRound(sz);
            Rect(x - S, y, S * 2 + 1, 1, "#FFD0C8", a); Rect(x, y - S, 1, S * 2 + 1, "#FFD0C8", a);
            Rect(x - 1, y - 1, 3, 3, "#FFFFFF", a); Rect(x - 2, y - 2, 5, 5, "#FF5A4E", a * 0.35);
        }
        void DrawStapler(double x, double y, double rot, double t = 0, bool flip = false)
        {   // 스테이플러 (obstacles.png stapler: 입이 딱딱). 그림이 없으면 사각형으로
            if (DrawSprite("obstacles", "stapler", "snap", t, x, y, new SO { anchor = "c", fw = 12, fh = 12, nofb = true, rot = rot, flip = flip })) return;
            cv.Save(); cv.Translate(JsRound(x), JsRound(y)); cv.Rotate((float)rot);
            Rect(-6, -1, 12, 3, "#3A3D43"); Rect(-6, -3, 10, 2, "#9AA0A8"); Rect(3, -3, 3, 1, "#A13D3D"); Rect(-6, 2, 12, 1, "#2A2C31");
            cv.Restore();
        }
        void DrawChair(double x, double y, double rot, double t = 0, bool flip = false, double sx = 1, double sy = 1)
        {   // 사무실 의자 (obstacles.png chair, 바퀴가 돈다). 그림이 없으면 사각형으로 (웹 drawChair)
            if (DrawSprite("obstacles", "chair", "roll", t, x, y - 0.5, new SO { anchor = "c", fw = 16, fh = 16, nofb = true, rot = rot, sx = sx, sy = sy, flip = flip })) return;
            cv.Save(); cv.Translate(JsRound(x), JsRound(y)); cv.Rotate((float)rot);
            Rect(-5, -11, 3, 9, "#3A4A6A"); Rect(-5, -3, 10, 3, "#3A4A6A"); Rect(-4, -2, 8, 1, "#4E6290");
            Rect(-1, 0, 2, 4, "#9AA0A8"); Rect(-5, 4, 10, 1, "#2A2C31"); Rect(-5, 5, 2, 2, "#1E1F22"); Rect(3, 5, 2, 2, "#1E1F22");
            cv.Restore();
        }
        void DrawWarnMark(double x, double y, Threat p)
        {
            double wx = Clamp(x, 8, W - 8);
            bool wk = DrawSprite("warning", "default", "blink", run.time - p.spawnT, wx, y, new SO { anchor = "c", fw = 8, fh = 12, nofb = true });
            if (!wk && Math.Floor(time * 8) % 2 == 0) { Rect(wx - 5, y - 7, 10, 14, PalRed); Txt("!", wx, y, T(10, "center", "middle", true, shadow: false)); }
        }
        void DrawRollingChair(Threat p, double ppx)
        {   // 튀어 내려오는 의자 (웹 drawRollingChair): 자리는 ChairPos(규칙과 같다). 내려앉을 때 눌리고, 공중에서는 아치를 따라 기운다
            var r = run; int s = p.side;
            var c = r.ChairPos(p, r.time);                                           // 규칙과 같은 자리
            double x = 160 + (c.x - r.camX), y = PLAYER_Y + (c.y - r.camY);
            double rot = -s * 0.3; bool ground = false;
            if (c.phase == RunSimulation.ChairPhase.Fall) rot = -s * (0.3 + c.e * 10);
            else if (c.phase == RunSimulation.ChairPhase.Drop) rot = -s * (0.3 + c.u * 0.6);
            else if (c.phase == RunSimulation.ChairPhase.Hop) { ground = c.u < 0.1 || c.u > 0.94; rot = -s * (0.3 + (c.u - 0.5) * 0.7); }
            DrawChair(x, y, rot, r.time - p.spawnT, s > 0, ground ? 1.16 : 0.95, ground ? 0.82 : 1.06);
            if (ShowMarks() && !p.fallT.HasValue && r.ChairHitT(p).HasValue) DrawWarnMark(x, y - 20, p);   // 나에게 닿을 의자에만 "!"
        }
        void DrawEnvelope(double x, double y)
        {   // 편지 봉투: 흰 봉투, 접힌 날개 (웹 drawEnvelope)
            double X = JsRound(x), Y = JsRound(y);
            Rect(X - 5, Y - 3, 10, 7, "#5A5F66"); Rect(X - 4, Y - 2, 8, 5, "#E8E6DF");
            Rect(X - 4, Y - 2, 2, 1, "#9AA0A8"); Rect(X - 2, Y - 1, 2, 1, "#9AA0A8"); Rect(X, Y - 1, 2, 1, "#9AA0A8"); Rect(X + 2, Y - 2, 2, 1, "#9AA0A8"); Rect(X - 1, Y, 2, 1, "#A13D3D");
        }
        void DrawEnvelopes(Threat p, double ppx, double ppy)   // ppx, ppy: 정해진 자리
        {   // 편지 3연발: 같은 쪽에서 envelopeGap 박자로 한 장씩 (웹 drawEnvelopes)
            var r = run; double dur = Math.Max(0.01, p.impactT - p.spawnT), tx = ppx + p.side * 8, ty = ppy - 16, x0 = p.side < 0 ? -16 : W + 16, gap = D(C.envelopeGap);
            for (int k = p.k; k < C.envelopeCount; k++)
            {
                double t0 = p.spawnT + k * gap;
                if (r.time < t0) continue;
                double u = Clamp((r.time - t0) / dur, 0, 1);
                double ex = Lerp(x0, tx, u), ey = ty - Math.Sin(u * Math.PI) * 14 + (k % 2) * 3, et = r.time - t0;
                for (int j = 1; j <= 3; j++) Rect(ex + p.side * (4 + j * 4) - 1, ey - 1 + (j % 2) * 2, 4, 1, "#E8E6DF", 0.42 - j * 0.12);   // 뒤에 남는 바람 줄
                if (!DrawSprite("obstacles", "envelope", "fly", et + k * 0.13, ex, ey, new SO { anchor = "c", fw = 12, fh = 12, nofb = true, rot = Math.Sin(et * 14 + k) * 0.22, flip = p.side > 0 }))
                    DrawEnvelope(ex, ey);                                          // 돌며 펄럭이고 흔들린다
            }
            if (r.time >= p.spawnT && ShowMarks())
            {
                double u = Clamp((r.time - p.spawnT) / dur, 0, 1), wx = Clamp(Lerp(x0, tx, u), 8, W - 8), wy = ty - 28;
                DrawWarnMark(wx, wy, p);
                Txt("×" + (C.envelopeCount - p.k), wx + 10, wy, T(7, "center", "middle", true, Hex("#E3B65C")));
            }
        }
        void DrawPlane(double x, double y, int side, double t = 0)
        {   // 종이비행기 (obstacles.png plane: 날개가 파닥). 그림이 없으면 사각형으로
            if (DrawSprite("obstacles", "plane", "flap", t, x, y, new SO { anchor = "c", fw = 10, fh = 10, nofb = true, flip = side > 0 })) return;
            int f = -side; double X = JsRound(x), Y = JsRound(y);
            Rect(X - 4, Y, 9, 1, "#E8E6DF"); Rect(f > 0 ? X - 3 : X - 2, Y - 1, 6, 1, "#E8E6DF"); Rect(f > 0 ? X - 4 : X + 1, Y - 2, 4, 1, "#C9CCD1");
            Rect(f > 0 ? X - 2 : X - 1, Y + 1, 4, 1, "#9AA0A8");
        }
        void DrawAirplanes(Threat p, double ppx, double ppy)   // ppx, ppy: 정해진 자리
        {
            var r = run; double dur = Math.Max(0.01, p.impactT - p.spawnT), tx = ppx + p.side * 8, ty = ppy - 16, x0 = p.side < 0 ? -16 : W + 16, gap = D(C.airplaneGap);
            for (int k = p.k; k < C.airplaneCount; k++)
            {
                double t0 = p.spawnT + k * gap;
                if (r.time < t0) continue;
                double u = Clamp((r.time - t0) / dur, 0, 1);
                DrawPlane(Lerp(x0, tx, u), ty - 6 + (k % 2) * 3 - Math.Sin(u * Math.PI) * 10, p.side, r.time - t0 + k * 0.07);
            }
            if (r.time >= p.spawnT && ShowMarks())
            {
                double u = Clamp((r.time - p.spawnT) / dur, 0, 1), wx = Clamp(Lerp(x0, tx, u), 8, W - 8), wy = ty - 26;
                bool wk = DrawSprite("warning", "default", "blink", r.time - p.spawnT, wx, wy, new SO { anchor = "c", fw = 8, fh = 12, nofb = true });
                if (!wk && Math.Floor(time * 8) % 2 == 0) { Rect(wx - 5, wy - 7, 10, 14, PalRed); Txt("!", wx, wy, T(10, "center", "middle", true, shadow: false)); }
                Txt("꾹!", wx, wy - 12, T(7, "center", "middle", true, Hex("#E3B65C")));
            }
        }

        void Plate(double x, double y, double w, double h)   // 05-3 컨셉의 이중 테두리 명판
        {
            Rect(x, y, w, h, PalBg, 0.92); FrameRect(x, y, w, h, "#9AA0A8"); FrameRect(x + 2, y + 2, w - 4, h - 4, "#5A5F66");
        }
        void DrawKeyCap(double x, double y, int idx, string iconName, string label)
        {
            bool lit = time < padUntil[idx] || (idx >= 2 && demo == null && screen == Screen.Play && run != null && run.held[idx - 2]);   // 누르고 있는 막기 키도 켠다
            Rect(x - 1, y - 1, 20, 20, "#121317", 0.92);                       // 컨셉 키 아이콘: 짙은 바깥 테두리
            Rect(x, y, 18, 18, lit ? PalGreen : Hex("#2B2E35"));
            FrameRect(x + 1, y + 1, 16, 16, lit ? PalText : Hex("#C9CCD1"));    // 밝은 안쪽 테두리
            bool ok = DrawSprite("hud_icons", "default", iconName, time, x + 3, y + 3, new SO { anchor = "tl", dw = 12, dh = 12, nofb = true });
            if (!ok) Txt(label, x + 9, y + 9, T(9, "center", "middle", true, lit ? PalBg : PalText, false));
            Rect(x + 12, y + 12, 9, 8, "#121317"); Rect(x + 13, y + 13, 7, 6, "#C24A64");   // 오른쪽 아래 빨간 딱지: 키 이름
            Txt(label, x + 16.5, y + 16, T(5, "center", "middle", true, Hex("#FFFFFF"), false));
        }
        void DrawHud()
        {
            var r = run;
            // 1 층수·직급 명판
            Plate(6, 5, 86, 19);
            DrawBadgeIcon(10, 9, 12);
            if (r.Mode == GameMode.Practice)
            {   // 연습: 층 대신 모드 이름, 일수·하트 대신 막은 수와 맞은 수
                Txt("연습 모드", 56, 14.5, T(9, "center", "middle", true));
                Plate(W - 92, 5, 86, 19);
                Txt("막음 " + r.parries + " · 맞음 " + r.hits, W - 49, 14.5, T(8, "center", "middle", true));
                Txt("ESC → 메인으로", W - 49, 31, T(6, "center", "middle", color: Hex("#9AA0A8")));
            }
            else
            {
                Txt((r.Mode == GameMode.Endless ? "∞ " : "") + r.floor + "F · " + C.rankNames[r.rankIdx], 56, 14.5, T(r.Mode == GameMode.Endless ? 8 : 9, "center", "middle", true));
                // 2 남은 일수 명판 + 하트
                int dayInt = (int)Math.Ceiling(r.days);
                bool low = r.days <= 7;
                Plate(W - 92, 5, 86, 19);
                bool ok = DrawSprite("hud_icons", "default", "calendar", 0, W - 88, 9, new SO { anchor = "tl", dw = 12, dh = 12, nofb = true });
                if (!ok) Rect(W - 88, 9, 12, 12, PalGray);
                Txt("D-" + dayInt, W - 44, 14.5, T(10, "center", "middle", true, low ? PalRed : PalText));
                Rect(W - 89, 21, 80 * Clamp(r.days / D(C.maxDays), 0, 1), 1, low ? PalRed : PalGreen);   // 일수 게이지
                for (int i = 0; i < C.maxHearts; i++) DrawHeart(W - 8 - (C.maxHearts - i) * 13, 27, i < r.hearts);
            }
            // 콤보 (가운데 위, 금색): COMBO = 연속으로 막은 횟수, 그 아래 점수 배수 (3연속마다 ×1, 최대 ×10)
            if (r.combo > 0)
            {
                Txt(r.combo + " COMBO", 160, 8, T(10, "center", bold: true, color: Hex("#E3B65C")));
                TxtO("점수 ×" + r.mult, 160, 21, T(6, "center", bold: true, color: r.mult >= C.maxMult ? Hex("#FF8A7A") : Hex("#E8E6DF")));
            }
            // 4 조작 키 (아래 가운데)
            DrawKeyCap(114, 158, 0, "arrow_left", "←");
            DrawKeyCap(136, 158, 1, "arrow_right", "→");
            DrawKeyCap(166, 158, 2, "shield_q", "Q");
            DrawKeyCap(188, 158, 3, "shield_e", "E");
        }
        void DrawPlay()
        {
            var r = run;
            float ox = 0, oy = 0;
            if (r.shake > 0) { ox = JsRound((Random.value * 2 - 1) * 2); oy = JsRound((Random.value * 2 - 1) * 2); }
            cv.Save(); cv.Translate(ox, oy);
            DrawPlayWorld();
            cv.Restore();
            DrawHud();
        }

        // ================================================================ 05-4 일시정지 / 설정 / 조작법
        void DrawPause()
        {
            DrawPlay();
            Rect(0, 0, W, H, PalBg, 0.65);                       // 5 반투명 오버레이
            Rect(80, 38, 160, 100, PalBg, 0.95); FrameRect(80, 38, 160, 100, PalGray);
            Txt("일시정지", 160, 48, T(12, "center", bold: true));   // 1 제목
            DrawButtons();
        }
        void DrawSettings()
        {
            Rect(0, 0, W, H, PalBg, 0.6);
            Rect(50, 14, 220, 152, PalBg, 0.97); FrameRect(50, 14, 220, 152, PalGray);
            Txt("설정", 160, 19, T(10, "center", bold: true));
            Txt("사운드", 62, 37, T(8));
            Txt("볼륨", 62, 55, T(8));
            Txt(Math.Round(audioDirector.vol * 100, MidpointRounding.AwayFromZero) + "%", 214, 55, T(8, "center"));
            Txt("화면", 62, 73, T(8));
            Txt("배율 ×" + Num(scale) + " (창 크기에 맞춰 자동)", 62, 86, T(7, color: PalGreen));
            Txt("조작법", 62, 99, T(8));
            Txt("장애물", 62, 117, T(8));
            Txt("← → 이동 · Q E 패링 · ESC 일시정지 (키 변경 불가)", 62, 132, T(6, color: PalGray));
            DrawButtons();
        }
        void DrawHelp()
        {
            Rect(0, 0, W, H, PalBg, 0.7);
            Rect(50, 16, 220, 150, PalBg, 0.97); FrameRect(50, 16, 220, 150, PalGray);
            Txt("조작법", 160, 21, T(10, "center", bold: true));
            string[,] rows = { { "← →", "다음 계단 방향으로 한 칸 오르기" }, { "Q", "왼쪽 패링" }, { "E", "오른쪽 패링" }, { "ESC", "일시정지" }, { "Enter", "메뉴 결정 (방향키 선택·클릭)" } };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                double y = 34 + i * 13;
                Rect(60, y, 36, 13, PalGray); FrameRect(60, y, 36, 13, PalText);
                Txt(rows[i, 0], 78, y + 7, T(7, "center", "middle", true));
                Txt(rows[i, 1], 104, y + 7, T(7, bas: "middle"));
            }
            Txt("\"!\" 1초 뒤 도착 · 도착 직전 0.2초 안에 맞는 쪽 키 (소리의 박)", 160, 100, T(6, "center", color: PalGreen));
            Txt("11층~ 경쟁 사원 · 커피(× 표시: 막으면 하트 -1) · 15층~ 스테이플러(반대쪽)", 160, 106, T(6, "center", color: Hex("#D9DCE0")));
            Txt("20층~ 종이비행기(키 꾹) · 에스컬레이터 · 21층~ 모니터 · 금 간 계단", 160, 112, T(6, "center", color: Hex("#D9DCE0")));
            Txt("25층~ 편지 3장(박자) · 30층~ 젖은 계단 · 튀어 내려오는 의자(내 칸이면 막기)", 160, 118, T(6, "center", color: Hex("#D9DCE0")));
            Txt("31층~ 전화기 · 35층~ 거꾸로 칸(반대 키) · 41층~ 서류 · 사라지는 칸", 160, 124, T(6, "center", color: Hex("#D9DCE0")));
            Txt("61층~ 상사의 눈 · 포스트잇(색=횟수) · 카메라 플래시(무해)", 160, 130, T(6, "center", color: Hex("#D9DCE0")));
            Txt("71층~ 서류가방(터질 때 막기)", 160, 136, T(6, "center", color: Hex("#D9DCE0")));
            Txt("틀린 방향·놓친 공격은 하트 -1, 새 층마다 하트 +1", 160, 142, T(6, "center", color: PalText));
            DrawButtons();
        }

        // ================================================================ 장애물 보기 (웹 DEMOS/openDemo/demoUpdate/drawDemo)
        // 장애물마다 짧은 장면을 실제 규칙(Tick)으로 돌리고, 자동으로 맞는 키를 맞는 순간에 눌러 보여 준다. 기록·점수에는 남지 않는다.
        // 설정 화면의 "장애물 · 보기"와 조작법 화면의 "장애물 보기"에서 연다 (설정은 타이틀·로비·일시정지·결과에서).
        // 장면은 따로 만든 판(demo.run)에서 돌고, 돌리거나 그릴 때만 run/screen/플레이어 동작을 잠깐 바꿔 끼운다.
        // 칸 번호: 계단 장애물은 칸 번호로 정해진다 (171 금 감, 189 젖음, 205 에스컬레이터, 262 사라짐. 156~161, 372, 402~407 근처는 안전)
        sealed class DemoScene
        {
            public string id, name, how, wait; public int at; public ThreatType? threat; public double[] climb; public double? dur, flash, vanishPhase, gazeT0; public int hp;
        }
        sealed class Demo
        {
            public int idx, ci, nowKey; public RunSimulation run; public string animState = ""; public double animStart;
            public double? doneAt, crossed; public bool spawned, sawLive, waiting, rivalWait; public double nowUntil, holdUntil; public string nowText = "지금!";
        }
        const int DemoSeed = 7;
        static readonly DemoScene[] Demos =
        {
            new DemoScene { id = "climb", name = "계단 오르기", how = "다음 계단이 놓인 쪽 화살표(← →). 틀리면 하트 -1, 오르고 막을수록 남은 일수가 늘어요", at = 0, climb = new[] { 0.5, 0.85, 1.2, 1.55, 1.9, 2.25, 2.6 }, dur = 3.4 },
            new DemoScene { id = "clipboard", name = "결재판", how = "\"딱… 딱!\"의 두 번째 딱에 날아오는 쪽 키 (Q 왼쪽 · E 오른쪽)", at = 156, threat = ThreatType.Clipboard },
            new DemoScene { id = "rival", name = "경쟁 사원", how = "층 바닥을 걸어와 계단을 쫓아와요. 덤비는 순간 내가 올라온 쪽(뒤쪽) 키", at = 156, threat = ThreatType.Rival, climb = new[] { 0.9, 1.25, 1.6, 1.95 } },
            new DemoScene { id = "coffee", name = "커피", how = "× 표시는 막지 마세요! 막으면 하트 -1, 그냥 두면 지나갑니다", at = 156, threat = ThreatType.Coffee },
            new DemoScene { id = "monitor", name = "모니터", how = "형광등이 깜박이면 위에서 떨어져요. 유리 깨지는 소리 1초 뒤에 막기", at = 156, threat = ThreatType.Monitor },
            new DemoScene { id = "phone", name = "전화기", how = "아래에서 솟아올라요. 두 번째 벨이 울리는 순간에 막기", at = 156, threat = ThreatType.Phone },
            new DemoScene { id = "paper", name = "서류", how = "던진 쪽 반대편에서 맞아요. \"!\"가 뜬 쪽(맞는 쪽) 키로", at = 156, threat = ThreatType.Paper },
            new DemoScene { id = "sticky", name = "포스트잇", how = "막아도 튕겨 돌아와요. 색 = 남은 횟수 (분홍5 파랑4 초록3 노랑2 흰색1)", at = 156, threat = ThreatType.Sticky, hp = 3 },
            new DemoScene { id = "suitcase", name = "서류가방", how = "\"똑, 똑, 펑!\" 터지는 순간에 막기", at = 156, threat = ThreatType.Suitcase },
            new DemoScene { id = "stapler", name = "스테이플러", how = "막으면 뒤로 튕겼다가 머리 위로 휘어 반대쪽에서 돌아와요. 반대쪽 키로 한 번 더 (↺)", at = 156, threat = ThreatType.Stapler },
            new DemoScene { id = "airplane", name = "종이비행기 편대", how = "날아오는 쪽 막기 키를 꾹 누르고 있기. 마지막 비행기까지 떼지 마세요", at = 156, threat = ThreatType.Airplane },
            new DemoScene { id = "envelope", name = "편지 3연발", how = "같은 쪽에서 박자에 맞춰 세 장이 와요. 한 장씩 \"착, 착, 착\" 세 번 막기", at = 156, threat = ThreatType.Envelope },
            new DemoScene { id = "chair", name = "튀어 내려오는 의자", how = "계단을 두 칸씩 튀며 내려와요. 내 칸에 내려앉을 때 맞으니 가만히 서서 오는 쪽 키로 막기", at = 186, threat = ThreatType.Chair },
            new DemoScene { id = "chairdodge", name = "의자 피하기", how = "의자가 내려앉는 칸에 없으면 맞지 않아요. 한 칸 올라 피하기", at = 186, threat = ThreatType.Chair, climb = new[] { 1.0 } },
            new DemoScene { id = "reverse", name = "거꾸로 칸", how = "보라색 칸에서는 반대쪽 화살표를 눌러야 올라가요 (\"반대로!\")", at = 236, climb = new[] { 0.5, 0.85, 1.2, 1.55 }, dur = 2.4 },
            new DemoScene { id = "cracked", name = "금 간 계단", how = "서 있으면 흔들리다 1.5초면 무너져요. 멈추지 말고 다음 칸으로", at = 170, climb = new[] { 0.5, 1.5, 1.85, 2.2 }, dur = 3.2 },
            new DemoScene { id = "escalator", name = "내려가는 에스컬레이터", how = "쇠빛 칸에 가만히 서 있으면 아래로 밀려요. 쉬지 말고 지나가기", at = 205, climb = new[] { 1.6, 1.95, 2.3, 2.65 }, dur = 3.4 },
            new DemoScene { id = "shock", name = "젖은 계단", how = "불꽃이 튀면 멈추고, 전기가 흐르고 난 뒤에 건너기 (3초마다 감전)", at = 188, wait = "wet" },
            new DemoScene { id = "vanish", name = "사라지는 칸", how = "하늘색 선 칸은 5초 있다가 1초 없어져요. 깜박이면 기다렸다가 다시 생기면 건너기", at = 261, wait = "vanish", vanishPhase = 3.8 },
            new DemoScene { id = "rivalwait", name = "경쟁 사원과 사라지는 칸", how = "경쟁 사원도 사라지는 칸·전기가 흐르는 칸 앞에서는 멈춰 기다려요", at = 356, threat = ThreatType.Rival, vanishPhase = 3.6 },
            new DemoScene { id = "rivalcrack", name = "경쟁 사원과 금 간 칸", how = "경쟁 사원은 금 간 칸에 0.8초만 서 있어도 떨어지고, 젖은 칸에서 전기가 흐르면 감전돼요", at = 212, threat = ThreatType.Rival },
            new DemoScene { id = "rivalgaze", name = "경쟁 사원과 상사의 눈", how = "레이저가 지나갈 때 움직인 경쟁 사원은 들켜서 실격! 나는 레이저가 지나갈 때까지 가만히", at = 402, threat = ThreatType.Rival, wait = "gaze", gazeT0 = -4.15, dur = 3.4 },
            new DemoScene { id = "gaze", name = "상사의 눈", how = "먼 곳에서 불꽃이 깜박이고 5초 뒤 거대한 레이저가 순식간에(0.6초) 화면을 쓸어내려요. 레이저가 내 위를 지날 때는 오르지 않기", at = 402, wait = "gaze", gazeT0 = -3.6 },
            new DemoScene { id = "flash", name = "카메라 플래시", how = "막을 수 없지만 피해도 없어요. 눈이 부셔도 계속 오르세요", at = 372, flash = 0.8, climb = new[] { 1.5, 1.85, 2.2 }, dur = 3.2 },
        };
        static readonly float[] DemoKeyX = { 114, 136, 166, 188 };

        public bool DemoOpen => demo != null;
        public int DemoIndex => demo != null ? demo.idx : -1;
        public void OpenDemo(string id)
        {
            int i = Array.FindIndex(Demos, d => d.id == id);
            demo = new Demo();
            DemoGo(i < 0 ? 0 : i);
            focus = 1;   // Enter = 다음
        }
        public void CloseDemo() { demo = null; focus = 0; }
        public void DemoNext() { if (demo != null) DemoGo(demo.idx + 1); }
        // 장면 하나를 돌리는 동안만 run/screen/플레이어 동작을 장면 것으로 바꿔 끼운다 (진행 중인 판은 그대로 남는다)
        void WithDemo(Action fn)
        {
            var sr = run; var ss = screen; string sa = animState; double sst = animStart;
            run = demo.run; screen = Screen.Play; animState = demo.animState; animStart = demo.animStart;
            try { fn(); }
            finally { demo.animState = animState; demo.animStart = animStart; run = sr; screen = ss; animState = sa; animStart = sst; }
        }
        void DemoGo(int i) => WithDemo(() => DemoStart(i));
        void DemoStart(int i)
        {
            var d = demo;
            d.idx = (i % Demos.Length + Demos.Length) % Demos.Length;
            var sc = Demos[d.idx];
            var r = new RunSimulation(C, DemoSeed, GameMode.Normal);
            r.Sfx += Sfx;
            run = d.run = r;
            r.steps = sc.at; r.floor = r.Stairs.FloorOfSteps(sc.at); r.facing = r.Stairs.DirAt(r.steps);
            int rk = 0; foreach (var f in C.promoteFloors) if (r.floor >= f) rk++;
            r.rankIdx = Math.Min(C.rankNames.Length - 1, rk);
            r.SnapCamera();
            r.nextAttack = sc.threat.HasValue ? 0.6 : 1e9; r.forceType = sc.threat;
            r.nextFlash = sc.flash ?? 1e9;
            r.vanishPhase = sc.vanishPhase ?? 0;
            if (sc.gazeT0.HasValue) r.gazeT0 = sc.gazeT0;
            d.ci = 0; d.doneAt = sc.dur; d.spawned = false; d.crossed = null; d.sawLive = false; d.waiting = false; d.nowKey = 0; d.nowUntil = 0; d.holdUntil = 0; d.nowText = "지금!";
            animState = ""; animStart = 0;
        }
        void DemoPress(Demo d, int k, string text = "지금!") { d.nowKey = k; d.nowUntil = time + 0.35; d.nowText = text; }
        void DemoStepNow(Demo d) { bool rev = run.Stairs.IsReverse(run.steps); int dir = run.Stairs.NeedDir(run.steps); KeyStep(dir); DemoPress(d, dir < 0 ? 0 : 1, rev ? "반대로!" : "지금!"); }
        void DemoUpdate(double dt)
        {
            WithDemo(() =>
            {
                var d = demo; var sc = Demos[d.idx]; var r = run;
                if (r.Ended) { DemoStart(d.idx); return; }                       // 혹시 끝나면 처음부터
                // 정해진 때에 오르기
                while (sc.climb != null && d.ci < sc.climb.Length && r.time >= sc.climb[d.ci]) { DemoStepNow(d); d.ci++; }
                // 기다렸다 건너기: 젖은 계단(전기), 사라지는 칸(없어짐), 상사의 눈(훑기)이 지나가면 건넌다
                if (sc.wait != null)
                {
                    var sh = r.Stairs.Shock(r.time); var vs = r.Vanish(); var gz = r.Gaze();
                    bool danger = sc.wait == "wet" ? sh.live : sc.wait == "vanish" ? vs.gone : gz.sweep;
                    bool soon = sc.wait == "wet" ? sh.warn : sc.wait == "vanish" ? vs.warn : gz.warn;
                    if (d.crossed == null)
                    {
                        if (danger) d.sawLive = true;
                        d.waiting = danger || soon;
                        if (d.sawLive && !danger) { DemoStepNow(d); d.crossed = r.time; d.waiting = false; }
                    }
                    else
                    {
                        double k = d.crossed.Value;
                        if (d.ci < 2 && r.time >= k + 0.35 * (d.ci + 1)) { DemoStepNow(d); d.ci++; }
                        if (d.doneAt == null) d.doneAt = k + 1.8;
                    }
                }
                // 막기: 막을 쪽이 정해진 위협은 도착 botParryLead 전에 (커피는 누르지 않는다). 종이비행기는 꾹 누르고 있기
                r.held[0] = r.held[1] = false;
                foreach (var p in r.threats)
                {
                    if (p.type != ThreatType.Airplane || p.done || r.time < p.impactT - D(C.botParryLead)) continue;
                    int hs = p.side < 0 ? 0 : 1; r.held[hs] = true; padUntil[hs + 2] = time + 0.05; DemoPress(d, hs + 2, "꾹!");
                }
                foreach (var p in r.threats)
                {
                    if (p.done || p.type == ThreatType.Airplane) continue;
                    var ch = p.type == ThreatType.Chair ? r.ChairHitT(p) : null; if (p.type == ThreatType.Chair && !ch.HasValue) continue;
                    double tti = (ch.HasValue ? ch.Value.t : p.type == ThreatType.Envelope ? p.impactT + p.k * D(C.envelopeGap) : p.impactT) - r.time; int s = p.side < 0 ? 0 : 1;
                    if (p.type == ThreatType.Coffee) { if (tti > 0 && tti <= 0.35) d.holdUntil = time + 0.45; continue; }
                    if (p.SideKnown && tti > 0 && tti <= D(C.botParryLead) && r.time >= r.parryCd[s]) { KeyParry(p.side); DemoPress(d, s + 2); }
                }
                r.Tick(dt);
                if (r.forceType.HasValue && r.threats.Count > 0)                 // 한 번만 내보낸다
                {
                    r.forceType = null; r.nextAttack = 1e9; d.spawned = true;
                    if (sc.hp > 0) r.threats[0].hp = sc.hp;
                }
                if (sc.flash.HasValue && r.flashing) r.nextFlash = 1e9;
                d.rivalWait = r.threats.Exists(p => p.IsRival && p.waiting);   // 경쟁 사원이 계단 장애물 앞에서 기다리는 중
                if (sc.threat.HasValue && d.spawned && r.threats.Count == 0 && d.doneAt == null) d.doneAt = r.time + 1.0;
                r.hearts = C.maxHearts; r.days = D(C.maxDays);                   // 장면은 끝나지 않는다
                if (d.doneAt != null && r.time >= d.doneAt.Value) DemoStart(d.idx + 1);
            });
        }
        void DrawDemo()
        {
            var d = demo; var sc = Demos[d.idx];
            WithDemo(DrawPlayWorld);
            Rect(0, 0, W, 36, PalBg, 0.88); Rect(0, 36, W, 1, "#9AA0A8", 0.6);
            Txt("장애물 보기 " + (d.idx + 1) + "/" + Demos.Length, 6, 4, T(6, color: Hex("#9AA0A8")));
            Txt(sc.name, 160, 4, T(11, "center", bold: true, color: Hex("#E3B65C")));
            Txt(sc.how, 160, 22, T(6, "center"));
            string[] icons = { "arrow_left", "arrow_right", "shield_q", "shield_e" }, labels = { "←", "→", "Q", "E" };
            for (int i = 0; i < 4; i++) DrawKeyCap(DemoKeyX[i], 158, i, icons[i], labels[i]);
            if (time < d.nowUntil)
            {   // 지금 누른 키
                double kx = DemoKeyX[d.nowKey] + 9;
                TxtO(d.nowText, kx, 134, T(7, "center", "middle", true, Hex("#F2C46A")));   // 층 표시와 겹치지 않게 위로
                Rect(kx - 1, 140, 2, 16, "#E3B65C");
            }
            if (time < d.holdUntil) TxtO("누르지 마세요!", 160, 146, T(7, "center", "middle", true, PalRed));
            if (d.waiting) Txt("기다리기…", 160, 146, T(7, "center", "middle", true, Hex("#E3B65C")));
            else if (d.rivalWait && time >= d.nowUntil) Txt("경쟁 사원도 기다려요", 160, 146, T(7, "center", "middle", true, Hex("#E3B65C")));
            DrawButtons();
        }

        // ================================================================ 05-5 결과
        void DrawVerdict(double x, double y, double w, double h, bool promo, string subtext)
        {
            cv.Save();
            cv.Translate((float)(x + w / 2), (float)(y + h / 2)); cv.Rotate(-0.04f);
            Rect(-w / 2, -h / 2, w, h, promo ? PalGreen : PalRed);
            FrameRect(-w / 2 + 2, -h / 2 + 2, w - 4, h - 4, promo ? PalBg : PalText);
            Txt(promo ? "승급" : "구조조정 대상", 0, -6, T(18, "center", "middle", true, promo ? PalBg : PalText, false));
            Txt(subtext, 0, 12, T(7, "center", "middle", false, promo ? PalBg : PalText, false));
            cv.Restore();
        }
        string CauseText(RunResult r) => r.cause == "days" ? "남은 일수 소진" : r.cause == "cracked" ? "금 간 계단이 무너짐" : r.cause == "shock" ? "젖은 계단에서 감전" : "하트 소진";
        List<(string a, string b)> ResultRows()
        {
            var r = result;
            return new List<(string, string)>
            {
                ("도달 층수", r.floor + "F"),
                ("직급", r.promoted ? C.rankNames[r.rankIdx - 1] + " → " + C.rankNames[r.rankIdx] : C.rankNames[r.rankIdx]),
                ("점수", r.score.ToString()),
                ("최고 콤보", r.maxCombo + "회 (점수 ×" + r.maxMult + ")"),
                ("전체 랭킹", "#-- (오프라인)"),
            };
        }
        void DrawResult()
        {
            // 컨셉 05-5: 어둡게 가라앉은 사무실(bg_result), 위 가운데 판정 판, 왼쪽 큰 캐릭터, 가운데 어두운 인사 평가 표
            var r = result; bool promo = r.promoted;
            if (!DrawImg("bg_result", 0, 0, W, H, null)) { DrawImg("bg_zone1", 0, 0, W, H, PalBg); Rect(0, 0, W, H, PalBg, 0.75); }
            // 1 상자를 들고 걸어 나가는 큰 모습 (구조조정은 회색 실루엣, 승급은 제 색으로 뿌듯한 자세)
            string rk = C.rankNames[r.rankIdx];
            DrawSprite("player", rk, promo ? "proud" : "box_exit", time, 62, 176,
                new SO { scale = 4, fw = 16, fh = 28, color = RankColors[r.rankIdx], flip = !promo, gray = !promo });
            // 3 위 가운데 판정 판. 승급도 구조조정도 도장처럼 크게 떨어져 쾅 찍힌다 (화면 흔들림, 잉크 튐)
            double rt = time - resultT0, k = Clamp((rt - 0.15) / 0.22, 0, 1);
            if (k >= 1 && !resultStamped) { resultStamped = true; Sfx("sfx_hit"); }
            double shake = rt > 0.37 && rt < 0.62 ? Math.Sin(rt * 90) * (0.62 - rt) * 10 : 0;
            if (k > 0)
            {
                cv.Save(); cv.Translate((float)(160 + shake), 11); float s = (float)(1 + (1 - k) * (1 - k) * 1.8); cv.Scale(s, s); cv.Rotate((float)((1 - k) * -0.12)); cv.Translate(-160, -11);
                cv.globalAlpha = (float)k;
                Rect(64, 0, 192, 22, promo ? PalGreen : PalRed); FrameRect(64, 0, 192, 22, PalText); FrameRect(66, 2, 188, 18, promo ? "#4E5733" : "#6E2626");
                Txt(promo ? "승급" : "구조조정 대상", 160, 11, T(13, "center", "middle", true));
                cv.Restore(); cv.globalAlpha = 1;
            }
            if (rt > 0.37 && rt < 1.1)
            {   // 잉크 튐 (승급은 올리브)
                double f = 1 - (rt - 0.37) / 0.73;
                int[,] ink = { { 58, 6 }, { 262, 14 }, { 70, 24 }, { 250, 2 }, { 150, 26 }, { 184, 25 } };
                for (int i = 0; i < 6; i++) Rect(ink[i, 0] + (i % 2 == 1 ? 1 : -1) * (1 - f) * 6, ink[i, 1], 2, 2, promo ? PalGreen : PalRed, 0.8 * f);
            }
            Txt(promo ? C.rankNames[r.rankIdx - 1] + " → " + rk + " · " + r.floor + "층 도달 (PoC 종료)"
                      : ((r.mode == GameMode.Endless ? "무한 모드 · " + r.floor + "F 도달 (최고 " + SaveData.BestEndless + "F) · " : "") + CauseText(r)),
                160, 28, T(6, "center", "middle", color: Hex("#9AA0A8")));
            // 4 인사 평가 결과 표 (칸마다 테두리, 값은 올리브 글자, 최고 콤보는 흰 테두리)
            Rect(112, 34, 140, 100, "#18191C", 0.95); FrameRect(112, 34, 140, 100, "#9AA0A8"); FrameRect(114, 36, 136, 96, PalGray);
            Rect(120, 41, 124, 12, "#141518"); FrameRect(120, 41, 124, 12, "#9AA0A8");
            Txt("인사 평가 결과", 182, 47.5, T(7, "center", "middle", true));
            (string a, string b, bool hi)[] rows =
            {
                ("도달 층수", r.floor + "F", false), ("직급", promo ? C.rankNames[r.rankIdx - 1] + "→" + rk : rk, false), ("점수", r.score.ToString(), false),
                ("최고 콤보", r.maxCombo + "회 ×" + r.maxMult, true), ("전체 랭킹", "#--", false),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                double y = 57 + i * 15;
                Rect(120, y, 76, 12, "#141518"); FrameRect(120, y, 76, 12, "#6E737A");
                Txt(rows[i].a, 124, y + 6.5, T(6, bas: "middle", color: Hex("#C9CCD1"), shadow: false));
                Rect(200, y, 44, 12, "#141518"); FrameRect(200, y, 44, 12, rows[i].hi ? PalText : PalGreen);
                Txt(rows[i].b, 222, y + 6.5, T(6, "center", "middle", true, rows[i].hi ? PalText : Hex("#A9B57C"), false));
            }
            if (r.newBest) TxtO("NEW", 262, 49, T(6, "right", bold: true, color: Hex("#FF6A5E")));
            // 구조조정 팁: 도장이 찍힌 뒤 떠오른다
            if (!promo)
            {
                double a = Clamp((rt - 0.5) / 0.3, 0, 1);
                Rect(40, 137, 240, 14, "#141518", 0.94 * a);   // rect는 알파를 받으면 끝에 1로 되돌리므로 먼저 그린다
                cv.globalAlpha = (float)a; FrameRect(40, 137, 240, 14, "#E3B65C");
                Txt("TIP", 50, 144, T(6, "center", "middle", true, Hex("#E3B65C")));
                Txt(Tips.TryGetValue(r.tip ?? "", out var tip) ? tip : Tips["clipboard"], 164, 144, T(6, "center", "middle", color: PalText));
                cv.globalAlpha = 1;
            }
            // 2, 5 버튼
            DrawButtons();
            if (time < toastUntil) Txt(toastText, 197, 174, T(6, "center", color: PalGreen));
        }

        // 기록 공유: 결과 카드를 640x360 PNG로 저장한다 (웹은 stairs_result.png를 내려받는다)
        string DoShare()
        {
            if (result == null) return null;
            string path = null;
            var rt = RenderTexture.GetTemporary((int)W * 2, (int)H * 2, 0, RenderTextureFormat.ARGB32);
            var prevRt = RenderTexture.active;
            float prevScale = scale;
            try
            {
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
                cv.Begin(new Rect(0, 0, W * 2, H * 2), 2);
                bool promo = result.promoted;
                Rect(0, 0, W, H, PalBg);
                Txt("승급의 계단", 160, 12, T(16, "center", bold: true));
                DrawVerdict(60, 38, 200, 42, promo, promo ? C.rankNames[result.rankIdx - 1] + " → " + C.rankNames[result.rankIdx] + " · " + result.floor + "층 승급" : (result.cause == "days" ? "남은 일수 소진" : "하트 소진"));
                Rect(60, 92, 200, 72, PalBg); FrameRect(60, 92, 200, 72, PalGray);
                Txt("인사 평가표", 68, 95, T(8, bold: true, color: PalGreen));
                var rows = ResultRows();
                for (int i = 0; i < rows.Count; i++)
                {
                    Txt(rows[i].a, 68, 108 + i * 10, T(8));
                    Txt(rows[i].b, 252, 108 + i * 10, T(8, "right"));
                }
                Txt("v0.1 PoC", 160, 168, T(7, "center", color: PalGray));
                cv.End();
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) dir = Application.persistentDataPath;
                path = Path.Combine(dir, "stairs_result.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Destroy(tex);
                Toast("결과 이미지를 저장했습니다");
                Debug.Log("[stairs] share → " + path);
            }
            catch (Exception e) { Toast("이미지 저장에 실패했습니다"); Debug.LogWarning("[stairs] share failed: " + e.Message); path = null; }
            finally { cv.SetScale(prevScale); RenderTexture.active = prevRt; RenderTexture.ReleaseTemporary(rt); }
            return path;
        }
    }
}
