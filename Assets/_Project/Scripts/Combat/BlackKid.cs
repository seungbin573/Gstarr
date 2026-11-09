using Gstarr.Game;
using Gstarr.World;
using UnityEngine;

namespace Gstarr.Combat
{
    /// <summary>하늘에서 떠 다니다가 플레이어가 있는 곳으로 직선 돌진한다.</summary>
    [RequireComponent(typeof(EnemyActor))]
    [DefaultExecutionOrder(80)]
    public sealed class BlackKid : MonoBehaviour
    {
        private enum Phase { Idle, Windup, Dash, Recover }
        private const float WindupSeconds = 0.46f;
        private const float DashSeconds = 0.34f;
        private const float RecoverSeconds = 0.7f;
        private const float DashSpeed = 16f;
        private const float HoverLift = 2.6f;

        private EnemyActor actor;
        private Rigidbody2D body;
        private SpriteRenderer visual;
        private Sprite idle;
        private Sprite[] charge;
        private Phase phase;
        private float elapsed;
        private float cruiseY;
        private Vector2 dash;

        private void Awake()
        {
            actor = GetComponent<EnemyActor>();
            body = GetComponent<Rigidbody2D>();
            visual = GetComponent<SpriteRenderer>();
            idle = CharacterArt.Idle(EnemyKind.BlackShadowKid);
            charge = CharacterArt.Frames(EnemyKind.BlackShadowKid, CharacterArt.Pose.Charge);
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
                    if (seen && elapsed >= 0.35f) Enter(Phase.Windup);
                    break;
                case Phase.Windup:
                    Hover(player, seen);
                    body.linearVelocity *= 0.4f;
                    if (seen) Face(player.position.x - transform.position.x);
                    if (elapsed >= WindupSeconds)
                    {
                        Vector2 target = player != null
                            ? (Vector2)player.position + Vector2.up * 0.4f
                            : (Vector2)transform.position + Vector2.right;
                        dash = target - (Vector2)transform.position;
                        if (dash.sqrMagnitude < 0.04f) dash = new Vector2(visual.flipX ? -1f : 1f, -0.2f);
                        dash.Normalize();
                        Enter(Phase.Dash);
                        GameAudio.Play(GameAudio.Id.Dash, 0.75f);
                    }
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
            ShowFrame();
        }

        private void ShowFrame()
        {
            if (charge == null || charge.Length == 0 || phase == Phase.Idle || phase == Phase.Recover)
            {
                visual.sprite = idle;
                return;
            }

            int index = 0;
            if (phase == Phase.Dash)
            {
                float t = Mathf.Clamp01(elapsed / DashSeconds);
                index = 1 + Mathf.Min(2, Mathf.FloorToInt(t * 3f));
            }

            visual.sprite = charge[Mathf.Min(index, charge.Length - 1)];
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
