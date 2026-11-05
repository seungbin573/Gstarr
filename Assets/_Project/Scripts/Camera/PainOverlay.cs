using System.Collections;
using System.Collections.Generic;
using Gstarr.Combat;
using Gstarr.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gstarr.CameraSystem
{
    /// <summary>
    /// 정신력이 낮을수록 시야 가장자리와 몸 주변이 붉게 맥박 친다.
    /// 쓰러진 뒤에는 화면을 검게 덮어 이야기로 넘긴다.
    /// </summary>
    public class PainOverlay : MonoBehaviour
    {
        private const int SortingOrder = 200;

        private Image vignette;
        private Image wash;
        private Image black;
        private SpriteRenderer aura;
        private readonly List<Object> generated = new List<Object>();
        private bool fading;
        private PlayerController player;
        private Health health;

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
                if (cameras[i] != null && cameras[i].GetComponent<PainOverlay>() == null)
                    cameras[i].gameObject.AddComponent<PainOverlay>();
            }
        }

        private void Awake()
        {
            var canvas = Gstarr.Game.UIFactory.CreateCanvas(transform, "PainCanvas", SortingOrder);
            var root = (RectTransform)canvas.transform;
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            vignette = Stretch("Vignette", root, MakeVignette(), new Color(0.55f, 0.02f, 0.04f, 0f));
            wash = Stretch("Wash", root, null, new Color(0.45f, 0.02f, 0.05f, 0f));
            black = Stretch("Fade", root, null, new Color(0f, 0f, 0f, 0f));
            black.transform.SetAsLastSibling();

            var auraObject = new GameObject("PainAura");
            aura = auraObject.AddComponent<SpriteRenderer>();
            aura.sprite = MakeBlob();
            aura.sortingOrder = 3;
            aura.color = new Color(1f, 0.15f, 0.12f, 0f);
        }

        private void OnDestroy()
        {
            if (aura != null) Destroy(aura.gameObject);
            for (int i = generated.Count - 1; i >= 0; i--)
                if (generated[i] != null) Destroy(generated[i]);
        }

        private void LateUpdate()
        {
            if (!fading) BindPlayer();
            float hurt = HurtAmount();
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(1.4f, 6.2f, hurt));
            float edge = hurt * Mathf.Lerp(0.22f, 0.58f, pulse);
            vignette.color = new Color(0.62f, 0.03f, 0.05f, edge);
            wash.color = new Color(0.4f, 0.02f, 0.04f, hurt * (0.05f + 0.07f * pulse));

            if (aura != null)
            {
                bool show = player != null && hurt > 0.02f && !fading;
                aura.enabled = show;
                if (show)
                {
                    aura.transform.position = player.transform.position + Vector3.up * 0.75f;
                    float radius = Mathf.Lerp(1.15f, 2.05f, hurt) * (1f + 0.06f * pulse);
                    aura.transform.localScale = new Vector3(radius, radius * 0.72f, 1f);
                    aura.color = new Color(1f, 0.18f, 0.12f, hurt * (0.1f + 0.16f * pulse));
                }
            }
        }

        public IEnumerator FadeToBlack(float seconds)
        {
            fading = true;
            if (aura != null) aura.enabled = false;
            float start = black.color.a;
            float duration = Mathf.Max(0.05f, seconds);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                var c = Color.black;
                c.a = Mathf.Lerp(start, 1f, t / duration);
                black.color = c;
                yield return null;
            }
            black.color = Color.black;
        }

        /// <summary>이야기 뒤 결과 화면이 보이게 검은 막을 걷는다.</summary>
        public void ClearFade()
        {
            fading = false;
            if (black != null) black.color = new Color(0f, 0f, 0f, 0f);
        }

        private void BindPlayer()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                health = null;
                return;
            }
            if (health == null || health.gameObject != player.gameObject)
                health = player.GetComponent<Health>();
        }

        private float HurtAmount()
        {
            if (health == null || health.Max <= 0 || health.Current >= health.Max) return 0f;
            if (health.Current <= 0) return 1f;
            return Mathf.Pow(1f - health.Current / (float)health.Max, 0.72f);
        }

        private Image Stretch(string name, RectTransform root, Sprite sprite, Color color)
        {
            var image = Gstarr.Game.UIFactory.CreateImage(name, root, color);
            image.sprite = sprite;
            image.raycastTarget = false;
            Gstarr.Game.UIFactory.Stretch(image.rectTransform);
            return image;
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
                float d = Mathf.Sqrt(dx * dx * 0.72f + dy * dy);
                float a = Mathf.SmoothStep(0f, 1f, (d - 0.42f) / 0.7f);
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
                px[y * size + x] = new Color32(255, 255, 255, (byte)(d * d * 255f));
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
