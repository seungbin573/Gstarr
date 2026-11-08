using Gstarr.Game;
using Gstarr.World;
using UnityEngine;

namespace Gstarr.Combat
{
    /// <summary>Standing rabbit: telegraph, dash, then rest. Only dash contact deals damage.</summary>
    [RequireComponent(typeof(EnemyActor))]
    [DefaultExecutionOrder(80)]
    public sealed class NormalBunny : MonoBehaviour
    {
        private enum Phase { Idle, Windup, Dash, Recover }
        private const float WindupSeconds = 0.42f;
        private const float NormalDashSpeed = 22f;
        private const float DistortedDashSpeed = 32f;
        private const float TwinDashSpeed = 12f;
        private const float DashSeconds = 0.16f;
        // 12 × 0.2 = 2.4. 예전 15 × 0.16 과 같은 거리이고 속도만 느리다.
        private const float TwinDashSeconds = 0.2f;
        private const float RecoverSeconds = 0.7f;
        private const float PopSeconds = 0.28f;
        private const float SplitScale = 0.72f;
        private EnemyActor actor;
        private Health health;
        private Rigidbody2D body;
        private SpriteRenderer visual;
        private Sprite idle;
        private Sprite windupLead;
        private Sprite[] charge;
        private Phase phase;
        private float elapsed;
        private int direction = 1;
        private bool hit;
        private bool twin;
        private bool distorted;
        private int dashDamage = 1;
        private bool split;
        private bool splitAfterGlory;
        private float dashSpeed = NormalDashSpeed;
        private float dashDuration = DashSeconds;
        private float gravity;
        private float popLeft;
        private float popPush;
        private float bodyScale;

        private void Awake()
        {
            actor = GetComponent<EnemyActor>();
            health = GetComponent<Health>();
            body = GetComponent<Rigidbody2D>();
            visual = GetComponent<SpriteRenderer>();
            twin = actor.Kind == EnemyKind.TwinBunny;
            distorted = actor.Kind == EnemyKind.DistortedBunny;
            dashSpeed = twin ? TwinDashSpeed : distorted ? DistortedDashSpeed : NormalDashSpeed;
            dashDuration = twin ? TwinDashSeconds : DashSeconds;
            dashDamage = 1;
            var art = twin ? EnemyKind.TwinBunny : distorted ? EnemyKind.DistortedBunny : EnemyKind.Bunny;
            idle = CharacterArt.Idle(art);
            windupLead = CharacterArt.WindupLead(art);
            charge = CharacterArt.Frames(art, CharacterArt.Pose.Charge);
            gravity = body.gravityScale;
            if (health != null && health.onDeath != null) health.onDeath.AddListener(OnDied);
            Enter(Phase.Idle);
        }

        private void OnDestroy()
        {
            if (health != null && health.onDeath != null) health.onDeath.RemoveListener(OnDied);
        }

        private void FixedUpdate()
        {
            actor.ClampRoom();
            if (popLeft > 0f)
            {
                body.linearVelocity = new Vector2(popPush, body.linearVelocity.y);
                return;
            }

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
                        if (phase == Phase.Dash) GameAudio.Play(GameAudio.Id.Dash, distorted ? 0.85f : 0.6f);
                    }
                    break;
                case Phase.Dash:
                    if (elapsed >= dashDuration || !CanDash()) Enter(Phase.Recover);
                    break;
                case Phase.Recover:
                    if (elapsed >= RecoverSeconds) Enter(Phase.Idle);
                    break;
            }

            body.linearVelocity = phase == Phase.Dash
                ? new Vector2(direction * dashSpeed, 0f)
                : new Vector2(0f, body.linearVelocity.y);
            ShowFrame();
        }

        private void Update()
        {
            if (splitAfterGlory && !GamePause.IsGloryKill)
            {
                splitAfterGlory = false;
                PerformSplit();
            }

            if (popLeft <= 0f) return;
            popLeft -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(popLeft / PopSeconds);
            float s = Mathf.Lerp(bodyScale * 0.2f, bodyScale, Mathf.SmoothStep(0f, 1f, t));
            transform.localScale = Vector3.one * s;
            if (popLeft <= 0f)
            {
                transform.localScale = Vector3.one * bodyScale;
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            }
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
            if (phase == Phase.Windup && windupLead != null && elapsed < WindupSeconds * 0.4f)
            {
                visual.sprite = windupLead;
                return;
            }

            // 준비는 웅크린 채 정지. 돌진은 작은 자세를 한 번 보여주고 큰 자세를 유지한다.
            int index = 0;
            if (phase == Phase.Dash) index = elapsed < dashDuration * 0.4f ? 1 : 2;
            visual.sprite = (phase == Phase.Windup || phase == Phase.Dash) && charge.Length > 0
                ? charge[Mathf.Min(index, charge.Length - 1)] : idle;
        }

        /// <summary>분열로 태어난 토끼. 작게 튀어나온 뒤 노말 토끼처럼 움직인다.</summary>
        public void PresentSplit(float scale, int face)
        {
            bodyScale = scale;
            popLeft = PopSeconds;
            popPush = face * 2.8f;
            transform.localScale = Vector3.one * (scale * 0.2f);
            if (visual != null) visual.flipX = face < 0;
            direction = face < 0 ? -1 : 1;
            if (body != null) body.linearVelocity = new Vector2(popPush, 3.2f);
        }

        private void OnDied()
        {
            if (!twin || split) return;
            split = true;
            if (GamePause.IsGloryKill)
            {
                splitAfterGlory = true;
                return;
            }

            PerformSplit();
        }

        private void PerformSplit()
        {
            Vector3 origin = transform.position;
            CombatFeel.Shake(0.14f, 0.14f);
            CombatFeel.Burst(origin, new Color(1f, 0.74f, 0.84f), 12);
            GameAudio.Play(GameAudio.Id.Dash, 0.55f);
            SplitGhost.Spawn(origin, visual != null ? visual.sprite : idle);
            SpawnChild(origin + Vector3.left * 0.85f, -1);
            SpawnChild(origin + Vector3.right * 0.85f, 1);
        }

        private void SpawnChild(Vector3 position, int face)
        {
            var child = EnemyActor.Spawn(null, position, EnemyKind.Bunny, 3);
            if (child == null) return;
            child.CopyLeashFrom(actor);
            child.SkipGloryKill();
            ArenaDoorSpawner.Adopt(child);
            child.GetComponent<NormalBunny>()?.PresentSplit(SplitScale, face);
        }

        private sealed class SplitGhost : MonoBehaviour
        {
            private SpriteRenderer view;
            private float life = 0.32f;
            private int face;

            public static void Spawn(Vector3 origin, Sprite sprite)
            {
                if (sprite == null) return;
                for (int i = 0; i < 2; i++)
                {
                    int face = i == 0 ? -1 : 1;
                    var go = new GameObject("TwinSplit");
                    go.transform.position = origin;
                    var view = go.AddComponent<SpriteRenderer>();
                    view.sprite = sprite;
                    view.flipX = face < 0;
                    view.sortingOrder = 14;
                    var ghost = go.AddComponent<SplitGhost>();
                    ghost.view = view;
                    ghost.face = face;
                }
            }

            private void Update()
            {
                life -= Time.deltaTime;
                float t = 1f - Mathf.Clamp01(life / 0.32f);
                transform.position += new Vector3(face * 3.4f, 0.55f, 0f) * Time.deltaTime;
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.45f, t);
                var color = view.color;
                color.a = 1f - t;
                view.color = color;
                if (life <= 0f) Destroy(gameObject);
            }
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
                health.TakeDamage(dashDamage, new Vector2(direction, 0.35f));
                return;
            }
            // Ground contact is harmless; a wall ends the dash immediately.
            for (int i = 0; i < collision.contactCount; i++)
                if (collision.GetContact(i).normal.x * direction < -0.5f)
                {
                    Enter(Phase.Recover);
                    return;
                }
        }
    }
}
