using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;

namespace Gstarr.Combat
{
    /// <summary>타일맵에 그려 둔 적 그림은 지운다. 실제 적은 방이 열릴 때 나온다.</summary>
    [RequireComponent(typeof(Tilemap))]
    public class EnemyTileSpawner : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Object.FindAnyObjectByType<EnemyTileSpawner>() != null) return;
            new GameObject("EnemyTileBootstrap").AddComponent<EnemyTileSpawner>();
        }

        public static bool HasPlacedEnemies()
        {
            foreach (var tilemap in FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                foreach (var cell in tilemap.cellBounds.allPositionsWithin)
                    if (tilemap.GetTile(cell) is EnemyTile) return true;
            }
            return false;
        }

        private void Awake()
        {
            var renderer = GetComponent<TilemapRenderer>();
            if (renderer != null) renderer.enabled = false;
        }

        private void Start()
        {
            var tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var tilemap in tilemaps)
            {
                tilemap.CompressBounds();
                bool placed = false;
                var cells = new List<Vector3Int>();
                foreach (var cell in tilemap.cellBounds.allPositionsWithin)
                    cells.Add(cell);

                for (int i = 0; i < cells.Count; i++)
                {
                    var cell = cells[i];
                    if (tilemap.GetTile(cell) is not EnemyTile) continue;
                    tilemap.SetTileFlags(cell, TileFlags.None);
                    tilemap.SetColor(cell, Color.clear);
                    tilemap.SetTile(cell, null);
                    placed = true;
                }

                if (!placed) continue;
                tilemap.RefreshAllTiles();
                var renderer = tilemap.GetComponent<TilemapRenderer>();
                if (renderer != null) renderer.enabled = false;
            }
        }
    }
}
