using Gstarr.Game;
using Gstarr.World;
using UnityEngine;

namespace Gstarr.Combat
{
    /// <summary>멀리서 공을 던진다. 공은 피해가 없고, 맞으면 화면을 기괴한 얼굴로 덮는다. 다가오면 물러난다.</summary>
    [RequireComponent(typeof(EnemyActor))]
    [DefaultExecutionOrder(80)]
    public sealed class AnnoyingKid : MonoBehaviour
    {
        private enum Phase { Idle, Throw, Laugh }
        private const float ThrowSeconds = 0.38f;
        private const float LaughSeconds = 0.42f;
        private const float IdleGap = 0.85f;
        private const float IdleFrameSeconds = 0.52f;
        private const float TooClose = 5.6f;
        private const float RetreatSpeed = 8.4f;
        private const float HoverLift = 2.15f;

        private EnemyActor actor;
        private Rigidbody2D body;
        private SpriteRenderer visual;
        private Sprite[] idle;
        private Sprite[] throwing;
        private Sprite laugh;
        private Sprite[] balls;
        private SpriteRenderer heldBall;
        private Phase phase;
        private float elapsed;
        private float cruiseY;
        private int direction = 1;

        private void Awake()
        {
            actor = GetComponent<EnemyActor>();
            body = GetComponent<Rigidbody2D>();
            visual = GetComponent<SpriteRenderer>();
            idle = CharacterArt.Frames(EnemyKind.AnnoyingKid, CharacterArt.Pose.Idle);
            throwing = CharacterArt.Frames(EnemyKind.AnnoyingKid, CharacterArt.Pose.Throw);
            laugh = CharacterArt.Idle(EnemyKind.AnnoyingKid);
            var laughs = CharacterArt.Frames(EnemyKind.AnnoyingKid, CharacterArt.Pose.Laugh);
            if (laughs != null && laughs.Length > 0) laugh = laughs[0];
            balls = CharacterArt.Frames(EnemyKind.AnnoyingKid, CharacterArt.Pose.Shot);
            var ballObject = new GameObject("HeldBall");
            ballObject.transform.SetParent(transform, false);
            heldBall = ballObject.AddComponent<SpriteRenderer>();
            heldBall.sortingOrder = 9;
            heldBall.enabled = false;
            if (balls != null && balls.Length > 0) heldBall.sprite = balls[0];
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
            if (seen)
            {
                direction = player.position.x >= transform.position.x ? 1 : -1;
                visual.flipX = direction < 0;
            }

            bool crowded = seen && Mathf.Abs(player.position.x - transform.position.x) < TooClose;
            int away = -direction;
            bool retreat = crowded && actor.RoomAhead(away);
            float vx = retreat ? away * RetreatSpeed : 0f;
            float targetY = cruiseY + Mathf.Sin(Time.time * 1.6f) * 0.35f;
            if (seen)
            {
                float above = player.position.y + HoverLift;
                if (crowded)
                {
                    bool goUp = transform.position.y >= player.position.y - 0.2f;
                    above = player.position.y + (goUp ? 3.6f : -1.8f);
                    if (!retreat) above = player.position.y + (goUp ? 4.2f : -2.2f);
                }
                targetY = above + Mathf.Sin(Time.time * 2.4f) * 0.28f;
            }
            targetY = Mathf.Clamp(targetY, cruiseY - 2.4f, cruiseY + 3.4f);
            float lift = crowded ? 4.8f : 2.6f;
            float vy = Mathf.Clamp(targetY - transform.position.y, -2.8f, 2.8f) * lift;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.linearVelocity = new Vector2(vx, vy);

            elapsed += Time.fixedDeltaTime;
            switch (phase)
            {
                case Phase.Idle:
                    if (seen && !crowded && elapsed >= IdleGap) Enter(Phase.Throw);
                    break;
                case Phase.Throw:
                    if (elapsed >= ThrowSeconds)
                    {
                        ThrowBall(player);
                        Enter(Phase.Laugh);
                    }
                    break;
                case Phase.Laugh:
                    if (elapsed >= LaughSeconds) Enter(Phase.Idle);
                    break;
            }

            ShowFrame();
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

        private void Enter(Phase next)
        {
            if (phase == next) return;
            phase = next;
            elapsed = 0f;
            ShowFrame();
        }

        private void ShowFrame()
        {
            if (phase == Phase.Throw && throwing != null && throwing.Length > 0)
            {
                int index = Mathf.Min(throwing.Length - 1, Mathf.FloorToInt(elapsed / ThrowSeconds * throwing.Length));
                visual.sprite = throwing[index];
                PlaceHeldBall(throwing.Length == 1 ? 1f : index / (float)(throwing.Length - 1));
                return;
            }

            if (heldBall != null) heldBall.enabled = false;

            if (phase == Phase.Laugh && laugh != null)
            {
                visual.sprite = laugh;
                return;
            }

            if (idle == null || idle.Length == 0) return;
            int frame = Mathf.FloorToInt(Time.time / IdleFrameSeconds) % idle.Length;
            visual.sprite = idle[frame];
        }

        private void PlaceHeldBall(float along)
        {
            if (heldBall == null) return;
            heldBall.enabled = true;
            if (balls != null && balls.Length > 1) heldBall.sprite = balls[1];
            else if (balls != null && balls.Length > 0) heldBall.sprite = balls[0];
            heldBall.flipX = visual.flipX;
            float x = Mathf.Lerp(0.22f, 0.92f, along);
            float y = Mathf.Lerp(0.42f, 0.58f, along);
            heldBall.transform.localPosition = new Vector3(direction * x, y, 0f);
        }

        private const float BallGravity = -14f;
        private const float ThrowSpeed = 9.6f;

        private void ThrowBall(Transform player)
        {
            if (player == null) return;
            PlaceHeldBall(1f);
            Vector2 origin = heldBall != null ? (Vector2)heldBall.transform.position : (Vector2)transform.position;
            Vector2 target = (Vector2)player.position + Vector2.up * 0.45f;
            Vector2 to = target - origin;
            if (to.sqrMagnitude < 0.01f) to = new Vector2(direction, -0.2f);
            float travel = ArenaDoorSpawner.RangeUntilDoor(origin.x, to.x, 12f);
            if (travel < 0.4f) return;

            Sprite shot = balls != null && balls.Length > 0 ? balls[0] : visual.sprite;
            var go = new GameObject("KidBall");
            go.transform.position = origin;
            var view = go.AddComponent<SpriteRenderer>();
            view.sprite = shot;
            view.sortingOrder = 8;
            var projectile = go.AddComponent<Projectile>();
            projectile.SetAnimation(shot != null ? new[] { shot } : balls);
            int playerLayer = player.gameObject.layer;
            Vector2 velocity = Ballistic(origin, target);
            float flight = Mathf.Abs(to.x) / Mathf.Max(0.01f, Mathf.Abs(velocity.x));
            float untilDoor = travel / Mathf.Max(0.01f, Mathf.Abs(velocity.x));
            float life = Mathf.Min(flight + 0.35f, untilDoor);
            var scare = CharacterArt.Frames(EnemyKind.AnnoyingKid, CharacterArt.Pose.Scare);
            projectile.Launch(velocity, 0, life * velocity.magnitude, 1 << playerLayer, gameObject, onHit: _ =>
                FoeEffects.DistractionScare(player, scare));
            projectile.SetGravity(new Vector2(0f, BallGravity));
            GameAudio.Play(GameAudio.Id.EnemyShot, 0.7f);
        }

        /// <summary>던지는 순간의 위치에 떨어지게 속도를 정한다. 날아가는 동안은 방향을 바꾸지 않는다.</summary>
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
    }
}
