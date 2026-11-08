using Gstarr.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Gstarr.World
{
    /// <summary>
    /// 첫 방 벽에 붙는 키 묶음 그림. 이동 스틱, 십자키, ABXY. 키보드는 없다.
    /// </summary>
    public static class RoomPoster
    {
        public enum Part
        {
            Move,
            Dpad,
            Buttons
        }

        private static readonly Color Ink = new Color(0.1f, 0.07f, 0.05f, 1f);
        private static readonly Color Cream = new Color(0.96f, 0.91f, 0.74f, 1f);
        private static readonly Color FaceX = new Color(0.35f, 0.85f, 1f, 1f);
        private static readonly Color FaceA = new Color(1f, 0.55f, 0.2f, 1f);
        private static readonly Color FaceB = new Color(0.95f, 0.28f, 0.28f, 1f);
        private static readonly Color FaceY = new Color(0.4f, 0.9f, 0.42f, 1f);
        private static readonly Sprite[] cache = new Sprite[3];

        private struct Sheet
        {
            public int Width;
            public int Height;
            public float WorldWidth;
            public string[] Words;
            public Vector2[] At;
        }

        public static void Hang(Transform parent, Vector2 world, float degrees, Part part)
        {
            var sheet = Describe(part);
            var root = new GameObject("ControllerPart");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(world.x, world.y, 0f);
            root.transform.rotation = Quaternion.Euler(0f, 0f, degrees);

            var shadow = Picture("Shadow", root.transform, part, 1);
            shadow.color = new Color(0.08f, 0.05f, 0.03f, 0.4f);
            shadow.transform.localPosition = new Vector3(0.05f, -0.05f, 0f);
            Picture("Chart", root.transform, part, 2);

            float unit = sheet.WorldWidth / sheet.Width;
            var canvasGo = new GameObject("Words", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 3;
            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(sheet.Width, sheet.Height);
            canvasRt.localScale = Vector3.one * unit;
            canvasRt.localPosition = Vector3.zero;

            for (int i = 0; i < sheet.Words.Length; i++)
                Word(canvasRt, sheet, sheet.Words[i], sheet.At[i]);

            root.AddComponent<SignBoard>().Arm(part, unit * sheet.Height);
        }

        private static SpriteRenderer Picture(string name, Transform parent, Part part, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteFor(part);
            renderer.sortingOrder = order;
            return renderer;
        }

        private static void Word(RectTransform root, Sheet sheet, string value, Vector2 pixel)
        {
            var text = UIFactory.CreateText(value, root, value, 16, TextAnchor.MiddleCenter, FontStyle.Bold);
            var outline = text.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            text.color = Ink;
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Mathf.Max(40f, value.Length * 18f), 18f);
            rt.anchoredPosition = new Vector2(pixel.x - sheet.Width * 0.5f, pixel.y - sheet.Height * 0.5f);
        }

        private static Sheet Describe(Part part)
        {
            switch (part)
            {
                case Part.Move:
                    return new Sheet { Width = 96, Height = 112, WorldWidth = 2.15f, Words = new[] { "이동" }, At = new[] { new Vector2(48f, 20f) } };
                case Part.Dpad:
                    return new Sheet
                    {
                        Width = 140, Height = 124, WorldWidth = 2.9f,
                        Words = new[] { "십자", "전투", "효과" },
                        At = new[] { new Vector2(70f, 16f), new Vector2(70f, 110f), new Vector2(112f, 40f) }
                    };
                default:
                    return new Sheet
                    {
                        Width = 172, Height = 150, WorldWidth = 3.4f,
                        Words = new[] { "아이템", "대시", "공격", "점프" },
                        At = new[] { new Vector2(86f, 134f), new Vector2(40f, 52f), new Vector2(132f, 52f), new Vector2(86f, 22f) }
                    };
            }
        }

        private static Sprite SpriteFor(Part part)
        {
            int index = (int)part;
            if (cache[index] != null) return cache[index];
            var sheet = Describe(part);
            var tex = new Texture2D(sheet.Width, sheet.Height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var clear = new Color[sheet.Width * sheet.Height];
            tex.SetPixels(clear);
            Card(tex);
            switch (part)
            {
                case Part.Move: DrawMove(tex); break;
                case Part.Dpad: DrawDpad(tex); break;
                default: DrawButtons(tex); break;
            }
            tex.Apply();
            cache[index] = Sprite.Create(tex, new Rect(0f, 0f, sheet.Width, sheet.Height), new Vector2(0.5f, 0.5f), sheet.Width / sheet.WorldWidth);
            return cache[index];
        }

        private static void DrawMove(Texture2D tex)
        {
            Disc(tex, 48, 64, 18, new Color(0.22f, 0.23f, 0.28f, 1f));
            Disc(tex, 48, 64, 8, new Color(0.55f, 0.56f, 0.62f, 1f));
            Arrow(tex, 48, 88, 0);
            Arrow(tex, 72, 64, 1);
            Arrow(tex, 48, 40, 2);
            Arrow(tex, 24, 64, 3);
        }

        private static void DrawDpad(Texture2D tex)
        {
            var combat = new Color(0.95f, 0.72f, 0.28f, 1f);
            var effect = new Color(0.72f, 0.55f, 1f, 1f);
            FillRect(tex, 62, 34, 78, 94, combat);
            FillRect(tex, 24, 58, 116, 74, effect);
            Disc(tex, 70, 66, 5, Ink);
        }

        private static void DrawButtons(Texture2D tex)
        {
            Ring(tex, 132, 96, 20, Ink);
            Face(tex, 86, 106, FaceX, "X");
            Face(tex, 40, 94, FaceY, "Y");
            Face(tex, 132, 96, FaceA, "A");
            Face(tex, 86, 58, FaceB, "B");
        }

        private static void Card(Texture2D tex)
        {
            int width = tex.width;
            int height = tex.height;
            var edge = new Color(0.86f, 0.72f, 0.42f, 1f);
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                int bottom = 2 + (int)(Mathf.Sin(ny * 12f) * 1f);
                int top = height - 3 - (int)(Mathf.Sin(ny * 9f) * 1f);
                for (int x = 0; x < width; x++)
                {
                    int left = 2 + (int)(Mathf.Sin(ny * 14f) * 1f);
                    int right = width - 3 - (int)(Mathf.Sin(ny * 11f + 1f) * 1f);
                    if (x < left || x > right || y < bottom || y > top) continue;
                    bool border = x < left + 3 || x > right - 3 || y < bottom + 3 || y > top - 3;
                    tex.SetPixel(x, y, border ? Ink : Color.Lerp(Cream, edge, ny * 0.22f));
                }
            }

            Nail(tex, 12, height - 10);
            Nail(tex, width - 14, height - 10);
        }

        private static void Face(Texture2D tex, int cx, int cy, Color color, string letter)
        {
            Disc(tex, cx, cy, 14, Ink);
            Disc(tex, cx, cy, 12, color);
            Glyph(tex, cx, cy, letter, Ink);
        }

        private static void Arrow(Texture2D tex, int cx, int cy, int direction)
        {
            for (int y = -5; y <= 5; y++)
            {
                for (int x = -5; x <= 5; x++)
                {
                    int px = x;
                    int py = y;
                    if (direction == 1) { px = y; py = -x; }
                    else if (direction == 2) { py = -y; }
                    else if (direction == 3) { px = -y; py = x; }
                    if (py > -3 && Mathf.Abs(px) <= 5 - py) Plot(tex, cx + px, cy + py, Ink);
                }
            }
        }

        private static void Glyph(Texture2D tex, int cx, int cy, string letter, Color color)
        {
            string[] rows = letter switch
            {
                "X" => new[] { "#...#", ".#.#.", "..#..", ".#.#.", "#...#" },
                "Y" => new[] { "#...#", ".#.#.", "..#..", "..#..", "..#.." },
                "A" => new[] { ".###.", "#...#", "#####", "#...#", "#...#" },
                "B" => new[] { "####.", "#...#", "####.", "#...#", "####." },
                "L" => new[] { "#....", "#....", "#....", "#....", "#####" },
                "R" => new[] { "####.", "#...#", "####.", "#.#..", "#..#." },
                _ => new[] { ".....", ".....", ".....", ".....", "....." }
            };
            int h = rows.Length;
            int w = rows[0].Length;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (rows[h - 1 - y][x] != '#') continue;
                    Plot(tex, cx - w / 2 + x, cy - h / 2 + y, color);
                    Plot(tex, cx - w / 2 + x + 1, cy - h / 2 + y, color);
                }
            }
        }

        private static void Ring(Texture2D tex, int cx, int cy, int radius, Color color)
        {
            int outer = radius * radius;
            int inner = (radius - 3) * (radius - 3);
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                {
                    int d = x * x + y * y;
                    if (d <= outer && d >= inner) Plot(tex, cx + x, cy + y, color);
                }
        }

        private static void Disc(Texture2D tex, int cx, int cy, int radius, Color color)
        {
            int r2 = radius * radius;
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (x * x + y * y <= r2) Plot(tex, cx + x, cy + y, color);
        }

        private static void FillRect(Texture2D tex, int x0, int y0, int x1, int y1, Color color)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Plot(tex, x, y, color);
        }

        private static void Nail(Texture2D tex, int cx, int cy)
        {
            Disc(tex, cx, cy, 3, new Color(0.45f, 0.36f, 0.26f, 1f));
            Disc(tex, cx, cy, 1, new Color(0.2f, 0.14f, 0.1f, 1f));
        }

        private static void Plot(Texture2D tex, int x, int y, Color color)
        {
            if ((uint)x >= tex.width || (uint)y >= tex.height) return;
            tex.SetPixel(x, y, color);
        }
    }
}
