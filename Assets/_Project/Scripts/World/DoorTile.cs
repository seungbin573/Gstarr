using UnityEngine;
using UnityEngine.Tilemaps;

namespace Gstarr.World
{
    /// <summary>그리드에 칠하는 문 칸. 같은 세로줄이 하나의 문이 된다.</summary>
    [CreateAssetMenu(menuName = "Gstarr/World/Door Tile", fileName = "DoorTile")]
    public class DoorTile : TileBase
    {
        [SerializeField] private Sprite icon;

        public void Setup(Sprite icon) => this.icon = icon;

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.sprite = icon;
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.LockAll;
            tileData.colliderType = Tile.ColliderType.None;
        }
    }
}
