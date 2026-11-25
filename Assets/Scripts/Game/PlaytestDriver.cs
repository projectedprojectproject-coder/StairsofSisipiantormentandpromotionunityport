// 자동 플레이테스트. 플레이어(또는 에디터)를 -stairsPlaytest <폴더> 로 실행했을 때만 켜진다 (-seed 1 과 같이 쓴다).
// 웹 판을 같은 순서로 찍는 Tools~/web_shots.py와 짝을 이룬다: 같은 seed, 같은 계단 수에서 찍어 화면을 맞대어 본다.
//   타이틀 → 설정 → 조작법 → 로비 → 보통 모드 봇(seed+0, 계단 20·70·180칸, 200칸에서 일시정지, 390·450칸) → 결과(승급) → 기록 공유
//   → 가만히 있는 판(seed+1) → 결과(구조조정) → 연습(seed+2) → 무한(seed+3, 25층)
// 로그의 오류·예외와 판 결과를 <폴더>/playtest_log.txt 에 남긴 뒤 끝낸다.
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
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-stairsAudioCheck") >= 0)
            {   // 소리만 확인: 타이틀에서 0.5초마다 출력 크기와 소리별 재생 위치
                for (int i = 0; i < 8; i++) { yield return new WaitForSecondsRealtime(0.5f); Line(AudioReport(gc, "타이틀 " + (i + 1) * 0.5f + "s")); }
                gc.bot = true; gc.StartRun(GameMode.Normal);
                for (int i = 0; i < 4; i++) { yield return new WaitForSecondsRealtime(0.5f); Line(AudioReport(gc, "플레이 " + (i + 1) * 0.5f + "s")); }
                Finish(); yield break;
            }
            Line("screen " + Screen.width + "x" + Screen.height + ", catalog " + (gc.catalog ? "ok" : "없음") + ", font " + gc.FontName);
            yield return new WaitForSecondsRealtime(1.5f); yield return Shot("01_title");
            Line(AudioReport(gc, "타이틀"));
            gc.OpenSettings(); yield return new WaitForSecondsRealtime(0.3f); yield return Shot("02_settings");
            gc.OpenHelp(); yield return new WaitForSecondsRealtime(0.3f); yield return Shot("03_help");
            // 장애물 보기: 장면마다 보기 좋은 순간에 찍는다 (web_shots.py와 같은 순간)
            gc.OpenDemo("climb");
            for (int i = 0; i < DemoMoments.Length; i++)
            {
                yield return new WaitForSecondsRealtime(DemoMoments[i]); yield return Shot("03_demo_" + (i + 1).ToString("00"));
                gc.DemoNext(); yield return null;
            }
            gc.CloseDemo();
            gc.CloseHelp(); gc.CloseSettings();
            gc.GoLobby(); yield return new WaitForSecondsRealtime(0.5f); yield return Shot("04_lobby");
            gc.OpenSettings(); yield return new WaitForSecondsRealtime(0.3f); yield return Shot("04_lobby_settings"); gc.CloseSettings();

            // 보통 모드: 봇이 100층까지 (seed+0)
            gc.bot = true; gc.StartRun(GameMode.Normal);
            yield return Steps(gc, 20); yield return Shot("05_play_steps20");
            Line(AudioReport(gc, "플레이"));
            yield return Steps(gc, 70); yield return Shot("06_play_steps70");
            yield return Steps(gc, 180); yield return Shot("07_play_steps180");
            yield return Steps(gc, 200); gc.PauseGame(); yield return new WaitForSecondsRealtime(0.3f); yield return Shot("08_pause");
            gc.Resume();
            yield return Steps(gc, 390); yield return Shot("09_play_steps390");
            yield return Steps(gc, 450); yield return Shot("10_play_steps450");
            yield return Until(() => gc.screen == GameController.Screen.Result, 150);
            Line("보통 봇 판: " + Describe(gc.result));
            yield return new WaitForSecondsRealtime(0.25f); yield return Shot("11_result_promoted_stamp");
            yield return new WaitForSecondsRealtime(1.5f); yield return Shot("12_result_promoted");
            Line("기록 공유: " + (gc.ShareResult() ?? "실패"));
            yield return new WaitForSecondsRealtime(0.3f); yield return Shot("13_result_share_toast");

            // 지는 판: 아무것도 하지 않으면 하트가 다 떨어진다 (seed+1)
            gc.bot = false; gc.StartRun(GameMode.Normal);
            yield return Until(() => gc.screen == GameController.Screen.Result, 40);
            Line("가만히 있는 판: " + Describe(gc.result));
            yield return new WaitForSecondsRealtime(1.5f); yield return Shot("14_result_fired");
            gc.OpenSettings(); yield return new WaitForSecondsRealtime(0.3f); yield return Shot("14_result_settings");
            gc.OpenDemo("climb"); yield return new WaitForSecondsRealtime(1.3f); yield return Shot("14_result_settings_demo"); gc.CloseDemo(); gc.CloseSettings();

            // 연습 모드(seed+2), 무한 모드(seed+3), 봇
            gc.bot = true; gc.StartRun(GameMode.Practice);
            yield return new WaitForSecondsRealtime(3f); yield return Shot("15_practice");
            Line("연습: 막음 " + gc.run.parries + " 맞음 " + gc.run.hits);
            gc.StartRun(GameMode.Endless);
            yield return Until(() => gc.run == null || gc.run.floor >= 25, 60);
            yield return Shot("16_endless_25F");
            Line("무한: " + gc.run.floor + "F 점수 " + gc.run.score + " 하트 " + gc.run.hearts);
            gc.QuitToLobby();
            yield return new WaitForSecondsRealtime(0.5f); yield return Shot("17_lobby_after_records");
            Line("기록: 최고 " + SaveData.BestFloor + "F, 점수 " + SaveData.BestScore + ", 무한 " + SaveData.BestEndless + "F");
            var sb = new StringBuilder("튼 효과음: ");
            foreach (var kv in gc.audioDirector.played) sb.Append(kv.Key).Append(' ').Append(kv.Value).Append(", ");
            Line(sb.ToString().TrimEnd(',', ' ') + " (" + gc.audioDirector.played.Count + "종)");
            Finish();
        }

        // 소리 확인: 출력 장치, 울리는 반복 소리, Unity가 실제로 내보내는 소리의 크기(RMS, 0이면 무음)
        static string AudioReport(GameController gc, string where)
        {
            var cfg = AudioSettings.GetConfiguration();
            float[] buf = new float[1024]; AudioListener.GetOutputData(buf, 0);
            double sum = 0; foreach (var v in buf) sum += v * v;
            var listener = FindFirstObjectByType<AudioListener>();
            return "소리(" + where + "): 출력 " + AudioSettings.outputSampleRate + "Hz " + cfg.speakerMode + ", 리스너 " + (listener ? "있음" : "없음")
                + ", 음소거 " + (gc.audioDirector && gc.audioDirector.muted) + ", 반복 " + (gc.audioDirector ? gc.audioDirector.Describe() : "-")
                + ", 출력 RMS " + System.Math.Sqrt(sum / buf.Length).ToString("0.0000");
        }

        static readonly float[] DemoMoments = { 1.3f, 1.45f, 1.9f, 1.5f, 1.55f, 1.5f, 1.5f, 1.9f, 1.5f, 1.2f, 2.2f, 0.85f };

        IEnumerator Steps(GameController gc, int n) => Until(() => gc.run == null || gc.run.Ended || gc.run.steps >= n, 90);

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
