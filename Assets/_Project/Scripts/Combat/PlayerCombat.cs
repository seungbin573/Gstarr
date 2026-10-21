using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Gstarr.Player;

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

        [Header("탄약 (근접은 무시)")]
        public int magazineSize = 12;
        public float reloadTime = 1.2f;

        [Header("근접")]
        public Vector2 meleeSize = new Vector2(1.6f, 1.2f);
        public float meleeReach = 0.9f;

        [Header("원거리 / 산탄")]
        public float projectileSpeed = 24f;
        public float range = 14f;
        public int pellets = 1;
        [Tooltip("산탄 전체 퍼짐 각도")]
        public float spreadAngle = 0f;
        public float recoil = 0f;

        [NonSerialized] public int ammo;

        public bool UsesAmmo => type != WeaponType.Melee;
    }

    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("공격이 맞을 레이어 (지형 포함 시 탄이 벽에 막힘)")]
        [SerializeField] private LayerMask hitMask;
        [SerializeField] private Vector2 muzzleOffset = new Vector2(0.5f, 0.1f);
        [Tooltip("비워두면 런타임에 단순한 탄 스프라이트를 생성")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private bool showDebugHud = true;

        [SerializeField] private Weapon[] weapons =
        {
            new Weapon
            {
                name = "Sword", type = WeaponType.Melee, damage = 2, fireInterval = 0.3f,
                meleeSize = new Vector2(1.6f, 1.2f), meleeReach = 0.9f
            },
            new Weapon
            {
                name = "Pistol", type = WeaponType.Ranged, damage = 1, fireInterval = 0.18f,
                magazineSize = 12, reloadTime = 1.1f, projectileSpeed = 26f, range = 16f, pellets = 1
            },
            new Weapon
            {
                name = "Shotgun", type = WeaponType.Shotgun, damage = 1, fireInterval = 0.7f,
                magazineSize = 4, reloadTime = 1.6f, projectileSpeed = 20f, range = 7f,
                pellets = 6, spreadAngle = 28f, recoil = 4f
            }
        };

        public event Action<Weapon> WeaponChanged;
        public event Action<Weapon> AmmoChanged;

        public Weapon CurrentWeapon => weapons[currentIndex];
        public bool IsReloading => reloadTimer > 0f;
        public float ReloadProgress => IsReloading ? 1f - reloadTimer / CurrentWeapon.reloadTime : 0f;

        private PlayerController controller;
        private Rigidbody2D rb;
        private InputAction attackAction, reloadAction, prevAction, nextAction;
        private InputActionMap ownedActionMap;

        private int currentIndex;
        private float cooldownTimer;
        private float reloadTimer;
        private float meleeFlashTimer;
        private Sprite runtimeBulletSprite;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            rb = GetComponent<Rigidbody2D>();

            if (hitMask.value == 0)
                hitMask = ~LayerMask.GetMask("Player", "Ignore Raycast");

            foreach (var w in weapons) w.ammo = w.magazineSize;

            SetupInput();
        }

        private void SetupInput()
        {
            if (inputActions != null)
            {
                var map = inputActions.FindActionMap("Player", throwIfNotFound: true);
                attackAction = map.FindAction("Attack", throwIfNotFound: true);
                reloadAction = map.FindAction("Reload", throwIfNotFound: true);
                prevAction = map.FindAction("PrevWeapon", throwIfNotFound: true);
                nextAction = map.FindAction("NextWeapon", throwIfNotFound: true);
                return;
            }

            ownedActionMap = new InputActionMap("Combat");
            attackAction = ownedActionMap.AddAction("Attack", InputActionType.Button);
            attackAction.AddBinding("<Gamepad>/buttonEast");
            attackAction.AddBinding("<Keyboard>/c");
            reloadAction = ownedActionMap.AddAction("Reload", InputActionType.Button);
            reloadAction.AddBinding("<Gamepad>/buttonNorth");
            reloadAction.AddBinding("<Keyboard>/r");
            prevAction = ownedActionMap.AddAction("PrevWeapon", InputActionType.Button);
            prevAction.AddBinding("<Gamepad>/dpad/left");
            prevAction.AddBinding("<Keyboard>/q");
            nextAction = ownedActionMap.AddAction("NextWeapon", InputActionType.Button);
            nextAction.AddBinding("<Gamepad>/dpad/right");
            nextAction.AddBinding("<Keyboard>/e");
        }

        private void OnEnable()
        {
            attackAction?.Enable();
            reloadAction?.Enable();
            prevAction?.Enable();
            nextAction?.Enable();
        }

        private void OnDisable()
        {
            attackAction?.Disable();
            reloadAction?.Disable();
            prevAction?.Disable();
            nextAction?.Disable();
        }

        private void OnDestroy()
        {
            ownedActionMap?.Dispose();
        }

        private void Start()
        {
            WeaponChanged?.Invoke(CurrentWeapon);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            cooldownTimer -= dt;
            meleeFlashTimer -= dt;

            if (prevAction.WasPressedThisFrame()) SwitchWeapon(-1);
            if (nextAction.WasPressedThisFrame()) SwitchWeapon(1);

            if (IsReloading)
            {
                reloadTimer -= dt;
                if (reloadTimer <= 0f) FinishReload();
            }

            if (reloadAction.WasPressedThisFrame()) StartReload();

            // 연사 가능하도록 누르고 있는 동안 계속 공격
            if (attackAction.IsPressed()) TryAttack();
        }

        public void SwitchWeapon(int delta)
        {
            reloadTimer = 0f;
            currentIndex = (currentIndex + delta + weapons.Length) % weapons.Length;
            cooldownTimer = Mathf.Min(cooldownTimer, 0.1f);
            WeaponChanged?.Invoke(CurrentWeapon);
        }

        public void StartReload()
        {
            var w = CurrentWeapon;
            if (!w.UsesAmmo || IsReloading || w.ammo >= w.magazineSize) return;
            reloadTimer = w.reloadTime;
        }

        private void FinishReload()
        {
            reloadTimer = 0f;
            CurrentWeapon.ammo = CurrentWeapon.magazineSize;
            AmmoChanged?.Invoke(CurrentWeapon);
        }

        private void TryAttack()
        {
            if (cooldownTimer > 0f || IsReloading) return;

            var w = CurrentWeapon;
            if (w.UsesAmmo && w.ammo <= 0)
            {
                StartReload();
                return;
            }

            cooldownTimer = w.fireInterval;
            int facing = controller.FacingDirection;

            if (w.type == WeaponType.Melee)
            {
                DoMelee(w, facing);
                return;
            }

            Fire(w, facing);
            w.ammo--;
            AmmoChanged?.Invoke(w);
        }

        private void DoMelee(Weapon w, int facing)
        {
            Vector2 center = (Vector2)transform.position + new Vector2(w.meleeReach * facing, 0f);
            var hits = Physics2D.OverlapBoxAll(center, w.meleeSize, 0f, hitMask);
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent(out IDamageable target))
                    target.TakeDamage(w.damage, new Vector2(facing, 0.3f));
            }
            meleeFlashTimer = 0.1f;

            var slash = new GameObject("MeleeSlash");
            slash.transform.position = center;
            slash.transform.localScale = new Vector3(w.meleeSize.x / 0.25f, w.meleeSize.y / 0.125f, 1f);
            var sr = slash.AddComponent<SpriteRenderer>();
            sr.sprite = GetRuntimeBulletSprite();
            sr.color = new Color(1f, 1f, 1f, 0.35f);
            sr.sortingOrder = 6;
            Destroy(slash, 0.08f);
        }

        private void Fire(Weapon w, int facing)
        {
            Vector2 origin = (Vector2)transform.position + new Vector2(muzzleOffset.x * facing, muzzleOffset.y);
            float baseAngle = facing > 0 ? 0f : 180f;

            for (int i = 0; i < w.pellets; i++)
            {
                float t = w.pellets > 1 ? i / (float)(w.pellets - 1) - 0.5f : 0f;
                float angle = baseAngle + t * w.spreadAngle + UnityEngine.Random.Range(-1.5f, 1.5f) * (w.pellets > 1 ? 1f : 0f);
                float speed = w.projectileSpeed * (w.pellets > 1 ? UnityEngine.Random.Range(0.9f, 1.1f) : 1f);
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

                SpawnProjectile(origin).Launch(dir * speed, w.damage, w.range, hitMask);
            }

            if (w.recoil > 0f)
            {
                Vector2 v = rb.linearVelocity;
                v.x -= facing * w.recoil;
                rb.linearVelocity = v;
            }
        }

        private Projectile SpawnProjectile(Vector2 position)
        {
            if (projectilePrefab != null)
                return Instantiate(projectilePrefab, position, Quaternion.identity);

            var go = new GameObject("Bullet");
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetRuntimeBulletSprite();
            sr.color = new Color(1f, 0.9f, 0.4f);
            sr.sortingOrder = 6;
            return go.AddComponent<Projectile>();
        }

        private Sprite GetRuntimeBulletSprite()
        {
            if (runtimeBulletSprite != null) return runtimeBulletSprite;
            var tex = new Texture2D(4, 2) { filterMode = FilterMode.Point };
            var px = new Color[8];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            runtimeBulletSprite = Sprite.Create(tex, new Rect(0, 0, 4, 2), new Vector2(0.5f, 0.5f), 16f);
            return runtimeBulletSprite;
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || meleeFlashTimer <= 0f) return;
            var w = CurrentWeapon;
            if (w.type != WeaponType.Melee) return;
            Gizmos.color = Color.red;
            Vector2 center = (Vector2)transform.position + new Vector2(w.meleeReach * controller.FacingDirection, 0f);
            Gizmos.DrawWireCube(center, w.meleeSize);
        }

        private void OnGUI()
        {
            if (!showDebugHud) return;
            var w = CurrentWeapon;
            string ammo = w.UsesAmmo ? $"{w.ammo} / {w.magazineSize}" : "-";
            string state = IsReloading ? $"  재장전 중 {ReloadProgress * 100f:0}%" : "";
            GUI.Label(new Rect(12, 12, 400, 24), $"[{currentIndex + 1}/{weapons.Length}] {w.name}   탄약: {ammo}{state}");
        }
    }
}
