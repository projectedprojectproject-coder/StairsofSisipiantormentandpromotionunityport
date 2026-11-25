// 컷신 재생 (웹 game.js의 컷신: playCutscene / updateCutscene / drawCutscene / drawCsCard 를 한 줄씩 옮김).
// 대본은 CutsceneData.cs (웹 CUTSCENES에서 Tools~/cutscenes/gen_cs_data.py로 만든다).
using System;
using System.Collections.Generic;
using Stairs.Core;
using UnityEngine;

namespace Stairs.Game
{
    public sealed class CsCam { public double t, x, y, z; public string e; public CsCam(double t, double x, double y, double z, string e = "l") { this.t = t; this.x = x; this.y = y; this.z = z; this.e = e; } }
    public sealed class CsKey
    {
        public double t, x, y, sx, sy, r; public int[] f; public bool cycle; public string e;
        public CsKey(double t, int[] f, bool cycle, double x, double y, double sx, double sy, double r, string e = "l") { this.t = t; this.f = f; this.cycle = cycle; this.x = x; this.y = y; this.sx = sx; this.sy = sy; this.r = r; this.e = e; }
    }
    public sealed class CsAct { public string s; public double z = 1, fps = 6; public bool once, flip; public CsKey[] k; }
    public sealed class CsCap { public double t0, t1; public string text, kind; public CsCap(double t0, double t1, string text, string kind) { this.t0 = t0; this.t1 = t1; this.text = text; this.kind = kind; } }
    public sealed class CsFx
    {
        public double t, a, d; public string kind, id, color;
        public CsFx(double t, string kind, string id = null, double a = 0, double d = 0, string color = null) { this.t = t; this.kind = kind; this.id = id; this.a = a; this.d = d; this.color = color; }
    }
    public sealed class CsShot
    {
        public double d, bw, bh, fin, fout; public string bg, fg, card, finc, foutc;
        public CsCam[] cam; public CsAct[] act = new CsAct[0]; public CsCap[] cap = new CsCap[0]; public CsFx[] fx = new CsFx[0];
    }

    public partial class GameController
    {
        sealed class Cutscene { public string id; public double t; public Action onEnd; public bool freeze; public HashSet<string> fired = new HashSet<string>(); }
        Cutscene cs;

        static double CsEase(string e, double u)
        {
            switch (e)
            {
                case "o": return 1 - Math.Pow(1 - u, 3);
                case "i": return u * u * u;
                case "io": return u < 0.5 ? 4 * u * u * u : 1 - Math.Pow(-2 * u + 2, 3) / 2;
                case "b": { const double c = 1.9; return 1 + (c + 1) * Math.Pow(u - 1, 3) + c * Math.Pow(u - 1, 2); }
                default: return u;
            }
        }
        static double CsTotal(string id) { double s = 0; foreach (var sh in CutsceneData.All[id]) s += sh.d; return s; }
        public string CutsceneId => cs?.id;
        public double CutsceneTime => cs?.t ?? 0;

        /// <summary>웹 playCutscene.</summary>
        public void PlayCutscene(string id, Action onEnd) { cs = new Cutscene { id = id, t = 0, onEnd = onEnd }; screen = Screen.Cutscene; focus = 0; Debug.Log("[stairs] cutscene " + id); }
        /// <summary>QA·플레이테스트: 컷신의 한 순간에 멈춘다 (웹 ?cs=..&cst=..&csfreeze=1).</summary>
        public void FreezeCutscene(string id, double t) { PlayCutscene(id, ToTitle); cs.t = t; cs.freeze = true; }
        void EndCutscene() { var f = cs?.onEnd; cs = null; f?.Invoke(); }
        public void SkipCutscene() { if (cs == null) return; Debug.Log("[stairs] cutscene skip " + cs.id); Sfx("sfx_click"); EndCutscene(); }
        /// <summary>보통 모드 새 판: 짧은 시작 컷신 (건너뛸 수 있다). 웹 startNormal.</summary>
        public void StartNormal() => PlayCutscene("runstart", () => StartRun(GameMode.Normal));

        (CsShot sh, double t) CsShotAt(double t)
        {
            var shots = CutsceneData.All[cs.id]; double t0 = 0;
            for (int i = 0; i < shots.Length; i++) { if (t < t0 + shots[i].d || i == shots.Length - 1) return (shots[i], t - t0); t0 += shots[i].d; }
            return (shots[shots.Length - 1], 0);
        }

        void UpdateCutscene(double dt)
        {
            if (cs == null) { screen = Screen.Title; return; }
            if (cs.freeze) return;
            double prev = cs.t; cs.t += dt;
            var shots = CutsceneData.All[cs.id]; double t0 = 0;
            for (int i = 0; i < shots.Length; i++)
            {
                for (int j = 0; j < shots[i].fx.Length; j++)
                {
                    var f = shots[i].fx[j]; double at = t0 + f.t; string key = i + ":" + j;
                    if (f.kind == "sfx" && !cs.fired.Contains(key) && prev <= at && cs.t > at) { cs.fired.Add(key); Sfx(f.id); }
                }
                t0 += shots[i].d;
            }
            if (cs.t >= CsTotal(cs.id)) EndCutscene();
        }

        struct CsPose { public int[] f; public bool cycle; public double x, y, sx, sy, r, t0; }
        static CsPose? CsKeys(CsKey[] k, double t)
        {
            if (t < k[0].t) return null;
            int i = 0; while (i + 1 < k.Length && t >= k[i + 1].t) i++;
            var a = k[i];
            if (i + 1 >= k.Length) return new CsPose { f = a.f, cycle = a.cycle, x = a.x, y = a.y, sx = a.sx, sy = a.sy, r = a.r, t0 = a.t };
            var b = k[i + 1]; double u = CsEase(b.e, Clamp((t - a.t) / Math.Max(1e-6, b.t - a.t), 0, 1));
            return new CsPose { f = a.f, cycle = a.cycle, x = Lerp(a.x, b.x, u), y = Lerp(a.y, b.y, u), sx = Lerp(a.sx, b.sx, u), sy = Lerp(a.sy, b.sy, u), r = Lerp(a.r, b.r, u), t0 = a.t };
        }
        static (double x, double y, double z) CsCamAt(CsCam[] cam, double t)
        {
            int i = 0; while (i + 1 < cam.Length && t >= cam[i + 1].t) i++;
            var a = cam[i]; if (i + 1 >= cam.Length) return (a.x, a.y, a.z);
            var b = cam[i + 1]; double u = CsEase(b.e, Clamp((t - a.t) / Math.Max(1e-6, b.t - a.t), 0, 1));
            return (Lerp(a.x, b.x, u), Lerp(a.y, b.y, u), Lerp(a.z, b.z, u));
        }
        void DrawCsActor(CsAct a, double t)
        {
            var kk = CsKeys(a.k, t); if (!kk.HasValue) return; var k = kk.Value;
            int f;
            if (k.cycle) { int n = (int)Math.Floor((t - (a.once ? a.k[0].t : 0)) * a.fps); f = a.once ? k.f[Math.Min(k.f.Length - 1, n)] : k.f[((n % k.f.Length) + k.f.Length) % k.f.Length]; }
            else f = k.f[0];
            if (f < 0) return;
            var sheet = catalog ? catalog.GetSheet(a.s) : null; double fh = sheet != null ? sheet.frameH / 2.0 * a.z : 0;   // 2배 밀도: 논리 높이 = 프레임/2
            // 발(아래 가운데)을 기준으로 눌리고 늘어나게
            DrawSprite(a.s, "default", "f" + f, 0, k.x, k.y - fh * (k.sy - 1) / 2, new SO { scale = 2 * a.z, sx = k.sx, sy = k.sy, rot = k.r, flip = a.flip, nofb = true });
        }
        static double CsHash(int i) { double x = Math.Sin(i * 127.1 + 311.7) * 43758.5453; return x - Math.Floor(x); }

        void DrawCutscene()
        {
            if (cs == null) return;
            var (sh, t) = CsShotAt(cs.t);
            Rect(0, 0, W, H, "#0B0C0E");
            double shx = 0, shy = 0;
            foreach (var f in sh.fx) if (f.kind == "shake" && t >= f.t && t < f.t + f.d) { double k = 1 - (t - f.t) / f.d; shx += Math.Sin(t * 73) * f.a * k; shy += Math.Cos(t * 61) * f.a * k * 0.7; }
            cv.Save(); cv.Translate(JsRound(shx), JsRound(shy));
            if (sh.bg != null)
            {
                var c = CsCamAt(sh.cam, t);
                cv.Save(); cv.Translate(160, 90); cv.Scale((float)c.z, (float)c.z); cv.Translate((float)-c.x, (float)-c.y);
                DrawImg(sh.bg, 0, 0, sh.bw, sh.bh, Hex("#1A2224"));
                foreach (var a in sh.act) DrawCsActor(a, t);
                if (sh.fg != null) DrawImg(sh.fg, 0, 0, sh.bw, sh.bh, null);
                foreach (var f in sh.fx)
                {
                    if (f.kind != "debris" || t < f.t || t > f.t + f.d) continue;
                    double e = t - f.t;
                    for (int i = 0; i < 26; i++)
                    {
                        double vx = (CsHash(i) - 0.5) * 140, vy = -40 - CsHash(i + 50) * 90, x = 160 + vx * e, y = 40 + vy * e + 110 * e * e;
                        if (y > 200) continue;
                        Rect(x, y, 2 + (i % 3), 2 + (i % 2), i % 4 != 0 ? "#3A3536" : "#E8A050");
                    }
                }
                cv.Restore();
            }
            if (sh.card == "title" || sh.card == "go") DrawCsCard(sh.card, t);
            cv.Restore();
            foreach (var f in sh.fx)   // 집중선
            {
                if (f.kind != "lines" || t < f.t || t > f.t + f.d) continue;
                for (int i = 0; i < 22; i++)
                {
                    double a = i / 22.0 * Math.PI * 2 + CsHash(i) * 0.2, len = 30 + CsHash(i + 9) * 50, r0 = 150 - ((t * 400 + CsHash(i + 3) * 200) % 60);
                    cv.Save(); cv.Translate((float)(160 + Math.Cos(a) * r0), (float)(90 + Math.Sin(a) * r0 * 0.6)); cv.Rotate((float)a);
                    Rect(0, -0.5, len, 1, "#FFFFFF", 0.55); cv.Restore();
                }
            }
            foreach (var f in sh.fx) if (f.kind == "flash" && t >= f.t && t < f.t + f.d) Rect(0, 0, W, H, f.color, 0.9 * (1 - (t - f.t) / f.d));
            if (sh.fin > 0 && t < sh.fin) Rect(0, 0, W, H, sh.finc ?? "#000000", 1 - t / sh.fin);
            if (sh.fout > 0 && t > sh.d - sh.fout) Rect(0, 0, W, H, sh.foutc ?? "#000000", Clamp((t - (sh.d - sh.fout)) / sh.fout, 0, 1));
            Rect(0, 0, W, 14, "#000000"); Rect(0, 154, W, 26, "#000000");
            foreach (var c in sh.cap)
            {
                if (t < c.t0 || t > c.t1) continue;   // 자막은 말과 설명만 (소리 효과는 글자로 쓰지 않는다)
                int n = Math.Min(c.text.Length, (int)Math.Floor((t - c.t0) * 26) + 1);
                Txt(c.text.Substring(0, n), 160, 167, T(8, "center", "middle", bold: c.kind == "s", color: Hex(c.kind == "s" ? "#F2C46A" : "#E8E6DF")));
            }
            Txt("Enter · 건너뛰기", 314, 4, T(6, "right", color: Hex("#6E737A"), shadow: false));
        }

        void DrawCsCard(string kind, double t)
        {
            Rect(0, 0, W, H, "#141518");
            double k = Clamp((t - 0.25) / 0.3, 0, 1), sc = kind == "go" ? 1 + (1 - CsEase("b", Clamp(t / 0.25, 0, 1))) * 1.2 : 1 + (1 - CsEase("b", k)) * 1.0;
            if (kind == "title" && t < 0.25) return;
            cv.Save(); cv.Translate(160, kind == "go" ? 88 : 80); cv.Scale((float)sc, (float)sc);
            TxtO(kind == "go" ? "업무 시작!" : "승급의 계단", 0, 0, T(kind == "go" ? 22 : 24, "center", "middle", bold: true, color: kind == "go" ? Hex("#F2C46A") : PalText));
            cv.Restore();
            if (kind == "title")
            {
                double u = Clamp((t - 0.7) / 0.4, 0, 1);
                Rect(160 - 60 * u, 96, 120 * u, 2, "#C0302E");
                if (t > 0.9) Txt("대기업 생존 액션", 160, 106, T(8, "center", "middle", bold: true, color: Hex("#C0302E")));
            }
        }
    }
}
