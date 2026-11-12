using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gstarr.Game
{
    /// <summary>
    /// 플레이 중 에이든 대사. 말풍선은 dialog, 그 위 초상은 playerdialog.
    /// 시간이 멈춘 동안에도 한 글자씩 나온다.
    /// </summary>
    public sealed class StoryBubble : MonoBehaviour
    {
        private const float CharSeconds = 0.045f;
        private const float LineHoldBase = 1.15f;
        private const float LineHoldPerChar = 0.02f;

        private Text bodyText;
        private Text promptText;

        public static StoryBubble Open()
        {
            var go = new GameObject("StoryBubble");
            var bubble = go.AddComponent<StoryBubble>();
            bubble.Build();
            return bubble;
        }

        public void Close()
        {
            if (this == null) return;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        public IEnumerator Hold(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }

        /// <param name="hold">다 나온 뒤 잠깐 기다린다. 누르면 바로 넘긴다.</param>
        public IEnumerator TypeLine(string line, bool hold)
        {
            line = line ?? "";
            if (promptText != null) promptText.text = "A / ENTER  다음";
            float t = 0f;
            int shown = 0;
            bodyText.text = "";
            yield return null;

            while (shown < line.Length)
            {
                if (AdvancePressed())
                {
                    shown = line.Length;
                    break;
                }

                t += Time.unscaledDeltaTime;
                int next = Mathf.Min(line.Length, Mathf.FloorToInt(t / CharSeconds));
                if (next != shown)
                {
                    shown = next;
                    bodyText.text = line.Substring(0, shown);
                }

                yield return null;
            }

            bodyText.text = line;
            if (!hold) yield break;

            float wait = LineHoldBase + line.Length * LineHoldPerChar;
            for (float h = 0f; h < wait; h += Time.unscaledDeltaTime)
            {
                if (AdvancePressed()) yield break;
                yield return null;
            }
        }

        private static bool AdvancePressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame
                || keyboard.zKey.wasPressedThisFrame)) return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            var pads = Gamepad.all;
            for (int i = 0; i < pads.Count; i++)
                if (pads[i] != null && (pads[i].buttonEast.wasPressedThisFrame || pads[i].buttonSouth.wasPressedThisFrame))
                    return true;
            return false;
        }

        private void Build()
        {
            const float dialogWidth = 1500f;
            const float dialogHeight = 325f;
            const float dialogBottom = 36f;

            var canvas = UIFactory.CreateCanvas(transform, "StoryBubbleCanvas", 250);
            var root = (RectTransform)canvas.transform;

            var dialog = UIFactory.CreateImage("Dialog", root, Color.white);
            var dialogSprite = LoadDialogArt("dialog.png", false);
            if (dialogSprite != null)
            {
                dialog.sprite = dialogSprite;
                dialog.preserveAspect = true;
            }
            else
            {
                dialog.color = new Color(0.22f, 0.16f, 0.32f, 0.92f);
            }

            UIFactory.Place(dialog.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, dialogBottom), new Vector2(dialogWidth, dialogHeight));

            var portraitSprite = LoadDialogArt("playerdialog.png", true);
            if (portraitSprite != null)
            {
                var portrait = UIFactory.CreateImage("Portrait", root, Color.white);
                portrait.sprite = portraitSprite;
                portrait.preserveAspect = true;
                const float portraitHeight = 300f;
                float aspect = portraitSprite.rect.width / Mathf.Max(1f, portraitSprite.rect.height);
                float portraitWidth = portraitHeight * aspect;
                float namePlateCenterX = -dialogWidth * 0.5f + (125f / 770f) * dialogWidth;
                float dialogTop = dialogBottom + dialogHeight;
                UIFactory.Place(
                    portrait.rectTransform,
                    new Vector2(0.5f, 0f),
                    new Vector2(namePlateCenterX, dialogTop - 12f),
                    new Vector2(portraitWidth, portraitHeight));
            }

            var name = UIFactory.CreateText("Name", root, StageStory.Speaker, 26, TextAnchor.MiddleCenter, FontStyle.Bold);
            float nameX = -dialogWidth * 0.5f + (125f / 770f) * dialogWidth;
            float nameY = dialogBottom + dialogHeight - (16f / 167f) * dialogHeight - 22f;
            UIFactory.Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(nameX, nameY), new Vector2(250f, 44f));
            name.color = new Color(0.98f, 0.96f, 1f);
            var nameOutline = name.GetComponent<Outline>();
            if (nameOutline != null)
            {
                nameOutline.effectColor = new Color(0.18f, 0.08f, 0.28f, 0.95f);
                nameOutline.effectDistance = new Vector2(1.4f, -1.4f);
            }

            bodyText = UIFactory.CreateText("Body", root, "", 32, TextAnchor.MiddleLeft, FontStyle.Normal);
            UIFactory.Place(bodyText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, dialogBottom + 78f), new Vector2(1240f, 156f));
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Truncate;
            promptText = UIFactory.CreateText("Prompt", root, "", 18, TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.Place(promptText.rectTransform, new Vector2(0.5f, 0f), new Vector2(480f, dialogBottom + 22f), new Vector2(360f, 28f));
            promptText.color = new Color(1f, 1f, 1f, 0.72f);
        }

        private static Sprite LoadDialogArt(string fileName, bool cropToOpaque)
        {
            string full = Path.Combine(Application.dataPath, "arts", "UI", fileName);
            if (!File.Exists(full)) return null;
            byte[] bytes;
            try { bytes = File.ReadAllBytes(full); }
            catch { return null; }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes, false))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.name = Path.GetFileNameWithoutExtension(fileName);

            var rect = new Rect(0f, 0f, texture.width, texture.height);
            var pivot = new Vector2(0.5f, 0.5f);
            if (cropToOpaque)
            {
                var pixels = texture.GetPixels32();
                int w = texture.width;
                int h = texture.height;
                int minX = w;
                int minY = h;
                int maxX = -1;
                int maxY = -1;
                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        if (pixels[row + x].a <= 12) continue;
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }

                if (maxX >= minX && maxY >= minY)
                {
                    const int pad = 2;
                    minX = Mathf.Max(0, minX - pad);
                    minY = Mathf.Max(0, minY - pad);
                    maxX = Mathf.Min(w - 1, maxX + pad);
                    maxY = Mathf.Min(h - 1, maxY + pad);
                    rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                    pivot = new Vector2(0.5f, 0f);
                }
            }

            return Sprite.Create(texture, rect, pivot, 100f);
        }
    }
}
