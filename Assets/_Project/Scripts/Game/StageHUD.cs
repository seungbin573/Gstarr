using Gstarr.Rhythm;
using UnityEngine;
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
        private Button retryButton;
        private float gameOverFade = -1f;

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

            BuildGameOver(root);
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
            if (song != null) name = string.IsNullOrEmpty(name) ? song.name : name + "  -  " + song.name;
            titleText.text = name;
        }

        private void BuildGameOver(RectTransform root)
        {
            gameOverPanel = UIFactory.Stretch(UIFactory.CreateRect("GameOver", root));
            gameOverGroup = gameOverPanel.gameObject.AddComponent<CanvasGroup>();
            var dim = UIFactory.CreateImage("Dim", gameOverPanel, new Color(0f, 0f, 0f, 0.78f));
            UIFactory.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            var head = UIFactory.CreateText("Head", gameOverPanel, "GAME OVER", 130, style: FontStyle.BoldAndItalic);
            UIFactory.Place(head.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1200f, 170f));
            head.color = new Color(1f, 0.35f, 0.3f);

            resultText = UIFactory.CreateText("Result", gameOverPanel, "", 40, style: FontStyle.Bold);
            UIFactory.Place(resultText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1000f, 200f));
            resultText.lineSpacing = 1.2f;

            var list = UIFactory.Place(UIFactory.CreateRect("Buttons", gameOverPanel), new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(420f, 200f));
            UIFactory.AddVerticalLayout(list, 20f);
            retryButton = UIFactory.CreateButton("Retry", list, "RETRY", 40, StageSession.Retry);
            UIFactory.SetHeight(retryButton, 80f);
            var menu = UIFactory.CreateButton("Menu", list, "MAIN MENU", 40, StageSession.ToMainMenu);
            UIFactory.SetHeight(menu, 80f);

            gameOverPanel.gameObject.SetActive(false);
        }

        private void OnFinished()
        {
            var combo = ComboSystem.Instance;
            long score = combo != null ? combo.Score : 0;
            int maxCombo = combo != null ? combo.MaxCombo : 0;

            var stage = StageSession.Current;
            long best = stage != null ? stage.BestScore : 0;
            bool newBest = stage != null && score > best;
            if (stage != null) stage.SubmitScore(score);

            resultText.text =
                "SCORE   " + score.ToString("N0") + (newBest ? "   NEW BEST!" : "") + "\n" +
                "MAX COMBO   " + maxCombo + "\n" +
                (stage != null ? "BEST   " + System.Math.Max(best, score).ToString("N0") : "");

            UIFactory.EnsureEventSystem();
            gameOverPanel.gameObject.SetActive(true);
            gameOverGroup.alpha = 0f;
            gameOverFade = 0f;
            Time.timeScale = 0f;
            GamePause.IsPaused = true;
            UIFactory.Select(retryButton);
        }

        private void Update()
        {
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

            if (gameOverFade >= 1f && UIFactory.Cancel.WasPressedThisFrame())
                StageSession.ToMainMenu();
        }

        private static string Format(float seconds)
        {
            int s = Mathf.FloorToInt(Mathf.Min(seconds, 5999f));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }
}
