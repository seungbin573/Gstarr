using System;
using System.Collections.Generic;
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
        [Tooltip("시작할 때 가지고 있는 아이템 (테스트용)")]
        [SerializeField] private List<ItemStack> items = new List<ItemStack>();
        [SerializeField] private float useCooldown = 0.25f;

        /// <summary>(새 인덱스, 이동 방향 -1/+1, 0 이면 목록 변경)</summary>
        public event Action<int, int> SelectionChanged;
        public event Action<ItemData> ItemUsed;

        public IReadOnlyList<ItemStack> Items => items;
        public int SelectedIndex => selectedIndex;
        public ItemStack Selected => items.Count > 0 ? items[selectedIndex] : null;

        private InputAction useAction, prevAction, nextAction;
        private InputActionMap ownedActionMap;
        private int selectedIndex;
        private float cooldownTimer;

        private void Awake()
        {
            items.RemoveAll(s => s == null || s.item == null || s.count <= 0);

            if (inputActions != null)
            {
                var map = inputActions.FindActionMap("Player", throwIfNotFound: true);
                useAction = map.FindAction("UseItem", throwIfNotFound: true);
                prevAction = map.FindAction("PrevItem", throwIfNotFound: true);
                nextAction = map.FindAction("NextItem", throwIfNotFound: true);
                return;
            }

            ownedActionMap = new InputActionMap("Inventory");
            useAction = ownedActionMap.AddAction("UseItem", InputActionType.Button);
            useAction.AddBinding("<Gamepad>/buttonNorth");
            useAction.AddBinding("<Keyboard>/v");
            prevAction = ownedActionMap.AddAction("PrevItem", InputActionType.Button);
            prevAction.AddBinding("<Gamepad>/dpad/up");
            prevAction.AddBinding("<Keyboard>/r");
            nextAction = ownedActionMap.AddAction("NextItem", InputActionType.Button);
            nextAction.AddBinding("<Gamepad>/dpad/down");
            nextAction.AddBinding("<Keyboard>/f");
        }

        private void OnEnable()
        {
            useAction?.Enable();
            prevAction?.Enable();
            nextAction?.Enable();
        }

        private void OnDisable()
        {
            useAction?.Disable();
            prevAction?.Disable();
            nextAction?.Disable();
        }

        private void OnDestroy()
        {
            ownedActionMap?.Dispose();
        }

        private void Update()
        {
            cooldownTimer -= Time.deltaTime;

            if (prevAction.WasPressedThisFrame()) Select(-1);
            if (nextAction.WasPressedThisFrame()) Select(1);
            if (useAction.WasPressedThisFrame()) UseSelected();
        }

        public void Select(int delta)
        {
            if (items.Count == 0)
            {
                // 빈 슬롯이라도 선택 UI는 보여줌
                SelectionChanged?.Invoke(0, delta);
                return;
            }
            selectedIndex = (selectedIndex + delta + items.Count) % items.Count;
            SelectionChanged?.Invoke(selectedIndex, delta);
        }

        public bool UseSelected()
        {
            var stack = Selected;
            if (stack == null || cooldownTimer > 0f) return false;
            if (!stack.item.Use(gameObject)) return false;

            cooldownTimer = useCooldown;
            ItemUsed?.Invoke(stack.item);

            if (stack.item.ConsumeOnUse && --stack.count <= 0)
            {
                items.RemoveAt(selectedIndex);
                if (selectedIndex >= items.Count) selectedIndex = Mathf.Max(0, items.Count - 1);
                SelectionChanged?.Invoke(selectedIndex, 0);
            }
            return true;
        }

        /// <summary>아이템 추가. 다 못 넣은 개수를 반환.</summary>
        public int Add(ItemData item, int count = 1)
        {
            if (item == null || count <= 0) return count;

            foreach (var s in items)
            {
                if (s.item != item || s.count >= item.MaxStack) continue;
                int add = Mathf.Min(count, item.MaxStack - s.count);
                s.count += add;
                count -= add;
                if (count == 0) break;
            }

            while (count > 0)
            {
                int add = Mathf.Min(count, item.MaxStack);
                items.Add(new ItemStack { item = item, count = add });
                count -= add;
            }

            SelectionChanged?.Invoke(selectedIndex, 0);
            return count;
        }
    }
}
