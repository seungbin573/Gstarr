using Gstarr.Combat;
using UnityEngine;

namespace Gstarr.Items
{
    /// <summary>전투 아이템 예시: 사용 위치 주변에 범위 피해</summary>
    [CreateAssetMenu(menuName = "Gstarr/Items/Area Damage Item", fileName = "AreaDamageItem")]
    public class AreaDamageItem : ItemData
    {
        [SerializeField] private int damage = 3;
        [SerializeField] private float radius = 2.5f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private Sprite effectSprite;

        public override bool Use(GameObject user)
        {
            Vector2 center = user.transform.position;
            foreach (var hit in Physics2D.OverlapCircleAll(center, radius, hitMask))
            {
                if (hit.attachedRigidbody != null && hit.attachedRigidbody.gameObject == user) continue;
                if (hit.TryGetComponent(out IDamageable target))
                    target.TakeDamage(damage, (Vector2)hit.transform.position - center + Vector2.up * 0.5f);
            }

            if (effectSprite != null)
            {
                var fx = new GameObject("AreaDamageFx");
                fx.transform.position = center;
                float size = Mathf.Max(effectSprite.bounds.size.x, effectSprite.bounds.size.y);
                fx.transform.localScale = Vector3.one * (radius * 2f / Mathf.Max(0.01f, size));
                var sr = fx.AddComponent<SpriteRenderer>();
                sr.sprite = effectSprite;
                sr.sortingOrder = 20;
                Object.Destroy(fx, 0.15f);
            }
            return true;
        }
    }
}
