using UnityEngine;

namespace Gstarr.CameraSystem
{
    /// <summary>
    /// 화면을 채우는 원경. 카메라보다 느리게 따라가서
    /// 플레이어가 움직일 때 멀리 있는 배경처럼 천천히 흐른다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [DefaultExecutionOrder(200)]
    public class ParallaxBackground : MonoBehaviour
    {
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("가로로 카메라를 따라가는 비율. 0이면 월드에 고정되고, 1이면 화면에 붙는다.")]
        private float followX = 0.4f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("세로로 카메라를 따라가는 비율. 1에 가까울수록 점프해도 배경이 화면을 비우지 않는다.")]
        private float followY = 0.92f;

        [SerializeField] private int sortingOrder = -100;
        [SerializeField] private float coveragePadding = 1.2f;

        private const int TileCount = 3;

        private SpriteRenderer source;
        private SpriteRenderer[] tiles;
        private Camera cam;
        private Vector3 tileScale;
        private float tileSpan;
        private Vector3 anchor;
        private bool hasAnchor;
        private float fittedSize;
        private float fittedAspect;

        private void Awake()
        {
            source = GetComponent<SpriteRenderer>();
            source.sortingOrder = sortingOrder;
            source.enabled = false;
            transform.localScale = Vector3.one;
        }

        private void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null || !cam.orthographic || source.sprite == null) return;

            Fit(cam);
            if (!hasAnchor)
            {
                anchor = cam.transform.position;
                hasAnchor = true;
            }

            Place(cam);
        }

        private void Fit(Camera camera)
        {
            float size = camera.orthographicSize;
            float aspect = camera.aspect;
            if (tiles != null && Mathf.Approximately(size, fittedSize) && Mathf.Approximately(aspect, fittedAspect))
                return;

            Vector2 spriteSize = source.sprite.bounds.size;
            if (spriteSize.x < 0.001f || spriteSize.y < 0.001f) return;

            float viewH = size * 2f * coveragePadding;
            float viewW = size * 2f * aspect * coveragePadding;
            float scale = Mathf.Max(viewW / spriteSize.x, viewH / spriteSize.y);
            tileScale = new Vector3(scale, scale, 1f);
            // 타일보다 조금 좁게 배치해 이음새에 빈 줄이 생기지 않게 한다.
            tileSpan = spriteSize.x * scale * 0.995f;

            EnsureTiles();
            fittedSize = size;
            fittedAspect = aspect;
        }

        private void EnsureTiles()
        {
            if (tiles == null || tiles.Length != TileCount || tiles[0] == null)
            {
                tiles = new SpriteRenderer[TileCount];
                for (int i = 0; i < TileCount; i++)
                {
                    var go = new GameObject("Background Tile");
                    go.hideFlags = HideFlags.DontSave;
                    go.transform.SetParent(transform, false);
                    tiles[i] = go.AddComponent<SpriteRenderer>();
                }
            }

            for (int i = 0; i < TileCount; i++)
            {
                tiles[i].sprite = source.sprite;
                tiles[i].sortingLayerID = source.sortingLayerID;
                tiles[i].sortingOrder = sortingOrder;
                tiles[i].color = source.color;
            }
        }

        private void Place(Camera camera)
        {
            if (tiles == null || tiles[0] == null) return;

            Vector3 camPos = camera.transform.position;
            float baseX = anchor.x + (camPos.x - anchor.x) * followX;
            float baseY = anchor.y + (camPos.y - anchor.y) * followY;

            float offset = Mathf.Repeat(camPos.x - baseX + tileSpan * 0.5f, tileSpan) - tileSpan * 0.5f;
            float midX = camPos.x - offset;
            int mid = TileCount / 2;

            for (int i = 0; i < TileCount; i++)
            {
                tiles[i].transform.position = new Vector3(midX + (i - mid) * tileSpan, baseY, 0f);
                tiles[i].transform.localScale = tileScale;
            }
        }
    }
}
