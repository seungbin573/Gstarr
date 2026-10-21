using Gstarr.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Gstarr.Rhythm
{
    /// <summary>
    /// 오른쪽: 랭크 이미지(C/B/A/S) + 스타일 게이지 + 콤보 수.
    /// 오른쪽 위: 총 점수와 획득 점수 팝업.
    /// </summary>
    [RequireComponent(typeof(ComboSystem))]
    public class ComboUI : MonoBehaviour
    {
        [Tooltip("C, B, A, S 순서의 랭크 이미지")]
        [SerializeField] private Sprite[] rankSprites = new Sprite[ComboSystem.RankCount];
        [SerializeField] private Vector2 rankImageSize = new Vector2(300f, 246f);
        [SerializeField] private Vector2 panelPosition = new Vector2(-40f, 120f);
        [SerializeField] private Vector2 gaugeSize = new Vector2(280f, 18f);

        [SerializeField] private Color[] rankColors =
        {
            new Color(0.35f, 0.75f, 1f),
            new Color(0.4f, 0.95f, 0.45f),
            new Color(1f, 0.7f, 0.2f),
            new Color(1f, 0.3f, 0.35f)
        };
        [SerializeField] private Color noRankColor = new Color(0.75f, 0.75f, 0.8f);

        private ComboSystem combo;
        private Font font;

        private RectTransform rankRt;
        private Image rankImage;
        private Image gaugeFill;
        private Text comboText;
        private Text comboLabel;
        private Text scoreText;
        private Text gainText;

        private float rankAnim;
        private float rankShake;
        private float comboPunch;
        private float gainTimer;
        private double displayedScore;

        private const float RankPopDuration = 0.35f;

        private void Awake()
        {
            combo = GetComponent<ComboSystem>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var assets = GameAssets.Instance;
            if (assets != null && assets.rankSprites != null)
                for (int i = 0; i < rankSprites.Length && i < assets.rankSprites.Length; i++)
                    if (rankSprites[i] == null) rankSprites[i] = assets.rankSprites[i];

            BuildUI();
            ApplyRank(StyleRank.None, instant: true);
        }

        private void OnEnable()
        {
            combo.ComboChanged += OnComboChanged;
            combo.RankChanged += OnRankChanged;
            combo.ScoreAdded += OnScoreAdded;
        }

        private void OnDisable()
        {
            combo.ComboChanged -= OnComboChanged;
            combo.RankChanged -= OnRankChanged;
            combo.ScoreAdded -= OnScoreAdded;
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("ComboCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 101;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 오른쪽 위 점수
            var scoreLabel = CreateText("ScoreLabel", canvasGo.transform, 26, TextAnchor.UpperRight);
            Anchor(scoreLabel.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -24f), new Vector2(400f, 34f));
            scoreLabel.text = "SCORE";
            scoreLabel.color = new Color(1f, 1f, 1f, 0.7f);

            scoreText = CreateText("Score", canvasGo.transform, 56, TextAnchor.UpperRight);
            Anchor(scoreText.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -54f), new Vector2(600f, 70f));
            scoreText.text = "0";

            gainText = CreateText("Gain", canvasGo.transform, 30, TextAnchor.UpperRight);
            Anchor(gainText.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -122f), new Vector2(400f, 40f));
            gainText.text = "";

            // 오른쪽 랭크 패널
            var panel = new GameObject("RankPanel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(canvasGo.transform, false);
            Anchor(panel, new Vector2(1f, 0.5f), panelPosition, new Vector2(rankImageSize.x, rankImageSize.y + 120f));

            rankRt = new GameObject("Rank", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rankRt.SetParent(panel, false);
            rankRt.anchorMin = rankRt.anchorMax = new Vector2(0.5f, 1f);
            rankRt.pivot = new Vector2(0.5f, 1f);
            rankRt.sizeDelta = rankImageSize;
            rankImage = rankRt.GetComponent<Image>();
            rankImage.raycastTarget = false;
            rankImage.preserveAspect = true;

            var gaugeBg = new GameObject("GaugeBg", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            gaugeBg.SetParent(panel, false);
            gaugeBg.anchorMin = gaugeBg.anchorMax = new Vector2(0.5f, 1f);
            gaugeBg.pivot = new Vector2(0.5f, 1f);
            gaugeBg.anchoredPosition = new Vector2(0f, -rankImageSize.y - 4f);
            gaugeBg.sizeDelta = gaugeSize + new Vector2(6f, 6f);
            var bgImg = gaugeBg.GetComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.6f);
            bgImg.raycastTarget = false;

            var fillRt = new GameObject("GaugeFill", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            fillRt.SetParent(gaugeBg, false);
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(-3f, -3f);
            gaugeFill = fillRt.GetComponent<Image>();
            gaugeFill.raycastTarget = false;
            gaugeFill.sprite = CreateWhiteSprite();
            gaugeFill.type = Image.Type.Filled;
            gaugeFill.fillMethod = Image.FillMethod.Horizontal;
            gaugeFill.fillOrigin = 0;

            comboText = CreateText("Combo", panel, 64, TextAnchor.UpperRight);
            comboText.rectTransform.anchorMin = comboText.rectTransform.anchorMax = new Vector2(1f, 1f);
            comboText.rectTransform.pivot = new Vector2(1f, 1f);
            comboText.rectTransform.anchoredPosition = new Vector2(-70f, -rankImageSize.y - 34f);
            comboText.rectTransform.sizeDelta = new Vector2(220f, 76f);

            comboLabel = CreateText("ComboLabel", panel, 26, TextAnchor.UpperLeft);
            comboLabel.rectTransform.anchorMin = comboLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            comboLabel.rectTransform.pivot = new Vector2(1f, 1f);
            comboLabel.rectTransform.anchoredPosition = new Vector2(0f, -rankImageSize.y - 66f);
            comboLabel.rectTransform.sizeDelta = new Vector2(70f, 34f);
            comboLabel.text = "HITS";
        }

        private Text CreateText(string name, Transform parent, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.BoldAndItalic;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static Sprite CreateWhiteSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        private void OnComboChanged(int value)
        {
            comboPunch = value > 0 ? 1f : 0f;
        }

        private void OnRankChanged(StyleRank oldRank, StyleRank newRank)
        {
            if (newRank > oldRank) rankAnim = RankPopDuration;
            else rankShake = 0.3f;
            ApplyRank(newRank, instant: false);
        }

        private void OnScoreAdded(int points, long total)
        {
            gainText.text = $"+{points:N0}";
            gainText.color = CurrentColor();
            gainTimer = 0.8f;
        }

        private void ApplyRank(StyleRank rank, bool instant)
        {
            bool has = rank != StyleRank.None && rankSprites[(int)rank] != null;
            rankImage.enabled = has;
            if (has) rankImage.sprite = rankSprites[(int)rank];
            gaugeFill.color = CurrentColor(rank);
            if (instant) rankAnim = 0f;
        }

        private Color CurrentColor() => CurrentColor(combo.Rank);

        private Color CurrentColor(StyleRank rank) =>
            rank == StyleRank.None ? noRankColor : rankColors[Mathf.Min((int)rank, rankColors.Length - 1)];

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // 랭크 등장: 크게 튀어나오며 기울었다가 제자리
            float scale = 1f;
            float angle = 0f;
            Vector2 offset = Vector2.zero;
            if (rankAnim > 0f)
            {
                rankAnim -= dt;
                float t = 1f - Mathf.Clamp01(rankAnim / RankPopDuration);
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                scale = Mathf.Lerp(1.9f, 1f, ease);
                angle = Mathf.Lerp(-14f, 0f, ease);
            }
            if (rankShake > 0f)
            {
                rankShake -= dt;
                offset = Random.insideUnitCircle * 10f * Mathf.Clamp01(rankShake / 0.3f);
            }
            // 랭크가 높을수록 박자에 맞춰 살짝 맥동
            float beatPulse = combo.Rank >= StyleRank.A ? 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 12f) : 1f;
            rankRt.localScale = Vector3.one * (scale * beatPulse);
            rankRt.localRotation = Quaternion.Euler(0f, 0f, angle);
            rankRt.anchoredPosition = offset;

            gaugeFill.fillAmount = Mathf.MoveTowards(gaugeFill.fillAmount, combo.RankProgress, dt * 3f);

            comboPunch = Mathf.MoveTowards(comboPunch, 0f, dt * 5f);
            comboText.text = combo.Combo > 0 ? combo.Combo.ToString() : "";
            comboLabel.enabled = combo.Combo > 0;
            comboText.rectTransform.localScale = Vector3.one * (1f + 0.35f * comboPunch);
            comboText.color = Color.Lerp(Color.white, CurrentColor(), 0.6f);

            // 점수는 빠르게 굴러 올라가는 연출
            displayedScore += (combo.Score - displayedScore) * Mathf.Min(1f, dt * 10f);
            if (combo.Score - displayedScore < 1) displayedScore = combo.Score;
            scoreText.text = ((long)displayedScore).ToString("N0");

            if (gainTimer > 0f)
            {
                gainTimer -= dt;
                var c = gainText.color;
                c.a = Mathf.Clamp01(gainTimer / 0.3f);
                gainText.color = c;
            }
        }
    }
}
