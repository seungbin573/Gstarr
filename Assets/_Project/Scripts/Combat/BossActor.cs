using System.Collections.Generic;
using Gstarr.Game;
using Gstarr.Player;
using UnityEngine;

namespace Gstarr.Combat
{
    /// <summary>
    /// 처음 놓인 자리에 머물고, 돌진과 바닥 쓸기만 나갔다가 그 자리로 돌아온다.
    /// 1페이즈: 돌진, 바닥 쓸기, 원거리, 느린 회전. 2페이즈: 돌진, 넓은 쓸기, 세 발, 빈칸 회전.
    /// </summary>
    [RequireComponent(typeof(EnemyActor), typeof(Health), typeof(Rigidbody2D))]
    public class BossActor : MonoBehaviour
    {
        private enum Motion { Idle, Mark, Move, Sweep, Shot }
        private enum Pattern { Charge, SweepFloor, Ranged, Orbit, Volley, Ring }

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
        private Vector2 shotFrom;
        private Vector2 shotTo;
        private float shotDuration = 0.42f;
        private SpriteRenderer shotVisual;
        private float sweepTime;
        private float sweepDuration = 3.4f;
        private float sweepStart = 90f;
        private float beamLength = 8f;
        private float ringRadius = 4f;
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

        private float MoveSpeed => phase == 1 ? 23f : 17f;
        private float ReturnSpeed => phase == 1 ? 18f : 13f;
        private float MarkTime => phase == 1 ? 0.28f : 0.38f;

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
            attackTimer = phase == 1 ? phaseOneAttackInterval : attackInterval;
            transform.localScale = baseScale;
            SetWarningsVisible(false);
            patternCursor = (patternCursor + 1) % 4;
        }

        private void BeginPattern()
        {
            pattern = phase == 1
                ? (Pattern)(patternCursor % 4)
                : patternCursor % 4 == 2 ? Pattern.Volley
                : patternCursor % 4 == 3 ? Pattern.Ring
                : (Pattern)(patternCursor % 4);
            waypoints.Clear();
            strikeAtWaypoint = -1;
            struckThisPattern = false;
            strikeCircle = false;
            Vector2 playerCell = Snap(PlayerGroundPoint());
            int toward = playerCell.x >= home.x ? 1 : -1;
            if (toward == 0) toward = facing;

            switch (pattern)
            {
                case Pattern.Charge:
                    BuildCharge(playerCell);
                    break;
                case Pattern.SweepFloor:
                    BuildFloorSweep(playerCell);
                    break;
                case Pattern.Ranged:
                    shotCount = 1;
                    AimShot(PlayerBodyCenter(), 0);
                    break;
                case Pattern.Volley:
                    shotCount = 3;
                    AimShot(PlayerBodyCenter(), 0);
                    break;
                case Pattern.Orbit:
                    sweepDuration = phase == 1 ? 1.45f : 1.15f;
                    sweepStart = 0f;
                    beamLength = 2.6f;
                    break;
                default:
                    sweepDuration = phase == 1 ? 1.25f : 1.0f;
                    sweepStart = 180f;
                    beamLength = 2.4f;
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

        private static bool IsSweep(Pattern value) => value == Pattern.Orbit || value == Pattern.Ring;

        private static bool IsRanged(Pattern value) => value == Pattern.Ranged || value == Pattern.Volley;

        private void BuildCharge(Vector2 playerCell)
        {
            float reach = cell.x * (phase == 1 ? 4.2f : 3.4f);
            var aim = new Vector2(playerCell.x, home.y);
            Vector2 delta = aim - (Vector2)home;
            if (delta.magnitude > reach) aim = (Vector2)home + delta.normalized * reach;
            aim = new Vector2(ClampX(aim.x), home.y);
            strikeCenter = ((Vector2)home + aim) * 0.5f;
            strikeSize = new Vector2(Mathf.Max(1.2f, Mathf.Abs(aim.x - home.x) + 1.1f), 1.2f);
            strikeAtWaypoint = 0;
            waypoints.Add(aim);
            waypoints.Add(home);
        }

        private void BuildFloorSweep(Vector2 playerCell)
        {
            float floor = arenaFloorY > float.NegativeInfinity ? arenaFloorY + 0.5f : home.y - cell.y;
            floor = Mathf.Clamp(floor, home.y - cell.y * 3f, home.y + cell.y);
            float half = cell.x * (phase == 1 ? 3.2f : 4.4f);
            float left = ClampX(playerCell.x - half);
            float right = ClampX(playerCell.x + half);
            if (right < left) right = left;
            strikeCenter = new Vector2((left + right) * 0.5f, floor);
            strikeSize = new Vector2(Mathf.Max(cell.x, right - left), 0.95f);
            strikeAtWaypoint = 1;
            waypoints.Add(new Vector2(left, floor));
            waypoints.Add(new Vector2(right, floor));
            waypoints.Add(home);
        }

        private void AimShot(Vector2 target, int index)
        {
            Vector2 origin = home;
            Vector2 aim = target - origin;
            aim.y = Mathf.Clamp(aim.y, -0.35f, 0.55f);
            if (aim.sqrMagnitude < 0.01f) aim = new Vector2(facing, 0f);
            float dist = Mathf.Clamp(aim.magnitude, cell.x * 3f, cell.x * 8f);
            Vector2 dir = aim.normalized;
            if (pattern == Pattern.Volley)
                dir = AngleDir(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + (index - 1) * 16f);
            shotFrom = origin;
            shotTo = origin + dir * dist;
        }

        private void BeginShot(int index)
        {
            AimShot(PlayerBodyCenter(), index);
            shotDuration = phase == 1 ? 0.42f : 0.55f;
            sweepTime = 0f;
            GameAudio.Play(GameAudio.Id.EnemyShot, 0.4f);
        }

        private void UpdateAim()
        {
            HideBeams();
            if (circleWarning != null) circleWarning.enabled = false;
            if (pattern == Pattern.Volley)
            {
                for (int i = 0; i < 3; i++)
                {
                    AimShot(PlayerBodyCenter(), i);
                    ShowBeam(i, shotFrom, Mathf.Atan2(shotTo.y - shotFrom.y, shotTo.x - shotFrom.x) * Mathf.Rad2Deg,
                        Vector2.Distance(shotFrom, shotTo), 0.18f, false);
                }
                return;
            }
            AimShot(PlayerBodyCenter(), 0);
            float angle = Mathf.Atan2(shotTo.y - shotFrom.y, shotTo.x - shotFrom.x) * Mathf.Rad2Deg;
            ShowBeam(0, shotFrom, angle, Vector2.Distance(shotFrom, shotTo), 0.16f, false);
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
                shotVisual.color = new Color(1f, 0.35f, 0.2f, 0.95f);
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

            if (pattern == Pattern.Orbit || pattern == Pattern.Ring)
            {
                SweepFlatLane(pattern == Pattern.Orbit ? 0.85f : 1.45f, pattern == Pattern.Orbit ? 0.72f : 0.55f, damaging, pattern == Pattern.Orbit);
                return;
            }
        }

        /// <summary>평지에서 좌우로만 지나가는 바닥 파동.</summary>
        private void SweepFlatLane(float lift, float thickness, bool damaging, bool leftToRight)
        {
            float left = hasArenaBounds ? arenaMinX + 0.7f : home.x - cell.x * 4f;
            float right = hasArenaBounds && !float.IsPositiveInfinity(arenaMaxX) ? arenaMaxX - 0.7f : home.x + cell.x * 4f;
            float x = Mathf.Lerp(leftToRight ? left : right, leftToRight ? right : left, Sweep01());
            float y = arenaFloorY > float.NegativeInfinity ? arenaFloorY + lift : home.y + lift * 0.35f;
            y = Mathf.Clamp(y, home.y - cell.y, home.y + cell.y * 1.15f);
            ShowBeam(0, new Vector2(x, y), 0f, beamLength, thickness, damaging);
            Face(leftToRight ? 1 : -1);
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
            float squash = Mathf.Lerp(1f, 0.7f, t);
            transform.localScale = new Vector3(baseScale.x, baseScale.y * squash, baseScale.z);
        }

        private void PoseDash()
        {
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
                if (Vector2.Distance(PlayerBodyCenter(), strikeCenter) > slamRadius) return;
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
                circleWarning.transform.localScale = Vector3.one * slamRadius * 2f;
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
            StageHUD.ShowHint("2페이즈. 돌진은 더 짧고, 탄이 세 발로 퍼집니다.", 4.5f);
            return true;
        }
    }
}
