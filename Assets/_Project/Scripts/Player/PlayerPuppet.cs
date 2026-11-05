using Gstarr.Game;
using UnityEngine;

namespace Gstarr.Player
{
    /// <summary>
    /// 같은 캔버스에 그려 둔 파츠를 관절 기준으로 겹치고, 서기·걷기·점프·대시에서 따로 움직인다.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class PlayerPuppet : MonoBehaviour
    {
        private const string Folder = "Assets/arts/character/player/";
        private const float Ppu = 460f;
        private const float CanvasHeight = 2048f;
        private const float FootX = 985f;
        private const float FootY = 171f;
        private const float FootDrop = -0.75f;

        public Transform WeaponHand { get; private set; }

        private PlayerController player;
        private SpriteMotion motion;
        private Transform rig;
        private Limb neck, hoodie, shorts, head;
        private Limb armBack, armFront, foreBack, foreFront, bootBack, bootFront;
        private SpriteRenderer[] parts;
        private float cycle;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            motion = GetComponent<SpriteMotion>();
            var source = GetComponent<SpriteRenderer>();
            int order = source != null ? source.sortingOrder : 5;

            rig = new GameObject("PlayerRig").transform;
            var body = transform.Find("BodyVisual");
            rig.SetParent(body != null ? body : transform, false);
            rig.localPosition = new Vector3(0f, FootDrop, 0f);

            const float bodyX = 997f;
            float bodyY = Top(1524f);
            bootBack = LimbAt("BootBack", Folder + "무제320_20261104171405.png", 944f, Top(1632f), order, rig, FootX, FootY, true, -70f / Ppu);
            bootFront = LimbAt("BootFront", Folder + "무제320_20261104171402.png", 1044f, Top(1590f), order + 1, rig, FootX, FootY);
            hoodie = LimbAt("Hoodie", Folder + "무제320_20261104171445.png", bodyX, bodyY, order + 3, rig, FootX, FootY);
            shorts = LimbAt("Shorts", Folder + "무제320_20261104171437.png", 992f, Top(1500f), order + 4, hoodie.Transform, bodyX, bodyY);
            bootBack.StickTo(shorts.Transform);
            bootFront.StickTo(shorts.Transform);
            const string armPathL = Folder + "무제320_20261104171429.png";
            const string armPathR = Folder + "무제320_20261104172258.png";
            const float armPivotL = 950f;
            float armTopL = Top(1266f);
            const float armPivotR = 1090f;
            float armTopR = Top(1293f);
            armBack = LimbAt("ArmBack", armPathL, armPivotL, armTopL, order + 5, hoodie.Transform, bodyX, bodyY);
            armFront = LimbAt("ArmFront", armPathR, armPivotR, armTopR, order + 6, hoodie.Transform, bodyX, bodyY);
            foreBack = ExtendArm("ForeBack", armPathL, armBack, armPivotL, armTopL, order + 7);
            foreFront = ExtendArm("ForeFront", armPathR, armFront, armPivotR, armTopR, order + 8);
            Vector3 neckOnBody = new Vector3(0.02f, 1.22f, 0f) - hoodie.Rest;
            Vector3 headOnBody = new Vector3(0.02f, 1.36f, 0f) - hoodie.Rest;
            neck = Place("Neck", PlayerArt.LoadTrimmed(Folder + "무제320_20261104171859.png", new Vector2(0.5f, 0.08f), 0.34f),
                neckOnBody, order + 7, hoodie.Transform);
            head = Place("Head", PlayerArt.LoadTrimmed(Folder + "화면 캡처 2026-11-06 005004.png", new Vector2(0.5f, 0f), 0.5f),
                headOnBody, order + 8, hoodie.Transform);

            var hand = new GameObject("Hand").transform;
            hand.SetParent(foreFront.Transform, false);
            hand.localPosition = HandPoint(Folder + "무제320_20261104172258.png");
            WeaponHand = hand;
            parts = rig.GetComponentsInChildren<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            if (player == null) return;
            float dt = Time.deltaTime;
            bool grounded = player.IsGrounded;
            bool dashing = player.IsDashing;
            float speed = Mathf.Abs(player.Velocity.x);
            float rate = dashing ? 10f : grounded && speed > 0.4f ? Mathf.Lerp(5.2f, 8.2f, speed / 8f) : 2.2f;
            cycle += dt * rate;
            float wave = Mathf.Sin(cycle);
            float step = Mathf.Cos(cycle);

            if (dashing) PoseDash(wave);
            else if (!grounded) PoseAir(wave);
            else if (speed > 0.4f) PoseWalk(wave, step, speed);
            else PoseIdle(wave);

            float attack = AttackAngle();
            if (Mathf.Abs(attack) > 1f)
                armFront.AddRotation(attack * 0.1f);

            ApplyFade();
        }

        private void PoseIdle(float wave)
        {
            float breath = Mathf.Sin(cycle * 0.85f);
            hoodie.Pose(wave * 0.8f, 0f, breath * 0.012f, 1f, 1f + breath * 0.015f);
            shorts.Pose(wave * 0.6f);
            head.Pose(wave * 2.5f, 0f, breath * 0.02f);
            neck.Pose(wave * 1.5f);
            armBack.Pose(wave * 8f);
            armFront.Pose(-wave * 8f);
            foreBack.Pose(wave * 5f - 6f);
            foreFront.Pose(-wave * 5f - 6f);
            bootBack.Pose(0f);
            bootFront.Pose(0f);
            Plant(0f);
        }

        private void PoseWalk(float wave, float step, float speed)
        {
            float pace = Mathf.Clamp01(speed / 8f);
            float left = Step(wave);
            float right = Step(-wave);
            hoodie.Pose(wave * 2f, wave * 0.012f);
            shorts.Pose(wave * 1.2f);
            head.Pose(-wave * 2f);
            neck.Pose(-wave * 1.2f);
            float swing = right - left;
            armBack.Pose(swing * 24f + wave * 4f);
            armFront.Pose(-swing * 24f - wave * 4f);
            foreBack.Pose(swing * 10f - 6f);
            foreFront.Pose(-swing * 10f - 6f);
            float reach = Mathf.Lerp(0.035f, 0.055f, pace);
            float hop = Mathf.Lerp(0.028f, 0.042f, pace);
            StepFoot(bootBack, cycle, reach, hop);
            StepFoot(bootFront, cycle + Mathf.PI, reach, hop);
            Plant(0f);
        }

        private void PoseAir(float wave)
        {
            hoodie.Pose(-3f, 0f, 0.015f);
            shorts.Pose(2f);
            head.Pose(-2f + wave * 1.5f, 0f, 0.02f);
            neck.Pose(-1.5f);
            armBack.Pose(-16f + wave * 5f);
            armFront.Pose(-12f - wave * 5f);
            foreBack.Pose(-8f);
            foreFront.Pose(-6f);
            bool rising = player.Velocity.y > 0.2f;
            float tuck = rising ? 0.03f : 0.012f;
            bootBack.Pose(0f, -0.02f, tuck);
            bootFront.Pose(0f, 0.02f, tuck * 0.6f);
            Plant(0f);
        }

        private void PoseDash(float wave)
        {
            hoodie.Pose(-4f, 0.015f, 0f);
            shorts.Pose(-2f);
            head.Pose(-3f, 0.01f, 0f);
            neck.Pose(-2f);
            armBack.Pose(10f + wave * 6f);
            armFront.Pose(16f - wave * 6f);
            foreBack.Pose(6f + wave * 4f);
            foreFront.Pose(8f - wave * 4f);
            bootBack.Pose(0f, -0.03f, 0.01f);
            bootFront.Pose(0f, 0.02f, 0f);
            Plant(0f);
        }

        /// <summary>부츠 그림은 돌리지 않는다. 앞으로 지날 때만 살짝 들고, 디딜 때는 발바닥이 수평이다.</summary>
        private static void StepFoot(Limb boot, float phase, float reach, float hop)
        {
            float travel = Mathf.Cos(phase);
            float up = travel > 0f ? travel * travel * hop : 0f;
            boot.Pose(0f, Mathf.Sin(phase) * reach, up);
        }

        private void Plant(float drop)
        {
            rig.localPosition = new Vector3(0f, FootDrop - drop, 0f);
        }

        private float AttackAngle()
        {
            if (WeaponHand == null) return 0f;
            var weapon = WeaponHand.Find("WeaponPivot");
            if (weapon == null) return 0f;
            float angle = weapon.localEulerAngles.z;
            if (angle > 180f) angle -= 360f;
            return angle;
        }

        private void ApplyFade()
        {
            if (parts == null) return;
            bool show = motion == null || motion.PoseVisible;
            float alpha = motion != null ? motion.PoseAlpha : 1f;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                var color = parts[i].color;
                color.r = color.g = color.b = 1f;
                color.a = alpha;
                parts[i].color = color;
                if (!show) parts[i].enabled = false;
            }
        }

        private static float Step(float wave) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(wave));

        /// <summary>같은 팔 그림을 위팔 끝점에 겹쳐 붙여 하나의 팔로 잇는다.</summary>
        private Limb ExtendArm(string name, string path, Limb upper, float pivotX, float pivotY, int order)
        {
            if (!PlayerArt.OpaqueEnds(path, out var top, out var bottom))
                return Place(name, null, Vector3.zero, order, upper.Transform);
            var sprite = PlayerArt.LoadSheet(path, top.x, top.y, Ppu);
            var tip = new Vector3((bottom.x - pivotX) / Ppu, (bottom.y - pivotY) / Ppu, 0f);
            var axis = new Vector3((bottom.x - top.x) / Ppu, (bottom.y - top.y) / Ppu, 0f);
            return Place(name, sprite, tip - axis * 0.58f, order, upper.Transform);
        }

        private static Vector3 HandPoint(string path)
        {
            if (!PlayerArt.OpaqueEnds(path, out var top, out var bottom)) return new Vector3(0f, -0.35f, 0f);
            return new Vector3((bottom.x - top.x) / Ppu, (bottom.y - top.y) / Ppu, 0f);
        }

        private Limb LimbAt(string name, string path, float jointX, float jointY, int order, Transform parent, float originX, float originY, bool flipX = false, float shiftX = 0f)
        {
            var sprite = PlayerArt.LoadSheet(path, jointX, jointY, Ppu, flipX);
            var local = new Vector3((jointX - originX) / Ppu + shiftX, (jointY - originY) / Ppu, 0f);
            return Place(name, sprite, local, order, parent);
        }

        private Limb Place(string name, Sprite sprite, Vector3 local, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            var source = GetComponent<SpriteRenderer>();
            if (source != null) renderer.sortingLayerID = source.sortingLayerID;
            return new Limb(go.transform);
        }

        private static float Top(float imageY) => CanvasHeight - imageY;

        private sealed class Limb
        {
            public Transform Transform { get; }
            public Vector3 Rest { get; private set; }
            private Vector3 rest;

            public Limb(Transform transform)
            {
                Transform = transform;
                Rest = rest = transform.localPosition;
            }

            public void StickTo(Transform parent)
            {
                Transform.SetParent(parent, true);
                Rest = rest = Transform.localPosition;
            }

            public void Pose(float degrees, float x = 0f, float y = 0f, float scaleX = 1f, float scaleY = 1f)
            {
                Transform.localPosition = rest + new Vector3(x, y, 0f);
                Transform.localRotation = Quaternion.Euler(0f, 0f, degrees);
                Transform.localScale = new Vector3(scaleX, scaleY, 1f);
            }

            public void AddRotation(float degrees)
            {
                Transform.localRotation *= Quaternion.Euler(0f, 0f, degrees);
            }
        }
    }
}
