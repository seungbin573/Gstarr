using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;

namespace Gstarr.Combat
{
    /// <summary>Enemies 타일맵에 배치한 적 타일을 씬 시작 때 실제 적으로 바꾼다.</summary>
    [RequireComponent(typeof(Tilemap))]
    public class EnemyTileSpawner : MonoBehaviour
    {
        private struct SpawnRequest
        {
            public EnemyKind Kind;
            public Vector3 Position;
        }

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
            var requests = new List<SpawnRequest>();
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
                    if (tilemap.GetTile(cell) is not EnemyTile tile) continue;
                    Vector3 bottom = tilemap.CellToWorld(cell);
                    Vector3 center = tilemap.GetCellCenterWorld(cell);
                    requests.Add(new SpawnRequest
                    {
                        Kind = tile.Kind,
                        Position = new Vector3(center.x, bottom.y, center.z)
                    });
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

            foreach (var request in requests)
            {
                if (request.Kind == EnemyKind.Boss) EnemyActor.SpawnBoss(request.Position);
                else EnemyActor.Spawn(null, request.Position, request.Kind);
            }
        }
    }
}
