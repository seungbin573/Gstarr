using Gstarr.Combat;
using Gstarr.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Gstarr.UI
{
    /// <summary>플레이어 근처 보스의 이름과 체력. 2페이즈면 이름 옆에 II를 붙인다.</summary>
    public sealed class BossHealthHud : MonoBehaviour
    {
        private RectTransform root;
        private Text nameText;
        private Image fill;
        private BossMark shown;
        private Health shownHealth;

        public static void Ensure()
        {
            if (Object.FindAnyObjectByType<BossHealthHud>() != null) return;
            new GameObject("BossHealthHud").AddComponent<BossHealthHud>();
        }

        private void Awake()
        {
            var canvas = UIFactory.CreateCanvas(transform, "BossBar", 125);
            var parent = (RectTransform)canvas.transform;

            root = UIFactory.CreateRect("Plate", parent);
            UIFactory.Place(root, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(560f, 64f));

            nameText = UIFactory.CreateText("Name", root, "", 28, style: FontStyle.Bold);
            UIFactory.Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(520f, 34f));
            nameText.alignment = TextAnchor.MiddleCenter;

            var back = UIFactory.CreateImage("Back", root, new Color(0.08f, 0.07f, 0.09f, 0.9f));
            UIFactory.Place(back.rectTransform, new Vector2(0f, 0f), new Vector2(20f, 8f), new Vector2(520f, 16f));
            back.rectTransform.pivot = new Vector2(0f, 0f);

            fill = UIFactory.CreateImage("Fill", back.transform, new Color(0.92f, 0.28f, 0.32f, 1f));
            var bar = fill.rectTransform;
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = Vector2.one;
            bar.offsetMin = new Vector2(2f, 2f);
            bar.offsetMax = new Vector2(-2f, -2f);
            bar.pivot = new Vector2(0f, 0.5f);

            root.gameObject.SetActive(false);
        }

        private void Update()
        {
            var mark = Nearest();
            if (mark != shown)
            {
                if (shownHealth != null) shownHealth.onHealthChanged.RemoveListener(Refresh);
                shown = mark;
                shownHealth = mark != null ? mark.GetComponent<Health>() : null;
                if (shownHealth != null) shownHealth.onHealthChanged.AddListener(Refresh);
            }

            bool visible = shown != null && shownHealth != null && shownHealth.Current > 0 && !GamePause.IsPaused;
            if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
            if (!visible) return;
            Refresh(shownHealth.Current, shownHealth.Max);
        }

        private void OnDestroy()
        {
            if (shownHealth != null) shownHealth.onHealthChanged.RemoveListener(Refresh);
        }

        private static BossMark Nearest()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return null;
            BossMark best = null;
            float bestDistance = 26f;
            var marks = BossMark.Living;
            for (int i = 0; i < marks.Count; i++)
            {
                var mark = marks[i];
                if (mark == null) continue;
                var health = mark.GetComponent<Health>();
                if (health == null || health.Current <= 0) continue;
                float distance = Mathf.Abs(player.transform.position.x - mark.transform.position.x);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = mark;
            }
            return best;
        }

        private void Refresh(int current, int max)
        {
            if (shown == null || fill == null) return;
            nameText.text = shown.Phase >= 2 ? shown.DisplayName + "  II" : shown.DisplayName;
            fill.color = shown.Phase >= 2
                ? new Color(1f, 0.55f, 0.2f, 1f)
                : new Color(0.92f, 0.28f, 0.32f, 1f);
            float ratio = max <= 0 ? 0f : Mathf.Clamp01(current / (float)max);
            var bar = fill.rectTransform;
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(ratio, 1f);
            bar.offsetMin = new Vector2(2f, 2f);
            bar.offsetMax = new Vector2(-2f, -2f);
        }
    }
}
