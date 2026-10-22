using System.Linq;
using Gstarr.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Gstarr.EditorTools
{
    public static class DoorTools
    {
        public const string SpritePath = "Assets/_Project/Tilemaps/DoorTiles/Door.png";
        public const string TilePath = "Assets/_Project/Tilemaps/DoorTiles/DoorTile.asset";
        private const string PaletteName = "Doors";

        [MenuItem("Tools/Gstarr/Build Door Palette", priority = 3)]
        public static void BuildDoorPaletteMenu()
        {
            GetOrCreateTile();
            Debug.Log("[Door Tools] Doors 팔레트를 만들었습니다. Tile Palette에서 Doors를 고르고 Doors 타일맵에 세로로 칠하세요.");
            EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");
        }

        public static DoorTile GetOrCreateTile()
        {
            TilesetImporter.EnsureFolder("Assets/_Project/Tilemaps/DoorTiles");
            var tile = AssetDatabase.LoadAssetAtPath<DoorTile>(TilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<DoorTile>();
                AssetDatabase.CreateAsset(tile, TilePath);
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            tile.Setup(sprite);
            EditorUtility.SetDirty(tile);
            TilesetImporter.BuildPalette(PaletteName, new TileBase[] { tile }, 1);
            AssetDatabase.SaveAssets();
            return tile;
        }

        public static Tilemap EnsureDoorTilemap(Transform grid)
        {
            var existing = grid.GetComponentsInChildren<ArenaDoorSpawner>(true).FirstOrDefault();
            if (existing != null) return existing.GetComponent<Tilemap>();

            var go = new GameObject("Doors", typeof(Tilemap), typeof(TilemapRenderer), typeof(ArenaDoorSpawner));
            Undo.RegisterCreatedObjectUndo(go, "Create Doors Tilemap");
            go.transform.SetParent(grid, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = 6;
            return go.GetComponent<Tilemap>();
        }

        public static void PaintColumn(Tilemap map, DoorTile tile, int x, int yMin, int yMax)
        {
            for (int y = yMin; y <= yMax; y++)
                map.SetTile(new Vector3Int(x, y, 0), tile);
        }
    }
}
