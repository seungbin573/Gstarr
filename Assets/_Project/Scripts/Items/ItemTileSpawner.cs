using UnityEngine;
using UnityEngine.Tilemaps;

namespace Gstarr.Items
{
    /// <summary>
    /// 타일맵에 그려진 ItemTile 을 게임 시작 시 ItemPickup 으로 바꿈.
    /// 어떤 타일맵에 그렸든 동작하도록 씬의 모든 타일맵을 검사한다.
    /// </summary>
    public class ItemTileSpawner : MonoBehaviour
    {
        [SerializeField] private int sortingOrder = 4;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("ItemTileBootstrap");
            go.AddComponent<ItemTileSpawner>();
        }

        private void Start()
        {
            var own = GetComponent<Tilemap>();
            if (own != null)
            {
                Convert(own);
                return;
            }

            foreach (var tilemap in FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                Convert(tilemap);
        }

        private void Convert(Tilemap tilemap)
        {
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
