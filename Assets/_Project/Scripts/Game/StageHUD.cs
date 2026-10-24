using System.Collections;
using Gstarr.Rhythm;
using Gstarr.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gstarr.Game
{
    /// <summary>
    /// 화면 위 음악 진행도 바, 그리고 음악이 끝나면 게임 오버 화면 (점수 / 최대 콤보 / 재시작 / 메인 메뉴).
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
        private Button saveInitialsButton;
        private Button menuButton;
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
        private Image cutsceneImage;
        private Text cutsceneTitle;
        private Text cutsceneBody;
        private Text cutscenePrompt;
        private Sprite introCutscene;
        private Sprite clearCutscene;
        private Sprite gameOverCutscene;
        private bool cutsceneActive;

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

            countdownText = UIFactory.CreateText("Countdown", root, "", 220, style: FontStyle.BoldAndItalic);
            UIFactory.Place(countdownText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(900f, 300f));
            countdownText.GetComponent<Outline>().effectDistance = new Vector2(5f, -5f);

            hintBackdrop = UIFactory.CreateImage("GameplayHintBg", root, new Color(0f, 0f, 0f, 0f));
            UIFactory.Place(hintBackdrop.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(1240f, 110f));
            hintText = UIFactory.CreateText("GameplayHint", root, "", 25, style: FontStyle.Bold);
            UIFactory.Place(hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(1180f, 96f));
            hintText.color = new Color(1f, 0.88f, 0.65f, 0f);

            BuildGameOver(root);
            BuildCutscene(root);
        }

        private Text countdownText;
        private string lastCount;
        private float countPop;

        // 음악 시작 전 박자에 맞춰 3, 2, 1, 음악 시작 순간 GO!
        private void UpdateCountdown(float dt)
        {
            string label = "";
            double t = conductor.SongTime;
            float beat = conductor.CountdownInterval;
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
        }

        private void OnEnable() => conductor.Finished += OnFinished;
        private void OnDisable() => conductor.Finished -= OnFinished;

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
            var dim = UIFactory.CreateImage("CutsceneDim", cutscenePanel, new Color(0.015f, 0.02f, 0.04f, 1f));
            UIFactory.Stretch(dim.rectTransform);

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
            while (conductor != null && conductor.enabled && !conductor.IsReady && !resultShown)
                yield return null;
            if (resultShown) yield break;

            if (conductor != null && conductor.IsReady) conductor.Pause();
            GamePause.SetPaused(true);
            yield return PlayCutscene(
                "에이든의 음악 시간",
                "오후 3시 42분, 에이든은 졸음을 참으려 눈을 비볐다.\n하지만 선생님의 피아노 소리가 자장가처럼 들려왔다.\n결국 책상에 엎드린 순간, 교실은 괴물들이 사는 꿈속으로 변했다.",
                introCutscene);
            GamePause.SetPaused(false);
            if (conductor != null) conductor.Resume();
            ShowHint("공중에서 점프를 한 번 더 누르면 더블 점프! 공격과 아이템은 박자에 맞춰 쓰세요.", 4f);
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
            var pad = Gamepad.current;
            return pad != null && (pad.buttonEast.wasPressedThisFrame || pad.buttonSouth.wasPressedThisFrame);
        }

        private static bool CutsceneSkipPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
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

            resultButtons = UIFactory.Place(UIFactory.CreateRect("Buttons", gameOverPanel), new Vector2(0.5f, 0.5f), new Vector2(0f, -365f), new Vector2(420f, 132f));
            var list = resultButtons;
            UIFactory.AddVerticalLayout(list, 12f);
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

        private void OnFinished() => ShowResult(false);

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
            resultText.text =
                "SCORE   " + score.ToString("N0") + (newBest ? "   NEW BEST!" : "") + "\n" +
                "MAX COMBO   " + maxCombo + "\n" +
                (stage != null ? "BEST   " + System.Math.Max(best, score).ToString("N0") : "");

            UIFactory.EnsureEventSystem();
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
            if (conductor != null && conductor.IsReady && !conductor.IsFinished) conductor.Pause();
            GamePause.SetPaused(true);
            var playerBody = playerController != null ? playerController.GetComponent<Rigidbody2D>() : null;
            if (playerBody != null) playerBody.linearVelocity = Vector2.zero;
            StartCoroutine(PlayEndingCutscene(cleared));
        }

        private void HideGameplayHud()
        {
            if (fill != null) fill.transform.parent.gameObject.SetActive(false);
            if (titleText != null) titleText.gameObject.SetActive(false);
            if (timeText != null) timeText.gameObject.SetActive(false);
            if (countdownText != null) countdownText.gameObject.SetActive(false);
            if (hintText != null) hintText.gameObject.SetActive(false);
            if (hintBackdrop != null) hintBackdrop.gameObject.SetActive(false);
        }

        private IEnumerator PlayEndingCutscene(bool cleared)
        {
            yield return PlayCutscene(
                cleared ? "아무도 몰랐어!" : "수업 중 들켜버렸다!",
                cleared
                    ? "딩동댕동, 수업 종이 울리고 친구들이 하나둘 교실을 나섰다.\n선생님도 에이든이 잠든 줄 모른 채 악보를 정리했다.\n에이든은 끝까지 들키지 않고, 포근한 꿈속에서 새근새근 잠들어 있었다."
                    : "선생님의 “에이든, 일어나렴!” 하는 목소리에 에이든은 화들짝 고개를 들었다.\n잠든 모습을 본 친구들의 시선이 한꺼번에 에이든에게 쏠렸다.\n얼굴이 새빨개진 에이든은 아무 말도 못 한 채 고개를 푹 숙였다.",
                cleared ? clearCutscene : gameOverCutscene);
            yield return null;

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
                if (resultShown) CommitInitials();
                StageSession.ToMainMenu();
                return;
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

            if (conductor.IsReady)
            {
                UpdateCountdown(Time.unscaledDeltaTime);
                fill.fillAmount = conductor.Progress;
                timeText.text = Format(Mathf.Max(0f, (float)conductor.SongTime)) + " / " + Format((float)conductor.Length);
            }

            if (gameOverFade >= 0f && gameOverFade < 1f)
            {
                gameOverFade = Mathf.MoveTowards(gameOverFade, 1f, Time.unscaledDeltaTime * 2.5f);
                gameOverGroup.alpha = gameOverFade;
            }

            if (!cutsceneActive && !waitingForInitials && gameOverFade >= 1f && UIFactory.Cancel.WasPressedThisFrame())
                HandleMenu();
        }

        private void UpdateInitialsInput()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            bool left = keyboard != null && keyboard.leftArrowKey.wasPressedThisFrame;
            bool right = keyboard != null && keyboard.rightArrowKey.wasPressedThisFrame;
            bool up = keyboard != null && keyboard.upArrowKey.wasPressedThisFrame;
            bool down = keyboard != null && keyboard.downArrowKey.wasPressedThisFrame;
            bool confirm = keyboard != null && keyboard.enterKey.wasPressedThisFrame;
            if (gamepad != null)
            {
                left |= gamepad.dpad.left.wasPressedThisFrame;
                right |= gamepad.dpad.right.wasPressedThisFrame;
                up |= gamepad.dpad.up.wasPressedThisFrame;
                down |= gamepad.dpad.down.wasPressedThisFrame;
                confirm |= gamepad.buttonEast.wasPressedThisFrame;
            }

            if (left) selectedInitial = (selectedInitial + initialIndices.Length - 1) % initialIndices.Length;
            if (right) selectedInitial = (selectedInitial + 1) % initialIndices.Length;
            if (up) initialIndices[selectedInitial] = (initialIndices[selectedInitial] + 1) % InitialCharacters.Length;
            if (down) initialIndices[selectedInitial] = (initialIndices[selectedInitial] + InitialCharacters.Length - 1) % InitialCharacters.Length;
            if (left || right || up || down) RefreshInitials();
            if (confirm) CommitInitials();
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
            resultText.text += "\nNICKNAME   " + name;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
            UIFactory.Select(retryButton);
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
