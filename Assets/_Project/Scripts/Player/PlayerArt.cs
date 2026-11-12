using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Gstarr.Player
{
    /// <summary>플레이어 파츠와 무기를 파일에서 읽어 발·손 피벗으로 맞춘다.</summary>
    public static class PlayerArt
    {
        public static Sprite LoadSheet(string assetPath, float pivotX, float pivotY, float pixelsPerUnit, bool flipX = false, bool openStance = false)
        {
            var texture = Read(assetPath);
            if (texture == null || pixelsPerUnit <= 0f) return null;
            if (flipX)
            {
                texture = FlipHorizontal(texture);
                pivotX = texture.width - pivotX;
            }
            if (openStance) OpenStance(texture, pivotY);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(pivotX / texture.width, pivotY / texture.height),
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = texture.name;
            return sprite;
        }

        /// <summary>
        /// 뒤집힌 부츠의 종아리를, 지금 안쪽으로 기댄 각도만큼 바깥으로 민다.
        /// 신발은 통째로 옮기기만 해서 발바닥 기울기는 그대로다.
        /// </summary>
        private static void OpenStance(Texture2D texture, float pivotY)
        {
            int w = texture.width;
            int h = texture.height;
            var src = texture.GetPixels32();
            int yMin = h;
            int yMax = -1;
            var rowMin = new int[h];
            var rowMax = new int[h];
            var rowOn = new bool[h];
            for (int i = 0; i < src.Length; i++)
            {
                if (src[i].a <= 40) continue;
                int x = i % w;
                int y = i / w;
                if (!rowOn[y])
                {
                    rowOn[y] = true;
                    rowMin[y] = rowMax[y] = x;
                }
                else
                {
                    if (x < rowMin[y]) rowMin[y] = x;
                    if (x > rowMax[y]) rowMax[y] = x;
                }
                if (y < yMin) yMin = y;
                if (y > yMax) yMax = y;
            }

            if (yMax < yMin) return;
            var ys = new List<int>();
            for (int y = yMax; y >= yMin; y--)
                if (rowOn[y]) ys.Add(y);
            if (ys.Count < 8) return;

            int mid = ys.Count / 2;
            var upper = new int[mid];
            for (int i = 0; i < mid; i++) upper[i] = rowMax[ys[i]] - rowMin[ys[i]];
            System.Array.Sort(upper);
            int shaft = upper[mid / 2];
            int ankle = ys[Mathf.Min(ys.Count - 1, (int)(ys.Count * 0.7f))];
            for (int i = mid; i < ys.Count; i++)
            {
                if (rowMax[ys[i]] - rowMin[ys[i]] <= shaft + 28) continue;
                ankle = ys[i];
                break;
            }

            var shaftRows = new List<int>();
            for (int i = 0; i < ys.Count; i++)
                if (ys[i] > ankle + 8) shaftRows.Add(ys[i]);
            if (shaftRows.Count < 8) return;

            int y0 = shaftRows[Mathf.FloorToInt(shaftRows.Count * 0.15f)];
            int y1 = shaftRows[Mathf.FloorToInt(shaftRows.Count * 0.9f)];
            float cx0 = (rowMin[y0] + rowMax[y0]) * 0.5f;
            float cx1 = (rowMin[y1] + rowMax[y1]) * 0.5f;
            float lean = Mathf.Atan2(cx1 - cx0, y0 - y1) * Mathf.Rad2Deg;
            float k = Mathf.Tan(-2f * lean * Mathf.Deg2Rad);
            float boundary = k * (pivotY - ankle);

            var dst = new Color32[src.Length];
            for (int y = 0; y < h; y++)
            {
                float shift = y > ankle ? k * (pivotY - y) : boundary;
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int sx = Mathf.RoundToInt(x - shift);
                    if ((uint)sx >= (uint)w) continue;
                    dst[row + x] = src[row + sx];
                }
            }

            texture.SetPixels32(dst);
            texture.Apply();
        }

        /// <summary>여백을 자르고, pivot01 은 잘린 그림 안에서의 손잡이 위치다. worldLength 는 긴 변.</summary>
        public static Sprite LoadTrimmed(string assetPath, Vector2 pivot01, float worldLength)
        {
            var texture = Read(assetPath);
            if (texture == null) return null;
            var pixels = texture.GetPixels32();
            int w = texture.width;
            int h = texture.height;
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a <= 20) continue;
                int x = i % w;
                int y = i / w;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            if (maxX < minX) return null;
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            float ppu = Mathf.Max(width, height) / Mathf.Max(0.2f, worldLength);
            var sprite = Sprite.Create(
                texture,
                new Rect(minX, minY, width, height),
                new Vector2(Mathf.Clamp01(pivot01.x), Mathf.Clamp01(pivot01.y)),
                ppu,
                0,
                SpriteMeshType.FullRect);
            sprite.name = texture.name;
            return sprite;
        }

        /// <summary>보이는 팔의 위쪽 중심과 아래쪽 중심. x는 왼쪽, y는 아래가 0.</summary>
        public static bool OpaqueEnds(string assetPath, out Vector2 top, out Vector2 bottom)
        {
            top = bottom = Vector2.zero;
            var texture = Read(assetPath);
            if (texture == null) return false;
            var pixels = texture.GetPixels32();
            int w = texture.width;
            int h = texture.height;
            int minY = h;
            int maxY = -1;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a <= 40) continue;
                int y = i / w;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            Object.Destroy(texture);
            if (maxY < minY) return false;
            int band = Mathf.Max(2, Mathf.RoundToInt((maxY - minY) * 0.12f));
            top = BandCenter(pixels, w, maxY - band + 1, maxY);
            bottom = BandCenter(pixels, w, minY, minY + band - 1);
            return true;
        }

        private static Vector2 BandCenter(Color32[] pixels, int w, int y0, int y1)
        {
            float sx = 0f;
            float sy = 0f;
            int n = 0;
            for (int y = y0; y <= y1; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (pixels[row + x].a <= 40) continue;
                    sx += x;
                    sy += y;
                    n++;
                }
            }

            return n == 0 ? Vector2.zero : new Vector2(sx / n, sy / n);
        }

        private static Texture2D FlipHorizontal(Texture2D source)
        {
            int w = source.width;
            int h = source.height;
            var pixels = source.GetPixels32();
            var flipped = new Color32[pixels.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    flipped[x + y * w] = pixels[(w - 1 - x) + y * w];
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels32(flipped);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.name = source.name;
            Object.Destroy(source);
            return texture;
        }

        private static Texture2D Read(string assetPath)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!File.Exists(full)) return null;
            byte[] bytes;
            try { bytes = File.ReadAllBytes(full); }
            catch { return null; }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.name = Path.GetFileNameWithoutExtension(assetPath);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            if (texture.LoadImage(bytes, false)) return texture;
            Object.Destroy(texture);
            return null;
        }
    }
}
