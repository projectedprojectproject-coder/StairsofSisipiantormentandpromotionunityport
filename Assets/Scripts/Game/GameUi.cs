// HUD와 메뉴 (uGUI, 코드로 만든다). 배치는 웹 PoC의 drawTitle/drawLobby/drawHud/drawPause/drawHelp/drawResult와 같은
// 논리 px(320x180)이고, CanvasScaler가 화면 높이에 맞춰 키운다. 아이콘은 웹과 같은 hud_icons 시트를 쓴다.
// 한글 글꼴: GameController.uiFont 에 NeoDunggeunmo(네오둥근모, SIL OFL) 또는 Noto Sans KR TTF를 넣는다.
// 비워 두면 OS 글꼴(맑은 고딕, Apple SD Gothic Neo, Noto Sans CJK KR)에서 찾는다. docs/UNITY_PORT.md 참고.
using System.Collections.Generic;
using Stairs.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using static Stairs.Game.UiKit;

namespace Stairs.Game
{
    public class GameUi : MonoBehaviour
    {
        public const float W = 320f, H = 180f;

        public static readonly Dictionary<string, string> Tips = new Dictionary<string, string>
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
        static readonly string[] ModeHints =
        {
            "연습 모드 · 11층 층 바닥에 서서 막기만 연습 (시간 제한·게임 오버 없음)",
            "업무 시작 · 1층에서 100층 CEO실까지 (20·40·60·80층 승급)",
            "무한 모드 · 1층부터 모든 장애물, 끝없이 (최고 층 기록)",
        };

        GameController gc;
        SpriteCatalog cat;
        StairsConfig C;

        RectTransform root, title, lobby, hud, pause, help, result;
        readonly List<UiButton> titleButtons = new List<UiButton>(), lobbyButtons = new List<UiButton>(), pauseButtons = new List<UiButton>(),
                                helpButtons = new List<UiButton>(), resultButtons = new List<UiButton>();
        // HUD
        Text floorText, daysText, comboText, multText, toastText, warnText, practiceText, practiceHint;
        Image daysGauge, hurt, cardPlayer; RectTransform normalRight, practiceRight;
        readonly List<Image> hearts = new List<Image>();
        readonly Image[] keyFill = new Image[4];
        // 로비
        Text bestText, bestSub, cardRank, lobbyHint;
        // 결과
        RectTransform stamp; CanvasGroup stampGroup, tipGroup; Image stampFill, stampInner, resultPlayer;
        Text stampText, subText, tipText, newText; readonly List<Text> rowValues = new List<Text>(); readonly List<Image> inks = new List<Image>();
        bool stamped;
        UiButton againButton, muteButton;

        public bool HelpOpen => help && help.gameObject.activeSelf;

        Sprite Hud(string anim) => cat ? cat.Get("hud_icons", anim, null, 0) : null;

        public void Build(GameController owner, SpriteCatalog catalog, StairsConfig config, Font f)
        {
            gc = owner; cat = catalog; C = config;
            font = f ? f : Font.CreateDynamicFontFromOSFont(new[] { "NeoDunggeunmo", "Galmuri11", "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR" }, 16);
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 320x180이 늘 다 보이게 (남는 쪽은 레터박스)
            if (!EventSystem.current) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // 320x180 논리 화면 (가운데)
            var go = new GameObject("Logical 320x180", typeof(RectTransform));
            root = (RectTransform)go.transform;
            root.SetParent(canvasGo.transform, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(W, H);

            BuildHud(); BuildTitle(); BuildLobby(); BuildPause(); BuildHelp(); BuildResult();
        }

        RectTransform Panel(string name)
        {
            var p = Node(root, name, 0, 0, W, H);
            p.gameObject.SetActive(false);
            return p;
        }

        Image Background(Transform parent, string id, float dim)
        {
            var s = cat ? cat.GetImage(id) : null;
            if (!s && cat) s = cat.GetImage("bg_zone1");
            var img = Box(parent, 0, 0, W, H, s ? Color.white : Bg, "Background " + id);
            img.sprite = s;
            if (!cat || !cat.GetImage(id)) Box(parent, 0, 0, W, H, A(Bg, dim), "Dim");
            return img;
        }

        // ------------------------------------------------------------ 05-1 타이틀
        void BuildTitle()
        {
            title = Panel("Title");
            Background(title, "title_bg", 0.3f);
            var logo = cat ? cat.GetImage("logo") : null;
            if (logo) Icon(title, logo, 20, 34, 280, 70, Color.clear, "Logo");
            else
            {
                Box(title, 50, 20, 220, 48, A(Bg, 0.8f)); Frame(title, 50, 20, 220, 48, Red);
                Label(title, "승급의 계단", 160, 40, 26, Ink, TextAnchor.MiddleCenter, true);
                Label(title, "구조조정까지 D-30", 160, 61, 8, Green);
            }
            titleButtons.Add(MakeButton(title, "시작하기", 122, 98, 76, 18, 10, true, gc.GoLobby));
            Label(title, "Enter 시작", 160, 124, 6, Silver);
            Label(title, "v0.2 Unity · 승급의 계단 제작팀", 160, 172, 6, Gray);
        }

        // ------------------------------------------------------------ 05-2 로비
        void BuildLobby()
        {
            lobby = Panel("Lobby");
            Background(lobby, "bg_lobby", 0.5f);
            Box(lobby, 124, 3, 72, 13, A(Bg, 0.92f)); Frame(lobby, 124, 3, 72, 13, Silver);   // 1 층·직급
            Label(lobby, "1F", 148, 9.5f, 8, Ink, TextAnchor.MiddleCenter, true);
            Label(lobby, C.rankNames[0], 172, 9.5f, 6, Silver);
            Box(lobby, 248, 5, 64, 18, Red); Frame(lobby, 248, 5, 64, 18, Ink); Frame(lobby, 249, 6, 62, 16, RedDark);   // 2 D-30
            Label(lobby, "D-" + C.startDays, 280, 14.5f, 11, Ink, TextAnchor.MiddleCenter, true);

            // 3 목걸이에 걸린 사원증 (웹 drawIdCard)
            float x = 12, y = 38;
            var strap = Hex(0x6E7A49);
            Box(lobby, x + 27, 0, 4, y - 6, strap); Box(lobby, x + 43, 0, 4, y - 6, strap);
            Box(lobby, x + 28, 0, 1, y - 6, Green); Box(lobby, x + 44, 0, 1, y - 6, Green);
            Box(lobby, x + 29, y - 9, 16, 12, Silver); Frame(lobby, x + 29, y - 9, 16, 12, Hex(0x44484E));
            Box(lobby, x, y, 74, 108, Ink); Frame(lobby, x, y, 74, 108, Gray);
            Box(lobby, x + 1, y + 1, 72, 13, Green); Box(lobby, x + 30, y + 5, 14, 3, OliveDark);
            Box(lobby, x + 15, y + 19, 44, 48, Hex(0x3A3D43)); Box(lobby, x + 18, y + 22, 38, 42, Silver);
            cardPlayer = Icon(lobby, null, x + 24, y + 20, 26, 45, Color.clear, "Card Player");
            string[] rows = { "NAME", "TITLE", "DEPARTMENT" };
            for (int i = 0; i < 3; i++)
            {
                float yy = y + 73 + i * 11;
                Label(lobby, rows[i], x + 5, yy + 2, 5, Hex(0x2A2C31), TextAnchor.MiddleLeft, true, 60, false);
                var v = Label(lobby, i == 0 ? "○○○" : i == 1 ? C.rankNames[0] : "총무팀", x + (rows[i].Length > 5 ? 44 : 28), yy + 2, 5, Gray, TextAnchor.MiddleLeft, false, 40, false);
                if (i == 1) cardRank = v;
                Box(lobby, x + 4, yy + 8, 66, 1, A(Silver, 0.6f));
            }

            Box(lobby, 244, 80, 68, 22, A(Bg, 0.9f)); Frame(lobby, 244, 80, 68, 22, Gray);   // 5 화이트보드 위 기록
            bestText = Label(lobby, "", 278, 87, 7, Ink, TextAnchor.MiddleCenter, true, 68);
            bestSub = Label(lobby, "", 278, 96, 5, Silver, TextAnchor.MiddleCenter, false, 68);

            lobbyHint = Label(lobby, "", 160, 142, 6, Ink);
            lobbyButtons.Add(MakeButton(lobby, "연습 모드", 12, 150, 84, 18, 8, false, () => gc.StartRun(GameMode.Practice)));
            lobbyButtons.Add(MakeButton(lobby, "업무 시작", 104, 150, 112, 18, 10, true, () => gc.StartRun(GameMode.Normal)));   // 4 업무 시작
            lobbyButtons.Add(MakeButton(lobby, "무한 모드", 224, 150, 84, 18, 8, false, () => gc.StartRun(GameMode.Endless)));
            Label(lobby, "← → 선택 · Enter 시작 · Esc 타이틀", 160, 174, 5, Silver);
        }

        void RefreshLobby()
        {
            int rIdx = SaveData.BestRankIdx(C);
            bestText.text = "최고 " + SaveData.BestFloor + "F";
            bestSub.text = "무한 " + SaveData.BestEndless + "F · 점수 " + SaveData.BestScore;
            cardRank.text = C.rankNames[rIdx];
            cardPlayer.sprite = cat ? cat.Get("player", "idle", C.rankNames[rIdx], Time.time) : null;
            cardPlayer.color = cardPlayer.sprite ? Color.white : Color.clear;
        }

        // ------------------------------------------------------------ 05-3 HUD
        void BuildHud()
        {
            hud = Panel("HUD");
            hurt = Box(hud, 0, 0, W, H, A(Red, 0), "Hurt");
            Plate(hud, 6, 5, 86, 19);                                                     // 1 층수·직급 명판
            Icon(hud, Hud("badge"), 10, 9, 12, 12, Green, "Badge");
            floorText = Label(hud, "", 56, 14.5f, 9, Ink, TextAnchor.MiddleCenter, true, 70);

            normalRight = Node(hud, "Days", 0, 0, W, H);                                   // 2 남은 일수 명판 + 하트
            Plate(normalRight, W - 92, 5, 86, 19);
            Icon(normalRight, Hud("calendar"), W - 88, 9, 12, 12, Gray, "Calendar");
            daysText = Label(normalRight, "", W - 44, 14.5f, 10, Ink, TextAnchor.MiddleCenter, true, 60);
            daysGauge = Box(normalRight, W - 89, 21, 80, 1, Green, "Gauge");
            for (int i = 0; i < C.maxHearts; i++) hearts.Add(Icon(normalRight, Hud("heart"), W - 8 - (C.maxHearts - i) * 13, 27, 12, 12, Red, "Heart"));

            practiceRight = Node(hud, "Practice", 0, 0, W, H);                             // 연습: 막은 수와 맞은 수
            Plate(practiceRight, W - 92, 5, 86, 19);
            practiceText = Label(practiceRight, "", W - 49, 14.5f, 8, Ink, TextAnchor.MiddleCenter, true, 84);
            practiceHint = Label(practiceRight, "ESC → 메뉴", W - 49, 31, 6, Silver);

            comboText = Label(hud, "", 160, 13, 10, Gold, TextAnchor.MiddleCenter, true);  // 콤보 (가운데 위, 금색)
            multText = Label(hud, "", 160, 24, 6, Ink, TextAnchor.MiddleCenter, true);
            toastText = Label(hud, "", 160, 70, 7, Green);
            warnText = Label(hud, "", 160, 46, 7, Gold, TextAnchor.MiddleCenter, true);

            string[] icons = { "arrow_left", "arrow_right", "shield_q", "shield_e" }, keys = { "←", "→", "Q", "E" };   // 4 조작 키
            float[] xs = { 114, 136, 166, 188 };
            for (int i = 0; i < 4; i++)
            {
                keyFill[i] = Box(hud, xs[i], 158, 18, 18, A(Bg, 0.85f), "Key " + keys[i]);
                Frame(keyFill[i].transform, 0, 0, 18, 18, Silver);
                var s = Hud(icons[i]);
                if (s) Icon(keyFill[i].transform, s, 3, 3, 12, 12, Color.clear);
                else Label(keyFill[i].transform, keys[i], 9, 9, 10, Ink, TextAnchor.MiddleCenter, true, 18, false);
            }
        }

        void RefreshHud()
        {
            var r = gc.run;
            if (r == null) return;
            bool practice = r.Mode == GameMode.Practice;
            floorText.text = practice ? "연습 모드" : (r.Mode == GameMode.Endless ? "∞ " : "") + r.floor + "F · " + r.RankName;
            floorText.fontSize = r.Mode == GameMode.Normal || practice ? 9 : 8;
            normalRight.gameObject.SetActive(!practice);
            practiceRight.gameObject.SetActive(practice);
            if (practice) practiceText.text = "막음 " + r.parries + " · 맞음 " + r.hits;
            else
            {
                bool low = r.days <= 7;
                daysText.text = "D-" + Mathf.CeilToInt(r.days);
                daysText.color = low ? Red : Ink;
                float k = Mathf.Clamp01(r.days / C.maxDays);
                daysGauge.rectTransform.sizeDelta = new Vector2(80 * k, 1);
                daysGauge.color = low ? Red : Green;
                for (int i = 0; i < hearts.Count; i++)
                {
                    var s = Hud(i < r.hearts ? "heart" : "heart_empty");
                    hearts[i].sprite = s;
                    hearts[i].color = s ? Color.white : (i < r.hearts ? Red : A(Gray, 0.5f));
                }
            }
            comboText.text = r.combo > 0 ? r.combo + " COMBO" : "";    // COMBO = 연속으로 막은 횟수
            multText.text = r.combo > 0 ? "점수 ×" + r.mult : "";      // 점수 배수 (3연속마다 ×1, 최대 ×10)
            multText.color = r.mult >= C.maxMult ? Red : Ink;
            toastText.text = gc.toast != null && Time.time < gc.toastUntil ? gc.toast : "";
            var sh = r.Shock;
            warnText.text = !r.Ended && sh.warn && r.Stairs.IsWet(r.steps + 1) ? "⚡ 곧 전기가 흐릅니다" : "";
            hurt.color = A(Red, 0.25f * r.hurtFlash / 0.35f);   // 맞으면 붉게 번쩍
            for (int i = 0; i < 4; i++)
            {
                bool lit = Time.time < gc.padUntil[i];
                keyFill[i].color = lit ? Green : A(Bg, 0.85f);
            }
        }

        // ------------------------------------------------------------ 05-4 일시정지, 조작법
        void BuildPause()
        {
            pause = Panel("Pause");
            Box(pause, 0, 0, W, H, A(Bg, 0.65f), "Overlay");                              // 5 반투명 오버레이
            Box(pause, 80, 38, 160, 100, A(Bg, 0.95f)); Frame(pause, 80, 38, 160, 100, Gray);
            Label(pause, "일시정지", 160, 54, 12, Ink, TextAnchor.MiddleCenter, true);     // 1 제목
            pauseButtons.Add(MakeButton(pause, "계속하기", 100, 66, 120, 14, 8, false, gc.Resume));
            pauseButtons.Add(MakeButton(pause, "조작법", 100, 84, 120, 14, 8, false, () => ShowHelp(true)));
            pauseButtons.Add(muteButton = MakeButton(pause, "", 100, 102, 120, 14, 8, false, gc.ToggleMute));
            pauseButtons.Add(MakeButton(pause, "메인으로", 100, 120, 120, 14, 8, false, gc.QuitToLobby));
            Label(pause, "Esc 계속 · ↑↓ 선택 · Enter 결정", 160, 146, 6, Silver);
        }

        void BuildHelp()
        {
            help = Panel("Help");
            Box(help, 0, 0, W, H, A(Bg, 0.7f), "Overlay");
            Box(help, 50, 16, 220, 150, A(Bg, 0.97f)); Frame(help, 50, 16, 220, 150, Gray);
            Label(help, "조작법", 160, 26, 10, Ink, TextAnchor.MiddleCenter, true);
            string[,] rows = { { "← →", "다음 계단 방향으로 한 칸 오르기" }, { "Q", "왼쪽 패링" }, { "E", "오른쪽 패링" }, { "ESC", "일시정지" }, { "Enter", "메뉴 결정 (방향키 선택·클릭)" } };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float y = 34 + i * 13;
                Box(help, 60, y, 36, 13, Gray); Frame(help, 60, y, 36, 13, Ink);
                Label(help, rows[i, 0], 78, y + 7, 7, Ink, TextAnchor.MiddleCenter, true, 36);
                Label(help, rows[i, 1], 104, y + 7, 7, Ink, TextAnchor.MiddleLeft, false, 160);
            }
            (string s, Color c)[] notes =
            {
                ("\"!\" 1초 뒤 도착 · 도착 직전 0.2초 안에 맞는 쪽 키 (소리의 박)", Green),
                ("11층~ 경쟁 사원(쫓아오면 뒤쪽 키) · 커피(× 표시: 막으면 하트 -1)", Red),
                ("21층~ 모니터 · 금 간 계단(1.5초) · 30층~ 젖은 계단(3초마다 감전)", Red),
                ("31층~ 전화기 · 41층~ 서류(반대쪽) · 61층~ 포스트잇(색=횟수)", Green),
                ("61층~ 카메라 플래시(무해) · 71층~ 서류가방(터질 때 막기)", Green),
                ("틀린 방향·놓친 공격은 하트 -1, 새 층마다 하트 +1", Ink),
            };
            for (int i = 0; i < notes.Length; i++) Label(help, notes[i].s, 160, 104 + i * 7, 6, notes[i].c);
            helpButtons.Add(MakeButton(help, "닫기", 110, 148, 100, 14, 8, false, () => ShowHelp(false)));
        }

        public void ShowHelp(bool on) { help.gameObject.SetActive(on); gc.ResetFocus(); }

        // ------------------------------------------------------------ 05-5 결과
        void BuildResult()
        {
            result = Panel("Result");
            Background(result, "bg_result", 0.75f);
            // 1 상자를 들고 걸어 나가는 큰 모습 (웹: scale 4.6, 발 (62,176))
            resultPlayer = Icon(result, null, 62 - 16 * 4.6f / 2, 176 - 28 * 4.6f, 16 * 4.6f, 28 * 4.6f, Color.clear, "Result Player");
            resultPlayer.rectTransform.pivot = new Vector2(0.5f, 0f);                     // 발 기준 (좌우 뒤집기도 제자리에서)
            resultPlayer.rectTransform.anchoredPosition = new Vector2(62, -176);

            // 3 판정 도장 (가운데 (160,11) 기준으로 크게 떨어져 쾅 찍힌다)
            stamp = Node(result, "Stamp", 64, 0, 192, 22);
            stamp.pivot = new Vector2(0.5f, 0.5f);
            stamp.anchoredPosition = new Vector2(160, -11);
            stampGroup = stamp.gameObject.AddComponent<CanvasGroup>();
            stampFill = Box(stamp, 0, 0, 192, 22, Red, "Fill");
            Frame(stamp, 0, 0, 192, 22, Ink);
            var innerFrame = Frame(stamp, 2, 2, 188, 18, RedDark);
            stampInner = innerFrame.GetComponentInChildren<Image>();
            stampText = Label(stamp, "", 96, 11, 13, Ink, TextAnchor.MiddleCenter, true, 192);
            int[,] ink = { { 58, 6 }, { 262, 14 }, { 70, 24 }, { 250, 2 }, { 150, 26 }, { 184, 25 } };
            for (int i = 0; i < ink.GetLength(0); i++) inks.Add(Box(result, ink[i, 0], ink[i, 1], 2, 2, Color.clear, "Ink"));
            subText = Label(result, "", 160, 28, 6, Silver);

            // 4 인사 평가 결과 표
            Box(result, 112, 34, 140, 100, A(Hex(0x18191C), 0.95f)); Frame(result, 112, 34, 140, 100, Silver); Frame(result, 114, 36, 136, 96, Gray);
            Box(result, 120, 41, 124, 12, Dark); Frame(result, 120, 41, 124, 12, Silver);
            Label(result, "인사 평가 결과", 182, 47.5f, 7, Ink, TextAnchor.MiddleCenter, true);
            string[] names = { "도달 층수", "직급", "점수", "최고 콤보", "전체 랭킹" };
            for (int i = 0; i < names.Length; i++)
            {
                float y = 57 + i * 15; bool hi = i == 3;
                Box(result, 120, y, 76, 12, Dark); Frame(result, 120, y, 76, 12, Mid);
                Label(result, names[i], 124, y + 6.5f, 6, Pale, TextAnchor.MiddleLeft, false, 70, false);
                Box(result, 200, y, 44, 12, Dark); Frame(result, 200, y, 44, 12, hi ? Ink : Green);
                rowValues.Add(Label(result, "", 222, y + 6.5f, 6, hi ? Ink : OliveText, TextAnchor.MiddleCenter, true, 44, false));
            }
            newText = Label(result, "NEW", 246, 64, 6, Red, TextAnchor.MiddleRight, true, 30);

            // 구조조정 팁: 도장이 찍힌 뒤 떠오른다
            var tip = Node(result, "Tip", 40, 137, 240, 14);
            tipGroup = tip.gameObject.AddComponent<CanvasGroup>();
            Box(tip, 0, 0, 240, 14, A(Dark, 0.94f)); Frame(tip, 0, 0, 240, 14, Gold);
            Label(tip, "TIP", 10, 7, 6, Gold, TextAnchor.MiddleCenter, true, 20);
            tipText = Label(tip, "", 124, 7, 6, Ink, TextAnchor.MiddleCenter, false, 200);

            // 2, 5 버튼
            resultButtons.Add(againButton = MakeButton(result, "다시 하기", 96, 155, 62, 16, 8, true, () => gc.StartRun(gc.result != null ? gc.result.mode : GameMode.Normal)));
            resultButtons.Add(MakeButton(result, "로비로", 166, 155, 62, 16, 8, false, gc.QuitToLobby));
        }

        void SetupResult()
        {
            var r = gc.result;
            if (r == null) return;
            bool promo = r.promoted;
            string rk = C.rankNames[r.rankIdx], prev = C.rankNames[Mathf.Max(0, r.rankIdx - 1)];
            stamped = false;
            stampFill.color = promo ? Green : Red;
            SetFrameColor(stampInner, promo ? OliveDark : RedDark);
            stampText.text = promo ? "승급" : "구조조정 대상";
            string cause = r.cause == "days" ? "남은 일수 소진" : r.cause == "cracked" ? "금 간 계단이 무너짐" : r.cause == "shock" ? "젖은 계단에서 감전" : "하트 소진";
            subText.text = promo ? prev + " → " + rk + " · " + r.floor + "층 도달 (PoC 종료)"
                : (r.mode == GameMode.Endless ? "무한 모드 · " + r.floor + "F 도달 (최고 " + SaveData.BestEndless + "F) · " : r.mode == GameMode.Practice ? "연습 모드 · " : "") + cause;
            rowValues[0].text = r.floor + "F";
            rowValues[1].text = promo ? prev + "→" + rk : rk;
            rowValues[2].text = r.score.ToString();
            rowValues[3].text = r.maxCombo + "회 ×" + r.maxMult;
            rowValues[4].text = "#--";
            newText.gameObject.SetActive(r.newBest);
            tipText.text = Tips.TryGetValue(r.tip ?? "", out var t) ? t : Tips["clipboard"];
            tipGroup.gameObject.SetActive(!promo);
            againButton.label.text = promo ? "1층부터" : "다시 하기";
            foreach (var ik in inks) ik.color = Color.clear;
            resultPlayer.sprite = cat ? cat.Get("player", promo ? "proud" : "box_exit", rk, 0) : null;
            resultPlayer.color = !resultPlayer.sprite ? Color.clear : promo ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);   // 구조조정은 회색 실루엣
            resultPlayer.rectTransform.localScale = new Vector3(promo ? 1 : -1, 1, 1);
        }

        static void SetFrameColor(Image anyEdge, Color c)
        {
            if (!anyEdge) return;
            foreach (var g in anyEdge.transform.parent.GetComponentsInChildren<Image>()) g.color = c;
        }

        void RefreshResult(float rt)
        {
            var r = gc.result;
            if (r == null) return;
            bool promo = r.promoted;
            // 승급도 구조조정도 판정 판이 도장처럼 찍힌다 (0.15초 뒤 0.22초 동안)
            float k = Mathf.Clamp01((rt - 0.15f) / 0.22f);
            if (k >= 1 && !stamped) { stamped = true; gc.PlaySfx("sfx_hit"); }
            float shake = rt > 0.37f && rt < 0.62f ? Mathf.Sin(rt * 90) * (0.62f - rt) * 10 : 0;
            float s = 1 + (1 - k) * (1 - k) * 1.8f;
            stamp.anchoredPosition = new Vector2(160 + shake, -11);
            stamp.localScale = new Vector3(s, s, 1);
            stamp.localEulerAngles = new Vector3(0, 0, (1 - k) * 0.12f * Mathf.Rad2Deg);   // 웹 rotate(-0.12): 화면에서 반시계
            stampGroup.alpha = k;
            float f = rt > 0.37f && rt < 1.1f ? 1 - (rt - 0.37f) / 0.73f : 0;   // 잉크 튐 (승급은 올리브)
            for (int i = 0; i < inks.Count; i++)
            {
                var rtf = inks[i].rectTransform;
                inks[i].color = A(promo ? Green : Red, 0.8f * f);
                float bx = i == 0 ? 58 : i == 1 ? 262 : i == 2 ? 70 : i == 3 ? 250 : i == 4 ? 150 : 184;
                rtf.anchoredPosition = new Vector2(bx + (i % 2 == 1 ? 1 : -1) * (1 - f) * 6, rtf.anchoredPosition.y);
            }
            tipGroup.alpha = Mathf.Clamp01((rt - 0.5f) / 0.3f);
            if (cat && resultPlayer.sprite) resultPlayer.sprite = cat.Get("player", promo ? "proud" : "box_exit", C.rankNames[r.rankIdx], rt);
        }

        // ------------------------------------------------------------ 화면 전환과 포커스
        public void Show(GameController.Screen s)
        {
            title.gameObject.SetActive(s == GameController.Screen.Title);
            lobby.gameObject.SetActive(s == GameController.Screen.Lobby);
            hud.gameObject.SetActive(s == GameController.Screen.Play || s == GameController.Screen.Pause);
            pause.gameObject.SetActive(s == GameController.Screen.Pause);
            result.gameObject.SetActive(s == GameController.Screen.Result);
            help.gameObject.SetActive(false);
            if (s == GameController.Screen.Lobby) RefreshLobby();
            if (s == GameController.Screen.Result) SetupResult();
            if (s == GameController.Screen.Pause) muteButton.label.text = gc.Muted ? "소리 꺼짐" : "소리 켜짐";
        }

        /// <summary>지금 화면에서 키보드로 고를 수 있는 버튼들 (조작법 창이 열려 있으면 그 창의 버튼).</summary>
        public List<UiButton> Buttons(GameController.Screen s)
        {
            if (HelpOpen) return helpButtons;
            switch (s)
            {
                case GameController.Screen.Title: return titleButtons;
                case GameController.Screen.Lobby: return lobbyButtons;
                case GameController.Screen.Pause: return pauseButtons;
                case GameController.Screen.Result: return resultButtons;
                default: return null;
            }
        }

        /// <summary>매 프레임: 움직이는 글자·아이콘과 포커스를 고친다.</summary>
        public void Tick(GameController.Screen s, float screenTime, int focus)
        {
            if (s == GameController.Screen.Play || s == GameController.Screen.Pause) RefreshHud();
            if (s == GameController.Screen.Result) RefreshResult(screenTime);
            if (s == GameController.Screen.Lobby)
            {
                lobbyHint.text = ModeHints[Mathf.Clamp(focus, 0, ModeHints.Length - 1)];
                if (cat && cardPlayer.sprite) cardPlayer.sprite = cat.Get("player", "idle", cardRank.text, Time.time);
            }
            if (s == GameController.Screen.Pause) muteButton.label.text = gc.Muted ? "소리 꺼짐" : "소리 켜짐";
            var bs = Buttons(s);
            if (bs != null) for (int i = 0; i < bs.Count; i++) bs[i].SetFocus(i == focus);
        }
    }
}
