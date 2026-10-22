using UnityEngine;

namespace Gstarr.World
{
    /// <summary>
    /// 록맨 보스방 문. 진행 방향으로 통과하면 셔터가 내려오고, 다시 나갈 수 없다.
    /// </summary>
    public class BossDoor : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("플레이어가 이 방향으로 통과하면 잠긴다. 1 = 오른쪽, -1 = 왼쪽")]
        private int enterDirection = 1;

        [SerializeField] private float passageHeight = 5.75f;
        [SerializeField] private float closeDuration = 0.1f;

        private const int PixelsPerUnit = 16;
        private const int TexWidth = 40;
        private const int TexHeight = 92;
        private const int PostPixels = 5;

        private static Sprite frameSprite;
        private static Sprite shutterSprite;
        private static AudioClip slamClip;

        private Transform shutter;
        private BoxCollider2D seal;
        private SpriteRenderer lamp;
        private float openY;
        private float closeTimer = -1f;
        private bool sealed;
        private Collider2D playerBody;

        private float Width => TexWidth / (float)PixelsPerUnit;
        private float Gap => (TexWidth - PostPixels * 2) / (float)PixelsPerUnit;
        private float VisualScale => passageHeight / (TexHeight / (float)PixelsPerUnit);

        private void Start()
        {
            enterDirection = enterDirection >= 0 ? 1 : -1;
            SnapToFloor();
            Build();
        }

        private void Update()
        {
            if (sealed && closeTimer < 0f) return;

            if (!sealed && PlayerHasPassed())
                BeginClose();

            if (closeTimer < 0f || shutter == null) return;
            closeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(closeTimer / Mathf.Max(0.01f, closeDuration));
            float eased = 1f - (1f - t) * (1f - t);
            var pos = shutter.localPosition;
            pos.y = Mathf.Lerp(openY, 0f, eased);
            shutter.localPosition = pos;
            if (t < 1f) return;

            closeTimer = -1f;
            if (lamp != null) lamp.color = new Color(1f, 0.25f, 0.2f);
            PlaySlam();
        }

        private void SnapToFloor()
        {
            int mask = LayerMask.GetMask("Ground");
            if (mask == 0) mask = Physics2D.DefaultRaycastLayers;
            var origin = (Vector2)transform.position + Vector2.up * 0.25f;
            var hit = Physics2D.Raycast(origin, Vector2.down, 8f, mask);
            if (hit.collider == null) return;
            transform.position = new Vector3(transform.position.x, hit.point.y, 0f);
        }

        private void Build()
        {
            EnsureSprites();
            float scale = passageHeight / (TexHeight / (float)PixelsPerUnit);
            int ground = LayerMask.NameToLayer("Ground");
            if (ground < 0) ground = 0;

            var frame = CreatePart("Frame", frameSprite, 2);
            frame.transform.localScale = new Vector3(scale, scale, 1f);

            var shutterGo = CreatePart("Shutter", shutterSprite, 3);
            shutter = shutterGo.transform;
            shutter.localScale = new Vector3(scale, scale, 1f);
            openY = passageHeight;
            shutter.localPosition = new Vector3(0f, openY, 0f);
            shutterGo.GetComponent<SpriteRenderer>().maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            var maskGo = new GameObject("Opening");
            maskGo.transform.SetParent(transform, false);
            maskGo.transform.localScale = new Vector3(scale, scale, 1f);
            var mask = maskGo.AddComponent<SpriteMask>();
            mask.sprite = shutterSprite;
            mask.isCustomRangeActive = true;
            mask.frontSortingOrder = 3;
            mask.backSortingOrder = 3;

            var lampGo = CreatePart("Lamp", LampSprite(), 4);
            lamp = lampGo.GetComponent<SpriteRenderer>();
            lamp.color = new Color(0.35f, 0.9f, 0.4f);
            lampGo.transform.localPosition = new Vector3(0f, passageHeight - 0.28f, 0f);

            float post = PostPixels / (float)PixelsPerUnit * scale;
            float gap = Gap * scale;
            AddPost(ground, -gap * 0.5f - post * 0.5f, post);
            AddPost(ground, gap * 0.5f + post * 0.5f, post);

            var sealGo = new GameObject("Seal");
            sealGo.layer = ground;
            sealGo.transform.SetParent(transform, false);
            seal = sealGo.AddComponent<BoxCollider2D>();
            seal.size = new Vector2(gap, passageHeight);
            seal.offset = new Vector2(0f, passageHeight * 0.5f);
            seal.enabled = false;
        }

        private GameObject CreatePart(string partName, Sprite sprite, int order)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return go;
        }

        private void AddPost(int layer, float x, float width)
        {
            var go = new GameObject("Post");
            go.layer = layer;
            go.transform.SetParent(transform, false);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(width, passageHeight);
            box.offset = new Vector2(x, passageHeight * 0.5f);
        }

        private bool PlayerHasPassed()
        {
            if (playerBody == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) return false;
                playerBody = player.GetComponent<Collider2D>();
                if (playerBody == null) return false;
            }

            float insideEdge = transform.position.x + enterDirection * Width * VisualScale * 0.5f;
            return enterDirection > 0
                ? playerBody.bounds.min.x > insideEdge
                : playerBody.bounds.max.x < insideEdge;
        }

        private void BeginClose()
        {
            sealed = true;
            closeTimer = 0f;
            if (seal != null) seal.enabled = true;
        }

        private void PlaySlam()
        {
            if (slamClip == null)
            {
                const int rate = 22050;
                int n = rate / 10;
                var data = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float u = i / (float)n;
                    float body = Mathf.Sin(u * 48f * Mathf.PI) * Mathf.Exp(-u * 10f);
                    float knock = (Mathf.PerlinNoise(u * 18f, 0.2f) * 2f - 1f) * Mathf.Exp(-u * 16f);
                    data[i] = (body * 0.7f + knock * 0.3f) * 0.85f;
                }
                slamClip = AudioClip.Create("BossDoorSlam", n, 1, rate, false);
                slamClip.SetData(data, 0);
            }

            var source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            source.PlayOneShot(slamClip, 0.9f);
        }

        private static void EnsureSprites()
        {
            if (frameSprite == null) frameSprite = MakeFrame();
            if (shutterSprite == null) shutterSprite = MakeShutter();
        }

        private static Sprite LampSprite()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite MakeFrame()
        {
            var px = new Color32[TexWidth * TexHeight];
            var edge = new Color32(18, 28, 58, 255);
            var metal = new Color32(46, 78, 138, 255);
            var trim = new Color32(168, 196, 230, 255);
            for (int y = 0; y < TexHeight; y++)
            {
                for (int x = 0; x < TexWidth; x++)
                {
                    bool post = x < PostPixels || x >= TexWidth - PostPixels;
                    bool lintel = y >= TexHeight - 8;
                    if (!post && !lintel) continue;
                    bool outline = x == 0 || x == TexWidth - 1 || y == TexHeight - 1 || (post && (x == PostPixels - 1 || x == TexWidth - PostPixels));
                    bool bolt = post && y % 14 == 4 && x > 0 && x < TexWidth - 1;
                    px[y * TexWidth + x] = bolt || outline ? trim : (x == 0 || x == TexWidth - 1 ? edge : metal);
                }
            }
            return SpriteFrom(px, "BossDoorFrame");
        }

        private static Sprite MakeShutter()
        {
            var px = new Color32[TexWidth * TexHeight];
            var dark = new Color32(24, 36, 72, 255);
            var plate = new Color32(62, 104, 176, 255);
            var shine = new Color32(186, 214, 242, 255);
            var warn = new Color32(232, 186, 48, 255);
            for (int y = 0; y < TexHeight; y++)
            {
                for (int x = PostPixels; x < TexWidth - PostPixels; x++)
                {
                    bool seam = y % 16 == 0;
                    bool highlight = x == PostPixels + 2;
                    bool stripe = y > TexHeight / 2 - 3 && y < TexHeight / 2 + 3;
                    px[y * TexWidth + x] = stripe ? warn : seam ? dark : highlight ? shine : plate;
                }
            }
            return SpriteFrom(px, "BossDoorShutter");
        }

        private static Sprite SpriteFrom(Color32[] pixels, string spriteName)
        {
            var tex = new Texture2D(TexWidth, TexHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = spriteName,
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixels32(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, TexWidth, TexHeight), new Vector2(0.5f, 0f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = sealed ? new Color(1f, 0.3f, 0.25f, 0.9f) : new Color(0.35f, 0.7f, 1f, 0.9f);
            var center = transform.position + Vector3.up * passageHeight * 0.5f;
            Gizmos.DrawWireCube(center, new Vector3(Width * VisualScale, passageHeight, 0f));
            Gizmos.DrawLine(transform.position, transform.position + Vector3.right * enterDirection * 1.2f);
        }
    }
}
