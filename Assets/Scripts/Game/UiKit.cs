// uGUI를 코드로 만드는 작은 도구. 좌표는 웹 PoC와 같은 논리 px(320x180, 왼쪽 위가 0,0, 아래가 +y)로 받는다.
// 웹의 rect/frameRect/txt/drawSprite(hud_icons)를 그대로 옮겨 쓸 수 있게 했다.
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Stairs.Game
{
    public static class UiKit
    {
        // 웹 PAL (기획서 03 팔레트)
        public static readonly Color Gray = Hex(0x5A5F66), Green = Hex(0x8A9A5B), Red = Hex(0xA13D3D), Bg = Hex(0x1E1F22), Ink = Hex(0xE8E6DF);   // Ink = 웹 PAL.text
        public static readonly Color Gold = Hex(0xE3B65C), Silver = Hex(0x9AA0A8), Dark = Hex(0x141518), RedDark = Hex(0x6E2626), RedHi = Hex(0xB84545);
        public static readonly Color OliveDark = Hex(0x4E5733), OliveText = Hex(0xA9B57C), Mid = Hex(0x6E737A), Pale = Hex(0xC9CCD1);

        public static Font font;   // GameUi가 정한다 (한글이 되는 글꼴)

        public static Color Hex(int rgb, float a = 1f) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        public static Color A(Color c, float a) { c.a = a; return c; }

        /// <summary>논리 좌표(왼쪽 위 기준)로 자리 잡은 빈 RectTransform.</summary>
        public static RectTransform Node(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>부모를 꽉 채우는 RectTransform.</summary>
        public static RectTransform Fill(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>웹의 rect: 채운 사각형.</summary>
        public static Image Box(Transform parent, float x, float y, float w, float h, Color c, string name = "Rect")
        {
            var img = Node(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>웹의 frameRect: 1px 테두리 (네 변).</summary>
        public static RectTransform Frame(Transform parent, float x, float y, float w, float h, Color c, string name = "Frame")
        {
            var rt = Node(parent, name, x, y, w, h);
            Box(rt, 0, 0, w, 1, c, "T"); Box(rt, 0, h - 1, w, 1, c, "B");
            Box(rt, 0, 0, 1, h, c, "L"); Box(rt, w - 1, 0, 1, h, c, "R");
            return rt;
        }

        /// <summary>웹의 plate: 05-3 컨셉의 이중 테두리 명판.</summary>
        public static RectTransform Plate(Transform parent, float x, float y, float w, float h)
        {
            var bg = Box(parent, x, y, w, h, A(Bg, 0.92f), "Plate");
            Frame(bg.transform, 0, 0, w, h, Silver);
            Frame(bg.transform, 2, 2, w - 4, h - 4, Gray);
            return bg.rectTransform;
        }

        /// <summary>웹의 txt. (cx, cy)는 align에 따라 왼쪽·가운데·오른쪽, 세로는 가운데 기준.</summary>
        public static Text Label(Transform parent, string s, float cx, float cy, int size, Color c, TextAnchor align = TextAnchor.MiddleCenter, bool bold = false, float w = 300, bool shadow = true)
        {
            float x = align == TextAnchor.MiddleLeft || align == TextAnchor.UpperLeft ? cx : align == TextAnchor.MiddleRight ? cx - w : cx - w / 2;
            var rt = Node(parent, "Text", x, cy - size, w, size * 2);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.color = c; t.text = s;
            t.alignment = align == TextAnchor.UpperLeft ? TextAnchor.MiddleLeft : align;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (shadow) { var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = A(Bg, 0.8f); sh.effectDistance = new Vector2(0.5f, -0.5f); }
            return t;
        }

        /// <summary>스프라이트 한 장 (hud_icons 아이콘 등). sprite가 없으면 fallback 색 사각형.</summary>
        public static Image Icon(Transform parent, Sprite sprite, float x, float y, float w, float h, Color fallback, string name = "Icon")
        {
            var img = Box(parent, x, y, w, h, sprite ? Color.white : fallback, name);
            img.sprite = sprite;
            img.preserveAspect = true;
            return img;
        }

        /// <summary>웹의 btn + drawButtons의 한 칸. 클릭(EventSystem)과 키보드 포커스 둘 다 쓴다.</summary>
        public sealed class UiButton
        {
            public Image fill; public RectTransform frame; public Text label; public Button button;
            public bool red; public Action onClick;

            public void SetFocus(bool f)
            {
                fill.color = red ? (f ? RedHi : Red) : (f ? Green : Gray);
                foreach (var g in frame.GetComponentsInChildren<Image>(true)) g.color = f ? Ink : Bg;
                label.color = f && !red ? Bg : Ink;
            }
        }

        public static UiButton MakeButton(Transform parent, string text, float x, float y, float w, float h, int size, bool red, Action onClick)
        {
            var b = new UiButton { red = red, onClick = onClick };
            b.fill = Box(parent, x, y, w, h, red ? Red : Gray, "Button " + text);
            b.fill.raycastTarget = true;
            b.frame = Frame(b.fill.transform, 0, 0, w, h, Bg);
            if (red) Frame(b.fill.transform, 1, 1, w - 2, h - 2, RedDark);
            b.label = Label(b.fill.transform, text, w / 2, h / 2 + 0.5f, size, Ink, TextAnchor.MiddleCenter, false, w);
            b.button = b.fill.gameObject.AddComponent<Button>();
            b.button.transition = Selectable.Transition.None;
            // 키보드는 GameController가 직접 다룬다. EventSystem이 버튼을 "선택"해 두면 Enter가 Submit으로 한 번 더 눌려
            // (클릭한 뒤 Enter → 두 번 실행) 갈팡질팡했으므로, 방향키 이동을 끄고 눌린 뒤 선택을 바로 푼다
            b.button.navigation = new Navigation { mode = Navigation.Mode.None };
            b.button.onClick.AddListener(() =>
            {
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
                b.onClick?.Invoke();
            });
            b.SetFocus(false);
            return b;
        }
    }
}
