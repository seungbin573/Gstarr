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

        /// <summary>런타임에 픽업 오브젝트 생성 (타일 배치 등에서 사용)</summary>
        public static ItemPickup Spawn(ItemData item, int count, Vector3 position, int sortingOrder = 4)
        {
            var go = new GameObject($"Pickup ({item.name})");
            go.transform.position = position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = item.Icon;
            sr.sortingOrder = sortingOrder;

            var circle = go.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            // 타일 위에 그려도 그 위를 걷는 플레이어에게 닿도록 위로 올린 판정
            circle.radius = 0.6f;
            circle.offset = new Vector2(0f, 0.5f);

            var pickup = go.AddComponent<ItemPickup>();
            pickup.item = item;
            pickup.count = count;
            return pickup;
        }

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void Start()
        {
            startPos = transform.position;
            if (item != null && item.Icon != null && TryGetComponent(out SpriteRenderer sr) && sr.sprite == null)
                sr.sprite = item.Icon;

            // 생성 순간 이미 겹쳐 있으면 Enter 이벤트가 없으므로 직접 확인
            if (TryGetComponent(out Collider2D col))
                foreach (var hit in Physics2D.OverlapBoxAll(col.bounds.center, col.bounds.size, 0f))
                    TryCollect(hit);
        }

        private void Update()
        {
            transform.position = startPos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryCollect(other);
        }

        private void TryCollect(Collider2D other)
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
