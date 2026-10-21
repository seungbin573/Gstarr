using UnityEngine;
using UnityEngine.Events;

namespace Gstarr.Combat
{
    public interface IDamageable
    {
        void TakeDamage(int amount, Vector2 hitDirection);
    }

    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private int maxHealth = 5;
        [SerializeField] private float knockbackForce = 4f;
        [SerializeField] private bool destroyOnDeath = true;

        public UnityEvent<int, int> onHealthChanged;
        public UnityEvent onDeath;

        public int Current { get; private set; }
        public int Max => maxHealth;

        private SpriteRenderer spriteRenderer;
        private Rigidbody2D rb;
        private float flashTimer;
        private Color baseColor;

        private void Awake()
        {
            Current = maxHealth;
            spriteRenderer = GetComponent<SpriteRenderer>();
            rb = GetComponent<Rigidbody2D>();
            if (spriteRenderer != null) baseColor = spriteRenderer.color;
        }

        public void TakeDamage(int amount, Vector2 hitDirection)
        {
            if (Current <= 0) return;

            Current = Mathf.Max(0, Current - amount);
            onHealthChanged?.Invoke(Current, maxHealth);

            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
                rb.AddForce(hitDirection.normalized * knockbackForce, ForceMode2D.Impulse);

            flashTimer = 0.08f;
            if (spriteRenderer != null) spriteRenderer.color = Color.white;

            if (Current == 0)
            {
                onDeath?.Invoke();
                if (destroyOnDeath) Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (flashTimer <= 0f) return;
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && spriteRenderer != null)
                spriteRenderer.color = baseColor;
        }
    }
}
