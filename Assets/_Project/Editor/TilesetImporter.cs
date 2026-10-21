using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Gstarr.EditorTools
{
    public static class TilesetImporter
    {
        public const string PaletteFolder = "Assets/_Project/Tilemaps/Palettes";
        public const string TileFolder = "Assets/_Project/Tilemaps/Tiles";

        public class Result
        {
            /// <summary>타일셋 이미지의 셀 인덱스(왼쪽 위부터 행 우선) 순서. 빈 셀은 null.</summary>
            public Tile[] Tiles;
            public int Columns;
            public int Rows;
            public string PalettePath;
        }

        public static Result Import(Texture2D texture, int tileSize, Vector2Int offset, Vector2Int padding,
            bool skipEmpty, string paletteName, Tile.ColliderType colliderType = Tile.ColliderType.Grid)
        {
            string texPath = AssetDatabase.GetAssetPath(texture);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);

            ConfigureImporter(importer, tileSize);

            importer.GetSourceTextureWidthAndHeight(out int texW, out int texH);
            int cols = Mathf.Max(0, (texW - offset.x + padding.x) / (tileSize + padding.x));
            int rows = Mathf.Max(0, (texH - offset.y + padding.y) / (tileSize + padding.y));

            Color32[] pixels = skipEmpty ? ReadSourcePixels(texPath, texW, texH) : null;

            string baseName = Path.GetFileNameWithoutExtension(texPath);
            var cellRects = new Dictionary<int, Rect>();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int x = offset.x + c * (tileSize + padding.x);
                    int yTop = offset.y + r * (tileSize + padding.y);
                    int y = texH - yTop - tileSize;
                    var rect = new Rect(x, y, tileSize, tileSize);

                    if (pixels != null && IsEmpty(pixels, texW, rect)) continue;
                    cellRects[r * cols + c] = rect;
                }
            }

            WriteSpriteRects(importer, baseName, cellRects);

            var sprites = AssetDatabase.LoadAllAssetsAtPath(texPath).OfType<Sprite>()
                .ToDictionary(s => s.name, s => s);

            string tileFolder = $"{TileFolder}/{baseName}";
            EnsureFolder(tileFolder);

            var tiles = new Tile[rows * cols];
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var kv in cellRects)
                {
                    string spriteName = $"{baseName}_{kv.Key}";
                    if (!sprites.TryGetValue(spriteName, out var sprite)) continue;

                    string tilePath = $"{tileFolder}/{spriteName}.asset";
                    var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                    if (tile == null)
                    {
                        tile = ScriptableObject.CreateInstance<Tile>();
                        tile.sprite = sprite;
                        tile.colliderType = colliderType;
                        AssetDatabase.CreateAsset(tile, tilePath);
                    }
                    else
                    {
                        tile.sprite = sprite;
                        EditorUtility.SetDirty(tile);
                    }
                    tiles[kv.Key] = tile;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.SaveAssets();

            string palettePath = BuildPalette(string.IsNullOrWhiteSpace(paletteName) ? baseName : paletteName,
                tiles, cols);

            return new Result { Tiles = tiles, Columns = cols, Rows = rows, PalettePath = palettePath };
        }

        private static void ConfigureImporter(TextureImporter importer, int tileSize)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Multiple;
            settings.spritePixelsPerUnit = tileSize;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            settings.filterMode = FilterMode.Point;
            settings.mipmapEnabled = false;
            settings.wrapMode = TextureWrapMode.Clamp;
            settings.alphaIsTransparency = true;
            importer.SetTextureSettings(settings);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void WriteSpriteRects(TextureImporter importer, string baseName, Dictionary<int, Rect> cellRects)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            var existingIds = dp.GetSpriteRects()
                .GroupBy(r => r.name)
                .ToDictionary(g => g.Key, g => g.First().spriteID);

            var rects = new List<SpriteRect>();
            foreach (var kv in cellRects.OrderBy(k => k.Key))
            {
                string name = $"{baseName}_{kv.Key}";
                rects.Add(new SpriteRect
                {
                    name = name,
                    rect = kv.Value,
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = existingIds.TryGetValue(name, out var id) ? id : GUID.Generate()
                });
            }
            dp.SetSpriteRects(rects.ToArray());

            var nameFileIds = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameFileIds?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();
        }

        private static string BuildPalette(string paletteName, Tile[] tiles, int cols)
        {
            EnsureFolder(PaletteFolder);
            string palettePath = $"{PaletteFolder}/{paletteName}.prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(palettePath) == null)
            {
                GridPaletteUtility.CreateNewPalette(PaletteFolder, paletteName,
                    GridLayout.CellLayout.Rectangle, GridPalette.CellSizing.Automatic,
                    Vector3.one, GridLayout.CellSwizzle.XYZ);
            }

            var root = PrefabUtility.LoadPrefabContents(palettePath);
            try
            {
                var tilemap = root.GetComponentInChildren<Tilemap>();
                tilemap.ClearAllTiles();
                for (int i = 0; i < tiles.Length; i++)
                {
                    if (tiles[i] == null) continue;
                    // 팔레트 배치를 원본 이미지 배치와 동일하게 (왼쪽 위 = (0,0), 아래로 갈수록 -y)
                    tilemap.SetTile(new Vector3Int(i % cols, -(i / cols), 0), tiles[i]);
                }
                PrefabUtility.SaveAsPrefabAsset(root, palettePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return palettePath;
        }

        private static Color32[] ReadSourcePixels(string texPath, int w, int h)
        {
            string ext = Path.GetExtension(texPath).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") return null;

            var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tmp.LoadImage(File.ReadAllBytes(texPath))) return null;
                if (tmp.width != w || tmp.height != h) return null;
                return tmp.GetPixels32();
            }
            finally
            {
                Object.DestroyImmediate(tmp);
            }
        }

        private static bool IsEmpty(Color32[] pixels, int texW, Rect rect)
        {
            for (int y = (int)rect.yMin; y < (int)rect.yMax; y++)
                for (int x = (int)rect.xMin; x < (int)rect.xMax; x++)
                    if (pixels[y * texW + x].a > 0) return false;
            return true;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    public class TilesetImporterWindow : EditorWindow
    {
        private Texture2D texture;
        private int tileSize = 16;
        private Vector2Int offset;
        private Vector2Int padding;
        private bool skipEmpty = true;
        private Tile.ColliderType colliderType = Tile.ColliderType.Grid;
        private string paletteName = "";

        [MenuItem("Tools/Gstarr/Tileset Importer", priority = 0)]
        public static void Open()
        {
            var w = GetWindow<TilesetImporterWindow>("Tileset Importer");
            w.minSize = new Vector2(320, 260);
            if (Selection.activeObject is Texture2D t) w.SetTexture(t);
        }

        [MenuItem("Assets/Gstarr/Tileset → Tile Palette", priority = 1000)]
        private static void OpenFromAsset() => Open();

        [MenuItem("Assets/Gstarr/Tileset → Tile Palette", true)]
        private static bool ValidateOpenFromAsset() => Selection.activeObject is Texture2D;

        private void SetTexture(Texture2D t)
        {
            texture = t;
            paletteName = t != null ? t.name : "";
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "타일셋 이미지를 지정한 크기로 잘라 Tile 에셋과 Tile Palette를 자동 생성합니다.\n" +
                "생성 후 Window > 2D > Tile Palette 에서 팔레트를 골라 씬에 그리면 됩니다.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            var newTex = (Texture2D)EditorGUILayout.ObjectField("Tileset Texture", texture, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck()) SetTexture(newTex);

            tileSize = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Tile Size (px)", "한 칸의 픽셀 크기. Pixels Per Unit 으로도 사용되어 1타일 = 1유닛"), tileSize));
            offset = EditorGUILayout.Vector2IntField(new GUIContent("Offset (px)", "이미지 왼쪽 위 여백"), offset);
            padding = EditorGUILayout.Vector2IntField(new GUIContent("Padding (px)", "타일 사이 간격"), padding);
            skipEmpty = EditorGUILayout.Toggle(new GUIContent("Skip Empty Cells", "완전히 투명한 칸은 건너뜀"), skipEmpty);
            colliderType = (Tile.ColliderType)EditorGUILayout.EnumPopup(new GUIContent("Collider Type", "Grid: 칸 전체 충돌 (권장) / Sprite: 스프라이트 Physics Shape"), colliderType);
            paletteName = EditorGUILayout.TextField("Palette Name", paletteName);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(texture == null))
            {
                if (GUILayout.Button("Slice & Create Palette", GUILayout.Height(32)))
                {
                    var result = TilesetImporter.Import(texture, tileSize, offset, padding, skipEmpty, paletteName, colliderType);
                    int count = result.Tiles.Count(t => t != null);
                    Debug.Log($"[Tileset Importer] {count}개 타일 생성 → {result.PalettePath}");
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(result.PalettePath));
                    EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");
                }
            }
        }
    }
}
