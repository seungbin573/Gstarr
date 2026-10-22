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
        private Transform focusTarget;
        private bool focusing;
        private float previousSize;

        public void BeginFocus(Transform subject, float orthographicSize)
        {
            if (subject == null) return;
            if (cam == null) cam = GetComponent<Camera>();
            if (!focusing && cam != null) previousSize = cam.orthographicSize;
            focusing = true;
            focusTarget = subject;
            velocity = Vector3.zero;
            if (cam != null) cam.orthographicSize = Mathf.Max(0.01f, orthographicSize);
        }

        public void EndFocus()
        {
            if (!focusing) return;
            if (cam != null) cam.orthographicSize = previousSize;
            focusing = false;
            focusTarget = null;
            velocity = Vector3.zero;
        }

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
            if (focusing && focusTarget == null) EndFocus();
            if (focusing)
            {
                Vector3 focusPosition = focusTarget.position;
                focusPosition.z = transform.position.z;
                transform.position = Vector3.SmoothDamp(transform.position, focusPosition,
                    ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
                return;
            }
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
