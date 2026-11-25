// 최고 기록 저장 (웹의 save/writeSave와 같은 키: stairs_best_floor, stairs_best_score, stairs_best_endless).
// 보통 모드는 최고 층과 최고 점수, 무한 모드는 최고 층만 따로 남긴다. 연습 모드는 기록하지 않는다
// (웹은 연습 모드도 보통 기록에 섞어 11층이 남을 수 있는데, Unity 판은 빼기로 했다).
using Stairs.Core;
using UnityEngine;

namespace Stairs.Game
{
    public static class SaveData
    {
        const string KeyFloor = "stairs_best_floor", KeyScore = "stairs_best_score", KeyEndless = "stairs_best_endless";
        const string KeyMuted = "stairs_muted";

        public static int BestFloor => Mathf.Max(1, PlayerPrefs.GetInt(KeyFloor, 1));
        public static int BestScore => PlayerPrefs.GetInt(KeyScore, 0);
        public static int BestEndless => PlayerPrefs.GetInt(KeyEndless, 0);

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(KeyMuted, 0) == 1;
            set { PlayerPrefs.SetInt(KeyMuted, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>한 판의 결과를 기록하고 새 기록인지 돌려준다 (result.newBest도 채운다).</summary>
        public static bool Record(RunResult r)
        {
            bool best = false;
            if (r.mode == GameMode.Endless)
            {
                best = r.floor > BestEndless;
                if (best) PlayerPrefs.SetInt(KeyEndless, r.floor);
            }
            else if (r.mode == GameMode.Normal)
            {
                bool f = r.floor > BestFloor, s = r.score > BestScore;
                if (f) PlayerPrefs.SetInt(KeyFloor, r.floor);
                if (s) PlayerPrefs.SetInt(KeyScore, r.score);
                best = f || s;
            }
            PlayerPrefs.Save();
            r.newBest = best;
            return best;
        }

        /// <summary>로비의 사원증 직급: 최고 층까지 지난 승급 층 수.</summary>
        public static int BestRankIdx(StairsConfig c)
        {
            int n = 0, f = BestFloor;
            foreach (var p in c.promoteFloors) if (f >= p) n++;
            return Mathf.Min(n, c.rankNames.Length - 1);
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(KeyFloor); PlayerPrefs.DeleteKey(KeyScore); PlayerPrefs.DeleteKey(KeyEndless);
            PlayerPrefs.Save();
        }
    }
}
