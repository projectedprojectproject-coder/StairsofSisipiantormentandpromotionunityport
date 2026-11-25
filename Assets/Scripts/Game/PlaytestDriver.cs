// 자동 플레이테스트. 플레이어(또는 에디터)를 -stairsPlaytest <폴더> 로 실행했을 때만 켜진다.
// 타이틀 → 로비 → 보통 모드(봇, 100층까지) → 일시정지 → 결과(승급) → 지는 판(가만히) → 결과(구조조정) → 연습 → 무한 순서로
// 화면마다 스크린샷을 찍고, 로그의 오류·예외와 판 결과를 <폴더>/playtest_log.txt 에 남긴 뒤 끝낸다.
// 빌드: Stairs.EditorTools.StairsAssetImporter.BuildPlaytestPlayer (docs/UNITY_PORT.md 참고)
using System.Collections;
using System.IO;
using System.Text;
using Stairs.Core;
using UnityEngine;

namespace Stairs.Game
{
    public class PlaytestDriver : MonoBehaviour
    {
        string dir;
        readonly StringBuilder log = new StringBuilder();
        int errors, shots;
        const float Speed = 3f;   // 봇 판을 빨리 돌린다 (Core는 1/60초 고정 진행이라 결과는 같다)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == "-stairsPlaytest") { new GameObject("PlaytestDriver").AddComponent<PlaytestDriver>().dir = a[i + 1]; return; }
        }

        void Awake()
        {
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    errors++;
                    log.AppendLine("[" + type + "] " + msg + "\n" + stack);
                }
                else if (type == LogType.Warning) log.AppendLine("[Warning] " + msg);
            };
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(dir);
            var gc = FindFirstObjectByType<GameController>();
            if (!gc) { log.AppendLine("GameController 없음"); Finish(); yield break; }
            Line("screen " + Screen.width + "x" + Screen.height + ", catalog " + (gc.catalog ? "ok" : "없음") + ", font " + (UiKit.font ? UiKit.font.name : "없음"));
            yield return new WaitForSecondsRealtime(1.5f); yield return Shot("01_title");
            gc.GoLobby();
            yield return new WaitForSecondsRealtime(1f); yield return Shot("02_lobby");

            // 보통 모드: 봇이 100층까지
            gc.bot = true; gc.StartRun(GameMode.Normal); Time.timeScale = Speed;
            yield return new WaitForSecondsRealtime(1.2f); yield return Shot("03_play_1F");
            yield return Until(() => gc.run == null || gc.run.floor >= 12 && gc.run.threats.Exists(t => t.IsRival && t.phase == RivalPhase.Climb), 60);
            yield return Shot("04_play_rival_climb");
            yield return Until(() => gc.run == null || gc.run.floor >= 32 && gc.run.Shock.live, 60);
            yield return Shot("05_play_wet_shock");
            gc.PauseGame(); yield return new WaitForSecondsRealtime(0.5f); yield return Shot("06_pause");
            gc.Resume();
            yield return Until(() => gc.run == null || gc.run.floor >= 65 && gc.run.flashing, 60);
            yield return Shot("07_play_camera_flash");
            yield return Until(() => gc.screen == GameController.Screen.Result, 120);
            Time.timeScale = 1;
            Line("보통 봇 판: " + Describe(gc.result));
            yield return new WaitForSecondsRealtime(0.25f); yield return Shot("08_result_promoted_stamp");
            yield return new WaitForSecondsRealtime(1.5f); yield return Shot("09_result_promoted");

            // 지는 판: 아무것도 하지 않으면 하트가 다 떨어진다
            gc.bot = false; gc.StartRun(GameMode.Normal);
            yield return Until(() => gc.screen == GameController.Screen.Result, 40);
            Line("가만히 있는 판: " + Describe(gc.result));
            yield return new WaitForSecondsRealtime(1.5f); yield return Shot("10_result_fired");

            // 연습 모드, 무한 모드 (봇)
            gc.bot = true; gc.StartRun(GameMode.Practice); Time.timeScale = Speed;
            yield return new WaitForSecondsRealtime(3f); yield return Shot("11_practice");
            Line("연습: 막음 " + gc.run.parries + " 맞음 " + gc.run.hits);
            gc.StartRun(GameMode.Endless);
            yield return Until(() => gc.run == null || gc.run.floor >= 25, 60);
            yield return Shot("12_endless");
            Line("무한: " + gc.run.floor + "F 점수 " + gc.run.score + " 하트 " + gc.run.hearts);
            Time.timeScale = 1;
            gc.QuitToLobby();
            yield return new WaitForSecondsRealtime(0.8f); yield return Shot("13_lobby_after_records");
            Line("기록: 최고 " + SaveData.BestFloor + "F, 점수 " + SaveData.BestScore + ", 무한 " + SaveData.BestEndless + "F");
            Finish();
        }

        static string Describe(RunResult r) => r == null ? "결과 없음" : (r.promoted ? "승급" : "구조조정") + " " + r.cause + " " + r.floor + "F 점수 " + r.score + " 콤보 " + r.maxCombo + " ×" + r.maxMult + " tip " + r.tip + (r.newBest ? " NEW" : "");

        IEnumerator Until(System.Func<bool> cond, float timeoutRealSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!cond())
            {
                if (Time.realtimeSinceStartup - t0 > timeoutRealSeconds) { Line("시간 초과: 조건을 " + timeoutRealSeconds + "초 동안 못 만남"); yield break; }
                yield return null;
            }
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            shots++;
        }

        void Line(string s) { log.AppendLine(s); Debug.Log("[playtest] " + s); }

        void Finish()
        {
            log.Insert(0, "스크린샷 " + shots + "장, 오류 " + errors + "개\n");
            File.WriteAllText(Path.Combine(dir, "playtest_log.txt"), log.ToString(), Encoding.UTF8);
            Application.Quit(errors > 0 ? 2 : 0);
        }
    }
}
