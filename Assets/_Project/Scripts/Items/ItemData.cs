using UnityEngine;

namespace Gstarr.Items
{
    /// <summary>
    /// 모든 아이템의 기본 클래스. 새 아이템은 이 클래스를 상속해 Use 를 구현하고
    /// [CreateAssetMenu] 를 붙여 에셋으로 만들어 쓴다.
    /// </summary>
    public abstract class ItemData : ScriptableObject
    {
        [SerializeField] private string displayName = "Item";
        [SerializeField] private Sprite icon;
        [Tooltip("한 슬롯에 겹칠 수 있는 최대 개수")]
        [SerializeField] private int maxStack = 9;
        [Tooltip("사용하면 개수가 줄어드는지")]
        [SerializeField] private bool consumeOnUse = true;

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int MaxStack => Mathf.Max(1, maxStack);
        public bool ConsumeOnUse => consumeOnUse;

        /// <summary>사용에 성공하면 true (실패하면 개수가 줄지 않음)</summary>
        public abstract bool Use(GameObject user);
    }
}
