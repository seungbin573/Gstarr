using System;
using System.Collections.Generic;
using Gstarr.Rhythm;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gstarr.Items
{
    [Serializable]
    public class ItemStack
    {
        public ItemData item;
        public int count = 1;
    }

    public class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("시작할 때 가지고 있는 아이템 (테스트용). 카테고리에 따라 자동 분류")]
        [SerializeField] private List<ItemStack> startingItems = new List<ItemStack>();
        [SerializeField] private float useCooldown = 0.25f;

        /// <summary>(카테고리, 새 인덱스, 이동 방향 -1/+1, 0 이면 목록 변경/사용)</summary>
        public event Action<ItemCategory, int, int> SelectionChanged;
        public event Action<ItemData> ItemUsed;

        /// <summary>X 버튼으로 사용할 카테고리 (마지막으로 전환한 쪽)</summary>
        public ItemCategory ActiveCategory { get; private set; } = ItemCategory.Combat;

        private readonly List<ItemStack> combatItems = new List<ItemStack>();
        private readonly List<ItemStack> effectItems = new List<ItemStack>();
        private int combatIndex;
        private int effectIndex;

        private InputAction useAction, prevCombat, nextCombat, prevEffect, nextEffect;
        private InputActionMap ownedActionMap;
        private float cooldownTimer;

        public IReadOnlyList<ItemStack> GetItems(ItemCategory c) => c == ItemCategory.Combat ? combatItems : effectItems;
        public int GetSelectedIndex(ItemCategory c) => c == ItemCategory.Combat ? combatIndex : effectIndex;

        public ItemStack GetSelected(ItemCategory c)
        {
            var list = GetItems(c);
            return list.Count > 0 ? list[GetSelectedIndex(c)] : null;
        }

        private void Awake()
        {
            foreach (var s in startingItems)
                if (s != null && s.item != null && s.count > 0)
                    AddSilently(s.item, s.count);

            // 지급된 아이템이 없으면 기본 아이템 (폭탄, 포션)
            if (combatItems.Count == 0 && effectItems.Count == 0 && GameAssets.Instance != null)
                foreach (var item in GameAssets.Instance.defaultItems)
                    if (item != null) AddSilently(item, GameAssets.Instance.defaultItemCount);

            SetupInput();
        }

        private void SetupInput()
        {
            if (inputActions == null && GameAssets.Instance != null)
                inputActions = GameAssets.Instance.inputActions;
            if (inputActions != null)
            {
                var map = inputActions.FindActionMap("Player", throwIfNotFound: true);
                useAction = map.FindAction("UseItem", throwIfNotFound: true);
                prevCombat = map.FindAction("PrevCombatItem", throwIfNotFound: true);
                nextCombat = map.FindAction("NextCombatItem", throwIfNotFound: true);
                prevEffect = map.FindAction("PrevEffectItem", throwIfNotFound: true);
                nextEffect = map.FindAction("NextEffectItem", throwIfNotFound: true);
                return;
            }

            ownedActionMap = new InputActionMap("Inventory");
            useAction = Add("UseItem", "<Gamepad>/buttonNorth", "<Keyboard>/v");
            prevCombat = Add("PrevCombatItem", "<Gamepad>/dpad/up", "<Keyboard>/r");
            nextCombat = Add("NextCombatItem", "<Gamepad>/dpad/down", "<Keyboard>/f");
            prevEffect = Add("PrevEffectItem", "<Gamepad>/dpad/left", "<Keyboard>/t");
            nextEffect = Add("NextEffectItem", "<Gamepad>/dpad/right", "<Keyboard>/g");

            InputAction Add(string name, string pad, string key)
            {
                var a = ownedActionMap.AddAction(name, InputActionType.Button);
                a.AddBinding(pad);
                a.AddBinding(key);
                return a;
            }
        }

        private void OnEnable()
        {
            useAction?.Enable();
            prevCombat?.Enable();
            nextCombat?.Enable();
            prevEffect?.Enable();
            nextEffect?.Enable();
        }

        private void OnDisable()
        {
            useAction?.Disable();
            prevCombat?.Disable();
            nextCombat?.Disable();
            prevEffect?.Disable();
            nextEffect?.Disable();
        }

        private void OnDestroy()
        {
            ownedActionMap?.Dispose();
        }

        private void Update()
        {
            if (Gstarr.Game.GamePause.BlocksGameplay) return;
            cooldownTimer -= Time.deltaTime;

            if (prevCombat.WasPressedThisFrame()) Select(ItemCategory.Combat, -1);
            if (nextCombat.WasPressedThisFrame()) Select(ItemCategory.Combat, 1);
            if (prevEffect.WasPressedThisFrame()) Select(ItemCategory.Effect, -1);
            if (nextEffect.WasPressedThisFrame()) Select(ItemCategory.Effect, 1);
            // 쓸 아이템이 있을 때만 박자 판정 (빈 손으로 누른 건 미스 처리하지 않음)
            if (useAction.WasPressedThisFrame() && GetSelected(ActiveCategory) != null && cooldownTimer <= 0f
                && BeatGate.TryAct(BeatAction.UseItem))
                UseSelected();
        }

        public void Select(ItemCategory category, int delta)
        {
            ActiveCategory = category;
            var list = GetItems(category);
            int index = GetSelectedIndex(category);
            if (list.Count > 0)
                index = (index + delta + list.Count) % list.Count;
            SetIndex(category, index);
            SelectionChanged?.Invoke(category, index, delta);
        }

        public bool UseSelected()
        {
            var category = ActiveCategory;
            var stack = GetSelected(category);
            if (stack == null || cooldownTimer > 0f) return false;
            if (!stack.item.Use(gameObject)) return false;

            cooldownTimer = useCooldown;
            ItemUsed?.Invoke(stack.item);

            if (stack.item.ConsumeOnUse && --stack.count <= 0)
            {
                var list = (List<ItemStack>)GetItems(category);
                int index = GetSelectedIndex(category);
                list.RemoveAt(index);
                SetIndex(category, Mathf.Clamp(index, 0, Mathf.Max(0, list.Count - 1)));
            }
            SelectionChanged?.Invoke(category, GetSelectedIndex(category), 0);
            return true;
        }

        /// <summary>아이템 추가. 다 못 넣은 개수를 반환.</summary>
        public int Add(ItemData item, int count = 1)
        {
            int left = AddSilently(item, count);
            if (item != null)
                SelectionChanged?.Invoke(item.Category, GetSelectedIndex(item.Category), 0);
            return left;
        }

        private int AddSilently(ItemData item, int count)
        {
            if (item == null || count <= 0) return count;
            var list = (List<ItemStack>)GetItems(item.Category);

            foreach (var s in list)
            {
                if (s.item != item || s.count >= item.MaxStack) continue;
                int add = Mathf.Min(count, item.MaxStack - s.count);
                s.count += add;
                count -= add;
                if (count == 0) return 0;
            }

            while (count > 0)
            {
                int add = Mathf.Min(count, item.MaxStack);
                list.Add(new ItemStack { item = item, count = add });
                count -= add;
            }
            return 0;
        }

        private void SetIndex(ItemCategory c, int index)
        {
            if (c == ItemCategory.Combat) combatIndex = index;
            else effectIndex = index;
        }
    }
}
