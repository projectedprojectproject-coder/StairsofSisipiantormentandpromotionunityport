// 웹 빌드의 sprite_definition.json을 Unity 에셋으로 옮긴 목록. Editor/StairsAssetImporter가 만든다.
// 시트 규칙은 웹과 같다: 행 = 변형(variant), 열 = 프레임. 애니메이션은 열 번호 목록과 fps.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stairs.Game
{
    [CreateAssetMenu(menuName = "Stairs/Sprite Catalog")]
    public class SpriteCatalog : ScriptableObject
    {
        [Serializable] public class Anim { public string name; public float fps = 1f; public int[] frames; }

        [Serializable]
        public class Sheet
        {
            public string id;
            public int frameW, frameH, cols, rows;
            public string[] variants;          // 행 이름 (예: 사원, zone1)
            public List<Anim> anims = new List<Anim>();
            public Sprite[] sprites;           // 행 * cols + 열
        }

        [Serializable] public class Image { public string id; public Sprite sprite; }
        [Serializable] public class Sound { public string id; public AudioClip clip; public bool loop; }

        public float pixelsPerUnit = 32f;      // 에셋 px. 논리 px(320x180)의 2배 밀도라 논리 16px = 1 유닛
        public int pixelScale = 2;             // sprite_definition.json의 pixel_scale (웹 PX): 에셋 px / 논리 px
        public List<Sheet> sheets = new List<Sheet>();
        public List<Image> images = new List<Image>();
        public List<Sound> sounds = new List<Sound>();

        Dictionary<string, Sheet> sheetMap;
        Dictionary<string, Image> imageMap;
        Dictionary<string, Sound> soundMap;

        void Build()
        {
            sheetMap = new Dictionary<string, Sheet>();
            foreach (var s in sheets) sheetMap[s.id] = s;
            imageMap = new Dictionary<string, Image>();
            foreach (var i in images) imageMap[i.id] = i;
            soundMap = new Dictionary<string, Sound>();
            foreach (var s in sounds) soundMap[s.id] = s;
        }

        public bool HasAnim(string id, string anim)
        {
            if (sheetMap == null) Build();
            return sheetMap.TryGetValue(id, out var s) && s.anims.Exists(a => a.name == anim);
        }

        // 웹 ONCE: 한 번만 재생하고 마지막 프레임에 멈추는 동작 (나머지는 되풀이)
        static readonly HashSet<string> Once = new HashSet<string> { "step", "parry_left", "parry_right", "hit", "box_exit", "proud", "attack", "leave" };

        /// <summary>시트 id, 애니메이션, 변형(행 이름 또는 번호), 시간 t(초)의 스프라이트. 없으면 null.</summary>
        public Sprite Get(string id, string anim, string variant, double t)
        {
            if (sheetMap == null) Build();
            if (!sheetMap.TryGetValue(id, out var s) || s.sprites == null) return null;
            // 웹 drawSprite와 같다: 없는 동작이면 0번 칸, 없는 변형이면 0번 줄
            var a = s.anims.Find(x => x.name == anim);
            int col = 0;
            if (a != null && a.frames != null && a.frames.Length > 0)
            {
                int n = (int)Math.Floor(Math.Max(0, t) * (a.fps > 0 ? a.fps : 1));   // 웹: Math.max(0, t). 음수면 배열 밖이라 예외가 났다
                n = Once.Contains(a.name) ? Math.Min(n, a.frames.Length - 1) : n % a.frames.Length;
                col = a.frames[n];
            }
            int row = 0;
            if (!string.IsNullOrEmpty(variant) && s.variants != null)
            {
                int idx = Array.IndexOf(s.variants, variant);
                if (idx >= 0) row = idx;
            }
            int k = Mathf.Clamp(row, 0, s.rows - 1) * s.cols + col;
            return k >= 0 && k < s.sprites.Length ? s.sprites[k] : null;
        }

        public Sheet GetSheet(string id)
        {
            if (sheetMap == null) Build();
            return sheetMap.TryGetValue(id, out var s) && s.sprites != null ? s : null;
        }

        public Sprite GetImage(string id)
        {
            if (imageMap == null) Build();
            return imageMap.TryGetValue(id, out var i) ? i.sprite : null;
        }

        public Sound GetSound(string id)
        {
            if (soundMap == null) Build();
            return soundMap.TryGetValue(id, out var s) ? s : null;
        }
    }
}
