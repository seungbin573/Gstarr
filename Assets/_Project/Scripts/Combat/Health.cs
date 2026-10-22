using UnityEngine;
using UnityEngine.Events;

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

        public UnityEvent<int, int> onHealthChanged;
        public UnityEvent onDeath;

        public int Current { get; private set; }
        public int Max => maxHealth;
        /// <summary>0보다 크면 피격 후 이 시간(초) 동안 무적. 플레이어만 켠다.</summary>
        public float InvulnOnHit { get; set; }
        public bool IsInvulnerable => invulnTimer > 0f;

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
        private float invulnTimer;
        private Color baseColor;

        private void Awake()
        {
            Current = maxHealth;
            spriteRenderer = GetComponent<SpriteRenderer>();
            rb = GetComponent<Rigidbody2D>();
            if (spriteRenderer != null) baseColor = spriteRenderer.color;
        }

        private void Start()
        {
            blinkRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        public bool TakeDamage(int amount, Vector2 hitDirection)
        {
            if (Current <= 0 || invulnTimer > 0f) return false;

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
                invulnTimer = InvulnOnHit;
                if (blinkRenderers == null || blinkRenderers.Length == 0)
                    blinkRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            }
            else
            {
                flashTimer = 0.08f;
                if (spriteRenderer != null) spriteRenderer.color = Color.white;
            }

            if (caught) enemy.EnterStagger();
            else if (Current == 0)
            {
                onDeath?.Invoke();
                if (destroyOnDeath) Destroy(gameObject);
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
            if (invulnTimer > 0f)
            {
                invulnTimer -= Time.deltaTime;
                if (Gstarr.Game.GamePause.IsGloryKill)
                {
                    SetBlinkVisible(true);
                    return;
                }
                bool visible = Mathf.Repeat(Time.unscaledTime * 8f, 1f) < 0.55f || invulnTimer <= 0f;
                if (blinkRenderers != null)
                    for (int i = 0; i < blinkRenderers.Length; i++)
                        if (blinkRenderers[i] != null) blinkRenderers[i].enabled = visible || invulnTimer <= 0f;
                if (invulnTimer <= 0f) SetBlinkVisible(true);
                return;
            }

            if (flashTimer <= 0f) return;
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && spriteRenderer != null)
                spriteRenderer.color = baseColor;
        }

        private void SetBlinkVisible(bool visible)
        {
            if (blinkRenderers == null) return;
            for (int i = 0; i < blinkRenderers.Length; i++)
                if (blinkRenderers[i] != null) blinkRenderers[i].enabled = visible;
        }
    }
}
