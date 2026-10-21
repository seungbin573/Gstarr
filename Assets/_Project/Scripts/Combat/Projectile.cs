using UnityEngine;

namespace Gstarr.Combat
{
    public class Projectile : MonoBehaviour
    {
        private Vector2 velocity;
        private int damage;
        private LayerMask hitMask;
        private float lifeTimer;
        private GameObject owner;

        public void Launch(Vector2 velocity, int damage, float range, LayerMask hitMask, GameObject owner = null)
        {
            this.velocity = velocity;
            this.damage = damage;
            this.hitMask = hitMask;
            this.owner = owner;
            lifeTimer = range / Mathf.Max(0.01f, velocity.magnitude);

            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector2 pos = transform.position;
            Vector2 step = velocity * dt;

            // 빠른 탄이 얇은 콜라이더를 뚫지 않도록 이동 경로를 레이캐스트
            RaycastHit2D hit = Physics2D.Raycast(pos, step.normalized, step.magnitude, hitMask);
            if (hit.collider != null)
            {
                if (owner == null || (hit.collider.transform != owner.transform && !hit.collider.transform.IsChildOf(owner.transform)))
                {
                    if (hit.collider.TryGetComponent(out IDamageable target) && target.TakeDamage(damage, velocity))
                        Gstarr.Rhythm.ComboSystem.RegisterHit(hit.collider.gameObject);
                }
                Destroy(gameObject);
                return;
            }

            transform.position = pos + step;

            lifeTimer -= dt;
            if (lifeTimer <= 0f) Destroy(gameObject);
        }
    }
}
