using Gstarr.CameraSystem;
using Gstarr.Combat;
using Gstarr.Player;
using UnityEngine;

namespace Gstarr.Game
{
    /// <summary>히트스톱, 카메라 흔들림, 맞는 자리에 튀는 파편.</summary>
    public static class CombatFeel
    {
        private static FeelClock clock;
        private static Sprite pixel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (clock != null) return;
            var go = new GameObject("CombatFeel");
            go.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(go);
            clock = go.AddComponent<FeelClock>();
        }

        public static void Impact(Vector3 position, bool heavy)
        {
            Shake(heavy ? 0.2f : 0.09f, heavy ? 0.14f : 0.08f);
            GamePause.HitStop(heavy ? 0.05f : 0.03f);
            Burst(position, heavy ? new Color(1f, 0.55f, 0.2f) : Color.white, heavy ? 8 : 5);
        }

        public static void Shake(float magnitude, float duration)
        {
            var camera = Object.FindAnyObjectByType<CameraFollow2D>();
            if (camera != null) camera.Shake(magnitude, duration);
        }

        public static void Burst(Vector3 position, Color color, int count)
        {
            var sprite = Pixel();
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float speed = Random.Range(2.5f, heavySpeed(count));
                var go = new GameObject("Spark");
                go.transform.position = position;
                go.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = color;
                renderer.sortingOrder = 30;
                var spark = go.AddComponent<FadeSpark>();
                spark.Init(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed, Random.Range(0.12f, 0.22f));
            }
        }

        private static float heavySpeed(int count) => count > 6 ? 7f : 5f;

        private static Sprite Pixel()
        {
            if (pixel != null) return pixel;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[4];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            pixel = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            return pixel;
        }

        private class FeelClock : MonoBehaviour
        {
            private void Update() => GamePause.TickHitStop();
        }
    }

    /// <summary>
    /// 몸 스프라이트만 따로 움직여 걷기, 점프, 피격, 사망을 보여 준다.
    /// 루트 스케일은 충돌 크기라 건드리지 않는다.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class SpriteMotion : MonoBehaviour
    {
        private SpriteRenderer source;
        private SpriteRenderer visual;
        private Rigidbody2D body;
        private PlayerController player;
        private bool boss;
        private float squashX = 1f;
        private float squashY = 1f;
        private float kick;
        private Color shownColor = Color.white;
        private bool dying;
        private float deathTime;
        private bool wasGrounded = true;

        private void Awake()
        {
            source = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            player = GetComponent<PlayerController>();
            boss = GetComponent<BossActor>() != null;
            if (source == null || source.sprite == null) return;

            var go = new GameObject("BodyVisual");
            go.transform.SetParent(transform, false);
            visual = go.AddComponent<SpriteRenderer>();
            visual.sprite = source.sprite;
            visual.sortingLayerID = source.sortingLayerID;
            visual.sortingOrder = source.sortingOrder;
            visual.color = source.color;
            shownColor = source.color;
            visual.flipX = source.flipX;
            visual.flipY = source.flipY;
        }

        public bool IsSource(SpriteRenderer renderer) => renderer == source;

        public void JumpPose()
        {
            squashX = 0.82f;
            squashY = 1.2f;
        }

        public void AttackPose()
        {
            squashX = 1.16f;
            squashY = 0.88f;
            kick = 0.08f;
        }

        public void HurtPose()
        {
            squashX = 1.24f;
            squashY = 0.76f;
            kick = 0.14f;
        }

        public void PlayDeath()
        {
            dying = true;
            deathTime = 0.28f;
        }

        private void LateUpdate()
        {
            if (visual == null || source == null) return;

            if (source.sprite != visual.sprite) visual.sprite = source.sprite;
            visual.flipX = source.flipX;
            visual.flipY = source.flipY;
            visual.sortingOrder = source.sortingOrder;

            if (source.color.a > 0.02f) shownColor = source.color;
            var color = shownColor;
            if (dying)
            {
                deathTime -= Time.unscaledDeltaTime;
                float t = 1f - Mathf.Clamp01(deathTime / 0.28f);
                squashX = Mathf.Lerp(1.3f, 0.2f, t);
                squashY = Mathf.Lerp(0.7f, 1.6f, t);
                color.a *= 1f - t;
                visual.transform.localRotation = Quaternion.Euler(0f, 0f, t * 28f);
                visual.enabled = deathTime > 0f;
            }
            else
            {
                TickPose();
                var health = GetComponent<Health>();
                if (health == null || !health.IsInvulnerable)
                    visual.enabled = source.enabled;
            }

            visual.color = color;
            var hidden = source.color;
            hidden.a = 0f;
            source.color = hidden;
            visual.transform.localScale = new Vector3(squashX, squashY, 1f);
            float facing = transform.localScale.x >= 0f ? 1f : -1f;
            visual.transform.localPosition = new Vector3(kick * facing, Bob(), 0f);
        }

        private void TickPose()
        {
            bool dashing = player != null && player.IsDashing;
            if (!boss && player != null && player.IsGrounded && !wasGrounded && !dashing)
            {
                squashX = 1.22f;
                squashY = 0.74f;
            }
            if (player != null) wasGrounded = player.IsGrounded;

            float dt = Time.deltaTime;
            squashX = Mathf.MoveTowards(squashX, 1f, dt * 5f);
            squashY = Mathf.MoveTowards(squashY, 1f, dt * 5f);
            kick = Mathf.MoveTowards(kick, 0f, dt * 1.4f);
            if (dashing)
            {
                squashX = 1.28f;
                squashY = 0.78f;
            }
        }

        private float Bob()
        {
            if (boss || dying || body == null) return 0f;
            bool moving = player != null
                ? player.IsGrounded && Mathf.Abs(player.Velocity.x) > 0.6f && !player.IsDashing
                : body.linearVelocity.sqrMagnitude > 0.4f;
            if (!moving) return 0f;
            return Mathf.Sin(Time.time * 16f) * 0.05f;
        }
    }

    public class FadeSpark : MonoBehaviour
    {
        private Vector2 velocity;
        private float life;
        private float duration;
        private SpriteRenderer spark;

        public void Init(Vector2 velocity, float duration)
        {
            this.velocity = velocity;
            this.duration = duration;
            life = duration;
            spark = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            transform.position += (Vector3)velocity * Time.unscaledDeltaTime;
            velocity *= 1f - 6f * Time.unscaledDeltaTime;
            life -= Time.unscaledDeltaTime;
            if (spark != null)
            {
                var color = spark.color;
                color.a = Mathf.Clamp01(life / duration);
                spark.color = color;
            }
            if (life <= 0f) Destroy(gameObject);
        }
    }
}
