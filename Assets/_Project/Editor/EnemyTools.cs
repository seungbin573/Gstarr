using System.Linq;
using Gstarr.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Gstarr.EditorTools
{
    public static class EnemyTools
    {
        private const string TileFolder = "Assets/_Project/Tilemaps/EnemyTiles";
        private const string PaletteName = "Enemies";

        [MenuItem("Tools/Gstarr/Build Enemy Palette", priority = 2)]
        public static void BuildEnemyPaletteMenu()
        {
            var tiles = BuildEnemyPalette();
            Debug.Log($"[Enemy Tools] {tiles.Length}개 적 타일을 '{PaletteName}' 팔레트에 만들었습니다. " +
                      "Tile Palette에서 Enemies 팔레트와 Enemies Tilemap을 선택해 배치하세요.");
            EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");
        }

        public static EnemyTile[] BuildEnemyPalette()
        {
            var tiles = BuildEnemyTiles();
            return tiles;
        }

        [MenuItem("Tools/Gstarr/Setup Enemy Tilemap In Scene", priority = 22)]
        public static void SetupEnemyTilemapInSceneMenu()
        {
            var grid = Object.FindAnyObjectByType<Grid>();
            if (grid == null)
            {
                Debug.LogWarning("[Enemy Tools] 씬에 Grid가 없습니다. 먼저 Tools/Gstarr/Create Level Scene을 실행하세요.");
                return;
            }

            var tilemap = EnsureEnemyTilemap(grid.transform);
            Selection.activeGameObject = tilemap.gameObject;
            EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);
            BuildEnemyPalette();
            EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");
        }

        public static Tilemap EnsureEnemyTilemap(Transform grid)
        {
            var existing = grid.GetComponentsInChildren<EnemyTileSpawner>(true).FirstOrDefault();
            if (existing != null) return existing.GetComponent<Tilemap>();

            var go = new GameObject("Enemies", typeof(Tilemap), typeof(TilemapRenderer), typeof(EnemyTileSpawner));
            Undo.RegisterCreatedObjectUndo(go, "Create Enemies Tilemap");
            go.transform.SetParent(grid, false);
            go.layer = LayerMask.NameToLayer("Default");
            go.GetComponent<TilemapRenderer>().sortingOrder = 7;
            return go.GetComponent<Tilemap>();
        }

        private static EnemyTile[] BuildEnemyTiles()
        {
            TilesetImporter.EnsureFolder(TileFolder);
            var kinds = new[]
            {
                EnemyKind.BeatChaser,
                EnemyKind.RangedBeat,
                EnemyKind.RangedHalfBeat,
                EnemyKind.Flyer,
                EnemyKind.Mole,
                EnemyKind.Boss,
                EnemyKind.Hopper
            };
            string[] names = { "BeatChaser", "RangedBeat", "RangedHalfBeat", "Flyer", "Mole", "Boss", "Hopper" };
            string[] icons = { "enemy1", "enemy2", "enemy3", "enemy4", "enemy1", "enemy1", "enemy1" };
            Color[] tints =
            {
                Color.white,
                Color.white,
                Color.white,
                Color.white,
                new Color(0.75f, 0.75f, 0.75f),
                new Color(1f, 0.65f, 0.45f),
                new Color(0.45f, 0.95f, 0.4f)
            };
            var palette = new TileBase[kinds.Length];
            var tiles = new EnemyTile[kinds.Length];

            for (int i = 0; i < kinds.Length; i++)
            {
                string path = $"{TileFolder}/{names[i]}Tile.asset";
                var tile = AssetDatabase.LoadAssetAtPath<EnemyTile>(path);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<EnemyTile>();
                    AssetDatabase.CreateAsset(tile, path);
                }

                var icon = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/arts/character/{icons[i]}.png");
                tile.Setup(kinds[i], icon, tints[i]);
                EditorUtility.SetDirty(tile);
                tiles[i] = tile;
                palette[i] = tile;
            }

            AssetDatabase.SaveAssets();
            TilesetImporter.BuildPalette(PaletteName, palette, palette.Length);
            return tiles;
        }
    }
}
