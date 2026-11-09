using System.Collections.Generic;
using Gstarr.Game;
using Gstarr.World;
using UnityEngine;

namespace Gstarr.Combat
{
    /// <summary>석상. 바닥을 미끄러지듯 직진하고, 정면으로 닿을 때만 피해를 준다. 최종 보스는 손 공격, 눈·총손 소환과 랜덤 몹 소환을 돌진 사이에 잇는다.</summary>
    [RequireComponent(typeof(EnemyActor))]
    [DefaultExecutionOrder(80)]
    public sealed class Angel : MonoBehaviour
    {
        private enum Phase { Idle, Windup, Dash, Pause, Quake, Summon, Recover }
        private enum Pattern { Slide, Quake, GunHands, Eyes, PullMobs, MultiHands, DoubleSlide, Reverse }
        private const float WindupSeconds = 0.56f;
        private const float QuakeWindupSeconds = 0.9f;
        private const float DashSpeed = 16f;
        private const float SecondDashSpeed = 21f;
        private const float DashSeconds = 0.2f;
        private const float RecoverSeconds = 0.75f;
        private const int QuakePulses = 5;

        private EnemyActor actor;
        private Health health;
        private Rigidbody2D body;
        private SpriteRenderer visual;
        private Sprite idle;
        private Sprite[] charge;
        private Phase phase;
        private Pattern pattern = Pattern.Slide;
        private float elapsed;
        private float dashSpeed = DashSpeed;
        private float pulseClock;
        private int direction = 1;
        private int quakeStep;
        private bool boss;
        private bool phase2;
        private bool secondDash;
        private bool hit;
        private float gravity;

        private readonly List<GameObject> summons = new List<GameObject>();
        private readonly List<GameObject> effects = new List<GameObject>();
        private static readonly EnemyKind[] RandomMobs = {
            EnemyKind.Bunny, EnemyKind.DistortedBunny, EnemyKind.AnnoyingKid,
            EnemyKind.BlackShadowKid, EnemyKind.BlackShadowThrow
        };

        public void TrackEffect(GameObject effect)
        {
            effects.RemoveAll(item => item == null);
            effects.Add(effect);
        }

        private void ClearSummons()
        {
            foreach (var summon in summons) if (summon != null) { summon.SetActive(false); Destroy(summon); }
            foreach (var effect in effects) if (effect != null) Destroy(effect);
            summons.Clear();
            effects.Clear();
        }

        private void OnDestroy() => ClearSummons();

        private bool SummonPattern => pattern == Pattern.GunHands || pattern == Pattern.Eyes || pattern == Pattern.PullMobs;

        public void BecomeBoss() => boss = true;

        private void Awake()
        {
            actor = GetComponent<EnemyActor>();
            health = GetComponent<Health>();
            body = GetComponent<Rigidbody2D>();
            visual = GetComponent<SpriteRenderer>();
            idle = CharacterArt.Idle(EnemyKind.Angel);
            charge = CharacterArt.Frames(EnemyKind.Angel, CharacterArt.Pose.Charge);
            gravity = body.gravityScale;
            health.onDeath.AddListener(ClearSummons);
            Enter(Phase.Idle);
        }

        private void FixedUpdate()
        {
            actor.ClampRoom();
            if (!actor.CanAct)
            {
                secondDash = false;
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
                    if (elapsed >= WindupTime)
                    {
                        if (boss && (pattern == Pattern.Quake || pattern == Pattern.MultiHands)) Enter(Phase.Quake);
                        else if (boss && SummonPattern) Enter(Phase.Summon);
                        else Enter(CanDash() ? Phase.Dash : Phase.Recover);
                        if (phase == Phase.Dash) GameAudio.Play(GameAudio.Id.Dash, 0.45f);
                    }
                    break;
                case Phase.Dash:
                    if (elapsed < DashTime && CanDash()) break;
                    if (boss && !secondDash && CanDash() && (pattern == Pattern.DoubleSlide || pattern == Pattern.Reverse))
                    {
                        if (pattern == Pattern.Reverse)
                        {
                            direction = -direction;
                            visual.flipX = direction < 0;
                        }
                        Enter(Phase.Pause);
                    }
                    else Enter(Phase.Recover);
                    break;
                case Phase.Pause:
                    if (elapsed < 0.2f) break;
                    secondDash = true;
                    Enter(Phase.Dash);
                    GameAudio.Play(GameAudio.Id.Dash, 0.55f);
                    break;
                case Phase.Quake:
                    pulseClock += Time.fixedDeltaTime;
                    if (pulseClock < PulseGap) break;
                    pulseClock = 0f;
                    if (!EmitPulse(quakeStep))
                    {
                        Enter(Phase.Recover);
                        break;
                    }
                    quakeStep++;
                    if (quakeStep >= PulseCount) Enter(Phase.Recover);
                    break;
                case Phase.Summon:
                    if (elapsed >= 0.85f) Enter(Phase.Recover);
                    break;
                case Phase.Recover:
                    if (elapsed >= RecoverSeconds) FinishRecover();
                    break;
            }

            body.linearVelocity = phase == Phase.Dash
                ? new Vector2(direction * dashSpeed, 0f)
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

        private float WindupTime => boss && (pattern == Pattern.Quake || pattern == Pattern.MultiHands || SummonPattern) ? QuakeWindupSeconds : WindupSeconds;
        private float DashTime => phase2 && pattern == Pattern.Slide ? 0.28f : DashSeconds;
        private float PulseGap => pattern == Pattern.MultiHands ? 0.48f : 0.3f;
        private int PulseCount => pattern == Pattern.MultiHands ? (phase2 ? 5 : 3) : QuakePulses;

        public void BeginPhaseTwo()
        {
            phase2 = true;
            secondDash = false;
            pattern = Pattern.Slide;
            GetComponent<BossMark>()?.EnterPhase(2);
            if (visual != null) visual.color = new Color(1f, 0.78f, 0.62f);
            Enter(Phase.Recover);
        }

        public void ResetPhaseOne()
        {
            ClearSummons();
            phase2 = false;
            secondDash = false;
            pattern = Pattern.Slide;
            if (visual != null) visual.color = Color.white;
            Enter(Phase.Idle);
        }

        private void FinishRecover()
        {
            secondDash = false;
            if (boss)
            {
                pattern = pattern switch
                {
                    Pattern.Slide => Pattern.Quake,
                    Pattern.Quake => Pattern.GunHands,
                    Pattern.GunHands => Pattern.Eyes,
                    Pattern.Eyes => Pattern.PullMobs,
                    Pattern.PullMobs => Pattern.MultiHands,
                    Pattern.MultiHands => phase2 ? Pattern.Reverse : Pattern.DoubleSlide,
                    Pattern.Reverse => Pattern.DoubleSlide,
                    _ => Pattern.Slide
                };
            }
            Enter(Phase.Idle);
        }

        private bool EmitPulse(int step)
        {
            bool kept = PlacePulse(direction, step);
            if (phase2 && step == 0) PlacePulse(-direction, 0);
            return kept;
        }

        private bool PlacePulse(int dir, int step)
        {
            var player = actor.PlayerTarget;
            float x = pattern == Pattern.MultiHands && player != null
                ? player.position.x
                : transform.position.x + dir * (1.15f + step * 1.4f);
            if (actor.HasLeash && (x < actor.RoomMin + 0.5f || x > actor.RoomMax - 0.5f)) return false;
            AngelHandStrike.Spawn(this, new Vector2(x, actor.GroundY),
                pattern == Pattern.MultiHands ? CharacterArt.MultiHand : CharacterArt.HandSkill);
            return true;
        }

        private Vector2 SummonPosition(float side, bool flying)
        {
            float x = transform.position.x + side * 2.5f;
            if (actor.HasLeash) x = Mathf.Clamp(x, actor.RoomMin + 1f, actor.RoomMax - 1f);
            return new Vector2(x, actor.GroundY + (flying ? 1.6f : 0.1f));
        }

        private void SpawnMinion(EnemyKind kind, Vector2 position)
        {
            summons.RemoveAll(item => item == null);
            if (summons.Count >= 5) return;
            var mob = EnemyActor.Spawn(null, position, kind, 3);
            mob.CopyLeashFrom(actor);
            var controller = mob.GetComponent<AngelSummon>();
            if (controller != null) controller.Owner = this;
            summons.Add(mob.gameObject);
        }

        public void BringRandomMob(Vector2 position)
        {
            if (!actor.CanAct) return;
            SpawnMinion(RandomMobs[Random.Range(0, RandomMobs.Length)], position + Vector2.up * 0.1f);
        }

        private void SummonWave()
        {
            if (pattern == Pattern.PullMobs)
            {
                AngelHandStrike.Spawn(this, SummonPosition(direction, false), CharacterArt.PullHand, true);
                return;
            }
            if (pattern == Pattern.GunHands)
            {
                SpawnMinion(EnemyKind.AngelGunHand, SummonPosition(direction, true));
                if (phase2) SpawnMinion(EnemyKind.AngelGunHand, SummonPosition(-direction, true));
            }
            else
            {
                SpawnMinion(EnemyKind.AngelEye, SummonPosition(direction, true));
                SpawnMinion(EnemyKind.AngelEyeTwo, SummonPosition(-direction, true));
            }
        }

        private void Enter(Phase next)
        {
            phase = next;
            elapsed = 0f;
            if (next == Phase.Dash)
            {
                hit = false;
                dashSpeed = DashSpeed;
                if (boss && phase2 && pattern == Pattern.Slide) dashSpeed = 20f;
                if (boss && pattern == Pattern.Reverse && secondDash) dashSpeed = 18f;
                if (boss && pattern == Pattern.DoubleSlide && secondDash) dashSpeed = SecondDashSpeed;
            }
            if (next == Phase.Summon) SummonWave();
            if (next == Phase.Quake)
            {
                quakeStep = 0;
                pulseClock = 0f;
            }
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

            if (boss && (SummonPattern || pattern == Pattern.Quake || pattern == Pattern.MultiHands))
            {
                visual.sprite = charge[charge.Length - 1];
                return;
            }
            int index = phase == Phase.Dash
                ? charge.Length - 1
                : elapsed < WindupTime * 0.45f ? 0 : 1;
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
                health.TakeEnemyHit(1, new Vector2(direction, 0.35f));
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
