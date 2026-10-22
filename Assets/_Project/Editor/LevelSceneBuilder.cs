using System.IO;
using System.Linq;
using Gstarr.CameraSystem;
using Gstarr.Combat;
using Gstarr.Items;
using Gstarr.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Gstarr.EditorTools
{
    public static class LevelSceneBuilder
    {
        private const string ProjectRoot = "Assets/_Project";
        private const string PlaceholderFolder = "Assets/arts/Placeholder";
        private const string TilesetPath = PlaceholderFolder + "/PlaceholderTileset.png";
        private const string PlayerSpritePath = PlaceholderFolder + "/PlaceholderPlayer.png";
        private const string NoFrictionPath = ProjectRoot + "/Physics/NoFriction.physicsMaterial2D";
        private const string InputActionsPath = ProjectRoot + "/Input/PlayerControls.inputactions";
        private const string TemplateScenePath = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";
        private const string ScenesFolder = "Assets/Scenes";

        private const int PlaceholderTileSize = 16;

        // 플레이스홀더 타일셋 셀 인덱스 (4열 x 2행, 왼쪽 위부터)
        private const int Grass = 0, Dirt = 1, Stone = 2, Brick = 3, Platform = 4, BackWall = 5, Crate = 6;

        [MenuItem("Tools/Gstarr/Create Level Scene", priority = 20)]
        public static void CreateLevelScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureLayer("Ground");
            EnsureLayer("Player");

            var tileset = CreatePlaceholderTileset();
            var playerSprite = LoadPlayerSprite();
            var noFriction = CreateNoFrictionMaterial();

            int stageNumber = StageAutoGenerator.NextStageNumber();
            string scenePath = $"{ScenesFolder}/{StageAutoGenerator.StageScenePrefix}{stageNumber}.unity";
            Scene scene;
            if (File.Exists(TemplateScenePath) && AssetDatabase.CopyAsset(TemplateScenePath, scenePath))
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }

            var grid = new GameObject("Grid", typeof(Grid));
            var background = CreateTilemap(grid.transform, "Background", -10, "Default");
            var ground = CreateTilemap(grid.transform, "Ground", 0, "Ground");
            var platforms = CreateTilemap(grid.transform, "Platforms (One-Way)", 0, "Ground");
            CreateTilemap(grid.transform, "Foreground", 10, "Default");

            AddSolidCollision(ground.gameObject);
            AddOneWayCollision(platforms.gameObject);

            PaintStarterLevel(tileset, background, ground, platforms);

            var player = CreatePlayer(playerSprite, noFriction, new Vector3(-2f, 0.5f, 0f));
            SetupCamera(player.transform);
            CreateParallaxBackground(new Vector3(-2f, 2f, 0f));
            EnemyActor.Spawn(null, new Vector3(8f, 0.5f, 0f), EnemyKind.BeatChaser);
            EnemyActor.Spawn(null, new Vector3(18f, 0.5f, 0f), EnemyKind.RangedBeat);
            EnemyActor.Spawn(null, new Vector3(30f, 0.5f, 0f), EnemyKind.RangedHalfBeat);
            CreateBossDoor(new Vector3(14f, 0.5f, 0f));

            // 아이템은 직접 그릴 수 있게 빈 Items 타일맵만 만들어 둠
            ItemTools.EnsureDefaultItems();
            ItemTools.BuildItemTiles();
            ItemTools.EnsureItemTilemap(grid.transform);
            EnemyTools.BuildEnemyPalette();
            var enemyTilemap = EnemyTools.EnsureEnemyTilemap(grid.transform);

            Selection.activeGameObject = ground.gameObject;

            EditorSceneManager.SaveScene(scene, scenePath);
            AddSceneToBuildSettings(scenePath);
            StageAutoGenerator.SyncAll();

            EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");
            Debug.Log($"[Level Builder] 씬 생성 완료: {scenePath}\n" +
                      "Tile Palette에서 Ground/Platforms/Background 또는 Enemies 팔레트를 고르고 해당 Tilemap에 배치하세요. " +
                      $"적 배치 타일맵: {enemyTilemap.name}");
        }

        private static Tilemap CreateTilemap(Transform parent, string name, int sortingOrder, string layer)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer(layer);
            var renderer = go.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.mode = TilemapRenderer.Mode.Chunk;
            return go.GetComponent<Tilemap>();
        }

        private static void AddSolidCollision(GameObject go)
        {
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            var composite = go.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

            var tc = go.AddComponent<TilemapCollider2D>();
            tc.compositeOperation = Collider2D.CompositeOperation.Merge;
        }

        private static void AddOneWayCollision(GameObject go)
        {
            AddSolidCollision(go);
            go.GetComponent<CompositeCollider2D>().usedByEffector = true;

            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 170f;
            effector.useSideFriction = false;
        }

        private static void PaintStarterLevel(TilesetImporter.Result tileset, Tilemap background, Tilemap ground, Tilemap platforms)
        {
            Tile T(int i) => tileset.Tiles[i];

            // 바닥 (구덩이 x 10..13)
            for (int x = -15; x <= 45; x++)
            {
                if (x >= 10 && x <= 13) continue;
                ground.SetTile(new Vector3Int(x, -2, 0), T(Grass));
                ground.SetTile(new Vector3Int(x, -3, 0), T(Dirt));
                ground.SetTile(new Vector3Int(x, -4, 0), T(Dirt));
            }

            // 왼쪽/오른쪽 벽
            for (int y = -1; y <= 8; y++)
            {
                ground.SetTile(new Vector3Int(-15, y, 0), T(Brick));
                ground.SetTile(new Vector3Int(45, y, 0), T(Brick));
            }

            // 계단 블록
            for (int x = 18; x <= 20; x++) ground.SetTile(new Vector3Int(x, -1, 0), T(Stone));
            for (int x = 19; x <= 20; x++) ground.SetTile(new Vector3Int(x, 0, 0), T(Stone));

            // 중간 벽 (대쉬/점프 테스트)
            for (int y = -1; y <= 2; y++) ground.SetTile(new Vector3Int(30, y, 0), T(Brick));

            ground.SetTile(new Vector3Int(3, -1, 0), T(Crate));

            // 아래에서 통과 가능한 발판
            for (int x = 4; x <= 7; x++) platforms.SetTile(new Vector3Int(x, 1, 0), T(Platform));
            for (int x = 22; x <= 25; x++) platforms.SetTile(new Vector3Int(x, 3, 0), T(Platform));

            // 배경
            for (int x = 26; x <= 36; x++)
                for (int y = -1; y <= 4; y++)
                    background.SetTile(new Vector3Int(x, y, 0), T(BackWall));
        }

        private static GameObject CreatePlayer(Sprite sprite, PhysicsMaterial2D material, Vector3 position)
        {
            var go = new GameObject("Player");
            go.layer = LayerMask.NameToLayer("Player");
            go.tag = "Player";
            go.transform.position = position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 5;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.sharedMaterial = material;

            var capsule = go.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            var size = sprite != null ? (Vector2)sprite.bounds.size : new Vector2(1f, 1.5f);
            capsule.size = new Vector2(size.x * 0.8f, size.y * 0.95f);
            capsule.offset = sprite != null ? (Vector2)sprite.bounds.center : Vector2.zero;
            capsule.sharedMaterial = material;

            var controller = go.AddComponent<PlayerController>();
            var so = new SerializedObject(controller);
            so.FindProperty("inputActions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            so.FindProperty("groundLayer").intValue = LayerMask.GetMask("Ground");
            so.ApplyModifiedPropertiesWithoutUndo();

            var combat = go.AddComponent<PlayerCombat>();
            var combatSo = new SerializedObject(combat);
            combatSo.FindProperty("inputActions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            combatSo.ApplyModifiedPropertiesWithoutUndo();
            combat.AssignDefaultArt();
            EditorUtility.SetDirty(combat);

            ItemTools.SetupPlayer(go);

            return go;
        }

        private static void CreateDummy(Sprite sprite, Vector3 position)
        {
            var go = new GameObject("Target Dummy");
            go.transform.position = position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0.9f, 0.35f, 0.35f);
            sr.sortingOrder = 4;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            rb.gravityScale = 4f;

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.85f, 1.45f);

            var health = go.AddComponent<Health>();
            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("destroyOnDeath").boolValue = true;
            healthSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupCamera(Transform target)
        {
            var cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera));
                camGo.tag = "MainCamera";
                cam = camGo.GetComponent<Camera>();
            }

            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.transform.position = new Vector3(target.position.x, target.position.y + 1.5f, -10f);

            if (!cam.TryGetComponent(out CameraFollow2D follow))
                follow = cam.gameObject.AddComponent<CameraFollow2D>();
            follow.Target = target;
            EditorUtility.SetDirty(follow);
        }

        private const string BackgroundSpritePath = "Assets/arts/picture/background.png";

        private static void CreateParallaxBackground(Vector3 viewCenter)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpritePath);
            if (sprite == null)
            {
                Debug.LogWarning("[Level Builder] Assets/arts/picture/background.png 스프라이트를 찾지 못했습니다.");
                return;
            }

            var go = new GameObject("Parallax Background");
            go.transform.position = viewCenter;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -100;

            float height = sprite.bounds.size.y;
            if (height > 0.001f)
            {
                float scale = 6f * 2f * 1.2f / height;
                go.transform.localScale = new Vector3(scale, scale, 1f);
            }

            go.AddComponent<ParallaxBackground>();
        }

        private static void CreateBossDoor(Vector3 position)
        {
            var go = new GameObject("Boss Door");
            go.transform.position = position;
            go.AddComponent<Gstarr.World.BossDoor>();
        }

        private static TilesetImporter.Result CreatePlaceholderTileset()
        {
            TilesetImporter.EnsureFolder(PlaceholderFolder);

            if (!File.Exists(TilesetPath))
            {
                const int s = PlaceholderTileSize;
                const int cols = 4, rows = 2;
                var tex = new Texture2D(cols * s, rows * s, TextureFormat.RGBA32, false);
                var px = new Color32[tex.width * tex.height];

                void Paint(int index, System.Func<int, int, Color32> f)
                {
                    int cx = index % cols, cy = rows - 1 - index / cols;
                    for (int ly = 0; ly < s; ly++)
                        for (int lx = 0; lx < s; lx++)
                            px[(cy * s + ly) * tex.width + cx * s + lx] = f(lx, ly);
                }

                Color32 Shade(Color32 c, int lx, int ly, int seed, float amount)
                {
                    int h = (lx * 73856093) ^ (ly * 19349663) ^ (seed * 83492791);
                    float n = ((h & 0xFF) / 255f - 0.5f) * amount;
                    return new Color32(
                        (byte)Mathf.Clamp(c.r + n * 255, 0, 255),
                        (byte)Mathf.Clamp(c.g + n * 255, 0, 255),
                        (byte)Mathf.Clamp(c.b + n * 255, 0, 255), c.a);
                }

                var dirt = new Color32(134, 90, 56, 255);
                var grass = new Color32(86, 170, 72, 255);
                var stone = new Color32(128, 132, 140, 255);
                var stoneLine = new Color32(77, 79, 84, 255);
                var brick = new Color32(168, 72, 56, 255);
                var mortar = new Color32(90, 80, 76, 255);
                var wood = new Color32(176, 124, 70, 255);
                var woodDark = new Color32(110, 72, 40, 255);
                var wall = new Color32(52, 58, 82, 255);
                var clear = new Color32(0, 0, 0, 0);

                Paint(Grass, (x, y) => y >= 12
                    ? Shade(y == 12 && x % 3 == 0 ? dirt : grass, x, y, 1, 0.08f)
                    : Shade(dirt, x, y, 2, 0.12f));
                Paint(Dirt, (x, y) => Shade(dirt, x, y, 3, 0.12f));
                Paint(Stone, (x, y) =>
                {
                    bool line = y % 8 == 0 || (x + (y / 8 % 2) * 8) % 16 == 0;
                    return line ? stoneLine : Shade(stone, x, y, 5, 0.1f);
                });
                Paint(Brick, (x, y) =>
                {
                    bool line = y % 4 == 0 || (x + (y / 4 % 2) * 4) % 8 == 0;
                    return line ? mortar : Shade(brick, x, y, 6, 0.08f);
                });
                Paint(Platform, (x, y) =>
                {
                    if (y < 10) return clear;
                    if (y == 10 || y == 15 || x == 0 || x == s - 1) return woodDark;
                    return Shade(wood, x, y, 7, 0.06f);
                });
                Paint(BackWall, (x, y) =>
                {
                    bool line = y % 8 == 0 || (x + (y / 8 % 2) * 8) % 16 == 0;
                    return line ? new Color32(38, 42, 60, 255) : Shade(wall, x, y, 8, 0.05f);
                });
                Paint(Crate, (x, y) =>
                {
                    bool border = x <= 1 || y <= 1 || x >= s - 2 || y >= s - 2;
                    bool cross = Mathf.Abs(x - y) <= 1 || Mathf.Abs(x - (s - 1 - y)) <= 1;
                    return border || cross ? woodDark : Shade(wood, x, y, 9, 0.06f);
                });
                Paint(7, (x, y) => clear);

                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(TilesetPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(TilesetPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetPath);
            return TilesetImporter.Import(texture, PlaceholderTileSize, Vector2Int.zero, Vector2Int.zero,
                skipEmpty: true, paletteName: "Placeholder");
        }

        private static readonly string[] PlayerSpritePaths =
        {
            "Assets/character/player.png",
            "Assets/arts/character/player.png"
        };

        /// <summary>Assets/character/player.png 를 플레이어 이미지로 사용 (없으면 플레이스홀더).</summary>
        private static Sprite LoadPlayerSprite()
        {
            foreach (var path in PlayerSpritePaths)
            {
                if (!File.Exists(path)) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.userData != "gstarr-player")
                {
                    importer.GetSourceTextureWidthAndHeight(out _, out int height);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    // 캐릭터 키가 약 1.5 유닛이 되도록
                    importer.spritePixelsPerUnit = Mathf.Max(1f, Mathf.Round(height / 1.5f));
                    importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.mipmapEnabled = false;
                    importer.userData = "gstarr-player";
                    importer.SaveAndReimport();
                }
                var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
                if (sprite != null) return sprite;
            }

            Debug.LogWarning("[Level Builder] Assets/character/player.png 가 없어 플레이스홀더 이미지를 사용합니다.");
            return CreatePlaceholderPlayerSprite();
        }

        private static Sprite CreatePlaceholderPlayerSprite()
        {
            TilesetImporter.EnsureFolder(PlaceholderFolder);

            if (!File.Exists(PlayerSpritePath))
            {
                const int w = 14, h = 24;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var body = new Color32(240, 200, 80, 255);
                var outline = new Color32(60, 40, 20, 255);
                var eye = new Color32(30, 30, 40, 255);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                        bool isEye = y >= 16 && y <= 18 && x >= 8 && x <= 9;
                        tex.SetPixel(x, y, edge ? outline : isEye ? eye : body);
                    }
                tex.Apply();
                File.WriteAllBytes(PlayerSpritePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(PlayerSpritePath, ImportAssetOptions.ForceSynchronousImport);

                var importer = (TextureImporter)AssetImporter.GetAtPath(PlayerSpritePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAllAssetsAtPath(PlayerSpritePath).OfType<Sprite>().First();
        }

        private static PhysicsMaterial2D CreateNoFrictionMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionPath);
            if (mat != null) return mat;

            TilesetImporter.EnsureFolder(Path.GetDirectoryName(NoFrictionPath).Replace('\\', '/'));
            mat = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(mat, NoFrictionPath);
            return mat;
        }

        private static void EnsureLayer(string layerName)
        {
            if (LayerMask.NameToLayer(layerName) != -1) return;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 6; i < layers.arraySize; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = layerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }
            Debug.LogError($"[Level Builder] 빈 레이어 슬롯이 없어 '{layerName}' 레이어를 추가하지 못했습니다.");
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == scenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
