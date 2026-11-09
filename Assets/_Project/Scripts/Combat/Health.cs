using UnityEngine;
using UnityEngine.Events;
using Gstarr.Game;
using Gstarr.Player;

namespace Gstarr.Combat
{
    public interface IDamageable
    {
        bool TakeDamage(int amount, Vector2 hitDirection);
    }

    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private int maxHealth = 5;
        [SerializeField] private float knockbackForce = 4f;
        [SerializeField] private bool destroyOnDeath = false;

        public UnityEvent<int, int> onHealthChanged = new UnityEvent<int, int>();
        public UnityEvent onDeath = new UnityEvent();

        public int Current { get; private set; }
        public int Max => maxHealth;
        /// <summary>0보다 크면 피격 후 이 시간(초) 동안 무적. 플레이어만 켠다.</summary>
        public float InvulnOnHit { get; set; }
        public bool IsInvulnerable => hitTimer > 0f || dashTimer > 0f || itemTimer > 0f;

        /// <summary>대시가 끝날 때 대시 무적만 걷어낸다.</summary>
        public void ClearInvulnerability()
        {
            dashTimer = 0f;
        }

        /// <summary>대시처럼 화면 효과 없이 무적이 된다.</summary>
        public void GrantInvulnerability(float seconds)
        {
            dashTimer = Mathf.Max(dashTimer, Mathf.Max(0f, seconds));
        }

        /// <summary>아이템 무적. 깜빡이지 않고, 몸 색이 바뀌며 주변에 원이 뜬다.</summary>
        public void GrantInvulnerability(float seconds, Color bodyColor)
        {
            itemTimer = Mathf.Max(itemTimer, Mathf.Max(0f, seconds));
            tintColor = bodyColor;
            BeginBodyTint();
            ShowBarrier();
        }

        public void Configure(int max, bool destroy)
        {
            maxHealth = Mathf.Max(1, max);
            Current = maxHealth;
            destroyOnDeath = destroy;
        }

        private SpriteRenderer spriteRenderer;
        private SpriteRenderer[] blinkRenderers;
        private Rigidbody2D rb;
        private float flashTimer;
        private float hitTimer;
        private float dashTimer;
        private float itemTimer;
        private Color tintColor = Color.white;
        private Color baseColor;
        private SpriteRenderer[] tintRenderers;
        private Color[] tintBases;
        private SpriteRenderer barrier;
        private static Sprite barrierSprite;

        private void Awake()
        {
            if (onHealthChanged == null) onHealthChanged = new UnityEvent<int, int>();
            if (onDeath == null) onDeath = new UnityEvent();
            Current = maxHealth;
            spriteRenderer = GetComponent<SpriteRenderer>();
            rb = GetComponent<Rigidbody2D>();
            if (spriteRenderer != null) baseColor = spriteRenderer.color;
        }

        private void Start()
        {
            blinkRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        /// <summary>적의 공격. 맞으면 가끔 방해 그림이 뜬다.</summary>
        public bool TakeEnemyHit(int amount, Vector2 hitDirection)
        {
            if (!TakeDamage(amount, hitDirection)) return false;
            if (Current > 0 && GetComponent<PlayerController>() != null)
                FoeEffects.MaybeOnHit();
            return true;
        }

        public bool TakeDamage(int amount, Vector2 hitDirection)
        {
            if (Current <= 0 || IsInvulnerable) return false;

            var enemy = GetComponent<EnemyActor>();
            if (enemy != null && enemy.AbsorbsDamage) return false;

            int next = Mathf.Max(0, Current - amount);
            bool caught = enemy != null && enemy.CatchGlory(ref next);
            Current = next;
            onHealthChanged?.Invoke(Current, maxHealth);

            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
                rb.AddForce(hitDirection.normalized * knockbackForce, ForceMode2D.Impulse);

            if (InvulnOnHit > 0f)
            {
                hitTimer = InvulnOnHit;
                blinkRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            }
            else
            {
                flashTimer = 0.08f;
                if (spriteRenderer != null) spriteRenderer.color = Color.white;
            }

            bool playerHit = GetComponent<PlayerController>() != null;
            if (playerHit)
            {
                GameAudio.Play(GameAudio.Id.Hurt);
                CombatFeel.Impact(transform.position, false);
                GetComponent<SpriteMotion>()?.HurtPose();
            }
            else
            {
                GameAudio.Play(GameAudio.Id.Hit, caught ? 0.7f : 1f);
                CombatFeel.Impact(transform.position, amount >= 2);
                GetComponent<SpriteMotion>()?.HurtPose();
            }

            if (caught) enemy.EnterStagger();
            else if (Current == 0)
            {
                onDeath?.Invoke();
                CombatFeel.Burst(transform.position, playerHit ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.75f, 0.3f), 10);
                if (playerHit)
                    GetComponent<SpriteMotion>()?.PlayDeath();
                else
                    HideSprites();
                if (enemy != null)
                {
                    if (EnemyActor.CountAlive() == 0 && !Gstarr.World.ArenaDoorSpawner.HasUnopenedRooms)
                        StageHUD.ShowGameClear();
                }
                else if (playerHit) StageHUD.BeginPlayerDeath();
                if (destroyOnDeath)
                {
                    var body = GetComponent<Rigidbody2D>();
                    if (body != null) body.simulated = false;
                    var cols = GetComponents<Collider2D>();
                    for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
                    Destroy(gameObject, 0.32f);
                }
            }
            return true;
        }

        /// <summary>회복. 이미 최대 체력이면 false.</summary>
        public bool Heal(int amount)
        {
            if (Current <= 0 || Current >= maxHealth) return false;
            Current = Mathf.Min(maxHealth, Current + amount);
            onHealthChanged?.Invoke(Current, maxHealth);
            return true;
        }

        private void Update()
        {
            if (Current <= 0)
            {
                HideBarrier();
                if (GetComponent<PlayerController>() == null)
                    HideSprites();
                return;
            }

            if (hitTimer > 0f) hitTimer -= Time.deltaTime;
            if (dashTimer > 0f) dashTimer -= Time.deltaTime;
            if (itemTimer > 0f) itemTimer -= Time.deltaTime;

            if (Gstarr.Game.GamePause.IsGloryKill)
            {
                SetBlinkVisible(true);
                return;
            }

            if (itemTimer > 0f)
            {
                SetBlinkVisible(true);
                ApplyBodyTint();
                UpdateBarrier();
            }
            else if (dashTimer > 0f)
            {
                HideBarrier();
                EndBodyTint();
                SetBlinkVisible(true);
            }
            else if (hitTimer > 0f)
            {
                HideBarrier();
                EndBodyTint();
                bool visible = Mathf.Repeat(Time.unscaledTime * 8f, 1f) < 0.55f;
                if (blinkRenderers != null)
                    for (int i = 0; i < blinkRenderers.Length; i++)
                        if (blinkRenderers[i] != null && !IsHiddenBody(blinkRenderers[i]) && !IsEffect(blinkRenderers[i]))
                            blinkRenderers[i].enabled = visible;
            }
            else
            {
                HideBarrier();
                EndBodyTint();
                SetBlinkVisible(true);
            }

            if (flashTimer <= 0f) return;
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && spriteRenderer != null)
                spriteRenderer.color = baseColor;
        }

        private void OnDisable()
        {
            itemTimer = 0f;
            HideBarrier();
            EndBodyTint();
        }

        private void HideSprites()
        {
            if (blinkRenderers == null || blinkRenderers.Length == 0)
                blinkRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < blinkRenderers.Length; i++)
                if (blinkRenderers[i] != null)
                    blinkRenderers[i].enabled = false;
        }

        private void SetBlinkVisible(bool visible)
        {
            if (blinkRenderers == null) return;
            for (int i = 0; i < blinkRenderers.Length; i++)
                if (blinkRenderers[i] != null && !IsHiddenBody(blinkRenderers[i]) && !IsEffect(blinkRenderers[i]))
                    blinkRenderers[i].enabled = visible;
        }

        private void ShowBarrier()
        {
            EnsureBarrier();
            barrier.enabled = true;
        }

        private void UpdateBarrier()
        {
            if (barrier == null) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
            barrier.enabled = true;
            var color = tintColor;
            color.a = 0.45f + 0.25f * pulse;
            barrier.color = color;
            barrier.transform.localScale = Vector3.one * (2.55f + 0.08f * pulse);
        }

        private void HideBarrier()
        {
            if (barrier != null) barrier.enabled = false;
        }

        private void EnsureBarrier()
        {
            if (barrier != null) return;
            var go = new GameObject("ItemBarrier");
            go.transform.SetParent(transform, false);
            var box = GetComponent<BoxCollider2D>();
            float centerY = box != null ? box.offset.y : 0.25f;
            go.transform.localPosition = new Vector3(0f, centerY, 0f);
            barrier = go.AddComponent<SpriteRenderer>();
            barrier.sprite = BarrierSprite();
            if (spriteRenderer != null) barrier.sortingLayerID = spriteRenderer.sortingLayerID;
            int order = spriteRenderer != null ? spriteRenderer.sortingOrder : 5;
            barrier.sortingOrder = order + 9;
            barrier.enabled = false;
        }

        private static Sprite BarrierSprite()
        {
            if (barrierSprite != null) return barrierSprite;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float outer = Mathf.SmoothStep(1f, 0.86f, r);
                float inner = Mathf.SmoothStep(0.68f, 0.8f, r);
                byte a = (byte)(Mathf.Clamp01(outer * inner) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            barrierSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return barrierSprite;
        }

        private static bool IsEffect(SpriteRenderer renderer)
        {
            if (renderer == null) return false;
            string name = renderer.gameObject.name;
            return name == "ItemBarrier" || name == "HeldItem" || name == "WeaponSprite";
        }

        private void BeginBodyTint()
        {
            if (tintBases != null) return;
            var all = GetComponentsInChildren<SpriteRenderer>(true);
            int count = 0;
            for (int i = 0; i < all.Length; i++)
                if (IsBodySprite(all[i])) count++;
            tintRenderers = new SpriteRenderer[count];
            tintBases = new Color[count];
            int n = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (!IsBodySprite(all[i])) continue;
                tintRenderers[n] = all[i];
                tintBases[n] = all[i].color;
                n++;
            }
        }

        private void ApplyBodyTint()
        {
            if (tintRenderers == null) return;
            float amount = 0.62f + 0.18f * Mathf.Sin(Time.time * 7f);
            for (int i = 0; i < tintRenderers.Length; i++)
            {
                if (tintRenderers[i] == null) continue;
                tintRenderers[i].color = Color.Lerp(tintBases[i], tintColor, amount);
            }
        }

        private void EndBodyTint()
        {
            if (tintRenderers == null || tintBases == null) return;
            for (int i = 0; i < tintRenderers.Length; i++)
                if (tintRenderers[i] != null) tintRenderers[i].color = tintBases[i];
            tintRenderers = null;
            tintBases = null;
        }

        private bool IsBodySprite(SpriteRenderer renderer)
        {
            if (renderer == null || IsHiddenBody(renderer)) return false;
            string name = renderer.gameObject.name;
            return name != "WeaponSprite" && name != "HeldItem" && name != "ItemBarrier";
        }

        private bool IsHiddenBody(SpriteRenderer renderer)
        {
            var motion = GetComponent<SpriteMotion>();
            return motion != null && motion.IsSource(renderer);
        }
    }
}
