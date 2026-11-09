using System;
using Gstarr.Items;
using Gstarr.Rhythm;
using UnityEngine;

namespace Gstarr.Combat
{
    public class Projectile : MonoBehaviour
    {
        private Sprite[] animationFrames;
        private SpriteRenderer sprite;
        private float animationTime;
        private Vector2 velocity;
        private Vector2 gravity;
        private int damage;
        private LayerMask hitMask;
        private float lifeTimer;
        private GameObject owner;
        private BeatJudgement judgement;
        private Action<Collider2D> onHit;

        public void Launch(Vector2 velocity, int damage, float range, LayerMask hitMask, GameObject owner = null,
            BeatJudgement judgement = BeatJudgement.Perfect, Action<Collider2D> onHit = null)
        {
            this.velocity = velocity;
            this.damage = damage;
            this.hitMask = hitMask;
            this.owner = owner;
            this.judgement = judgement;
            this.onHit = onHit;
            lifeTimer = range / Mathf.Max(0.01f, velocity.magnitude);

            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        public void SetGravity(Vector2 gravity) => this.gravity = gravity;

        public void SetAnimation(Sprite[] frames)
        {
            animationFrames = frames;
            sprite = GetComponent<SpriteRenderer>();
            animationTime = 0f;
            if (sprite != null && frames != null && frames.Length > 0) sprite.sprite = frames[0];
        }

        private void Update()
        {
            if (sprite == null || animationFrames == null || animationFrames.Length < 2) return;
            animationTime += Time.deltaTime;
            sprite.sprite = animationFrames[Mathf.FloorToInt(animationTime / 0.1f) % animationFrames.Length];
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            velocity += gravity * dt;
            Vector2 pos = transform.position;
            Vector2 step = velocity * dt;

            // 빠른 탄이 얇은 벽을 뚫지 않게 경로를 본다. 아이템은 지나간다.
            float distance = Mathf.Max(0.01f, step.magnitude);
            var hits = Physics2D.RaycastAll(pos, step.normalized, distance, hitMask);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i].collider;
                if (col == null) continue;
                if (owner != null && (col.transform == owner.transform || col.transform.IsChildOf(owner.transform))) continue;
                if (col.GetComponentInParent<ItemPickup>() != null) continue;
                onHit?.Invoke(col);
                var target = col.GetComponentInParent<Health>();
                bool fromEnemy = owner != null && owner.GetComponentInParent<EnemyActor>() != null;
                bool landed = damage > 0 && target != null && (fromEnemy
                    ? target.TakeEnemyHit(damage, velocity)
                    : target.TakeDamage(damage, velocity));
                if (landed)
                    ComboSystem.RegisterHit(col.gameObject, judgement);
                Destroy(gameObject);
                return;
            }

            transform.position = pos + step;
            if (velocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);

            lifeTimer -= dt;
            if (lifeTimer <= 0f) Destroy(gameObject);
        }
    }
}
