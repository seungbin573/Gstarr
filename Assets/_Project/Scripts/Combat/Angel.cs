using Gstarr.Game;
using Gstarr.World;
using UnityEngine;

namespace Gstarr.Combat
{
    /// <summary>석상. 점프 없이 바닥을 미끄러지듯 직진하고, 정면으로 닿을 때만 피해를 준다.</summary>
    [RequireComponent(typeof(EnemyActor))]
    [DefaultExecutionOrder(80)]
    public sealed class Angel : MonoBehaviour
    {
        private enum Phase { Idle, Windup, Dash, Recover }
        private const float WindupSeconds = 0.56f;
        private const float DashSpeed = 16f;
        private const float DashSeconds = 0.2f;
        private const float RecoverSeconds = 0.75f;

        private EnemyActor actor;
        private Rigidbody2D body;
        private SpriteRenderer visual;
        private Sprite idle;
        private Sprite[] charge;
        private Phase phase;
        private float elapsed;
        private int direction = 1;
        private bool hit;
        private float gravity;

        private void Awake()
        {
            actor = GetComponent<EnemyActor>();
            body = GetComponent<Rigidbody2D>();
            visual = GetComponent<SpriteRenderer>();
            idle = CharacterArt.Idle(EnemyKind.Angel);
            charge = CharacterArt.Frames(EnemyKind.Angel, CharacterArt.Pose.Charge);
            gravity = body.gravityScale;
            Enter(Phase.Idle);
        }

        private void FixedUpdate()
        {
            actor.ClampRoom();
            if (!actor.CanAct)
            {
                Enter(Phase.Idle);
                return;
            }

            elapsed += Time.fixedDeltaTime;
            var player = actor.PlayerTarget;
            switch (phase)
            {
                case Phase.Idle:
                    if (SeesPlayer(player))
                    {
                        direction = player.position.x >= transform.position.x ? 1 : -1;
                        visual.flipX = direction < 0;
                        Enter(Phase.Windup);
                    }
                    break;
                case Phase.Windup:
                    if (elapsed >= WindupSeconds)
                    {
                        Enter(CanDash() ? Phase.Dash : Phase.Recover);
                        if (phase == Phase.Dash) GameAudio.Play(GameAudio.Id.Dash, 0.45f);
                    }
                    break;
                case Phase.Dash:
                    if (elapsed >= DashSeconds || !CanDash()) Enter(Phase.Recover);
                    break;
                case Phase.Recover:
                    if (elapsed >= RecoverSeconds) Enter(Phase.Idle);
                    break;
            }

            body.linearVelocity = phase == Phase.Dash
                ? new Vector2(direction * DashSpeed, 0f)
                : new Vector2(0f, body.linearVelocity.y);
            ShowFrame();
        }

        private bool SeesPlayer(Transform player)
        {
            if (player == null) return false;
            if (ArenaDoorSpawner.SeparatedByDoor(transform.position.x, player.position.x)) return false;
            if (actor.HasLeash)
                return player.position.x >= actor.RoomMin && player.position.x <= actor.RoomMax;
            return Mathf.Abs(player.position.x - transform.position.x) <= 8.5f
                && Mathf.Abs(player.position.y - transform.position.y) <= 1.5f;
        }

        private bool CanDash() => actor.RoomAhead(direction) && !actor.PitInFront(direction);

        private void Enter(Phase next)
        {
            phase = next;
            elapsed = 0f;
            if (next == Phase.Dash) hit = false;
            body.gravityScale = next == Phase.Dash ? 0f : gravity;
            body.constraints = RigidbodyConstraints2D.FreezeRotation
                | (next == Phase.Dash ? RigidbodyConstraints2D.None : RigidbodyConstraints2D.FreezePositionX);
            if (next != Phase.Dash) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            ShowFrame();
        }

        private void ShowFrame()
        {
            if (charge == null || charge.Length == 0 || phase == Phase.Idle || phase == Phase.Recover)
            {
                visual.sprite = idle;
                return;
            }

            int index = phase == Phase.Dash
                ? charge.Length - 1
                : elapsed < WindupSeconds * 0.45f ? 0 : 1;
            visual.sprite = charge[Mathf.Min(index, charge.Length - 1)];
        }

        private bool StruckFromFront(Collision2D collision)
        {
            var target = actor.PlayerTarget;
            if (target != null && (target.position.x - transform.position.x) * direction < 0.05f)
                return false;
            for (int i = 0; i < collision.contactCount; i++)
                if (collision.GetContact(i).normal.x * direction < -0.4f)
                    return true;
            return false;
        }

        private void OnCollisionEnter2D(Collision2D collision) => Touch(collision);
        private void OnCollisionStay2D(Collision2D collision) => Touch(collision);

        private void Touch(Collision2D collision)
        {
            if (phase != Phase.Dash || !actor.CanAct) return;
            var target = actor.PlayerTarget;
            var health = collision.collider.GetComponentInParent<Health>();
            if (target != null && health != null && health == target.GetComponent<Health>())
            {
                if (hit || !StruckFromFront(collision)) return;
                hit = true;
                health.TakeDamage(1, new Vector2(direction, 0.35f));
                return;
            }

            for (int i = 0; i < collision.contactCount; i++)
                if (collision.GetContact(i).normal.x * direction < -0.5f)
                {
                    Enter(Phase.Recover);
                    return;
                }
        }
    }
}
