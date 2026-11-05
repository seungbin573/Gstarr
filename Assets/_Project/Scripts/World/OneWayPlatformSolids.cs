using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Gstarr.World
{
    /// <summary>
    /// 한 방향 발판은 타일마다 콜라이더를 두면 칸 사이에 구멍이 생긴다.
    /// 이어진 칸은 박스 하나로 덮어 그 틈으로 빠지지 않게 한다.
    /// </summary>
    public static class OneWayPlatformSolids
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshGroundColliders();
            var maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < maps.Length; i++)
            {
                var map = maps[i];
                if (map == null || map.GetComponent<PlatformEffector2D>() == null) continue;
                if (map.GetComponent<SolidOneWay>() != null) continue;
                Build(map);
            }
        }

        private static void Build(Tilemap map)
        {
            var tileCollider = map.GetComponent<TilemapCollider2D>();
            var composite = map.GetComponent<CompositeCollider2D>();
            if (tileCollider != null) tileCollider.enabled = false;
            if (composite != null) composite.enabled = false;

            var cells = new List<Vector3Int>();
            foreach (var cell in map.cellBounds.allPositionsWithin)
                if (map.HasTile(cell)) cells.Add(cell);
            cells.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

            int index = 0;
            while (index < cells.Count)
            {
                int y = cells[index].y;
                int x0 = cells[index].x;
                bool full = FillsCell(map, cells[index]);
                int x1 = x0;
                index++;
                while (index < cells.Count
                    && cells[index].y == y
                    && cells[index].x == x1 + 1
                    && FillsCell(map, cells[index]) == full)
                {
                    x1 = cells[index].x;
                    index++;
                }
                AddRun(map, x0, x1, y, full);
            }

            map.gameObject.AddComponent<SolidOneWay>();
        }

        /// <summary>칸 전체를 채우는 타일은 보이는 사각형 전체가 벽이다. 얇은 발판만 윗면을 쓴다.</summary>
        private static bool FillsCell(Tilemap map, Vector3Int cell)
        {
            return map.GetTile(cell) is Tile tile && tile.colliderType == Tile.ColliderType.Grid;
        }

        private static void RefreshGroundColliders()
        {
            var composites = Object.FindObjectsByType<CompositeCollider2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < composites.Length; i++)
            {
                var composite = composites[i];
                if (composite == null || composite.GetComponent<PlatformEffector2D>() != null) continue;
                var tileCollider = composite.GetComponent<TilemapCollider2D>();
                if (tileCollider == null) continue;
                tileCollider.enabled = false;
                tileCollider.enabled = true;
                tileCollider.ProcessTilemapChanges();
                composite.GenerateGeometry();
            }
        }

        private static void AddRun(Tilemap map, int x0, int x1, int y, bool fullCell)
        {
            Vector3 bottomLeft = map.CellToWorld(new Vector3Int(x0, y, 0));
            Vector3 topRight = map.CellToWorld(new Vector3Int(x1 + 1, y + 1, 0));
            float width = topRight.x - bottomLeft.x;
            float tileHeight = Mathf.Abs(topRight.y - bottomLeft.y);
            float top = Mathf.Max(bottomLeft.y, topRight.y);
            float bottom = Mathf.Min(bottomLeft.y, topRight.y);
            float thickness = fullCell ? tileHeight : Mathf.Max(0.45f, tileHeight * 0.5f);
            float centerY = fullCell ? (bottom + top) * 0.5f : top - thickness * 0.5f;
            var worldCenter = new Vector3(bottomLeft.x + width * 0.5f, centerY, 0f);
            Vector3 local = map.transform.InverseTransformPoint(worldCenter);

            var box = map.gameObject.AddComponent<BoxCollider2D>();
            box.usedByEffector = true;
            box.offset = local;
            box.size = new Vector2(Mathf.Abs(width), thickness);
        }

        private sealed class SolidOneWay : MonoBehaviour
        {
        }
    }
}
