using Gstarr.Combat;
using Gstarr.Game;
using Gstarr.Items;
using Gstarr.Player;
using Gstarr.Rhythm;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace Gstarr.World
{
    /// <summary>
    /// 뒤틀린 통로. 배경이 페이드 인 된 뒤 점점 확대되고,
    /// 가운데 글로리 킬을 제한 시간 안에 여러 번 성공하면 다음 방으로 나간다.
    /// </summary>
    public class CorridorChase : MonoBehaviour
    {
        public static bool IsActive { get; private set; }

        private const float EscapeSeconds = 30f;
        private const int GloriesToEscape = 4;
        private const int NotesPerGlory = 4;
        private const float IntroSeconds = 1.22f;

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

        private PlayerController player;
        private CanvasGroup group;
        private CanvasGroup pictureGroup;
        private CanvasGroup hudGroup;
        private Image veil;
        private RectTransform zoomRoot;
        private Image photoA;
        private Image photoB;
        private Image redWash;
        private Image flash;
        private Text timerText;
        private Text countText;
        private Text status;
        private RectTransform note;
        private Image noteImage;
        private Text noteLabel;
        private Image ring;
        private Sprite[] passage;
        private int[] chart;
        private int chartCursor;
        private int[] order;
        private long[] beats;
        private float[] slip;
        private int noteIndex;
        private int retries;
        private int successes;
        private float intro;
        private bool playing;
        private float elapsed;
        private float depth;
        private float cool;
        private float statusTimer;
        private float escapeTimer;
        private float travel = 0.55f;
        private double setStart;
        private bool escaping;
        private bool arrived;
        private bool ended;
        private static Sprite arrowSprite;

        public static void Begin(PlayerController runner)
        {
            if (IsActive || runner == null) return;
            var go = new GameObject("CorridorChase");
            var chase = go.AddComponent<CorridorChase>();
            chase.player = runner;
            chase.Build();
            runner.SetScripted(true);
            IsActive = true;
        }

        private void Build()
        {
            var canvas = UIFactory.CreateCanvas(transform, "CorridorCanvas", 90);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;

            zoomRoot = UIFactory.CreateRect("Zoom", canvas.transform);
            UIFactory.Stretch(zoomRoot);
            pictureGroup = zoomRoot.gameObject.AddComponent<CanvasGroup>();
            pictureGroup.alpha = 0f;
            pictureGroup.blocksRaycasts = false;
            pictureGroup.interactable = false;
            photoA = UIFactory.CreateImage("PhotoA", zoomRoot, Color.white);
            photoB = UIFactory.CreateImage("PhotoB", zoomRoot, Color.white);
            UIFactory.Stretch(photoA.rectTransform);
            UIFactory.Stretch(photoB.rectTransform);
            photoA.preserveAspect = false;
            photoB.preserveAspect = false;
            photoB.color = new Color(1f, 1f, 1f, 0f);

            passage = LoadPassage();
            photoA.sprite = passage[0];
            photoB.sprite = passage.Length > 1 ? passage[1] : passage[0];
            if (photoA.sprite == null) photoA.color = new Color(0.12f, 0.05f, 0.18f);

            veil = UIFactory.CreateImage("Veil", canvas.transform, new Color(0.02f, 0.01f, 0.045f, 0f));
            UIFactory.Stretch(veil.rectTransform);

            redWash = UIFactory.CreateImage("Red", canvas.transform, new Color(0.72f, 0.02f, 0.05f, 0f));
            UIFactory.Stretch(redWash.rectTransform);

            var hud = UIFactory.CreateRect("Hud", canvas.transform);
            UIFactory.Stretch(hud);
            hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
            hudGroup.alpha = 0f;
            hudGroup.blocksRaycasts = false;
            hudGroup.interactable = false;

            ring = UIFactory.CreateImage("Ring", canvas.transform, Color.white);
            bool ringFromAsset = false;
            ring.sprite = RingSprite(out ringFromAsset);
            ring.preserveAspect = true;
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ring.rectTransform.sizeDelta = new Vector2(96f, 96f);
            if (!ringFromAsset) ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            ring.gameObject.SetActive(false);

            note = UIFactory.CreateRect("Note", canvas.transform);
            note.anchorMin = note.anchorMax = new Vector2(0.5f, 0.5f);
            note.pivot = new Vector2(0.5f, 0.5f);
            note.sizeDelta = new Vector2(132f, 132f);
            noteImage = UIFactory.CreateImage("Arrow", note, Color.white);
            noteImage.preserveAspect = true;
            UIFactory.Stretch(noteImage.rectTransform);
            note.gameObject.SetActive(false);

            noteLabel = UIFactory.CreateText("Face", canvas.transform, "", 36);
            noteLabel.rectTransform.anchorMin = noteLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            noteLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            noteLabel.rectTransform.sizeDelta = new Vector2(80f, 50f);
            noteLabel.gameObject.SetActive(false);

            var title = UIFactory.CreateText("Title", hud, "뒤틀린 통로", 42);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(720f, 54f));
            title.color = new Color(0.92f, 0.84f, 1f);

            timerText = UIFactory.CreateText("Timer", hud, "30", 64);
            UIFactory.Place(timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(280f, 72f));

            countText = UIFactory.CreateText("Count", hud, "0 / 4", 32);
            UIFactory.Place(countText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -176f), new Vector2(280f, 42f));
            countText.color = new Color(1f, 0.9f, 0.55f);

            status = UIFactory.CreateText("Status", hud, "", 40);
            UIFactory.Place(status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 168f), new Vector2(640f, 56f));

            var prompt = UIFactory.CreateText("Prompt", hud, "가운데로 들어오는 방향을 맞혀라", 26);
            UIFactory.Place(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(900f, 40f));
            prompt.color = new Color(1f, 0.94f, 0.86f);

            flash = UIFactory.CreateImage("Flash", canvas.transform, new Color(1f, 0.97f, 0.92f, 0f));
            UIFactory.Stretch(flash.rectTransform);

            chart = GloryChart.Load(StageSession.Current != null ? StageSession.Current.gloryChart : null);
            order = new int[NotesPerGlory];
            beats = new long[NotesPerGlory];
            slip = new float[NotesPerGlory];
        }

        private static Sprite[] LoadPassage()
        {
            var background = Object.FindAnyObjectByType<CameraSystem.ParallaxBackground>();
            var sprites = new Sprite[3];
            for (int i = 0; i < sprites.Length; i++)
                sprites[i] = background != null ? background.SpriteAt(5 + i) : null;
            return sprites;
        }

        private void OnDestroy()
        {
            if (IsActive) IsActive = false;
        }

        private void Update()
        {
            if (player == null)
            {
                Destroy(gameObject);
                return;
            }

            var health = player.GetComponent<Health>();
            if (health != null && health.Current <= 0)
            {
                ended = true;
                player.SetScripted(false);
                Destroy(gameObject);
                return;
            }

            float dt = Time.deltaTime;
            statusTimer = Mathf.Max(0f, statusTimer - dt);
            if (status != null)
            {
                var color = status.color;
                color.a = statusTimer > 0.2f ? 1f : Mathf.Clamp01(statusTimer / 0.2f);
                status.color = color;
            }

            if (escaping)
            {
                TickEscape(dt);
                PushZoom();
                return;
            }

            if (!playing)
            {
                TickIntro(dt);
                PushZoom();
                return;
            }

            if (GamePause.IsPaused || (RhythmConductor.Instance != null && RhythmConductor.Instance.IsPaused))
                return;

            elapsed += dt;
            depth = Mathf.Clamp01(depth + dt * 0.045f);
            float remain = Mathf.Max(0f, EscapeSeconds - elapsed);
            PaintClock(remain);
            if (remain <= 0f)
            {
                TimeUp(health);
                if (ended) return;
            }
            else
                TickGlory();

            PushZoom();
        }

        private void TickIntro(float dt)
        {
            intro += dt;
            float dark = intro < 0.24f
                ? intro / 0.24f
                : Mathf.Clamp01(1f - (intro - 0.32f) / 0.7f);
            if (veil != null) veil.color = new Color(0.02f, 0.01f, 0.045f, dark);
            float picture = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 1.02f, intro));
            if (pictureGroup != null) pictureGroup.alpha = picture;
            float hud = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, IntroSeconds, intro));
            if (hudGroup != null) hudGroup.alpha = hud;
            if (ring != null)
            {
                ring.gameObject.SetActive(hud > 0.04f);
                var color = ring.color;
                color.a = hud;
                ring.color = color;
            }

            if (intro < IntroSeconds) return;
            playing = true;
            elapsed = 0f;
            if (ring != null)
            {
                ring.gameObject.SetActive(true);
                ring.color = Color.white;
            }
            BeginSet();
        }

        private void TickEscape(float dt)
        {
            escapeTimer += dt;
            depth = Mathf.MoveTowards(depth, 1f, dt * 0.7f);
            if (veil != null) veil.color = new Color(0.02f, 0.01f, 0.045f, 0f);
            if (flash != null)
            {
                float lift = Mathf.Clamp01(escapeTimer / 0.55f) * 0.35f;
                if (escapeTimer > 0.5f) lift = Mathf.Lerp(0.35f, 0f, (escapeTimer - 0.5f) / 0.8f);
                flash.color = new Color(0.97f, 0.95f, 1f, lift);
            }

            if (!arrived && escapeTimer >= 0.48f)
            {
                arrived = true;
                ArenaDoorSpawner.ArriveAfterCorridor();
            }

            if (escapeTimer >= 0.55f && group != null)
                group.alpha = Mathf.MoveTowards(group.alpha, 0f, dt / 0.85f);
            if (group != null && group.alpha <= 0.55f && player.IsScripted)
                player.SetScripted(false);
            if (arrived && escapeTimer >= 0.95f && escapeTimer - dt < 0.95f)
                StageHUD.ShowHint("통로를 빠져나왔다.", 2.2f);
            if (group != null && group.alpha <= 0.02f)
                Destroy(gameObject);
        }

        private void TimeUp(Health health)
        {
            successes = 0;
            elapsed = 0f;
            cool = 0.45f;
            HideNote();
            Say("시간 초과", new Color(1f, 0.3f, 0.25f));
            PaintCount();
            PaintClock(EscapeSeconds);
            CombatFeel.Shake(0.18f, 0.14f);
            if (health == null) return;
            health.TakeDamage(1, Vector2.left);
            if (health.Current <= 0)
            {
                ended = true;
                player.SetScripted(false);
                Destroy(gameObject);
            }
        }

        private void TickGlory()
        {
            if (cool > 0f)
            {
                cool -= Time.deltaTime;
                if (cool <= 0f) BeginSet();
                return;
            }

            if (order == null || noteIndex >= order.Length) return;
            PlaceNote();
            if (cool > 0f || escaping) return;
            ReadButton();
        }

        private void BeginSet()
        {
            if (escaping || ended) return;
            var conductor = RhythmConductor.Instance;
            if (conductor != null && conductor.IsReady)
                travel = Mathf.Max(0.35f, conductor.CountdownInterval);
            else
                travel = 0.6f;

            double now = conductor != null && conductor.IsReady
                ? conductor.SongTime - conductor.InputOffsetSeconds
                : Time.unscaledTimeAsDouble;
            long first = conductor != null && conductor.IsReady
                ? conductor.FirstBeatAtOrAfter(now + travel)
                : 0;
            setStart = Time.unscaledTimeAsDouble;

            noteIndex = 0;
            for (int i = 0; i < NotesPerGlory; i++)
            {
                order[i] = NextFace();
                beats[i] = first + i;
            }

            retries = 0;
            cool = 0f;
            for (int i = 0; i < slip.Length; i++) slip[i] = 0f;
            if (ring != null) ring.gameObject.SetActive(true);
            if (note != null) note.gameObject.SetActive(true);
            if (noteLabel != null) noteLabel.gameObject.SetActive(true);
        }

        private int NextFace()
        {
            if (chart != null && chart.Length > 0)
            {
                int face = chart[chartCursor % chart.Length];
                chartCursor++;
                return Mathf.Clamp(face, 0, Faces.Length - 1);
            }

            return (successes + noteIndex + chartCursor++) % Faces.Length;
        }

        private void PlaceNote()
        {
            if (note == null || noteIndex >= order.Length) return;
            int faceIndex = order[noteIndex];
            var face = Faces[faceIndex];
            double remain = Remaining(noteIndex);
            float dist = Mathf.Clamp01((float)(remain / travel)) * 420f;
            Vector2 pos = face.Direction * dist;
            note.anchoredPosition = pos;
            note.localScale = Vector3.one * Mathf.Lerp(1.4f, 0.88f, Mathf.Clamp01((float)(remain / travel)));
            ApplyNoteSprite(faceIndex);
            noteLabel.rectTransform.anchoredPosition = pos;
            noteLabel.text = face.Label;

            if (ring != null)
            {
                float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 16f);
                ring.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }

            if (double.IsInfinity(remain))
            {
                FailSet();
                return;
            }

            if (remain < -Window())
            {
                if (retries < 1)
                {
                    retries++;
                    beats[noteIndex] += 1;
                    slip[noteIndex] += travel;
                    return;
                }
                FailSet();
            }
        }

        private void ReadButton()
        {
            if (noteIndex >= order.Length) return;
            int pressed = PressedFace();
            if (pressed < 0) return;
            double remain = Remaining(noteIndex);
            if (Mathf.Abs((float)remain) > Window()) return;
            if (pressed != order[noteIndex])
            {
                FailSet();
                return;
            }

            retries = 0;
            bool last = noteIndex >= order.Length - 1;
            PlayCue(last);
            CombatFeel.Shake(last ? 0.16f : 0.06f, last ? 0.12f : 0.05f);
            depth = Mathf.Clamp01(depth + (last ? 0.1f : 0.035f));
            noteIndex++;
            if (!last) return;

            successes++;
            PaintCount();
            Say("성공", new Color(1f, 0.88f, 0.35f));
            if (successes >= GloriesToEscape)
            {
                BeginEscape();
                return;
            }
            cool = 0.28f;
            HideNote();
        }

        private void FailSet()
        {
            if (cool > 0f || escaping) return;
            cool = 0.42f;
            HideNote();
            Say("다시", new Color(1f, 0.42f, 0.38f));
        }

        private void BeginEscape()
        {
            if (escaping) return;
            escaping = true;
            escapeTimer = 0f;
            HideNote();
            if (ring != null) ring.gameObject.SetActive(false);
            Say("빠져나왔다", new Color(1f, 0.95f, 0.82f));
            if (countText != null) countText.text = GloriesToEscape + " / " + GloriesToEscape;
        }

        private double Remaining(int cue)
        {
            var conductor = RhythmConductor.Instance;
            if (conductor != null && conductor.IsReady && cue >= 0 && cue < beats.Length)
                return conductor.BeatTime(beats[cue]) + conductor.InputOffsetSeconds - conductor.SongTime;
            return setStart + travel * (cue + 1) + slip[cue] - Time.unscaledTimeAsDouble;
        }

        private float Window()
        {
            var conductor = RhythmConductor.Instance;
            return conductor != null && conductor.IsReady ? conductor.GoodWindow : 0.16f;
        }

        private void HideNote()
        {
            if (note != null) note.gameObject.SetActive(false);
            if (noteLabel != null) noteLabel.gameObject.SetActive(false);
        }

        private void PaintClock(float remain)
        {
            if (timerText == null) return;
            timerText.text = Mathf.CeilToInt(remain).ToString();
            float rush = 1f - Mathf.Clamp01(remain / EscapeSeconds);
            timerText.color = Color.Lerp(Color.white, new Color(1f, 0.28f, 0.22f), Mathf.Pow(rush, 1.35f));
            if (redWash != null)
            {
                float pulse = remain < 8f ? 0.08f * Mathf.Sin(Time.unscaledTime * 10f) : 0f;
                redWash.color = new Color(0.75f, 0.02f, 0.04f, Mathf.Pow(rush, 1.55f) * 0.72f + pulse);
            }
        }

        private void PaintCount()
        {
            if (countText != null) countText.text = successes + " / " + GloriesToEscape;
        }

        private void PushZoom()
        {
            if (zoomRoot != null)
            {
                float settle = playing || escaping
                    ? 1f
                    : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 1.05f, intro));
                float near = Mathf.Lerp(1.2f, 1.05f, settle);
                float far = escaping ? 2.45f : 2.05f;
                float run = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(depth));
                zoomRoot.localScale = Vector3.one * Mathf.Lerp(near, far, playing || escaping ? run : 0f);
            }

            if (passage == null || passage.Length == 0 || photoA == null) return;
            float span = Mathf.Clamp01(depth) * (passage.Length - 1);
            int index = Mathf.Clamp(Mathf.FloorToInt(span), 0, passage.Length - 1);
            int next = Mathf.Min(passage.Length - 1, index + 1);
            float blend = next == index ? 1f : span - index;
            photoA.sprite = passage[index];
            photoA.color = photoA.sprite != null ? Color.white : new Color(0.12f, 0.05f, 0.18f);
            if (photoB == null) return;
            photoB.sprite = passage[next];
            var tint = photoB.sprite != null ? Color.white : photoA.color;
            tint.a = next == index ? 0f : blend;
            photoB.color = tint;
        }

        private void Say(string text, Color color)
        {
            if (status == null) return;
            status.text = text;
            color.a = 1f;
            status.color = color;
            statusTimer = 0.6f;
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

        private static void PlayCue(bool success)
        {
            var assets = GameAssets.Instance;
            AudioClip clip = assets != null
                ? (success ? assets.glorySuccessSound : assets.gloryNoteSound)
                : null;
            if (clip != null) GameAudio.PlayClip(clip, success ? 1f : 0.75f);
            else GameAudio.Play(success ? GameAudio.Id.GlorySuccess : GameAudio.Id.GloryNote, success ? 1f : 0.75f);
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
                    px[y * s + x] = new Color32(240, 244, 250, 255);
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            arrowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return arrowSprite;
        }
    }
}
