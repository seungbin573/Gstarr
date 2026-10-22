using UnityEngine;
using UnityEngine.Tilemaps;

namespace Gstarr.Combat
{
    /// <summary>적 팔레트에서 고른 타일. 씬의 Enemies 타일맵에 그리면 게임 시작 시 적이 된다.</summary>
    [CreateAssetMenu(menuName = "Gstarr/Combat/Enemy Tile", fileName = "EnemyTile")]
    public class EnemyTile : TileBase
    {
        [SerializeField] private EnemyKind kind;
        [SerializeField] private Sprite icon;
        [SerializeField] private Color tint = Color.white;

        public EnemyKind Kind => kind;

        public void Setup(EnemyKind kind, Sprite icon, Color tint)
        {
            this.kind = kind;
            this.icon = icon;
            this.tint = tint;
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.sprite = icon;
            tileData.color = tint;
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.LockAll;
            tileData.colliderType = Tile.ColliderType.None;
        }
    }
}
