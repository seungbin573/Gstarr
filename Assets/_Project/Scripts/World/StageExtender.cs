using System.Collections.Generic;
using Gstarr.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Gstarr.World
{
    /// <summary>
    /// 플레이 시작 때 스테이지 본편을 한 번 더 이어 붙인다.
    /// 양쪽 끝의 벽 사이 바닥, 발판, 문을 통째로 복사해 오른쪽에 둔다. 적은 방이 열릴 때 따로 나온다.
    /// </summary>
    public static class StageExtender
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Tilemap ground = null;
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i].gameObject.scene != scene || maps[i].name != "Ground") continue;
                ground = maps[i];
                break;
            }
            if (ground == null) return;

            ground.CompressBounds();
            var columns = new Dictionary<int, List<Vector3Int>>();
            foreach (var cell in ground.cellBounds.allPositionsWithin)
            {
                if (ground.GetTile(cell) == null) continue;
                if (!columns.TryGetValue(cell.x, out var list))
                {
                    list = new List<Vector3Int>();
                    columns[cell.x] = list;
                }
                list.Add(cell);
            }
            if (columns.Count == 0) return;

            int left = int.MaxValue;
            int right = int.MinValue;
            foreach (var pair in columns)
            {
                if (!IsCap(pair.Value)) continue;
                if (pair.Key < left) left = pair.Key;
                if (pair.Key > right) right = pair.Key;
            }
            if (left >= right || !columns.ContainsKey(right - 1)) return;

            int sourceStart = left + 1;
            int sourceEnd = right - 1;
            int shift = right - left;

            var wallCells = columns[right];
            var wallTiles = new TileBase[wallCells.Count];
            for (int i = 0; i < wallCells.Count; i++)
                wallTiles[i] = ground.GetTile(wallCells[i]);

            for (int i = 0; i < wallCells.Count; i++)
                ground.SetTile(wallCells[i], null);

            var seam = columns[sourceEnd];
            for (int i = 0; i < seam.Count; i++)
                ground.SetTile(new Vector3Int(right, seam[i].y, 0), ground.GetTile(seam[i]));

            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i].gameObject.scene != scene) continue;
                Stamp(maps[i], sourceStart, sourceEnd, shift);
            }

            for (int i = 0; i < wallCells.Count; i++)
            {
                var cell = wallCells[i];
                ground.SetTile(new Vector3Int(cell.x + shift, cell.y, 0), wallTiles[i]);
            }
        }

        private static void Stamp(Tilemap map, int sourceStart, int sourceEnd, int shift)
        {
            map.CompressBounds();
            var cells = new List<Vector3Int>();
            var tiles = new List<TileBase>();
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                if (cell.x < sourceStart || cell.x > sourceEnd) continue;
                var tile = map.GetTile(cell);
                if (tile == null || tile is EnemyTile) continue;
                cells.Add(new Vector3Int(cell.x + shift, cell.y, cell.z));
                tiles.Add(tile);
            }
            if (cells.Count == 0) return;
            map.SetTiles(cells.ToArray(), tiles.ToArray());
        }

        private static bool IsCap(List<Vector3Int> column)
        {
            int minY = int.MaxValue;
            for (int i = 0; i < column.Count; i++)
                if (column[i].y < minY) minY = column[i].y;
            return minY >= -2;
        }
    }
}
