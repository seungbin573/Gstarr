using UnityEngine;

namespace Gstarr.Items
{
    /// <summary>플레이어가 닿으면 인벤토리에 아이템을 넣고 사라지는 오브젝트. Collider2D 는 Is Trigger 로 설정.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class ItemPickup : MonoBehaviour
    {
        [SerializeField] private ItemData item;
        [SerializeField] private int count = 1;
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float bobSpeed = 3f;

        private Vector3 startPos;

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void Start()
        {
            startPos = transform.position;
            if (item != null && item.Icon != null && TryGetComponent(out SpriteRenderer sr) && sr.sprite == null)
                sr.sprite = item.Icon;
        }

        private void Update()
        {
            transform.position = startPos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (item == null) return;
            var inventory = other.GetComponentInParent<PlayerInventory>();
            if (inventory == null) return;

            int left = inventory.Add(item, count);
            if (left <= 0) Destroy(gameObject);
            else count = left;
        }
    }
}
