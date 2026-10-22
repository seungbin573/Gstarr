using Gstarr.Combat;
using UnityEngine;

namespace Gstarr.Items
{
    [CreateAssetMenu(menuName = "Gstarr/Items/Damage Potion", fileName = "DamagePotion")]
    public class DamageBoostItem : ItemData
    {
        [SerializeField] private int damageBonus = 10;
        [SerializeField] private float duration = 10f;

        public override bool Use(GameObject user)
        {
            if (!user.TryGetComponent(out PlayerCombat combat)) return false;
            combat.SetTemporaryDamageBonus(damageBonus, duration);
            return true;
        }
    }
}
