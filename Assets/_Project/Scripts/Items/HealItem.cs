using Gstarr.Combat;
using UnityEngine;

namespace Gstarr.Items
{
    [CreateAssetMenu(menuName = "Gstarr/Items/Heal Item", fileName = "HealItem")]
    public class HealItem : ItemData
    {
        [SerializeField] private int healAmount = 2;

        public override bool Use(GameObject user)
        {
            if (!user.TryGetComponent(out Health health)) return false;
            return health.Heal(healAmount);
        }
    }
}
