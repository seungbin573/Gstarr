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
    /// 차트 노트는 곡의 정박 번호와 맞춘다. 곧 닿는 박자는 건너뛰고 그 다음 노트부터 5개를 친다.
    /// </summary>
    public class GloryKillDirector : MonoBehaviour
    {
        private const int NotesPerKill = 5;
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
        private AudioSource hitSource;
        private static Sprite arrowSprite;
        private static AudioClip hitClip;

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

            int[] chart = LoadOrder();
            if (chart == null || chart.Length == 0)
            {
                IsRunning = false;
                return;
            }

            // 곧 닿는 정박은 노트가 바로 붙어서 너무 빠르다. 항상 그 다음 정박 노트부터.
            order = new int[NotesPerKill];
            beats = new long[NotesPerKill];
            if (conductor != null && conductor.IsReady)
            {
                long first = conductor.FirstBeatAtOrAfter(conductor.SongTime) + 1;
                for (int i = 0; i < NotesPerKill; i++)
                {
                    long beat = first + i;
                    beats[i] = beat;
                    order[i] = chart[(int)(PositiveMod(beat, chart.Length))];
                }
                travel = Mathf.Max(0.35f, conductor.CountdownInterval);
            }
            else
            {
                for (int i = 0; i < NotesPerKill; i++)
                {
                    beats[i] = i;
                    order[i] = chart[i % chart.Length];
                }
                travel = 0.6f;
            }

            enemy.BeginGlory();
            GamePause.SetGloryKill(true);
            if (cameraFollow != null) cameraFollow.BeginFocus(enemy.transform, 3.1f);
            BuildUi();
        }

        /// <summary>스테이지의 noteforge 차트를 순서대로. 없으면 네 방향 한 번씩.</summary>
        private static int[] LoadOrder()
        {
            var stage = StageSession.Current;
            var parsed = stage != null ? GloryChart.Load(stage.gloryChart) : null;
            if (parsed != null && parsed.Length > 0) return parsed;

            var fallback = new[] { 0, 1, 2, 3 };
            for (int i = 0; i < fallback.Length; i++)
            {
                int j = Random.Range(i, fallback.Length);
                (fallback[i], fallback[j]) = (fallback[j], fallback[i]);
            }
            return fallback;
        }

        private static long PositiveMod(long value, int length)
        {
            long m = value % length;
            return m < 0 ? m + length : m;
        }

        private void BuildUi()
        {
            if (canvasRoot != null) Destroy(canvasRoot.gameObject);
            var canvas = UIFactory.CreateCanvas(transform, "GloryCanvas", 140);
            canvasRoot = (RectTransform)canvas.transform;

            ring = UIFactory.CreateImage("Ring", canvasRoot, Color.white);
            ring.sprite = RingSprite(out bool ringFromAsset);
            ring.preserveAspect = true;
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ring.rectTransform.sizeDelta = new Vector2(96f, 96f);
            if (!ringFromAsset) ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            note = UIFactory.CreateRect("Note", canvasRoot);
            note.anchorMin = note.anchorMax = new Vector2(0.5f, 0.5f);
            note.pivot = new Vector2(0.5f, 0.5f);
            note.sizeDelta = new Vector2(132f, 132f);
            noteImage = UIFactory.CreateImage("Arrow", note, Color.white);
            noteImage.preserveAspect = true;
            UIFactory.Stretch(noteImage.rectTransform);

            noteLabel = UIFactory.CreateText("Face", canvasRoot, "", 36, style: FontStyle.Bold);
            noteLabel.rectTransform.anchorMin = noteLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            noteLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            noteLabel.rectTransform.sizeDelta = new Vector2(80f, 50f);
            noteLabel.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);

            if (hitSource == null) hitSource = gameObject.AddComponent<AudioSource>();
            hitSource.playOnAwake = false;
            hitSource.spatialBlend = 0f;

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
            if (index >= order.Length || note == null) return;
            int faceIndex = order[index];
            var face = Faces[faceIndex];
            double remain = Remaining(index);
            float dist = Mathf.Clamp01((float)(remain / travel)) * 460f;

            Vector2 center = EnemyLocalPoint();
            Vector2 pos = center + face.Direction * dist;
            note.anchoredPosition = pos;
            ApplyNoteSprite(faceIndex);
            noteLabel.rectTransform.anchoredPosition = pos;
            noteLabel.text = face.Label;
            note.localScale = Vector3.one * Mathf.Lerp(1.45f, 0.9f, Mathf.Clamp01((float)(remain / travel)));

            if (ring != null)
            {
                ring.rectTransform.anchoredPosition = center;
                float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 16f);
                ring.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }

            if (double.IsInfinity(remain) || remain < -Window()) Fail();
        }

        private void ReadButton()
        {
            if (order == null || index >= order.Length) return;
            int pressed = PressedFace();
            if (pressed < 0) return;

            double remain = Remaining(index);
            if (Mathf.Abs((float)remain) > Window()) return;

            if (pressed != order[index])
            {
                Fail();
                return;
            }

            PlayHit();
            index++;
            if (index >= order.Length) Succeed();
        }

        private double Remaining(int cue)
        {
            if (conductor != null && conductor.IsReady && cue >= 0 && cue < beats.Length)
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
            if (noteLabel != null) noteLabel.gameObject.SetActive(false);
            endTimer = 0.85f;
        }

        private void Fail()
        {
            if (endTimer >= 0f) return;
            if (enemy != null) enemy.CancelGlory();
            banner.text = "FAILED";
            banner.color = new Color(1f, 0.35f, 0.3f);
            if (note != null) note.gameObject.SetActive(false);
            if (noteLabel != null) noteLabel.gameObject.SetActive(false);
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

        private void PlayHit()
        {
            if (hitSource == null) return;
            hitSource.PlayOneShot(HitClip(), 0.85f);
        }

        private void ApplyNoteSprite(int faceIndex)
        {
            if (noteImage == null) return;
            noteImage.sprite = NoteSprite(faceIndex, out bool fromAsset);
            noteImage.color = fromAsset ? Color.white : Faces[faceIndex].Color;
            if (fromAsset)
            {
                noteImage.rectTransform.localRotation = Quaternion.identity;
                return;
            }

            Vector2 dir = Faces[faceIndex].Direction;
            float angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            noteImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -angle);
        }

        /// <summary>위, 오른쪽, 아래, 왼쪽 순. 에셋이 있으면 그 이미지를 그대로 쓴다.</summary>
        private static Sprite NoteSprite(int face, out bool fromAsset)
        {
            fromAsset = false;
            var assets = GameAssets.Instance;
            if (assets != null && assets.gloryNoteSprites != null
                && face >= 0 && face < assets.gloryNoteSprites.Length
                && assets.gloryNoteSprites[face] != null)
            {
                fromAsset = true;
                return assets.gloryNoteSprites[face];
            }

            return ArrowSprite();
        }

        private static Sprite RingSprite(out bool fromAsset)
        {
            fromAsset = false;
            var assets = GameAssets.Instance;
            if (assets != null && assets.gloryRingSprite != null)
            {
                fromAsset = true;
                return assets.gloryRingSprite;
            }

            return ArrowSprite();
        }

        private static Sprite ArrowSprite()
        {
            if (arrowSprite != null) return arrowSprite;
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var clear = new Color32(0, 0, 0, 0);
            var px = new Color32[s * s];
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float nx = (x + 0.5f) / s * 2f - 1f;
                    float ny = (y + 0.5f) / s * 2f - 1f;
                    bool shaft = Mathf.Abs(nx) <= 0.28f && ny >= -0.72f && ny <= 0.28f;
                    bool head = ny >= 0.05f && ny <= 0.82f && Mathf.Abs(nx) <= (0.82f - ny) * 0.95f;
                    if (!shaft && !head) continue;
                    bool edge = (Mathf.Abs(nx) > 0.18f && ny < 0.28f) || (head && Mathf.Abs(nx) > (0.72f - ny) * 0.95f);
                    byte a = 255;
                    px[y * s + x] = edge ? new Color32(255, 255, 255, a) : new Color32(230, 236, 245, a);
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            arrowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return arrowSprite;
        }

        private static AudioClip HitClip()
        {
            if (hitClip != null) return hitClip;
            const int rate = 44100;
            int n = rate / 14;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * 36f);
                data[i] = (Mathf.Sin(t * 880f * Mathf.PI * 2f) * 0.65f + Mathf.Sin(t * 1320f * Mathf.PI * 2f) * 0.35f) * env;
            }
            hitClip = AudioClip.Create("GloryHit", n, 1, rate, false);
            hitClip.SetData(data, 0);
            return hitClip;
        }
    }
}
