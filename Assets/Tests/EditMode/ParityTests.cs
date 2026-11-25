// 웹 PoC(demo_api/game.js)와 같은 결과가 나오는지 확인한다. 기대값은 웹 코드를 node로 돌려 뽑았다 (2026-11-25).
// Window > General > Test Runner > EditMode 에서 돌린다.
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
    public void ShockCycle_LiveLastHalfSecondOfEveryThree()
    {
        var s = new StairLayout(new StairsConfig(), 1, GameMode.Normal);
        Assert.IsFalse(s.Shock(2.4f).live);
        Assert.IsTrue(s.Shock(2.4f).warn);
        Assert.IsTrue(s.Shock(2.6f).live);
        Assert.IsFalse(s.Shock(3.1f).live);
        Assert.AreEqual(2.0f, s.Shock(0.5f).shockIn, 1e-4);
    }

    [Test]
    public void StandingOnWetTile_EndsRunWhenCurrentFlows()
    {
        var sim = new RunSimulation(new StairsConfig(), 1, GameMode.Normal);
        sim.steps = 176; sim.floor = sim.Stairs.FloorOfSteps(176); sim.hearts = 99; sim.days = 99; sim.nextAttack = 999;
        for (int i = 0; i < 60 * 3 && !sim.Ended; i++) sim.Tick(1f / 60f);
        Assert.IsTrue(sim.Ended);
        Assert.AreEqual("shock", sim.ended.cause);
        Assert.AreEqual("shock", sim.ended.tip);
        Assert.GreaterOrEqual(sim.time, 2.5f);   // 흐르기 전(0~2.5초)에는 살아 있다
    }

    [Test]
    public void Bot_ReachesFloor100AsCeo()
    {
        var sim = new RunSimulation(new StairsConfig(), 1, GameMode.Normal);
        for (int i = 0; i < 60 * 600 && !sim.Ended; i++) { sim.BotTick(1f / 60f); sim.Tick(1f / 60f); }
        Assert.IsTrue(sim.Ended);
        Assert.IsTrue(sim.ended.promoted, "봇이 " + sim.ended.cause + "로 " + sim.ended.floor + "층에서 끝남");
        Assert.AreEqual(100, sim.ended.floor);
        Assert.AreEqual("CEO", sim.RankName);
    }
}
