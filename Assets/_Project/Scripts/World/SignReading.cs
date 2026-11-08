using System.Collections.Generic;
using Gstarr.Game;
using Gstarr.Rhythm;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gstarr.World
{
    /// <summary>
    /// 표지판 앞에 서면 A 가 뜨고, 누르면 그 파트의 설명과 예시 그림을 연다.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public sealed class SignBoard : MonoBehaviour
    {
        private static readonly List<SignBoard> boards = new List<SignBoard>();

        private RoomPoster.Part part;
        private Transform prompt;
        private Transform player;
        private Vector3 promptRest;

        private void OnEnable() => boards.Add(this);

        private void OnDisable() => boards.Remove(this);

        public void Arm(RoomPoster.Part lesson, float worldHeight)
        {
            part = lesson;
            prompt = BuildPrompt();
            promptRest = new Vector3(0f, -(worldHeight * 0.5f + 0.42f), 0f);
            prompt.localPosition = promptRest;
            prompt.gameObject.SetActive(false);
            SignDetail.Ensure();
        }

        private void Update()
        {
            if (prompt == null) return;
            if (SignDetail.IsOpen)
            {
                prompt.gameObject.SetActive(false);
                return;
            }

            if (player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged == null) return;
                player = tagged.transform;
            }

            float dx = Mathf.Abs(player.position.x - transform.position.x);
            float below = transform.position.y - player.position.y;
            bool near = dx < 1.45f && below > 0.5f && below < 4.4f && !GamePause.BlocksGameplay && IsClosest(dx);
            prompt.gameObject.SetActive(near);
            if (!near) return;

            float bob = Mathf.Sin(Time.unscaledTime * 5f) * 0.06f;
            prompt.localPosition = promptRest + new Vector3(0f, bob, 0f);
            if (SignDetail.ConfirmPressed()) SignDetail.Open(part);
        }

        private bool IsClosest(float dx)
        {
            for (int i = 0; i < boards.Count; i++)
            {
                var other = boards[i];
                if (other == this) continue;
                float otherDx = Mathf.Abs(player.position.x - other.transform.position.x);
                if (otherDx < dx) return false;
            }
            return true;
        }

        private Transform BuildPrompt()
        {
            var root = new GameObject("ReadPrompt").transform;
            root.SetParent(transform, false);

            var button = new GameObject("A");
            button.transform.SetParent(root, false);
            button.transform.localPosition = new Vector3(-0.42f, 0f, 0f);
            var renderer = button.AddComponent<SpriteRenderer>();
            renderer.sprite = SignDetail.ButtonSprite();
            renderer.sortingOrder = 6;

            button.transform.localScale = new Vector3(0.72f, 0.72f, 1f);

            var canvasGo = new GameObject("Read", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(root, false);
            canvasGo.transform.localPosition = new Vector3(0.38f, 0f, 0f);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 6;
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(72f, 32f);
            rt.localScale = Vector3.one * 0.02f;
            var tag = UIFactory.CreateImage("Tag", rt, new Color(0.96f, 0.91f, 0.74f, 1f));
            UIFactory.Place(tag.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 32f));
            var text = UIFactory.CreateText("Label", rt, "읽기", 22, TextAnchor.MiddleCenter, FontStyle.Bold);
            var outline = text.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            text.color = new Color(0.1f, 0.07f, 0.05f, 1f);
            UIFactory.Place(text.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 32f));
            return root;
        }
    }

    /// <summary>표지판 설명. 왼쪽은 예시 그림, 오른쪽은 그 그림이 말하는 조작.</summary>
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
        private bool visible;
        private bool waitingRelease;
        private bool releaseSeen;
        private bool pausedMusic;
        private int openedFrame;

        public static bool IsOpen => instance != null && (instance.visible || instance.waitingRelease);

        /// <summary>결과 화면으로 넘어갈 때 표지판 설명을 남기지 않는다.</summary>
        public static void Dismiss()
        {
            if (instance == null) return;
            instance.visible = false;
            instance.waitingRelease = false;
            instance.pausedMusic = false;
            if (instance.sheet != null) instance.sheet.SetActive(false);
        }

        public static void Ensure()
        {
            if (instance != null) return;
            var host = new GameObject("SignDetail");
            instance = host.AddComponent<SignDetail>();
            instance.Build();
        }

        public static void Open(RoomPoster.Part part)
        {
            Ensure();
            if (instance.visible || instance.waitingRelease) return;
            instance.Load(part);
            instance.sheet.SetActive(true);
            instance.visible = true;
            instance.openedFrame = Time.frameCount;
            instance.PausePlay();
            GameAudio.Play(GameAudio.Id.Ui, 0.55f);
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
            if (EscapePressed())
            {
                Close();
                return;
            }
            if (BackPressed())
            {
                if (page > 0) Turn(-1);
                else Close();
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
            var conductor = RhythmConductor.Instance;
            pausedMusic = conductor != null && conductor.IsReady && !conductor.IsPaused && !conductor.IsFinished;
            if (pausedMusic) conductor.Pause();
            GamePause.SetPaused(true);
        }

        private void ResumePlay()
        {
            if (pausedMusic)
            {
                var conductor = RhythmConductor.Instance;
                if (conductor != null) conductor.Resume();
                pausedMusic = false;
            }
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

        private void Load(RoomPoster.Part part)
        {
            pages = Pages(part);
            page = 0;
            Show();
        }

        private void Show()
        {
            var leaf = pages[page];
            title.text = leaf.Title;
            body.text = leaf.Body;
            caption.text = leaf.Caption;
            example.sprite = Picture(leaf.Picture);
            bool many = pages.Length > 1;
            pageMark.text = many ? (page + 1) + " / " + pages.Length : "";
            prevLabel.text = page > 0 ? "B  이전" : "";
            nextLabel.text = page < pages.Length - 1 ? "다음" : "닫기";
        }

        private static Page[] Pages(RoomPoster.Part part)
        {
            switch (part)
            {
                case RoomPoster.Part.Move:
                    return new[]
                    {
                        Leaf("이동", "왼쪽 스틱을 좌우로 기울이면 그쪽으로 걷는다.", "기울인 쪽으로 걷는다", Pic.Move)
                    };
                case RoomPoster.Part.Dpad:
                    return new[]
                    {
                        Leaf("십자키", "위·아래는 전투 아이템, 왼쪽·오른쪽은 효과 아이템이다.\n위와 왼쪽이 이전, 아래와 오른쪽이 다음이다.", "위아래 전투, 좌우 효과", Pic.Combat)
                    };
                default:
                    string miss = GameSettings.RhythmAssist
                        ? "놓쳐도 약하게 나간다."
                        : "놓치면 공격이 나가지 않는다.";
                    return new[]
                    {
                        Leaf("버튼", "B는 점프다. 공중에서 한 번 더 누를 수 있다.\nY는 대시다. 스틱 쪽으로 파고들고, 공중에서는 한 번이다.\nA는 공격이다. 노트가 링에 닿을 때 누른다. " + miss + "\nX는 고른 아이템이다. 쓰는 것도 박자다.\nL과 R로 무기를 바꾼다.", "B 점프, Y 대시, A 공격, X 아이템", Pic.Jump),
                        Leaf("글로리 킬", "체력이 얼마 안 남으면 적이 비틀거린다. 가까이서 A를 누르면 시간이 느려지고, 노트 네 개가 가운데로 온다.\n버튼 자리가 방향이다. X가 위, A가 오른쪽, B가 아래, Y가 왼쪽.\n네 박자를 맞추면 끝난다. 버튼을 틀리면 바로 풀리고, 한 번 늦으면 그 노트만 다시 온다.\n스타일이 B 이상이면, 바로 쓰러지던 적도 한 번 버틴다.", "방향 버튼을 박자에 맞춘다", Pic.Glory)
                    };
            }
        }

        private static Page Leaf(string title, string body, string caption, Pic picture)
        {
            return new Page { Title = title, Body = body, Caption = caption, Picture = picture };
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
                case Pic.Jump: plate.SceneButtons(); break;
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
