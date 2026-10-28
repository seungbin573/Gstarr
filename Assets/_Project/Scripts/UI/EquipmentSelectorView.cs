using System.Collections.Generic;
using Gstarr.Game;
using Gstarr.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Gstarr.UI
{
    /// <summary>
    /// 오른쪽 위 아이템 리본. 고른 칸은 빛나고, 나머지는 작게 떠 있다.
    /// 전투는 십자키 위/아래, 효과는 왼쪽/오른쪽.
    /// </summary>
    public class EquipmentSelectorView : MonoBehaviour
    {
        public const string ArtFolder = "Assets/arts";

        private static readonly Color CombatAccent = new Color(1f, 0.78f, 0.45f);
        private static readonly Color EffectAccent = new Color(0.78f, 0.62f, 1f);

        [SerializeField] private Sprite slotSprite;
        [SerializeField] private Color selectedTint = new Color(1f, 0.92f, 0.55f);

        private const int SortingOrder = 112;
        private const float SlotSize = 62f;
        private const float SlotGap = 10f;

        private PlayerInventory inventory;
        private Row combat;
        private Row effect;
        private Sprite glowSprite;
        private Sprite ribbonSprite;
        private readonly List<Object> generated = new List<Object>();

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            if (slotSprite == null && GameAssets.Instance != null)
                slotSprite = GameAssets.Instance.slotSprite;

            glowSprite = MakeGlow();
            ribbonSprite = MakeRibbon();

            var canvas = UIFactory.CreateCanvas(transform, "ItemHud", SortingOrder);
            var root = (RectTransform)canvas.transform;
            combat = BuildRow(root, ItemCategory.Combat, "전투", "▲\n▼", CombatAccent, new Vector2(-28f, -16f));
            effect = BuildRow(root, ItemCategory.Effect, "효과", "◀   ▶", EffectAccent, new Vector2(-28f, -148f));
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.SelectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.SelectionChanged -= OnSelectionChanged;
        }

        private void OnDestroy()
        {
            for (int i = generated.Count - 1; i >= 0; i--)
            {
                if (generated[i] != null) Destroy(generated[i]);
            }
        }

        private void Start() => RefreshAll();

        private void OnSelectionChanged(ItemCategory category, int index, int delta)
        {
            var row = category == ItemCategory.Combat ? combat : effect;
            if (row != null && delta != 0)
            {
                row.punch = 1f;
                row.direction = delta;
            }
            RefreshAll();
        }

        private void LateUpdate()
        {
            if (inventory == null) return;
            float dt = Time.unscaledDeltaTime;
            Tick(combat, dt);
            Tick(effect, dt);
        }

        private void RefreshAll()
        {
            if (inventory == null) return;
            Sync(combat);
            Sync(effect);
        }

        private Row BuildRow(RectTransform root, ItemCategory category, string title, string arrows, Color accent, Vector2 pos)
        {
            var rowRoot = UIFactory.CreateRect(category + "Row", root);
            UIFactory.Place(rowRoot, new Vector2(1f, 1f), pos, new Vector2(620f, 124f));

            var ribbon = UIFactory.CreateImage("Ribbon", rowRoot, accent);
            ribbon.sprite = ribbonSprite;
            UIFactory.Stretch(ribbon.rectTransform);

            var label = UIFactory.CreateText("Label", rowRoot, title, 24, TextAnchor.MiddleRight, FontStyle.BoldAndItalic);
            UIFactory.Place(label.rectTransform, new Vector2(1f, 1f), new Vector2(-10f, -2f), new Vector2(120f, 32f));
            var hint = UIFactory.CreateText("Hint", rowRoot, arrows, 16, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(hint.rectTransform, new Vector2(1f, 0.5f), new Vector2(-14f, -4f), new Vector2(72f, 52f));
            hint.color = accent;

            var name = UIFactory.CreateText("Name", rowRoot, "", 22, TextAnchor.MiddleRight, FontStyle.BoldAndItalic);
            UIFactory.Place(name.rectTransform, new Vector2(1f, 0f), new Vector2(-96f, 2f), new Vector2(360f, 30f));
            TintOutline(name, new Color(0.12f, 0.04f, 0.22f, 0.95f));

            return new Row
            {
                category = category,
                root = rowRoot,
                ribbon = ribbon,
                label = label,
                hint = hint,
                name = name,
                accent = accent
            };
        }

        private void Tick(Row row, float dt)
        {
            if (row == null) return;
            int count = inventory.GetItems(row.category).Count;
            if (count != row.slots.Count) Sync(row);
            row.punch = Mathf.MoveTowards(row.punch, 0f, dt * 2.2f);
            ApplyVisuals(row, dt);
        }

        private void Sync(Row row)
        {
            var items = inventory.GetItems(row.category);
            for (int i = 0; i < row.slots.Count; i++)
                Destroy(row.slots[i].root.gameObject);
            row.slots.Clear();

            for (int i = 0; i < items.Count; i++)
            {
                float fromRight = 108f + (items.Count - 1 - i) * (SlotSize + SlotGap);
                var pos = new Vector2(-fromRight, 8f);
                var slotRoot = UIFactory.CreateRect("Slot" + i, row.root);
                UIFactory.Place(slotRoot, new Vector2(1f, 0.5f), pos, new Vector2(SlotSize, SlotSize));

                var glow = UIFactory.CreateImage("Glow", slotRoot, row.accent);
                glow.sprite = glowSprite;
                var glowRt = glow.rectTransform;
                glowRt.anchorMin = new Vector2(-0.55f, -0.55f);
                glowRt.anchorMax = new Vector2(1.55f, 1.55f);
                glowRt.offsetMin = glowRt.offsetMax = Vector2.zero;

                var frame = UIFactory.CreateImage("Frame", slotRoot, Color.white);
                if (slotSprite != null) frame.sprite = slotSprite;
                UIFactory.Stretch(frame.rectTransform);

                var icon = UIFactory.CreateImage("Icon", frame.transform, Color.white);
                icon.preserveAspect = true;
                var iconRt = icon.rectTransform;
                iconRt.anchorMin = Vector2.zero;
                iconRt.anchorMax = Vector2.one;
                iconRt.offsetMin = new Vector2(10f, 10f);
                iconRt.offsetMax = new Vector2(-10f, -10f);

                var count = UIFactory.CreateText("Count", frame.transform, "", 18, TextAnchor.LowerRight, FontStyle.Bold);
                UIFactory.Place(count.rectTransform, new Vector2(1f, 0f), new Vector2(-2f, 0f), new Vector2(36f, 22f));

                row.slots.Add(new Slot { root = slotRoot, frame = frame, glow = glow, icon = icon, count = count, rest = pos, scale = 0.82f });
            }

            ApplyVisuals(row, 1f);
        }

        private void ApplyVisuals(Row row, float dt)
        {
            var items = inventory.GetItems(row.category);
            int selected = items.Count == 0 ? 0 : Mathf.Clamp(inventory.GetSelectedIndex(row.category), 0, items.Count - 1);
            bool active = inventory.ActiveCategory == row.category;
            float time = Time.unscaledTime;
            float breathe = 0.5f + 0.5f * Mathf.Sin(time * 2.4f);

            float ribbonAlpha = active ? 0.72f : 0.28f;
            row.ribbon.color = new Color(row.accent.r, row.accent.g, row.accent.b, ribbonAlpha);
            row.label.color = active ? Color.Lerp(row.accent, Color.white, 0.35f) : new Color(1f, 1f, 1f, 0.45f);
            var hint = row.accent;
            hint.a = active ? 0.45f + 0.4f * breathe : 0.22f;
            row.hint.color = hint;
            float hintNudge = active ? Mathf.Sin(time * 3.2f) * 3f : 0f;
            row.hint.rectTransform.anchoredPosition = new Vector2(-14f, -4f + (row.category == ItemCategory.Combat ? hintNudge : 0f));
            if (row.category == ItemCategory.Effect)
                row.hint.rectTransform.anchoredPosition = new Vector2(-14f + hintNudge, -4f);

            if (items.Count == 0)
            {
                row.name.text = "없음";
                row.name.color = new Color(1f, 1f, 1f, 0.35f);
                return;
            }

            var stack = items[selected];
            row.name.text = stack.item != null ? stack.item.DisplayName : "";
            var nameColor = Color.Lerp(row.accent, Color.white, 0.55f + 0.45f * row.punch);
            nameColor.a = active ? 1f : 0.7f;
            row.name.color = nameColor;

            for (int i = 0; i < row.slots.Count && i < items.Count; i++)
            {
                bool on = i == selected;
                var slot = row.slots[i];
                var item = items[i].item;
                slot.icon.sprite = item != null ? item.Icon : null;
                slot.icon.enabled = slot.icon.sprite != null;

                var tint = item != null ? item.IconTint : Color.white;
                tint.a = on ? 1f : 0.55f;
                slot.icon.color = tint;

                slot.frame.color = on ? Color.Lerp(selectedTint, Color.white, 0.25f * breathe) : new Color(0.72f, 0.68f, 0.82f, 0.55f);
                slot.glow.enabled = on;
                if (on)
                {
                    var glow = row.accent;
                    glow.a = (active ? 0.55f : 0.28f) + 0.22f * breathe;
                    slot.glow.color = glow;
                    slot.glow.rectTransform.localScale = Vector3.one * (1f + 0.08f * breathe);
                }

                float target = on ? 1.16f + 0.14f * row.punch : 0.78f;
                float follow = dt >= 1f ? 1f : 1f - Mathf.Exp(-14f * dt);
                slot.scale = Mathf.Lerp(slot.scale, target, follow);
                slot.root.localScale = Vector3.one * slot.scale;

                float lift = on ? 8f + 3f * breathe : Mathf.Sin(time * 1.3f + i * 1.7f) * 2f - 4f;
                slot.root.anchoredPosition = slot.rest + new Vector2(0f, lift);
                float tilt = on ? -row.direction * 10f * row.punch : 0f;
                slot.root.localRotation = Quaternion.Euler(0f, 0f, tilt);

                int n = items[i].count;
                slot.count.text = n > 1 ? n.ToString() : "";
                slot.count.color = on ? Color.white : new Color(1f, 1f, 1f, 0.7f);
            }
        }

        private static void TintOutline(Text text, Color color)
        {
            var outline = text.GetComponent<Outline>();
            if (outline != null) outline.effectColor = color;
        }

        private Sprite MakeGlow()
        {
            const int size = 64;
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                byte a = (byte)(d * d * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return NewSprite(tex);
        }

        private Sprite MakeRibbon()
        {
            const int width = 128;
            const int height = 32;
            var tex = NewTexture(width, height);
            var px = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float along = Mathf.SmoothStep(0f, 1f, x / (float)(width - 1));
                float across = 1f - Mathf.Abs(y / (float)(height - 1) * 2f - 1f);
                across = Mathf.SmoothStep(0f, 1f, across);
                byte a = (byte)(along * across * 255f);
                px[y * width + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return NewSprite(tex);
        }

        private Texture2D NewTexture(int width, int height = 0)
        {
            if (height <= 0) height = width;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            generated.Add(tex);
            return tex;
        }

        private Sprite NewSprite(Texture2D tex)
        {
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            generated.Add(sprite);
            return sprite;
        }

        private class Row
        {
            public ItemCategory category;
            public RectTransform root;
            public Image ribbon;
            public Text label;
            public Text hint;
            public Text name;
            public Color accent;
            public readonly List<Slot> slots = new List<Slot>();
            public float punch;
            public int direction = 1;
        }

        private class Slot
        {
            public RectTransform root;
            public Image frame;
            public Image glow;
            public Image icon;
            public Text count;
            public Vector2 rest;
            public float scale;
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
