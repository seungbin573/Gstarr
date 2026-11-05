using Gstarr.Game;
using Gstarr.Player;
using Gstarr.World;
using UnityEngine;

namespace Gstarr.Combat
{
    /// <summary>character 폴더 적들의 공격 패턴. 기존 박자 적과 따로 움직인다.</summary>
    [RequireComponent(typeof(EnemyActor))]
    [DefaultExecutionOrder(80)]
    public class CharacterFoe : MonoBehaviour
    {
        private enum Phase
        {
            Idle,
            Windup,
            Dash,
            Recover,
            Throw,
            Vanishing,
            Hidden,
            Appear,
            Grab
        }

        private EnemyActor actor;
        private Rigidbody2D rb;
        private SpriteRenderer sprite;
        private Collider2D body;
        private Transform player;
        private Phase phase = Phase.Idle;
        private float timer;
        private float animTime;
        private int animFrame;
        private Sprite[] animationFrames;
        private float frameSeconds = 0.14f;
        private bool loopAnimation = true;
        private float hiddenWait;
        private CharacterArt.Pose pose = CharacterArt.Pose.Idle;
        private bool dashHit;
        private bool hidden;
        private bool freezeAnimation;
        private bool kindBound;
        private EnemyKind boundKind;
        private SpriteRenderer grabArm;

        private void Awake()
        {
            actor = GetComponent<EnemyActor>();
            rb = GetComponent<Rigidbody2D>();
            sprite = GetComponent<SpriteRenderer>();
            body = GetComponent<Collider2D>();
            ApplyFirstSprite();
            StripBorrowedMotion();
        }

        public void BindKind(EnemyKind value)
        {
            boundKind = value;
            kindBound = true;
            actor = GetComponent<EnemyActor>();
            sprite = GetComponent<SpriteRenderer>();
            rb = GetComponent<Rigidbody2D>();
            ApplyFirstSprite();
            StripBorrowedMotion();
        }

        private void Start()
        {
            BindPlayer();
            ApplyFirstSprite();
            StripBorrowedMotion();
            if (Flies && rb != null)
            {
                rb.gravityScale = 0f;
                var pos = transform.position;
                if (pos.y < actor.GroundY + 2.2f)
                    transform.position = new Vector3(pos.x, actor.GroundY + 3.2f, pos.z);
            }

            if (Kind == EnemyKind.Shadowman)
                SetPose(CharacterArt.Pose.Idle);
        }

        private void OnDestroy()
        {
            if (grabArm != null) Destroy(grabArm.gameObject);
        }

        private EnemyKind Kind => kindBound ? boundKind : actor != null ? actor.Kind : EnemyKind.BeatChaser;
        private bool Flies => Kind == EnemyKind.BlackShadowKid || Kind == EnemyKind.BlackShadowThrow;

        private void ApplyFirstSprite()
        {
            pose = CharacterArt.Pose.Idle;
            animFrame = 0;
            animTime = 0f;
            animationFrames = CharacterArt.Frames(Kind, pose);
            frameSeconds = 0.11f;
            loopAnimation = true;
            var idle = CharacterArt.Idle(Kind);
            if (sprite == null || idle == null) return;
            sprite.sprite = idle;
            sprite.enabled = true;
            sprite.color = Color.white;
            sprite.sortingOrder = 8;
        }

        private void BindPlayer()
        {
            player = actor != null ? actor.PlayerTarget : null;
            if (player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }
        }

        private void Update()
        {
            if (actor == null || actor.IsDead) return;
            Animate(Time.deltaTime);
            if (!actor.CanAct)
            {
                if (rb != null && Flies) rb.linearVelocity = Vector2.zero;
                return;
            }

            BindPlayer();
        }

        private void FixedUpdate()
        {
            if (actor == null) return;
            actor.ClampRoom();
            if (actor.IsDead || !actor.CanAct || rb == null) return;
            if (phase != Phase.Dash) return;
            StepDashPhysics();
        }

        private void StepDashPhysics()
        {
            timer -= Time.fixedDeltaTime;
            int dir = rb.linearVelocity.x >= 0f ? 1 : -1;
            if (!Flies)
            {
                if (actor.PitInFront(dir) || !actor.RoomAhead(dir))
                    EndDash();
            }
            else if (!actor.RoomAhead(dir))
                EndDash();

            if (!dashHit && PlayerClose(1.45f, Flies ? 1.6f : 1.35f))
            {
                dashHit = true;
                HitPlayer(1);
            }

            if (timer <= 0f) EndDash();
        }

        private void EndDash()
        {
            phase = Phase.Recover;
            timer = 0.35f;
            SetPose(CharacterArt.Pose.Idle);
            if (rb != null)
                rb.linearVelocity = Flies ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
        }

        private void StepThrower(bool flies, float keepDistance, float throwEvery)
        {
            FacePlayer();
            if (phase == Phase.Throw)
            {
                if (flies) rb.linearVelocity = Vector2.zero;
                else HoldGround();
                timer -= Time.deltaTime;
                if (timer > 0f) return;
                FireThrown(flies);
                phase = Phase.Recover;
                timer = throwEvery;
                SetPose(flies ? CharacterArt.Pose.Idle : CharacterArt.Pose.Laugh);
                return;
            }

            if (phase == Phase.Recover)
            {
                KeepDistance(keepDistance, flies);
                timer -= Time.deltaTime;
                if (timer <= 0f) phase = Phase.Idle;
                return;
            }

            KeepDistance(keepDistance, flies);
            if (!PlayerInSight(9f)) return;
            phase = Phase.Throw;
            timer = 0.42f;
            SetPose(CharacterArt.Pose.Throw, timer);
        }

        private void StepShadowman()
        {
            if (phase == Phase.Idle)
            {
                HoldGround();
                if (PlayerInSight(8.5f))
                    EnterHidden(0.9f);
                return;
            }

            if (phase == Phase.Vanishing)
            {
                HoldGround();
                timer -= Time.deltaTime;
                if (timer > 0f) return;
                SetHidden(true);
                phase = Phase.Hidden;
                timer = hiddenWait;
                return;
            }

            if (phase == Phase.Hidden)
            {
                timer -= Time.deltaTime;
                if (timer > 0f || player == null || !PlayerInSight(10f)) return;
                AppearBehind();
                return;
            }

            if (phase == Phase.Appear)
            {
                timer -= Time.deltaTime;
                FacePlayer();
                if (timer > 0f) return;
                BeginGrab();
                return;
            }

            if (phase == Phase.Grab)
            {
                timer -= Time.deltaTime;
                StickGrab();
                if (timer > 0f) return;
                ReleaseGrab();
                phase = Phase.Recover;
                timer = 1.1f;
                SetHidden(false);
                SetPose(CharacterArt.Pose.Idle);
                return;
            }

            if (phase == Phase.Recover)
            {
                FacePlayer();
                HoldGround();
                timer -= Time.deltaTime;
                if (timer > 0f) return;
                EnterHidden(2.4f);
            }
        }

        private void EnterHidden(float wait)
        {
            phase = Phase.Vanishing;
            hiddenWait = wait;
            timer = 0.42f;
            SetPose(CharacterArt.Pose.Vanish, timer);
        }

        private void AppearBehind()
        {
            if (player == null) return;
            int facing = 1;
            var control = player.GetComponent<PlayerController>();
            if (control != null) facing = control.FacingDirection == 0 ? 1 : control.FacingDirection;
            float x = player.position.x - facing * 1.15f;
            if (actor.HasLeash)
                x = Mathf.Clamp(x, actor.RoomMin + 0.6f, actor.RoomMax - 0.6f);
            float y = player.position.y;
            transform.position = new Vector3(x, y, transform.position.z);
            if (rb != null)
            {
                rb.position = transform.position;
                rb.linearVelocity = Vector2.zero;
            }

            SetHidden(false);
            SetPose(CharacterArt.Pose.Grab, 0.64f);
            FacePlayer();
            phase = Phase.Appear;
            timer = 0.16f;
            GameAudio.Play(GameAudio.Id.Dash, 0.5f);
        }

        private void BeginGrab()
        {
            phase = Phase.Grab;
            timer = 0.48f;
            SetPose(CharacterArt.Pose.Grab, 0.64f);
            GameAudio.Play(GameAudio.Id.EnemySwing, 0.9f);
            HitPlayer(2);
            var control = player != null ? player.GetComponent<PlayerController>() : null;
            if (control != null)
            {
                Vector3 offset = new Vector3(-control.FacingDirection * 0.35f, 0.15f, 0f);
                control.GrabbedBy(transform, offset, 0.48f);
            }

            EnsureGrabArm();
            CombatFeel.Impact(transform.position, true);
        }

        private void StickGrab()
        {
            if (player == null || rb == null) return;
            Vector3 hold = player.position + new Vector3(-PlayerDir() * 0.55f, 0.1f, 0f);
            rb.position = hold;
            transform.position = hold;
            rb.linearVelocity = Vector2.zero;
            if (grabArm != null)
            {
                grabArm.enabled = true;
                grabArm.transform.position = player.position + Vector3.up * 0.35f;
                float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 18f);
                grabArm.color = new Color(0.55f, 0.02f, 0.06f, pulse);
            }
        }

        private void ReleaseGrab()
        {
            if (grabArm != null) grabArm.enabled = false;
        }

        private void EnsureGrabArm()
        {
            var frames = CharacterArt.Frames(EnemyKind.Shadowman, CharacterArt.Pose.Grab);
            Sprite art = frames != null && frames.Length > 2 ? frames[2] : CharacterArt.Idle(EnemyKind.Shadowman);
            if (grabArm == null)
            {
                var go = new GameObject("ShadowmanArm");
                grabArm = go.AddComponent<SpriteRenderer>();
                grabArm.sortingOrder = 12;
            }

            grabArm.sprite = art;
            grabArm.enabled = true;
        }

        private void SetHidden(bool value)
        {
            hidden = value;
            if (sprite != null) sprite.enabled = !value;
            if (body != null) body.enabled = !value;
            if (rb == null) return;
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = value ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
            rb.gravityScale = value || Flies ? 0f : 4f;
        }

        private void KeepDistance(float distance, bool flies)
        {
            if (player == null)
            {
                if (flies) rb.linearVelocity = Vector2.zero;
                else HoldGround();
                return;
            }

            float dx = player.position.x - transform.position.x;
            int dir;
            if (Mathf.Abs(dx) > distance + 0.45f) dir = dx > 0f ? 1 : -1;
            else if (Mathf.Abs(dx) < distance - 0.45f) dir = dx > 0f ? -1 : 1;
            else
            {
                if (flies)
                {
                    float targetY = player.position.y + 2.4f;
                    float vy = Mathf.Clamp(targetY - transform.position.y, -1.6f, 1.6f);
                    rb.linearVelocity = new Vector2(0f, vy * 2.2f);
                }
                else HoldGround();
                return;
            }

            if (!actor.RoomAhead(dir)) dir = 0;
            FacePlayer();
            if (flies)
            {
                float targetY = player.position.y + 2.6f;
                float vy = Mathf.Clamp(targetY - transform.position.y, -2f, 2f);
                rb.linearVelocity = new Vector2(dir * 3.4f, vy * 2.4f);
            }
            else
            {
                rb.linearVelocity = new Vector2(dir * 3.2f, rb.linearVelocity.y);
            }
        }

        private void FireThrown(bool paper)
        {
            if (player == null) return;
            Vector2 origin = (Vector2)transform.position + new Vector2(PlayerDir() * 0.55f, paper ? 0.35f : 0.55f);
            Vector2 to = (Vector2)player.position + Vector2.up * 0.35f - origin;
            if (to.sqrMagnitude < 0.01f) to = new Vector2(PlayerDir(), 0.1f);
            float travel = ArenaDoorSpawner.RangeUntilDoor(origin.x, to.x, 9f);
            if (travel < 0.4f) return;

            var frames = CharacterArt.Frames(Kind, CharacterArt.Pose.Shot);
            Sprite shotSprite = frames != null && frames.Length > 0 ? frames[0] : sprite.sprite;
            var go = new GameObject(paper ? "RolledPaper" : "KidBall");
            go.transform.position = origin;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = shotSprite;
            sr.sortingOrder = 8;
            float height = shotSprite != null ? Mathf.Max(0.08f, shotSprite.bounds.size.y) : 0.4f;
            go.transform.localScale = Vector3.one * (paper ? 0.7f / Mathf.Max(0.08f, height) : 0.55f / Mathf.Max(0.08f, height));
            var projectile = go.AddComponent<Projectile>();
            projectile.SetAnimation(frames);
            int playerLayer = player.gameObject.layer;
            Vector2 velocity = paper
                ? to.normalized * 8.5f
                : new Vector2(Mathf.Sign(to.x) * 7.2f, Mathf.Clamp(to.y * 0.45f + 2.4f, 1.2f, 4.2f));
            if (paper)
            {
                projectile.Launch(velocity, 1, travel, 1 << playerLayer, gameObject);
            }
            else
            {
                projectile.Launch(velocity, 0, travel, 1 << playerLayer, gameObject, onHit: _ =>
                    FoeEffects.DistractionScare(player, CharacterArt.Frames(EnemyKind.AnnoyingKid, CharacterArt.Pose.Scare)));
                projectile.SetGravity(new Vector2(0f, -14f));
            }

            GameAudio.Play(GameAudio.Id.EnemyShot, 0.7f);
        }

        private void HitPlayer(int damage)
        {
            if (player == null) return;
            var hp = player.GetComponent<Health>();
            if (hp == null) return;
            hp.TakeDamage(damage, new Vector2(PlayerDir(), 0.35f));
        }

        private void HoldGround()
        {
            if (rb == null) return;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        private void FacePlayer()
        {
            if (player == null || actor == null) return;
            actor.FaceToward(player.position.x - transform.position.x);
        }

        private int PlayerDir()
        {
            if (player == null) return actor != null && actor.Facing() > 0 ? 1 : -1;
            float dx = player.position.x - transform.position.x;
            return dx >= 0f ? 1 : -1;
        }

        private bool PlayerInSight(float range)
        {
            if (player == null) return false;
            if (ArenaDoorSpawner.SeparatedByDoor(transform.position.x, player.position.x)) return false;
            return Mathf.Abs(player.position.x - transform.position.x) <= range
                && Mathf.Abs(player.position.y - transform.position.y) <= 8f;
        }

        private bool PlayerClose(float x, float y)
        {
            if (player == null) return false;
            return Mathf.Abs(player.position.x - transform.position.x) <= x
                && Mathf.Abs(player.position.y - transform.position.y) <= y;
        }

        private void SetPose(CharacterArt.Pose next, float duration = 0f)
        {
            bool same = pose == next && animationFrames != null && animationFrames.Length > 0;
            if (same && duration <= 0f) return;
            pose = next;
            animFrame = 0;
            animTime = 0f;
            animationFrames = CharacterArt.Frames(Kind, pose);
            freezeAnimation = false;
            loopAnimation = pose == CharacterArt.Pose.Idle
                || (pose == CharacterArt.Pose.Charge && Kind == EnemyKind.BlackShadowKid);
            float seconds = 0.11f;
            if (pose == CharacterArt.Pose.Charge || pose == CharacterArt.Pose.Throw)
                seconds = 0.08f;
            if (duration > 0f && animationFrames != null && animationFrames.Length > 0)
                seconds = Mathf.Max(0.05f, duration / animationFrames.Length);
            frameSeconds = seconds;
            ShowFrame();
        }

        private void Animate(float deltaTime)
        {
            if (freezeAnimation || animationFrames == null || animationFrames.Length == 0 || sprite == null) return;
            animTime += deltaTime;
            int advance = Mathf.FloorToInt(animTime / frameSeconds);
            if (advance == 0) return;
            animTime -= advance * frameSeconds;
            animFrame = loopAnimation
                ? (animFrame + advance) % animationFrames.Length
                : Mathf.Min(animFrame + advance, animationFrames.Length - 1);
            ShowFrame();
        }

        private void ShowFrame()
        {
            var frames = animationFrames;
            if (frames == null || frames.Length == 0 || sprite == null) return;
            int index = Mathf.Clamp(animFrame, 0, frames.Length - 1);
            if (frames[index] == null) return;
            Paint(sprite, frames[index]);
            var visual = transform.Find("BodyVisual");
            if (visual != null) Paint(visual.GetComponent<SpriteRenderer>(), frames[index]);
        }

        private void Paint(SpriteRenderer target, Sprite frame)
        {
            if (target == null || frame == null) return;
            target.sprite = frame;
            if (!hidden) target.enabled = true;
            target.color = Color.white;
            if (sprite != null) target.flipX = sprite.flipX;
        }

        private void StripBorrowedMotion()
        {
            var motion = GetComponent<Gstarr.Game.SpriteMotion>();
            if (motion != null) DestroyImmediate(motion);
            var visual = transform.Find("BodyVisual");
            if (visual != null) DestroyImmediate(visual.gameObject);
            if (sprite == null) return;
            sprite.enabled = true;
            sprite.color = Color.white;
        }

    }

    internal static class FoeEffects
    {
        public static void DistractionScare(Transform victim, Sprite[] frames)
        {
            if (frames == null || frames.Length == 0) return;
            if (activeScare != null) Object.Destroy(activeScare);

            GameAudio.Play(GameAudio.Id.EnemySwing, 1f);
            GamePause.HitStop(0.09f);
            CombatFeel.Shake(0.32f, 0.22f);
            if (victim != null) CombatFeel.Impact(victim.position, true);

            var canvas = UIFactory.CreateCanvas(null, "DistractionScare", 200);
            activeScare = canvas.gameObject;
            var root = (RectTransform)canvas.transform;
            var shade = NewImage("Shade", root);
            var shadeRect = shade.rectTransform;
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = Vector2.zero;
            shadeRect.offsetMax = Vector2.zero;
            shade.color = new Color(0.02f, 0f, 0.01f, 0.82f);

            const int columns = 3;
            const int rows = 2;
            var faces = new UnityEngine.UI.Image[columns * rows];
            var homes = new Vector2[faces.Length];
            for (int i = 0; i < faces.Length; i++)
            {
                int x = i % columns;
                int y = i / columns;
                var face = NewImage("Face", root);
                var rect = face.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(760f, 640f);
                homes[i] = new Vector2(-620f + x * 620f, 280f - y * 560f);
                rect.anchoredPosition = homes[i];
                rect.localEulerAngles = new Vector3(0f, 0f, (i % 2 == 0 ? -3f : 3f));
                face.preserveAspect = true;
                face.sprite = frames[0];
                faces[i] = face;
            }

            var flash = NewImage("Flash", root);
            var flashRect = flash.rectTransform;
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            flash.color = new Color(1f, 0.92f, 0.92f, 1f);

            var scare = canvas.gameObject.AddComponent<ScareFlash>();
            scare.Bind(shade, flash, faces, frames, 0.78f);
        }

        private static UnityEngine.UI.Image NewImage(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            return image;
        }

        private static GameObject activeScare;

        private sealed class ScareFlash : MonoBehaviour
        {
            private UnityEngine.UI.Image shade;
            private UnityEngine.UI.Image flash;
            private UnityEngine.UI.Image[] faces;
            private Sprite[] frames;
            private float life;
            private float duration;

            public void Bind(UnityEngine.UI.Image shade, UnityEngine.UI.Image flash, UnityEngine.UI.Image[] faces, Sprite[] frames, float life)
            {
                this.shade = shade;
                this.flash = flash;
                this.faces = faces;
                this.frames = frames;
                this.life = life;
                duration = life;
            }

            private void Update()
            {
                life -= Time.unscaledDeltaTime;
                float t = 1f - Mathf.Clamp01(life / Mathf.Max(0.01f, duration));
                float punch = t < 0.1f ? Mathf.Lerp(1.18f, 1f, t / 0.1f) : 1f;
                float alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                if (flash != null)
                {
                    var color = flash.color;
                    color.a = t < 0.08f ? 1f - t / 0.08f : 0f;
                    flash.color = color;
                }

                if (faces != null && frames != null && frames.Length > 0)
                {
                    int flip = Mathf.FloorToInt(Time.unscaledTime * 3.5f);
                    for (int i = 0; i < faces.Length; i++)
                    {
                        var face = faces[i];
                        if (face == null) continue;
                        int frame = flip % frames.Length;
                        if (frames[frame] != null) face.sprite = frames[frame];
                        var color = face.color;
                        color.a = alpha;
                        face.color = color;
                        face.rectTransform.localScale = Vector3.one * punch;
                    }
                }

                if (shade != null)
                {
                    var color = shade.color;
                    color.a = 0.62f * alpha;
                    shade.color = color;
                }

                if (life <= 0f)
                {
                    if (activeScare == gameObject) activeScare = null;
                    Destroy(gameObject);
                }
            }
        }
    }
}
