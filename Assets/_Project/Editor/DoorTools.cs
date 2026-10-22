using System.Linq;
using Gstarr.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Gstarr.EditorTools
{
    public static class DoorTools
    {
        public const string DoorPath = "Assets/_Project/Tilemaps/DoorTiles/Door.png";
        public const string FramePath = "Assets/_Project/Tilemaps/DoorTiles/DoorFrame.png";
        public const string LampPath = "Assets/_Project/Tilemaps/DoorTiles/DoorLamp.png";
        public const string TilePath = "Assets/_Project/Tilemaps/DoorTiles/DoorTile.asset";
        private const string PaletteName = "Doors";

        [MenuItem("Tools/Gstarr/Build Door Palette", priority = 3)]
        public static void BuildDoorPaletteMenu()
        {
            GetOrCreateTile();
            Debug.Log("[Door Tools] Doors 팔레트를 만들었습니다. Door.png 를 바꾸면 문 그림이 바뀝니다. 바닥 칸에 한 칸만 칠하면 위로 3칸 문이 됩니다.");
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

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DoorPath);
            tile.Setup(sprite);
            EditorUtility.SetDirty(tile);
            TilesetImporter.BuildPalette(PaletteName, new TileBase[] { tile }, 1);
            AssetDatabase.SaveAssets();
            return tile;
        }

        public static Tilemap EnsureDoorTilemap(Transform grid)
        {
            var existing = grid.GetComponentsInChildren<ArenaDoorSpawner>(true).FirstOrDefault();
            if (existing != null)
            {
                AssignArt(existing);
                var map = existing.GetComponent<Tilemap>();
                map.tileAnchor = new Vector3(0.5f, 0f, 0f);
                return map;
            }

            var go = new GameObject("Doors", typeof(Tilemap), typeof(TilemapRenderer), typeof(ArenaDoorSpawner));
            Undo.RegisterCreatedObjectUndo(go, "Create Doors Tilemap");
            go.transform.SetParent(grid, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = 6;
            var tilemap = go.GetComponent<Tilemap>();
            tilemap.tileAnchor = new Vector3(0.5f, 0f, 0f);
            AssignArt(go.GetComponent<ArenaDoorSpawner>());
            return tilemap;
        }

        public static void PaintDoor(Tilemap map, DoorTile tile, int x, int floorCellY)
        {
            map.SetTile(new Vector3Int(x, floorCellY, 0), tile);
        }

        static void AssignArt(ArenaDoorSpawner spawner)
        {
            spawner.AssignArt(
                AssetDatabase.LoadAssetAtPath<Sprite>(DoorPath),
                AssetDatabase.LoadAssetAtPath<Sprite>(FramePath),
                AssetDatabase.LoadAssetAtPath<Sprite>(LampPath));
            EditorUtility.SetDirty(spawner);
        }
    }
}
