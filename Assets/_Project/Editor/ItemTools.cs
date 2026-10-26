using System.IO;
using System.Linq;
using Gstarr.Combat;
using Gstarr.Items;
using Gstarr.Player;
using Gstarr.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace Gstarr.EditorTools
{
    public static class ItemTools
    {
        public const string ItemFolder = "Assets/_Project/Items";
        public const string ItemTileFolder = "Assets/_Project/Tilemaps/ItemTiles";
        public const string ItemPaletteName = "Items";
        public const string InputActionsPath = "Assets/_Project/Input/PlayerControls.inputactions";
        public const string SlotSpritePath = "Assets/arts/UI/Slot.png";

        private const string PotionPath = ItemFolder + "/Potion.asset";
        private const string BombPath = ItemFolder + "/Bomb.asset";
        private const string DamagePotionPath = ItemFolder + "/DamagePotion.asset";
        private const string InvulnerabilityPotionPath = ItemFolder + "/InvulnerabilityPotion.asset";
        private const string FlashbangPath = ItemFolder + "/Flashbang.asset";
        private const string GameAssetsPath = "Assets/Resources/Gstarr/GameAssets.asset";

        [InitializeOnLoadMethod]
        private static void EnsureItemGridOnEditorLoad()
        {
            EditorApplication.delayCall += () =>
            {
                EnsureDefaultItems();
                if (PaletteNeedsRebuild()) BuildItemTiles();
            };
        }

        [MenuItem("Tools/Gstarr/Build Item Palette", priority = 1)]
        public static void BuildItemPaletteMenu()
        {
            EnsureDefaultItems();
            var tiles = BuildItemTiles();
            Debug.Log($"[Item Tools] {tiles.Length}개 아이템 타일 → 팔레트 '{ItemPaletteName}'. " +
                      "Tile Palette 에서 'Items' 팔레트를 고르고 Active Tilemap 을 'Items' 로 두고 그리세요.");
            EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");
        }

        [MenuItem("Tools/Gstarr/Setup Player In Scene", priority = 21)]
        public static void SetupPlayersInSceneMenu()
        {
            var players = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (players.Length == 0)
            {
                Debug.LogWarning("[Item Tools] 씬에 PlayerController 가 없습니다.");
                return;
            }

            foreach (var p in players) SetupPlayer(p.gameObject);

            var grid = Object.FindAnyObjectByType<Grid>();
            if (grid != null) EnsureItemTilemap(grid.transform);

            EditorSceneManager.MarkSceneDirty(players[0].gameObject.scene);
            Debug.Log($"[Item Tools] 플레이어 {players.Length}명 설정 완료 (Health / PlayerInventory / EquipmentSelectorView / Items 타일맵).");
        }

        /// <summary>플레이어에 없는 컴포넌트를 추가하고 참조를 채움. 이미 있는 값은 건드리지 않음.</summary>
        public static void SetupPlayer(GameObject go)
        {
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

            if (go.TryGetComponent(out PlayerCombat combat))
            {
                SetIfEmpty(combat, "inputActions", input);
                combat.AssignDefaultArt();
                EditorUtility.SetDirty(combat);
            }

            if (!go.TryGetComponent(out Health health))
            {
                health = Undo.AddComponent<Health>(go);
                var so = new SerializedObject(health);
                so.FindProperty("destroyOnDeath").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (!go.TryGetComponent(out PlayerInventory inventory))
            {
                EnsureDefaultItems();
                inventory = Undo.AddComponent<PlayerInventory>(go);
                var so = new SerializedObject(inventory);
                var items = so.FindProperty("startingItems");
                var assets = AssetDatabase.LoadAssetAtPath<GameAssets>(GameAssetsPath);
                items.arraySize = assets != null && assets.defaultItems != null ? assets.defaultItems.Length : 0;
                for (int i = 0; i < items.arraySize; i++)
                {
                    items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = assets.defaultItems[i];
                    items.GetArrayElementAtIndex(i).FindPropertyRelative("count").intValue = 3;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            SetIfEmpty(inventory, "inputActions", input);

            if (!go.TryGetComponent(out EquipmentSelectorView view))
                view = Undo.AddComponent<EquipmentSelectorView>(go);
            SetIfEmpty(view, "slotSprite", AssetDatabase.LoadAssetAtPath<Sprite>(SlotSpritePath));
        }

        public static Tilemap EnsureItemTilemap(Transform grid)
        {
            var existing = grid.GetComponentsInChildren<ItemTileSpawner>(true).FirstOrDefault();
            if (existing != null) return existing.GetComponent<Tilemap>();

            var go = new GameObject("Items", typeof(Tilemap), typeof(TilemapRenderer), typeof(ItemTileSpawner));
            Undo.RegisterCreatedObjectUndo(go, "Create Items Tilemap");
            go.transform.SetParent(grid, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = 4;
            return go.GetComponent<Tilemap>();
        }

        public static void EnsureDefaultItems()
        {
            TilesetImporter.EnsureFolder(ItemFolder);

            CreateItemIfMissing<HealItem>(PotionPath, "Potion", "Assets/arts/Items/Potion.png", ItemCategory.Effect, null);
            CreateItemIfMissing<AreaDamageItem>(BombPath, "Bomb", "Assets/arts/Items/Bomb.png", ItemCategory.Combat,
                so => so.FindProperty("effectSprite").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/arts/Effects/Explosion.png"));
            CreateItemIfMissing<FlashbangItem>(FlashbangPath, "Flashbang", "Assets/arts/Items/Bomb.png", ItemCategory.Combat,
                so => so.FindProperty("iconTint").colorValue = new Color(0.8f, 0.95f, 1f));
            CreateItemIfMissing<DamageBoostItem>(DamagePotionPath, "Strength Potion", "Assets/arts/Items/Potion.png", ItemCategory.Effect,
                so => so.FindProperty("iconTint").colorValue = new Color(1f, 0.55f, 0.45f));
            CreateItemIfMissing<InvulnerabilityPotionItem>(InvulnerabilityPotionPath, "Invulnerability Potion", "Assets/arts/Items/Potion.png", ItemCategory.Effect,
                so => so.FindProperty("iconTint").colorValue = new Color(0.5f, 0.85f, 1f));
            AssignDedicatedIcon(DamagePotionPath, "Assets/arts/Items/DamagePotion.png");
            AssignDedicatedIcon(InvulnerabilityPotionPath, "Assets/arts/Items/InvulnerabilityPotion.png");
            AssignDedicatedIcon(FlashbangPath, "Assets/arts/Items/Flashbang.png");

            var gameAssets = AssetDatabase.LoadAssetAtPath<GameAssets>(GameAssetsPath);
            if (gameAssets != null)
            {
                var defaults = new ItemData[]
                {
                    AssetDatabase.LoadAssetAtPath<ItemData>(BombPath),
                    AssetDatabase.LoadAssetAtPath<ItemData>(FlashbangPath),
                    AssetDatabase.LoadAssetAtPath<ItemData>(PotionPath),
                    AssetDatabase.LoadAssetAtPath<ItemData>(DamagePotionPath),
                    AssetDatabase.LoadAssetAtPath<ItemData>(InvulnerabilityPotionPath)
                };
                if (gameAssets.defaultItems == null || !gameAssets.defaultItems.SequenceEqual(defaults))
                {
                    gameAssets.defaultItems = defaults;
                    EditorUtility.SetDirty(gameAssets);
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static bool PaletteNeedsRebuild()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{TilesetImporter.PaletteFolder}/{ItemPaletteName}.prefab") == null)
                return true;
            return AssetDatabase.FindAssets("t:ItemData")
                .Select(g => AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(i => i != null)
                .Any(i => AssetDatabase.LoadAssetAtPath<ItemTile>($"{ItemTileFolder}/{i.name}Tile.asset") == null);
        }

        private static void CreateItemIfMissing<T>(string path, string displayName, string iconPath, ItemCategory category,
            System.Action<SerializedObject> extra) where T : ItemData
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;

            var item = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(item, path);
            var so = new SerializedObject(item);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            so.FindProperty("category").enumValueIndex = (int)category;
            extra?.Invoke(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignDedicatedIcon(string itemPath, string iconPath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(itemPath);
            if (sprite == null || item == null) return;
            var so = new SerializedObject(item);
            var icon = so.FindProperty("icon");
            if (icon.objectReferenceValue == sprite) return;
            icon.objectReferenceValue = sprite;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        /// <summary>모든 ItemData 에 대해 ItemTile 을 만들고 Items 팔레트를 갱신. (전투 = 첫 줄, 효과 = 둘째 줄)</summary>
        public static ItemTile[] BuildItemTiles()
        {
            TilesetImporter.EnsureFolder(ItemTileFolder);

            var items = AssetDatabase.FindAssets("t:ItemData")
                .Select(g => AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(i => i != null)
                .OrderBy(i => i.Category).ThenBy(i => i.name)
                .ToArray();

            var combat = items.Where(i => i.Category == ItemCategory.Combat).ToArray();
            var effect = items.Where(i => i.Category == ItemCategory.Effect).ToArray();
            int cols = Mathf.Max(1, Mathf.Max(combat.Length, effect.Length));

            var grid = new TileBase[cols * 2];
            var all = new System.Collections.Generic.List<ItemTile>();
            for (int i = 0; i < combat.Length; i++) all.Add((ItemTile)(grid[i] = GetOrCreateTile(combat[i])));
            for (int i = 0; i < effect.Length; i++) all.Add((ItemTile)(grid[cols + i] = GetOrCreateTile(effect[i])));

            AssetDatabase.SaveAssets();
            TilesetImporter.BuildPalette(ItemPaletteName, grid, cols);
            return all.ToArray();
        }

        public static ItemTile GetOrCreateTile(ItemData item)
        {
            string path = $"{ItemTileFolder}/{item.name}Tile.asset";
            var tile = AssetDatabase.LoadAssetAtPath<ItemTile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<ItemTile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.Setup(item);
            EditorUtility.SetDirty(tile);
            return tile;
        }

        private static void SetIfEmpty(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(property);
            if (prop == null || prop.objectReferenceValue != null) return;
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
