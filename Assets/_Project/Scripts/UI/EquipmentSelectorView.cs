using System;
using Gstarr.Items;
using UnityEngine;

namespace Gstarr.UI
{
    /// <summary>
    /// 아이템을 바꿀 때 플레이어 옆에 슬롯 캐러셀을 띄움 (야숨 화살 교체 UI 스타일).
    /// 전투 아이템은 옆쪽 세로, 효과 아이템은 머리 위 가로로 슬라이드.
    /// </summary>
    public class EquipmentSelectorView : MonoBehaviour
    {
        public const string ArtFolder = "Assets/arts";

        [SerializeField] private Sprite slotSprite;
        [Tooltip("전투 아이템 (위/아래) 세로 슬롯 위치")]
        [SerializeField] private Vector2 combatOffset = new Vector2(1.5f, 0.2f);
        [Tooltip("효과 아이템 (좌/우) 가로 슬롯 위치")]
        [SerializeField] private Vector2 effectOffset = new Vector2(0f, 1.9f);
        [SerializeField] private float slotSpacing = 1.35f;
        [SerializeField] private float iconSize = 0.9f;
        [Tooltip("마지막 교체 후 UI가 떠 있는 시간")]
        [SerializeField] private float showDuration = 1.2f;
        [SerializeField] private float fadeDuration = 0.15f;
        [SerializeField] private float slideSharpness = 18f;
        [SerializeField] private int sortingOrder = 50;
        [SerializeField] private Color selectedTint = new Color(1f, 0.92f, 0.55f);

        private PlayerInventory inventory;
        private Transform root;
        private Carousel combatCarousel;
        private Carousel effectCarousel;
        private Font font;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            root = new GameObject("ItemSelector").transform;

            if (inventory == null)
            {
                Debug.LogWarning("[EquipmentSelectorView] PlayerInventory 가 없어 아이템 UI를 표시할 수 없습니다.", this);
                return;
            }
            if (slotSprite == null)
                Debug.LogWarning("[EquipmentSelectorView] Slot Sprite 가 비어있습니다 (Assets/arts/UI/Slot.png).", this);

            combatCarousel = CreateCarousel(ItemCategory.Combat, "CombatItems", combatOffset, Vector2.down);
            effectCarousel = CreateCarousel(ItemCategory.Effect, "EffectItems", effectOffset, Vector2.right);
        }

        private Carousel CreateCarousel(ItemCategory category, string name, Vector2 offset, Vector2 axis)
        {
            return new Carousel(this, name, offset, axis,
                () => inventory.GetItems(category).Count,
                i => inventory.GetItems(category)[i].item.Icon,
                i => inventory.GetItems(category)[i].item.DisplayName,
                i => inventory.GetItems(category)[i].count);
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.SelectionChanged += OnItemChanged;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.SelectionChanged -= OnItemChanged;
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }

        private void OnItemChanged(ItemCategory category, int index, int delta)
        {
            var shown = category == ItemCategory.Combat ? combatCarousel : effectCarousel;
            var other = category == ItemCategory.Combat ? effectCarousel : combatCarousel;
            if (shown == null) return;

            // 획득 등 목록만 바뀐 경우: 이미 떠 있을 때만 갱신
            if (delta == 0 && inventory.ActiveCategory != category)
            {
                shown.SetCenter(index);
                return;
            }
            shown.Show(index, delta);
            other?.HideImmediate();
        }

        private void LateUpdate()
        {
            root.position = transform.position;
            float dt = Time.deltaTime;
            combatCarousel?.Tick(dt);
            effectCarousel?.Tick(dt);
        }
        private class Slot
        {
            public Transform transform;
            public SpriteRenderer frame;
            public SpriteRenderer icon;
            public TextMesh count;
        }

        private class Carousel
        {
            private const int Range = 2;

            private readonly EquipmentSelectorView view;
            private readonly Transform root;
            private readonly Vector2 axis;
            private readonly Func<int> getCount;
            private readonly Func<int, Sprite> getIcon;
            private readonly Func<int, string> getName;
            private readonly Func<int, int> getStack;
            private readonly Slot[] slots = new Slot[Range * 2 + 1];
            private readonly TextMesh nameLabel;

            private int center;
            private float scroll;
            private float showTimer;
            private float visibility;

            public Carousel(EquipmentSelectorView view, string name, Vector2 offset, Vector2 axis,
                Func<int> getCount, Func<int, Sprite> getIcon, Func<int, string> getName, Func<int, int> getStack)
            {
                this.view = view;
                this.axis = axis;
                this.getCount = getCount;
                this.getIcon = getIcon;
                this.getName = getName;
                this.getStack = getStack;

                root = new GameObject(name).transform;
                root.SetParent(view.root, false);
                root.localPosition = offset;

                for (int i = 0; i < slots.Length; i++)
                    slots[i] = CreateSlot(i);

                // 가로 캐러셀은 아래, 세로 캐러셀은 오른쪽에 이름 표시
                bool horizontal = Mathf.Abs(axis.x) > 0.5f;
                nameLabel = view.CreateText(root, "Name",
                    horizontal ? new Vector2(0f, -0.85f) : new Vector2(0.85f, 0f),
                    horizontal ? TextAnchor.UpperCenter : TextAnchor.MiddleLeft, 0.045f);

                root.gameObject.SetActive(false);
            }

            private Slot CreateSlot(int i)
            {
                var go = new GameObject($"Slot{i}");
                go.transform.SetParent(root, false);

                var frame = go.AddComponent<SpriteRenderer>();
                frame.sprite = view.slotSprite;
                frame.sortingOrder = view.sortingOrder;

                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(go.transform, false);
                var icon = iconGo.AddComponent<SpriteRenderer>();
                icon.sortingOrder = view.sortingOrder + 1;

                var count = view.CreateText(go.transform, "Count", new Vector2(0.5f, -0.25f), TextAnchor.LowerRight, 0.035f);

                return new Slot { transform = go.transform, frame = frame, icon = icon, count = count };
            }

            public void Show(int newIndex, int delta)
            {
                center = newIndex;
                scroll = Mathf.Clamp(scroll + delta, -Range, Range);
                showTimer = view.showDuration;
            }

            public void SetCenter(int index)
            {
                center = index;
                scroll = 0f;
            }

            public void HideImmediate()
            {
                showTimer = 0f;
                visibility = 0f;
                scroll = 0f;
                root.gameObject.SetActive(false);
            }

            public void Tick(float dt)
            {
                showTimer -= dt;
                float target = showTimer > 0f ? 1f : 0f;
                visibility = Mathf.MoveTowards(visibility, target, dt / Mathf.Max(0.01f, view.fadeDuration));

                bool visible = visibility > 0f;
                if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
                if (!visible) return;

                scroll = Mathf.Lerp(scroll, 0f, 1f - Mathf.Exp(-view.slideSharpness * dt));
                if (Mathf.Abs(scroll) < 0.001f) scroll = 0f;

                int n = getCount();
                for (int k = -Range; k <= Range; k++)
                    UpdateSlot(slots[k + Range], k, n);

                nameLabel.text = n > 0 ? getName(Mod(center, n)) : "Empty";
                nameLabel.color = new Color(1f, 1f, 1f, visibility * Mathf.Clamp01(1f - Mathf.Abs(scroll) * 2f));
            }

            private void UpdateSlot(Slot slot, int k, int n)
            {
                float pos = k + scroll;
                float dist = Mathf.Abs(pos);
                float alpha = Mathf.Clamp01(1.6f - dist) * visibility;

                // 빈 목록이면 가운데 빈 슬롯만, 1개면 양 옆 복제 숨김
                if ((n == 0 || n == 1) && k != 0) alpha = 0f;

                bool shown = alpha > 0.001f;
                slot.transform.gameObject.SetActive(shown);
                if (!shown) return;

                float scale = Mathf.Lerp(1.15f, 0.75f, Mathf.Clamp01(dist));
                slot.transform.localPosition = axis * (pos * view.slotSpacing);
                slot.transform.localScale = Vector3.one * scale;

                Color tint = Color.Lerp(view.selectedTint, Color.white, Mathf.Clamp01(dist));
                tint.a = alpha;
                slot.frame.color = tint;

                if (n == 0)
                {
                    slot.icon.enabled = false;
                    slot.count.text = "";
                    return;
                }

                int idx = Mod(center + k, n);
                Sprite sprite = getIcon(idx);
                slot.icon.enabled = sprite != null;
                if (sprite != null)
                {
                    slot.icon.sprite = sprite;
                    Vector2 size = sprite.bounds.size;
                    float fit = view.iconSize / Mathf.Max(0.01f, Mathf.Max(size.x, size.y));
                    slot.icon.transform.localScale = Vector3.one * fit;
                    slot.icon.color = new Color(1f, 1f, 1f, alpha);
                }

                int stack = getStack(idx);
                slot.count.text = stack > 1 ? stack.ToString() : "";
                slot.count.color = new Color(1f, 1f, 1f, alpha);
            }

            private static int Mod(int a, int n) => ((a % n) + n) % n;
        }

        private TextMesh CreateText(Transform parent, string name, Vector2 localPos, TextAnchor anchor, float characterSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = characterSize;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = sortingOrder + 2;
            return tm;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (slotSprite == null)
                slotSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/UI/Slot.png");
        }
#endif
    }
}
