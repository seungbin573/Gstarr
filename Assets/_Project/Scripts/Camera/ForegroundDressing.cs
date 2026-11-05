using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Gstarr.CameraSystem
{
    /// <summary>
    /// 교회 기둥을 플레이어보다 앞에 두고, 카메라보다 빨리 지나가게 해 가까운 장식처럼 보이게 한다.
    /// </summary>
    public class ForegroundDressing : MonoBehaviour
    {
        [SerializeField] private Sprite[] pillars;
        [SerializeField] private float zoneStart = 432f;
        [SerializeField] private float zoneEnd = 462f;
        [SerializeField] private float spacing = 15f;

        private readonly List<Prop> props = new List<Prop>();
        private Camera cam;

        private struct Prop
        {
            public Transform Body;
            public float AnchorX;
            public float FloorY;
            public float Factor;
        }

        private void Start()
        {
            if (pillars == null || pillars.Length == 0) return;
            var ground = FindGround();
            int count = 0;
            for (float x = zoneStart; x <= zoneEnd; x += spacing, count++)
            {
                float side = (count % 2 == 0 ? -1f : 1f) * 7.2f;
                AddPillar(
                    pillars[count % pillars.Length],
                    x + side,
                    FloorTop(ground, x),
                    1.16f,
                    3,
                    4.2f);
            }
        }

        private void AddPillar(Sprite sprite, float anchorX, float floor, float factor, int order, float height)
        {
            if (sprite == null) return;
            var go = new GameObject("Pillar");
            go.transform.SetParent(transform, false);
            var view = go.AddComponent<SpriteRenderer>();
            view.sprite = sprite;
            view.sortingOrder = order;
            view.color = new Color(1f, 1f, 1f, 0.82f);
            float spriteHeight = sprite.bounds.size.y;
            float scale = spriteHeight > 0.01f ? height / spriteHeight : 1f;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            props.Add(new Prop
            {
                Body = go.transform,
                AnchorX = anchorX,
                FloorY = floor - sprite.bounds.min.y * scale,
                Factor = factor
            });
        }

        private void LateUpdate()
        {
            if (props.Count == 0) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            float cameraX = cam.transform.position.x;
            for (int i = 0; i < props.Count; i++)
            {
                Prop prop = props[i];
                float x = cameraX + (prop.AnchorX - cameraX) * prop.Factor;
                prop.Body.position = new Vector3(x, prop.FloorY, 0f);
            }
        }

        private Tilemap FindGround()
        {
            var maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i] != null && maps[i].name == "Ground") return maps[i];
            }

            return null;
        }

        private static float FloorTop(Tilemap ground, float worldX)
        {
            if (ground == null) return 0f;
            int x = ground.WorldToCell(new Vector3(worldX, 0f, 0f)).x;
            var bounds = ground.cellBounds;
            for (int y = bounds.yMax - 1; y >= bounds.yMin; y--)
            {
                if (!ground.HasTile(new Vector3Int(x, y, 0))) continue;
                return ground.CellToWorld(new Vector3Int(x, y + 1, 0)).y;
            }

            return 0f;
        }
    }
}
