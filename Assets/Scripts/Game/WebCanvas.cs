// 웹 PoC의 2D 캔버스(game.js의 rect/frameRect/txt/drawSprite/drawImg와 ctx.save/translate/rotate/scale/globalAlpha)를
// Unity GL 즉시 그리기로 옮겼다. 좌표는 웹과 같은 논리 px(320x180, 왼쪽 위 0,0, 아래가 +y).
// GameController가 game.js의 그리기 함수를 이 위에서 한 줄씩 옮겨 쓰므로 화면이 웹과 같은 순서·같은 자리로 그려진다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stairs.Game
{
    public sealed class WebCanvas
    {
        public const float W = 320, H = 180;

        static readonly Dictionary<string, Color> colorCache = new Dictionary<string, Color>();   // 아래 PAL보다 먼저 만들어져야 한다

        // 웹 PAL
        public static readonly Color Gray = Hex("#5A5F66"), Green = Hex("#8A9A5B"), Red = Hex("#A13D3D"), Bg = Hex("#1E1F22"), Text = Hex("#E8E6DF");
        public static Color Hex(string s)
        {
            if (colorCache.TryGetValue(s, out var c)) return c;
            ColorUtility.TryParseHtmlString(s, out c);
            colorCache[s] = c;
            return c;
        }

        struct M   // 2D 어파인 행렬 (캔버스 setTransform의 a b c d e f)
        {
            public float a, b, c, d, e, f;
            public static M Identity => new M { a = 1, d = 1 };
            public M Mul(M n) => new M   // this * n
            {
                a = a * n.a + c * n.b, b = b * n.a + d * n.b,
                c = a * n.c + c * n.d, d = b * n.c + d * n.d,
                e = a * n.e + c * n.f + e, f = b * n.e + d * n.f + f,
            };
            public Vector2 Apply(float x, float y) => new Vector2(a * x + c * y + e, b * x + d * y + f);
        }
        struct State { public M m; public float alpha; public bool gray; }

        readonly Material spriteMat, grayMat, textMat;
        readonly Texture2D white;
        readonly Stack<State> stack = new Stack<State>();
        State st;
        Material curMat; Texture curTex; bool open;
        Font font, fontBold;   // 브라우저처럼 굵은 글자는 굵은 글꼴 파일로 (가변 글꼴은 Unity가 굵기를 못 고른다)
        float pxScale = 1;   // 화면 픽셀 / 논리 px (글자를 화면 해상도로 뽑는다)
        public bool fontDirty;

        public float globalAlpha { get => st.alpha; set => st.alpha = value; }
        /// <summary>CSS filter "grayscale(1) brightness(0.85) contrast(0.85)" (결과 화면의 구조조정 실루엣)</summary>
        public bool grayFilter { get => st.gray; set => st.gray = value; }

        public WebCanvas(Font f, Font bold = null)
        {
            var sh = Resources.Load<Shader>("StairsCanvas");
            spriteMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            grayMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave }; grayMat.SetFloat("_Gray", 1);
            textMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave }; textMat.SetFloat("_Text", 1);
            white = Texture2D.whiteTexture;
            font = f; fontBold = bold;
            Font.textureRebuilt += OnRebuilt;
        }
        void OnRebuilt(Font f) { if (f == font || f == fontBold) fontDirty = true; }
        Font F(bool bold) => bold && fontBold ? fontBold : font;
        FontStyle Style(bool bold) => bold && !fontBold ? FontStyle.Bold : FontStyle.Normal;
        public void Dispose() { Font.textureRebuilt -= OnRebuilt; }

        /// <summary>한 장면 그리기 시작. viewport는 화면 픽셀(왼쪽 아래 원점), scale은 웹 resize()의 배율.</summary>
        public void Begin(Rect viewport, float scale)
        {
            pxScale = scale;
            GL.PushMatrix();
            GL.Viewport(viewport);
            GL.LoadPixelMatrix(0, W, H, 0);   // 웹처럼 y가 아래로 커진다
            st = new State { m = M.Identity, alpha = 1 };
            stack.Clear();
            curMat = null; curTex = null; open = false;
        }
        public void End() { Flush(); GL.PopMatrix(); }

        // ---- ctx.save/restore/translate/rotate/scale ----
        public void Save() => stack.Push(st);
        public void Restore() { if (stack.Count > 0) st = stack.Pop(); }
        public void Translate(float x, float y) => st.m = st.m.Mul(new M { a = 1, d = 1, e = x, f = y });
        public void Rotate(float r) { float cs = Mathf.Cos(r), sn = Mathf.Sin(r); st.m = st.m.Mul(new M { a = cs, b = sn, c = -sn, d = cs }); }
        public void Scale(float x, float y) => st.m = st.m.Mul(new M { a = x, d = y });

        void Use(Material m, Texture t)
        {
            if (open && m == curMat && t == curTex) return;
            Flush();
            m.mainTexture = t;
            m.SetPass(0);
            GL.Begin(GL.QUADS);
            curMat = m; curTex = t; open = true;
        }
        void Flush() { if (open) { GL.End(); open = false; } }

        void Quad(float x, float y, float w, float h, Color c, float u0, float v0, float u1, float v1)
        {
            c.a *= st.alpha;
            GL.Color(c);
            Vector2 p0 = st.m.Apply(x, y), p1 = st.m.Apply(x + w, y), p2 = st.m.Apply(x + w, y + h), p3 = st.m.Apply(x, y + h);
            GL.TexCoord2(u0, v1); GL.Vertex3(p0.x, p0.y, 0);
            GL.TexCoord2(u1, v1); GL.Vertex3(p1.x, p1.y, 0);
            GL.TexCoord2(u1, v0); GL.Vertex3(p2.x, p2.y, 0);
            GL.TexCoord2(u0, v0); GL.Vertex3(p3.x, p3.y, 0);
        }

        static float JsRound(float v) => Mathf.Floor(v + 0.5f);

        /// <summary>웹 ctx.fillRect (반올림 없이).</summary>
        public void Fill(float x, float y, float w, float h, Color c)
        {
            Use(spriteMat, white);
            Quad(x, y, w, h, c, 0, 0, 1, 1);
        }

        /// <summary>웹 rect(x, y, w, h, c, a): 반올림해서 채우고, a를 주면 globalAlpha를 a로 했다가 1로 되돌린다.</summary>
        public void Rect(float x, float y, float w, float h, Color c, float? a = null)
        {
            if (a.HasValue) st.alpha = a.Value;
            Fill(JsRound(x), JsRound(y), JsRound(w), JsRound(h), c);
            if (a.HasValue) st.alpha = 1;
        }
        public void Rect(float x, float y, float w, float h, string c, float? a = null) => Rect(x, y, w, h, Hex(c), a);

        public void FrameRect(float x, float y, float w, float h, Color c)
        {
            Rect(x, y, w, 1, c); Rect(x, y + h - 1, w, 1, c); Rect(x, y, 1, h, c); Rect(x + w - 1, y, 1, h, c);
        }
        public void FrameRect(float x, float y, float w, float h, string c) => FrameRect(x, y, w, h, Hex(c));

        /// <summary>ctx.drawImage(sprite의 사각형, dx, dy, dw, dh). 회전·뒤집기는 지금 행렬을 따른다.</summary>
        public void DrawImage(Sprite s, float dx, float dy, float dw, float dh)
        {
            if (!s) return;
            // 시트의 한 칸 전체(sprite.rect). textureRect는 Unity가 투명한 가장자리를 잘라 낸 사각형이라, 그것을 칸 크기로 늘리면
            // 캐릭터가 1.6배쯤 뚱뚱해지고 프레임마다 잘린 폭이 달라 애니메이션이 흔들렸다 (2026-11-25)
            var tex = s.texture; var r = s.rect;
            Use(st.gray ? grayMat : spriteMat, tex);
            Quad(dx, dy, dw, dh, Color.white, r.xMin / tex.width, r.yMin / tex.height, r.xMax / tex.width, r.yMax / tex.height);
        }

        // ---- 글자 (웹 txt: ctx.font = (bold) size px "Noto Sans KR", sans-serif) ----
        public enum Align { Left, Center, Right }
        public enum Base { Top, Middle }
        const float Ascent = 0.88f;   // em 위쪽에서 기준선까지 (본고딕·맑은 고딕 계열)

        public float Measure(string s, int size, bool bold)
        {
            var font = F(bold);
            if (!font || string.IsNullOrEmpty(s)) return 0;
            int ps = Mathf.Max(1, Mathf.RoundToInt(size * pxScale));
            var style = Style(bold);
            font.RequestCharactersInTexture(s, ps, style);
            float w = 0;
            foreach (char ch in s) if (font.GetCharacterInfo(ch, out var ci, ps, style)) w += ci.advance;
            return w / pxScale;
        }

        /// <summary>웹 ctx.fillText (textAlign, textBaseline). x, y는 이미 반올림된 값을 받는다.</summary>
        public void FillText(string s, float x, float y, int size, bool bold, Align align, Base bas, Color color)
        {
            var font = F(bold);
            if (!font || string.IsNullOrEmpty(s)) return;
            int ps = Mathf.Max(1, Mathf.RoundToInt(size * pxScale));
            var style = Style(bold);
            font.RequestCharactersInTexture(s, ps, style);
            float w = Measure(s, size, bold);
            float pen = align == Align.Left ? x : align == Align.Center ? x - w / 2 : x - w;
            float baseline = bas == Base.Top ? y + size * Ascent : y + size * (Ascent - 0.5f);
            Use(textMat, font.material.mainTexture);
            foreach (char ch in s)
            {
                if (!font.GetCharacterInfo(ch, out var ci, ps, style)) continue;
                float gx = pen + ci.minX / pxScale, gy = baseline - ci.maxY / pxScale;
                float gw = (ci.maxX - ci.minX) / pxScale, gh = (ci.maxY - ci.minY) / pxScale;
                if (gw > 0 && gh > 0)
                {
                    var c = color; c.a *= st.alpha;
                    GL.Color(c);
                    Vector2 p0 = st.m.Apply(gx, gy), p1 = st.m.Apply(gx + gw, gy), p2 = st.m.Apply(gx + gw, gy + gh), p3 = st.m.Apply(gx, gy + gh);
                    GL.TexCoord(ci.uvTopLeft); GL.Vertex3(p0.x, p0.y, 0);
                    GL.TexCoord(ci.uvTopRight); GL.Vertex3(p1.x, p1.y, 0);
                    GL.TexCoord(ci.uvBottomRight); GL.Vertex3(p2.x, p2.y, 0);
                    GL.TexCoord(ci.uvBottomLeft); GL.Vertex3(p3.x, p3.y, 0);
                }
                pen += ci.advance / pxScale;
            }
        }

        /// <summary>미리 글자를 글꼴 텍스처에 올려 둔다 (그리는 도중 텍스처가 다시 만들어져 한 프레임 깨지는 것을 줄인다).</summary>
        public void Warm(string chars, int[] sizes)
        {
            foreach (int s in sizes)
            {
                int ps = Mathf.Max(1, Mathf.RoundToInt(s * pxScale));
                if (F(false)) F(false).RequestCharactersInTexture(chars, ps, Style(false));
                if (F(true)) F(true).RequestCharactersInTexture(chars, ps, Style(true));
            }
        }
        public void SetScale(float s) => pxScale = s;
    }
}
