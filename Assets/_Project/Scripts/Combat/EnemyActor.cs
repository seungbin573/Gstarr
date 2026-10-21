using System.Collections.Generic;
using Gstarr.Game;
using Gstarr.Rhythm;
using UnityEngine;

namespace Gstarr.Combat
{
    /// <summary>
    /// 박자마다 플레이어에게 걸어와 공격한다.
    /// 체력이 20% 이하가 되면 노랗게 비틀거리고, 그 사이 가까이서 공격하면 글로리 킬이 된다.
    /// 시간을 넘기면 비틀거림이 끝나고 다시 싸운다. 한 번 놓치면 다시 비틀거리지 않는다.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyActor : MonoBehaviour
    {
        private enum State
        {
            Combat,
            Stagger,
            Glory,
            Dead
        }

        private static readonly List<EnemyActor> All = new List<EnemyActor>();

        [SerializeField] private float moveSpeed = 6.5f;
        [SerializeField] private float jumpSpeed = 13f;
        [SerializeField] private float attackRange = 1.25f;
        [SerializeField] private int attackDamage = 1;
        [SerializeField] private float staggerSeconds = 4f;
        [SerializeField] private Color bodyColor = new Color(0.85f, 0.22f, 0.25f);
        [SerializeField] private Color staggerColor = new Color(1f, 0.82f, 0.15f);

        private Health health;
        private Rigidbody2D rb;
        private SpriteRenderer sprite;
        private Transform player;
        private RhythmConductor conductor;
        private bool beatBound;
        private State state;
        private bool staggerSpent;
        private float staggerTimer;
        private float stepTimer;
        private Color idleColor;
        private LayerMask groundMask;
        private Collider2D body;

        public bool IsStaggered => state == State.Stagger;
        public bool AbsorbsDamage => state == State.Stagger || state == State.Glory;
        public int GloryFloor => Mathf.Max(1, Mathf.CeilToInt(health.Max * 0.2f));

        public static Sprite Art
        {
            get
            {
                var assets = Gstarr.Items.GameAssets.Instance;
                return assets != null ? assets.enemySprite : null;
            }
        }

        public static EnemyActor Spawn(Sprite spriteSource, Vector3 position, int maxHealth = 16)
        {
            if (spriteSource == null) spriteSource = Art;
            var go = new GameObject("Enemy");
            go.transform.position = position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spriteSource;
            sr.color = Color.white;
            sr.sortingOrder = 4;

            var body = go.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.gravityScale = 4f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var box = go.AddComponent<BoxCollider2D>();
            if (spriteSource != null)
            {
                var size = (Vector2)spriteSource.bounds.size;
                box.size = new Vector2(size.x * 0.75f, size.y * 0.9f);
                box.offset = spriteSource.bounds.center;
            }
            else box.size = new Vector2(0.8f, 1.4f);

            var hp = go.AddComponent<Health>();
            hp.Configure(maxHealth, true);
            return go.AddComponent<EnemyActor>();
        }

        public static EnemyActor NearestStaggered(Vector3 from, float range)
        {
            EnemyActor best = null;
            float bestSq = range * range;
            for (int i = 0; i < All.Count; i++)
            {
                var enemy = All[i];
                if (enemy == null || !enemy.IsStaggered) continue;
                float sq = ((Vector2)enemy.transform.position - (Vector2)from).sqrMagnitude;
                if (sq <= bestSq)
                {
                    bestSq = sq;
                    best = enemy;
                }
            }
            return best;
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            rb = GetComponent<Rigidbody2D>();
            sprite = GetComponent<SpriteRenderer>();
            body = GetComponent<Collider2D>();
            groundMask = LayerMask.GetMask("Ground");
            if (groundMask.value == 0) groundMask = Physics2D.DefaultRaycastLayers;

            var art = Art;
            if (art != null && sprite != null)
            {
                sprite.sprite = art;
                sprite.color = Color.white;
                FitCollider(art);
            }
            idleColor = sprite != null ? sprite.color : bodyColor;
        }

        private void FitCollider(Sprite art)
        {
            if (body is BoxCollider2D box && art != null)
            {
                var size = (Vector2)art.bounds.size;
                box.size = new Vector2(Mathf.Max(0.4f, size.x * 0.7f), Mathf.Max(0.6f, size.y * 0.9f));
                box.offset = art.bounds.center;
            }
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void OnDestroy()
        {
            if (conductor != null) conductor.Beat -= OnBeat;
        }

        /// <summary>이번 타격으로 20% 선 아래로 내려가면 그 체력에 붙잡아 둔다.</summary>
        public bool CatchGlory(ref int nextHealth)
        {
            if (state != State.Combat || staggerSpent || health == null) return false;
            int floor = GloryFloor;
            if (health.Current > floor && nextHealth <= floor)
            {
                nextHealth = floor;
                return true;
            }
            return false;
        }

        public void EnterStagger()
        {
            if (state != State.Combat || staggerSpent) return;
            state = State.Stagger;
            staggerTimer = staggerSeconds;
            if (sprite != null) sprite.color = staggerColor;
            if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        public void BeginGlory()
        {
            state = State.Glory;
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        public void CancelGlory()
        {
            staggerSpent = true;
            state = State.Combat;
            transform.rotation = Quaternion.identity;
            if (sprite != null) sprite.color = idleColor;
        }

        public void FinishGlory()
        {
            state = State.Dead;
            if (health != null && health.Current > 0)
                health.TakeDamage(health.Current, Vector2.up);
        }

        private void Update()
        {
            BindConductor();
            if (player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }

            if (state == State.Stagger)
            {
                staggerTimer -= Time.deltaTime;
                if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                if (staggerTimer <= 0f) CancelGlory();
                return;
            }

            if (state == State.Glory)
            {
                if (rb != null) rb.linearVelocity = new Vector2(0f, Mathf.Min(0f, rb.linearVelocity.y));
                return;
            }

            if (stepTimer > 0f)
            {
                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0f && rb != null)
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }

        private void LateUpdate()
        {
            if (state != State.Stagger && state != State.Glory) return;
            float wobble = Mathf.Sin(Time.unscaledTime * (state == State.Stagger ? 28f : 18f)) * (state == State.Stagger ? 10f : 6f);
            transform.rotation = Quaternion.Euler(0f, 0f, wobble);
            if (sprite != null) sprite.color = staggerColor;
        }

        private void BindConductor()
        {
            if (beatBound) return;
            conductor = RhythmConductor.Instance;
            if (conductor == null) return;
            conductor.Beat += OnBeat;
            beatBound = true;
        }

        private void OnBeat(long beat)
        {
            if (state != State.Combat || player == null || GamePause.BlocksGameplay) return;

            float dx = player.position.x - transform.position.x;
            float dy = player.position.y - transform.position.y;
            Face(dx);

            if (Mathf.Abs(dx) <= attackRange && Mathf.Abs(dy) < 1.6f)
            {
                Attack();
                return;
            }

            if (Mathf.Abs(dx) > 0.35f && rb != null)
            {
                float dir = Mathf.Sign(dx);
                bool grounded = IsGrounded();
                bool jump = grounded && (dy > 1.2f || GapAhead(dir) || WallAhead(dir));
                float vy = jump ? jumpSpeed : rb.linearVelocity.y;
                rb.linearVelocity = new Vector2(dir * moveSpeed, vy);
                stepTimer = jump ? 0.4f : 0.16f;
            }
        }

        private bool IsGrounded()
        {
            if (rb != null && rb.linearVelocity.y > 0.15f) return false;
            Vector2 origin = body != null
                ? (Vector2)body.bounds.center + Vector2.down * (body.bounds.extents.y + 0.04f)
                : (Vector2)transform.position + Vector2.down * 0.8f;
            return Physics2D.OverlapCircle(origin, 0.12f, groundMask) != null;
        }

        private bool GapAhead(float dir)
        {
            Vector2 origin = (body != null ? (Vector2)body.bounds.center : (Vector2)transform.position) + new Vector2(dir * 0.9f, -0.2f);
            return Physics2D.Raycast(origin, Vector2.down, 1.6f, groundMask).collider == null;
        }

        private bool WallAhead(float dir)
        {
            Vector2 origin = body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;
            return Physics2D.Raycast(origin, Vector2.right * dir, 0.7f, groundMask).collider != null;
        }

        private void Attack()
        {
            if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            Vector2 center = (Vector2)transform.position + new Vector2(Facing() * 0.7f, 0.2f);
            var hits = Physics2D.OverlapBoxAll(center, new Vector2(1.1f, 1.2f), 0f);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].GetComponentInParent<Gstarr.Player.PlayerController>() == null) continue;
                if (hits[i].GetComponentInParent<Health>() is Health target)
                    target.TakeDamage(attackDamage, new Vector2(Facing(), 0.4f));
                break;
            }
        }

        private void Face(float dx)
        {
            if (Mathf.Abs(dx) < 0.05f) return;
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * (dx >= 0f ? 1f : -1f);
            transform.localScale = s;
        }

        private int Facing() => transform.localScale.x >= 0f ? 1 : -1;
    }
}
