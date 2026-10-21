using UnityEngine;
using UnityEngine.Tilemaps;

namespace Gstarr.Items
{
    /// <summary>Items 타일맵에 그려진 ItemTile 들을 게임 시작 시 ItemPickup 오브젝트로 바꿈.</summary>
    [RequireComponent(typeof(Tilemap))]
    public class ItemTileSpawner : MonoBehaviour
    {
        [SerializeField] private int sortingOrder = 4;

        private void Start()
        {
            var tilemap = GetComponent<Tilemap>();
            tilemap.CompressBounds();

            foreach (var cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(cell) is not ItemTile tile || tile.Item == null) continue;
                ItemPickup.Spawn(tile.Item, tile.Count, tilemap.GetCellCenterWorld(cell), sortingOrder);
                tilemap.SetTile(cell, null);
            }
        }
    }
}
