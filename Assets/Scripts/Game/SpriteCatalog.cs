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

        /// <summary>시트 id, 애니메이션, 변형(행 이름 또는 번호), 시간 t(초)의 스프라이트. 없으면 null.</summary>
        public Sprite Get(string id, string anim, string variant, float t)
        {
            if (sheetMap == null) Build();
            if (!sheetMap.TryGetValue(id, out var s) || s.sprites == null) return null;
            var a = s.anims.Find(x => x.name == anim) ?? (s.anims.Count > 0 ? s.anims[0] : null);
            if (a == null || a.frames == null || a.frames.Length == 0) return null;
            int col = a.frames[(int)Mathf.Floor(t * a.fps) % a.frames.Length];
            int row = 0;
            if (!string.IsNullOrEmpty(variant) && s.variants != null)
            {
                int idx = Array.IndexOf(s.variants, variant);
                if (idx >= 0) row = idx; else int.TryParse(variant, out row);
            }
            int k = Mathf.Clamp(row, 0, s.rows - 1) * s.cols + col;
            return k >= 0 && k < s.sprites.Length ? s.sprites[k] : null;
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
