using System.Collections;
using System.Collections.Generic;
using Gstarr.CameraSystem;
using Gstarr.Rhythm;
using Gstarr.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gstarr.Game
{
    /// <summary>
    /// 화면 위 음악 진행도 바. 방을 비우면 GO GO, 스테이지를 비우거나 죽으면 결과.
    /// </summary>
    [RequireComponent(typeof(RhythmConductor))]
    public class StageHUD : MonoBehaviour
    {
        [SerializeField] private Vector2 barSize = new Vector2(720f, 14f);
        [SerializeField] private Color barColor = new Color(1f, 0.6f, 0.25f);

        private RhythmConductor conductor;
        private Image fill;
        private Text titleText;
        private Text timeText;
        private RectTransform gameOverPanel;
        private CanvasGroup gameOverGroup;
        private Text resultText;
        private Text resultTitle;
        private Text initialsText;
        private RectTransform initialsPanel;
        private RectTransform resultButtons;
        private Button retryButton;
        private Button continueButton;
        private Button yesButton;
        private Button noButton;
        private Button saveInitialsButton;
        private Button menuButton;
        private bool resultCleared;
        private bool askingContinue;
        private bool continueDeclined;
        private string recordBody;
        private float gameOverFade = -1f;
        private bool resultShown;
        private bool waitingForInitials;
        private bool resultSubmitted;
        private long pendingScore;
        private string pendingStageName;
        private readonly int[] initialIndices = { 0, 0, 0 };
        private int selectedInitial;
        private const string InitialCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private Text hintText;
        private Image hintBackdrop;
        private float hintTimer;
        private float lastArcadeInput;
        private bool comboHintShown;
        private bool returningToMenu;
        private PlayerController playerController;
        private RectTransform cutscenePanel;
        private CanvasGroup cutsceneGroup;
        private Image cutsceneDim;
        private Image cutsceneImage;
        private Text cutsceneTitle;
        private Text cutsceneBody;
        private Text cutscenePrompt;
        private Sprite introCutscene;
        private Sprite clearCutscene;
        private Sprite gameOverCutscene;
        private bool cutsceneActive;
        private readonly HashSet<int> toldStories = new HashSet<int>();
        private RectTransform storyCaption;
        private CanvasGroup storyCaptionGroup;
        private Image storyCaptionImage;
        private Text storyCaptionTitle;
        private Text storyCaptionBody;
        private int storyCaptionToken;
        private Text marchText;
        private bool showMarch;
        private bool routeHintShown;

        private void Awake()
        {
            conductor = GetComponent<RhythmConductor>();
            var canvas = UIFactory.CreateCanvas(transform, "StageCanvas", 120);
            var root = (RectTransform)canvas.transform;

            var bg = UIFactory.CreateImage("ProgressBg", root, new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Place(bg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), barSize + new Vector2(6f, 6f));

            fill = UIFactory.CreateImage("ProgressFill", bg.rectTransform, barColor);
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(3f, 3f);
            frt.offsetMax = new Vector2(-3f, -3f);
            fill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;

            titleText = UIFactory.CreateText("SongTitle", root, "", 24, TextAnchor.UpperLeft, FontStyle.Normal);
            UIFactory.Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(-barSize.x * 0.5f + 300f, -48f), new Vector2(600f, 30f));
            timeText = UIFactory.CreateText("SongTime", root, "", 24, TextAnchor.UpperRight, FontStyle.Normal);
            UIFactory.Place(timeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(barSize.x * 0.5f - 150f, -48f), new Vector2(300f, 30f));
            bg.gameObject.SetActive(false);
            titleText.gameObject.SetActive(false);
            timeText.gameObject.SetActive(false);

            countdownText = UIFactory.CreateText("Countdown", root, "", 220, style: FontStyle.BoldAndItalic);
            UIFactory.Place(countdownText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(900f, 300f));
            countdownText.GetComponent<Outline>().effectDistance = new Vector2(5f, -5f);

            marchText = UIFactory.CreateText("MarchCall", root, "", 110, TextAnchor.MiddleRight, FontStyle.BoldAndItalic);
            UIFactory.Place(marchText.rectTransform, new Vector2(1f, 0.5f), new Vector2(-70f, 10f), new Vector2(860f, 200f));
            marchText.color = new Color(1f, 0.55f, 0.12f, 0f);
            marchText.GetComponent<Outline>().effectDistance = new Vector2(4f, -4f);

            hintBackdrop = UIFactory.CreateImage("GameplayHintBg", root, new Color(0f, 0f, 0f, 0f));
            UIFactory.Place(hintBackdrop.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(1240f, 110f));
            hintText = UIFactory.CreateText("GameplayHint", root, "", 25, style: FontStyle.Bold);
            UIFactory.Place(hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(1180f, 96f));
            hintText.color = new Color(1f, 0.88f, 0.65f, 0f);

            BuildGameOver(root);
            BuildCutscene(root);
            BuildStoryCaption(root);

            countdownDim = UIFactory.CreateImage("CountdownBlack", root, Color.black);
            UIFactory.Stretch(countdownDim.rectTransform);
            countdownDim.raycastTarget = false;
            countdownDim.enabled = true;
            countdownText.text = "";
            countdownText.transform.SetAsLastSibling();
        }

        private Text countdownText;
        private Image countdownDim;
        private string lastCount;
        private float countPop;
        private bool coverUntilWake = true;
        private bool wakeOwnsCountdown;

        // 음악 시작 전 박자에 맞춰 3, 2, 1, 음악 시작 순간 GO!
        private void UpdateCountdown(float dt)
        {
            string label = "";
            double t = conductor != null ? conductor.SongTime : -1;
            float beat = conductor != null ? conductor.CountdownInterval : 0.5f;
            if (wakeOwnsCountdown)
            {
                if (countdownDim != null) countdownDim.enabled = false;
                countdownText.text = "";
                return;
            }
            if (coverUntilWake)
            {
                if (countdownDim != null)
                {
                    countdownDim.enabled = true;
                    countdownDim.color = Color.black;
                    countdownDim.transform.SetAsLastSibling();
                }
                countdownText.text = "";
                return;
            }
            if (t < 0)
            {
                int k = Mathf.CeilToInt((float)(-t / beat));
                if (k <= conductor.CountdownBeats) label = Mathf.Max(1, k).ToString();
            }
            else if (t < 0.6)
            {
                label = "GO!";
            }

            if (label != lastCount)
            {
                lastCount = label;
                countPop = 1f;
                countdownText.text = label;
                countdownText.color = label == "GO!" ? new Color(1f, 0.6f, 0.2f) : Color.white;
            }

            countPop = Mathf.MoveTowards(countPop, 0f, dt * 4f);
            float s = 1f + 0.6f * countPop * countPop;
            countdownText.rectTransform.localScale = new Vector3(s, s, 1f);
            var c = countdownText.color;
            c.a = label == "GO!" ? Mathf.Clamp01((float)(0.6 - t) / 0.25f) : 1f;
            countdownText.color = c;

            bool black = label == "1" || label == "2" || label == "3";
            if (countdownDim != null)
            {
                countdownDim.enabled = black;
                countdownDim.color = Color.black;
                if (black)
                {
                    countdownDim.transform.SetAsLastSibling();
                    countdownText.transform.SetAsLastSibling();
                }
            }
        }

        private void OnEnable()
        {
            ParallaxBackground.BackgroundAdvanced += OnBackgroundAdvanced;
        }

        private void OnDisable()
        {
            ParallaxBackground.BackgroundAdvanced -= OnBackgroundAdvanced;
        }

        private void Start()
        {
            var stage = StageSession.Current;
            string name = stage != null ? stage.displayName : "";
            var song = conductor.Music;
            if (song != null)
            {
                string title = BeatChart.StripBpmSuffix(song.name);
                name = string.IsNullOrEmpty(name) ? title : name + "  -  " + title;
            }
            titleText.text = name;
            playerController = FindAnyObjectByType<PlayerController>();
            lastArcadeInput = Time.realtimeSinceStartup;
            introCutscene = LoadCutscene("Aiden_Intro");
            clearCutscene = LoadCutscene("Aiden_Clear");
            gameOverCutscene = LoadCutscene("Aiden_GameOver");
            StartCoroutine(PlayOpeningCutscene());
        }

        private void BuildCutscene(RectTransform root)
        {
            cutscenePanel = UIFactory.Stretch(UIFactory.CreateRect("StoryCutscene", root));
            cutsceneGroup = cutscenePanel.gameObject.AddComponent<CanvasGroup>();
            cutsceneGroup.interactable = false;
            cutsceneGroup.blocksRaycasts = false;
            cutsceneDim = UIFactory.CreateImage("CutsceneDim", cutscenePanel, new Color(0.015f, 0.02f, 0.04f, 1f));
            UIFactory.Stretch(cutsceneDim.rectTransform);

            cutsceneImage = UIFactory.CreateImage("CutsceneImage", cutscenePanel, Color.white);
            UIFactory.Place(cutsceneImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 95f), new Vector2(1500f, 844f));
            cutsceneImage.preserveAspect = true;

            var caption = UIFactory.CreateImage("CaptionBg", cutscenePanel, new Color(0.015f, 0.02f, 0.04f, 0.94f));
            UIFactory.Place(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1680f, 230f));
            cutsceneTitle = UIFactory.CreateText("CutsceneTitle", cutscenePanel, "", 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place(cutsceneTitle.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 222f), new Vector2(1580f, 44f));
            cutsceneTitle.color = new Color(1f, 0.75f, 0.35f);
            cutsceneBody = UIFactory.CreateText("CutsceneBody", cutscenePanel, "", 30, TextAnchor.MiddleLeft, FontStyle.Normal);
            UIFactory.Place(cutsceneBody.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(1580f, 115f));
            cutsceneBody.lineSpacing = 1.2f;
            cutscenePrompt = UIFactory.CreateText("CutscenePrompt", cutscenePanel, "ENTER / SPACE / A  다음 줄    ESC  건너뛰기", 20, TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.Place(cutscenePrompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(1580f, 30f));
            cutscenePrompt.color = new Color(1f, 1f, 1f, 0.7f);
            cutscenePanel.gameObject.SetActive(false);
        }

        private void BuildStoryCaption(RectTransform root)
        {
            storyCaption = UIFactory.CreateRect("StoryCaption", root);
            UIFactory.Place(storyCaption, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(980f, 92f));
            storyCaptionGroup = storyCaption.gameObject.AddComponent<CanvasGroup>();
            storyCaptionGroup.interactable = false;
            storyCaptionGroup.blocksRaycasts = false;
            storyCaptionGroup.alpha = 0f;

            var bg = UIFactory.CreateImage("StoryCaptionBg", storyCaption, new Color(0.02f, 0.02f, 0.04f, 0.78f));
            UIFactory.Stretch(bg.rectTransform);
            bg.raycastTarget = false;

            storyCaptionImage = UIFactory.CreateImage("StoryCaptionImage", storyCaption, Color.white);
            UIFactory.Place(storyCaptionImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(72f, 72f));
            storyCaptionImage.preserveAspect = true;
            storyCaptionImage.raycastTarget = false;

            storyCaptionTitle = UIFactory.CreateText("StoryCaptionTitle", storyCaption, "", 22, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place(storyCaptionTitle.rectTransform, new Vector2(0f, 1f), new Vector2(96f, -8f), new Vector2(860f, 32f));
            storyCaptionTitle.color = new Color(1f, 0.78f, 0.4f);
            storyCaptionTitle.raycastTarget = false;

            storyCaptionBody = UIFactory.CreateText("StoryCaptionBody", storyCaption, "", 22, TextAnchor.MiddleLeft, FontStyle.Normal);
            UIFactory.Place(storyCaptionBody.rectTransform, new Vector2(0f, 0f), new Vector2(96f, 12f), new Vector2(860f, 36f));
            storyCaptionBody.raycastTarget = false;
            storyCaption.gameObject.SetActive(false);
        }

        private static Sprite LoadCutscene(string assetName)
        {
            // 임시 컷신 그림은 같은 이름의 Resources PNG를 바꾸면 교체할 수 있다.
            var texture = Resources.Load<Texture2D>("Cutscenes/" + assetName);
            return texture != null
                ? Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f))
                : null;
        }

        private IEnumerator PlayOpeningCutscene()
        {
            float waitedForSong = 0f;
            while (conductor != null && conductor.enabled && !conductor.IsReady && !resultShown && waitedForSong < 2f)
            {
                waitedForSong += Time.unscaledDeltaTime;
                yield return null;
            }
            if (conductor != null && conductor.enabled && !conductor.IsReady)
            {
                conductor.enabled = false;
                GamePause.SetCountdown(false);
            }
            if (resultShown) yield break;

            if (conductor != null && conductor.IsReady) conductor.Pause();
            GamePause.SetPaused(true);
            if (RunCheckpoint.ResumedThisLoad)
            {
                coverUntilWake = false;
                wakeOwnsCountdown = false;
                if (countdownDim != null) countdownDim.enabled = false;
                GamePause.SetPaused(false);
                if (conductor != null && conductor.IsReady && RunCheckpoint.HasSong)
                    conductor.SeekTo(RunCheckpoint.SongTime, RunCheckpoint.DeathHeardBoss, RunCheckpoint.BossTime);
                else if (conductor != null && conductor.IsReady)
                    conductor.StartCountdownNow(conductor.CountdownBeats);
                cutsceneActive = false;
                ShowHint("세이브 지점에서 이어집니다. 점수는 0이고, 곡은 죽은 순간부터입니다.", 4f);
                yield break;
            }
            // 잠드는 인트로 (SleepIntro.cs). 예전 정지 컷신으로 돌리려면 아래를 PlayCutscene(...) 으로 바꾸면 된다.
            cutsceneActive = true;
            var background = FindAnyObjectByType<ParallaxBackground>();
            yield return SleepIntro.Play(
                introCutscene,
                StageStory.Speaker,
                StageStory.DrowsyLines,
                conductor != null ? conductor.CountdownInterval : 0.5f,
                BeginWakeCountdown,
                StageStory.Speaker,
                StageStory.DreamLines,
                background != null ? background.SpriteAt(0) : null);
            wakeOwnsCountdown = false;
            coverUntilWake = false;
            cutsceneActive = false;
            routeHintShown = true;
            GamePause.SetPaused(false);
        }

        private void OnBackgroundAdvanced(int index, Sprite image)
        {
            if (resultShown || toldStories.Contains(index)) return;
            var beat = StageStory.ForBackground(index);
            if (beat == null) return;
            toldStories.Add(index);
            StartCoroutine(PlayBackgroundCaption(beat, image));
        }

        private IEnumerator PlayBackgroundCaption(StageStory.Beat beat, Sprite image)
        {
            if (resultShown || storyCaption == null) yield break;

            int token = ++storyCaptionToken;
            storyCaptionTitle.text = beat.Title;
            storyCaptionBody.text = beat.Body;
            storyCaptionImage.sprite = image;
            storyCaptionImage.enabled = image != null;
            storyCaption.gameObject.SetActive(true);
            storyCaption.SetAsLastSibling();

            const float fadeIn = 0.25f;
            for (float t = 0f; t < fadeIn; t += Time.unscaledDeltaTime)
            {
                if (token != storyCaptionToken) yield break;
                storyCaptionGroup.alpha = Mathf.Clamp01(t / fadeIn);
                yield return null;
            }
            if (token != storyCaptionToken) yield break;
            storyCaptionGroup.alpha = 1f;

            const float hold = 2.6f;
            for (float t = 0f; t < hold; t += Time.unscaledDeltaTime)
            {
                if (token != storyCaptionToken) yield break;
                yield return null;
            }

            const float fadeOut = 0.45f;
            for (float t = 0f; t < fadeOut; t += Time.unscaledDeltaTime)
            {
                if (token != storyCaptionToken) yield break;
                storyCaptionGroup.alpha = 1f - Mathf.Clamp01(t / fadeOut);
                yield return null;
            }
            if (token != storyCaptionToken) yield break;
            storyCaptionGroup.alpha = 0f;
            storyCaption.gameObject.SetActive(false);
        }

        private IEnumerator RevealLine(string line)
        {
            line = line ?? "";
            cutsceneBody.text = "";
            float step = 0.028f;
            int shown = 0;
            float t = 0f;
            while (shown < line.Length)
            {
                if (CutsceneAdvancePressed() || CutsceneSkipPressed())
                {
                    cutsceneBody.text = line;
                    yield break;
                }
                t += Time.unscaledDeltaTime;
                int next = Mathf.Min(line.Length, Mathf.FloorToInt(t / step));
                if (next != shown)
                {
                    shown = next;
                    cutsceneBody.text = line.Substring(0, shown);
                }
                yield return null;
            }
            cutsceneBody.text = line;
        }

        private void BeginWakeCountdown()
        {
            coverUntilWake = false;
            wakeOwnsCountdown = true;
            if (countdownDim != null) countdownDim.enabled = false;
            // 이야기에서 이미 나오던 곡은 눈을 뜰 때도 이어서 튼다.
            if (conductor != null && conductor.IsReady && conductor.SongTime >= 0)
            {
                GamePause.SetCountdown(false);
                return;
            }
            GamePause.SetPaused(false);
            if (conductor != null && conductor.IsReady)
                conductor.StartCountdownNow(conductor.CountdownBeats);
            else
                GamePause.SetCountdown(false);
        }

        private IEnumerator PlayCutscene(string title, string body, Sprite image)
        {
            cutsceneTitle.text = title;
            cutsceneImage.sprite = image;
            cutsceneImage.color = image != null ? Color.white : new Color(0.12f, 0.13f, 0.18f, 1f);
            cutsceneActive = true;
            cutscenePanel.gameObject.SetActive(true);
            cutscenePanel.SetAsLastSibling();
            string[] lines = body.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                cutsceneBody.text = lines[i];
                cutscenePrompt.text = i < lines.Length - 1
                    ? "ENTER / SPACE / A  다음 줄    ESC  건너뛰기"
                    : "ENTER / SPACE / A  계속";
                yield return null;
                while (!CutsceneAdvancePressed() && !CutsceneSkipPressed()) yield return null;
                if (CutsceneSkipPressed()) break;
            }
            cutscenePanel.gameObject.SetActive(false);
            cutsceneActive = false;
        }

        private static bool CutsceneAdvancePressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame
                || keyboard.zKey.wasPressedThisFrame)) return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            var pads = Gamepad.all;
            for (int i = 0; i < pads.Count; i++)
                if (pads[i] != null && (pads[i].buttonEast.wasPressedThisFrame || pads[i].buttonSouth.wasPressedThisFrame))
                    return true;
            return false;
        }

        private static bool CutsceneSkipPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return true;
            var pads = Gamepad.all;
            for (int i = 0; i < pads.Count; i++)
                if (pads[i] != null && pads[i].startButton.wasPressedThisFrame)
                    return true;
            return false;
        }

        public static void ShowHint(string message, float seconds = 4f)
        {
            var hud = FindAnyObjectByType<StageHUD>();
            if (hud == null || hud.hintText == null) return;
            hud.hintText.text = message;
            hud.hintTimer = seconds;
            hud.hintText.color = new Color(1f, 0.88f, 0.65f, 1f);
            hud.hintBackdrop.color = new Color(0f, 0f, 0f, 0.58f);
        }

        private void BuildGameOver(RectTransform root)
        {
            gameOverPanel = UIFactory.Stretch(UIFactory.CreateRect("GameOver", root));
            gameOverGroup = gameOverPanel.gameObject.AddComponent<CanvasGroup>();
            var dim = UIFactory.CreateImage("Dim", gameOverPanel, new Color(0.04f, 0.035f, 0.07f, 1f));
            UIFactory.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            resultTitle = UIFactory.CreateText("Head", gameOverPanel, "GAME OVER", 130, style: FontStyle.BoldAndItalic);
            UIFactory.Place(resultTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1200f, 140f));
            resultTitle.color = new Color(1f, 0.35f, 0.3f);

            resultText = UIFactory.CreateText("Result", gameOverPanel, "", 40, style: FontStyle.Bold);
            UIFactory.Place(resultText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 75f), new Vector2(1000f, 100f));
            resultText.lineSpacing = 1.2f;

            resultButtons = UIFactory.Place(UIFactory.CreateRect("Buttons", gameOverPanel), new Vector2(0.5f, 0.5f), new Vector2(0f, -340f), new Vector2(480f, 210f));
            var list = resultButtons;
            UIFactory.AddVerticalLayout(list, 12f);
            continueButton = UIFactory.CreateButton("Continue", list, "세이브에서 시작", 28, HandleContinue);
            UIFactory.SetHeight(continueButton, 58f);
            continueButton.gameObject.SetActive(false);
            yesButton = UIFactory.CreateButton("Yes", list, "예", 30, AcceptContinue);
            UIFactory.SetHeight(yesButton, 58f);
            yesButton.gameObject.SetActive(false);
            noButton = UIFactory.CreateButton("No", list, "아니요", 30, DeclineContinue);
            UIFactory.SetHeight(noButton, 58f);
            noButton.gameObject.SetActive(false);
            retryButton = UIFactory.CreateButton("Retry", list, "RETRY", 30, HandleRetry);
            UIFactory.SetHeight(retryButton, 58f);
            menuButton = UIFactory.CreateButton("Menu", list, "MAIN MENU", 30, HandleMenu);
            UIFactory.SetHeight(menuButton, 58f);

            initialsPanel = UIFactory.CreateRect("InitialsEntry", gameOverPanel);
            UIFactory.Place(initialsPanel, new Vector2(0.5f, 0.5f), new Vector2(0f, -105f), new Vector2(800f, 160f));
            var prompt = UIFactory.CreateText("Prompt", initialsPanel, "ENTER YOUR INITIALS", 32, style: FontStyle.Bold);
            UIFactory.Place(prompt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(760f, 40f));
            initialsText = UIFactory.CreateText("Initials", initialsPanel, "AAA", 66, style: FontStyle.BoldAndItalic);
            initialsText.supportRichText = true;
            UIFactory.Place(initialsText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(500f, 70f));
            var controls = UIFactory.CreateText("Controls", initialsPanel, "LEFT / RIGHT: SELECT     UP / DOWN: CHANGE     ENTER: SAVE", 20, style: FontStyle.Normal);
            UIFactory.Place(controls.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(780f, 30f));
            saveInitialsButton = UIFactory.CreateButton("SaveInitials", gameOverPanel, "SAVE SCORE", 28, CommitInitials);
            UIFactory.Place((RectTransform)saveInitialsButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -225f), new Vector2(300f, 54f));

            gameOverPanel.gameObject.SetActive(false);
            initialsPanel.gameObject.SetActive(false);
        }

        public static bool HasResult
        {
            get
            {
                var hud = FindAnyObjectByType<StageHUD>();
                return hud != null && hud.resultShown;
            }
        }

        public static void AnnounceRoomClear(bool moreRooms)
        {
            if (HasResult) return;
            if (moreRooms) ShowAdvance();
            else ShowGameClear();
        }

        public static void ShowAdvance()
        {
            var hud = FindAnyObjectByType<StageHUD>();
            if (hud == null || hud.resultShown) return;
            hud.showMarch = true;
            var camera = FindAnyObjectByType<Gstarr.CameraSystem.CameraFollow2D>();
            if (camera != null) camera.PushForward(1.4f);
        }

        public static void HideAdvance()
        {
            var hud = FindAnyObjectByType<StageHUD>();
            if (hud != null) hud.showMarch = false;
        }

        public static void ShowGameClear()
        {
            var hud = FindAnyObjectByType<StageHUD>();
            if (hud != null) hud.ShowResult(true);
        }

        public static void ShowGameOver()
        {
            var hud = FindAnyObjectByType<StageHUD>();
            if (hud != null) hud.ShowResult(false);
        }

        private void ShowResult(bool cleared)
        {
            if (resultShown) return;
            resultShown = true;
            Combat.GloryKillDirector.Dismiss();
            Combat.FoeEffects.Dismiss();
            World.SignDetail.Dismiss();
            resultCleared = cleared;
            lastArcadeInput = Time.realtimeSinceStartup;
            var combo = ComboSystem.Instance;
            long score = combo != null ? combo.Score : 0;
            int maxCombo = combo != null ? combo.MaxCombo : 0;

            var stage = StageSession.Current;
            long best = stage != null ? stage.BestScore : 0;
            bool newBest = stage != null && score > best;
            if (stage != null) stage.SubmitScore(score);
            pendingScore = score;
            pendingStageName = stage != null ? stage.displayName : "STAGE";
            waitingForInitials = false;
            resultSubmitted = false;
            selectedInitial = 0;
            for (int i = 0; i < initialIndices.Length; i++) initialIndices[i] = 0;
            RefreshInitials();
            initialsPanel.gameObject.SetActive(false);
            if (resultButtons != null) resultButtons.gameObject.SetActive(false);
            gameOverPanel.gameObject.SetActive(false);
            HideGameplayHud();

            resultTitle.text = cleared ? "아무도 몰랐어!" : "수업 중 들켜버렸다!";
            resultTitle.color = cleared ? new Color(1f, 0.75f, 0.25f) : new Color(1f, 0.35f, 0.3f);
            recordBody =
                "SCORE   " + score.ToString("N0") + (newBest ? "   NEW BEST!" : "") + "\n" +
                "MAX COMBO   " + maxCombo + "\n" +
                (stage != null ? "BEST   " + System.Math.Max(best, score).ToString("N0") : "");
            resultText.text = recordBody;

            UIFactory.EnsureEventSystem();
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
            SuppressGameplayInput();
            if (!cleared && RunCheckpoint.HasSave && conductor != null)
            {
                conductor.CapturePlayback(out double songTime, out bool heardBoss, out float bossTime);
                RunCheckpoint.RememberDeathSong(songTime, heardBoss, bossTime);
            }
            if (conductor != null && conductor.IsReady && !conductor.IsFinished) conductor.Pause();
            GamePause.SetPaused(true);
            var playerBody = playerController != null ? playerController.GetComponent<Rigidbody2D>() : null;
            if (playerBody != null) playerBody.linearVelocity = Vector2.zero;
            showMarch = false;
            StartCoroutine(PlayEndingCutscene(cleared));
        }

        /// <summary>쓰러지고 화면이 어두워진 뒤에 들킨 이야기로 넘긴다.</summary>
        public static void BeginPlayerDeath()
        {
            var hud = FindAnyObjectByType<StageHUD>();
            if (hud == null || hud.resultShown || hud.deathSequence) return;
            hud.deathSequence = true;
            hud.StartCoroutine(hud.CollapseThenStory());
        }

        private bool deathSequence;

        private IEnumerator CollapseThenStory()
        {
            var player = playerController != null ? playerController : FindAnyObjectByType<PlayerController>();
            var motion = player != null ? player.GetComponent<SpriteMotion>() : null;
            float guard = 0f;
            while ((motion == null || !motion.DeathPresented) && guard < 3.2f)
            {
                guard += Time.unscaledDeltaTime;
                yield return null;
            }

            var pain = FindAnyObjectByType<Gstarr.CameraSystem.PainOverlay>();
            if (pain != null) yield return pain.FadeToBlack(0.5f);
            else yield return new WaitForSecondsRealtime(0.35f);
            ShowResult(false);
        }

        private void HideGameplayHud()
        {
            if (fill != null) fill.transform.parent.gameObject.SetActive(false);
            if (titleText != null) titleText.gameObject.SetActive(false);
            if (timeText != null) timeText.gameObject.SetActive(false);
            if (countdownText != null) countdownText.gameObject.SetActive(false);
            if (hintText != null) hintText.gameObject.SetActive(false);
            if (hintBackdrop != null) hintBackdrop.gameObject.SetActive(false);
            showMarch = false;
            if (marchText != null) marchText.text = "";
        }

        private IEnumerator PlayEndingCutscene(bool cleared)
        {
            cutsceneActive = true;
            if (cleared)
            {
                yield return SleepIntro.PlayClear(
                    clearCutscene,
                    new[]
                    {
                        "딩동댕동, 수업 종이 울리고 친구들이 하나둘 교실을 나섰다.",
                        "선생님도 에이든이 잠든 줄 모른 채 악보를 정리했다.",
                        "에이든은 끝까지 들키지 않고, 포근한 꿈속에서 새근새근 잠들어 있었다."
                    },
                    ShowResultPanel);
            }
            else
            {
                yield return SleepIntro.PlayCaught(
                    gameOverCutscene,
                    new[]
                    {
                        "선생님의 “에이든, 일어나렴!” 하는 목소리에 에이든은 화들짝 고개를 들었다.",
                        "잠든 모습을 본 친구들의 시선이 한꺼번에 에이든에게 쏠렸다.",
                        "얼굴이 새빨개진 에이든은 아무 말도 못 한 채 고개를 푹 숙였다."
                    },
                    RunCheckpoint.HasSave ? ShowContinuePrompt : ShowResultPanel);
            }
            cutsceneActive = false;
        }

        private void ShowContinuePrompt()
        {
            FindAnyObjectByType<Gstarr.CameraSystem.PainOverlay>()?.ClearFade();
            askingContinue = true;
            continueDeclined = false;
            gameOverPanel.SetAsLastSibling();
            gameOverPanel.gameObject.SetActive(true);
            gameOverGroup.alpha = 1f;
            gameOverFade = 1f;
            resultTitle.text = "이어하시겠습니까?";
            resultTitle.color = new Color(1f, 0.82f, 0.35f);
            resultText.text = "세이브 지점에서 이어 합니다.\n점수는 0이 됩니다.";
            initialsPanel.gameObject.SetActive(false);
            saveInitialsButton.gameObject.SetActive(false);
            if (continueButton != null) continueButton.gameObject.SetActive(false);
            if (retryButton != null) retryButton.gameObject.SetActive(false);
            if (menuButton != null) menuButton.gameObject.SetActive(false);
            if (yesButton != null) yesButton.gameObject.SetActive(true);
            if (noButton != null) noButton.gameObject.SetActive(true);
            if (resultButtons != null) resultButtons.gameObject.SetActive(true);
            waitingForInitials = false;
            UIFactory.EnsureEventSystem();
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
            UIFactory.Select(yesButton);
        }

        private void AcceptContinue()
        {
            if (!askingContinue) return;
            askingContinue = false;
            StageSession.ContinueFromCheckpoint();
        }

        private void DeclineContinue()
        {
            if (!askingContinue) return;
            askingContinue = false;
            continueDeclined = true;
            resultTitle.text = "수업 중 들켜버렸다!";
            resultTitle.color = new Color(1f, 0.35f, 0.3f);
            resultText.text = recordBody ?? "";
            if (yesButton != null) yesButton.gameObject.SetActive(false);
            if (noButton != null) noButton.gameObject.SetActive(false);
            ShowResultPanel();
        }

        private void ShowResultPanel()
        {
            FindAnyObjectByType<Gstarr.CameraSystem.PainOverlay>()?.ClearFade();
            gameOverPanel.SetAsLastSibling();
            gameOverPanel.gameObject.SetActive(true);
            gameOverGroup.alpha = 1f;
            gameOverFade = 1f;
            initialsPanel.gameObject.SetActive(true);
            if (resultButtons != null) resultButtons.gameObject.SetActive(false);
            saveInitialsButton.gameObject.SetActive(true);
            waitingForInitials = true;
            UIFactory.EnsureEventSystem();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.sendNavigationEvents = false;
            }
        }

        private void Update()
        {
            if (AnyButtonPressed()) lastArcadeInput = Time.realtimeSinceStartup;
            if (playerController != null && playerController.HasMoveInput) lastArcadeInput = Time.realtimeSinceStartup;

            if (GameSettings.ArcadeMode && !cutsceneActive && !returningToMenu && Time.realtimeSinceStartup - lastArcadeInput > (resultShown ? 10f : 25f))
            {
                returningToMenu = true;
                if (resultShown && waitingForInitials) CommitInitials();
                StageSession.ToMainMenu();
                return;
            }

            if (!routeHintShown && conductor.IsReady && conductor.SongTime >= 0.7)
            {
                routeHintShown = true;
                ShowHint("방을 비우면 GO GO. 오른쪽으로 가세요. 공격은 박자에 맞추세요.", 4.5f);
            }

            if (showMarch && marchText != null)
            {
                int arrows = 1 + (int)(Time.unscaledTime * 6f) % 3;
                marchText.text = "GO GO  " + new string('>', arrows);
                float pulse = 0.92f + 0.08f * Mathf.Sin(Time.unscaledTime * 14f);
                marchText.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
                marchText.color = new Color(1f, 0.55f, 0.12f, 1f);
            }
            else if (marchText != null && marchText.text.Length > 0)
            {
                marchText.text = "";
            }

            if (!comboHintShown && ComboSystem.Instance != null && ComboSystem.Instance.MaxCombo > 0)
            {
                comboHintShown = true;
                ShowHint("연속으로 적을 맞혀 콤보와 스타일 랭크를 올리세요.", 4f);
            }
            if (hintTimer > 0f)
            {
                hintTimer -= Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(hintTimer);
                hintText.color = new Color(1f, 0.88f, 0.65f, alpha);
                hintBackdrop.color = new Color(0f, 0f, 0f, alpha * 0.58f);
            }

            if (waitingForInitials && !cutsceneActive) UpdateInitialsInput();
            else if (resultShown && !cutsceneActive && (askingContinue || gameOverFade >= 1f))
                UpdateResultChoices();

            if (conductor.IsReady)
            {
                UpdateCountdown(Time.unscaledDeltaTime);
                fill.fillAmount = conductor.Progress;
                double shown = conductor.SongTime;
                if (shown < 0) shown = 0;
                else if (conductor.Loops && conductor.Length > 0) shown %= conductor.Length;
                timeText.text = Format((float)shown) + " / " + Format((float)conductor.Length);
            }

            if (gameOverFade >= 0f && gameOverFade < 1f)
            {
                gameOverFade = Mathf.MoveTowards(gameOverFade, 1f, Time.unscaledDeltaTime * 2.5f);
                gameOverGroup.alpha = gameOverFade;
            }

            bool cancel = UIFactory.Cancel.WasPressedThisFrame() || PadSouthPressed();
            if (askingContinue && cancel)
                DeclineContinue();
            else if (!cutsceneActive && !waitingForInitials && !askingContinue && gameOverFade >= 1f && cancel)
                HandleMenu();
        }

        private Vector2 menuStick;
        private readonly List<Button> resultChoices = new List<Button>();

        private void SuppressGameplayInput()
        {
            GameSettings.Actions?.FindActionMap("Player")?.Disable();
            var bodies = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            for (int i = 0; i < bodies.Length; i++)
                if (bodies[i] != null) bodies[i].SuppressInput();
            var combats = FindObjectsByType<Gstarr.Combat.PlayerCombat>(FindObjectsSortMode.None);
            for (int i = 0; i < combats.Length; i++)
                if (combats[i] != null) combats[i].SuppressInput();
            var bags = FindObjectsByType<Gstarr.Items.PlayerInventory>(FindObjectsSortMode.None);
            for (int i = 0; i < bags.Length; i++)
                if (bags[i] != null) bags[i].SuppressInput();
        }

        private void UpdateInitialsInput()
        {
            var keyboard = Keyboard.current;
            bool left = keyboard != null && keyboard.leftArrowKey.wasPressedThisFrame;
            bool right = keyboard != null && keyboard.rightArrowKey.wasPressedThisFrame;
            bool up = keyboard != null && keyboard.upArrowKey.wasPressedThisFrame;
            bool down = keyboard != null && keyboard.downArrowKey.wasPressedThisFrame;
            bool confirm = keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.zKey.wasPressedThisFrame);
            ReadMenuPad(ref left, ref right, ref up, ref down, ref confirm);

            if (left) selectedInitial = (selectedInitial + initialIndices.Length - 1) % initialIndices.Length;
            if (right) selectedInitial = (selectedInitial + 1) % initialIndices.Length;
            if (up) initialIndices[selectedInitial] = (initialIndices[selectedInitial] + 1) % InitialCharacters.Length;
            if (down) initialIndices[selectedInitial] = (initialIndices[selectedInitial] + InitialCharacters.Length - 1) % InitialCharacters.Length;
            if (left || right || up || down) RefreshInitials();
            if (confirm) CommitInitials();
        }

        private void UpdateResultChoices()
        {
            resultChoices.Clear();
            ConsiderChoice(continueButton);
            ConsiderChoice(yesButton);
            ConsiderChoice(noButton);
            ConsiderChoice(retryButton);
            ConsiderChoice(menuButton);
            if (resultChoices.Count == 0) return;

            int index = 0;
            var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            for (int i = 0; i < resultChoices.Count; i++)
                if (resultChoices[i].gameObject == current) { index = i; break; }

            bool left = false, right = false, up = false, down = false, confirm = false;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                up = keyboard.upArrowKey.wasPressedThisFrame;
                down = keyboard.downArrowKey.wasPressedThisFrame;
                confirm = keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.zKey.wasPressedThisFrame;
            }
            ReadMenuPad(ref left, ref right, ref up, ref down, ref confirm);
            if (up) index = Mathf.Max(0, index - 1);
            if (down) index = Mathf.Min(resultChoices.Count - 1, index + 1);
            if (EventSystem.current != null && (up || down || current != resultChoices[index].gameObject))
                UIFactory.Select(resultChoices[index]);
            if (confirm && !up && !down)
                resultChoices[index].onClick.Invoke();
        }

        private void ConsiderChoice(Button button)
        {
            if (button != null && button.gameObject.activeInHierarchy && button.interactable)
                resultChoices.Add(button);
        }

        private void ReadMenuPad(ref bool left, ref bool right, ref bool up, ref bool down, ref bool confirm)
        {
            var pads = Gamepad.all;
            Vector2 stick = Vector2.zero;
            for (int i = 0; i < pads.Count; i++)
            {
                var pad = pads[i];
                if (pad == null) continue;
                left |= pad.dpad.left.wasPressedThisFrame;
                right |= pad.dpad.right.wasPressedThisFrame;
                up |= pad.dpad.up.wasPressedThisFrame;
                down |= pad.dpad.down.wasPressedThisFrame;
                confirm |= pad.buttonEast.wasPressedThisFrame;
                var value = pad.leftStick.ReadValue();
                if (value.sqrMagnitude > stick.sqrMagnitude) stick = value;
            }

            const float press = 0.65f;
            const float release = 0.35f;
            if (menuStick.x > -release && stick.x <= -press) left = true;
            if (menuStick.x < release && stick.x >= press) right = true;
            if (menuStick.y < release && stick.y >= press) up = true;
            if (menuStick.y > -release && stick.y <= -press) down = true;
            menuStick = stick;
        }

        private static bool PadSouthPressed()
        {
            var pads = Gamepad.all;
            for (int i = 0; i < pads.Count; i++)
                if (pads[i] != null && pads[i].buttonSouth.wasPressedThisFrame)
                    return true;
            return false;
        }

        private void RefreshInitials()
        {
            var value = new System.Text.StringBuilder();
            for (int i = 0; i < initialIndices.Length; i++)
            {
                string character = InitialCharacters[initialIndices[i]].ToString();
                value.Append(i == selectedInitial ? "<color=#FF9A40>" + character + "</color>" : character);
            }
            initialsText.text = value.ToString();
        }

        private void CommitInitials()
        {
            if (!waitingForInitials || resultSubmitted) return;
            var name = new System.Text.StringBuilder(initialIndices.Length);
            for (int i = 0; i < initialIndices.Length; i++) name.Append(InitialCharacters[initialIndices[i]]);
            RunLeaderboard.SetNickname(name.ToString());
            RunLeaderboard.Submit(pendingScore, pendingStageName);
            resultSubmitted = true;
            waitingForInitials = false;
            initialsPanel.gameObject.SetActive(false);
            saveInitialsButton.gameObject.SetActive(false);
            if (resultButtons != null) resultButtons.gameObject.SetActive(true);
            ApplyDeathChoices();
            resultText.text += "\nNICKNAME   " + name;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
        }

        private void ApplyDeathChoices()
        {
            bool save = !resultCleared && !continueDeclined && RunCheckpoint.HasSave;
            if (continueButton != null) continueButton.gameObject.SetActive(save);
            var label = retryButton != null ? retryButton.GetComponentInChildren<Text>() : null;
            if (label != null) label.text = save ? "처음부터" : "RETRY";
            UIFactory.Select(save ? continueButton : retryButton);
        }

        private void HandleContinue()
        {
            CommitInitials();
            StageSession.ContinueFromCheckpoint();
        }

        private void HandleRetry()
        {
            CommitInitials();
            StageSession.Retry();
        }

        private void HandleMenu()
        {
            CommitInitials();
            StageSession.ToMainMenu();
        }

        private static string Format(float seconds)
        {
            int s = Mathf.FloorToInt(Mathf.Min(seconds, 5999f));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        private static bool AnyButtonPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return true;
            var pad = Gamepad.current;
            return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame
                || pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame
                || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame
                || pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame
                || pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame
                || pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame);
        }
    }
}
