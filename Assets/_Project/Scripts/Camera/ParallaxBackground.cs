using Gstarr.Game;
using UnityEngine;

namespace Gstarr.CameraSystem
{
    /// <summary>
    /// 화면보다 큰 원경 한 장을 깔고, 카메라가 그 위를 지나가는 것처럼 위치를 옮긴다.
    /// 그림의 오른쪽 끝에 닿으면 지금 배경을 페이드 아웃하고 다음 배경을 드러낸다.
    /// 마지막 배경의 끝에서는 그 장면을 유지한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [DefaultExecutionOrder(200)]
    public class ParallaxBackground : MonoBehaviour
    {
        [SerializeField] private int sortingOrder = -100;

        [SerializeField]
        [Tooltip("이동 순서. 앞쪽부터 forest, ball, forest_snow, church.")]
        private Sprite[] backgrounds;

        [SerializeField]
        [Min(1f)]
        [Tooltip("화면보다 배경을 몇 배로 크게 둘지. 1에 가까울수록 그림 전체가 화면에 들어온다.")]
        private float giantScale = 1.15f;

        [SerializeField]
        [Min(1f)]
        [Tooltip("이 거리만큼 오른쪽으로 이동하면 그림의 오른쪽 끝에 닿아 다음 배경으로 넘어간다.")]
        private float segmentDistance = 56f;

        [SerializeField]
        [Min(0.05f)]
        [Tooltip("끝에 닿았을 때 다음 배경으로 바뀌는 시간(초).")]
        private float fadeDuration = 0.8f;

        private const float VerticalDrift = 0.45f;

        private SpriteRenderer source;
        private SpriteRenderer front;
        private SpriteRenderer back;
        private Camera cam;
        private float originX;
        private float originY;
        private bool hasOrigin;
        private int index;
        private bool resumeApplied;
        private float fade;
        private int fadeDir;

        /// <summary>앞으로 다음 배경에 들어섰을 때. 인자는 새 배경 번호와 그 그림.</summary>
        public static event System.Action<int, Sprite> BackgroundAdvanced;

        public Sprite SpriteAt(int backgroundIndex)
        {
            if (backgrounds == null || backgroundIndex < 0 || backgroundIndex >= backgrounds.Length) return null;
            return backgrounds[backgroundIndex];
        }

        private struct Frame
        {
            public float scale;
            public float width;
            public float height;
            public Vector2 center;
            public float viewWidth;
            public float viewHeight;
        }

        public void Configure(Sprite[] sprites)
        {
            backgrounds = sprites;
            if (source == null) source = GetComponent<SpriteRenderer>();
            if (backgrounds != null && backgrounds.Length > 0 && backgrounds[0] != null && source != null)
                source.sprite = backgrounds[0];
        }

        private void Awake()
        {
            source = GetComponent<SpriteRenderer>();
            source.sortingOrder = sortingOrder;
            source.enabled = false;
            transform.localScale = Vector3.one;
            if (backgrounds != null && backgrounds.Length > 0 && backgrounds[0] != null)
                source.sprite = backgrounds[0];
        }

        private void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null || !cam.orthographic) return;
            if (backgrounds == null || backgrounds.Length == 0 || backgrounds[0] == null) return;

            if (!hasOrigin)
            {
                originX = RunCheckpoint.ResumedThisLoad ? 3f : cam.transform.position.x;
                originY = cam.transform.position.y;
                hasOrigin = true;
            }

            float segment = Mathf.Max(1f, segmentDistance);
            if (RunCheckpoint.ResumedThisLoad && !resumeApplied)
            {
                resumeApplied = true;
                int last = backgrounds.Length - 1;
                float traveled = Mathf.Max(0f, cam.transform.position.x - originX);
                index = Mathf.Min(Mathf.FloorToInt(traveled / segment), last);
                fade = 0f;
                fadeDir = 0;
            }

            EnsureLayers();
            UpdateFade(cam.transform.position.x, segment);
            Draw(segment);
        }

        private void UpdateFade(float cameraX, float span)
        {
            int last = backgrounds.Length - 1;
            float traveled = Mathf.Max(0f, cameraX - originX);
            int target = Mathf.Min(Mathf.FloorToInt(traveled / span), last);
            float step = Time.deltaTime / Mathf.Max(0.05f, fadeDuration);

            if (fadeDir == 0)
            {
                if (target > index) fadeDir = 1;
                else if (target < index) fadeDir = -1;
            }

            if (fadeDir == 1)
            {
                fade = Mathf.MoveTowards(fade, target > index ? 1f : 0f, step);
                if (fade <= 0f)
                {
                    fade = 0f;
                    fadeDir = 0;
                }
                else if (fade >= 1f)
                {
                    index = Mathf.Min(index + 1, last);
                    fade = 0f;
                    fadeDir = 0;
                    BackgroundAdvanced?.Invoke(index, SpriteAt(index));
                }
            }
            else if (fadeDir == -1)
            {
                fade = Mathf.MoveTowards(fade, target < index ? 1f : 0f, step);
                if (fade <= 0f)
                {
                    fade = 0f;
                    fadeDir = 0;
                }
                else if (fade >= 1f)
                {
                    index = Mathf.Max(index - 1, 0);
                    fade = 0f;
                    fadeDir = 0;
                }
            }
        }

        private void Draw(float span)
        {
            float cameraX = cam.transform.position.x;
            float local = cameraX - originX - index * span;
            float currentT = Mathf.Clamp01(local / span);
            int last = backgrounds.Length - 1;

            if (fadeDir == 1 && index < last)
            {
                float nextT = Mathf.Clamp01((local - span) / span);
                Place(back, backgrounds[index + 1], nextT, 1f, sortingOrder);
                Place(front, backgrounds[index], currentT, 1f - fade, sortingOrder + 1);
            }
            else if (fadeDir == -1 && index > 0)
            {
                float previousT = Mathf.Clamp01((local + span) / span);
                Place(back, backgrounds[index - 1], previousT, 1f, sortingOrder);
                Place(front, backgrounds[index], currentT, 1f - fade, sortingOrder + 1);
            }
            else
            {
                Place(front, backgrounds[index], currentT, 1f, sortingOrder);
                back.enabled = false;
            }
        }

        private void Place(SpriteRenderer renderer, Sprite sprite, float t, float alpha, int order)
        {
            if (renderer == null || sprite == null)
            {
                if (renderer != null) renderer.enabled = false;
                return;
            }

            Frame frame = Measure(cam, sprite);
            float cameraX = cam.transform.position.x;
            float cameraY = cam.transform.position.y;
            float extraX = Mathf.Max(0f, frame.width - frame.viewWidth);
            float centerX = cameraX + (0.5f - Mathf.Clamp01(t)) * extraX;
            float spareY = Mathf.Max(0f, (frame.height - frame.viewHeight) * 0.5f);
            float shiftY = Mathf.Clamp((originY - cameraY) * VerticalDrift, -spareY, spareY);
            var scale = new Vector3(frame.scale, frame.scale, 1f);
            Vector3 visualCenter = new Vector3(centerX, cameraY + shiftY, 0f);

            renderer.enabled = true;
            renderer.sprite = sprite;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = order;
            Color color = source.color;
            color.a *= alpha;
            renderer.color = color;
            renderer.transform.localScale = scale;
            renderer.transform.position = visualCenter - Vector3.Scale((Vector3)frame.center, scale);
        }

        private Frame Measure(Camera camera, Sprite sprite)
        {
            float viewH = camera.orthographicSize * 2f;
            float viewW = viewH * camera.aspect;
            Vector2 size = sprite.bounds.size;
            float cover = Mathf.Max(viewW / Mathf.Max(0.001f, size.x), viewH / Mathf.Max(0.001f, size.y));
            float scale = cover * Mathf.Max(1f, giantScale);
            return new Frame
            {
                scale = scale,
                width = size.x * scale,
                height = size.y * scale,
                center = sprite.bounds.center,
                viewWidth = viewW,
                viewHeight = viewH,
            };
        }

        private void EnsureLayers()
        {
            if (front != null && back != null) return;

            front = CreateLayer("Background Front");
            back = CreateLayer("Background Back");
        }

        private SpriteRenderer CreateLayer(string layerName)
        {
            var go = new GameObject(layerName);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);
            return go.AddComponent<SpriteRenderer>();
        }
    }
}
