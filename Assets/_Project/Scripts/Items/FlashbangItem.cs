using UnityEngine;

namespace Gstarr.Items
{
    [CreateAssetMenu(menuName = "Gstarr/Items/Flashbang", fileName = "Flashbang")]
    public class FlashbangItem : ItemData
    {
        [SerializeField] private float stunDuration = 4f;
        [SerializeField] private float radius = 4f;
        [SerializeField] private float throwRange = 12f;
        [SerializeField] private float throwSpeed = 11f;
        [SerializeField] private float arcHeight = 1.5f;
        [SerializeField] private LayerMask hitMask = ~0;

        public override bool Use(GameObject user)
        {
            return ThrownItemEffect.Throw(user, Icon, IconTint, Icon, radius, 0,
                throwRange, throwSpeed, arcHeight, hitMask, stunDuration);
        }
    }
}
