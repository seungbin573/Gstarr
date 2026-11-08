using Gstarr.Game;
using Gstarr.World;
using UnityEngine;

namespace Gstarr.Combat
{
    /// <summary>천사석상의 날개 눈과 십자가를 쏘는 손. 일반 적과 같은 체력/공격 판정을 쓴다.</summary>
    [RequireComponent(typeof(EnemyActor))]
    public sealed class AngelSummon : MonoBehaviour
    {
        private EnemyActor actor;
        private Rigidbody2D body;
        private SpriteRenderer visual;
        private float shotClock = 1.2f;
        private float contactClock;
        private Vector2 lockedAim;
        private bool aiming;
        public Angel Owner { get; set; }

        private void Awake()
        {
            actor = GetComponent<EnemyActor>();
            body = GetComponent<Rigidbody2D>();
            visual = GetComponent<SpriteRenderer>();
            body.gravityScale = 0f;
            if (visual.sprite != null) gameObject.name = visual.sprite.name;
        }

        private void FixedUpdate()
        {
            if (!actor.CanAct) { body.linearVelocity = Vector2.zero; return; }
            actor.ClampRoom();
            var player = actor.PlayerTarget;
            if (player == null || ArenaDoorSpawner.SeparatedByDoor(transform.position.x, player.position.x)
                || (actor.HasLeash && (player.position.x < actor.RoomMin || player.position.x > actor.RoomMax)))
            { body.linearVelocity = Vector2.zero; return; }
            contactClock -= Time.fixedDeltaTime;
            Vector2 target = (Vector2)player.position + Vector2.up * 0.45f;
            Vector2 delta = target - body.position;
            bool gun = actor.Kind == EnemyKind.AngelGunHand;
            visual.flipX = delta.x < 0f;
            if (!gun)
            {
                float speed = actor.Kind == EnemyKind.AngelEyeTwo ? 3.2f : 2.2f;
                body.linearVelocity = delta.normalized * speed;
                return;
            }

            body.linearVelocity = Vector2.zero;
            shotClock -= Time.fixedDeltaTime;
            if (!aiming && shotClock <= 0.4f)
            {
                lockedAim = delta.normalized;
                aiming = true;
            }
            if (aiming) visual.flipX = lockedAim.x < 0f;
            if (shotClock > 0f) return;
            FireCross(lockedAim, player);
            aiming = false;
            shotClock = 1.8f;
        }

        private void FireCross(Vector2 direction, Transform target)
        {
            var art = CharacterArt.Monster(CharacterArt.Cross, 0.65f);
            if (art == null) return;
            Vector2 origin = (Vector2)transform.position + new Vector2(direction.x >= 0f ? 0.65f : -0.65f, 0.35f);
            float range = ArenaDoorSpawner.RangeUntilDoor(origin.x, direction.x, 14f);
            var go = new GameObject(CharacterArt.Cross);
            go.transform.position = origin;
            var view = go.AddComponent<SpriteRenderer>();
            view.sprite = art;
            view.sortingOrder = 10;
            var shot = go.AddComponent<Projectile>();
            shot.Launch(direction * 7f, 1, range,
                (1 << target.gameObject.layer) | LayerMask.GetMask("Ground"), gameObject);
            Owner?.TrackEffect(go);
            GameAudio.Play(GameAudio.Id.EnemyShot, 0.65f);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!actor.CanAct || contactClock > 0f || actor.Kind == EnemyKind.AngelGunHand) return;
            var player = actor.PlayerTarget;
            var health = collision.collider.GetComponentInParent<Health>();
            if (player == null || health == null || health != player.GetComponent<Health>()) return;
            if (health.TakeDamage(1, player.position - transform.position)) contactClock = 0.8f;
        }
    }

    /// <summary>고정된 위치에 예고를 보여 준 뒤 솟아오르는 손. 공격당 한 번만 피해를 준다.</summary>
    public sealed class AngelHandStrike : MonoBehaviour
    {
        private Angel owner;
        private EnemyActor actor;
        private SpriteRenderer visual;
        private Sprite strike;
        private float elapsed;
        private bool struck;
        private bool summon;

        public static GameObject Spawn(Angel owner, Vector2 position, string art, bool summon = false)
        {
            var go = new GameObject(art);
            go.transform.position = position;
            var view = go.AddComponent<SpriteRenderer>();
            view.sprite = CharacterArt.Monster(summon ? CharacterArt.PullHand : art == CharacterArt.MultiHand ? CharacterArt.MultiHand : CharacterArt.SummonedHand, art == CharacterArt.MultiHand ? 3.5f : 1.5f);
            view.sortingOrder = 9;
            view.color = new Color(1f, 0.55f, 0.55f, 0.3f);
            var effect = go.AddComponent<AngelHandStrike>();
            effect.owner = owner;
            effect.actor = owner.GetComponent<EnemyActor>();
            effect.visual = view;
            effect.strike = CharacterArt.Monster(art, art == CharacterArt.MultiHand ? 3.5f : 1.5f);
            effect.summon = summon;
            owner.TrackEffect(go);
            return go;
        }

        private void Update()
        {
            if (owner == null) { Destroy(gameObject); return; }
            if (!actor.CanAct) return;
            elapsed += Time.deltaTime;
            if (elapsed >= 0.65f && !struck)
            {
                struck = true;
                visual.sprite = strike;
                visual.color = Color.white;
                if (summon) owner.BringRandomMob(transform.position);
                else
                {
                    var player = actor.PlayerTarget;
                    var collider = player != null ? player.GetComponent<Collider2D>() : null;
                    if (collider != null && visual.bounds.Intersects(collider.bounds))
                        player.GetComponent<Health>()?.TakeDamage(1, Vector2.up);
                }
            }
            if (elapsed >= 1.05f) Destroy(gameObject);
        }
    }
}
