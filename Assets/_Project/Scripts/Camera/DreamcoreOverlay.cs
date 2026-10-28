using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gstarr.CameraSystem
{
    /// <summary>
    /// 꿈속 화면. 가운데는 선명하게 두고, 모서리와 빛만 부드럽게 깔린다.
    /// 플레이 카메라에만 붙고, 결과가 나오면 걷힌다.
    /// </summary>
    public class DreamcoreOverlay : MonoBehaviour
    {
        private const int SortingOrder = 40;
        private const int MoteCount = 7;

        private CanvasGroup group;
        private Image vignette;
        private Image wash;
        private Image glow;
        private Image leak;
        private readonly Mote[] motes = new Mote[MoteCount];
        private readonly System.Collections.Generic.List<Object> generated = new System.Collections.Generic.List<Object>();
        private float presence;

        private struct Mote
        {
            public RectTransform rect;
            public Image image;
            public Vector2 velocity;
            public float phase;
            public float alpha;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var cameras = Object.FindObjectsByType<CameraFollow2D>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].GetComponent<DreamcoreOverlay>() == null)
                    cameras[i].gameObject.AddComponent<DreamcoreOverlay>();
            }
        }

        private void Awake()
        {
            var canvas = Gstarr.Game.UIFactory.CreateCanvas(transform, "DreamcoreCanvas", SortingOrder);
            var root = (RectTransform)canvas.transform;
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;

            var vignetteSprite = MakeVignette();
            var blob = MakeBlob();
            var band = MakeBand();

            vignette = StretchImage("Vignette", root, vignetteSprite, new Color(0.22f, 0.1f, 0.38f, 0.22f));
            wash = StretchImage("Wash", root, null, new Color(0.78f, 0.7f, 0.95f, 0.035f));
            glow = PlaceImage("Glow", root, blob, new Vector2(0f, 30f), new Vector2(1600f, 1000f), new Color(0.9f, 0.84f, 1f, 0.035f));
            leak = PlaceImage("Leak", root, band, Vector2.zero, new Vector2(2400f, 220f), new Color(0.95f, 0.86f, 1f, 0.04f));

            for (int i = 0; i < motes.Length; i++)
                motes[i] = MakeMote(root, blob, i);
        }

        private void OnDestroy()
        {
            for (int i = generated.Count - 1; i >= 0; i--)
            {
                if (generated[i] != null) Destroy(generated[i]);
            }
        }

        private void Update()
        {
            float target = Gstarr.Game.StageHUD.HasResult ? 0f : 1f;
            float speed = target > presence ? 0.35f : 0.7f;
            presence = Mathf.MoveTowards(presence, target, Time.unscaledDeltaTime * speed);
            group.alpha = presence;
            if (presence <= 0.001f) return;

            float t = Time.unscaledTime;
            float breathe = 0.5f + 0.5f * Mathf.Sin(t * 0.35f);
            vignette.color = new Color(0.2f, 0.1f, 0.36f, Mathf.Lerp(0.16f, 0.26f, breathe));

            float hue = 0.5f + 0.5f * Mathf.Sin(t * 0.1f);
            Color lilac = new Color(0.72f, 0.64f, 0.95f);
            Color pink = new Color(0.96f, 0.78f, 0.88f);
            wash.color = WithAlpha(Color.Lerp(lilac, pink, hue), 0.028f + 0.012f * breathe);

            glow.color = new Color(0.92f, 0.86f, 1f, 0.025f + 0.02f * breathe);
            glow.rectTransform.localScale = Vector3.one * (1.02f + 0.04f * breathe);
            glow.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(t * 0.12f) * 40f, 24f + Mathf.Cos(t * 0.09f) * 16f);

            leak.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(t * 0.08f) * 70f, Mathf.Sin(t * 0.11f) * 280f);
            leak.color = new Color(0.96f, 0.88f, 1f, 0.025f + 0.02f * breathe);

            for (int i = 0; i < motes.Length; i++)
            {
                var mote = motes[i];
                var pos = mote.rect.anchoredPosition + mote.velocity * Time.unscaledDeltaTime;
                if (pos.y > 640f) pos.y = -640f;
                if (pos.x > 1100f) pos.x = -1100f;
                if (pos.x < -1100f) pos.x = 1100f;
                mote.rect.anchoredPosition = pos;
                float twinkle = 0.75f + 0.25f * Mathf.Sin(t * 0.5f + mote.phase);
                var c = mote.image.color;
                c.a = mote.alpha * twinkle;
                mote.image.color = c;
                mote.rect.localScale = Vector3.one * (1f + 0.04f * twinkle);
            }
        }

        private Mote MakeMote(RectTransform root, Sprite blob, int index)
        {
            float size = Random.Range(180f, 340f);
            var color = index % 3 == 0
                ? new Color(0.95f, 0.86f, 1f)
                : index % 3 == 1 ? new Color(0.78f, 0.9f, 1f) : new Color(1f, 0.86f, 0.92f);
            color.a = Random.Range(0.035f, 0.07f);
            var image = PlaceImage("Mote" + index, root, blob,
                new Vector2(Random.Range(-860f, 860f), Random.Range(-480f, 480f)),
                new Vector2(size, size), color);
            return new Mote
            {
                rect = image.rectTransform,
                image = image,
                velocity = new Vector2(Random.Range(-8f, 10f), Random.Range(6f, 16f)),
                phase = Random.Range(0f, 6.28f),
                alpha = color.a
            };
        }

        private Image StretchImage(string name, RectTransform root, Sprite sprite, Color color)
        {
            var image = Gstarr.Game.UIFactory.CreateImage(name, root, color);
            image.sprite = sprite;
            Gstarr.Game.UIFactory.Stretch(image.rectTransform);
            return image;
        }

        private Image PlaceImage(string name, RectTransform root, Sprite sprite, Vector2 pos, Vector2 size, Color color)
        {
            var image = Gstarr.Game.UIFactory.CreateImage(name, root, color);
            image.sprite = sprite;
            Gstarr.Game.UIFactory.Place(image.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
            return image;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private Sprite MakeVignette()
        {
            const int size = 256;
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx * 0.75f + dy * dy);
                float a = Mathf.SmoothStep(0f, 1f, (d - 0.78f) / 0.55f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return NewSprite(tex);
        }

        private Sprite MakeBlob()
        {
            const int size = 128;
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                byte a = (byte)(d * d * d * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return NewSprite(tex);
        }

        private Sprite MakeBand()
        {
            const int height = 128;
            var tex = NewTexture(4, height);
            var px = new Color32[4 * height];
            for (int y = 0; y < height; y++)
            {
                float d = Mathf.Abs(y / (float)(height - 1) * 2f - 1f);
                float a = Mathf.SmoothStep(0f, 1f, 1f - d);
                byte v = (byte)(a * a * 255f);
                var c = new Color32(255, 255, 255, v);
                for (int x = 0; x < 4; x++) px[y * 4 + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply();
            return NewSprite(tex);
        }

        private Texture2D NewTexture(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            generated.Add(tex);
            return tex;
        }

        private Sprite NewSprite(Texture2D tex)
        {
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            generated.Add(sprite);
            return sprite;
        }
    }
}
