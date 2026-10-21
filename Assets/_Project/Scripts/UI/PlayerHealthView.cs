using Gstarr.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Gstarr.UI
{
    /// <summary>왼쪽 위 체력 5칸.</summary>
    [RequireComponent(typeof(Health))]
    public class PlayerHealthView : MonoBehaviour
    {
        private const int Segments = 5;

        private Health health;
        private Image[] fills;

        private void Awake()
        {
            health = GetComponent<Health>();
            var canvas = Gstarr.Game.UIFactory.CreateCanvas(transform, "HealthCanvas", 110);
            var root = (RectTransform)canvas.transform;

            fills = new Image[Segments];
            for (int i = 0; i < Segments; i++)
            {
                var back = Gstarr.Game.UIFactory.CreateImage("Pip" + i, root, new Color(0.08f, 0.08f, 0.1f, 0.9f));
                Gstarr.Game.UIFactory.Place(back.rectTransform, new Vector2(0f, 1f), new Vector2(36f + i * 58f, -32f), new Vector2(50f, 28f));
                back.rectTransform.pivot = new Vector2(0f, 1f);

                var fill = Gstarr.Game.UIFactory.CreateImage("Fill", back.transform, new Color(0.92f, 0.22f, 0.28f, 1f));
                var rt = fill.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(4f, 4f);
                rt.offsetMax = new Vector2(-4f, -4f);
                fills[i] = fill;
            }
        }

        private void OnEnable()
        {
            if (health != null) health.onHealthChanged.AddListener(Refresh);
        }

        private void OnDisable()
        {
            if (health != null) health.onHealthChanged.RemoveListener(Refresh);
        }

        private void Start() => Refresh(health.Current, health.Max);

        private void Refresh(int current, int max)
        {
            int shown = max <= 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt(current / (float)max * Segments), 0, Segments);
            for (int i = 0; i < fills.Length; i++)
                fills[i].enabled = i < shown;
        }
    }
}
