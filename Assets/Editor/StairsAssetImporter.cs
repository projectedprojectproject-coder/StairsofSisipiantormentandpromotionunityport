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

        // 웹 빌드 에셋을 찾는 순서:
        //   1) 이 Unity 프로젝트에 함께 넣은 웹 판 사본 WebReference/assets (Unity 판만 따로 받은 저장소)
        //   2) 웹 저장소 안의 unity/StairsOfPromotion 이라면 그 저장소의 파이프라인 결과(output, output_api) → 데모(demo_api, demo)
        static readonly string[] Candidates = { "output/build/assets", "output_api/build/assets", "demo_api/assets", "demo/assets" };

        static string DefaultSource
        {
            get
            {
                string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string bundled = Path.Combine(project, "WebReference", "assets");
                if (File.Exists(Path.Combine(bundled, "sprite_definition.json"))) return bundled;
                string root = Path.GetFullPath(Path.Combine(project, "../.."));
                foreach (var c in Candidates)
                {
                    string p = Path.Combine(root, c);
                    if (File.Exists(Path.Combine(p, "sprite_definition.json"))) return p;
                }
                return bundled;
            }
        }

        [MenuItem("Stairs/Import Web Build Assets")]
        public static void ImportMenu()
        {
            string src = DefaultSource;
            if (!File.Exists(Path.Combine(src, "sprite_definition.json")))
            {
                if (Application.isBatchMode) throw new FileNotFoundException("웹 빌드 에셋(sprite_definition.json)을 찾지 못했습니다: " + src);
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
            catalog.pixelScale = def.ContainsKey("pixel_scale") ? Int(def, "pixel_scale") : 1;
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
            // 빠짐없이 들어왔는지: 정의의 모든 시트·이미지·소리, 시트의 모든 칸, 모든 동작의 프레임 번호가 그림 안에 있는지
            var missing = new List<string>();
            foreach (var sh in catalog.sheets)
            {
                for (int k = 0; k < sh.sprites.Length; k++) if (!sh.sprites[k]) missing.Add(sh.id + " 칸 " + k);
                foreach (var an in sh.anims) foreach (int f in an.frames) if (f < 0 || f >= sh.cols) missing.Add(sh.id + "." + an.name + " 프레임 " + f + " (칸 " + sh.cols + "개)");
            }
            foreach (var im in catalog.images) if (!im.sprite) missing.Add("이미지 " + im.id);
            foreach (var so in catalog.sounds) if (!so.clip) missing.Add("소리 " + so.id);
            if (catalog.sheets.Count != sprites.Count || catalog.images.Count != images.Count || catalog.sounds.Count != sounds.Count) missing.Add("정의와 개수가 다름");
            if (missing.Count > 0) Debug.LogError("[Stairs] 가져오지 못한 것: " + string.Join(", ", missing));
            Debug.Log($"[Stairs] 가져오기 완료: 시트 {catalog.sheets.Count}/{sprites.Count}, 이미지 {catalog.images.Count}/{images.Count}, 소리 {catalog.sounds.Count}/{sounds.Count}, 빠진 것 {missing.Count} ({src})");
            return catalog;
        }

        static SpriteCatalog.Sheet SliceSheet(string id, string path, int fw, int fh, Vector2 pivot)
        {
            // 실제 PNG 크기로 자른다. 처음 가져올 때(기본 텍스처 설정)는 2의 거듭제곱으로 늘려 불러오므로(240x24 → 256x32)
            // 그 크기로 자르면 자른 칸이 그림 밖으로 나가 Unity가 버린다 (2026-11-25 플레이테스트: 한 줄짜리 시트는 모두, 나머지는 한 줄씩 빠졌다)
            var (texW, texH) = PngSize(path);
            int cols = Mathf.Max(1, texW / fw), rows = Mathf.Max(1, texH / fh);
            var metas = new List<SpriteMetaData>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    metas.Add(new SpriteMetaData
                    {
                        name = $"{id}_{r}_{c}",
                        rect = new Rect(c * fw, texH - (r + 1) * fh, fw, fh),   // 웹은 위에서부터, Unity는 아래에서부터
                        alignment = (int)SpriteAlignment.Custom,
                        pivot = pivot,
                    });
            SetPixelArt(path, SpriteImportMode.Multiple, metas.ToArray());
            var all = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
            var arr = new Sprite[rows * cols];
            int missing = 0;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (!all.TryGetValue($"{id}_{r}_{c}", out arr[r * cols + c])) missing++;
            if (missing > 0) Debug.LogError($"[Stairs] {id}: {rows * cols}칸 중 {missing}칸을 자르지 못했습니다 ({texW}x{texH}, 프레임 {fw}x{fh})");
            return new SpriteCatalog.Sheet { id = id, frameW = fw, frameH = fh, cols = cols, rows = rows, sprites = arr };
        }

        /// <summary>PNG 머리(IHDR)에서 원래 가로·세로를 읽는다.</summary>
        static (int w, int h) PngSize(string path)
        {
            using (var f = File.OpenRead(path))
            {
                var b = new byte[24];
                if (f.Read(b, 0, 24) < 24) throw new IOException("PNG가 아닙니다: " + path);
                int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                int h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                return (w, h);
            }
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
            ti.npotScale = TextureImporterNPOTScale.None;   // 픽셀 아트: 크기를 늘리지 않는다
            var st = new TextureImporterSettings(); ti.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;           // 칸 전체를 쓴다 (Tight는 투명한 가장자리를 잘라 낸다)
            ti.SetTextureSettings(st);
#pragma warning disable 618   // Unity 6에서 spritesheet는 ISpriteEditorDataProvider 권장이지만 아직 동작한다
            if (metas != null) ti.spritesheet = metas;
#pragma warning restore 618
            ti.SaveAndReimport();
        }

        [MenuItem("Stairs/Create Game Scene")]
        public static void CreateScene()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Dst + "/SpriteCatalog.asset");
            if (!catalog && Application.isBatchMode) { ImportMenu(); catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Dst + "/SpriteCatalog.asset"); }
            else if (!catalog && EditorUtility.DisplayDialog("Stairs", "SpriteCatalog가 없습니다. 웹 빌드 에셋을 먼저 가져올까요?", "가져오기", "그냥 만들기"))
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

        /// <summary>
        /// 명령줄 플레이테스트: 에셋 가져오기 → 씬 만들기 → Windows 플레이어 빌드.
        ///   Unity.exe -batchmode -projectPath unity/StairsOfPromotion -executeMethod Stairs.EditorTools.StairsAssetImporter.BuildPlaytestPlayer -stairsBuild &lt;폴더&gt; -quit
        /// 만든 Stairs.exe를 -stairsPlaytest &lt;폴더&gt; 로 실행하면 PlaytestDriver가 화면마다 스크린샷을 찍고 끝낸다.
        /// </summary>
        public static void BuildPlaytestPlayer()
        {
            string outDir = Arg("-stairsBuild") ?? Path.GetFullPath("Builds/Playtest");
            Import(DefaultSource);
            CreateScene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Game.unity" },
                locationPathName = Path.Combine(outDir, "Stairs.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            Debug.Log("[Stairs] 플레이테스트 빌드: " + report.summary.result + " (" + report.summary.totalErrors + " errors) → " + outDir);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
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
