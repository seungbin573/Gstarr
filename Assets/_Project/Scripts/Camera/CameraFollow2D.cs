using UnityEngine;

namespace Gstarr.CameraSystem
{
    public class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 offset = new Vector2(0f, 1.5f);
        [SerializeField] private float smoothTime = 0.15f;
        [Tooltip("플레이어가 바라보는 방향으로 카메라를 미리 당겨줌")]
        [SerializeField] private float lookAheadDistance = 1.5f;
        [SerializeField] private float lookAheadSmoothTime = 0.4f;

        [Header("Bounds (선택)")]
        [SerializeField] private bool useBounds = false;
        [SerializeField] private Vector2 minBounds = new Vector2(-50f, -10f);
        [SerializeField] private Vector2 maxBounds = new Vector2(50f, 30f);

        private Vector3 velocity;
        private float lookAhead;
        private float lookAheadVelocity;
        private Camera cam;
        private Transform focus;
        private float focusSize = 3.2f;
        private float baseSize = 6f;
        private bool focusing;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam != null) baseSize = cam.orthographicSize;
        }

        public void BeginFocus(Transform focusTarget, float orthographicSize)
        {
            focus = focusTarget;
            focusSize = orthographicSize;
            focusing = true;
        }

        public void EndFocus()
        {
            focusing = false;
            focus = null;
        }

        private void LateUpdate()
        {
            Transform follow = focusing && focus != null ? focus : target;
            if (follow == null) return;

            float dt = focusing ? Time.unscaledDeltaTime : Time.deltaTime;
            float facing = Mathf.Sign(follow.localScale.x);
            float lookTarget = focusing ? 0f : facing * lookAheadDistance;
            lookAhead = Mathf.SmoothDamp(lookAhead, lookTarget, ref lookAheadVelocity, lookAheadSmoothTime, Mathf.Infinity, dt);

            Vector3 desired = new Vector3(
                follow.position.x + (focusing ? 0f : offset.x) + lookAhead,
                follow.position.y + (focusing ? 0.4f : offset.y),
                transform.position.z);

            Vector3 pos = Vector3.SmoothDamp(transform.position, desired, ref velocity, focusing ? 0.08f : smoothTime, Mathf.Infinity, dt);
            if (cam != null && cam.orthographic)
            {
                float sizeTarget = focusing ? focusSize : baseSize;
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, sizeTarget, 1f - Mathf.Exp(-dt * (focusing ? 8f : 4f)));
            }

            if (useBounds && cam != null && cam.orthographic)
            {
                float halfH = cam.orthographicSize;
                float halfW = halfH * cam.aspect;
                pos.x = Mathf.Clamp(pos.x, minBounds.x + halfW, Mathf.Max(minBounds.x + halfW, maxBounds.x - halfW));
                pos.y = Mathf.Clamp(pos.y, minBounds.y + halfH, Mathf.Max(minBounds.y + halfH, maxBounds.y - halfH));
            }

            transform.position = pos;
        }

        private void OnDrawGizmosSelected()
        {
            if (!useBounds) return;
            Gizmos.color = Color.cyan;
            Vector3 center = (minBounds + maxBounds) * 0.5f;
            Vector3 size = maxBounds - minBounds;
            Gizmos.DrawWireCube(center, size);
        }
    }
}
