using UnityEngine;
using UnityEngine.Tilemaps;

namespace Gstarr.Items
{
    /// <summary>
    /// 타일 팔레트로 아이템을 배치하기 위한 타일. 에디터에서는 아이콘으로 보이고,
    /// 게임 시작 시 ItemTileSpawner 가 실제 ItemPickup 으로 바꿔줌.
    /// </summary>
    [CreateAssetMenu(menuName = "Gstarr/Items/Item Tile", fileName = "ItemTile")]
    public class ItemTile : TileBase
    {
        [SerializeField] private ItemData item;
        [SerializeField] private int count = 1;

        public ItemData Item => item;
        public int Count => Mathf.Max(1, count);

        public void Setup(ItemData item, int count = 1)
        {
            this.item = item;
            this.count = count;
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.sprite = item != null ? item.Icon : null;
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.LockAll;
            tileData.colliderType = Tile.ColliderType.None;
        }
    }
}
