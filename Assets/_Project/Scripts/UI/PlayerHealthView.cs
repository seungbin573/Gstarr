using Gstarr.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Gstarr.UI
{
    /// <summary>
    /// 왼쪽 위 체력 바. heartbar.png 는 가득 찬 칸부터 빈 칸까지 6장이 세로로 붙어 있다.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerHealthView : MonoBehaviour
    {
        private const int Steps = 5;

        // 각 칸의 픽셀 영역 (왼쪽 아래 원점). 바 가운데가 같은 자리에 오도록 248x76 으로 맞췄다.
        private static readonly Rect[] FramePixels =
        {
            new Rect(7f, 54f, 248f, 76f),
            new Rect(6f, 158f, 248f, 76f),
            new Rect(10f, 250f, 248f, 76f),
            new Rect(8f, 327f, 248f, 76f),
            new Rect(10f, 424f, 248f, 76f),
            new Rect(13f, 520f, 248f, 76f)
        };

        private Health health;
        private RawImage bar;
        private Image[] fills;

        private void Awake()
        {
            health = GetComponent<Health>();
            var canvas = Gstarr.Game.UIFactory.CreateCanvas(transform, "HealthCanvas", 110);
            var root = (RectTransform)canvas.transform;
            var heart = Gstarr.Items.GameAssets.Instance != null ? Gstarr.Items.GameAssets.Instance.heartSprite : null;
            if (TryCreateBar(root, heart)) return;

            fills = new Image[Steps];
            for (int i = 0; i < Steps; i++)
            {
                var back = Gstarr.Game.UIFactory.CreateImage("Pip" + i, root, new Color(0.08f, 0.08f, 0.1f, heart != null ? 0f : 0.9f));
                Gstarr.Game.UIFactory.Place(back.rectTransform, new Vector2(0f, 1f), new Vector2(36f + i * 58f, -32f), new Vector2(50f, 28f));
                back.rectTransform.pivot = new Vector2(0f, 1f);

                var fill = Gstarr.Game.UIFactory.CreateImage("Fill", back.transform, heart != null ? Color.white : new Color(0.92f, 0.22f, 0.28f, 1f));
                if (heart != null)
                {
                    fill.sprite = heart;
                    fill.preserveAspect = true;
                }
                var rt = fill.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(4f, 4f);
                rt.offsetMax = new Vector2(-4f, -4f);
                fills[i] = fill;
            }
        }

        /// <summary>세로로 긴 시트면 한 칸씩 잘라 왼쪽 위에 둔다. 화면의 플레이어 키와 비슷한 크기다.</summary>
        private bool TryCreateBar(RectTransform root, Sprite heart)
        {
            var tex = heart != null ? heart.texture : null;
            if (tex == null || tex.height <= tex.width * 1.5f) return false;

            var go = new GameObject("HeartBar", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(root, false);
            bar = go.GetComponent<RawImage>();
            bar.texture = tex;
            bar.raycastTarget = false;
            bar.color = Color.white;
            // 1920x1080 기준. 프레임을 2배로 키우면 1.5유닛 캐릭터와 화면에서 키가 비슷하다.
            Gstarr.Game.UIFactory.Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -18f), new Vector2(496f, 152f));
            return true;
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
            int shown = max <= 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt(current / (float)max * Steps), 0, Steps);
            if (bar != null)
            {
                var tex = bar.texture;
                var px = FramePixels[shown];
                bar.uvRect = new Rect(px.x / tex.width, px.y / tex.height, px.width / tex.width, px.height / tex.height);
                return;
            }

            if (fills == null) return;
            for (int i = 0; i < fills.Length; i++)
            {
                bool alive = i < shown;
                if (fills[i].sprite != null)
                {
                    fills[i].enabled = true;
                    fills[i].color = alive ? Color.white : new Color(1f, 1f, 1f, 0.28f);
                }
                else fills[i].enabled = alive;
            }
        }
    }
}
