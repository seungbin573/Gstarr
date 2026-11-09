using Gstarr.Game;
using Gstarr.Items;
using Gstarr.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Gstarr.UI
{
    /// <summary>
    /// 지금 쓸 아이템 하나만 머리 위 slot 칸에 크게 붙인다.
    /// 공격 아이템은 위아래로, 포션은 좌우로 넘어간다.
    /// </summary>
    [DefaultExecutionOrder(300)]
    public class EquipmentSelectorView : MonoBehaviour
    {
        public const string ArtFolder = "Assets/arts";

        private const int SortingOrder = 115;
        private const float SlotSize = 116f;
        private const float IconSize = 76f;
        private const float HeadClearance = 1.7f;
        private const float SwapDistance = 90f;

        [SerializeField] private Sprite slotSprite;
        [SerializeField] private Color selectedTint = new Color(1f, 0.92f, 0.55f);

        private PlayerInventory inventory;
        private PlayerController body;
        private Canvas canvas;
        private RectTransform cross;
        private CanvasGroup group;
        private Held held;
        private Camera cam;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            body = GetComponent<PlayerController>();
            if (slotSprite == null && GameAssets.Instance != null)
                slotSprite = GameAssets.Instance.slotSprite;

            canvas = UIFactory.CreateCanvas(transform, "ItemHud", SortingOrder);
            cross = UIFactory.CreateRect("Held", canvas.transform);
            cross.anchorMin = cross.anchorMax = new Vector2(0.5f, 0.5f);
            cross.pivot = new Vector2(0.5f, 0.5f);
            cross.sizeDelta = new Vector2(SlotSize, SlotSize);
            group = cross.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            held = MakeSlot();
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.SelectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.SelectionChanged -= OnSelectionChanged;
        }

        private void Start() => Refresh();

        private void OnSelectionChanged(ItemCategory category, int index, int delta)
        {
            if (delta != 0)
                held.BeginSwap(PressDirection(category, delta));
            else
                held.punch = 1f;
            Refresh();
        }

        private void LateUpdate()
        {
            if (inventory == null || cross == null) return;
            held.Tick(Time.unscaledDeltaTime);
            FollowPlayer();
            Refresh();
        }

        private void FollowPlayer()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            float top = 1.7f;
            if (TryGetComponent<Collider2D>(out var solid))
                top = solid.bounds.max.y - transform.position.y;
            var world = transform.position + Vector3.up * (top + HeadClearance);
            var screen = cam.WorldToScreenPoint(world);
            bool behind = screen.z < 0f;
            var canvasRt = (RectTransform)canvas.transform;
            if (!behind && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out var local))
                cross.anchoredPosition = Fit(canvasRt, local);

            float facing = canvas.transform.lossyScale.x < 0f ? -1f : 1f;
            cross.localScale = new Vector3(facing, 1f, 1f);

            var stack = inventory.GetSelected(inventory.ActiveCategory);
            bool scripted = body != null && body.IsScripted;
            float target = !behind && stack != null && !scripted ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 6f);
        }

        private static Vector2 Fit(RectTransform canvasRt, Vector2 local)
        {
            var half = canvasRt.rect.size * 0.5f;
            const float marginX = 90f;
            const float marginY = 150f;
            float maxY = half.y - marginY;
            var fitted = local;
            if (fitted.y > maxY)
                fitted.x += local.x >= 0f ? -110f : 110f;
            fitted.x = Mathf.Clamp(fitted.x, -half.x + marginX, half.x - marginX);
            fitted.y = Mathf.Clamp(fitted.y, -half.y + marginY, maxY);
            return fitted;
        }

        private void Refresh()
        {
            if (inventory == null) return;
            var category = inventory.ActiveCategory;
            var stack = inventory.GetSelected(category);
            bool visible = stack != null;
            held.root.gameObject.SetActive(visible);
            if (!visible) return;

            var item = stack.item;
            if (held.ready && item != held.shown && held.swap <= 0.001f)
                held.BeginSwap(category == ItemCategory.Combat ? Vector2.down : Vector2.right);

            held.shown = item;
            held.ready = true;
            held.icon.sprite = item != null ? item.Icon : null;
            held.icon.enabled = held.icon.sprite != null;
            float iconScale = item != null ? item.IconScale : 1f;
            held.icon.rectTransform.localScale = new Vector3(iconScale, iconScale, 1f);

            var tint = item != null ? item.IconTint : Color.white;
            float reveal = held.swap > 0f ? 1f - held.swap : 1f;
            tint.a *= reveal;
            held.icon.color = tint;

            float ease = held.swap > 0f ? Smooth(1f - held.swap) : 1f;
            Vector2 travel = held.pressDir * SwapDistance;
            held.icon.rectTransform.anchoredPosition = Vector2.Lerp(travel, Vector2.zero, ease);
            if (held.ghost.enabled)
            {
                var ghost = held.ghost.color;
                ghost.a = 1f - ease;
                held.ghost.color = ghost;
                held.ghost.rectTransform.anchoredPosition = Vector2.Lerp(Vector2.zero, -travel, ease);
                if (held.swap <= 0.001f) held.ghost.enabled = false;
            }

            float breathe = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f);
            held.frame.color = Color.Lerp(Color.white, selectedTint, 0.12f + 0.16f * breathe);
            float target = 1f + 0.025f * breathe + 0.1f * held.punch;
            held.scale = Mathf.Lerp(held.scale, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            held.root.localScale = Vector3.one * held.scale;
            held.root.anchoredPosition = held.pressDir * (12f * held.punch);

            int count = stack.count;
            held.count.text = count > 1 ? count.ToString() : "";
            held.name.text = item != null ? item.DisplayName : "";
        }

        private static Vector2 PressDirection(ItemCategory category, int delta)
        {
            Vector2 axis = category == ItemCategory.Combat ? Vector2.down : Vector2.right;
            return axis * (delta < 0 ? -1f : 1f);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private Held MakeSlot()
        {
            var root = UIFactory.CreateRect("Selected", cross);
            UIFactory.Place(root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SlotSize, SlotSize));

            var frame = UIFactory.CreateImage("Frame", root, Color.white);
            if (slotSprite != null) frame.sprite = slotSprite;
            UIFactory.Stretch(frame.rectTransform);

            var window = UIFactory.CreateRect("Window", root);
            window.anchorMin = new Vector2(0.16f, 0.16f);
            window.anchorMax = new Vector2(0.84f, 0.84f);
            window.offsetMin = window.offsetMax = Vector2.zero;
            window.gameObject.AddComponent<RectMask2D>();

            var icon = Icon("Icon", window);
            var ghost = Icon("Ghost", window);
            ghost.enabled = false;

            var count = UIFactory.CreateText("Count", root, "", 22, TextAnchor.LowerRight, FontStyle.Bold);
            UIFactory.Place(count.rectTransform, new Vector2(1f, 0f), new Vector2(-6f, 10f), new Vector2(40f, 24f));

            var name = UIFactory.CreateText("Name", cross, "", 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -88f), new Vector2(260f, 40f));

            return new Held { root = root, frame = frame, icon = icon, ghost = ghost, count = count, name = name, scale = 1f };
        }

        private static Image Icon(string name, Transform parent)
        {
            var icon = UIFactory.CreateImage(name, parent, Color.white);
            icon.preserveAspect = true;
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(IconSize, IconSize));
            return icon;
        }

        private class Held
        {
            public RectTransform root;
            public Image frame;
            public Image icon;
            public Image ghost;
            public Text count;
            public Text name;
            public ItemData shown;
            public bool ready;
            public float punch;
            public float swap;
            public float scale = 1f;
            public Vector2 pressDir = Vector2.down;

            public void BeginSwap(Vector2 direction)
            {
                if (direction.sqrMagnitude > 0.01f) pressDir = direction.normalized;
                ghost.sprite = icon.sprite;
                ghost.rectTransform.localScale = icon.rectTransform.localScale;
                ghost.rectTransform.anchoredPosition = icon.rectTransform.anchoredPosition;
                var color = icon.color;
                color.a = 1f;
                ghost.color = color;
                ghost.enabled = ghost.sprite != null;
                swap = 1f;
                punch = 1f;
            }

            public void Tick(float dt)
            {
                punch = Mathf.MoveTowards(punch, 0f, dt * 3.4f);
                swap = Mathf.MoveTowards(swap, 0f, dt / 0.24f);
            }
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
