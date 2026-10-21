using Gstarr.CameraSystem;
using Gstarr.Game;
using Gstarr.Items;
using Gstarr.Rhythm;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace Gstarr.Combat
{
    /// <summary>
    /// 글로리 킬. 카메라가 적에게 다가가고 주변은 슬로우가 된다.
    /// 적 중심으로 네 방향 노트가 박자에 도착하고, A/B/X/Y 로 맞추면 성공한다.
    /// </summary>
    public class GloryKillDirector : MonoBehaviour
    {
        private struct Face
        {
            public string Label;
            public Vector2 Direction;
            public Color Color;
        }

        private static readonly Face[] Faces =
        {
            new Face { Label = "X", Direction = Vector2.up, Color = new Color(0.35f, 0.85f, 1f) },
            new Face { Label = "A", Direction = Vector2.right, Color = new Color(1f, 0.55f, 0.2f) },
            new Face { Label = "B", Direction = Vector2.down, Color = new Color(1f, 0.35f, 0.35f) },
            new Face { Label = "Y", Direction = Vector2.left, Color = new Color(0.45f, 0.95f, 0.45f) }
        };

        public static GloryKillDirector Instance { get; private set; }
        public bool IsRunning { get; private set; }

        private EnemyActor enemy;
        private RhythmConductor conductor;
        private CameraFollow2D cameraFollow;
        private int[] order;
        private long[] beats;
        private bool[] hit;
        private int index;
        private float travel = 1.05f;
        private float endTimer = -1f;
        private double sequenceStart;

        private RectTransform canvasRoot;
        private RectTransform note;
        private Image noteImage;
        private Text noteLabel;
        private Text banner;
        private Image ring;

        public static void Begin(EnemyActor target)
        {
            if (target == null || GamePause.BlocksGameplay) return;
            if (Instance != null && Instance.IsRunning) return;

            var host = Instance != null ? Instance.gameObject : new GameObject("GloryKill");
            var director = host.GetComponent<GloryKillDirector>();
            if (director == null) director = host.AddComponent<GloryKillDirector>();
            director.StartSequence(target);
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void StartSequence(EnemyActor target)
        {
            enemy = target;
            conductor = RhythmConductor.Instance;
            cameraFollow = Object.FindAnyObjectByType<CameraFollow2D>();
            index = 0;
            endTimer = -1f;
            sequenceStart = Time.unscaledTimeAsDouble;
            IsRunning = true;

            order = new[] { 0, 1, 2, 3 };
            for (int i = 0; i < order.Length; i++)
            {
                int j = Random.Range(i, order.Length);
                (order[i], order[j]) = (order[j], order[i]);
            }

            beats = new long[4];
            hit = new bool[4];
            if (conductor != null && conductor.IsReady)
            {
                long first = conductor.FirstBeatAtOrAfter(conductor.SongTime + 0.45);
                for (int i = 0; i < 4; i++) beats[i] = first + i;
                travel = Mathf.Max(0.55f, conductor.CountdownInterval * 1.6f);
            }
            else
            {
                for (int i = 0; i < 4; i++) beats[i] = i;
            }

            enemy.BeginGlory();
            GamePause.SetGloryKill(true);
            if (cameraFollow != null) cameraFollow.BeginFocus(enemy.transform, 3.1f);
            BuildUi();
        }

        private void BuildUi()
        {
            if (canvasRoot != null) Destroy(canvasRoot.gameObject);
            var canvas = UIFactory.CreateCanvas(transform, "GloryCanvas", 140);
            canvasRoot = (RectTransform)canvas.transform;

            ring = UIFactory.CreateImage("Ring", canvasRoot, new Color(1f, 0.85f, 0.2f, 0.9f));
            ring.rectTransform.sizeDelta = new Vector2(86f, 86f);

            var noteRt = UIFactory.CreateImage("Note", canvasRoot, Color.white);
            note = noteRt.rectTransform;
            note.sizeDelta = new Vector2(92f, 92f);
            noteImage = noteRt;
            noteLabel = UIFactory.CreateText("Face", note, "", 42, style: FontStyle.Bold);
            UIFactory.Stretch(noteLabel.rectTransform);
            noteLabel.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);

            banner = UIFactory.CreateText("Banner", canvasRoot, "GLORY KILL", 72, style: FontStyle.BoldAndItalic);
            UIFactory.Place(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(900f, 100f));
            banner.color = new Color(1f, 0.82f, 0.2f);
        }

        private void Update()
        {
            if (!IsRunning || GamePause.IsPaused) return;

            if (endTimer >= 0f)
            {
                endTimer -= Time.unscaledDeltaTime;
                if (endTimer <= 0f) Finish();
                return;
            }

            if (enemy == null)
            {
                Fail();
                return;
            }

            PlaceNote();
            ReadButton();
        }

        private void PlaceNote()
        {
            if (index >= 4 || note == null) return;
            var face = Faces[order[index]];
            double remain = Remaining(index);
            float dist = Mathf.Clamp01((float)(remain / travel)) * 460f;

            Vector2 center = EnemyLocalPoint();
            note.anchoredPosition = center + face.Direction * dist;
            noteImage.color = face.Color;
            noteLabel.text = face.Label;
            note.localScale = Vector3.one * Mathf.Lerp(1.35f, 0.85f, Mathf.Clamp01((float)(remain / travel)));

            if (ring != null)
            {
                ring.rectTransform.anchoredPosition = center;
                float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 16f);
                ring.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }

            if (remain < -Window()) Fail();
        }

        private void ReadButton()
        {
            if (index >= 4) return;
            int pressed = PressedFace();
            if (pressed < 0) return;

            double remain = Remaining(index);
            if (Mathf.Abs((float)remain) > Window()) return;

            if (pressed != order[index])
            {
                Fail();
                return;
            }

            hit[index] = true;
            index++;
            if (index >= 4) Succeed();
        }

        private double Remaining(int cue)
        {
            if (conductor != null && conductor.IsReady)
                return conductor.BeatTime(beats[cue]) - conductor.SongTime;
            return sequenceStart + 0.6 + cue * 0.65 - Time.unscaledTimeAsDouble;
        }

        private float Window()
        {
            return conductor != null && conductor.IsReady ? conductor.GoodWindow : 0.12f;
        }

        private Vector2 EnemyLocalPoint()
        {
            var cam = Camera.main;
            if (cam == null || enemy == null) return Vector2.zero;
            Vector2 screen = cam.WorldToScreenPoint(enemy.transform.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot, screen, null, out var local);
            return local;
        }

        private static int PressedFace()
        {
            var pad = Gamepad.current;
            var key = Keyboard.current;
            if (Pressed(pad?.buttonNorth, key?.xKey, key?.upArrowKey)) return 0;
            if (Pressed(pad?.buttonEast, key?.aKey, key?.rightArrowKey)) return 1;
            if (Pressed(pad?.buttonSouth, key?.bKey, key?.downArrowKey)) return 2;
            if (Pressed(pad?.buttonWest, key?.yKey, key?.leftArrowKey)) return 3;
            return -1;
        }

        private static bool Pressed(ButtonControl pad, KeyControl key, KeyControl alt)
        {
            if (pad != null && pad.wasPressedThisFrame) return true;
            if (key != null && key.wasPressedThisFrame) return true;
            return alt != null && alt.wasPressedThisFrame;
        }

        private void Succeed()
        {
            int bonus = 1000;
            var combo = ComboSystem.Instance;
            if (combo != null)
            {
                bonus = Mathf.RoundToInt(1000 * combo.RankMultiplier);
                combo.AddBonus(bonus);
            }

            var assets = GameAssets.Instance;
            if (assets != null && assets.defaultItems != null && assets.defaultItems.Length > 0 && enemy != null)
            {
                var item = assets.defaultItems[Random.Range(0, assets.defaultItems.Length)];
                if (item != null) ItemPickup.Spawn(item, 1, enemy.transform.position + Vector3.up * 0.4f, 6);
            }

            if (enemy != null) enemy.FinishGlory();
            banner.text = "GLORY KILL  +" + bonus.ToString("N0");
            banner.color = new Color(1f, 0.9f, 0.35f);
            if (note != null) note.gameObject.SetActive(false);
            endTimer = 0.85f;
        }

        private void Fail()
        {
            if (endTimer >= 0f) return;
            if (enemy != null) enemy.CancelGlory();
            banner.text = "FAILED";
            banner.color = new Color(1f, 0.35f, 0.3f);
            if (note != null) note.gameObject.SetActive(false);
            endTimer = 0.55f;
        }

        private void Finish()
        {
            IsRunning = false;
            GamePause.SetGloryKill(false);
            if (cameraFollow != null) cameraFollow.EndFocus();
            if (canvasRoot != null) Destroy(canvasRoot.gameObject);
            enemy = null;
        }
    }
}
