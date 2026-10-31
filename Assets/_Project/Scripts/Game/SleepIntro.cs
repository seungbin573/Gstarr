using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gstarr.Game
{
    /// <summary>
    /// 스테이지 시작 인트로: 음악 시간에 졸다가 잠들어 꿈(스테이지)으로 넘어가는 장면.
    ///   1) 교실 그림이 천천히 에이든 쪽으로 다가오고, 대사가 한 글자씩 나온다.
    ///      메트로놈 소리(째깍)가 곡 박자로 시작해 점점 느려지고 작아진다.
    ///   2) 대사마다 눈꺼풀이 무겁게 감겼다 뜨고, 마지막 대사 뒤 완전히 감긴다.
    ///   3) 어둠 속 "Zzz..." 뒤 꿈 소리가 퍼지고, 눈을 뜨면 보라빛 꿈속(스테이지)이다.
    /// 입력이 없어도 끝까지 자동으로 진행된다 (부스용).
    ///   A / Enter / Space / 클릭 = 다음 줄,  START / ESC = 건너뛰기.
    /// 같은 스테이지를 재시작하면 눈 뜨는 장면만 짧게 나온다. 메인 메뉴로 가면 초기화.
    ///
    /// 쓰는 곳: StageHUD.PlayOpeningCutscene() 에서
    ///   yield return SleepIntro.Play(그림, 제목, 대사들, 박자 간격);
    /// 값을 바꾸고 싶으면 아래 [튜닝] 상수만 고치면 된다.
    /// </summary>
    public class SleepIntro : MonoBehaviour
    {
        // ───────── [튜닝] ─────────
        private const int SortingOrder = 300;          // StageCanvas(120) 보다 위
        private const float CharSeconds = 0.05f;       // 한 글자 나오는 시간
        private const float LineHoldBase = 1.4f;       // 한 줄 다 나온 뒤 기다리는 시간
        private const float LineHoldPerChar = 0.03f;   // 긴 줄은 조금 더 기다림
        private const float ZoomAmount = 0.14f;        // 그림이 다가오는 정도 (0.14 = 14%)
        private static readonly Vector2 ZoomFocus = new Vector2(0.47f, 0.45f); // 그림 속 에이든 위치 (왼쪽 아래 0,0)
        private static readonly Color DreamColor = new Color(0.45f, 0.25f, 0.8f);
        private const string SleepText = "Z z z . . .";
        // ──────────────────────────

        private const float HalfScreen = 540f;   // 캔버스 기준 높이 1080 의 절반
        private const float LidEdge = 140f;      // 눈꺼풀 끝 부드러운 부분 높이

        private static string seenScene;

        private CanvasGroup rootGroup;
        private Image cover;
        private bool photoStill;
        private RectTransform sceneLayer;
        private Image fadeBlack;
        private Image photo;
        private Image dim;
        private Image vignette;
        private Image dreamTint;
        private RectTransform topLid, bottomLid, topSolid, bottomSolid;
        private CanvasGroup caption;
        private Text titleText, bodyText, promptText, centerText;
        private AudioSource audioSource;
        private AudioClip tickClip, tockClip, dreamClip;

        private float lid;            // 0 = 눈 뜸, 1 = 완전히 감김
        private float drowsy;         // 0 = 멀쩡, 1 = 잠듦
        private float drowsyTarget;
        private float zoomTime;
        private float zoomDuration = 12f;
        private float swayTime;
        private bool metronomeOn;
        private float beatInterval = 0.5f;
        private bool skipped;
        private string dreamTitle;
        private string[] dreamLines;
        private Sprite dreamImage;
        private Coroutine blinkRoutine;

        // ───────────────────────── 진입점 ─────────────────────────

        /// <summary>인트로 전체를 재생하고 끝나면 스스로 사라진다.</summary>
        public static IEnumerator Play(
            Sprite classroom, string title, string[] lines, float beatInterval, System.Action onWake = null,
            string dreamTitle = null, string[] dreamLines = null, Sprite dreamImage = null)
        {
            string scene = SceneManager.GetActiveScene().name;
            bool shortVersion = seenScene == scene;
            seenScene = scene;

            var go = new GameObject("SleepIntro");
            var intro = go.AddComponent<SleepIntro>();
            intro.beatInterval = Mathf.Clamp(beatInterval, 0.25f, 1.2f);
            intro.dreamTitle = dreamTitle;
            intro.dreamLines = dreamLines;
            intro.dreamImage = dreamImage;
            intro.Build(classroom, title);

            if (shortVersion)
                yield return intro.StartCoroutine(intro.RunShort());
            else
                yield return intro.StartCoroutine(intro.RunFull(lines ?? new string[0]));

            onWake?.Invoke();
            yield return intro.StartCoroutine(intro.WakeForCountdown());
            Destroy(go);
        }

        /// <summary>깨서 들킨 엔딩. 눈을 뜨면 컷신, 끝나면 어두워진 뒤 결과 화면을 드러낸다.</summary>
        public static IEnumerator PlayCaught(Sprite image, string[] lines, System.Action reveal)
        {
            var go = new GameObject("SleepIntro");
            var intro = go.AddComponent<SleepIntro>();
            intro.Build(image, "");
            yield return intro.StartCoroutine(intro.RunCaught(lines ?? new string[0]));
            reveal?.Invoke();
            yield return intro.StartCoroutine(FadeGroup(intro.rootGroup, 1f, 0f, intro.skipped ? 0.2f : 0.45f));
            Destroy(go);
        }

        /// <summary>클리어. 눈을 뜨면 해냈다는 컷신이 나오고, 그 다음 결과 화면으로 넘긴다.</summary>
        public static IEnumerator PlayClear(Sprite image, string[] lines, System.Action reveal)
        {
            var go = new GameObject("SleepIntro");
            var intro = go.AddComponent<SleepIntro>();
            intro.Build(image, "해냈다!");
            yield return intro.StartCoroutine(intro.RunClear(lines ?? new string[0]));
            reveal?.Invoke();
            yield return intro.StartCoroutine(FadeGroup(intro.rootGroup, 1f, 0f, intro.skipped ? 0.25f : 0.55f));
            Destroy(go);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            seenScene = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // 메인 메뉴로 돌아가면 다음 관람객은 처음부터 본다
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (FindAnyObjectByType<MainMenu>() != null) seenScene = null;
        }

        // ───────────────────────── 장면 순서 ─────────────────────────

        private IEnumerator RunFull(string[] lines)
        {
            // 이야기가 시작되는 순간 곡을 튼다. 눈 뜨는 카운트에서 다시 시작하지 않는다.
            Gstarr.Rhythm.RhythmConductor.Instance?.BeginWithStory();

            // 0. 어둠에서 교실이 밝아진다
            SetLids(0f);
            sceneLayer.gameObject.SetActive(true);
            zoomDuration = 3f + lines.Length * 3.6f;
            metronomeOn = true;
            StartCoroutine(Metronome());
            yield return FadeImage(fadeBlack, 1f, 0f, 1.2f);

            // 1. 대사 한 줄씩. 줄마다 더 졸리고, 줄 사이에 눈이 무겁게 감겼다 뜬다
            int n = lines.Length;
            for (int i = 0; i < n && !skipped; i++)
            {
                drowsyTarget = 0.85f * (i + 1) / Mathf.Max(1, n);
                yield return TypeLine(lines[i], i == n - 1);
                if (skipped) break;

                if (i < n - 1)
                {
                    float depth = Mathf.Lerp(0.4f, 0.75f, n > 2 ? i / (float)(n - 2) : 0f);
                    float rest = Mathf.Lerp(0.05f, 0.22f, n > 2 ? i / (float)(n - 2) : 0f);
                    StopBlink();
                    blinkRoutine = StartCoroutine(Blink(depth, rest));
                }
            }

            // 2. 완전히 잠든다
            StopBlink();
            drowsyTarget = 1f;
            StartCoroutine(FadeGroup(caption, caption.alpha, 0f, skipped ? 0.2f : 0.7f));
            yield return AnimateLid(lid, 1f, skipped ? 0.3f : 1.6f, EaseInOut);
            metronomeOn = false;
            sceneLayer.gameObject.SetActive(false);

            if (!skipped)
            {
                yield return Wait(0.5f);
                yield return ShowCenter(SleepText, 0.5f, 1.0f, 0.5f);
            }

            // 잠든 뒤의 꿈 대사는 같은 자막으로 이어 가고, 그 다음 눈이 뜨이며 곡이 시작한다.
            if (!skipped && dreamLines != null && dreamLines.Length > 0)
                yield return ContinueDream();

            // 3. 눈은 감긴 채로 넘긴다. 3, 2, 1 은 Play 가 이어서 맞춘다.
        }

        private IEnumerator ContinueDream()
        {
            PlaySfx(dreamClip, 0.4f);
            if (dreamImage != null)
            {
                photoStill = true;
                sceneLayer.gameObject.SetActive(true);
                photo.sprite = dreamImage;
                photo.color = new Color(0.82f, 0.86f, 0.95f, 1f);
                photo.rectTransform.localScale = Vector3.one;
                fadeBlack.color = Color.black;
                yield return FadeImage(fadeBlack, 1f, 0.28f, 0.9f);
                yield return AnimateLid(1f, 0.18f, 1.15f, EaseOut);
            }

            if (titleText != null) titleText.text = string.IsNullOrEmpty(dreamTitle) ? "" : dreamTitle;
            yield return FadeGroup(caption, caption.alpha, 1f, 0.35f);
            for (int i = 0; i < dreamLines.Length && !skipped; i++)
                yield return TypeLine(dreamLines[i], i == dreamLines.Length - 1);

            yield return FadeGroup(caption, caption.alpha, 0f, skipped ? 0.15f : 0.4f);
            yield return AnimateLid(lid, 1f, skipped ? 0.2f : 0.55f, EaseInOut);
            sceneLayer.gameObject.SetActive(false);
            photoStill = false;
        }

        private IEnumerator RunCaught(string[] lines)
        {
            photoStill = true;
            sceneLayer.gameObject.SetActive(true);
            caption.alpha = 0f;
            fadeBlack.color = Color.clear;
            vignette.color = new Color(0f, 0f, 0f, 0.2f);
            SetLids(1f);
            yield return AnimateLid(1f, 0f, skipped ? 0.15f : 0.65f, EaseOut);
            if (skipped)
            {
                yield return FadeImage(cover, 0f, 1f, 0.15f);
                yield break;
            }

            yield return FadeGroup(caption, 0f, 1f, 0.25f);
            int n = lines.Length;
            for (int i = 0; i < n && !skipped; i++)
            {
                yield return TypeLine(lines[i], i == n - 1);
            }

            yield return FadeImage(cover, 0f, 1f, skipped ? 0.2f : 0.85f);
        }

        private IEnumerator RunClear(string[] lines)
        {
            photoStill = true;
            sceneLayer.gameObject.SetActive(true);
            caption.alpha = 0f;
            fadeBlack.color = Color.clear;
            vignette.color = new Color(1f, 0.72f, 0.28f, 0.18f);
            dreamTint.color = new Color(1f, 0.78f, 0.35f, 0.28f);
            if (titleText != null) titleText.color = new Color(1f, 0.84f, 0.35f);
            centerText.fontSize = 96;
            centerText.color = new Color(1f, 0.84f, 0.4f, 0f);
            SetLids(1f);

            yield return AnimateLid(1f, 0f, skipped ? 0.15f : 0.75f, EaseOut);
            StartCoroutine(FadeImage(dreamTint, dreamTint.color.a, 0f, skipped ? 0.15f : 0.8f));
            if (skipped)
            {
                yield return FadeImage(cover, 0f, 1f, 0.15f);
                yield break;
            }

            centerText.text = "해냈다!";
            yield return FadeText(centerText, 0f, 1f, 0.25f);
            yield return Wait(0.7f);
            yield return FadeText(centerText, 1f, 0f, 0.25f);
            centerText.text = "";

            yield return FadeGroup(caption, 0f, 1f, 0.25f);
            int n = lines.Length;
            for (int i = 0; i < n && !skipped; i++)
                yield return TypeLine(lines[i], i == n - 1);

            yield return FadeImage(cover, 0f, 1f, skipped ? 0.2f : 0.7f);
        }

        private IEnumerator RunShort()
        {
            sceneLayer.gameObject.SetActive(false);
            caption.alpha = 0f;
            fadeBlack.color = Color.clear;
            SetLids(1f);
            yield break;
        }

        private IEnumerator WakeForCountdown()
        {
            sceneLayer.gameObject.SetActive(false);
            caption.alpha = 0f;
            fadeBlack.color = Color.clear;
            SetLids(1f);
            var conductor = Gstarr.Rhythm.RhythmConductor.Instance;
            int beats = conductor != null ? Mathf.Max(1, conductor.CountdownBeats) : 3;
            float beat = beatInterval;
            double duration = beat * beats;
            PlaySfx(dreamClip, 0.55f);
            dreamTint.color = WithAlpha(DreamColor, 0.55f);
            vignette.color = WithAlpha(DreamColor * 0.35f, 0.85f);
            centerText.fontSize = 200;
            centerText.fontStyle = FontStyle.BoldAndItalic;
            centerText.color = Color.white;
            centerText.rectTransform.SetAsLastSibling();

            if (conductor != null && conductor.SongTime >= 0)
            {
                const float open = 0.9f;
                for (float t = 0f; t < open; t += Time.unscaledDeltaTime)
                {
                    float p = Mathf.Clamp01(t / open);
                    SetLids(Mathf.Lerp(1f, 0f, p));
                    dreamTint.color = WithAlpha(DreamColor, Mathf.Lerp(0.55f, 0f, p));
                    vignette.color = WithAlpha(DreamColor * 0.35f, Mathf.Lerp(0.85f, 0f, p));
                    centerText.text = "";
                    yield return null;
                }
                SetLids(0f);
                centerText.text = "";
                dreamTint.color = WithAlpha(DreamColor, 0f);
                vignette.color = WithAlpha(DreamColor * 0.35f, 0f);
                yield break;
            }

            float waited = 0f;
            while (waited < duration + 0.75f)
            {
                double song = conductor != null ? conductor.SongTime : waited - duration;
                float p = duration <= 0.01 ? 1f : Mathf.Clamp01((float)((song + duration) / duration));
                SetLids(LidForCountdown(p));
                dreamTint.color = WithAlpha(DreamColor, Mathf.Lerp(0.55f, 0f, p));
                vignette.color = WithAlpha(DreamColor * 0.35f, Mathf.Lerp(0.85f, 0f, p));
                string label = "";
                if (song < 0)
                {
                    int k = Mathf.CeilToInt((float)(-song / Mathf.Max(0.05f, beat)));
                    if (k >= 1 && k <= beats) label = k.ToString();
                }
                centerText.text = label;
                centerText.color = new Color(1f, 1f, 1f, label.Length > 0 ? 1f : 0f);
                if (song >= 0) break;
                if (SkipPressed())
                {
                    conductor?.SkipOpeningCountdown();
                    break;
                }
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            SetLids(0f);
            centerText.text = "";
            dreamTint.color = WithAlpha(DreamColor, 0f);
            vignette.color = WithAlpha(DreamColor * 0.35f, 0f);
        }

        private static float LidForCountdown(float p)
        {
            if (p < 0.34f) return Mathf.Lerp(1f, 0.42f, EaseOut(p / 0.34f));
            if (p < 0.62f) return Mathf.Lerp(0.42f, 0.68f, (p - 0.34f) / 0.28f);
            return Mathf.Lerp(0.68f, 0f, EaseOut((p - 0.62f) / 0.38f));
        }

        private IEnumerator WakeInDream(float speed)
        {
            float k = 1f / Mathf.Max(0.1f, speed);
            PlaySfx(dreamClip, 0.7f);
            dreamTint.color = WithAlpha(DreamColor, 0.6f);
            vignette.color = WithAlpha(DreamColor * 0.35f, 0.9f);

            // 반쯤 떴다가, 다시 스르르 감기다가, 번쩍 뜬다
            yield return AnimateLid(1f, 0.45f, 0.5f * k, EaseOut);
            yield return AnimateLid(0.45f, 0.7f, 0.35f * k, EaseInOut);
            StartCoroutine(FadeImage(dreamTint, dreamTint.color.a, 0f, 1.4f * k));
            StartCoroutine(FadeImage(vignette, vignette.color.a, 0f, 1.6f * k));
            yield return AnimateLid(0.7f, 0f, 0.45f * k, EaseOut);
            yield return Wait(0.6f * k);
        }

        // ───────────────────────── 대사 ─────────────────────────

        private IEnumerator TypeLine(string line, bool last)
        {
            line = line ?? "";
            promptText.text = last ? "A / ENTER  계속" : "A / ENTER  다음    START / ESC  건너뛰기";
            float t = 0f;
            int shown = 0;
            bodyText.text = "";
            yield return null;

            // 한 글자씩
            while (shown < line.Length)
            {
                if (SkipPressed()) { skipped = true; yield break; }
                if (AdvancePressed()) { shown = line.Length; break; }
                t += Time.unscaledDeltaTime;
                int next = Mathf.Min(line.Length, Mathf.FloorToInt(t / CharSeconds));
                if (next != shown)
                {
                    shown = next;
                    bodyText.text = line.Substring(0, shown);
                }
                yield return null;
            }
            bodyText.text = line;
            yield return null;

            // 다 나오면 잠깐 기다림 (누르면 바로 다음)
            float hold = LineHoldBase + line.Length * LineHoldPerChar;
            for (float h = 0f; h < hold; h += Time.unscaledDeltaTime)
            {
                if (SkipPressed()) { skipped = true; yield break; }
                if (AdvancePressed()) break;
                yield return null;
            }
        }

        private IEnumerator ShowCenter(string text, float fadeIn, float hold, float fadeOut)
        {
            centerText.text = text;
            yield return FadeText(centerText, 0f, 0.85f, fadeIn);
            yield return Wait(hold);
            yield return FadeText(centerText, 0.85f, 0f, fadeOut);
        }

        // ───────────────────────── 눈꺼풀 / 소리 ─────────────────────────

        private void StopBlink()
        {
            if (blinkRoutine != null) StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        private IEnumerator Blink(float depth, float rest)
        {
            float from = lid;
            yield return AnimateLid(from, depth, 0.55f, EaseInOut);
            yield return Wait(0.18f);
            yield return AnimateLid(depth, rest, 0.4f, EaseOut);
        }

        private IEnumerator AnimateLid(float from, float to, float seconds, System.Func<float, float> ease)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetLids(Mathf.Lerp(from, to, ease(t / seconds)));
                yield return null;
            }
            SetLids(to);
        }

        private void SetLids(float value)
        {
            lid = Mathf.Clamp01(value);
            float h = lid * (HalfScreen + LidEdge);
            topLid.sizeDelta = new Vector2(0f, h);
            bottomLid.sizeDelta = new Vector2(0f, h);
            float solid = Mathf.Max(0f, h - LidEdge);
            topSolid.sizeDelta = new Vector2(0f, solid);
            bottomSolid.sizeDelta = new Vector2(0f, solid);
        }

        // 째깍-똑딱. 졸릴수록 느리고 작아진다
        private IEnumerator Metronome()
        {
            bool tick = true;
            while (metronomeOn)
            {
                PlaySfx(tick ? tickClip : tockClip, 0.55f * (1f - 0.7f * drowsy));
                tick = !tick;
                float wait = beatInterval * (1f + drowsy * 1.1f);
                for (float t = 0f; t < wait && metronomeOn; t += Time.unscaledDeltaTime)
                    yield return null;
            }
        }

        private void PlaySfx(AudioClip clip, float volume)
        {
            if (clip == null || audioSource == null) return;
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume) * GameSettings.SfxVolume);
        }

        // ───────────────────────── 매 프레임 분위기 ─────────────────────────

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            drowsy = Mathf.MoveTowards(drowsy, drowsyTarget, dt * 0.3f);

            if (sceneLayer != null && sceneLayer.gameObject.activeSelf && !photoStill)
            {
                zoomTime += dt;
                swayTime += dt;
                float z = EaseInOut(Mathf.Clamp01(zoomTime / zoomDuration));
                photo.rectTransform.localScale = Vector3.one * (1f + ZoomAmount * z);
                // 졸면서 고개가 까딱이는 느낌
                photo.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Sin(swayTime * 0.9f) * 7f * drowsy);
                photo.color = Color.Lerp(Color.white, new Color(0.78f, 0.74f, 0.92f), drowsy);
                dim.color = new Color(0f, 0f, 0f, 0.05f + 0.35f * drowsy);
                vignette.color = new Color(0f, 0f, 0f, 0.35f + 0.6f * drowsy);
            }
        }

        // ───────────────────────── 입력 ─────────────────────────

        private static bool AdvancePressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame
                || keyboard.zKey.wasPressedThisFrame)) return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            var pad = Gamepad.current;
            return pad != null && (pad.buttonEast.wasPressedThisFrame || pad.buttonSouth.wasPressedThisFrame);
        }

        private static bool SkipPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return true;
            var pad = Gamepad.current;
            return pad != null && pad.startButton.wasPressedThisFrame;
        }

        // ───────────────────────── 화면 만들기 ─────────────────────────

        private void Build(Sprite classroom, string title)
        {
            var canvas = UIFactory.CreateCanvas(transform, "SleepIntroCanvas", SortingOrder);
            var root = (RectTransform)canvas.transform;
            rootGroup = root.gameObject.AddComponent<CanvasGroup>();
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;

            // 교실 (잠들면 통째로 끈다 → 뒤에 게임 화면이 보임)
            sceneLayer = UIFactory.Stretch(UIFactory.CreateRect("Classroom", root));
            UIFactory.Stretch(UIFactory.CreateImage("Black", sceneLayer, Color.black).rectTransform);
            photo = UIFactory.CreateImage("Photo", sceneLayer, Color.white);
            UIFactory.Stretch(photo.rectTransform);
            photo.rectTransform.pivot = ZoomFocus;
            photo.sprite = classroom;
            if (classroom == null) photo.color = new Color(0.12f, 0.13f, 0.18f, 1f);
            dim = UIFactory.CreateImage("Dim", sceneLayer, new Color(0f, 0f, 0f, 0.05f));
            UIFactory.Stretch(dim.rectTransform);
            fadeBlack = UIFactory.CreateImage("FadeIn", sceneLayer, Color.black);
            UIFactory.Stretch(fadeBlack.rectTransform);

            vignette = UIFactory.CreateImage("Vignette", root, new Color(0f, 0f, 0f, 0.35f));
            UIFactory.Stretch(vignette.rectTransform);
            vignette.sprite = MakeVignette();

            dreamTint = UIFactory.CreateImage("DreamTint", root, WithAlpha(DreamColor, 0f));
            UIFactory.Stretch(dreamTint.rectTransform);

            // 눈꺼풀
            topLid = MakeLid("TopLid", root, true, out topSolid);
            bottomLid = MakeLid("BottomLid", root, false, out bottomSolid);

            // 자막
            var captionRt = UIFactory.Stretch(UIFactory.CreateRect("Caption", root));
            caption = captionRt.gameObject.AddComponent<CanvasGroup>();
            caption.interactable = false;
            caption.blocksRaycasts = false;
            var bar = UIFactory.CreateImage("CaptionBg", captionRt, new Color(0.015f, 0.02f, 0.04f, 0.8f));
            UIFactory.Place(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1640f, 240f));
            titleText = UIFactory.CreateText("Title", captionRt, title ?? "", 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place(titleText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 212f), new Vector2(1560f, 44f));
            titleText.color = new Color(1f, 0.75f, 0.35f);
            bodyText = UIFactory.CreateText("Body", captionRt, "", 34, TextAnchor.MiddleLeft, FontStyle.Normal);
            UIFactory.Place(bodyText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1560f, 90f));
            promptText = UIFactory.CreateText("Prompt", captionRt, "", 20, TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.Place(promptText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(1560f, 30f));
            promptText.color = new Color(1f, 1f, 1f, 0.6f);

            centerText = UIFactory.CreateText("Sleep", root, "", 64, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            UIFactory.Place(centerText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 160f));
            centerText.color = new Color(0.8f, 0.75f, 1f, 0f);

            cover = UIFactory.CreateImage("Cover", root, Color.black);
            UIFactory.Stretch(cover.rectTransform);
            cover.color = new Color(0f, 0f, 0f, 0f);
            cover.transform.SetAsLastSibling();

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.ignoreListenerPause = true;
            tickClip = MakeClick("sfx-intro-tick", 1650f);
            tockClip = MakeClick("sfx-intro-tock", 1150f);
            dreamClip = MakeDreamSwell();
        }

        private static RectTransform MakeLid(string name, RectTransform root, bool top, out RectTransform solid)
        {
            var anchorY = top ? 1f : 0f;
            var lidRt = UIFactory.CreateRect(name, root);
            lidRt.anchorMin = new Vector2(0f, anchorY);
            lidRt.anchorMax = new Vector2(1f, anchorY);
            lidRt.pivot = new Vector2(0.5f, anchorY);
            lidRt.anchoredPosition = Vector2.zero;
            lidRt.sizeDelta = Vector2.zero;

            // 화면 끝쪽은 까맣게
            var solidImg = UIFactory.CreateImage("Solid", lidRt, Color.black);
            solid = solidImg.rectTransform;
            solid.anchorMin = new Vector2(0f, anchorY);
            solid.anchorMax = new Vector2(1f, anchorY);
            solid.pivot = new Vector2(0.5f, anchorY);
            solid.anchoredPosition = Vector2.zero;
            solid.sizeDelta = Vector2.zero;

            // 화면 가운데 쪽은 부드럽게 흐려짐
            float edgeY = top ? 0f : 1f;
            var edge = UIFactory.CreateImage("Edge", lidRt, Color.black);
            edge.sprite = MakeLidEdge(top);
            var ert = edge.rectTransform;
            ert.anchorMin = new Vector2(0f, edgeY);
            ert.anchorMax = new Vector2(1f, edgeY);
            ert.pivot = new Vector2(0.5f, edgeY);
            ert.anchoredPosition = Vector2.zero;
            ert.sizeDelta = new Vector2(0f, LidEdge);
            return lidRt;
        }

        // ───────────────────────── 작은 도구들 ─────────────────────────

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }

        private static IEnumerator FadeImage(Image img, float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                img.color = WithAlpha(img.color, Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            img.color = WithAlpha(img.color, to);
        }

        private static IEnumerator FadeText(Text text, float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                text.color = WithAlpha(text.color, Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            text.color = WithAlpha(text.color, to);
        }

        private static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            group.alpha = to;
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        private static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        // 가장자리가 어두운 원형 그림 (코드로 생성)
        private static Sprite MakeVignette()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f;
                float dy = (y + 0.5f) / size - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float a = EaseInOut((d - 0.55f) / 0.75f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // 눈꺼풀 끝: 화면 끝쪽은 불투명, 가운데 쪽은 투명
        private static Sprite MakeLidEdge(bool top)
        {
            const int h = 64;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[h];
            for (int y = 0; y < h; y++)
            {
                float u = y / (float)(h - 1);          // 0 = 아래, 1 = 위
                float a = EaseInOut(top ? u : 1f - u);
                px[y] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f));
        }

        // 메트로놈 째깍 소리
        private static AudioClip MakeClick(string name, float freq)
        {
            const int rate = 44100;
            int n = (int)(rate * 0.06f);
            var data = new float[n];
            var rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)rate;
                float tone = Mathf.Sin(u * freq * Mathf.PI * 2f) * 0.5f * Mathf.Exp(-u * 90f);
                float click = ((float)rng.NextDouble() * 2f - 1f) * 0.35f * Mathf.Exp(-u * 500f);
                data[i] = tone + click;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // 꿈에 들어갈 때 퍼지는 화음 (C 장조, 천천히 커졌다 사라짐)
        private static AudioClip MakeDreamSwell()
        {
            const int rate = 44100;
            const float length = 3.2f;
            int n = (int)(rate * length);
            var data = new float[n];
            float[] freqs = { 261.63f, 329.63f, 392f, 523.25f, 783.99f };
            float[] amps = { 0.16f, 0.12f, 0.12f, 0.1f, 0.05f };
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)rate;
                float env = Mathf.Clamp01(u / 0.7f) * Mathf.Exp(-Mathf.Max(0f, u - 0.7f) * 1.1f);
                float vib = 1f + 0.004f * Mathf.Sin(u * 5.5f * Mathf.PI * 2f);
                float s = 0f;
                for (int k = 0; k < freqs.Length; k++)
                    s += Mathf.Sin(u * freqs[k] * vib * Mathf.PI * 2f) * amps[k];
                data[i] = s * env;
            }
            var clip = AudioClip.Create("sfx-intro-dream", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
