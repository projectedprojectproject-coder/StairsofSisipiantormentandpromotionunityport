// 웹 PoC(demo_api/game.js)와 같은 결과가 나오는지 확인한다. 기대값은 웹 코드를 node로 돌려 뽑았다 (2026-11-25).
// Window > General > Test Runner > EditMode 에서 돌린다.
// Core는 상태를 double로 쌓으므로 1/60초(RunSimulation.Step60)씩 돌리면 웹과 프레임 단위까지 같다.
// 기대값 다시 뽑기: node unity/StairsOfPromotion/Tools~/rival_expect.js demo_api/game.js
//                  node unity/StairsOfPromotion/Tools~/playtest.js demo_api/game.js
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stairs.Core;

public class ParityTests
{
    [Test]
    public void Mulberry32_MatchesWeb()
    {
        var r = new Mulberry32(1);
        Assert.AreEqual(0.627073941, r.Next(), 1e-9);
        Assert.AreEqual(0.002735721, r.Next(), 1e-9);
        Assert.AreEqual(0.527447040, r.Next(), 1e-9);
    }

    [Test]
    public void StairDirections_MatchWeb_Seed1()
    {
        var s = new StairLayout(new StairsConfig(), 1, GameMode.Normal);
        string dirs = new string(Enumerable.Range(0, 40).Select(i => s.DirAt(i) > 0 ? 'R' : 'L').ToArray());
        Assert.AreEqual("RRRLLRRLRLRRLLLRRRRLLLRLLRRRLRLLLRRLRLRR", dirs);
    }

    [Test]
    public void CrackedAndWetTiles_MatchWeb()
    {
        var s = new StairLayout(new StairsConfig(), 1, GameMode.Normal);
        var cracked = Enumerable.Range(0, 600).Where(s.IsCracked).ToArray();
        var wet = Enumerable.Range(0, 600).Where(s.IsWet).ToArray();
        Assert.AreEqual(64, cracked.Length);
        CollectionAssert.AreEqual(new[] { 123, 128, 131, 140, 145, 149, 152, 154, 166, 171, 185, 197 }, cracked.Take(12));
        Assert.AreEqual(48, wet.Length);
        CollectionAssert.AreEqual(new[] { 176, 181, 184, 189, 195, 200, 233, 236, 239, 244, 247, 272 }, wet.Take(12));
        Assert.IsTrue(wet.All(i => s.FloorOfSteps(i) >= 30 && !s.IsCracked(i)));
    }

    [Test]
    public void ConfigValues_ReadAsWebDoubles()
    {
        // (double)0.33f 는 0.32999998... 이라 1일 길이가 웹과 달라진다. D()는 웹의 0.33 그대로
        Assert.AreEqual(0.33, StairsConfig.D(0.33f));
        Assert.AreEqual(0.145, StairsConfig.D(0.145f));
        Assert.AreEqual(0.19, StairsConfig.D(new StairsConfig().botStepInterval));
    }

    [Test]
    public void ShockCycle_LiveLastHalfSecondOfEveryThree()
    {
        var s = new StairLayout(new StairsConfig(), 1, GameMode.Normal);
        Assert.IsFalse(s.Shock(2.4).live);
        Assert.IsTrue(s.Shock(2.4).warn);
        Assert.IsTrue(s.Shock(2.6).live);
        Assert.IsFalse(s.Shock(3.1).live);
        Assert.AreEqual(2.0, s.Shock(0.5).shockIn, 1e-9);
    }

    [Test]
    public void StandingOnWetTile_EndsRunWhenCurrentFlows()
    {
        var sim = new RunSimulation(new StairsConfig(), 1, GameMode.Normal);
        sim.steps = 176; sim.floor = sim.Stairs.FloorOfSteps(176); sim.hearts = 99; sim.days = 99; sim.nextAttack = 999;
        for (int i = 0; i < 60 * 3 && !sim.Ended; i++) sim.Tick(RunSimulation.Step60);
        Assert.IsTrue(sim.Ended);
        Assert.AreEqual("shock", sim.ended.cause);
        Assert.AreEqual("shock", sim.ended.tip);
        Assert.GreaterOrEqual(sim.time, 2.5);   // 흐르기 전(0~2.5초)에는 살아 있다
    }

    [Test]
    public void Bot_ReachesFloor100AsCeo()
    {
        var sim = new RunSimulation(new StairsConfig(), 1, GameMode.Normal);
        sim.Bot = true;   // 웹처럼 Tick 안(시간을 더한 뒤)에서 봇이 돈다
        for (int i = 0; i < 60 * 600 && !sim.Ended; i++) sim.Tick(RunSimulation.Step60);
        Assert.IsTrue(sim.Ended);
        Assert.IsTrue(sim.ended.promoted, "봇이 " + sim.ended.cause + "로 " + sim.ended.floor + "층에서 끝남");
        Assert.AreEqual(100, sim.ended.floor);
        Assert.AreEqual("CEO", sim.RankName);
    }

    // ---------------------------------------------------------------- 한 판 전체가 웹과 같은지 (봇 플레이테스트)
    // 웹 game.js를 node에서 ?bot=1로 돌린 결과. seed는 웹 runCount 순서(보통 1~6, 무한 7~9, 연습 10~11 중 일부).
    // float로 쌓던 이전 Core는 같은 seed에서 점수가 146,700(웹 110,160)처럼 달라졌다.
    [TestCase(GameMode.Normal, 1, 60 * 300, "floor100", 100, 110160, 65, 45)]
    [TestCase(GameMode.Normal, 2, 60 * 300, "floor100", 100, 141670, 61, 61)]
    [TestCase(GameMode.Endless, 7, 60 * 240, null, 196, 279430, 197, 74)]
    [TestCase(GameMode.Practice, 10, 60 * 90, null, 11, 24200, 61, 61)]
    public void BotRun_MatchesWebFrameForFrame(GameMode mode, int seed, int ticks, string cause, int floor, int score, int parries, int maxCombo)
    {
        var sim = new RunSimulation(new StairsConfig(), seed, mode) { Bot = true };
        for (int i = 0; i < ticks && !sim.Ended; i++) sim.Tick(RunSimulation.Step60);
        Assert.AreEqual(cause, sim.ended?.cause);
        Assert.AreEqual(floor, sim.floor);
        Assert.AreEqual(score, sim.score);
        Assert.AreEqual(parries, sim.parries);
        Assert.AreEqual(maxCombo, sim.maxCombo);
        Assert.AreEqual(3, sim.hearts);
    }

    // ---------------------------------------------------------------- 경쟁 사원 쫓아오기 (2단계)
    // 기대값은 웹 game.js를 node에서 돌려 뽑았다: 경쟁 사원을 강제로 1번 내고(pickType 난수는 그대로 씀) 1/60초씩 진행.
    // 30칸(6층 바닥)에 서서 시작하고, 정해진 프레임에 다음 칸으로 오른다. seed는 웹의 runCount 순서대로 1~4.

    sealed class RivalTrace
    {
        public int spawnSide, floorStep, lockTick = -1, lockSide, stepsAtLock, doneTick = -1;
        public double wx0, lockRi, lockImpact;
        public RivalPhase lockPhase;
        public readonly List<string> sfx = new List<string>();
    }

    static RunSimulation RivalScenario(int seed, int[] stepTicks, int parryTick, out RivalTrace tr)
    {
        var sim = new RunSimulation(new StairsConfig(), seed, GameMode.Normal);
        sim.steps = 30; sim.floor = sim.Stairs.FloorOfSteps(30); sim.hearts = 3; sim.days = 30;
        sim.SnapCamera();
        sim.nextAttack = 0; sim.forceType = ThreatType.Rival;
        var t = new RivalTrace();
        sim.Sfx += id => t.sfx.Add(id + (t.lockTick < 0 ? "@before" : "@after"));
        for (int k = 1; k <= 400; k++)
        {
            if (System.Array.IndexOf(stepTicks, k) >= 0) sim.Step(sim.NextDir);
            if (k == parryTick && sim.threats.Count > 0) sim.Parry(sim.threats[0].side);
            sim.Tick(RunSimulation.Step60);
            if (k == 1)
            {
                sim.nextAttack = 999; sim.forceType = null;
                var p0 = sim.threats[0];
                Assert.AreEqual(ThreatType.Rival, p0.type);
                t.spawnSide = p0.side; t.floorStep = p0.floorStep; t.wx0 = p0.wx;
            }
            var p = sim.threats.Count > 0 ? sim.threats[0] : null;
            if (p != null && p.locked && t.lockTick < 0)
            {
                t.lockTick = k; t.lockSide = p.side; t.lockPhase = p.phase; t.lockRi = p.ri; t.lockImpact = p.impactT; t.stepsAtLock = sim.steps;
            }
            if (sim.threats.Count == 0 && t.lockTick >= 0) { t.doneTick = k; break; }
        }
        tr = t;
        return sim;
    }

    [Test]
    public void Rival_OnFloor_StandsBesidePlayer_KeepsSpawnSide()
    {
        var sim = RivalScenario(1, new int[0], 0, out var t);
        Assert.AreEqual(1, t.spawnSide);
        Assert.AreEqual(30, t.floorStep);
        Assert.AreEqual(260.0, t.wx0, 1e-9);                // camX(=stairX(30)=84) + 화면 반 폭 160 + 16
        Assert.AreEqual(RivalPhase.Stand, t.lockPhase);
        Assert.AreEqual(1, t.lockSide);                     // 층 바닥에서는 나온 쪽 그대로
        Assert.AreEqual(79, t.lockTick);                    // 치기 rivalLeap(0.3초) 전에 잠긴다
        Assert.AreEqual(98, t.doneTick);
        Assert.AreEqual(2, sim.hearts);                     // 막지 않았으니 하트 -1
    }

    [Test]
    public void Rival_ChasesUpStairs_LocksOnSideThePlayerCameFrom()
    {
        var sim = RivalScenario(2, new[] { 20, 32, 44, 56 }, 0, out var t);
        Assert.AreEqual(RivalPhase.Climb, t.lockPhase);
        Assert.AreEqual(34, t.stepsAtLock);
        Assert.AreEqual(33.0, t.lockRi, 1e-9);              // 바로 뒤 칸
        Assert.AreEqual(-1, t.lockSide);                    // 웹 값
        Assert.AreEqual(-sim.Stairs.DirAt(t.stepsAtLock - 1), t.lockSide);   // 규칙: 플레이어가 마지막으로 오른 방향의 반대쪽
        Assert.AreEqual(79, t.lockTick);
        Assert.AreEqual(98, t.doneTick);
        Assert.AreEqual(2, sim.hearts);
    }

    [Test]
    public void Rival_ParryOnLockedSide_CountsAsParry()
    {
        var sim = RivalScenario(3, new[] { 20, 32, 44, 56 }, 92, out var t);
        Assert.AreEqual(216.0, t.wx0, 1e-9);
        Assert.AreEqual(RivalPhase.Climb, t.lockPhase);
        Assert.AreEqual(1, t.lockSide);                     // 웹 값 (seed 3은 오른쪽 뒤)
        Assert.AreEqual(-sim.Stairs.DirAt(t.stepsAtLock - 1), t.lockSide);
        Assert.AreEqual(3, sim.hearts);
        Assert.AreEqual(1, sim.combo);
    }

    [Test]
    public void Rival_NotCaughtUpYet_DelaysHitUntilBehindPlayer()
    {
        var sim = RivalScenario(4, Enumerable.Range(70, 9).ToArray(), 0, out var t);   // 잠기기 직전에 9칸을 몰아 오른다
        Assert.AreEqual(39, t.stepsAtLock);
        Assert.AreEqual(RivalPhase.Climb, t.lockPhase);
        Assert.AreEqual(38.0, t.lockRi, 1e-9);
        Assert.AreEqual(-1, t.lockSide);
        Assert.AreEqual(-sim.Stairs.DirAt(38), t.lockSide);
        Assert.AreEqual(121, t.lockTick);                   // 웹 121 (원래 79에서 밀림)
        Assert.AreEqual(2.3, t.lockImpact, 1e-9);           // 웹 2.30초 (원래 1.62초)
        Assert.AreEqual(139, t.doneTick);
        Assert.AreEqual(2, sim.hearts);
    }

    [Test]
    public void Rival_TimingCue_OnlyAfterSideIsLocked()
    {
        RivalScenario(2, new[] { 20, 32, 44, 56 }, 0, out var t);
        Assert.IsFalse(t.sfx.Contains("sfx_cue_rival@before"));
        Assert.IsTrue(t.sfx.Contains("sfx_cue_rival@after"));
    }
}
