using System.Collections.Generic;
using Gstarr.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Gstarr.Rhythm
{
    /// <summary>
    /// 화면 아래 박자 바. 노트가 양쪽에서 가운데 링으로 모여들고, 링에 닿는 순간이 박자.
    /// 판정 결과(Perfect / Good / Miss)를 링 위에 표시한다.
    /// </summary>
    [RequireComponent(typeof(RhythmConductor))]
    public class BeatBarUI : MonoBehaviour
    {
        [SerializeField] private Sprite noteSprite;
        [SerializeField] private Sprite centerSprite;
        [Tooltip("노트가 가운데에 도착하기 몇 초 전부터 보일지")]
        [SerializeField] private float lookahead = 1.6f;
        [SerializeField] private Vector2 barSize = new Vector2(1000f, 110f);
        [SerializeField] private float bottomMargin = 36f;
        [SerializeField] private Vector2 noteSize = new Vector2(16f, 56f);
        [SerializeField] private float centerSize = 92f;

        [Header("색")]
        [SerializeField] private Color noteColor = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField] private Color centerColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color perfectColor = new Color(1f, 0.85f, 0.25f);
        [SerializeField] private Color goodColor = new Color(0.4f, 0.9f, 1f);
        [SerializeField] private Color missColor = new Color(1f, 0.3f, 0.3f);

        private RhythmConductor conductor;
        private RectTransform bar;
        private RectTransform hitZone;
        private Image center;
        private Text judgeText;
        private readonly List<Image> notePool = new List<Image>();

        private float pulse;
        private float flash;
        private Color flashColor;
        private float judgeTimer;
        private Font font;

        private void Awake()
        {
            conductor = GetComponent<RhythmConductor>();
            var assets = GameAssets.Instance;
            if (assets != null)
            {
                if (noteSprite == null) noteSprite = assets.beatNoteSprite;
                if (centerSprite == null) centerSprite = assets.beatCenterSprite;
            }
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildUI();
        }

        private void OnEnable()
        {
            conductor.Beat += OnBeat;
            conductor.Judged += OnJudged;
        }

        private void OnDisable()
        {
            conductor.Beat -= OnBeat;
            conductor.Judged -= OnJudged;
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("BeatBarCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            bar = CreateImage("Bar", canvasGo.transform, null, new Color(0f, 0f, 0f, 0.35f));
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, bottomMargin);
            bar.sizeDelta = barSize;

            var line = CreateImage("Line", bar, null, new Color(1f, 1f, 1f, 0.25f));
            line.sizeDelta = new Vector2(barSize.x - 40f, 4f);

            hitZone = CreateImage("HitZone", bar, null, new Color(1f, 1f, 1f, 0.12f));

            center = CreateImage("Center", bar, centerSprite, centerColor).GetComponent<Image>();
            center.rectTransform.sizeDelta = new Vector2(centerSize, centerSize);

            var judgeGo = new GameObject("Judge", typeof(RectTransform), typeof(Text));
            judgeGo.transform.SetParent(bar, false);
            var judgeRt = (RectTransform)judgeGo.transform;
            judgeRt.anchoredPosition = new Vector2(0f, barSize.y * 0.5f + 34f);
            judgeRt.sizeDelta = new Vector2(400f, 60f);
            judgeText = judgeGo.GetComponent<Text>();
            judgeText.font = font;
            judgeText.fontSize = 40;
            judgeText.fontStyle = FontStyle.Bold;
            judgeText.alignment = TextAnchor.MiddleCenter;
            judgeText.text = "";
            var outline = judgeGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private RectTransform CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        private Image GetNote(int index)
        {
            while (notePool.Count <= index)
            {
                var rt = CreateImage("Note", bar, noteSprite, noteColor);
                rt.sizeDelta = noteSize;
                rt.SetSiblingIndex(center.transform.GetSiblingIndex());
                notePool.Add(rt.GetComponent<Image>());
            }
            return notePool[index];
        }

        private void OnBeat(long beat)
        {
            pulse = 1f;
        }

        private void OnJudged(BeatAction action, BeatJudgement judgement, double error)
        {
            switch (judgement)
            {
                case BeatJudgement.Perfect: flashColor = perfectColor; judgeText.text = "PERFECT"; break;
                case BeatJudgement.Good: flashColor = goodColor; judgeText.text = error < 0 ? "GOOD  EARLY" : "GOOD  LATE"; break;
                default: flashColor = missColor; judgeText.text = "MISS"; break;
            }
            judgeText.color = flashColor;
            flash = 1f;
            judgeTimer = 0.6f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float half = barSize.x * 0.5f - 20f;

            float zoneWidth = conductor.GoodWindow / lookahead * half * 2f;
            hitZone.sizeDelta = new Vector2(zoneWidth, barSize.y - 20f);

            int used = 0;
            if (conductor.IsReady)
            {
                double now = conductor.SongTime;
                for (long b = conductor.FirstBeatAtOrAfter(now - 0.15); ; b++)
                {
                    double until = conductor.BeatTime(b) - now;
                    if (double.IsInfinity(until) || until > lookahead) break;

                    float x = (float)(until / lookahead) * half;
                    // 다가올 때는 점점 진하게, 지나간 뒤에는 빠르게 사라짐
                    float alpha = until >= 0
                        ? Mathf.Clamp01((float)(1.0 - until / lookahead) * 1.6f)
                        : Mathf.Clamp01(1f + (float)until / 0.15f);

                    for (int side = -1; side <= 1; side += 2)
                    {
                        var note = GetNote(used++);
                        note.enabled = true;
                        note.rectTransform.anchoredPosition = new Vector2(side * x, 0f);
                        var c = noteColor;
                        c.a *= alpha;
                        note.color = c;
                    }
                }
            }
            for (int i = used; i < notePool.Count; i++) notePool[i].enabled = false;

            pulse = Mathf.MoveTowards(pulse, 0f, dt * 6f);
            flash = Mathf.MoveTowards(flash, 0f, dt * 3f);
            float s = 1f + 0.25f * pulse * pulse;
            center.rectTransform.localScale = new Vector3(s, s, 1f);
            center.color = Color.Lerp(centerColor, flashColor, flash);

            if (judgeTimer > 0f)
            {
                judgeTimer -= dt;
                var c = judgeText.color;
                c.a = Mathf.Clamp01(judgeTimer / 0.25f);
                judgeText.color = c;
                float pop = 1f + 0.3f * Mathf.Clamp01((judgeTimer - 0.45f) / 0.15f);
                judgeText.rectTransform.localScale = new Vector3(pop, pop, 1f);
            }
        }
    }
}
