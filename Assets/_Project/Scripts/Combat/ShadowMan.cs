using Gstarr.Player;
using Gstarr.World;
using UnityEngine;

namespace Gstarr.Combat
{
    /// <summary>바닥을 천천히 걷는다. 가까이 오거나 맞으면 쫓아와 팔로 붙잡는다.</summary>
    [RequireComponent(typeof(EnemyActor))]
    [DefaultExecutionOrder(80)]
    public sealed class ShadowMan : MonoBehaviour
    {
        private enum Phase { Walk, Reach, Grab, Recover }
        private const float CalmSpeed = 1.6f;
        private const float ChaseSpeed = 3.1f;
        private const float NoticeRange = 5.2f;
        private const float ReachSeconds = 0.48f;
        private const float GrabSeconds = 0.75f;
        private const float RecoverSeconds = 0.85f;
        private const float WalkFrameSeconds = 0.24f;
        private const int GrabDamage = 3;

        private EnemyActor actor;
        private Health health;
        private Rigidbody2D body;
        private SpriteRenderer visual;
        private Sprite[] walk;
        private Sprite[] reach;
        private Phase phase;
        private float elapsed;
        private int direction = 1;
        private int noticedHealth;
        private bool aggro;
        private bool grabbed;

        private void Awake()
        {
            actor = GetComponent<EnemyActor>();
            health = GetComponent<Health>();
            body = GetComponent<Rigidbody2D>();
            visual = GetComponent<SpriteRenderer>();
            walk = CharacterArt.Frames(EnemyKind.Shadowman, CharacterArt.Pose.Idle);
            reach = CharacterArt.Frames(EnemyKind.Shadowman, CharacterArt.Pose.Grab);
            noticedHealth = health != null ? health.Current : 0;
            if (health != null && health.onHealthChanged != null)
                health.onHealthChanged.AddListener(OnHurt);
            Enter(Phase.Walk);
        }

        private void OnDestroy()
        {
            if (health != null && health.onHealthChanged != null)
                health.onHealthChanged.RemoveListener(OnHurt);
        }

        private void OnHurt(int current, int max)
        {
            if (current < noticedHealth) aggro = true;
            noticedHealth = current;
        }

        private void FixedUpdate()
        {
            actor.ClampRoom();
            if (!actor.CanAct)
            {
                Enter(Phase.Walk);
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            var player = actor.PlayerTarget;
            if (!aggro && Near(player)) aggro = true;
            elapsed += Time.fixedDeltaTime;
            body.gravityScale = 4f;

            switch (phase)
            {
                case Phase.Walk:
                    StepWalk(player);
                    if (aggro && InReach(player)) Enter(Phase.Reach);
                    break;
                case Phase.Reach:
                    Hold();
                    FacePlayer(player);
                    if (!grabbed && elapsed >= ReachSeconds * 0.62f && InReach(player))
                        Grab(player);
                    if (elapsed >= ReachSeconds)
                        Enter(grabbed ? Phase.Grab : Phase.Recover);
                    break;
                case Phase.Grab:
                    Hold();
                    if (elapsed >= GrabSeconds) Enter(Phase.Recover);
                    break;
                case Phase.Recover:
                    Hold();
                    if (elapsed >= RecoverSeconds) Enter(Phase.Walk);
                    break;
            }

            ShowFrame();
        }

        private void StepWalk(Transform player)
        {
            int move = direction;
            if (aggro && player != null)
            {
                move = player.position.x >= transform.position.x ? 1 : -1;
                direction = move;
            }

            if (!actor.RoomAhead(move) || actor.PitInFront(move))
            {
                if (!aggro) direction = -direction;
                move = 0;
            }

            visual.flipX = direction < 0;
            bool walking = move != 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation
                | (walking ? RigidbodyConstraints2D.None : RigidbodyConstraints2D.FreezePositionX);
            body.linearVelocity = new Vector2(walking ? move * (aggro ? ChaseSpeed : CalmSpeed) : 0f, body.linearVelocity.y);
        }

        private void Hold()
        {
            body.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }

        private void FacePlayer(Transform player)
        {
            if (player == null) return;
            direction = player.position.x >= transform.position.x ? 1 : -1;
            visual.flipX = direction < 0;
        }

        private bool Near(Transform player)
        {
            if (player == null) return false;
            if (ArenaDoorSpawner.SeparatedByDoor(transform.position.x, player.position.x)) return false;
            if (actor.HasLeash && (player.position.x < actor.RoomMin || player.position.x > actor.RoomMax))
                return false;
            return Mathf.Abs(player.position.x - transform.position.x) <= NoticeRange
                && Mathf.Abs(player.position.y - transform.position.y) <= 1.8f;
        }

        private bool InReach(Transform player)
        {
            if (!Near(player)) return false;
            float dx = player.position.x - transform.position.x;
            return dx * direction > 0.2f
                && Mathf.Abs(dx) <= 2.8f
                && Mathf.Abs(player.position.y - transform.position.y) <= 1.5f;
        }

        private void Grab(Transform player)
        {
            grabbed = true;
            var control = player.GetComponent<PlayerController>();
            control?.GrabbedBy(transform, new Vector3(direction * 1.85f, 1.55f, 0f), GrabSeconds);
            player.GetComponent<Health>()?.TakeDamage(GrabDamage, new Vector2(direction, 0.25f));
        }

        private void Enter(Phase next)
        {
            if (phase == next && next == Phase.Walk) return;
            phase = next;
            elapsed = 0f;
            if (next != Phase.Reach) grabbed = false;
            ShowFrame();
        }

        private void ShowFrame()
        {
            if ((phase == Phase.Reach || phase == Phase.Grab) && reach != null && reach.Length > 0)
            {
                float t = phase == Phase.Grab ? 1f : Mathf.Clamp01(elapsed / ReachSeconds);
                int index = Mathf.Min(reach.Length - 1, Mathf.FloorToInt(t * reach.Length));
                visual.sprite = reach[index];
                return;
            }

            if (walk == null || walk.Length == 0) return;
            bool moving = phase == Phase.Walk && Mathf.Abs(body.linearVelocity.x) > 0.1f;
            int frame = moving ? Mathf.FloorToInt(Time.time / WalkFrameSeconds) % walk.Length : 0;
            visual.sprite = walk[frame];
        }
    }
}
