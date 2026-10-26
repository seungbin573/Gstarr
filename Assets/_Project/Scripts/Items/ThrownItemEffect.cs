using System.Collections.Generic;
using Gstarr.Combat;
using Gstarr.Player;
using Gstarr.Rhythm;
using UnityEngine;

namespace Gstarr.Items
{
    /// <summary>폭탄과 섬광탄의 조준 투척, 비행, 착탄 효과를 처리한다.</summary>
    public class ThrownItemEffect : MonoBehaviour
    {
        private Transform target;
        private Vector2 origin;
        private Vector2 fallbackTarget;
        private Sprite effectSprite;
        private float radius;
        private float arcHeight;
        private float flightDuration;
        private float flightTimer;
        private float stunDuration;
        private int damage;
        private LayerMask hitMask;
        private BeatJudgement judgement;

        public static bool Throw(GameObject user, Sprite icon, Color tint, Sprite effectSprite, float radius,
            int damage, float range, float speed, float arcHeight, LayerMask hitMask, float stunDuration)
        {
            if (user == null) return false;

            Vector2 start = user.transform.position;
            var enemy = EnemyActor.NearestInRange(start, range);
            int facing = user.TryGetComponent(out PlayerController controller) ? controller.FacingDirection : 1;
            Vector2 destination = enemy != null
                ? enemy.AimPoint
                : start + Vector2.right * (facing * range);

            var go = new GameObject(stunDuration > 0f ? "ThrownFlashbang" : "ThrownBomb");
            go.transform.position = start;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = icon;
            renderer.color = tint;
            renderer.sortingOrder = 12;

            var thrown = go.AddComponent<ThrownItemEffect>();
            thrown.target = enemy != null ? enemy.transform : null;
            thrown.origin = start;
            thrown.fallbackTarget = destination;
            thrown.effectSprite = effectSprite;
            thrown.radius = Mathf.Max(0.1f, radius);
            thrown.damage = Mathf.Max(0, damage);
            thrown.hitMask = hitMask;
            thrown.stunDuration = Mathf.Max(0f, stunDuration);
            thrown.arcHeight = Mathf.Max(0f, arcHeight);
            thrown.flightDuration = Mathf.Clamp(Vector2.Distance(start, destination) / Mathf.Max(0.1f, speed), 0.18f, 1.25f);
            thrown.judgement = ComboSystem.CurrentJudgement;
            return true;
        }

        private void Update()
        {
            flightTimer += Time.deltaTime;
            float t = Mathf.Clamp01(flightTimer / flightDuration);
            Vector2 destination = target != null && target.TryGetComponent(out EnemyActor enemy) && !enemy.IsDead
                ? enemy.AimPoint
                : fallbackTarget;
            transform.position = Vector2.Lerp(origin, destination, t) + Vector2.up * (4f * arcHeight * t * (1f - t));
            transform.Rotate(0f, 0f, -720f * Time.deltaTime);

            if (t >= 1f) Detonate();
        }

        private void Detonate()
        {
            Vector2 center = transform.position;
            var enemies = new HashSet<EnemyActor>();
            foreach (var hit in Physics2D.OverlapCircleAll(center, radius, hitMask))
            {
                var enemy = hit.GetComponentInParent<EnemyActor>();
                if (enemy == null || enemy.IsDead || !enemies.Add(enemy)) continue;

                if (stunDuration > 0f)
                {
                    enemy.Stun(stunDuration);
                    continue;
                }

                if (damage > 0 && enemy.TryGetComponent(out Health health)
                    && health.TakeDamage(damage, (Vector2)enemy.transform.position - center + Vector2.up * 0.5f))
                    ComboSystem.RegisterHit(enemy.gameObject, judgement);
            }

            if (effectSprite != null)
            {
                var fx = new GameObject("ThrownItemImpact");
                fx.transform.position = center;
                float size = Mathf.Max(effectSprite.bounds.size.x, effectSprite.bounds.size.y);
                fx.transform.localScale = Vector3.one * (radius * 2f / Mathf.Max(0.01f, size));
                var sr = fx.AddComponent<SpriteRenderer>();
                sr.sprite = effectSprite;
                sr.color = stunDuration > 0f ? new Color(1f, 1f, 0.8f, 0.9f) : Color.white;
                sr.sortingOrder = 20;
                Destroy(fx, 0.18f);
            }

            Destroy(gameObject);
        }
    }
}
