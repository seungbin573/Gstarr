using UnityEngine;
using UnityEngine.Tilemaps;

namespace Gstarr.Combat
{
    /// <summary>Enemies 타일맵에 배치한 적 타일을 씬 시작 때 실제 적으로 바꾼다.</summary>
    [RequireComponent(typeof(Tilemap))]
    public class EnemyTileSpawner : MonoBehaviour
    {
        private void Start()
        {
            var tilemap = GetComponent<Tilemap>();
            tilemap.CompressBounds();
            foreach (var cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(cell) is not EnemyTile tile) continue;
                Vector3 bottom = tilemap.CellToWorld(cell);
                Vector3 center = tilemap.GetCellCenterWorld(cell);
                var position = new Vector3(center.x, bottom.y, center.z);
                if (tile.Kind == EnemyKind.Boss) EnemyActor.SpawnBoss(position);
                else EnemyActor.Spawn(null, position, tile.Kind);
                tilemap.SetTile(cell, null);
            }
        }
    }
}
