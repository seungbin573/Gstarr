using System.Collections.Generic;
using Gstarr.Game;
using Gstarr.Rhythm;
using UnityEngine;

namespace Gstarr.Combat
{
    public enum EnemyKind
    {
        BeatChaser,
        RangedBeat,
        RangedHalfBeat,
        Flyer,
        Mole,
        Boss
    }

    [RequireComponent(typeof(Health))]
    public class EnemyActor : MonoBehaviour
    {
        private enum State { Combat, Stunned, Stagger, Glory, Dead }

        private static readonly List<EnemyActor> All = new List<EnemyActor>();

        [SerializeField] private EnemyKind kind;
        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField] private float attackRange = 1.35f;
        [SerializeField] private float detectionRange = 8f;
        [SerializeField] private int attackDamage = 1;
        [SerializeField] private float staggerSeconds = 5f;
        [SerializeField] private Color bodyColor = new Color(0.85f, 0.22f, 0.25f);
        [SerializeField] private Color staggerColor = new Color(1f, 0.82f, 0.15f);
        [SerializeField] private Color stunnedColor = new Color(0.5f, 0.9f, 1f);

        private Health health;
        private Rigidbody2D rb;
        private SpriteRenderer sprite;
        private Transform player;
        private RhythmConductor conductor;
        private bool beatBound;
        private State state;
        private bool staggerSpent;
        private float staggerTimer;
        private float stunTimer;
        private Color idleColor;
        private LayerMask groundMask;
        private Collider2D body;
        private int patrolDirection = 1;
        private Vector3 patrolHome;
        private bool preparing;
        private bool attackArmed;
        private float beatMoveTimer;

        private bool moleInitialized;
        private bool moleBuried;
        private bool moleEmerging;
        private bool moleActive;
        private Vector3 moleSurfacePosition;
        private Vector3 moleDestinationPosition;
        private float moleHeight;
        private float moleGroundY;
        private float moleTimer;
        private SpriteRenderer moleTrail;
        private SpriteRenderer moleDestinationMarker;

        private const float WallProbe = 0.22f;
        private const float GroundProbe = 1.15f;
        private const float FlyerPatrolWidth = 3f;

        public bool IsStaggered => state == State.Stagger;
        public bool IsStunned => state == State.Stunned;
        public bool AbsorbsDamage => state == State.Stagger || state == State.Glory;
        public bool IsBoss => kind == EnemyKind.Boss;
        public bool IsDead => state == State.Dead || health == null || health.Current <= 0;
        public int GloryFloor => Mathf.Max(1, Mathf.CeilToInt(health.Max * 0.3f));

        public static Sprite Art
        {
            get
            {
                var assets = Gstarr.Items.GameAssets.Instance;
                return assets != null ? assets.enemySprite : null;
            }
        }

        private static Sprite ArtFor(EnemyKind enemyKind)
        {
            var assets = Gstarr.Items.GameAssets.Instance;
            int index = enemyKind switch
            {
                EnemyKind.RangedBeat => 1,
                EnemyKind.RangedHalfBeat => 2,
                EnemyKind.Flyer => 3,
                _ => 0
            };
            if (assets != null && assets.enemySprites != null && index < assets.enemySprites.Length
                && assets.enemySprites[index] != null)
                return assets.enemySprites[index];
            return Art;
        }

        public static EnemyActor Spawn(Sprite spriteSource, Vector3 position, int maxHealth = 0) =>
            Spawn(spriteSource, position, EnemyKind.BeatChaser, maxHealth);

        public static EnemyActor Spawn(Sprite spriteSource, Vector3 position, EnemyKind kind, int maxHealth = 0)
        {
            if (spriteSource == null) spriteSource = ArtFor(kind);
            var go = new GameObject($"Enemy_{kind}");
            go.transform.position = position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spriteSource;
            sr.sortingOrder = 4;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            rb.gravityScale = 4f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var box = go.AddComponent<BoxCollider2D>();
            if (spriteSource != null)
            {
                var size = (Vector2)spriteSource.bounds.size;
                box.size = new Vector2(size.x * 0.75f, size.y * 0.9f);
                box.offset = spriteSource.bounds.center;
            }
            else box.size = new Vector2(0.8f, 1.4f);

            var hp = go.AddComponent<Health>();
            hp.Configure(maxHealth > 0 ? maxHealth : DefaultHealth(kind), true);
            var enemy = go.AddComponent<EnemyActor>();
            enemy.SetKind(kind);
            if (kind == EnemyKind.Boss)
            {
                go.transform.localScale = Vector3.one * 1.6f;
                go.AddComponent<BossActor>();
            }
            return enemy;
        }

        public static EnemyActor SpawnBoss(Vector3 position, int maxHealth = 0) =>
            Spawn(null, position, EnemyKind.Boss, maxHealth);

        private static int DefaultHealth(EnemyKind kind) => kind switch
        {
            EnemyKind.BeatChaser => 6,
            EnemyKind.RangedBeat => 6,
            EnemyKind.RangedHalfBeat => 6,
            EnemyKind.Flyer => 5,
            EnemyKind.Mole => 7,
            EnemyKind.Boss => 28,
            _ => 8
        };

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

        public static EnemyActor NearestInRange(Vector2 from, float range)
        {
            EnemyActor best = null;
            float bestSq = range * range;
            for (int i = 0; i < All.Count; i++)
            {
                var enemy = All[i];
                if (enemy == null || !enemy.IsAimTarget) continue;
                float sq = ((Vector2)enemy.AimPoint - from).sqrMagnitude;
                if (sq >= bestSq) continue;
                bestSq = sq;
                best = enemy;
            }
            return best;
        }

        /// <summary>플레이어가 바라보는 쪽, 사거리 안의 가장 가까운 적.</summary>
        public static EnemyActor NearestInFront(Vector2 from, int facing, float range)
        {
            if (facing == 0) facing = 1;
            EnemyActor best = null;
            float bestSq = range * range;
            for (int i = 0; i < All.Count; i++)
            {
                var enemy = All[i];
                if (enemy == null || !enemy.IsAimTarget) continue;
                Vector2 delta = enemy.AimPoint - from;
                if (delta.x * facing <= 0.05f) continue;
                float sq = delta.sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = enemy;
                }
            }
            return best;
        }

        public Vector2 AimPoint => body != null && body.enabled ? body.bounds.center : (Vector2)transform.position;

        private bool IsAimTarget =>
            state != State.Dead
            && state != State.Glory
            && health != null
            && health.Current > 0
            && (sprite == null || sprite.enabled);

        private void Awake()
        {
            health = GetComponent<Health>();
            rb = GetComponent<Rigidbody2D>();
            sprite = GetComponent<SpriteRenderer>();
            body = GetComponent<Collider2D>();
            groundMask = LayerMask.GetMask("Ground");
            if (groundMask.value == 0) groundMask = Physics2D.DefaultRaycastLayers;

            if (sprite != null && sprite.sprite == null) sprite.sprite = Art;
            patrolHome = transform.position;
            ApplyKind();
            if (GetComponent<Gstarr.Game.SpriteMotion>() == null)
                gameObject.AddComponent<Gstarr.Game.SpriteMotion>();
        }

        private void SetKind(EnemyKind value)
        {
            kind = value;
            ApplyKind();
            moveSpeed = kind switch
            {
                EnemyKind.BeatChaser => 4.2f,
                EnemyKind.RangedBeat => 3.6f,
                EnemyKind.RangedHalfBeat => 3.8f,
                EnemyKind.Flyer => 4.8f,
                EnemyKind.Mole => 3.6f,
                _ => moveSpeed
            };
        }

        private void ApplyKind()
        {
            if (sprite == null) sprite = GetComponent<SpriteRenderer>();
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (body == null) body = GetComponent<Collider2D>();

            idleColor = Color.white;
            if (sprite != null) sprite.color = idleColor;
            if (rb != null) rb.gravityScale = kind == EnemyKind.Flyer ? 0f : 4f;
            if (kind == EnemyKind.Flyer && rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
            }
            ApplyHitbox();
        }

        /// <summary>종류마다 다른 히트박스. 발밑은 스프라이트 바닥에 맞춘다.</summary>
        private void ApplyHitbox()
        {
            Bounds art = sprite != null && sprite.sprite != null
                ? sprite.sprite.bounds
                : new Bounds(new Vector3(0f, 0.6f, 0f), new Vector3(1f, 1.2f, 0f));
            float foot = art.min.y;
            float midX = art.center.x;
            var material = body != null ? body.sharedMaterial : null;
            if (body != null) DestroyImmediate(body);

            switch (kind)
            {
                case EnemyKind.BeatChaser:
                    body = AddBox(new Vector2(1.25f, 0.78f), new Vector2(midX, foot + 0.39f), material);
                    break;
                case EnemyKind.RangedBeat:
                    body = AddBox(new Vector2(0.48f, 1.55f), new Vector2(midX, foot + 0.78f), material);
                    break;
                case EnemyKind.RangedHalfBeat:
                    body = AddCircle(0.36f, art.center, material);
                    break;
                case EnemyKind.Flyer:
                    body = AddCircle(0.55f, art.center, material);
                    break;
                case EnemyKind.Mole:
                    body = AddBox(new Vector2(1.5f, 0.4f), new Vector2(midX, foot + 0.2f), material);
                    break;
                default:
                    var capsule = gameObject.AddComponent<CapsuleCollider2D>();
                    capsule.direction = CapsuleDirection2D.Vertical;
                    capsule.size = new Vector2(1.2f, 1.9f);
                    capsule.offset = new Vector2(midX, foot + 0.95f);
                    capsule.sharedMaterial = material;
                    body = capsule;
                    break;
            }
        }

        private BoxCollider2D AddBox(Vector2 size, Vector2 offset, PhysicsMaterial2D material)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = size;
            box.offset = offset;
            box.sharedMaterial = material;
            return box;
        }

        private CircleCollider2D AddCircle(float radius, Vector3 center, PhysicsMaterial2D material)
        {
            var circle = gameObject.AddComponent<CircleCollider2D>();
            circle.radius = radius;
            circle.offset = center;
            circle.sharedMaterial = material;
            return circle;
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable()
        {
            All.Remove(this);
            UnbindConductor();
        }

        private void OnDestroy() => UnbindConductor();

        private void UnbindConductor()
        {
            if (!beatBound || conductor == null) return;
            conductor.Beat -= OnBeat;
            beatBound = false;
        }

        public static int CountAlive()
        {
            int count = 0;
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null && !All[i].IsDead) count++;
            return count;
        }

        public bool CatchGlory(ref int nextHealth)
        {
            if ((state != State.Combat && state != State.Stunned) || health == null) return false;
            int floor = GloryFloor;
            var boss = GetComponent<BossActor>();
            if (boss != null)
            {
                if (!boss.RequiresPhaseGlory) return false;
                if (health.Current <= floor || nextHealth <= floor)
                {
                    nextHealth = floor;
                    return true;
                }
                return false;
            }

            if (staggerSpent) return false;
            if (health.Current > floor && nextHealth <= floor)
            {
                nextHealth = floor;
                return true;
            }
            return false;
        }

        public void EnterStagger()
        {
            var boss = GetComponent<BossActor>();
            if ((state != State.Combat && state != State.Stunned)
                || (staggerSpent && (boss == null || !boss.RequiresPhaseGlory))) return;
            state = State.Stagger;
            staggerTimer = staggerSeconds;
            if (sprite != null) sprite.color = staggerColor;
            HoldInPlace();
        }

        public void Stun(float seconds)
        {
            if (state != State.Combat || seconds <= 0f) return;
            state = State.Stunned;
            stunTimer = seconds;
            if (sprite != null) sprite.color = stunnedColor;
            HoldInPlace();
        }

        public void BeginGlory()
        {
            state = State.Glory;
            HoldInPlace();
        }

        public void CancelGlory()
        {
            state = State.Stagger;
            staggerTimer = Mathf.Max(staggerSeconds, 5f);
            transform.rotation = Quaternion.identity;
            if (sprite != null) sprite.color = staggerColor;
            HoldInPlace();
        }

        public void FinishGlory()
        {
            var boss = GetComponent<BossActor>();
            if (boss != null && boss.TryAdvancePhase())
            {
                state = State.Combat;
                transform.rotation = Quaternion.identity;
                if (sprite != null) sprite.color = idleColor;
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            state = State.Dead;
            if (health != null && health.Current > 0)
                health.TakeDamage(health.Current, Vector2.up);
        }

        private void Update()
        {
            if (health != null && health.Current <= 0)
            {
                HoldInPlace();
                return;
            }

            BindConductor();
            if (player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }

            if (state == State.Stunned)
            {
                stunTimer -= Time.deltaTime;
                HoldInPlace();
                if (stunTimer <= 0f)
                {
                    state = State.Combat;
                    transform.rotation = Quaternion.identity;
                    if (sprite != null) sprite.color = idleColor;
                }
            }
            else if (state == State.Stagger)
            {
                staggerTimer -= Time.deltaTime;
                HoldInPlace();
                if (staggerTimer <= 0f)
                {
                    state = State.Combat;
                    transform.rotation = Quaternion.identity;
                    if (sprite != null) sprite.color = idleColor;
                }
            }
            else if (state == State.Glory)
            {
                HoldInPlace();
            }
        }

        /// <summary>공중 적은 중력이 없어 맞은 순간의 상승 속도가 남는다. 그 자리에 고정한다.</summary>
        private void HoldInPlace()
        {
            if (rb == null) return;
            if (kind == EnemyKind.Flyer)
                rb.linearVelocity = Vector2.zero;
            else if (state == State.Glory)
                rb.linearVelocity = new Vector2(0f, Mathf.Min(0f, rb.linearVelocity.y));
            else
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        private void FixedUpdate()
        {
            if (IsDead || state != State.Combat || GamePause.BlocksGameplay || rb == null) return;
            if (kind == EnemyKind.Boss) return;
            if (kind == EnemyKind.BeatChaser || kind == EnemyKind.Flyer)
            {
                if (beatMoveTimer > 0f)
                {
                    beatMoveTimer -= Time.fixedDeltaTime;
                    if (beatMoveTimer <= 0f)
                        rb.linearVelocity = kind == EnemyKind.Flyer
                            ? Vector2.zero
                            : new Vector2(0f, rb.linearVelocity.y);
                }
                return;
            }
            if (kind == EnemyKind.Mole && UpdateMole()) return;
            MoveGroundEnemy();
        }

        private void LateUpdate()
        {
            if (state == State.Stunned)
            {
                float wobble = Mathf.Sin(Time.unscaledTime * 22f) * 5f;
                transform.rotation = Quaternion.Euler(0f, 0f, wobble);
                if (sprite != null) sprite.color = stunnedColor;
            }
            else if (state == State.Stagger || state == State.Glory)
            {
                float wobble = Mathf.Sin(Time.unscaledTime * (state == State.Stagger ? 28f : 18f)) * (state == State.Stagger ? 10f : 6f);
                transform.rotation = Quaternion.Euler(0f, 0f, wobble);
                if (sprite != null) sprite.color = staggerColor;
            }
            else if (sprite != null)
            {
                sprite.color = preparing ? Color.Lerp(idleColor, Color.white, 0.7f) : idleColor;
            }

            UpdateMoleVisuals();
        }

        private void BindConductor()
        {
            var current = RhythmConductor.Instance;
            if (beatBound && conductor == current) return;
            UnbindConductor();
            conductor = current;
            if (conductor == null) return;
            conductor.Beat += OnBeat;
            beatBound = true;
        }

        private void OnBeat(long beat)
        {
            if (state != State.Combat || kind == EnemyKind.Boss || GamePause.BlocksGameplay) return;

            if (kind == EnemyKind.Flyer)
            {
                StepFlyer();
                beatMoveTimer = BeatStepDuration();
            }

            // 짝수 박은 예고, 홀수 박에 공격. 보고 나서 한 박 뒤에 친다.
            bool attackTurn = (beat & 1) == 1;
            if (!attackTurn)
            {
                attackArmed = CanWindUp();
                preparing = attackArmed;
                if (kind == EnemyKind.BeatChaser && !IsPlayerInMeleeRange())
                {
                    MoveGroundEnemy();
                    beatMoveTimer = BeatStepDuration();
                }
                return;
            }

            preparing = false;
            bool strike = attackArmed;
            attackArmed = false;

            switch (kind)
            {
                case EnemyKind.BeatChaser:
                    if (strike && IsPlayerInMeleeRange()) AttackMelee();
                    else if (!IsPlayerInMeleeRange())
                    {
                        MoveGroundEnemy();
                        beatMoveTimer = BeatStepDuration();
                    }
                    break;
                case EnemyKind.Mole:
                    if (strike) AttackMelee();
                    break;
                case EnemyKind.RangedBeat:
                case EnemyKind.RangedHalfBeat:
                case EnemyKind.Flyer:
                    if (strike) FireRanged();
                    break;
            }
        }

        private bool CanWindUp()
        {
            if (player == null) return false;
            if (kind == EnemyKind.BeatChaser || kind == EnemyKind.Mole)
                return (kind != EnemyKind.Mole || moleActive) && IsPlayerInMeleeRange();
            return Vector2.Distance(player.position, transform.position) <= detectionRange;
        }

        private float BeatStepDuration()
        {
            float interval = conductor != null ? conductor.CountdownInterval : 0.5f;
            return Mathf.Clamp(interval * 0.56f, 0.14f, 0.4f);
        }

        private void MoveGroundEnemy()
        {
            if (!IsGrounded())
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                return;
            }

            float dx = player != null ? player.position.x - transform.position.x : 0f;
            float dy = player != null ? player.position.y - transform.position.y : 0f;
            bool canSeePlayer = player != null
                && Mathf.Abs(dx) <= detectionRange
                && Mathf.Abs(dy) <= 1.6f
                && IsPlayerOnSameGround();
            float stopDistance = kind == EnemyKind.RangedBeat || kind == EnemyKind.RangedHalfBeat
                ? 4f
                : attackRange * 0.8f;

            if (canSeePlayer && Mathf.Abs(dx) <= stopDistance)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                Face(dx);
                return;
            }

            int direction = patrolDirection;
            bool chasing = canSeePlayer && Mathf.Abs(dx) > stopDistance;
            if (chasing)
            {
                int towardPlayer = dx > 0f ? 1 : -1;
                if (CanMoveSafely(towardPlayer)) direction = towardPlayer;
                else chasing = false;
            }

            if (!chasing)
            {
                direction = patrolDirection;
                if (!CanMoveSafely(direction))
                {
                    int reverse = -direction;
                    if (!CanMoveSafely(reverse))
                    {
                        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                        return;
                    }

                    direction = reverse;
                    patrolDirection = reverse;
                }
            }

            Face(direction);
            rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        }

        private bool IsPlayerOnSameGround()
        {
            if (player == null || body == null || !body.enabled) return false;
            var playerBody = player.GetComponent<Collider2D>();
            if (playerBody == null || !playerBody.enabled || !IsGrounded()) return false;

            Vector2 enemyFoot = new Vector2(body.bounds.center.x, body.bounds.min.y + 0.16f);
            Vector2 playerFoot = new Vector2(playerBody.bounds.center.x, playerBody.bounds.min.y + 0.16f);
            RaycastHit2D enemyFloor = Physics2D.Raycast(enemyFoot, Vector2.down, 0.36f, groundMask);
            RaycastHit2D playerFloor = Physics2D.Raycast(playerFoot, Vector2.down, 0.36f, groundMask);
            if (enemyFloor.collider == null || playerFloor.collider == null
                || Mathf.Abs(enemyFloor.point.y - playerFloor.point.y) > 0.35f)
                return false;

            float floorY = enemyFloor.point.y;
            float distance = Mathf.Abs(playerFoot.x - enemyFoot.x);
            int samples = Mathf.CeilToInt(distance / 0.45f);
            for (int i = 1; i < samples; i++)
            {
                float x = Mathf.Lerp(enemyFoot.x, playerFoot.x, i / (float)samples);
                Vector2 origin = new Vector2(x, floorY + 0.35f);
                RaycastHit2D floor = Physics2D.Raycast(origin, Vector2.down, 0.7f, groundMask);
                if (floor.collider == null || Mathf.Abs(floor.point.y - floorY) > 0.35f)
                    return false;
            }

            return true;
        }

        private bool CanMoveSafely(int direction)
        {
            if (WallAhead(direction)) return false;
            Vector2 edge = (Vector2)body.bounds.center
                + new Vector2(direction * (body.bounds.extents.x + WallProbe), -body.bounds.extents.y + 0.03f);
            return Physics2D.Raycast(edge, Vector2.down, GroundProbe, groundMask).collider != null;
        }

        private bool IsGrounded()
        {
            if (rb.linearVelocity.y > 0.15f || body == null || !body.enabled) return false;
            Vector2 foot = new Vector2(body.bounds.center.x, body.bounds.min.y + 0.02f);
            return Physics2D.Raycast(foot, Vector2.down, 0.14f, groundMask).collider != null;
        }

        private bool WallAhead(int direction)
        {
            if (body == null || !body.enabled) return false;
            Vector2 origin = body.bounds.center;
            float distance = body.bounds.extents.x + WallProbe;
            return Physics2D.Raycast(origin, Vector2.right * direction, distance, groundMask).collider != null;
        }

        private void StepFlyer()
        {
            if (player != null && Vector2.Distance(player.position, transform.position) <= detectionRange)
            {
                Vector2 delta = (Vector2)player.position + Vector2.up * 1.2f - (Vector2)transform.position;
                rb.linearVelocity = delta.sqrMagnitude > 1.1f * 1.1f
                    ? delta.normalized * moveSpeed
                    : Vector2.zero;
                Face(delta.x);
                return;
            }

            float homeDelta = transform.position.x - patrolHome.x;
            if (Mathf.Abs(homeDelta) >= FlyerPatrolWidth) patrolDirection *= -1;
            float returnY = Mathf.Clamp(patrolHome.y - transform.position.y, -1f, 1f);
            rb.linearVelocity = new Vector2(patrolDirection * moveSpeed * 0.65f, returnY * moveSpeed * 0.35f);
            Face(patrolDirection);
        }

        private bool UpdateMole()
        {
            if (!moleInitialized)
            {
                if (IsGrounded()) BeginMoleBurrow(initial: true);
                return true;
            }

            if (moleBuried)
            {
                moleTimer -= Time.fixedDeltaTime;
                bool pursuingPlayer = IsPlayerOnMoleGround()
                    && Mathf.Abs(player.position.x - moleSurfacePosition.x) <= detectionRange;
                if (pursuingPlayer)
                {
                    float side = player.position.x >= moleSurfacePosition.x ? -1f : 1f;
                    if (Mathf.Abs(player.position.x - moleSurfacePosition.x) < 0.15f)
                        side = -patrolDirection;
                    float offset = Mathf.Clamp(attackRange * 0.65f, 0.55f, 0.9f);
                    moleDestinationPosition = new Vector3(player.position.x + side * offset, moleGroundY, transform.position.z);
                }
                else
                {
                    float patrolLimit = Mathf.Max(2f, detectionRange * 0.5f);
                    if (Mathf.Abs(moleSurfacePosition.x - patrolHome.x) >= patrolLimit)
                        patrolDirection = moleSurfacePosition.x < patrolHome.x ? 1 : -1;
                    moleDestinationPosition = new Vector3(patrolHome.x + patrolDirection * patrolLimit, moleGroundY, transform.position.z);
                }

                moleSurfacePosition = Vector3.MoveTowards(
                    moleSurfacePosition,
                    moleDestinationPosition,
                    Time.fixedDeltaTime * moveSpeed * 1.8f);
                transform.position = moleSurfacePosition - Vector3.up * moleHeight * 0.85f;

                bool reachedDestination = Mathf.Abs(moleSurfacePosition.x - moleDestinationPosition.x) < 0.12f;
                if (moleTimer <= 0f && reachedDestination)
                    BeginMoleEmerge();
                return true;
            }

            if (moleEmerging)
            {
                transform.position = Vector3.MoveTowards(transform.position, moleSurfacePosition, Time.fixedDeltaTime * 5f);
                if ((transform.position - moleSurfacePosition).sqrMagnitude < 0.0025f)
                {
                    transform.position = moleSurfacePosition;
                    moleEmerging = false;
                    moleActive = true;
                    moleTimer = 2.5f;
                    body.enabled = true;
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.gravityScale = 4f;
                }
                return true;
            }

            if (moleActive)
            {
                moleTimer -= Time.fixedDeltaTime;
                if (IsPlayerOnSameGround() && !IsPlayerInMeleeRange())
                    MoveGroundEnemy();
                if (moleTimer <= 0f) BeginMoleBurrow();
                return true;
            }
            return false;
        }

        private void BeginMoleBurrow(bool initial = false)
        {
            if (body == null || rb == null) return;
            moleSurfacePosition = transform.position;
            moleHeight = body.bounds.extents.y;
            var floorHit = Physics2D.Raycast(moleSurfacePosition + Vector3.up * 0.3f, Vector2.down, 2f, groundMask);
            moleGroundY = floorHit.collider != null ? floorHit.point.y : moleSurfacePosition.y;
            moleDestinationPosition = moleSurfacePosition;
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            body.enabled = false;
            transform.position = moleSurfacePosition - Vector3.up * moleHeight * 0.85f;
            if (sprite != null) sprite.enabled = false;
            EnsureMoleVisuals();
            moleInitialized = true;
            moleBuried = true;
            moleEmerging = false;
            moleActive = false;
            moleTimer = initial ? 0.25f : 0.45f;
        }

        private void BeginMoleEmerge()
        {
            moleBuried = false;
            moleEmerging = true;
            rb.linearVelocity = Vector2.zero;
            transform.position = moleSurfacePosition - Vector3.up * moleHeight * 0.85f;
            if (sprite != null) sprite.enabled = true;
        }

        private bool IsPlayerOnMoleGround()
        {
            if (player == null) return false;
            var playerBody = player.GetComponent<Collider2D>();
            if (playerBody == null || !playerBody.enabled) return false;

            Vector2 playerFoot = new Vector2(playerBody.bounds.center.x, playerBody.bounds.min.y + 0.2f);
            RaycastHit2D playerFloor = Physics2D.Raycast(playerFoot, Vector2.down, 0.55f, groundMask);
            if (playerFloor.collider == null || Mathf.Abs(playerFloor.point.y - moleGroundY) > 0.35f)
                return false;

            float distance = Mathf.Abs(playerFoot.x - moleSurfacePosition.x);
            int samples = Mathf.CeilToInt(distance / 0.45f);
            for (int i = 1; i < samples; i++)
            {
                float x = Mathf.Lerp(moleSurfacePosition.x, playerFoot.x, i / (float)samples);
                var floor = Physics2D.Raycast(new Vector2(x, moleGroundY + 0.35f), Vector2.down, 0.7f, groundMask);
                if (floor.collider == null || Mathf.Abs(floor.point.y - moleGroundY) > 0.35f)
                    return false;
            }

            return true;
        }

        private void EnsureMoleVisuals()
        {
            if (sprite == null || sprite.sprite == null) return;
            if (moleTrail == null)
            {
                var trailObject = new GameObject("MoleBurrowTrail");
                trailObject.transform.SetParent(transform, false);
                moleTrail = trailObject.AddComponent<SpriteRenderer>();
                moleTrail.sprite = sprite.sprite;
                moleTrail.sortingOrder = sprite.sortingOrder - 1;
                moleTrail.color = new Color(0.62f, 0.35f, 0.16f, 0.82f);
                trailObject.transform.localScale = new Vector3(0.55f, 0.24f, 1f);
            }

            if (moleDestinationMarker == null)
            {
                var markerObject = new GameObject("MoleEmergenceMarker");
                markerObject.transform.SetParent(transform, false);
                moleDestinationMarker = markerObject.AddComponent<SpriteRenderer>();
                moleDestinationMarker.sprite = sprite.sprite;
                moleDestinationMarker.sortingOrder = sprite.sortingOrder - 1;
                moleDestinationMarker.color = new Color(1f, 0.72f, 0.2f, 0.5f);
                markerObject.transform.localScale = Vector3.one * 0.65f;
            }
        }

        private void UpdateMoleVisuals()
        {
            bool show = kind == EnemyKind.Mole && moleBuried;
            if (moleTrail != null)
            {
                moleTrail.enabled = show;
                if (show) moleTrail.transform.position = moleSurfacePosition + Vector3.up * 0.04f;
            }
            if (moleDestinationMarker != null)
            {
                moleDestinationMarker.enabled = show;
                if (show)
                {
                    moleDestinationMarker.transform.position = moleDestinationPosition + Vector3.up * 0.12f;
                    float pulse = 0.45f + Mathf.PingPong(Time.time * 1.8f, 0.35f);
                    var color = moleDestinationMarker.color;
                    color.a = pulse;
                    moleDestinationMarker.color = color;
                }
            }
        }

        private void AttackMelee()
        {
            if (kind == EnemyKind.Mole && !moleActive) return;
            if (!IsPlayerInMeleeRange()) return;
            Gstarr.Game.GameAudio.Play(Gstarr.Game.GameAudio.Id.EnemySwing);
            GetComponent<Gstarr.Game.SpriteMotion>()?.AttackPose();
            if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            float dx = player.position.x - transform.position.x;
            Face(dx);
            Vector2 center = (Vector2)transform.position + new Vector2(Facing() * 0.65f, 0.2f);
            var hits = Physics2D.OverlapBoxAll(center, new Vector2(1.2f, 1.2f), 0f);
            for (int i = 0; i < hits.Length; i++)
            {
                var target = hits[i].GetComponentInParent<Gstarr.Player.PlayerController>();
                if (target == null) continue;
                target.GetComponent<Health>()?.TakeDamage(attackDamage, new Vector2(Facing(), 0.4f));
                break;
            }
        }

        private bool IsPlayerInMeleeRange()
        {
            return player != null
                   && Mathf.Abs(player.position.x - transform.position.x) <= attackRange
                   && Mathf.Abs(player.position.y - transform.position.y) <= 1.6f;
        }

        private void FireRanged()
        {
            if (player == null || Vector2.Distance(player.position, transform.position) > detectionRange) return;
            Vector2 origin = transform.position + new Vector3(Facing() * 0.45f, 0.15f, 0f);
            Vector2 direction = ((Vector2)player.position - origin).normalized;
            RaycastHit2D wall = Physics2D.Raycast(origin, direction, detectionRange, groundMask);
            if (wall.collider != null && Vector2.Distance(origin, wall.point) < Vector2.Distance(origin, player.position)) return;

            Gstarr.Game.GameAudio.Play(Gstarr.Game.GameAudio.Id.EnemyShot);
            GetComponent<Gstarr.Game.SpriteMotion>()?.AttackPose();
            var shot = new GameObject("EnemyProjectile");
            shot.transform.position = origin;
            shot.transform.localScale = Vector3.one * 0.2f;
            var shotSprite = shot.AddComponent<SpriteRenderer>();
            shotSprite.sprite = sprite != null ? sprite.sprite : Art;
            shotSprite.color = kind == EnemyKind.RangedHalfBeat ? new Color(1f, 0.45f, 1f) : new Color(1f, 0.65f, 0.25f);
            shotSprite.sortingOrder = 6;
            var projectile = shot.AddComponent<Projectile>();
            int playerLayer = player.gameObject.layer;
            projectile.Launch(direction * 9f, attackDamage, detectionRange, 1 << playerLayer, gameObject);
        }

        private void Face(float dx)
        {
            if (Mathf.Abs(dx) < 0.05f) return;
            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (dx >= 0f ? 1f : -1f);
            transform.localScale = scale;
        }

        private int Facing() => transform.localScale.x >= 0f ? 1 : -1;
    }
}
