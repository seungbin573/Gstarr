using System.Collections.Generic;
using Gstarr.Game;
using Gstarr.Rhythm;
using Gstarr.World;
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
        Boss,
        Hopper
    }

    [RequireComponent(typeof(Health))]
    public class EnemyActor : MonoBehaviour
    {
        private enum State { Combat, Stunned, Stagger, Glory, Dead }

        private static readonly List<EnemyActor> All = new List<EnemyActor>();

        [SerializeField] private EnemyKind kind;
        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField] private float attackRange = 1.35f;
        [SerializeField] private float detectionRange = 10f;
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
        private bool hasLeash;
        private float leashMinX;
        private float leashMaxX;
        private bool preparing;
        private bool attackArmed;
        private float beatMoveTimer;
        private bool lunging;
        private bool lungeHit;
        private float lungeTimer;
        private bool hopping;
        private bool hopHit;
        private bool hopLeftGround;
        private float hopTimer;
        private bool jumping;
        private bool jumpHit;
        private bool jumpLeftGround;
        private float jumpTimer;
        private bool waitingForPlayer;
        private bool waitAnchorReady;
        private Vector2 waitOrigin;

        private bool moleInitialized;
        private bool moleBuried;
        private bool moleEmerging;
        private bool moleActive;
        private Vector3 moleSurfacePosition;
        private Vector3 moleDestinationPosition;
        private float moleSafeX;
        private float moleHeight;
        private float moleGroundY;
        private float moleTimer;
        private SpriteRenderer moleTrail;
        private SpriteRenderer moleDestinationMarker;

        private const float WallProbe = 0.22f;
        private const float GroundProbe = 1.15f;
        private const float FlyerPatrolWidth = 3f;
        private const float ChaseVertical = 8f;
        private const float MaxGapJump = 4.2f;
        private const float GapJumpVelocity = 13f;

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
            if (enemyKind == EnemyKind.Boss && assets != null && assets.bossSprite != null)
                return assets.bossSprite;
            if (enemyKind == EnemyKind.Mole && assets != null && assets.moleSprite != null)
                return assets.moleSprite;
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
            EnemyKind.RangedBeat => 4,
            EnemyKind.RangedHalfBeat => 4,
            EnemyKind.Flyer => 4,
            EnemyKind.Mole => 7,
            EnemyKind.Boss => 28,
            EnemyKind.Hopper => 4,
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

        public bool HasLeash => hasLeash;

        /// <summary>문이 열린 뒤, 플레이어가 움직이기 전에는 제자리에 둔다.</summary>
        public void WaitForPlayerMove() => waitingForPlayer = true;

        public bool HoldsStill
        {
            get
            {
                if (!waitingForPlayer || state != State.Combat) return false;
                if (player == null)
                {
                    var tagged = GameObject.FindGameObjectWithTag("Player");
                    if (tagged != null) player = tagged.transform;
                }
                if (player == null) return true;
                if (!waitAnchorReady)
                {
                    waitOrigin = player.position;
                    waitAnchorReady = true;
                    return true;
                }
                if (((Vector2)player.position - waitOrigin).sqrMagnitude > 0.16f)
                {
                    waitingForPlayer = false;
                    return false;
                }
                return true;
            }
        }

        public Vector2 AimPoint => body != null && body.enabled ? body.bounds.center : (Vector2)transform.position;

        /// <summary>이 방 밖으로 나가지 못하게 한다. 보스는 자기 경기장 경계를 쓴다.</summary>
        public void SetLeash(float minX, float maxX)
        {
            if (kind == EnemyKind.Boss || hasLeash) return;
            leashMinX = minX;
            leashMaxX = maxX;
            hasLeash = true;
        }

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
                EnemyKind.BeatChaser => 5.1f,
                EnemyKind.RangedBeat => 4.4f,
                EnemyKind.RangedHalfBeat => 4.6f,
                EnemyKind.Flyer => 5.6f,
                EnemyKind.Mole => 4.4f,
                EnemyKind.Hopper => 0f,
                _ => moveSpeed
            };
        }

        private void ApplyKind()
        {
            if (sprite == null) sprite = GetComponent<SpriteRenderer>();
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (body == null) body = GetComponent<Collider2D>();

            idleColor = kind == EnemyKind.Hopper ? new Color(0.45f, 0.95f, 0.4f) : Color.white;
            if (sprite != null) sprite.color = idleColor;
            if (rb != null) rb.gravityScale = kind == EnemyKind.Flyer ? 0f : 4f;
            if (kind == EnemyKind.Flyer && rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
            }
            ApplyHitbox();
        }

        private const float SharedWidth = 0.9f;
        private const float SharedHeight = 1.9f;

        /// <summary>보스 외 적은 모두 0.9 x 1.9. 발밑은 스프라이트 바닥에 맞춘다.</summary>
        private void ApplyHitbox()
        {
            Bounds art = sprite != null && sprite.sprite != null
                ? sprite.sprite.bounds
                : new Bounds(new Vector3(0f, 0.6f, 0f), new Vector3(1f, 1.2f, 0f));
            float foot = art.min.y;
            float midX = art.center.x;
            var material = body != null ? body.sharedMaterial : null;
            if (body != null) DestroyImmediate(body);

            if (kind == EnemyKind.Boss)
            {
                var capsule = gameObject.AddComponent<CapsuleCollider2D>();
                capsule.direction = CapsuleDirection2D.Vertical;
                capsule.size = new Vector2(1.2f, 1.9f);
                capsule.offset = new Vector2(midX, foot + 0.95f);
                capsule.sharedMaterial = material;
                body = capsule;
                return;
            }

            body = AddBox(new Vector2(SharedWidth, SharedHeight), new Vector2(midX, foot + SharedHeight * 0.5f), material);
        }

        private BoxCollider2D AddBox(Vector2 size, Vector2 offset, PhysicsMaterial2D material)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = size;
            box.offset = offset;
            box.sharedMaterial = material;
            return box;
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
            conductor.HalfBeat -= OnHalfBeat;
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
            if (IsDead || state != State.Combat || GamePause.BlocksGameplay || rb == null)
            {
                ClampLeash();
                return;
            }
            if (HoldsStill)
            {
                HoldInPlace();
                ClampLeash();
                return;
            }
            if (kind == EnemyKind.Boss) return;
            if (jumping)
            {
                StepGapJump();
                ClampLeash();
                return;
            }
            if (kind == EnemyKind.BeatChaser || kind == EnemyKind.Flyer)
            {
                if (kind == EnemyKind.BeatChaser && lunging)
                {
                    StepLunge();
                    ClampLeash();
                    return;
                }
                if (beatMoveTimer > 0f)
                {
                    beatMoveTimer -= Time.fixedDeltaTime;
                    if (kind == EnemyKind.BeatChaser && IsGrounded())
                    {
                        float slide = rb.linearVelocity.x;
                        int slideDir = slide > 0.1f ? 1 : slide < -0.1f ? -1 : 0;
                        if (slideDir != 0 && PitAhead(slideDir))
                        {
                            beatMoveTimer = 0f;
                            if (!TryJumpGap(slideDir))
                                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                            ClampLeash();
                            return;
                        }
                    }
                    if (beatMoveTimer <= 0f)
                        rb.linearVelocity = kind == EnemyKind.Flyer
                            ? Vector2.zero
                            : new Vector2(0f, rb.linearVelocity.y);
                }
                ClampLeash();
                return;
            }
            if (kind == EnemyKind.Mole && UpdateMole())
            {
                ClampLeash();
                return;
            }
            if (kind == EnemyKind.Hopper)
            {
                if (hopping) StepHop();
                else rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                ClampLeash();
                return;
            }
            MoveGroundEnemy();
            ClampLeash();
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
            if (kind == EnemyKind.Mole && moleInitialized) KeepMoleBehindDoors();
            ClampLeash();
        }

        private void KeepMoleBehindDoors()
        {
            float x = transform.position.x;
            float limited = ArenaDoorSpawner.LimitCrossing(moleSafeX, x, 0.42f);
            if (Mathf.Abs(limited - x) > 0.01f)
            {
                var position = transform.position;
                position.x = limited;
                transform.position = position;
                moleSurfacePosition.x = limited;
                moleDestinationPosition.x = ArenaDoorSpawner.LimitCrossing(limited, moleDestinationPosition.x, 0.42f);
                if (rb != null)
                {
                    var velocity = rb.linearVelocity;
                    velocity.x = 0f;
                    rb.linearVelocity = velocity;
                }
                moleSafeX = limited;
                return;
            }

            moleSafeX = x;
        }

        private void BindConductor()
        {
            var current = RhythmConductor.Instance;
            if (beatBound && conductor == current) return;
            UnbindConductor();
            conductor = current;
            if (conductor == null) return;
            conductor.Beat += OnBeat;
            conductor.HalfBeat += OnHalfBeat;
            beatBound = true;
        }

        private void OnBeat(long beat)
        {
            if (state != State.Combat || kind == EnemyKind.Boss || GamePause.BlocksGameplay || HoldsStill || jumping) return;

            if (kind == EnemyKind.Flyer)
            {
                StepFlyer();
                beatMoveTimer = BeatStepDuration();
            }

            if (kind == EnemyKind.RangedHalfBeat) return;

            // 짝수 박은 예고, 홀수 박에 공격. 보고 나서 한 박 뒤에 친다.
            bool attackTurn = (beat & 1) == 1;
            if (!attackTurn)
            {
                attackArmed = CanWindUp();
                preparing = attackArmed;
                if (kind == EnemyKind.BeatChaser && !IsPlayerInMeleeRange())
                {
                    MoveGroundEnemy();
                    if (!jumping) beatMoveTimer = BeatStepDuration();
                }
                return;
            }

            preparing = false;
            bool strike = attackArmed;
            attackArmed = false;

            switch (kind)
            {
                case EnemyKind.BeatChaser:
                    if (strike) Lunge();
                    else if (!IsPlayerInMeleeRange() && !jumping)
                    {
                        MoveGroundEnemy();
                        beatMoveTimer = BeatStepDuration();
                    }
                    break;
                case EnemyKind.Mole:
                    if (strike) AttackMelee();
                    break;
                case EnemyKind.RangedBeat:
                    if (strike) FireLane();
                    break;
                case EnemyKind.Flyer:
                    if (strike) FireFromAbove();
                    break;
                case EnemyKind.Hopper:
                    if (strike) BeginHop();
                    break;
            }
        }

        private void OnHalfBeat(long index)
        {
            if (state != State.Combat || kind != EnemyKind.RangedHalfBeat || GamePause.BlocksGameplay || HoldsStill || jumping) return;
            long phase = index & 3;
            if (phase == 0)
            {
                attackArmed = CanWindUp();
                preparing = attackArmed;
                return;
            }

            if (phase != 1)
            {
                preparing = false;
                attackArmed = false;
                return;
            }

            preparing = false;
            if (!attackArmed) return;
            attackArmed = false;
            FireSnap();
        }

        private bool IsShooter =>
            kind == EnemyKind.RangedBeat || kind == EnemyKind.RangedHalfBeat || kind == EnemyKind.Flyer;

        private bool CanSeePlayer()
        {
            if (player == null) return false;
            if (ArenaDoorSpawner.SeparatedByDoor(transform.position.x, player.position.x)) return false;
            float range = IsShooter ? 8.2f : detectionRange;
            float dx = Mathf.Abs(player.position.x - transform.position.x);
            float dy = Mathf.Abs(player.position.y - transform.position.y);
            return dx <= range && dy <= ChaseVertical;
        }

        /// <summary>같은 구간의 원거리 적 중 가장 가까운 한 마리만 쏜다.</summary>
        private bool IsLeadShooter()
        {
            if (!IsShooter || player == null) return false;
            if (!CanSeePlayer()) return false;
            float mine = ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude;
            for (int i = 0; i < All.Count; i++)
            {
                var other = All[i];
                if (other == null || other == this || other.IsDead || !other.IsShooter) continue;
                if (ArenaDoorSpawner.SeparatedByDoor(other.transform.position.x, player.position.x)) continue;
                float dist = ((Vector2)player.position - (Vector2)other.transform.position).sqrMagnitude;
                if (dist < mine - 0.05f) return false;
                if (dist <= mine + 0.05f && other.GetInstanceID() < GetInstanceID()) return false;
            }
            return true;
        }

        private bool CanWindUp()
        {
            if (player == null) return false;
            if (kind == EnemyKind.BeatChaser)
                return Mathf.Abs(player.position.x - transform.position.x) <= detectionRange * 0.9f
                    && Mathf.Abs(player.position.y - transform.position.y) <= ChaseVertical
                    && !ArenaDoorSpawner.SeparatedByDoor(transform.position.x, player.position.x);
            if (kind == EnemyKind.Hopper)
                return Mathf.Abs(player.position.x - transform.position.x) <= 8.5f
                    && Mathf.Abs(player.position.y - transform.position.y) <= ChaseVertical
                    && !ArenaDoorSpawner.SeparatedByDoor(transform.position.x, player.position.x);
            if (kind == EnemyKind.Mole)
                return moleActive && IsPlayerInMeleeRange();
            if (!IsLeadShooter()) return false;
            return CanSeePlayer();
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
            bool canSeePlayer = player != null
                && Mathf.Abs(dx) <= detectionRange
                && Mathf.Abs(player.position.y - transform.position.y) <= ChaseVertical
                && !ArenaDoorSpawner.SeparatedByDoor(transform.position.x, player.position.x);
            float stopDistance = kind == EnemyKind.RangedBeat || kind == EnemyKind.RangedHalfBeat
                ? 3.2f
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
                else if (PitAhead(towardPlayer))
                {
                    Face(dx);
                    if (TryJumpGap(towardPlayer)) return;
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    return;
                }
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

        private bool CanMoveSafely(int direction)
        {
            if (!RoomAhead(direction)) return false;
            if (WallAhead(direction)) return false;
            return HasFooting(FrontEdge(direction));
        }

        private bool PitAhead(int direction)
        {
            if (!RoomAhead(direction) || WallAhead(direction) || body == null || !body.enabled) return false;
            return !HasFooting(FrontEdge(direction));
        }

        private Vector2 FrontEdge(int direction)
        {
            return (Vector2)body.bounds.center
                + new Vector2(direction * (body.bounds.extents.x + WallProbe), -body.bounds.extents.y + 0.03f);
        }

        private float CurrentFloorY()
        {
            if (body == null) return transform.position.y;
            Vector2 foot = new Vector2(body.bounds.center.x, body.bounds.min.y + 0.08f);
            RaycastHit2D hit = Physics2D.Raycast(foot, Vector2.down, 0.45f, groundMask);
            return hit.collider != null ? hit.point.y : body.bounds.min.y;
        }

        private bool HasFooting(Vector2 origin)
        {
            return Physics2D.Raycast(origin, Vector2.down, GroundProbe, groundMask).collider != null;
        }

        private bool WillFall(int direction, float distance)
        {
            if (body == null || !body.enabled || direction == 0) return false;
            float floorY = CurrentFloorY();
            float x0 = FrontEdge(direction).x;
            for (float d = 0.3f; d <= distance; d += 0.35f)
            {
                float x = x0 + direction * d;
                RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, floorY + 0.45f), Vector2.down, GroundProbe + 0.25f, groundMask);
                if (hit.collider == null) return true;
            }

            return false;
        }

        /// <summary>같은 높이의 맞은편 발이 점프로 닿으면 그 구멍은 넘는다.</summary>
        private bool TryJumpGap(int direction)
        {
            if (jumping || rb == null || body == null || !body.enabled || direction == 0 || player == null) return false;
            if (!IsGrounded()) return false;

            float floorY = CurrentFloorY();
            Vector2 edge = FrontEdge(direction);
            float landing = 0f;
            bool seenHole = false;
            bool found = false;
            for (float d = 0.3f; d <= MaxGapJump + 1.6f; d += 0.25f)
            {
                float x = edge.x + direction * d;
                if (hasLeash)
                {
                    float pad = body.bounds.extents.x;
                    if (x < leashMinX + pad || x > leashMaxX - pad) break;
                }

                RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, floorY + 0.55f), Vector2.down, 1.3f, groundMask);
                bool solid = hit.collider != null && Mathf.Abs(hit.point.y - floorY) <= 0.45f;
                if (!solid)
                {
                    seenHole = true;
                    continue;
                }

                if (!seenHole) continue;
                landing = x;
                found = true;
                break;
            }

            if (!seenHole || !found) return false;
            if (Mathf.Abs(landing - edge.x) > MaxGapJump + 1.4f) return false;
            float playerX = player.position.x;
            bool farSide = direction > 0 ? playerX >= landing - 0.75f : playerX <= landing + 0.75f;
            bool overHole = direction > 0
                ? playerX > transform.position.x + 0.25f && playerX < landing
                : playerX < transform.position.x - 0.25f && playerX > landing;
            if (!farSide && !overHole) return false;

            float travel = Mathf.Abs(landing - body.bounds.center.x) + body.bounds.extents.x;
            float gravity = Mathf.Abs(Physics2D.gravity.y) * Mathf.Max(0.01f, rb.gravityScale);
            float air = 2f * GapJumpVelocity / gravity;
            float vx = Mathf.Clamp(travel / Mathf.Max(0.2f, air), 6f, 12f);
            jumping = true;
            jumpHit = false;
            jumpLeftGround = false;
            jumpTimer = air + 0.2f;
            beatMoveTimer = 0f;
            lunging = false;
            Face(direction);
            rb.linearVelocity = new Vector2(direction * vx, GapJumpVelocity);
            Gstarr.Game.GameAudio.Play(Gstarr.Game.GameAudio.Id.Dash, 0.4f);
            return true;
        }

        private void StepGapJump()
        {
            jumpTimer -= Time.fixedDeltaTime;
            bool onGround = IsGrounded();
            if (rb.linearVelocity.y <= 0.05f && !onGround) jumpLeftGround = true;
            if (!jumpHit && IsPlayerInMeleeRange(2.3f))
            {
                jumpHit = true;
                AttackMelee(2.3f, true);
            }

            bool landed = jumpLeftGround && onGround;
            bool fellBack = onGround && rb.linearVelocity.y <= 0.05f && !jumpLeftGround;
            if (landed || fellBack || jumpTimer <= -0.35f)
            {
                jumping = false;
                if (onGround) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }

        private bool RoomAhead(int direction)
        {
            if (!hasLeash || direction == 0) return true;
            float pad = body != null && body.enabled ? body.bounds.extents.x : 0.45f;
            float x = transform.position.x + direction * (pad + 0.2f);
            return x >= leashMinX + pad && x <= leashMaxX - pad;
        }

        private void ClampLeash()
        {
            if (!hasLeash || kind == EnemyKind.Boss || rb == null) return;
            float pad = body != null && body.enabled ? body.bounds.extents.x : 0.45f;
            float min = leashMinX + pad;
            float max = leashMaxX - pad;
            if (max <= min)
            {
                float mid = (leashMinX + leashMaxX) * 0.5f;
                min = mid;
                max = mid;
            }

            var pos = rb.position;
            float x = pos.x;
            if (x < min || x > max)
            {
                pos.x = Mathf.Clamp(x, min, max);
                rb.position = pos;
                transform.position = new Vector3(pos.x, pos.y, transform.position.z);
            }

            var velocity = rb.linearVelocity;
            if ((rb.position.x <= min + 0.02f && velocity.x < 0f) || (rb.position.x >= max - 0.02f && velocity.x > 0f))
            {
                velocity.x = 0f;
                rb.linearVelocity = velocity;
            }
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
            bool seePlayer = player != null
                && PlayerInsideLeash()
                && Mathf.Abs(player.position.x - transform.position.x) <= detectionRange
                && Mathf.Abs(player.position.y - transform.position.y) <= ChaseVertical
                && !ArenaDoorSpawner.SeparatedByDoor(transform.position.x, player.position.x);
            if (seePlayer)
            {
                Vector2 delta = (Vector2)player.position + Vector2.up * 1.2f - (Vector2)transform.position;
                if (!RoomAhead(delta.x >= 0f ? 1 : -1)) delta.x = 0f;
                rb.linearVelocity = delta.sqrMagnitude > 1.1f * 1.1f
                    ? delta.normalized * moveSpeed
                    : Vector2.zero;
                Face(delta.x);
                return;
            }

            float homeDelta = transform.position.x - patrolHome.x;
            if (Mathf.Abs(homeDelta) >= FlyerPatrolWidth || !RoomAhead(patrolDirection)) patrolDirection *= -1;
            if (!RoomAhead(patrolDirection)) patrolDirection *= -1;
            float returnY = Mathf.Clamp(patrolHome.y - transform.position.y, -1f, 1f);
            rb.linearVelocity = new Vector2(patrolDirection * moveSpeed * 0.65f, returnY * moveSpeed * 0.35f);
            Face(patrolDirection);
        }

        private bool PlayerInsideLeash()
        {
            if (!hasLeash || player == null) return true;
            float x = player.position.x;
            return x >= leashMinX && x <= leashMaxX;
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

                moleDestinationPosition.x = ClampAcrossFloor(
                    moleSurfacePosition.x, moleDestinationPosition.x, moleGroundY);
                moleDestinationPosition.x = ArenaDoorSpawner.LimitCrossing(
                    moleSurfacePosition.x, moleDestinationPosition.x, 0.42f);
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
                    moleTimer = 1.9f;
                    body.enabled = true;
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.gravityScale = 4f;
                    ShockOnEmerge();
                }
                return true;
            }

            if (moleActive)
            {
                moleTimer -= Time.fixedDeltaTime;
                if (!IsPlayerInMeleeRange()
                    && player != null
                    && Mathf.Abs(player.position.x - transform.position.x) <= detectionRange
                    && Mathf.Abs(player.position.y - transform.position.y) <= ChaseVertical
                    && !ArenaDoorSpawner.SeparatedByDoor(transform.position.x, player.position.x))
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
            moleSafeX = transform.position.x;
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
            RaycastHit2D playerFloor = Physics2D.Raycast(playerFoot, Vector2.down, ChaseVertical, groundMask);
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

        private void Lunge()
        {
            if (player == null || rb == null) return;
            float dx = player.position.x - transform.position.x;
            Face(dx);
            if (IsPlayerInMeleeRange())
            {
                AttackMelee();
                return;
            }

            int dir = dx >= 0f ? 1 : -1;
            if (Mathf.Abs(dx) > detectionRange || Mathf.Abs(player.position.y - transform.position.y) > ChaseVertical || !RoomAhead(dir))
            {
                MoveGroundEnemy();
                if (!jumping) beatMoveTimer = BeatStepDuration();
                return;
            }

            if (PitAhead(dir) || WillFall(dir, moveSpeed * 3.1f * 0.32f))
            {
                if (!TryJumpGap(dir))
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                return;
            }

            lunging = true;
            lungeHit = false;
            lungeTimer = 0.32f;
            rb.linearVelocity = new Vector2(Mathf.Sign(dx) * moveSpeed * 3.1f, rb.linearVelocity.y);
            Gstarr.Game.GameAudio.Play(Gstarr.Game.GameAudio.Id.Dash, 0.7f);
        }

        private void BeginHop()
        {
            if (player == null || rb == null) return;
            float dx = player.position.x - transform.position.x;
            Face(dx);
            int dir = Mathf.Abs(dx) < 0.15f ? Facing() : (dx > 0f ? 1 : -1);
            if (TryJumpGap(dir) || WillFall(dir, 3.2f)) return;
            hopping = true;
            hopHit = false;
            hopLeftGround = false;
            hopTimer = 0.7f;
            rb.linearVelocity = new Vector2(dir * 7f, 8.2f);
            Gstarr.Game.GameAudio.Play(Gstarr.Game.GameAudio.Id.Dash, 0.45f);
        }

        private void StepHop()
        {
            hopTimer -= Time.fixedDeltaTime;
            if (!IsGrounded()) hopLeftGround = true;
            if (!hopHit && IsPlayerInMeleeRange())
            {
                hopHit = true;
                AttackMelee();
            }

            bool landed = hopLeftGround && IsGrounded();
            if (landed || (hopTimer <= 0f && IsGrounded()))
            {
                hopping = false;
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }

        private void StepLunge()
        {
            lungeTimer -= Time.fixedDeltaTime;
            int lungeDir = rb.linearVelocity.x >= 0f ? 1 : -1;
            if (IsGrounded() && PitAhead(lungeDir))
            {
                lunging = false;
                if (!TryJumpGap(lungeDir))
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                return;
            }

            if (!lungeHit && IsPlayerInMeleeRange())
            {
                lungeHit = true;
                AttackMelee();
            }

            if (lungeTimer > 0f && RoomAhead(rb.linearVelocity.x >= 0f ? 1 : -1)) return;
            lunging = false;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        private void ShockOnEmerge()
        {
            if (player == null) return;
            if (Mathf.Abs(player.position.x - moleSurfacePosition.x) > 1.15f) return;
            if (Mathf.Abs(player.position.y - moleSurfacePosition.y) > 1.4f) return;
            Gstarr.Game.GameAudio.Play(Gstarr.Game.GameAudio.Id.EnemySwing, 0.8f);
            player.GetComponentInParent<Health>()?.TakeDamage(attackDamage, new Vector2(Mathf.Sign(player.position.x - moleSurfacePosition.x), 0.8f));
        }

        private void AttackMelee(float verticalReach = 1.6f, bool keepMoving = false)
        {
            if (kind == EnemyKind.Mole && !moleActive) return;
            if (!IsPlayerInMeleeRange(verticalReach)) return;
            Gstarr.Game.GameAudio.Play(Gstarr.Game.GameAudio.Id.EnemySwing);
            GetComponent<Gstarr.Game.SpriteMotion>()?.AttackPose();
            if (rb != null && !keepMoving) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            float dx = player.position.x - transform.position.x;
            Face(dx);
            float height = Mathf.Max(1.2f, verticalReach);
            Vector2 center = (Vector2)transform.position + new Vector2(Facing() * 0.65f, 0.2f + (height - 1.2f) * 0.25f);
            var hits = Physics2D.OverlapBoxAll(center, new Vector2(1.4f, height), 0f);
            for (int i = 0; i < hits.Length; i++)
            {
                var target = hits[i].GetComponentInParent<Gstarr.Player.PlayerController>();
                if (target == null) continue;
                target.GetComponent<Health>()?.TakeDamage(attackDamage, new Vector2(Facing(), 0.4f));
                break;
            }
        }

        private bool IsPlayerInMeleeRange(float verticalReach = 1.6f)
        {
            return player != null
                   && Mathf.Abs(player.position.x - transform.position.x) <= attackRange
                   && Mathf.Abs(player.position.y - transform.position.y) <= verticalReach;
        }

        private float ClampAcrossFloor(float fromX, float toX, float floorY)
        {
            float dir = Mathf.Sign(toX - fromX);
            if (Mathf.Abs(dir) < 0.5f) return fromX;
            float x = fromX;
            for (int guard = 0; guard < 80 && Mathf.Abs(toX - x) > 0.3f; guard++)
            {
                float next = x + dir * 0.3f;
                if ((dir > 0f && next > toX) || (dir < 0f && next < toX)) next = toX;
                RaycastHit2D hit = Physics2D.Raycast(new Vector2(next, floorY + 0.35f), Vector2.down, 0.8f, groundMask);
                if (hit.collider == null || Mathf.Abs(hit.point.y - floorY) > 0.4f) return x;
                x = next;
            }

            return toX;
        }

        private void FireLane()
        {
            if (!IsLeadShooter()) return;
            Face(player.position.x - transform.position.x);
            Vector2 origin = (Vector2)transform.position + new Vector2(Facing() * 0.55f, 0.35f);
            LaunchShot(origin, new Vector2(Facing(), 0f), 10.5f, new Color(1f, 0.45f, 0.15f), 0.28f);
        }

        private void FireSnap()
        {
            if (!IsLeadShooter()) return;
            Vector2 origin = (Vector2)transform.position + new Vector2(Facing() * 0.4f, 0.55f);
            Vector2 aim = ((Vector2)player.position + Vector2.up * 0.35f - origin);
            LaunchShot(origin, aim, 11f, new Color(0.55f, 0.95f, 1f), 0.14f);
        }

        private void FireFromAbove()
        {
            if (!IsLeadShooter()) return;
            Vector2 origin = (Vector2)transform.position + Vector2.down * 0.2f;
            Vector2 aim = ((Vector2)player.position - origin);
            LaunchShot(origin, aim, 8f, new Color(0.75f, 0.85f, 1f), 0.18f);
        }

        private void LaunchShot(Vector2 origin, Vector2 direction, float speed, Color color, float scale, bool audible = true)
        {
            if (player != null && ArenaDoorSpawner.SeparatedByDoor(origin.x, player.position.x)) return;
            if (direction.sqrMagnitude < 0.01f) direction = new Vector2(Facing(), 0f);
            direction.Normalize();
            float travel = ArenaDoorSpawner.RangeUntilDoor(origin.x, direction.x, detectionRange);
            if (travel < 0.35f) return;
            RaycastHit2D wall = Physics2D.Raycast(origin, direction, travel, groundMask);
            if (wall.collider != null && player != null && Vector2.Distance(origin, wall.point) < Vector2.Distance(origin, player.position) - 0.2f)
                return;

            if (audible) Gstarr.Game.GameAudio.Play(Gstarr.Game.GameAudio.Id.EnemyShot);
            GetComponent<Gstarr.Game.SpriteMotion>()?.AttackPose();
            var shot = new GameObject("EnemyProjectile");
            shot.transform.position = origin;
            shot.transform.localScale = Vector3.one * scale;
            var shotSprite = shot.AddComponent<SpriteRenderer>();
            shotSprite.sprite = sprite != null ? sprite.sprite : Art;
            shotSprite.color = color;
            shotSprite.sortingOrder = 6;
            var projectile = shot.AddComponent<Projectile>();
            int playerLayer = player != null ? player.gameObject.layer : 0;
            projectile.Launch(direction * speed, attackDamage, travel, 1 << playerLayer, gameObject);
        }

        private void FireRanged()
        {
            if (!IsLeadShooter()) return;
            Vector2 origin = transform.position + new Vector3(Facing() * 0.45f, 0.15f, 0f);
            Vector2 direction = (Vector2)player.position - origin;
            Color color = kind == EnemyKind.RangedHalfBeat ? new Color(1f, 0.45f, 1f) : new Color(1f, 0.65f, 0.25f);
            LaunchShot(origin, direction, 9f, color, 0.2f);
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
