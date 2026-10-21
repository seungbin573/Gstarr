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

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            float facing = Mathf.Sign(target.localScale.x);
            lookAhead = Mathf.SmoothDamp(lookAhead, facing * lookAheadDistance, ref lookAheadVelocity, lookAheadSmoothTime);

            Vector3 desired = new Vector3(
                target.position.x + offset.x + lookAhead,
                target.position.y + offset.y,
                transform.position.z);

            Vector3 pos = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);

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
