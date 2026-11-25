// 계단 배치와 계단 장애물(금 간 계단, 젖은 계단). 웹 PoC의 newStairs/ensureStairs/isCracked/isWet/shockState.
using System;
using System.Collections.Generic;

namespace Stairs.Core
{
    public enum GameMode { Normal, Endless, Practice }

    public struct ShockState
    {
        public bool live;     // 지금 전기가 흐름 (젖은 칸에 있으면 감전)
        public bool warn;     // 곧 흐름 (전선 끝 불꽃)
        public float shockIn; // 다음에 흐르기까지 초 (흐르는 중이면 0)
        public int cycle;
    }

    public sealed class StairLayout
    {
        readonly StairsConfig c;
        readonly Mulberry32 rng;
        readonly List<int> dirs = new List<int>();
        readonly List<float> xs = new List<float> { 0f };
        int runLen;
        public GameMode Mode;

        public StairLayout(StairsConfig config, int seed, GameMode mode)
        {
            c = config;
            Mode = mode;
            rng = new Mulberry32(unchecked((uint)seed ^ 0x9e3779b1u));
        }

        void Ensure(int n)
        {
            while (dirs.Count < n)
            {
                int d;
                if (dirs.Count == 0) { d = rng.Next() < 0.5 ? -1 : 1; runLen = 1; }
                else
                {
                    int last = dirs[dirs.Count - 1];
                    if (runLen >= c.stairRunMax || rng.Next() >= c.stairKeepProb) { d = -last; runLen = 1; }
                    else { d = last; runLen++; }
                }
                dirs.Add(d);
                xs.Add(xs[xs.Count - 1] + d * c.stepW);
            }
        }

        /// <summary>i번째 칸에서 다음 칸으로 가는 방향 (-1 왼쪽, +1 오른쪽).</summary>
        public int DirAt(int i) { Ensure(i + 1); return dirs[i]; }
        /// <summary>i번째 칸의 가로 위치(논리 px). 세로는 -i * stepH.</summary>
        public float StairX(int i) { Ensure(i + 1); return xs[i]; }
        public int FloorOfSteps(int n) => 1 + n / c.stairsPerFloor;
        public static int ZoneOf(int f) => f <= 20 ? 1 : f <= 40 ? 2 : f <= 60 ? 3 : f <= 99 ? 4 : 5;
        /// <summary>배경음악 단계 1~5 (20층마다. 배경 구간과 달리 81~99층이 5단계).</summary>
        public static int StageOf(int f) => Math.Min(5, (f - 1) / 20 + 1);

        // 웹: ((i * k) >>> 0) % 1000 / 1000 < share. float 비율의 오차(0.14f > 0.14)로 갈리지 않게 천분율 정수로 비교한다
        static bool HashBelow(int i, ulong k, float share) => (uint)((ulong)i * k) % 1000 < (uint)Math.Round(share * 1000.0);

        public bool IsCracked(int i)
        {
            if (Mode == GameMode.Practice) return false;
            int from = Mode == GameMode.Endless ? 2 : c.crackFromFloor;
            if (FloorOfSteps(i) < from || i % c.stairsPerFloor == 0) return false;
            return HashBelow(i, 2654435761UL, c.crackShare);
        }

        public bool IsWet(int i)
        {
            if (Mode == GameMode.Practice) return false;
            int from = Mode == GameMode.Endless ? 3 : c.wetFromFloor;
            if (FloorOfSteps(i) < from || i % c.stairsPerFloor == 0 || IsCracked(i)) return false;
            return HashBelow(i, 2246822519UL, c.wetShare);
        }

        public ShockState Shock(float t)
        {
            float P = c.shockPeriod, ph = t % P, start = P - c.shockTime;
            return new ShockState
            {
                live = ph >= start,
                warn = ph >= start - c.shockWarn && ph < start,
                shockIn = ph >= start ? 0f : start - ph,
                cycle = (int)Math.Floor(t / P),
            };
        }
    }
}
