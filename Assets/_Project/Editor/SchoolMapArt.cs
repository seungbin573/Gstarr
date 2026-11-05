using System;
using System.Collections.Generic;
using System.IO;
using Gstarr.CameraSystem;
using Gstarr.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Gstarr.EditorTools
{
    /// <summary>
    /// 배경이 바뀌기 전 첫 구간만 Assets/arts/school 그림으로 바꾼다.
    /// 이후 볼풀, 눈 숲, 교회 구간은 그대로 둔다.
    /// </summary>
    public static class SchoolMapArt
    {
        private const string SchoolFolder = "Assets/arts/school";
        private const string BackgroundPath = SchoolFolder + "/school_background.jpg";
        private const string FloorPath = SchoolFolder + "/school_floor.jpg";
        private const string SkyPath = SchoolFolder + "/school_skyfloor.jpg";
        private const string DoorPath = SchoolFolder + "/school_door.png";
        private const string SchoolDoorTilePath = "Assets/_Project/Tilemaps/DoorTiles/SchoolDoorTile.asset";
        private const string ScenePath = "Assets/Scenes/Stage_1.unity";
        private const int TileSize = 120;

        /// <summary>첫 배경(뒤틀린 학교)이 깔리는 칸. Stage_1 의 첫 맵 끝과 맞춘다.</summary>
        private const int FirstChapterEndX = 64;

        [MenuItem("Tools/Gstarr/Apply School Art To Stage 1", priority = 21)]
        public static void ApplyFromMenu()
        {
            Apply();
        }

        public static void Apply()
        {
            var floor = Slice(FloorPath, "SchoolFloor", Tile.ColliderType.Grid, false);
            var sky = Slice(SkyPath, "SchoolSky", Tile.ColliderType.Grid, false);
            var wall = Slice(BackgroundPath, "SchoolWall", Tile.ColliderType.None, true);
            if (wall.FullSprite == null)
                throw new InvalidOperationException("school_background 전체 스프라이트를 만들지 못했습니다.");

            var door = ImportDoor();
            PaintScene(floor, sky, wall, door);
            AssetDatabase.SaveAssets();
            Debug.Log("[School Art] 뒤틀린 학교 구간(x < 64)만 학교 그림으로 바꿨습니다.");
        }

        private static TilesetImporter.Result Slice(string path, string palette, Tile.ColliderType collider, bool fullImage)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
                throw new FileNotFoundException("학교 이미지를 찾지 못했습니다.", path);

            var result = TilesetImporter.Import(
                texture, TileSize, Vector2Int.zero, Vector2Int.zero,
                false, palette, collider, FilterMode.Bilinear, fullImage, false);

            if (result.Columns != 16 || result.Rows != 9)
                throw new InvalidOperationException($"{path} 를 16x9 칸으로 나누지 못했습니다. ({result.Columns}x{result.Rows})");

            for (int i = 0; i < result.Tiles.Length; i++)
            {
                if (result.Tiles[i] == null)
                    throw new InvalidOperationException($"{path} 타일 {i} 가 비었습니다.");
            }

            return result;
        }

        private static Sprite ImportDoor()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(DoorPath);
            if (importer == null)
                throw new FileNotFoundException("학교 문 이미지를 찾지 못했습니다.", DoorPath);

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spritePixelsPerUnit = 409f / 3f;
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = new Vector2(0.5f, 0f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 1;
            settings.filterMode = FilterMode.Bilinear;
            settings.mipmapEnabled = false;
            settings.alphaIsTransparency = true;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DoorPath);
            if (sprite == null)
                throw new InvalidOperationException("school_door 스프라이트를 읽지 못했습니다.");
            return sprite;
        }

        private static void PaintScene(
            TilesetImporter.Result floor,
            TilesetImporter.Result sky,
            TilesetImporter.Result wall,
            Sprite door)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent(out ParallaxBackground parallax))
                        AssignBackground(parallax, wall.FullSprite);

                    foreach (var map in root.GetComponentsInChildren<Tilemap>(true))
                    {
                        switch (map.name)
                        {
                            case "Ground":
                                Repaint(map, floor.Tiles, floor.Columns, floor.Rows);
                                break;
                            case "Platforms (One-Way)":
                                Repaint(map, sky.Tiles, sky.Columns, sky.Rows);
                                break;
                            case "Background":
                                Repaint(map, wall.Tiles, wall.Columns, wall.Rows);
                                break;
                            case "Doors":
                                AssignDoor(map, door);
                                break;
                        }
                    }
                }

                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssignBackground(ParallaxBackground parallax, Sprite sprite)
        {
            var so = new SerializedObject(parallax);
            var backgrounds = so.FindProperty("backgrounds");
            if (backgrounds.arraySize < 1)
                backgrounds.arraySize = 1;
            backgrounds.GetArrayElementAtIndex(0).objectReferenceValue = sprite;
            so.ApplyModifiedPropertiesWithoutUndo();

            var renderer = parallax.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            EditorUtility.SetDirty(parallax);
            EditorUtility.SetDirty(renderer);
        }

        private static void AssignDoor(Tilemap map, Sprite door)
        {
            var schoolDoor = AssetDatabase.LoadAssetAtPath<DoorTile>(SchoolDoorTilePath);
            if (schoolDoor == null)
            {
                schoolDoor = ScriptableObject.CreateInstance<DoorTile>();
                AssetDatabase.CreateAsset(schoolDoor, SchoolDoorTilePath);
            }

            schoolDoor.Setup(door);
            EditorUtility.SetDirty(schoolDoor);

            var positions = new List<Vector3Int>();
            var painted = new List<TileBase>();
            foreach (var pos in map.cellBounds.allPositionsWithin)
            {
                if (pos.x >= FirstChapterEndX) continue;
                if (map.GetTile(pos) is not DoorTile) continue;
                positions.Add(pos);
                painted.Add(schoolDoor);
            }

            if (positions.Count > 0)
                map.SetTiles(positions.ToArray(), painted.ToArray());
            EditorUtility.SetDirty(map);
        }

        private static void Repaint(Tilemap map, Tile[] tiles, int cols, int rows)
        {
            var positions = new List<Vector3Int>();
            var painted = new List<TileBase>();
            foreach (var pos in map.cellBounds.allPositionsWithin)
            {
                if (pos.x >= FirstChapterEndX) continue;
                if (!map.HasTile(pos)) continue;
                int index = Mod(pos.x, cols) + Mod(-pos.y, rows) * cols;
                positions.Add(pos);
                painted.Add(tiles[index]);
            }

            if (positions.Count > 0)
                map.SetTiles(positions.ToArray(), painted.ToArray());
            EditorUtility.SetDirty(map);
        }

        private static int Mod(int value, int divisor)
        {
            int remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }
    }
}
