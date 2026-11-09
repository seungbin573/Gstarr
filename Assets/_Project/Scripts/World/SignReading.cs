using System.Globalization;
using Gstarr.Game;
using Gstarr.Rhythm;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gstarr.World
{
    /// <summary>조작 설명. 왼쪽은 예시 그림, 오른쪽은 그 그림이 말하는 조작. 시간이 멈춘 채로 연다.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SignDetail : MonoBehaviour
    {
        private enum Pic
        {
            Move,
            Combat,
            Effect,
            Jump,
            Dash,
            Attack,
            Item,
            Weapons,
            Glory
        }

        public enum Lesson
        {
            Move,
            Jump,
            Dash,
            Items,
            Combat,
            Glory
        }

        private struct Page
        {
            public string Title;
            public string Body;
            public string Caption;
            public Pic Picture;
        }

        private static SignDetail instance;
        private static Sprite buttonSprite;
        private static readonly Sprite[] pictures = new Sprite[9];

        private GameObject sheet;
        private Image example;
        private Text title;
        private Text body;
        private Text caption;
        private Text pageMark;
        private Text nextLabel;
        private Text prevLabel;
        private Page[] pages;
        private int page;
        private int bodySize = 26;
        private bool visible;
        private bool waitingRelease;
        private bool releaseSeen;
        private bool lesson;
        private int openedFrame;
        private Texture2D clipTexture;
        private Sprite[] clipFrames;
        private float[] clipDelays;
        private int clipFrame;
        private float clipTime;
        private string clipName;

        public static bool IsOpen => instance != null && (instance.visible || instance.waitingRelease);

        /// <summary>결과 화면으로 넘어갈 때 표지판 설명을 남기지 않는다.</summary>
        public static void Dismiss()
        {
            if (instance == null) return;
            instance.visible = false;
            instance.waitingRelease = false;
            instance.lesson = false;
            RhythmConductor.Instance?.DuckForStory(false);
            instance.StopClip();
            if (instance.sheet != null) instance.sheet.SetActive(false);
        }

        public static void Ensure()
        {
            if (instance != null) return;
            var host = new GameObject("SignDetail");
            instance = host.AddComponent<SignDetail>();
            instance.Build();
        }

        /// <summary>처음 만나는 상황에 시간을 멈추고 그 조작만 보여 준다.</summary>
        public static bool OpenLesson(Lesson lesson)
        {
            Ensure();
            if (instance.visible || instance.waitingRelease) return false;
            instance.pages = LessonPages(lesson);
            instance.page = 0;
            instance.bodySize = 32;
            instance.lesson = true;
            instance.Show();
            instance.Present();
            GamePause.HaltMotion();
            return true;
        }

        public static bool ConfirmPressed()
        {
            var pad = Gamepad.current;
            if (pad != null && pad.buttonEast.wasPressedThisFrame) return true;
            var key = Keyboard.current;
            return key != null && key.cKey.wasPressedThisFrame;
        }

        public static Sprite ButtonSprite()
        {
            if (buttonSprite != null) return buttonSprite;
            var plate = new Plate(40, 40, Color.clear);
            plate.Face(20, 20, 16, new Color(1f, 0.55f, 0.2f, 1f), "A");
            buttonSprite = plate.Bake(40f);
            return buttonSprite;
        }

        private void Update()
        {
            if (visible) AdvanceClip();

            if (waitingRelease)
            {
                if (Held())
                {
                    releaseSeen = false;
                    return;
                }
                if (!releaseSeen)
                {
                    releaseSeen = true;
                    return;
                }
                waitingRelease = false;
                ResumePlay();
                return;
            }

            if (!visible || Time.frameCount == openedFrame) return;
            if (ConfirmPressed())
            {
                if (page < pages.Length - 1) Turn(1);
                else Close();
                return;
            }
            if (!lesson && EscapePressed())
            {
                Close();
                return;
            }
            if (BackPressed())
            {
                if (page > 0) Turn(-1);
                else if (!lesson) Close();
            }
        }

        private void Turn(int step)
        {
            page = Mathf.Clamp(page + step, 0, pages.Length - 1);
            Show();
            GameAudio.Play(GameAudio.Id.Ui, 0.4f);
        }

        private void Close()
        {
            visible = false;
            sheet.SetActive(false);
            StopClip();
            waitingRelease = true;
            releaseSeen = false;
            GameAudio.Play(GameAudio.Id.Ui, 0.4f);
        }

        private static bool BackPressed()
        {
            var pad = Gamepad.current;
            if (pad != null && pad.buttonSouth.wasPressedThisFrame) return true;
            var key = Keyboard.current;
            return key != null && key.zKey.wasPressedThisFrame;
        }

        private static bool EscapePressed()
        {
            var key = Keyboard.current;
            return key != null && key.escapeKey.wasPressedThisFrame;
        }

        private static bool Held()
        {
            var pad = Gamepad.current;
            if (pad != null && (pad.buttonEast.isPressed || pad.buttonSouth.isPressed)) return true;
            var key = Keyboard.current;
            return key != null && (key.cKey.isPressed || key.zKey.isPressed || key.escapeKey.isPressed);
        }

        private void PausePlay()
        {
            RhythmConductor.Instance?.DuckForStory(true);
            GamePause.SetPaused(true);
        }

        private void ResumePlay()
        {
            RhythmConductor.Instance?.DuckForStory(false);
            GamePause.SetPaused(false);
        }

        private void Build()
        {
            var canvas = UIFactory.CreateCanvas(transform, "Sheet", 200);
            sheet = canvas.gameObject;

            var dim = UIFactory.CreateImage("Dim", sheet.transform, new Color(0.02f, 0.02f, 0.04f, 0.72f));
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = Vector2.zero;
            dim.rectTransform.offsetMax = Vector2.zero;

            var ink = new Color(0.1f, 0.07f, 0.05f, 1f);
            var back = UIFactory.CreateImage("Back", sheet.transform, ink);
            UIFactory.Place(back.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1236f, 676f));
            var card = UIFactory.CreateImage("Card", sheet.transform, new Color(0.96f, 0.91f, 0.74f, 1f));
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1220f, 660f));

            example = UIFactory.CreateImage("Example", card.transform, Color.white);
            example.preserveAspect = true;
            UIFactory.Place(example.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-310f, 28f), new Vector2(520f, 400f));

            caption = UIFactory.CreateText("Caption", card.transform, "", 22, TextAnchor.MiddleCenter, FontStyle.Bold);
            caption.color = ink;
            caption.GetComponent<Outline>().enabled = false;
            UIFactory.Place(caption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-310f, -230f), new Vector2(520f, 40f));

            title = UIFactory.CreateText("Title", card.transform, "", 40, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            title.color = new Color(0.86f, 0.32f, 0.1f, 1f);
            title.GetComponent<Outline>().enabled = false;
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(250f, 248f), new Vector2(560f, 60f));

            body = UIFactory.CreateText("Body", card.transform, "", 26, TextAnchor.UpperLeft, FontStyle.Bold);
            body.color = ink;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.lineSpacing = 1.08f;
            body.GetComponent<Outline>().enabled = false;
            UIFactory.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(250f, 4f), new Vector2(560f, 360f));

            pageMark = UIFactory.CreateText("Page", card.transform, "", 22, TextAnchor.MiddleCenter, FontStyle.Bold);
            pageMark.color = ink;
            pageMark.GetComponent<Outline>().enabled = false;
            UIFactory.Place(pageMark.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -276f), new Vector2(120f, 40f));

            prevLabel = UIFactory.CreateText("Prev", card.transform, "", 26, TextAnchor.MiddleRight, FontStyle.Bold);
            prevLabel.color = ink;
            prevLabel.GetComponent<Outline>().enabled = false;
            UIFactory.Place(prevLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-300f, -276f), new Vector2(180f, 40f));

            var closeButton = UIFactory.CreateImage("CloseButton", card.transform, Color.white);
            closeButton.sprite = ButtonSprite();
            closeButton.preserveAspect = true;
            UIFactory.Place(closeButton.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(150f, -276f), new Vector2(48f, 48f));
            nextLabel = UIFactory.CreateText("Next", card.transform, "닫기", 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            nextLabel.color = ink;
            nextLabel.GetComponent<Outline>().enabled = false;
            UIFactory.Place(nextLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(250f, -276f), new Vector2(140f, 40f));

            sheet.SetActive(false);
        }

        private void Present()
        {
            sheet.SetActive(true);
            visible = true;
            openedFrame = Time.frameCount;
            PausePlay();
            GameAudio.Play(GameAudio.Id.Ui, 0.55f);
        }

        private void Show()
        {
            var leaf = pages[page];
            title.text = leaf.Title;
            body.text = leaf.Body;
            body.fontSize = bodySize;
            caption.text = leaf.Caption;
            example.sprite = ExampleSprite(leaf.Picture);
            bool many = pages.Length > 1;
            pageMark.text = many ? (page + 1) + " / " + pages.Length : "";
            prevLabel.text = page > 0 ? "B  이전" : "";
            nextLabel.text = page < pages.Length - 1 ? "다음" : "닫기";
        }

        private static Page[] LessonPages(Lesson lesson)
        {
            switch (lesson)
            {
                case Lesson.Move:
                    return new[]
                    {
                        Leaf("이동", "왼쪽 스틱을 좌우로 기울이면 그쪽으로 걷는다.\n키보드는 왼쪽, 오른쪽 방향키.", "기울인 쪽으로 걷는다", Pic.Move)
                    };
                case Lesson.Jump:
                    return new[]
                    {
                        Leaf("점프", "B로 뛴다. 키보드는 Z.\n공중에서 한 번 더 뛸 수 있다.", "B 점프", Pic.Jump)
                    };
                case Lesson.Dash:
                    return new[]
                    {
                        Leaf("대시", "Y로 대시한다. 키보드는 X.\n스틱이 가리키는 쪽으로 파고든다.\n공중에서는 한 번이다.", "Y 대시", Pic.Dash)
                    };
                case Lesson.Items:
                    return new[]
                    {
                        Leaf("아이템 고르기", "위·아래는 전투 아이템, 왼쪽·오른쪽은 회복 아이템이다.\n위와 왼쪽이 이전, 아래와 오른쪽이 다음이다.\n키보드는 전투 R·F, 회복 T·G.\n고른 아이템만 머리 위에 크게 뜬다. 공격은 위아래, 포션은 좌우로 넘어간다.", "위아래 전투, 좌우 회복", Pic.Combat),
                        Leaf("아이템 사용", "X로 고른 아이템을 쓴다. 키보드는 V.\n쓰는 것도 노트가 링에 닿을 때다.", "링에 닿을 때 X", Pic.Item)
                    };
                case Lesson.Glory:
                    return new[]
                    {
                        Leaf(
                            "글로리 킬",
                            "적이 노랗게 비틀거린다.\n가까이서 A를 누른다. 키보드는 C.\n노트 네 개가 가운데로 온다.\nX가 위, A가 오른쪽.\nB가 아래, Y가 왼쪽.\n네 번 맞추면 끝난다.\n틀리면 풀리고, 늦으면 그 노트만 다시 온다.",
                            "가까이서 A, 방향을 맞춘다",
                            Pic.Glory)
                    };
                default:
                    string miss = GameSettings.RhythmAssist
                        ? "놓쳐도 약하게 나간다."
                        : "놓치면 공격이 나가지 않는다.";
                    return new[]
                    {
                        Leaf(
                            "공격",
                            "A로 공격한다. 키보드는 C.\n화면 아래 노트가 링에 닿을 때 누른다.\n" + miss,
                            "링에 닿을 때 A",
                            Pic.Attack),
                        Leaf(
                            "무기 교체",
                            "L은 이전 무기, R은 다음 무기다.\n키보드는 Q와 E.\n검, 권총, 새총이 돌아간다.",
                            "L 이전, R 다음",
                            Pic.Weapons)
                    };
            }
        }

        private static Page Leaf(string title, string body, string caption, Pic picture)
        {
            return new Page { Title = title, Body = body, Caption = caption, Picture = picture };
        }

        private void OnDestroy() => StopClip();

        private Sprite ExampleSprite(Pic pic)
        {
            string name = ClipFile(pic);
            if (string.IsNullOrEmpty(name))
            {
                StopClip();
                return Picture(pic);
            }

            if (clipName == name && clipFrames != null && clipFrames.Length > 0)
            {
                clipFrame = 0;
                clipTime = 0f;
                return clipFrames[0];
            }

            if (!LoadClip(name))
            {
                StopClip();
                return Picture(pic);
            }

            return clipFrames[0];
        }

        private static string ClipFile(Pic pic)
        {
            switch (pic)
            {
                case Pic.Move: return "moving";
                case Pic.Jump: return "jump";
                case Pic.Dash: return "dash";
                case Pic.Combat: return "itemChange";
                case Pic.Item: return "itemUse";
                case Pic.Glory: return "glorykill";
                case Pic.Attack: return "weaponAttack";
                case Pic.Weapons: return "weaponChange";
                default: return null;
            }
        }

        private bool LoadClip(string name)
        {
            StopClip();
            var texture = Resources.Load<Texture2D>("TutorialGifs/" + name);
            var meta = Resources.Load<TextAsset>("TutorialGifs/" + name + "_frames");
            if (texture == null || meta == null)
            {
                if (texture != null) Resources.UnloadAsset(texture);
                return false;
            }

            var lines = meta.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            var head = lines.Length > 0 ? lines[0].Split(' ') : new string[0];
            if (lines.Length < 2 || head.Length < 7
                || !int.TryParse(head[0], out int frames) || !int.TryParse(head[1], out int cols)
                || !int.TryParse(head[2], out int cellW) || !int.TryParse(head[3], out int cellH)
                || !int.TryParse(head[4], out int pad) || !int.TryParse(head[5], out int atlasW)
                || !int.TryParse(head[6], out int atlasH) || frames <= 0 || cols <= 0)
            {
                Resources.UnloadAsset(texture);
                return false;
            }

            float scaleX = atlasW > 0 ? texture.width / (float)atlasW : 1f;
            float scaleY = atlasH > 0 ? texture.height / (float)atlasH : 1f;
            var delays = lines[1].Split(',');
            clipFrames = new Sprite[frames];
            clipDelays = new float[frames];
            int strideX = cellW + pad * 2;
            int strideY = cellH + pad * 2;
            for (int i = 0; i < frames; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float x = (col * strideX + pad) * scaleX;
                float y = (atlasH - (row + 1) * strideY + pad) * scaleY;
                var rect = new Rect(x, y, cellW * scaleX, cellH * scaleY);
                clipFrames[i] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                float ms = 40f;
                if (i < delays.Length
                    && float.TryParse(delays[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                    && parsed > 0f)
                    ms = parsed;
                clipDelays[i] = ms / 1000f;
            }

            clipTexture = texture;
            clipName = name;
            clipFrame = 0;
            clipTime = 0f;
            return true;
        }

        private void AdvanceClip()
        {
            if (clipFrames == null || clipFrames.Length == 0 || example == null) return;
            clipTime += Time.unscaledDeltaTime;
            float wait = clipDelays != null && clipFrame < clipDelays.Length ? clipDelays[clipFrame] : 0.04f;
            if (clipTime < wait) return;
            clipTime -= wait;
            clipFrame = (clipFrame + 1) % clipFrames.Length;
            if (clipFrames[clipFrame] != null) example.sprite = clipFrames[clipFrame];
        }

        private void StopClip()
        {
            if (clipFrames != null)
            {
                for (int i = 0; i < clipFrames.Length; i++)
                    if (clipFrames[i] != null) Destroy(clipFrames[i]);
            }

            clipFrames = null;
            clipDelays = null;
            clipName = null;
            clipFrame = 0;
            clipTime = 0f;
            if (clipTexture == null) return;
            var texture = clipTexture;
            clipTexture = null;
            if (isActiveAndEnabled) StartCoroutine(ReleaseTexture(texture));
            else Resources.UnloadAsset(texture);
        }

        private static System.Collections.IEnumerator ReleaseTexture(Texture2D texture)
        {
            yield return new WaitForSecondsRealtime(1.5f);
            if (texture != null) Resources.UnloadAsset(texture);
        }

        private static Sprite Picture(Pic pic)
        {
            int index = (int)pic;
            if (pictures[index] != null) return pictures[index];
            var plate = new Plate(288, 200, new Color(0.96f, 0.91f, 0.74f, 1f));
            plate.Frame();
            switch (pic)
            {
                case Pic.Move: plate.SceneMove(); break;
                case Pic.Combat: plate.SceneDpad(); break;
                case Pic.Effect: plate.SceneEffect(); break;
                case Pic.Jump: plate.SceneJump(); break;
                case Pic.Dash: plate.SceneDash(); break;
                case Pic.Attack: plate.SceneAttack(); break;
                case Pic.Item: plate.SceneItem(); break;
                case Pic.Weapons: plate.SceneWeapons(); break;
                default: plate.SceneGlory(); break;
            }
            pictures[index] = plate.Bake(100f);
            return pictures[index];
        }
    }

    /// <summary>예시 그림을 점 필터로 그린다. y는 아래가 0.</summary>
    internal sealed class Plate
    {
        private static readonly Color Ink = new Color(0.1f, 0.07f, 0.05f, 1f);
        private static readonly Color Stage = new Color(0.16f, 0.17f, 0.24f, 1f);
        private static readonly Color Ground = new Color(0.45f, 0.34f, 0.24f, 1f);
        private static readonly Color Hero = new Color(0.96f, 0.91f, 0.74f, 1f);
        private static readonly Color Orange = new Color(1f, 0.55f, 0.2f, 1f);
        private static readonly Color Red = new Color(0.95f, 0.28f, 0.28f, 1f);
        private static readonly Color Green = new Color(0.4f, 0.9f, 0.42f, 1f);
        private static readonly Color Cyan = new Color(0.35f, 0.85f, 1f, 1f);
        private static readonly Color Gold = new Color(0.95f, 0.72f, 0.28f, 1f);
        private static readonly Color Purple = new Color(0.72f, 0.55f, 1f, 1f);

        private readonly int width;
        private readonly int height;
        private readonly Color[] pixels;

        public Plate(int width, int height, Color paper)
        {
            this.width = width;
            this.height = height;
            pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = paper;
        }

        public Sprite Bake(float pixelsPerUnit)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        public void Frame()
        {
            Box(8, 8, width - 9, height - 9, Ink);
            Box(12, 12, width - 13, height - 13, Stage);
        }

        public void SceneMove()
        {
            GroundLine();
            Person(78, 36, new Color(0.55f, 0.5f, 0.42f, 1f));
            Person(128, 36, new Color(0.75f, 0.68f, 0.52f, 1f));
            Person(186, 36, Hero);
            Arrow(230, 62, 1, Orange);
            Disc(48, 128, 24, Ink);
            Disc(48, 128, 20, new Color(0.22f, 0.23f, 0.28f, 1f));
            Disc(62, 128, 8, Orange);
        }

        public void SceneDpad()
        {
            Slot(78, 132, Gold, true);
            Slot(78, 72, Gold, false);
            Arrow(78, 168, 0, Gold);
            Arrow(78, 42, 2, Gold);
            Slot(168, 102, Purple, true);
            Slot(236, 102, Purple, false);
            Arrow(202, 102, 1, Purple);
        }

        public void SceneButtons()
        {
            Ring(214, 112, 28, Hero);
            Ring(214, 112, 22, Orange);
            Face(214, 112, 14, Orange, "A");
            Face(144, 156, 14, Cyan, "X");
            Face(74, 112, 14, Green, "Y");
            Face(144, 62, 14, Red, "B");
        }

        public void SceneEffect()
        {
            Slot(96, 104, Purple, true);
            Slot(176, 104, Purple, false);
            Arrow(52, 104, 3, Purple);
            Arrow(220, 104, 1, Purple);
        }

        public void SceneJump()
        {
            GroundLine();
            Arc(36, 150, 40, 78);
            Arc(150, 240, 40, 48);
            Person(92, 108, Hero);
            Face(250, 150, 14, Red, "B");
        }

        public void SceneDash()
        {
            GroundLine();
            for (int i = 0; i < 4; i++)
                Box(48 + i * 22, 70 - i * 6, 78 + i * 22, 78 - i * 6, Hero);
            Person(196, 36, Hero);
            Face(250, 130, 14, Green, "Y");
        }

        public void SceneAttack()
        {
            Box(28, 70, 190, 78, new Color(0.35f, 0.32f, 0.28f, 1f));
            Disc(52, 74, 6, new Color(0.7f, 0.62f, 0.4f, 1f));
            Disc(100, 74, 7, Gold);
            Disc(148, 74, 8, Orange);
            Ring(214, 112, 30, Hero);
            Ring(214, 112, 24, Orange);
            Disc(214, 112, 6, Hero);
            Person(52, 36, Hero);
            Box(78, 86, 160, 94, Hero);
            Face(40, 150, 14, Orange, "A");
        }

        public void SceneItem()
        {
            Slot(168, 104, Gold, true);
            Face(72, 120, 16, Cyan, "X");
            Disc(118, 112, 5, Cyan);
            Disc(136, 108, 3, Cyan);
        }

        public void SceneWeapons()
        {
            Face(36, 150, 14, Hero, "L");
            Face(252, 150, 14, Hero, "R");
            Arrow(78, 150, 1, Hero);
            Arrow(210, 150, 1, Hero);
            Box(108, 48, 118, 110, Hero);
            Box(100, 40, 126, 52, Orange);
            Box(150, 78, 196, 92, Hero);
            Box(186, 84, 206, 98, Orange);
            Disc(230, 100, 5, Orange);
            Disc(248, 78, 5, Orange);
            Disc(236, 56, 5, Orange);
        }

        public void SceneGlory()
        {
            Disc(144, 104, 26, new Color(0.55f, 0.22f, 0.24f, 1f));
            Disc(144, 104, 22, new Color(0.78f, 0.32f, 0.3f, 1f));
            Box(136, 108, 140, 112, Ink);
            Box(148, 108, 152, 112, Ink);
            Face(144, 168, 12, Cyan, "X");
            Arrow(144, 150, 2, Cyan);
            Face(230, 104, 12, Orange, "A");
            Arrow(210, 104, 3, Orange);
            Face(144, 40, 12, Red, "B");
            Arrow(144, 58, 0, Red);
            Face(58, 104, 12, Green, "Y");
            Arrow(78, 104, 1, Green);
        }

        private void GroundLine()
        {
            Box(16, 28, width - 17, 36, Ground);
            Box(16, 34, width - 17, 36, new Color(0.62f, 0.48f, 0.32f, 1f));
        }

        private void Person(int x, int feet, Color fill)
        {
            Disc(x, feet + 30, 8, Ink);
            Disc(x, feet + 30, 6, fill);
            Box(x - 5, feet + 14, x + 5, feet + 26, Ink);
            Box(x - 4, feet + 15, x + 4, feet + 25, fill);
            Box(x - 5, feet, x - 1, feet + 14, fill);
            Box(x + 2, feet + 1, x + 6, feet + 13, fill);
            Box(x + 2, feet + 30, x + 4, feet + 32, Ink);
        }

        private void Arc(int x0, int x1, int y, int rise)
        {
            for (int i = 0; i <= 14; i++)
            {
                float t = i / 14f;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int py = y + Mathf.RoundToInt(Mathf.Sin(t * Mathf.PI) * rise);
                Disc(x, py, 2, Hero);
            }
        }

        private void Slot(int x, int y, Color color, bool picked)
        {
            Box(x - 16, y - 16, x + 16, y + 16, picked ? Hero : Ink);
            Box(x - 12, y - 12, x + 12, y + 12, color);
        }

        public void Face(int cx, int cy, int radius, Color color, string letter)
        {
            Disc(cx, cy, radius, Ink);
            Disc(cx, cy, radius - 2, color);
            Glyph(cx, cy, letter, Ink);
        }

        private void Arrow(int cx, int cy, int direction, Color color)
        {
            for (int y = -6; y <= 6; y++)
            {
                for (int x = -6; x <= 6; x++)
                {
                    int px = x;
                    int py = y;
                    if (direction == 1) { px = y; py = -x; }
                    else if (direction == 2) { py = -y; }
                    else if (direction == 3) { px = -y; py = x; }
                    if (py > -2 && Mathf.Abs(px) <= 6 - py) Plot(cx + px, cy + py, color);
                }
            }
        }

        private void Glyph(int cx, int cy, string letter, Color color)
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
                    Plot(cx - w + x * 2, cy - h + y * 2, color);
                    Plot(cx - w + x * 2 + 1, cy - h + y * 2, color);
                    Plot(cx - w + x * 2, cy - h + y * 2 + 1, color);
                    Plot(cx - w + x * 2 + 1, cy - h + y * 2 + 1, color);
                }
            }
        }

        private void Ring(int cx, int cy, int radius, Color color)
        {
            int outer = radius * radius;
            int inner = (radius - 3) * (radius - 3);
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                {
                    int d = x * x + y * y;
                    if (d <= outer && d >= inner) Plot(cx + x, cy + y, color);
                }
        }

        private void Disc(int cx, int cy, int radius, Color color)
        {
            int r2 = radius * radius;
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (x * x + y * y <= r2) Plot(cx + x, cy + y, color);
        }

        private void Box(int x0, int y0, int x1, int y1, Color color)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Plot(x, y, color);
        }

        private void Plot(int x, int y, Color color)
        {
            if ((uint)x >= width || (uint)y >= height) return;
            pixels[y * width + x] = color;
        }
    }
}
