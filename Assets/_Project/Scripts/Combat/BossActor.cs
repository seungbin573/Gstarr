using Gstarr.Game;
using Gstarr.Player;
using UnityEngine;

namespace Gstarr.Combat
{
    /// <summary>1단계는 잡몹 소환과 강타, 2단계는 강타와 휩쓸기를 사용한다.</summary>
    [RequireComponent(typeof(EnemyActor), typeof(Health), typeof(Rigidbody2D))]
    public class BossActor : MonoBehaviour
    {
        private enum Attack { Idle, SlamWindup, SweepWindup }

        [SerializeField] private float summonInterval = 3.5f;
        [SerializeField] private int maxMinions = 4;
        [SerializeField] private float attackInterval = 1.1f;
        [SerializeField] private float phaseOneAttackInterval = 2.2f;
        [SerializeField] private float moveSpeed = 2.8f;
        [SerializeField] private float slamRadius = 1.8f;
        [SerializeField] private Vector2 sweepSize = new Vector2(5f, 0.85f);

        private Health health;
        private Transform player;
        private Vector3 baseScale;
        private int phase = 1;
        private int summonIndex;
        private int facing = 1;
        private float summonTimer = 1.2f;
        private float attackTimer = 0.7f;
        private Attack attack;
        private bool nextSlam = true;
        private EnemyActor enemyActor;
        private Rigidbody2D body;
        private Collider2D hitbox;
        private SpriteRenderer slamWarning;
        private SpriteRenderer sweepWarning;
        private Vector2 slamTarget;
        private int attackFacing = 1;
        private float arenaMinX;
        private float arenaMaxX;
        private bool hasArenaBounds;
        private bool encounterActive;
        private static Sprite warningCircleSprite;
        private static Sprite warningSquareSprite;

        public int Phase => phase;
        public bool RequiresPhaseGlory => phase == 1;

        private void Awake()
        {
            health = GetComponent<Health>();
            enemyActor = GetComponent<EnemyActor>();
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<Collider2D>();
            baseScale = transform.localScale;
            CreateWarnings();
        }

        private void Start()
        {
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

            if (phase == 1)
            {
                summonTimer -= Time.deltaTime;
                if (summonTimer <= 0f)
                {
                    SummonMinion();
                    summonTimer = summonInterval;
                }
            }

            UpdateAttackPattern();
        }

        private void FixedUpdate()
        {
            if (body == null) return;
            if (!encounterActive)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            bool canAct = !GamePause.BlocksGameplay && (enemyActor == null || !enemyActor.IsStunned);
            if (canAct && attack == Attack.Idle && player != null)
            {
                float dx = player.position.x - body.position.x;
                FacePlayer();
                float speedX = Mathf.Abs(dx) > 1.65f ? Mathf.Sign(dx) * moveSpeed : 0f;
                body.linearVelocity = new Vector2(speedX, body.linearVelocity.y);
            }
            else if (attack != Attack.Idle || !canAct || player == null)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            }

            if (!hasArenaBounds || hitbox == null) return;

            float minX = arenaMinX + hitbox.bounds.extents.x;
            float maxX = float.IsPositiveInfinity(arenaMaxX)
                ? arenaMaxX
                : arenaMaxX - hitbox.bounds.extents.x;
            if (maxX < minX) minX = maxX = (arenaMinX + arenaMaxX) * 0.5f;

            float clampedX = Mathf.Clamp(body.position.x, minX, maxX);
            if (Mathf.Abs(clampedX - body.position.x) < 0.001f) return;
            body.position = new Vector2(clampedX, body.position.y);
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }

        public void SetArenaBounds(float minX, float maxX)
        {
            arenaMinX = minX;
            arenaMaxX = maxX;
            hasArenaBounds = true;
        }

        public void SetEncounterActive(bool active)
        {
            if (encounterActive == active) return;
            encounterActive = active;
            if (active)
            {
                FindPlayer();
                summonTimer = 1.2f;
                attackTimer = 0.7f;
                return;
            }

            attack = Attack.Idle;
            transform.localScale = baseScale;
            SetWarningsVisible(false);
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void UpdateAttackPattern()
        {
            if (attack == Attack.Idle)
            {
                transform.localScale = baseScale;
                attackTimer -= Time.deltaTime;
                float distance = Vector2.Distance(transform.position, player.position);
                if (attackTimer > 0f || distance > 9f) return;

                FacePlayer();
                attack = phase == 1 || nextSlam ? Attack.SlamWindup : Attack.SweepWindup;
                if (phase > 1) nextSlam = !nextSlam;
                attackFacing = facing;
                attackTimer = attack == Attack.SlamWindup ? 0.8f : 0.65f;
                if (attack == Attack.SlamWindup) slamTarget = PlayerGroundPoint();
            }

            attackTimer -= Time.deltaTime;
            if (attack == Attack.SlamWindup)
            {
                float squash = Mathf.Lerp(1f, 0.58f, Mathf.Clamp01(1f - attackTimer / 0.8f));
                transform.localScale = new Vector3(baseScale.x, baseScale.y * squash, baseScale.z);
            }
            else
            {
                float stretch = Mathf.Lerp(1f, 1.25f, Mathf.Clamp01(1f - attackTimer / 0.65f));
                transform.localScale = new Vector3(baseScale.x * stretch, baseScale.y, baseScale.z);
            }
            UpdateWarnings();

            if (attackTimer > 0f) return;
            if (attack == Attack.SlamWindup) Slam();
            else Sweep();
            attack = Attack.Idle;
            attackTimer = phase == 1 ? phaseOneAttackInterval : attackInterval;
            transform.localScale = baseScale;
            SetWarningsVisible(false);
        }

        private void Slam()
        {
            if (player == null) return;
            var playerCollider = player.GetComponent<Collider2D>();
            float playerFootY = playerCollider != null ? playerCollider.bounds.min.y : player.position.y;
            if (Mathf.Abs(player.position.x - slamTarget.x) > slamRadius
                || playerFootY > slamTarget.y + 0.75f) return;
            DamagePlayer(new Vector2(attackFacing, 1.1f));
        }

        private void Sweep()
        {
            float floorY = hitbox != null ? hitbox.bounds.min.y : transform.position.y;
            Vector2 center = (Vector2)transform.position + new Vector2(attackFacing * sweepSize.x * 0.5f, floorY - transform.position.y + sweepSize.y * 0.5f);
            var hits = Physics2D.OverlapBoxAll(center, sweepSize, 0f);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].GetComponentInParent<PlayerController>() == null) continue;
                DamagePlayer(new Vector2(attackFacing, 0.65f));
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

        private void CreateWarnings()
        {
            if (warningCircleSprite == null) warningCircleSprite = MakeWarningCircle();
            if (warningSquareSprite == null)
            {
                var white = Texture2D.whiteTexture;
                warningSquareSprite = Sprite.Create(white, new Rect(0f, 0f, white.width, white.height), new Vector2(0.5f, 0.5f), white.width);
            }

            slamWarning = MakeWarning("BossSlamWarning", warningCircleSprite);
            sweepWarning = MakeWarning("BossSweepWarning", warningSquareSprite);
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
            if (attack == Attack.SlamWindup && slamWarning != null)
            {
                slamWarning.enabled = true;
                slamWarning.transform.position = new Vector3(slamTarget.x, slamTarget.y + 0.02f, transform.position.z);
                slamWarning.transform.localScale = Vector3.one * slamRadius * 2f;
                float danger = 1f - Mathf.Clamp01(attackTimer / 0.8f);
                slamWarning.color = Color.Lerp(new Color(1f, 0.8f, 0.15f, 0.45f), new Color(1f, 0.15f, 0.08f, 0.85f), danger);
            }
            else if (attack == Attack.SweepWindup && sweepWarning != null)
            {
                float floorY = hitbox != null ? hitbox.bounds.min.y : transform.position.y;
                sweepWarning.enabled = true;
                sweepWarning.transform.position = (Vector2)transform.position + new Vector2(attackFacing * sweepSize.x * 0.5f, floorY + sweepSize.y * 0.5f);
                sweepWarning.transform.localScale = new Vector3(sweepSize.x, sweepSize.y, 1f);
                float danger = 1f - Mathf.Clamp01(attackTimer / 0.65f);
                sweepWarning.color = Color.Lerp(new Color(1f, 0.8f, 0.15f, 0.28f), new Color(1f, 0.12f, 0.08f, 0.72f), danger);
            }
            else
            {
                SetWarningsVisible(false);
            }
        }

        private void SetWarningsVisible(bool visible)
        {
            if (slamWarning != null) slamWarning.enabled = visible && attack == Attack.SlamWindup;
            if (sweepWarning != null) sweepWarning.enabled = visible && attack == Attack.SweepWindup;
        }

        private void OnDestroy()
        {
            if (slamWarning != null) Destroy(slamWarning.gameObject);
            if (sweepWarning != null) Destroy(sweepWarning.gameObject);
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
            attackTimer = 0.8f;
            SetWarningsVisible(false);
            transform.localScale = baseScale;
            health.Heal(health.Max);
            return true;
        }

    }
}
