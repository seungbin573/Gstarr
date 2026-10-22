using UnityEngine;

namespace Gstarr.Items
{
    /// <summary>적을 향해 던져 폭발시키는 폭탄.</summary>
    [CreateAssetMenu(menuName = "Gstarr/Items/Area Damage Item", fileName = "AreaDamageItem")]
    public class AreaDamageItem : ItemData
    {
        [SerializeField] private int damage = 3;
        [SerializeField] private float radius = 2.5f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private Sprite effectSprite;
        [SerializeField] private float throwRange = 12f;
        [SerializeField] private float throwSpeed = 11f;
        [SerializeField] private float arcHeight = 1.5f;

        public override bool Use(GameObject user)
        {
            return ThrownItemEffect.Throw(user, Icon, IconTint, effectSprite, radius, damage,
                throwRange, throwSpeed, arcHeight, hitMask, 0f);
        }
    }
}
