using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Gstarr.Items;
using Gstarr.Player;
using Gstarr.Rhythm;
using Gstarr.UI;

namespace Gstarr.Combat
{
    public enum WeaponType
    {
        Melee,
        Ranged,
        Shotgun
    }

    [Serializable]
    public class Weapon
    {
        public string name;
        public WeaponType type;
        public int damage = 1;
        [Tooltip("공격 간격 (초)")]
        public float fireInterval = 0.3f;

        [Header("외형 (오른쪽을 바라볼 때 기준, 플레이어 로컬 좌표)")]
        public Sprite sprite;
        [Tooltip("손에 든 무기 스프라이트 중심 위치")]
        public Vector2 holdOffset;
        [Tooltip("탄이 나가는 위치")]
        public Vector2 muzzleOffset;

        [Header("근접")]
        public Vector2 meleeSize = new Vector2(1.6f, 1.2f);
        public float meleeReach = 0.9f;

        [Header("원거리 / 산탄")]
        public Sprite projectileSprite;
        public float projectileSpeed = 24f;
        public float range = 14f;
        public int pellets = 1;
        [Tooltip("산탄 전체 퍼짐 각도")]
        public float spreadAngle = 0f;
        public float recoil = 0f;
    }

    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
        public const string ArtFolder = "Assets/arts";

        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("공격이 맞을 레이어 (지형 포함 시 탄이 벽에 막힘)")]
        [SerializeField] private LayerMask hitMask;
        [Tooltip("비워두면 projectileSprite 로 탄을 런타임 생성")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Sprite slashSprite;
        [SerializeField] private int weaponSortingOrder = 6;

        [SerializeField] private Weapon[] weapons =
        {
            new Weapon
            {
                name = "Sword", type = WeaponType.Melee, damage = 2, fireInterval = 0.3f,
                holdOffset = new Vector2(0.7f, -0.1f),
                meleeSize = new Vector2(1.6f, 1.2f), meleeReach = 0.9f
            },
            new Weapon
            {
                name = "Pistol", type = WeaponType.Ranged, damage = 1, fireInterval = 0.18f,
                holdOffset = new Vector2(0.55f, -0.05f), muzzleOffset = new Vector2(0.9f, 0.08f),
                projectileSpeed = 26f, range = 16f, pellets = 1
            },
            new Weapon
            {
                name = "Shotgun", type = WeaponType.Shotgun, damage = 1, fireInterval = 0.7f,
                holdOffset = new Vector2(0.6f, -0.08f), muzzleOffset = new Vector2(1.15f, 0.02f),
                projectileSpeed = 20f, range = 7f, pellets = 6, spreadAngle = 28f, recoil = 4f
            }
        };

        /// <summary>(새 인덱스, 이동 방향 -1/+1)</summary>
        public event Action<int, int> WeaponChanged;

        public IReadOnlyList<Weapon> Weapons => weapons;
        public int CurrentIndex => currentIndex;
        public Weapon CurrentWeapon => weapons[currentIndex];

        private const float SwingDuration = 0.14f;
        private const float SwingStartAngle = 80f;
        private const float SwingEndAngle = -70f;
        private const float RecoilKickDistance = 0.12f;
        private const float RecoilRecoverSpeed = 1.2f;
        private const float LiftDuration = 0.28f;
        private const float LiftHeight = 0.22f;
        private const float LiftAngle = 35f;
        private const float LiftScalePop = 0.15f;
        private const float HolsterTime = 0.13f;
        private const float ItemUseTime = 0.4f;
        private const float DrawTime = 0.16f;

        private PlayerController controller;
        private Rigidbody2D rb;
        private InputAction attackAction, prevAction, nextAction;
        private InputActionMap ownedActionMap;

        private Transform weaponPivot;
        private SpriteRenderer weaponRenderer;
        private SpriteRenderer heldItemRenderer;

        private int currentIndex;
        private float cooldownTimer;
        private float swingTimer;
        private float recoilKick;
        private float liftTimer;
        private float itemAnimTimer;
        private Sprite fallbackSprite;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            rb = GetComponent<Rigidbody2D>();

            if (hitMask.value == 0)
                hitMask = ~LayerMask.GetMask("Player", "Ignore Raycast");

            SetupInput();
            CreateWeaponVisual();

            // 씬에 인벤토리/UI가 없어도 아이템이 동작하도록 보장
            if (GetComponent<Health>() == null) gameObject.AddComponent<Health>();
            if (GetComponent<PlayerInventory>() == null) gameObject.AddComponent<PlayerInventory>();
            if (GetComponent<EquipmentSelectorView>() == null) gameObject.AddComponent<EquipmentSelectorView>();
        }

        private void SetupInput()
        {
            if (inputActions == null && GameAssets.Instance != null)
                inputActions = GameAssets.Instance.inputActions;
            if (inputActions != null)
            {
                var map = inputActions.FindActionMap("Player", throwIfNotFound: true);
                attackAction = map.FindAction("Attack", throwIfNotFound: true);
                prevAction = map.FindAction("PrevWeapon", throwIfNotFound: true);
                nextAction = map.FindAction("NextWeapon", throwIfNotFound: true);
                return;
            }

            ownedActionMap = new InputActionMap("Combat");
            attackAction = ownedActionMap.AddAction("Attack", InputActionType.Button);
            attackAction.AddBinding("<Gamepad>/buttonEast");
            attackAction.AddBinding("<Keyboard>/c");
            prevAction = ownedActionMap.AddAction("PrevWeapon", InputActionType.Button);
            prevAction.AddBinding("<Gamepad>/leftShoulder");
            prevAction.AddBinding("<Keyboard>/q");
            nextAction = ownedActionMap.AddAction("NextWeapon", InputActionType.Button);
            nextAction.AddBinding("<Gamepad>/rightShoulder");
            nextAction.AddBinding("<Keyboard>/e");
        }

        private void CreateWeaponVisual()
        {
            weaponPivot = new GameObject("WeaponPivot").transform;
            weaponPivot.SetParent(transform, false);

            var spriteGo = new GameObject("WeaponSprite");
            spriteGo.transform.SetParent(weaponPivot, false);
            weaponRenderer = spriteGo.AddComponent<SpriteRenderer>();

            var playerRenderer = GetComponent<SpriteRenderer>();
            if (playerRenderer != null)
            {
                weaponRenderer.sortingLayerID = playerRenderer.sortingLayerID;
                weaponRenderer.sortingOrder = Mathf.Max(weaponSortingOrder, playerRenderer.sortingOrder + 1);
            }
            else
            {
                weaponRenderer.sortingOrder = weaponSortingOrder;
            }

            var heldGo = new GameObject("HeldItem");
            heldGo.transform.SetParent(transform, false);
            heldItemRenderer = heldGo.AddComponent<SpriteRenderer>();
            heldItemRenderer.sortingOrder = weaponRenderer.sortingOrder;
            heldItemRenderer.enabled = false;
        }

        private void OnEnable()
        {
            attackAction?.Enable();
            prevAction?.Enable();
            nextAction?.Enable();
            if (TryGetComponent(out PlayerInventory inventory))
                inventory.ItemUsed += OnItemUsed;
        }

        private void OnDisable()
        {
            attackAction?.Disable();
            prevAction?.Disable();
            nextAction?.Disable();
            if (TryGetComponent(out PlayerInventory inventory))
                inventory.ItemUsed -= OnItemUsed;
        }

        private void OnDestroy()
        {
            ownedActionMap?.Dispose();
        }

        private void Start()
        {
            ApplyWeaponVisual();
        }

        private void Update()
        {
            if (Gstarr.Game.GamePause.BlocksGameplay) return;
            float dt = Time.deltaTime;
            cooldownTimer -= dt;

            if (prevAction.WasPressedThisFrame()) SwitchWeapon(-1);
            if (nextAction.WasPressedThisFrame()) SwitchWeapon(1);

            if (attackAction.WasPressedThisFrame())
            {
                var glory = EnemyActor.NearestStaggered(transform.position, 2.4f);
                if (glory != null)
                {
                    GloryKillDirector.Begin(glory);
                    return;
                }
            }

            // 누르고 있어도 박자 창 안에서만 한 번씩 나간다
            if (attackAction.IsPressed()) TryAttack();

            AnimateWeapon(dt);
        }

        public void SwitchWeapon(int delta)
        {
            swingTimer = 0f;
            recoilKick = 0f;
            currentIndex = (currentIndex + delta + weapons.Length) % weapons.Length;
            cooldownTimer = Mathf.Max(cooldownTimer, LiftDuration * 0.5f);
            liftTimer = LiftDuration;
            ApplyWeaponVisual();
            WeaponChanged?.Invoke(currentIndex, delta);
        }

        private void ApplyWeaponVisual()
        {
            var w = CurrentWeapon;
            weaponRenderer.sprite = w.sprite;
            weaponRenderer.enabled = w.sprite != null;
            weaponPivot.localPosition = w.holdOffset;
            weaponPivot.localRotation = Quaternion.identity;
        }

        private void OnItemUsed(ItemData item)
        {
            if (heldItemRenderer != null)
            {
                heldItemRenderer.sprite = item.Icon;
                heldItemRenderer.enabled = false;
            }
            itemAnimTimer = HolsterTime + ItemUseTime + DrawTime;
            swingTimer = 0f;
            liftTimer = 0f;
        }

        private void AnimateWeapon(float dt)
        {
            if (itemAnimTimer > 0f)
            {
                AnimateItemUse(dt);
                return;
            }

            var w = CurrentWeapon;
            float angle = 0f;
            float lift = 0f;

            if (swingTimer > 0f)
            {
                swingTimer -= dt;
                float t = 1f - Mathf.Clamp01(swingTimer / SwingDuration);
                angle = Mathf.Lerp(SwingStartAngle, SwingEndAngle, 1f - (1f - t) * (1f - t));
            }
            else if (liftTimer > 0f)
            {
                // 교체한 무기를 살짝 들어올렸다가 제자리로
                liftTimer -= dt;
                float t = 1f - Mathf.Clamp01(liftTimer / LiftDuration);
                lift = Mathf.Sin(t * Mathf.PI) * (1f - t * 0.3f);
                angle = LiftAngle * lift;
            }

            recoilKick = Mathf.MoveTowards(recoilKick, 0f, RecoilRecoverSpeed * dt);

            // 부모(플레이어)의 localScale.x 반전으로 좌우가 자동으로 뒤집히므로 오른쪽 기준으로만 계산
            weaponPivot.localPosition = w.holdOffset + new Vector2(-recoilKick, LiftHeight * lift);
            weaponPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
            weaponPivot.localScale = Vector3.one * (1f + LiftScalePop * lift);
        }

        /// <summary>무기를 집어넣고 아이템을 들어 사용한 뒤 다시 꺼내는 연출</summary>
        private void AnimateItemUse(float dt)
        {
            float total = HolsterTime + ItemUseTime + DrawTime;
            float elapsed = total - itemAnimTimer;
            itemAnimTimer -= dt;

            var w = CurrentWeapon;
            float weaponAngle = 0f;
            float weaponScale = 1f;
            bool showItem = false;
            float itemLift = 0f;
            float itemScale = 1f;

            if (elapsed < HolsterTime)
            {
                float t = elapsed / HolsterTime;
                weaponAngle = -100f * t;
                weaponScale = 1f - t;
            }
            else if (elapsed < HolsterTime + ItemUseTime)
            {
                float t = (elapsed - HolsterTime) / ItemUseTime;
                weaponScale = 0f;
                showItem = true;
                itemLift = Mathf.Sin(t * Mathf.PI) * 0.7f;
                itemScale = 1f + 0.3f * Mathf.Sin(t * Mathf.PI);
            }
            else
            {
                float t = (elapsed - HolsterTime - ItemUseTime) / DrawTime;
                weaponAngle = -100f * (1f - t);
                weaponScale = t;
            }

            weaponPivot.localPosition = w.holdOffset;
            weaponPivot.localRotation = Quaternion.Euler(0f, 0f, weaponAngle);
            weaponPivot.localScale = Vector3.one * weaponScale;

            if (heldItemRenderer != null)
            {
                heldItemRenderer.enabled = showItem && heldItemRenderer.sprite != null;
                heldItemRenderer.transform.localPosition = w.holdOffset + new Vector2(0.1f, 0.3f + itemLift);
                heldItemRenderer.transform.localScale = Vector3.one * itemScale;
            }

            if (itemAnimTimer <= 0f)
            {
                weaponPivot.localScale = Vector3.one;
                weaponPivot.localRotation = Quaternion.identity;
                if (heldItemRenderer != null) heldItemRenderer.enabled = false;
            }
        }

        private void TryAttack()
        {
            if (cooldownTimer > 0f || itemAnimTimer > 0f) return;
            if (!BeatGate.TryPerform(BeatAction.Attack, attackAction.WasPressedThisFrame())) return;

            var w = CurrentWeapon;
            cooldownTimer = w.fireInterval;
            int facing = controller.FacingDirection;

            if (w.type == WeaponType.Melee)
                DoMelee(w, facing);
            else
                Fire(w, facing);
        }

        private void DoMelee(Weapon w, int facing)
        {
            Vector2 center = (Vector2)transform.position + new Vector2(w.meleeReach * facing, 0f);
            var hits = Physics2D.OverlapBoxAll(center, w.meleeSize, 0f, hitMask);
            foreach (var hit in hits)
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (hit.TryGetComponent(out IDamageable target) && target.TakeDamage(w.damage, new Vector2(facing, 0.3f)))
                    ComboSystem.RegisterHit(hit.gameObject);
            }

            swingTimer = SwingDuration;

            if (slashSprite != null)
            {
                var slash = new GameObject("MeleeSlash");
                slash.transform.position = center;
                slash.transform.localScale = new Vector3(facing, 1f, 1f);
                var sr = slash.AddComponent<SpriteRenderer>();
                sr.sprite = slashSprite;
                sr.sortingOrder = weaponRenderer.sortingOrder + 1;
                Destroy(slash, 0.1f);
            }
        }

        private void Fire(Weapon w, int facing)
        {
            Vector2 origin = (Vector2)transform.position + new Vector2(w.muzzleOffset.x * facing, w.muzzleOffset.y);
            float baseAngle = facing > 0 ? 0f : 180f;

            for (int i = 0; i < w.pellets; i++)
            {
                float t = w.pellets > 1 ? i / (float)(w.pellets - 1) - 0.5f : 0f;
                float jitter = w.pellets > 1 ? UnityEngine.Random.Range(-1.5f, 1.5f) : 0f;
                float angle = baseAngle + t * w.spreadAngle + jitter;
                float speed = w.projectileSpeed * (w.pellets > 1 ? UnityEngine.Random.Range(0.9f, 1.1f) : 1f);
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

                SpawnProjectile(w, origin).Launch(dir * speed, w.damage, w.range, hitMask, gameObject);
            }

            recoilKick = RecoilKickDistance;

            if (w.recoil > 0f)
            {
                Vector2 v = rb.linearVelocity;
                v.x -= facing * w.recoil;
                rb.linearVelocity = v;
            }
        }

        private Projectile SpawnProjectile(Weapon w, Vector2 position)
        {
            if (projectilePrefab != null)
                return Instantiate(projectilePrefab, position, Quaternion.identity);

            var go = new GameObject("Bullet");
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = w.projectileSprite != null ? w.projectileSprite : GetFallbackSprite();
            sr.sortingOrder = weaponRenderer.sortingOrder;
            return go.AddComponent<Projectile>();
        }

        private Sprite GetFallbackSprite()
        {
            if (fallbackSprite != null) return fallbackSprite;
            var tex = new Texture2D(4, 2) { filterMode = FilterMode.Point };
            var px = new Color[8];
            for (int i = 0; i < px.Length; i++) px[i] = Color.yellow;
            tex.SetPixels(px);
            tex.Apply();
            fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 4, 2), new Vector2(0.5f, 0.5f), 16f);
            return fallbackSprite;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            AssignDefaultArt();
        }

        /// <summary>비어있는 스프라이트 칸을 Assets/arts 의 기본 이미지로 채움</summary>
        public void AssignDefaultArt()
        {
            if (weapons == null) return;

            foreach (var w in weapons)
            {
                if (w == null) continue;
                switch (w.type)
                {
                    case WeaponType.Melee:
                        if (w.sprite == null) w.sprite = LoadArt("Weapons/Sword.png");
                        if (w.holdOffset == Vector2.zero) w.holdOffset = new Vector2(0.7f, -0.1f);
                        break;
                    case WeaponType.Ranged:
                        if (w.sprite == null) w.sprite = LoadArt("Weapons/Pistol.png");
                        if (w.projectileSprite == null) w.projectileSprite = LoadArt("Projectiles/Bullet.png");
                        if (w.holdOffset == Vector2.zero) w.holdOffset = new Vector2(0.55f, -0.05f);
                        if (w.muzzleOffset == Vector2.zero) w.muzzleOffset = new Vector2(0.9f, 0.08f);
                        break;
                    case WeaponType.Shotgun:
                        if (w.sprite == null) w.sprite = LoadArt("Weapons/Shotgun.png");
                        if (w.projectileSprite == null) w.projectileSprite = LoadArt("Projectiles/Pellet.png");
                        if (w.holdOffset == Vector2.zero) w.holdOffset = new Vector2(0.6f, -0.08f);
                        if (w.muzzleOffset == Vector2.zero) w.muzzleOffset = new Vector2(1.15f, 0.02f);
                        break;
                }
            }

            if (slashSprite == null) slashSprite = LoadArt("Effects/Slash.png");
        }

        private static Sprite LoadArt(string relativePath)
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{relativePath}");
        }
#endif
    }
}
