using System.Collections.Generic;
using Gstarr.Game;
using Gstarr.Player;
using UnityEngine;

namespace Gstarr.Combat
{
    /// <summary>
    /// 제자리에 머물며 예고 후 한 가지 기술만 쓴다.
    /// 마지막 보스 1페이즈: 고정 한 발, 몸통 돌진, 바닥 칼, 지휘봉.
    /// 마지막 보스 2페이즈: 빠른 돌진, 두꺼운 바닥 칼, 세 갈래, 빈칸 회전.
    /// 중간보스 1페이즈: 바닥 찍기, 양옆 탄, 한쪽 바닥 칼, 양쪽에서 좁혀 오는 칼.
    /// 중간보스 2페이즈: 찍기 두 번, 더 좁은 양옆 탄, 되돌아오는 칼, 더 높은 협공.
    /// </summary>
    [RequireComponent(typeof(EnemyActor), typeof(Health), typeof(Rigidbody2D))]
    public class BossActor : MonoBehaviour
    {
        private enum Motion { Idle, Mark, Move, Sweep, Shot }
        private enum Pattern { Charge, SweepFloor, Ranged, Orbit, Volley, Ring, Stamp, Pocket, HalfSweep, Cross }

        [SerializeField] private float attackInterval = 0.32f;
        [SerializeField] private float phaseOneAttackInterval = 0.18f;
        [SerializeField] private float slamRadius = 1.05f;

        private readonly List<Vector2> waypoints = new List<Vector2>(3);
        private readonly List<SpriteRenderer> beams = new List<SpriteRenderer>(8);

        private Health health;
        private Transform player;
        private Vector3 baseScale;
        private Vector3 home;
        private Vector2 cell = Vector2.one;
        private int phase = 1;
        private int facing = 1;
        private int patternCursor;
        private int waypointIndex;
        private float attackTimer = 0.8f;
        private float moveTimer;
        private Motion motion;
        private Pattern pattern;
        private bool arrived;
        private bool strikeCircle = true;
        private EnemyActor enemyActor;
        private Rigidbody2D body;
        private Collider2D hitbox;
        private SpriteRenderer circleWarning;
        private SpriteRenderer boxWarning;
        private Vector2 strikeCenter;
        private Vector2 strikeSize = new Vector2(4f, 0.9f);
        private int attackFacing = 1;
        private int strikeAtWaypoint = -1;
        private int shotIndex;
        private int shotCount = 1;
        private bool struckThisPattern;
        private bool aimLocked;
        private bool midBoss;
        private float lockedAngle;
        private float sweepBaseAngle;
        private Vector2 lockedPoint;
        private Vector2 shotFrom;
        private Vector2 shotTo;
        private float shotDuration = 0.42f;
        private SpriteRenderer shotVisual;
        private float sweepTime;
        private float sweepDuration = 3.4f;
        private float beamLength = 8f;
        private float hurtCooldown;
        private const float EntranceHold = 2.2f;

        private float arenaMinX;
        private float arenaMaxX;
        private float arenaFloorY = float.NegativeInfinity;
        private bool hasArenaBounds;
        private bool encounterActive;
        private bool homeCaptured;
        private static Sprite warningCircleSprite;
        private static Sprite warningSquareSprite;

        public int Phase => phase;
        public bool RequiresPhaseGlory => phase == 1;

        private float MoveSpeed => phase == 1 ? 11f : 13f;
        private float ReturnSpeed => phase == 1 ? 12f : 13f;
        private float MarkTime => midBoss
            ? (phase == 1 ? 0.95f : 0.78f)
            : (phase == 1 ? 0.9f : 0.72f);
        private float ActiveSlamRadius => pattern == Pattern.Stamp ? (phase == 1 ? 1.4f : 1.15f) : slamRadius;

        /// <summary>오른쪽에 보스가 더 있으면 중간보스다. 마지막 보스는 그대로 둔다.</summary>
        public void SetMidBoss(bool value) => midBoss = value;

        private void Awake()
        {
            health = GetComponent<Health>();
            enemyActor = GetComponent<EnemyActor>();
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<Collider2D>();
            baseScale = transform.localScale;
            CaptureHome();
            KeepHovering();
            CreateWarnings();
        }

        private void Start()
        {
            CaptureHome();
            KeepHovering();
            if (Object.FindAnyObjectByType<Gstarr.World.ArenaDoorSpawner>() == null)
                SetEncounterActive(true);
        }

        private void Update()
        {
            if (!encounterActive) return;
            if (health == null || health.Current <= 0 || GamePause.BlocksGameplay || (enemyActor != null && enemyActor.IsStunned))
            {
                SetWarningsVisible(false);
                return;
            }
            if (enemyActor != null && enemyActor.HoldsStill)
            {
                motion = Motion.Idle;
                SetWarningsVisible(false);
                return;
            }
            FindPlayer();
            if (player == null) return;
            var playerHealth = player.GetComponent<Health>();
            if (playerHealth != null && playerHealth.Current <= 0)
            {
                motion = Motion.Idle;
                SetWarningsVisible(false);
                return;
            }
            UpdateAttackPattern();
        }

        private void FixedUpdate()
        {
            if (body == null) return;
            if (enemyActor != null && enemyActor.HoldsStill)
            {
                KeepHovering();
                Hold(HoverPoint());
                return;
            }
            KeepHovering();
            if (motion == Motion.Move && waypointIndex < waypoints.Count)
                FlyToward(waypoints[waypointIndex], MoveSpeed);
            else
                Hold(HoverPoint());
        }

        private void LateUpdate()
        {
            if (!homeCaptured || motion == Motion.Move) return;
            var point = HoverPoint();
            transform.position = new Vector3(point.x, point.y, transform.position.z);
        }

        public void SetArenaBounds(float minX, float maxX, float floorY = float.NegativeInfinity)
        {
            arenaMinX = minX;
            arenaMaxX = maxX;
            arenaFloorY = floorY;
            hasArenaBounds = true;
        }

        public void SetEncounterActive(bool active)
        {
            if (encounterActive == active) return;
            encounterActive = active;
            if (active)
            {
                FindPlayer();
                motion = Motion.Idle;
                arrived = false;
                attackTimer = EntranceHold;
                SetWarningsVisible(false);
                if (body != null) Hold((Vector2)home);
                return;
            }

            motion = Motion.Idle;
            arrived = false;
            transform.localScale = baseScale;
            SetWarningsVisible(false);
            if (body != null) Hold(body.position);
        }

        private void UpdateAttackPattern()
        {
            if (motion == Motion.Idle)
            {
                transform.localScale = baseScale;
                FacePlayer();
                if (!AtHome()) return;
                attackTimer -= Time.deltaTime;
                if (attackTimer > 0f) return;
                BeginPattern();
                return;
            }

            if (motion == Motion.Mark)
            {
                attackTimer -= Time.deltaTime;
                PoseMark();
                if (IsSweep(pattern)) UpdateSweep(false);
                else if (IsRanged(pattern)) UpdateAim();
                else UpdateWarnings();
                if (attackTimer > 0f) return;
                if (IsSweep(pattern))
                {
                    motion = Motion.Sweep;
                    sweepTime = 0f;
                    hurtCooldown = 0f;
                    GameAudio.Play(GameAudio.Id.EnemyShot, 0.45f);
                    return;
                }
                if (IsRanged(pattern))
                {
                    motion = Motion.Shot;
                    shotIndex = 0;
                    hurtCooldown = 0f;
                    BeginShot(0);
                    return;
                }
                motion = Motion.Move;
                waypointIndex = 0;
                moveTimer = 2.2f;
                arrived = false;
                struckThisPattern = false;
                GameAudio.Play(GameAudio.Id.Dash, 0.45f);
                return;
            }

            if (motion == Motion.Shot)
            {
                UpdateShot();
                return;
            }

            if (motion == Motion.Sweep)
            {
                sweepTime += Time.deltaTime;
                hurtCooldown -= Time.deltaTime;
                PoseDash();
                UpdateSweep(true);
                if (sweepTime < sweepDuration) return;
                FinishPattern();
                return;
            }

            if (waypoints.Count == 0)
            {
                FinishPattern();
                return;
            }

            moveTimer -= Time.deltaTime;
            PoseDash();
            UpdateWarnings();
            Face((int)Mathf.Sign(waypoints[waypointIndex].x - transform.position.x));
            if (pattern == Pattern.Charge && waypointIndex == 0)
                HitBossBody();
            if (!arrived && moveTimer > 0f) return;

            if (waypointIndex == strikeAtWaypoint && !struckThisPattern)
            {
                struckThisPattern = true;
                Strike();
            }

            if (waypointIndex < waypoints.Count - 1)
            {
                waypointIndex++;
                moveTimer = 2.2f;
                arrived = false;
                return;
            }

            FinishPattern();
        }

        private void FinishPattern()
        {
            motion = Motion.Idle;
            arrived = false;
            attackTimer = midBoss
                ? (phase == 1 ? 1.15f : 0.9f)
                : (phase == 1 ? 1.05f : 0.85f);
            transform.localScale = baseScale;
            transform.rotation = Quaternion.identity;
            SetWarningsVisible(false);
            patternCursor = (patternCursor + 1) % 6;
        }

        private void BeginPattern()
        {
            pattern = PickPattern();
            waypoints.Clear();
            strikeAtWaypoint = -1;
            struckThisPattern = false;
            strikeCircle = false;
            aimLocked = false;
            Vector2 playerCell = Snap(PlayerGroundPoint());
            int toward = playerCell.x >= home.x ? 1 : -1;
            if (toward == 0) toward = facing;

            switch (pattern)
            {
                case Pattern.Charge:
                    BuildCharge(playerCell);
                    strikeAtWaypoint = -1;
                    break;
                case Pattern.SweepFloor:
                    sweepDuration = phase == 1 ? 1.7f : 1.35f;
                    break;
                case Pattern.Ranged:
                    shotCount = 1;
                    LockAim();
                    break;
                case Pattern.Volley:
                    shotCount = 3;
                    LockAim();
                    break;
                case Pattern.Orbit:
                    sweepDuration = 1.55f;
                    beamLength = 4.8f;
                    break;
                case Pattern.Stamp:
                    BuildStamp();
                    break;
                case Pattern.Pocket:
                    shotCount = 2;
                    LockPocket();
                    break;
                case Pattern.HalfSweep:
                    sweepDuration = phase == 1 ? 1.45f : 1.7f;
                    break;
                case Pattern.Cross:
                    sweepDuration = phase == 1 ? 1.5f : 1.25f;
                    break;
                default:
                    sweepDuration = 2.15f;
                    beamLength = 4.2f;
                    Vector2 toPlayer = PlayerBodyCenter() - (Vector2)home;
                    sweepBaseAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg + 90f;
                    break;
            }

            attackFacing = toward;
            Face(attackFacing);
            motion = Motion.Mark;
            attackTimer = MarkTime;
            arrived = false;
        }

        private void FlyToward(Vector2 target, float speed)
        {
            Vector2 next = Vector2.MoveTowards(body.position, target, speed * Time.fixedDeltaTime);
            body.MovePosition(next);
            arrived = Vector2.Distance(next, target) <= 0.08f;
            if (Mathf.Abs(target.x - body.position.x) > 0.05f)
                Face(target.x >= body.position.x ? 1 : -1);
        }

        private void Hold(Vector2 point)
        {
            body.MovePosition(point);
            arrived = false;
        }

        private Vector2 HoverPoint()
        {
            float bob = Mathf.Sin(Time.time * 2.1f) * 0.07f;
            return (Vector2)home + new Vector2(0f, bob);
        }

        private bool AtHome() => Vector2.Distance(body.position, home) <= 0.22f;

        private Pattern PickPattern()
        {
            int step = patternCursor % 6;
            if (!midBoss)
            {
                return phase == 1
                    ? step switch
                    {
                        0 => Pattern.Ranged,
                        1 => Pattern.Charge,
                        2 => Pattern.SweepFloor,
                        3 => Pattern.Stamp,
                        4 => Pattern.Orbit,
                        _ => Pattern.Pocket
                    }
                    : step switch
                    {
                        0 => Pattern.Ranged,
                        1 => Pattern.SweepFloor,
                        2 => Pattern.Stamp,
                        3 => Pattern.Orbit,
                        4 => Pattern.Volley,
                        _ => Pattern.Charge
                    };
            }

            return phase == 1
                ? step switch
                {
                    0 => Pattern.Stamp,
                    1 => Pattern.Ranged,
                    2 => Pattern.HalfSweep,
                    3 => Pattern.Pocket,
                    4 => Pattern.Charge,
                    _ => Pattern.Cross
                }
                : step switch
                {
                    0 => Pattern.Stamp,
                    1 => Pattern.Pocket,
                    2 => Pattern.HalfSweep,
                    3 => Pattern.Ranged,
                    4 => Pattern.SweepFloor,
                    _ => Pattern.Cross
                };
        }

        private static bool IsSweep(Pattern value) =>
            value == Pattern.SweepFloor || value == Pattern.Orbit || value == Pattern.Ring
            || value == Pattern.HalfSweep || value == Pattern.Cross;

        private static bool IsRanged(Pattern value) =>
            value == Pattern.Ranged || value == Pattern.Volley || value == Pattern.Pocket;

        private void BuildStamp()
        {
            strikeCircle = true;
            float floor = arenaFloorY > float.NegativeInfinity ? arenaFloorY + 0.04f : home.y - cell.y;
            strikeCenter = new Vector2(ClampX(PlayerGroundPoint().x), floor);
            strikeAtWaypoint = 0;
            waypoints.Add(new Vector2(strikeCenter.x, home.y));
            waypoints.Add(home);
        }

        private void BuildCharge(Vector2 playerCell)
        {
            float reach = cell.x * (phase == 1 ? 3.1f : 2.6f);
            var aim = new Vector2(playerCell.x, home.y);
            Vector2 delta = aim - (Vector2)home;
            if (delta.magnitude > reach) aim = (Vector2)home + delta.normalized * reach;
            aim = new Vector2(ClampX(aim.x), home.y);
            strikeCenter = ((Vector2)home + aim) * 0.5f;
            strikeSize = new Vector2(Mathf.Max(1.2f, Mathf.Abs(aim.x - home.x) + 1.1f), 1.2f);
            strikeAtWaypoint = -1;
            waypoints.Add(aim);
            waypoints.Add(home);
        }

        private void LockAim()
        {
            Vector2 aim = PlayerBodyCenter() - (Vector2)home;
            aim.y = Mathf.Clamp(aim.y, -0.45f, 0.65f);
            if (aim.sqrMagnitude < 0.01f) aim = new Vector2(facing, 0f);
            lockedAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            aimLocked = true;
        }

        private void LockPocket()
        {
            lockedPoint = PlayerBodyCenter();
            aimLocked = true;
        }

        private void AimShot(int index)
        {
            if (pattern == Pattern.Pocket)
            {
                float gap = phase == 1 ? 2.15f : 1.85f;
                float side = index == 0 ? -1f : 1f;
                Vector2 target = lockedPoint + new Vector2(side * gap, 0f);
                Vector2 dir = target - (Vector2)home;
                if (dir.sqrMagnitude < 0.01f) dir = new Vector2(side, 0f);
                float dist = Mathf.Clamp(dir.magnitude, cell.x * 2.2f, cell.x * 8f);
                shotFrom = home;
                shotTo = (Vector2)home + dir.normalized * dist;
                return;
            }

            float angle = aimLocked ? lockedAngle : facing > 0 ? 0f : 180f;
            if (pattern == Pattern.Volley)
                angle += (index - 1) * 34f;
            float distAim = cell.x * (phase == 1 ? 7.2f : 8.2f);
            shotFrom = home;
            shotTo = (Vector2)home + AngleDir(angle) * distAim;
        }

        private void BeginShot(int index)
        {
            AimShot(index);
            shotDuration = phase == 1 ? 0.36f : 0.3f;
            sweepTime = 0f;
            GameAudio.Play(GameAudio.Id.EnemyShot, 0.4f);
        }

        private void UpdateAim()
        {
            HideBeams();
            if (circleWarning != null) circleWarning.enabled = false;
            int count = pattern == Pattern.Volley ? 3 : pattern == Pattern.Pocket ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                AimShot(i);
                float angle = Mathf.Atan2(shotTo.y - shotFrom.y, shotTo.x - shotFrom.x) * Mathf.Rad2Deg;
                ShowBeam(i, shotFrom, angle, Vector2.Distance(shotFrom, shotTo), 0.16f, false);
            }
        }

        private void UpdateShot()
        {
            sweepTime += Time.deltaTime;
            hurtCooldown -= Time.deltaTime;
            float u = shotDuration <= 0.01f ? 1f : Mathf.Clamp01(sweepTime / shotDuration);
            Vector2 pos = Vector2.Lerp(shotFrom, shotTo, u);
            if (shotVisual != null)
            {
                shotVisual.enabled = true;
                shotVisual.transform.position = pos;
                shotVisual.transform.localScale = Vector3.one * 0.42f;
                shotVisual.color = new Color(1f, 0.92f, 0.55f, 0.95f);
            }
            HideBeams();
            if (hurtCooldown <= 0f && player != null && Vector2.Distance(pos, PlayerBodyCenter()) <= 0.5f)
            {
                hurtCooldown = 0.6f;
                DamagePlayer((PlayerBodyCenter() - pos).normalized);
            }
            if (u < 1f) return;
            shotIndex++;
            if (shotIndex < shotCount)
            {
                BeginShot(shotIndex);
                return;
            }
            if (shotVisual != null) shotVisual.enabled = false;
            FinishPattern();
        }

        private void UpdateSweep(bool damaging)
        {
            if (boxWarning != null) boxWarning.enabled = false;
            if (circleWarning != null) circleWarning.enabled = false;
            HideBeams();

            if (pattern == Pattern.SweepFloor)
                SweepFloorLane(damaging);
            else if (pattern == Pattern.HalfSweep)
                SweepHalfFloor(damaging);
            else if (pattern == Pattern.Cross)
                SweepCross(damaging);
            else if (pattern == Pattern.Ring)
                SweepGap(damaging);
            else
                SweepBaton(damaging);
        }

        /// <summary>플레이어가 있는 쪽 바닥만 가른다. 반대쪽으로 빠지면 맞는다.</summary>
        private void SweepHalfFloor(bool damaging)
        {
            float left = hasArenaBounds ? arenaMinX + 0.45f : home.x - cell.x * 5f;
            float right = hasArenaBounds && !float.IsPositiveInfinity(arenaMaxX) ? arenaMaxX - 0.45f : home.x + cell.x * 5f;
            float edge = attackFacing > 0 ? right : left;
            float along = phase == 2 ? Mathf.PingPong(Sweep01() * 2f, 1f) : Sweep01();
            float x = Mathf.Lerp(home.x, edge, along);
            float floor = arenaFloorY > float.NegativeInfinity ? arenaFloorY : home.y - cell.y;
            ShowBeam(0, new Vector2(x, floor + 0.58f), 0f, 2.35f, 0.4f, damaging);
            Face(attackFacing);
        }

        /// <summary>양쪽 끝에서 가운데로 칼이 모인다. 그 자리에서 뛰면 넘긴다.</summary>
        private void SweepCross(bool damaging)
        {
            float left = hasArenaBounds ? arenaMinX + 0.4f : home.x - cell.x * 5f;
            float right = hasArenaBounds && !float.IsPositiveInfinity(arenaMaxX) ? arenaMaxX - 0.4f : home.x + cell.x * 5f;
            float mid = (left + right) * 0.5f;
            float u = Sweep01();
            float floor = arenaFloorY > float.NegativeInfinity ? arenaFloorY : home.y - cell.y;
            float y = floor + 0.55f;
            ShowBeam(0, new Vector2(Mathf.Lerp(left, mid, u), y), 0f, 2.1f, 0.36f, damaging);
            ShowBeam(1, new Vector2(Mathf.Lerp(right, mid, u), y), 0f, 2.1f, 0.36f, damaging);
        }

        /// <summary>바닥을 따라 짧은 칼이 지나간다. 뛰면 넘긴다.</summary>
        private void SweepFloorLane(bool damaging)
        {
            float left = hasArenaBounds ? arenaMinX + 0.45f : home.x - cell.x * 5f;
            float right = hasArenaBounds && !float.IsPositiveInfinity(arenaMaxX) ? arenaMaxX - 0.45f : home.x + cell.x * 5f;
            float floor = arenaFloorY > float.NegativeInfinity ? arenaFloorY : home.y - cell.y;
            float x = Mathf.Lerp(left, right, Sweep01());
            float span = Mathf.Max(1f, right - left);
            float length = phase == 1 ? Mathf.Max(2.1f, span * 0.22f) : Mathf.Max(2.6f, span * 0.3f);
            ShowBeam(0, new Vector2(x, floor + 0.5f), 0f, length, 0.36f, damaging);
            Face(1);
        }

        /// <summary>플레이어가 있는 쪽으로 지휘봉을 아래에서 위로 훑는다. 뒤로 빠지면 맞는다.</summary>
        private void SweepBaton(bool damaging)
        {
            float sweep = Mathf.Lerp(-62f, 62f, Sweep01());
            float angle = attackFacing > 0 ? sweep : 180f - sweep;
            float length = Mathf.Clamp(OuterReach() * 0.55f, cell.x * 2.4f, cell.x * 3.6f);
            ShowBeam(0, home, angle, length, 0.3f, damaging);
            transform.rotation = Quaternion.Euler(0f, 0f, sweep * 0.2f);
        }

        /// <summary>맞은편 두 줄이 돈다. 줄 사이 빈칸에 서서 같이 움직인다.</summary>
        private void SweepGap(bool damaging)
        {
            float angle = sweepBaseAngle + Sweep01() * 170f;
            float length = Mathf.Clamp(OuterReach() * 0.92f, cell.x * 3.2f, cell.x * 8f);
            ShowBeam(0, home, angle, length, 0.26f, damaging);
            ShowBeam(1, home, angle + 180f, length, 0.26f, damaging);
        }

        private void HitBossBody()
        {
            if (struckThisPattern || player == null) return;
            var hits = Physics2D.OverlapBoxAll(transform.position, new Vector2(1.45f, 1.75f), 0f);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].GetComponentInParent<PlayerController>() == null) continue;
                struckThisPattern = true;
                DamagePlayer(new Vector2(attackFacing, 0.35f));
                return;
            }
        }

        private float Sweep01() => sweepDuration <= 0.01f ? 1f : Mathf.Clamp01(sweepTime / sweepDuration);

        private static Vector2 AngleDir(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        private void ShowBeam(int index, Vector2 origin, float angleDeg, float length, float thickness, bool damaging)
        {
            var warning = BeamView(index);
            Vector2 dir = AngleDir(angleDeg);
            Vector2 center = origin + dir * (length * 0.5f);
            warning.enabled = true;
            warning.transform.position = center;
            warning.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            warning.transform.localScale = new Vector3(Mathf.Max(0.2f, length), thickness, 1f);
            warning.color = damaging
                ? new Color(0.9f, 0.96f, 1f, 0.9f)
                : new Color(1f, 0.86f, 0.25f, 0.42f);
            if (damaging) HitBeam(center, new Vector2(length, thickness), angleDeg);
        }

        private void HitBeam(Vector2 center, Vector2 size, float angle)
        {
            if (hurtCooldown > 0f || player == null) return;
            var hits = Physics2D.OverlapBoxAll(center, size, angle);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].GetComponentInParent<PlayerController>() == null) continue;
                hurtCooldown = 0.5f;
                DamagePlayer((PlayerBodyCenter() - center).normalized);
                return;
            }
        }

        private float OuterReach()
        {
            float span = cell.x * 8f;
            if (!hasArenaBounds) return span;
            float right = float.IsPositiveInfinity(arenaMaxX) ? home.x + span : arenaMaxX;
            return Mathf.Max(cell.x * 3f, Mathf.Max(home.x - arenaMinX, right - home.x));
        }

        private float InnerReach()
        {
            if (!hasArenaBounds) return cell.x * 5f;
            float left = home.x - arenaMinX;
            float right = float.IsPositiveInfinity(arenaMaxX) ? cell.x * 8f : arenaMaxX - home.x;
            return Mathf.Max(cell.x * 2.2f, Mathf.Min(left, right) - 0.35f);
        }

        private SpriteRenderer BeamView(int index)
        {
            while (beams.Count <= index)
                beams.Add(MakeWarning("BossBeam" + beams.Count, warningSquareSprite));
            return beams[index];
        }

        private void HideBeams()
        {
            for (int i = 0; i < beams.Count; i++)
                if (beams[i] != null) beams[i].enabled = false;
        }

        private void PoseMark()
        {
            float t = 1f - Mathf.Clamp01(attackTimer / MarkTime);
            float lift = Mathf.Lerp(0f, 1f, t);
            transform.localScale = new Vector3(baseScale.x, baseScale.y * Mathf.Lerp(1f, 1.08f, lift), baseScale.z);
            transform.rotation = Quaternion.Euler(0f, 0f, -facing * 28f * lift);
        }

        private void PoseDash()
        {
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(baseScale.x * 1.18f, baseScale.y * 0.86f, baseScale.z);
        }

        private void Strike()
        {
            transform.localScale = new Vector3(baseScale.x * 1.3f, baseScale.y * 0.62f, baseScale.z);
            GameAudio.Play(GameAudio.Id.EnemySwing, 0.7f);
            CombatFeel.Shake(0.12f, 0.1f);
            CombatFeel.Burst(strikeCenter, new Color(1f, 0.45f, 0.15f), 8);
            if (player == null) return;

            if (strikeCircle)
            {
                if (Vector2.Distance(PlayerBodyCenter(), strikeCenter) > ActiveSlamRadius) return;
                DamagePlayer((PlayerBodyCenter() - (Vector2)transform.position).normalized);
                return;
            }

            var hits = Physics2D.OverlapBoxAll(strikeCenter, strikeSize, 0f);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].GetComponentInParent<PlayerController>() == null) continue;
                DamagePlayer(new Vector2(attackFacing, 0.45f));
                return;
            }
        }

        private Vector2 PlayerGroundPoint()
        {
            var playerCollider = player != null ? player.GetComponent<Collider2D>() : null;
            return playerCollider != null
                ? new Vector2(playerCollider.bounds.center.x, playerCollider.bounds.min.y + 0.03f)
                : (Vector2)player.position;
        }

        private Vector2 PlayerBodyCenter()
        {
            var playerCollider = player != null ? player.GetComponent<Collider2D>() : null;
            return playerCollider != null ? playerCollider.bounds.center : (Vector2)player.position;
        }

        private void CaptureHome()
        {
            if (homeCaptured) return;
            homeCaptured = true;
            var grid = GetComponentInParent<Grid>();
            if (grid == null) grid = Object.FindAnyObjectByType<Grid>();
            cell = grid != null ? (Vector2)grid.cellSize : Vector2.one;
            if (cell.x < 0.01f) cell.x = 1f;
            if (cell.y < 0.01f) cell.y = 1f;
            home = transform.position;
        }

        private Vector2 GridPoint(int stepX, int stepY) =>
            ClampPoint(new Vector2(home.x + stepX * cell.x, home.y + stepY * cell.y));

        private Vector2 Snap(Vector2 world)
        {
            float x = home.x + Mathf.Round((world.x - home.x) / cell.x) * cell.x;
            float y = home.y + Mathf.Round((world.y - home.y) / cell.y) * cell.y;
            return ClampPoint(new Vector2(x, y));
        }

        private Vector2 ClampPoint(Vector2 point)
        {
            point.x = ClampX(point.x);
            float minY = home.y - cell.y * 2f;
            if (arenaFloorY > float.NegativeInfinity)
                minY = Mathf.Max(minY, arenaFloorY + 0.6f);
            point.y = Mathf.Clamp(point.y, minY, home.y + cell.y * 1.15f);
            return point;
        }

        private float BodyPad()
        {
            float pad = hitbox != null ? hitbox.bounds.extents.x : 0.9f;
            return Mathf.Max(0.9f, pad);
        }

        private float ClampX(float x)
        {
            if (!hasArenaBounds) return x;
            float pad = BodyPad();
            float minX = arenaMinX + pad;
            float maxX = float.IsPositiveInfinity(arenaMaxX) ? arenaMaxX : arenaMaxX - pad;
            if (maxX < minX) return (arenaMinX + arenaMaxX) * 0.5f;
            return Mathf.Clamp(x, minX, maxX);
        }

        private void ClampToArena()
        {
            if (!hasArenaBounds || body == null) return;
            float clampedX = ClampX(body.position.x);
            float minY = home.y - cell.y * 2f;
            if (arenaFloorY > float.NegativeInfinity)
                minY = Mathf.Max(minY, arenaFloorY + 0.6f);
            float clampedY = Mathf.Clamp(body.position.y, minY, home.y + cell.y * 1.15f);
            if (Mathf.Abs(clampedX - body.position.x) < 0.001f && Mathf.Abs(clampedY - body.position.y) < 0.001f)
                return;
            body.position = new Vector2(clampedX, clampedY);
            if (motion == Motion.Move) arrived = true;
        }

        private void KeepHovering()
        {
            if (body == null) return;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
        }

        private void CreateWarnings()
        {
            if (warningCircleSprite == null) warningCircleSprite = MakeWarningCircle();
            if (warningSquareSprite == null)
            {
                var white = Texture2D.whiteTexture;
                warningSquareSprite = Sprite.Create(white, new Rect(0f, 0f, white.width, white.height), new Vector2(0.5f, 0.5f), white.width);
            }

            circleWarning = MakeWarning("BossCircleWarning", warningCircleSprite);
            boxWarning = MakeWarning("BossBoxWarning", warningSquareSprite);
            shotVisual = MakeWarning("BossShot", warningCircleSprite);
        }

        private SpriteRenderer MakeWarning(string warningName, Sprite warningSprite)
        {
            var warning = new GameObject(warningName).AddComponent<SpriteRenderer>();
            warning.sprite = warningSprite;
            warning.sortingOrder = 5;
            warning.enabled = false;
            return warning;
        }

        private static Sprite MakeWarningCircle()
        {
            const int size = 48;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float radius = size * 0.48f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / radius;
                byte alpha = d < 0.82f ? (byte)18 : d <= 1f ? (byte)170 : (byte)0;
                pixels[y * size + x] = new Color32(255, 80, 40, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private void UpdateWarnings()
        {
            float danger = motion == Motion.Move ? 1f : 1f - Mathf.Clamp01(attackTimer / MarkTime);
            if (strikeCircle && circleWarning != null)
            {
                circleWarning.enabled = true;
                if (boxWarning != null) boxWarning.enabled = false;
                circleWarning.transform.position = new Vector3(strikeCenter.x, strikeCenter.y + 0.02f, transform.position.z);
                circleWarning.transform.localScale = Vector3.one * ActiveSlamRadius * 2f;
                circleWarning.color = Color.Lerp(new Color(1f, 0.8f, 0.15f, 0.45f), new Color(1f, 0.15f, 0.08f, 0.85f), danger);
                return;
            }

            if (boxWarning != null)
            {
                boxWarning.enabled = true;
                if (circleWarning != null) circleWarning.enabled = false;
                boxWarning.transform.position = strikeCenter;
                boxWarning.transform.localScale = new Vector3(strikeSize.x, strikeSize.y, 1f);
                boxWarning.color = Color.Lerp(new Color(1f, 0.8f, 0.15f, 0.28f), new Color(1f, 0.12f, 0.08f, 0.72f), danger);
            }
        }

        private void SetWarningsVisible(bool visible)
        {
            if (visible) return;
            if (circleWarning != null) circleWarning.enabled = false;
            if (boxWarning != null) boxWarning.enabled = false;
            if (shotVisual != null) shotVisual.enabled = false;
            HideBeams();
        }

        private void OnDestroy()
        {
            if (circleWarning != null) Destroy(circleWarning.gameObject);
            if (boxWarning != null) Destroy(boxWarning.gameObject);
            if (shotVisual != null) Destroy(shotVisual.gameObject);
            for (int i = 0; i < beams.Count; i++)
                if (beams[i] != null) Destroy(beams[i].gameObject);
        }

        private void DamagePlayer(Vector2 direction)
        {
            if (player == null) return;
            if (direction.sqrMagnitude < 0.001f) direction = new Vector2(attackFacing, 0.4f);
            player.GetComponent<Health>()?.TakeDamage(2, direction);
        }

        private void FindPlayer()
        {
            if (player != null) return;
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) player = tagged.transform;
        }

        private void FacePlayer()
        {
            if (player == null) return;
            Face(player.position.x >= transform.position.x ? 1 : -1);
        }

        private void Face(int direction)
        {
            if (direction == 0) return;
            facing = direction;
            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * facing;
            transform.localScale = scale;
            baseScale.x = Mathf.Abs(baseScale.x) * facing;
        }

        public bool TryAdvancePhase()
        {
            if (phase != 1) return false;
            phase = 2;
            patternCursor = 0;
            motion = Motion.Idle;
            arrived = false;
            attackTimer = 0.9f;
            SetWarningsVisible(false);
            transform.localScale = baseScale;
            health.Heal(health.Max);
            StageHUD.ShowHint(midBoss
                ? "2페이즈. 같은 기술을 조금 빨리 씁니다. 예고를 보고 한 칸만 비우면 됩니다."
                : "2페이즈. 탄이 넓게 갈라지고, 바닥 칼이 조금 길어집니다. 예고된 자리만 피하면 됩니다.", 4.5f);
            return true;
        }
    }
}
