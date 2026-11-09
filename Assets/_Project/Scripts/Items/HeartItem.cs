using Gstarr.Combat;
using UnityEngine;

namespace Gstarr.Items
{
    /// <summary>일정 시간 무적이 되고, 그 동안 주인공 색이 바뀐다.</summary>
    [CreateAssetMenu(menuName = "Gstarr/Items/Heart", fileName = "Heart")]
    public class HeartItem : ItemData
    {
        [SerializeField] private float duration = 5f;
        [SerializeField] private Color bodyColor = new Color(1f, 0.46f, 0.72f, 1f);

        public override bool Use(GameObject user)
        {
            if (!user.TryGetComponent(out Health health)) return false;
            health.GrantInvulnerability(duration, bodyColor);
            return true;
        }
    }
}
