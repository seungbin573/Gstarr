using Gstarr.Game;
using Gstarr.World;
using UnityEngine;

namespace Gstarr.Combat
{
    /// <summary>블랙 키드처럼 날며 돌진한다. 종이공은 그 자리로 던지고, 맞으면 피해를 준다.</summary>
    [RequireComponent(typeof(EnemyActor))]
    [DefaultExecutionOrder(80)]
    public sealed class BlackKidThrow : MonoBehaviour
    {
        private enum Phase { Idle, Throw, Dash, Recover }
        private const float ThrowSeconds = 0.52f;
        private const float DashSeconds = 0.34f;
        private const float RecoverSeconds = 0.7f;
        private const float DashSpeed = 16f;
        private const float HoverLift = 2.6f;
        private const float BallGravity = -14f;
        private const float ThrowSpeed = 9.6f;

        private EnemyActor actor;
        private Rigidbody2D body;
        private SpriteRenderer visual;
        private Sprite idle;
        private Sprite[] throwing;
        private Sprite paper;
        private Phase phase;
        private float elapsed;
        private float cruiseY;
        private Vector2 dash;
        private bool tossed;

        private void Awake()
        {
            actor = GetComponent<EnemyActor>();
            body = GetComponent<Rigidbody2D>();
            visual = GetComponent<SpriteRenderer>();
            idle = CharacterArt.Idle(EnemyKind.BlackShadowThrow);
            throwing = CharacterArt.Frames(EnemyKind.BlackShadowThrow, CharacterArt.Pose.Throw);
            var shots = CharacterArt.Frames(EnemyKind.BlackShadowThrow, CharacterArt.Pose.Shot);
            paper = shots != null && shots.Length > 0 ? shots[0] : idle;
            cruiseY = transform.position.y;
            body.gravityScale = 0f;
            Enter(Phase.Idle);
        }

        private void FixedUpdate()
        {
            actor.ClampRoom();
            if (!actor.CanAct)
            {
                Enter(Phase.Idle);
                body.linearVelocity = Vector2.zero;
                return;
            }

            var player = actor.PlayerTarget;
            bool seen = SeesPlayer(player);
            elapsed += Time.fixedDeltaTime;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            switch (phase)
            {
                case Phase.Idle:
                    Hover(player, seen);
                    if (seen && elapsed >= 0.35f) Enter(Phase.Throw);
                    break;
                case Phase.Throw:
                    Hover(player, seen);
                    body.linearVelocity *= 0.35f;
                    if (seen) Face(player.position.x - transform.position.x);
                    if (!tossed && elapsed >= ThrowSeconds * 0.72f)
                    {
                        tossed = true;
                        ThrowPaper(player);
                    }
                    if (elapsed >= ThrowSeconds) BeginDash(player);
                    break;
                case Phase.Dash:
                    Face(dash.x);
                    body.linearVelocity = dash * DashSpeed;
                    int dir = dash.x >= 0f ? 1 : -1;
                    if (elapsed >= DashSeconds || !actor.RoomAhead(dir)) Enter(Phase.Recover);
                    break;
                case Phase.Recover:
                    Hover(player, seen);
                    if (elapsed >= RecoverSeconds) Enter(Phase.Idle);
                    break;
            }

            ShowFrame();
        }

        private void Hover(Transform player, bool seen)
        {
            float targetY = cruiseY + Mathf.Sin(Time.time * 1.7f) * 0.22f;
            float targetX = transform.position.x;
            if (seen && player != null)
            {
                targetY = player.position.y + HoverLift + Mathf.Sin(Time.time * 1.7f) * 0.22f;
                float side = Mathf.Sin(Time.time * 0.8f) * 1.4f;
                targetX = player.position.x + side;
                Face(player.position.x - transform.position.x);
            }

            targetY = Mathf.Clamp(targetY, cruiseY - 0.8f, cruiseY + 1.6f);
            if (actor.HasLeash) targetX = Mathf.Clamp(targetX, actor.RoomMin + 0.8f, actor.RoomMax - 0.8f);
            Vector2 delta = new Vector2(targetX - transform.position.x, targetY - transform.position.y);
            body.linearVelocity = Vector2.ClampMagnitude(delta * 2.4f, 4.2f);
        }

        private void BeginDash(Transform player)
        {
            Vector2 target = player != null
                ? (Vector2)player.position + Vector2.up * 0.4f
                : (Vector2)transform.position + Vector2.right;
            dash = target - (Vector2)transform.position;
            if (dash.sqrMagnitude < 0.04f) dash = new Vector2(visual.flipX ? -1f : 1f, -0.2f);
            dash.Normalize();
            Enter(Phase.Dash);
            GameAudio.Play(GameAudio.Id.Dash, 0.7f);
        }

        private void ThrowPaper(Transform player)
        {
            if (player == null || paper == null) return;
            int direction = visual.flipX ? -1 : 1;
            Vector2 origin = (Vector2)transform.position + new Vector2(direction * 0.7f, 0.55f);
            Vector2 target = (Vector2)player.position + Vector2.up * 0.45f;
            float travel = ArenaDoorSpawner.RangeUntilDoor(origin.x, target.x - origin.x, 12f);
            if (travel < 0.4f) return;

            var go = new GameObject("RolledPaper");
            go.transform.position = origin;
            var view = go.AddComponent<SpriteRenderer>();
            view.sprite = paper;
            view.sortingOrder = 9;
            var projectile = go.AddComponent<Projectile>();
            projectile.SetAnimation(new[] { paper });
            Vector2 velocity = Ballistic(origin, target);
            float flight = Mathf.Abs(target.x - origin.x) / Mathf.Max(0.01f, Mathf.Abs(velocity.x));
            float untilDoor = travel / Mathf.Max(0.01f, Mathf.Abs(velocity.x));
            float life = Mathf.Min(flight + 0.35f, untilDoor);
            projectile.Launch(velocity, 1, life * velocity.magnitude, 1 << player.gameObject.layer, gameObject);
            projectile.SetGravity(new Vector2(0f, BallGravity));
            GameAudio.Play(GameAudio.Id.EnemyShot, 0.75f);
        }

        private static Vector2 Ballistic(Vector2 from, Vector2 to)
        {
            float dx = to.x - from.x;
            float dy = to.y - from.y;
            float t = Mathf.Clamp(Mathf.Abs(dx) / ThrowSpeed, 0.28f, 0.9f);
            float dt = Time.fixedDeltaTime;
            float vx = dx / t;
            float vy = (dy - BallGravity * t * (t + dt) * 0.5f) / t;
            return new Vector2(vx, vy);
        }

        private bool SeesPlayer(Transform player)
        {
            if (player == null) return false;
            if (ArenaDoorSpawner.SeparatedByDoor(transform.position.x, player.position.x)) return false;
            if (actor.HasLeash)
                return player.position.x >= actor.RoomMin && player.position.x <= actor.RoomMax;
            return Mathf.Abs(player.position.x - transform.position.x) <= 9f
                && Mathf.Abs(player.position.y - transform.position.y) <= 6f;
        }

        private void Face(float dx)
        {
            if (Mathf.Abs(dx) < 0.05f) return;
            visual.flipX = dx < 0f;
        }

        private void Enter(Phase next)
        {
            if (phase == next && next != Phase.Dash) return;
            phase = next;
            elapsed = 0f;
            if (next == Phase.Throw) tossed = false;
            ShowFrame();
        }

        private void ShowFrame()
        {
            if (phase == Phase.Throw && throwing != null && throwing.Length > 0)
            {
                int index = Mathf.Min(throwing.Length - 1, Mathf.FloorToInt(elapsed / ThrowSeconds * throwing.Length));
                visual.sprite = throwing[index];
                return;
            }

            if (phase == Phase.Dash && throwing != null && throwing.Length > 0)
            {
                visual.sprite = throwing[throwing.Length - 1];
                return;
            }

            visual.sprite = idle;
        }

        private void OnCollisionEnter2D(Collision2D collision) => Touch(collision);
        private void OnCollisionStay2D(Collision2D collision) => Touch(collision);

        private void Touch(Collision2D collision)
        {
            if (!actor.CanAct) return;
            var target = actor.PlayerTarget;
            var health = collision.collider.GetComponentInParent<Health>();
            if (target == null || health == null || health != target.GetComponent<Health>()) return;
            Vector2 push = (Vector2)target.position - (Vector2)transform.position;
            if (push.sqrMagnitude < 0.01f) push = dash.sqrMagnitude > 0.01f ? dash : Vector2.right;
            health.TakeEnemyHit(1, push.normalized);
        }
    }
}
