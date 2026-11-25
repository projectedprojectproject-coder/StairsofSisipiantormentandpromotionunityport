// 웹 파이프라인이 만든 에셋(output/build/assets)을 Unity로 가져온다.
// Designer → Publisher가 만든 PNG 시트·배경·ogg와 Developer의 sprite_definition.json을 그대로 쓰므로,
// 에이전트 파이프라인을 다시 돌린 뒤 이 메뉴만 누르면 Unity 판도 같은 그림과 소리가 된다.
//   Stairs > Import Web Build Assets : 복사, 픽셀 아트 설정(Point, 압축 없음, PPU 32), 시트 자르기, SpriteCatalog 만들기
//   Stairs > Create Game Scene       : GameController가 연결된 Assets/Scenes/Game.unity
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Stairs.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Stairs.EditorTools
{
    public static class StairsAssetImporter
    {
        const string Dst = "Assets/Imported";
        const float PPU = 32f;   // 에셋 px 32 = 1 유닛 (논리 16px, 2배 밀도)
        static readonly HashSet<string> FeetPivot = new HashSet<string> { "player", "rival", "commuter", "props" };

        static string DefaultSource => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../output/build/assets"));

        [MenuItem("Stairs/Import Web Build Assets")]
        public static void ImportMenu()
        {
            string src = DefaultSource;
            if (!File.Exists(Path.Combine(src, "sprite_definition.json")))
            {
                src = EditorUtility.OpenFolderPanel("웹 빌드의 assets 폴더 (sprite_definition.json이 있는 곳)", src, "");
                if (string.IsNullOrEmpty(src)) return;
            }
            Import(src);
        }

        public static SpriteCatalog Import(string src)
        {
            var def = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(Path.Combine(src, "sprite_definition.json")));
            Directory.CreateDirectory(Dst + "/Sprites"); Directory.CreateDirectory(Dst + "/Images"); Directory.CreateDirectory(Dst + "/Audio");

            var sprites = (Dictionary<string, object>)def["sprites"];
            var images = (Dictionary<string, object>)def["images"];
            var sounds = (Dictionary<string, object>)def["sounds"];

            foreach (var kv in sprites) Copy(src, Str(kv.Value, "file"), Dst + "/Sprites");
            foreach (var kv in images) Copy(src, Str(kv.Value, "file"), Dst + "/Images");
            foreach (var kv in sounds)
            {
                var files = (Dictionary<string, object>)((Dictionary<string, object>)kv.Value)["files"];
                string f = files.ContainsKey("ogg") ? (string)files["ogg"] : (string)files.Values.First();
                Copy(src, f, Dst + "/Audio");
            }
            AssetDatabase.Refresh();

            var catalog = LoadOrCreate<SpriteCatalog>(Dst + "/SpriteCatalog.asset");
            catalog.pixelsPerUnit = PPU;
            catalog.sheets.Clear(); catalog.images.Clear(); catalog.sounds.Clear();

            foreach (var kv in sprites)
            {
                var e = (Dictionary<string, object>)kv.Value;
                string path = Dst + "/Sprites/" + Str(e, "file");
                int fw = Int(e, "frame_w"), fh = Int(e, "frame_h");
                var sheet = SliceSheet(kv.Key, path, fw, fh, FeetPivot.Contains(kv.Key) ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f));
                var variants = (Dictionary<string, object>)e["variants"];
                sheet.variants = new string[sheet.rows];
                foreach (var v in variants) { int r = (int)(double)v.Value; if (r < sheet.rows) sheet.variants[r] = v.Key; }
                foreach (var a in (Dictionary<string, object>)e["animations"])
                {
                    var ad = (Dictionary<string, object>)a.Value;
                    sheet.anims.Add(new SpriteCatalog.Anim
                    {
                        name = a.Key,
                        fps = ad.ContainsKey("fps") ? (float)(double)ad["fps"] : 1f,
                        frames = ((List<object>)ad["frames"]).Select(x => (int)(double)x).ToArray(),
                    });
                }
                catalog.sheets.Add(sheet);
            }
            foreach (var kv in images)
            {
                string path = Dst + "/Images/" + Str(kv.Value, "file");
                SetPixelArt(path, SpriteImportMode.Single, null);
                catalog.images.Add(new SpriteCatalog.Image { id = kv.Key, sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path) });
            }
            foreach (var kv in sounds)
            {
                var e = (Dictionary<string, object>)kv.Value;
                var files = (Dictionary<string, object>)e["files"];
                string f = files.ContainsKey("ogg") ? (string)files["ogg"] : (string)files.Values.First();
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Dst + "/Audio/" + f);
                catalog.sounds.Add(new SpriteCatalog.Sound { id = kv.Key, clip = clip, loop = e.ContainsKey("loop") && (bool)e["loop"] });
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Stairs] 가져오기 완료: 시트 {catalog.sheets.Count}, 이미지 {catalog.images.Count}, 소리 {catalog.sounds.Count} ({src})");
            return catalog;
        }

        static SpriteCatalog.Sheet SliceSheet(string id, string path, int fw, int fh, Vector2 pivot)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            int cols = Mathf.Max(1, tex.width / fw), rows = Mathf.Max(1, tex.height / fh);
            var metas = new List<SpriteMetaData>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    metas.Add(new SpriteMetaData
                    {
                        name = $"{id}_{r}_{c}",
                        rect = new Rect(c * fw, tex.height - (r + 1) * fh, fw, fh),   // 웹은 위에서부터, Unity는 아래에서부터
                        alignment = (int)SpriteAlignment.Custom,
                        pivot = pivot,
                    });
            SetPixelArt(path, SpriteImportMode.Multiple, metas.ToArray());
            var all = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
            var arr = new Sprite[rows * cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    all.TryGetValue($"{id}_{r}_{c}", out arr[r * cols + c]);
            return new SpriteCatalog.Sheet { id = id, frameW = fw, frameH = fh, cols = cols, rows = rows, sprites = arr };
        }

        static void SetPixelArt(string path, SpriteImportMode mode, SpriteMetaData[] metas)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = mode;
            ti.spritePixelsPerUnit = PPU;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
#pragma warning disable 618   // Unity 6에서 spritesheet는 ISpriteEditorDataProvider 권장이지만 아직 동작한다
            if (metas != null) ti.spritesheet = metas;
#pragma warning restore 618
            ti.SaveAndReimport();
        }

        [MenuItem("Stairs/Create Game Scene")]
        public static void CreateScene()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Dst + "/SpriteCatalog.asset");
            if (!catalog && EditorUtility.DisplayDialog("Stairs", "SpriteCatalog가 없습니다. 웹 빌드 에셋을 먼저 가져올까요?", "가져오기", "그냥 만들기"))
            {
                ImportMenu();
                catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Dst + "/SpriteCatalog.asset");
            }
            Directory.CreateDirectory("Assets/Settings"); Directory.CreateDirectory("Assets/Scenes");
            var config = LoadOrCreate<GameConfigAsset>("Assets/Settings/GameConfig.asset");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>(); cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.AddComponent<AudioListener>();
            var go = new GameObject("Stairs Game");
            var gc = go.AddComponent<GameController>();
            var ad = go.AddComponent<AudioDirector>();
            gc.catalog = catalog; gc.configAsset = config; gc.audioDirector = ad; ad.catalog = catalog;
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Game.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Game.unity", true) };
            Debug.Log("[Stairs] Assets/Scenes/Game.unity 를 만들었습니다. Play를 누르세요.");
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static void Copy(string src, string file, string dstDir) => File.Copy(Path.Combine(src, file), Path.Combine(dstDir, file), true);
        static string Str(object o, string k) => (string)((Dictionary<string, object>)o)[k];
        static int Int(Dictionary<string, object> o, string k) => (int)(double)o[k];
    }
}
