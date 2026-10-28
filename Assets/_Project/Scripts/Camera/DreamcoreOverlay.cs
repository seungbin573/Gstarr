using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gstarr.CameraSystem
{
    /// <summary>
    /// 꿈속 화면. 가장자리가 보라로 잠기고, 색이 번지며, 옅은 빛과 먼지가 천천히 떠다닌다.
    /// 플레이 카메라에만 붙고, 결과가 나오면 걷힌다.
    /// </summary>
    public class DreamcoreOverlay : MonoBehaviour
    {
        private const int SortingOrder = 40;
        private const int MoteCount = 18;

        private CanvasGroup group;
        private Image vignette;
        private Image wash;
        private Image glow;
        private Image leak;
        private Image leftFringe;
        private Image rightFringe;
        private RawImage grain;
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
            var side = MakeSideFade();
            var band = MakeBand();

            vignette = StretchImage("Vignette", root, vignetteSprite, new Color(0.16f, 0.05f, 0.28f, 0.5f));
            wash = StretchImage("Wash", root, null, new Color(0.62f, 0.48f, 0.95f, 0.1f));
            glow = PlaceImage("Glow", root, blob, new Vector2(0f, 40f), new Vector2(1400f, 900f), new Color(0.86f, 0.78f, 1f, 0.06f));
            leak = PlaceImage("Leak", root, band, Vector2.zero, new Vector2(2200f, 260f), new Color(0.9f, 0.78f, 1f, 0.06f));
            leftFringe = Edge("LeftFringe", root, side, true, new Color(0.45f, 0.92f, 1f, 0.16f));
            rightFringe = Edge("RightFringe", root, side, false, new Color(1f, 0.42f, 0.72f, 0.16f));

            grain = new GameObject("Grain", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            grain.transform.SetParent(root, false);
            grain.texture = MakeGrain();
            grain.raycastTarget = false;
            grain.color = new Color(1f, 1f, 1f, 0.07f);
            Gstarr.Game.UIFactory.Stretch(grain.rectTransform);

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
            float speed = target > presence ? 0.4f : 0.85f;
            presence = Mathf.MoveTowards(presence, target, Time.unscaledDeltaTime * speed);
            float t = Time.unscaledTime;
            float flicker = 1f - 0.03f * Mathf.PerlinNoise(t * 0.65f, 3.1f);
            group.alpha = presence * flicker;
            if (presence <= 0.001f) return;

            float breathe = 0.5f + 0.5f * Mathf.Sin(t * 0.5f);
            vignette.color = new Color(0.14f, 0.04f, 0.26f, Mathf.Lerp(0.4f, 0.58f, breathe));

            float hue = 0.5f + 0.5f * Mathf.Sin(t * 0.13f);
            float cool = 0.5f + 0.5f * Mathf.Sin(t * 0.08f + 1.7f);
            Color lilac = new Color(0.58f, 0.42f, 0.95f);
            Color pink = new Color(0.96f, 0.68f, 0.82f);
            Color cyan = new Color(0.5f, 0.82f, 0.98f);
            wash.color = WithAlpha(Color.Lerp(Color.Lerp(lilac, pink, hue), cyan, cool * 0.4f), 0.08f + 0.035f * breathe);

            glow.color = new Color(0.88f, 0.78f, 1f, 0.04f + 0.035f * breathe);
            glow.rectTransform.localScale = Vector3.one * (1.02f + 0.07f * breathe);
            glow.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(t * 0.17f) * 50f, 30f + Mathf.Cos(t * 0.13f) * 24f);

            leak.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(t * 0.1f) * 90f, Mathf.Sin(t * 0.16f) * 380f);
            leak.color = new Color(0.92f, 0.8f, 1f, 0.035f + 0.03f * (0.5f + 0.5f * Mathf.Sin(t * 0.4f)));

            float fringe = 160f + 50f * Mathf.Sin(t * 0.27f);
            SetWidth(leftFringe.rectTransform, fringe);
            SetWidth(rightFringe.rectTransform, fringe + 20f * Mathf.Sin(t * 0.21f + 1f));

            grain.uvRect = new Rect(t * 0.17f, t * 0.09f, 5f, 5f);

            for (int i = 0; i < motes.Length; i++)
            {
                var mote = motes[i];
                var pos = mote.rect.anchoredPosition + mote.velocity * Time.unscaledDeltaTime;
                if (pos.y > 620f) pos.y = -620f;
                if (pos.x > 1100f) pos.x = -1100f;
                if (pos.x < -1100f) pos.x = 1100f;
                mote.rect.anchoredPosition = pos;
                float twinkle = 0.55f + 0.45f * Mathf.Sin(t * (0.6f + mote.phase) + mote.phase);
                var c = mote.image.color;
                c.a = mote.alpha * twinkle;
                mote.image.color = c;
            }
        }

        private Mote MakeMote(RectTransform root, Sprite blob, int index)
        {
            float big = index < 5 ? 1f : 0f;
            float size = big > 0.5f ? Random.Range(90f, 180f) : Random.Range(10f, 36f);
            var color = index % 3 == 0
                ? new Color(0.95f, 0.78f, 1f)
                : index % 3 == 1 ? new Color(0.7f, 0.9f, 1f) : new Color(1f, 0.82f, 0.9f);
            color.a = big > 0.5f ? Random.Range(0.05f, 0.1f) : Random.Range(0.18f, 0.42f);
            var image = PlaceImage("Mote" + index, root, blob,
                new Vector2(Random.Range(-900f, 900f), Random.Range(-520f, 520f)),
                new Vector2(size, size), color);
            return new Mote
            {
                rect = image.rectTransform,
                image = image,
                velocity = new Vector2(Random.Range(-12f, 18f), Random.Range(8f, big > 0.5f ? 22f : 46f)),
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

        private Image Edge(string name, RectTransform root, Sprite sprite, bool left, Color color)
        {
            var image = Gstarr.Game.UIFactory.CreateImage(name, root, color);
            image.sprite = sprite;
            var rt = image.rectTransform;
            float x = left ? 0f : 1f;
            rt.anchorMin = new Vector2(x, 0f);
            rt.anchorMax = new Vector2(x, 1f);
            rt.pivot = new Vector2(x, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(180f, 0f);
            if (!left) rt.localScale = new Vector3(-1f, 1f, 1f);
            return image;
        }

        private static void SetWidth(RectTransform rt, float width)
        {
            var size = rt.sizeDelta;
            size.x = width;
            rt.sizeDelta = size;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private Sprite MakeVignette()
        {
            const int size = 128;
            var tex = NewTexture(size, size, FilterMode.Bilinear, TextureWrapMode.Clamp);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx * 0.82f + dy * dy);
                float a = Mathf.SmoothStep(0f, 1f, (d - 0.38f) / 0.85f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return NewSprite(tex);
        }

        private Sprite MakeBlob()
        {
            const int size = 64;
            var tex = NewTexture(size, size, FilterMode.Bilinear, TextureWrapMode.Clamp);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                byte a = (byte)(d * d * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return NewSprite(tex);
        }

        private Sprite MakeSideFade()
        {
            const int width = 64;
            var tex = NewTexture(width, 4, FilterMode.Bilinear, TextureWrapMode.Clamp);
            var px = new Color32[width * 4];
            for (int x = 0; x < width; x++)
            {
                byte a = (byte)(Mathf.SmoothStep(0f, 1f, 1f - x / (float)(width - 1)) * 255f);
                var c = new Color32(255, 255, 255, a);
                for (int y = 0; y < 4; y++) px[y * width + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply();
            return NewSprite(tex);
        }

        private Sprite MakeBand()
        {
            const int height = 64;
            var tex = NewTexture(4, height, FilterMode.Bilinear, TextureWrapMode.Clamp);
            var px = new Color32[4 * height];
            for (int y = 0; y < height; y++)
            {
                float d = Mathf.Abs(y / (float)(height - 1) * 2f - 1f);
                byte a = (byte)(Mathf.SmoothStep(0f, 1f, 1f - d) * 255f);
                var c = new Color32(255, 255, 255, a);
                for (int x = 0; x < 4; x++) px[y * 4 + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply();
            return NewSprite(tex);
        }

        private Texture MakeGrain()
        {
            const int size = 64;
            var tex = NewTexture(size, size, FilterMode.Point, TextureWrapMode.Repeat);
            var px = new Color32[size * size];
            var rng = new System.Random(7);
            for (int i = 0; i < px.Length; i++)
            {
                byte v = (byte)rng.Next(150, 256);
                byte a = (byte)rng.Next(0, 255);
                px[i] = new Color32(v, v, v, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private Texture2D NewTexture(int width, int height, FilterMode filter, TextureWrapMode wrap)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = filter;
            tex.wrapMode = wrap;
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
