using Gstarr.Game;
using Gstarr.Player;
using UnityEngine;

namespace Gstarr.Combat
{
    /// <summary>2단계 보스. 1단계는 잡몹 소환, 2단계는 내려찍기와 휩쓸기를 사용한다.</summary>
    [RequireComponent(typeof(EnemyActor), typeof(Health), typeof(Rigidbody2D))]
    public class BossActor : MonoBehaviour
    {
        private enum Attack { Idle, SlamWindup, SweepWindup }

        [SerializeField] private float summonInterval = 5f;
        [SerializeField] private int maxMinions = 4;
        [SerializeField] private float attackInterval = 1.8f;
        [SerializeField] private float slamRadius = 2.6f;
        [SerializeField] private Vector2 sweepSize = new Vector2(4.6f, 1.5f);

        private Health health;
        private Transform player;
        private Vector3 baseScale;
        private int phase = 1;
        private int summonIndex;
        private int facing = 1;
        private float timer = 1.5f;
        private Attack attack;
        private bool nextSlam = true;
        private EnemyActor enemyActor;

        public int Phase => phase;
        public bool RequiresPhaseGlory => phase == 1;

        private void Awake()
        {
            health = GetComponent<Health>();
            enemyActor = GetComponent<EnemyActor>();
            baseScale = transform.localScale;
        }

        private void Update()
        {
            if (health == null || health.Current <= 0 || GamePause.BlocksGameplay || (enemyActor != null && enemyActor.IsStunned)) return;
            FindPlayer();

            if (phase == 1)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    SummonMinion();
                    timer = summonInterval;
                }
                return;
            }

            UpdatePhaseTwo();
        }

        private void UpdatePhaseTwo()
        {
            if (attack == Attack.Idle)
            {
                transform.localScale = baseScale;
                timer -= Time.deltaTime;
                if (timer > 0f || player == null) return;

                FacePlayer();
                attack = nextSlam ? Attack.SlamWindup : Attack.SweepWindup;
                timer = nextSlam ? 0.85f : 0.65f;
                nextSlam = !nextSlam;
            }

            timer -= Time.deltaTime;
            if (attack == Attack.SlamWindup)
            {
                float squash = Mathf.Lerp(1f, 0.58f, Mathf.Clamp01(1f - timer / 0.85f));
                transform.localScale = new Vector3(baseScale.x, baseScale.y * squash, baseScale.z);
            }
            else
            {
                float stretch = Mathf.Lerp(1f, 1.25f, Mathf.Clamp01(1f - timer / 0.65f));
                transform.localScale = new Vector3(baseScale.x * stretch, baseScale.y, baseScale.z);
            }

            if (timer > 0f) return;
            if (attack == Attack.SlamWindup) Slam();
            else Sweep();
            attack = Attack.Idle;
            timer = attackInterval;
            transform.localScale = baseScale;
        }

        private void Slam()
        {
            if (player == null || Vector2.Distance(transform.position, player.position) > slamRadius) return;
            DamagePlayer(new Vector2(facing, 1.1f));
        }

        private void Sweep()
        {
            Vector2 center = (Vector2)transform.position + new Vector2(facing * sweepSize.x * 0.5f, 0.35f);
            var hits = Physics2D.OverlapBoxAll(center, sweepSize, 0f);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].GetComponentInParent<PlayerController>() == null) continue;
                DamagePlayer(new Vector2(facing, 0.65f));
                return;
            }
        }

        private void DamagePlayer(Vector2 direction)
        {
            if (player == null) return;
            player.GetComponent<Health>()?.TakeDamage(2, direction);
        }

        private void SummonMinion()
        {
            var enemies = FindObjectsByType<EnemyActor>(FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i] != null && !enemies[i].IsBoss && !enemies[i].IsDead) count++;
            if (count >= maxMinions) return;

            EnemyKind[] kinds = { EnemyKind.BeatChaser, EnemyKind.RangedBeat, EnemyKind.RangedHalfBeat };
            EnemyKind kind = kinds[summonIndex++ % kinds.Length];
            int side = summonIndex % 2 == 0 ? -1 : 1;
            Vector2 probe = (Vector2)transform.position + new Vector2(side * 2.2f, 1f);
            int groundMask = LayerMask.GetMask("Ground");
            if (groundMask == 0) groundMask = Physics2D.DefaultRaycastLayers;
            RaycastHit2D floor = Physics2D.Raycast(probe, Vector2.down, 3.5f, groundMask);
            Vector3 spawnAt = floor.collider != null
                ? new Vector3(probe.x, floor.point.y, transform.position.z)
                : transform.position + new Vector3(side * 2.2f, 0f, 0f);
            EnemyActor.Spawn(null, spawnAt, kind, 12);
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
            facing = player.position.x >= transform.position.x ? 1 : -1;
            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * facing;
            transform.localScale = scale;
            baseScale.x = Mathf.Abs(baseScale.x) * facing;
        }

        public bool TryAdvancePhase()
        {
            if (phase != 1) return false;
            phase = 2;
            attack = Attack.Idle;
            nextSlam = true;
            timer = 1.4f;
            transform.localScale = baseScale;
            health.Heal(health.Max);
            return true;
        }

    }
}
