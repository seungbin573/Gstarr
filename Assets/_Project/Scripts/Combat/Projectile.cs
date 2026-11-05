using System;
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

            // 빠른 탄이 얇은 콜라이더를 뚫지 않도록 이동 경로를 레이캐스트
            RaycastHit2D hit = Physics2D.Raycast(pos, step.normalized, Mathf.Max(0.01f, step.magnitude), hitMask);
            if (hit.collider != null)
            {
                if (owner == null || (hit.collider.transform != owner.transform && !hit.collider.transform.IsChildOf(owner.transform)))
                {
                    onHit?.Invoke(hit.collider);
                    var target = hit.collider.GetComponentInParent<Health>();
                    if (damage > 0 && target != null && target.TakeDamage(damage, velocity))
                        ComboSystem.RegisterHit(hit.collider.gameObject, judgement);
                }
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
