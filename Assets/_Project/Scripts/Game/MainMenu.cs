using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gstarr.Game
{
    /// <summary>
    /// 메인 화면: START / SETTINGS.
    /// START → 스테이지 목록 (Resources/Gstarr/Stages 의 StageData 자동 나열).
    /// SETTINGS → 키 설정 / 볼륨 / 그래픽.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private string title = "GSTARR";

        private RectTransform root;
        private RectTransform mainPanel, stagePanel, settingsPanel;
        private Selectable mainFirst, stageFirst;
        private RectTransform current;
        private SettingsView settingsView;
        private readonly Text[] leaderboardRows = new Text[5];
        private Text leaderboardStatus;
        private float resetStatusTimer;
        private RectTransform selectCursor;

        private void Awake()
        {
            GamePause.Clear();
            UIFactory.EnsureEventSystem();
            var canvas = UIFactory.CreateCanvas(transform, "MenuCanvas", 10);
            root = (RectTransform)canvas.transform;

            var bg = UIFactory.CreateImage("Background", root, new Color(0.06f, 0.05f, 0.1f, 1f));
            UIFactory.Stretch(bg.rectTransform);
            var menuArt = Gstarr.Items.GameAssets.Instance;
            if (menuArt != null && menuArt.menuBackground != null)
            {
                bg.sprite = menuArt.menuBackground;
                bg.color = Color.white;
                bg.type = Image.Type.Simple;
                bg.preserveAspect = false;
            }

            BuildMain();
            BuildStages();
            settingsView = SettingsView.Create(root, Back);
            settingsPanel = settingsView.Panel;
            Show(mainPanel, mainFirst);
        }

        private void Update()
        {
            if (current == mainPanel && IsResetShortcutPressed())
            {
                RunLeaderboard.Clear();
                RefreshLeaderboard();
                leaderboardStatus.text = "LEADERBOARD CLEARED";
                resetStatusTimer = 2.5f;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                return;
            }

            if (resetStatusTimer > 0f)
            {
                resetStatusTimer -= Time.unscaledDeltaTime;
                if (resetStatusTimer <= 0f) leaderboardStatus.text = "SHIFT + ENTER: RESET SCORES";
            }

            if (settingsView.IsRebinding || settingsView.IsCalibrating) return;
            if (UIFactory.Cancel.WasPressedThisFrame()) Back();

            // 마우스 클릭으로 선택이 풀렸을 때 컨트롤러로 다시 잡을 수 있게
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            {
                var gp = Gamepad.current;
                bool nav = (gp != null && (gp.dpad.ReadValue().sqrMagnitude > 0.1f || gp.leftStick.ReadValue().sqrMagnitude > 0.3f))
                           || (Keyboard.current != null && (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame));
                if (nav) UIFactory.Select(current == mainPanel ? mainFirst : current == stagePanel ? stageFirst : settingsView.FirstSelectable);
            }

            PlaceSelectCursor();
        }

        private void PlaceSelectCursor()
        {
            if (selectCursor == null) return;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var target = selected != null ? selected.GetComponent<RectTransform>() : null;
            bool show = current == mainPanel && target != null && target.IsChildOf(mainPanel);
            selectCursor.gameObject.SetActive(show);
            if (!show) return;

            Vector3 edge = target.TransformPoint(new Vector3(target.rect.xMin, target.rect.center.y, 0f));
            selectCursor.position = edge;
            float bob = Mathf.Sin(Time.unscaledTime * 5f) * 4f;
            selectCursor.anchoredPosition += new Vector2(-18f + bob, 0f);
        }

        private void Back()
        {
            if (current == stagePanel || current == settingsPanel)
            {
                GameSettings.Save();
                Show(mainPanel, mainFirst);
            }
        }

        private void Show(RectTransform panel, Selectable first)
        {
            mainPanel.gameObject.SetActive(panel == mainPanel);
            stagePanel.gameObject.SetActive(panel == stagePanel);
            settingsPanel.gameObject.SetActive(panel == settingsPanel);
            current = panel;
            UIFactory.Select(first);
        }

        // ─── 메인 ───────────────────────────────────────────

        private void BuildMain()
        {
            mainPanel = UIFactory.Stretch(UIFactory.CreateRect("Main", root));

            var titleText = UIFactory.CreateText("Title", mainPanel, title, 150, style: FontStyle.BoldAndItalic);
            UIFactory.Place(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-330f, 220f), new Vector2(1000f, 200f));
            titleText.color = new Color(1f, 0.6f, 0.25f);
            var logo = Gstarr.Items.GameAssets.Instance != null ? Gstarr.Items.GameAssets.Instance.titleLogo : null;
            if (logo != null)
            {
                titleText.gameObject.SetActive(false);
                var logoImage = UIFactory.CreateImage("TitleLogo", mainPanel, Color.white);
                logoImage.sprite = logo;
                logoImage.preserveAspect = true;
                UIFactory.Place(logoImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-330f, 220f), new Vector2(1000f, 200f));
            }

            var list = UIFactory.Place(UIFactory.CreateRect("Buttons", mainPanel), new Vector2(0.5f, 0.5f), new Vector2(-330f, -80f), new Vector2(520f, 460f));
            var layout = UIFactory.AddVerticalLayout(list, 18f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var startArt = LoadMenuArt("game start.png");
            var quitArt = LoadMenuArt("quit.png");
            var selectArt = LoadMenuArt("ｓｅｌｅｃｔ.png");

            var start = startArt != null
                ? ArtButton("Start", list, startArt, 168f, BeginGame)
                : UIFactory.CreateButton("Start", list, "START", 44, BeginGame);
            if (startArt == null) UIFactory.SetHeight(start, 90f);

            var settings = UIFactory.CreateButton("Settings", list, "SETTINGS", 36, () =>
            {
                if (!IsResetShortcutPressed()) Show(settingsPanel, settingsView.FirstSelectable);
            });
            UIFactory.SetHeight(settings, 72f);

            var quit = quitArt != null
                ? ArtButton("Quit", list, quitArt, 112f, QuitGame)
                : UIFactory.CreateButton("Quit", list, "QUIT", 44, QuitGame);
            if (quitArt == null) UIFactory.SetHeight(quit, 90f);
            mainFirst = start;

            if (selectArt != null)
            {
                var cursor = UIFactory.CreateImage("Select", mainPanel, Color.white);
                cursor.sprite = selectArt;
                cursor.preserveAspect = true;
                cursor.raycastTarget = false;
                selectCursor = cursor.rectTransform;
                selectCursor.anchorMin = selectCursor.anchorMax = new Vector2(0.5f, 0.5f);
                selectCursor.pivot = new Vector2(1f, 0.5f);
                selectCursor.sizeDelta = new Vector2(92f, 136f);
                selectCursor.SetAsLastSibling();
            }

            BuildLeaderboard(mainPanel);
        }

        private void BeginGame()
        {
            if (IsResetShortcutPressed()) return;
            StageData only = null;
            int playable = 0;
            foreach (var stage in StageData.LoadAll())
            {
                if (!stage.IsPlayable) continue;
                playable++;
                only = stage;
            }
            if (playable == 1) StageSession.Play(only);
            else Show(stagePanel, stageFirst);
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private Button ArtButton(string name, Transform parent, Sprite art, float height, UnityEngine.Events.UnityAction onClick)
        {
            var rt = UIFactory.CreateRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = art;
            image.preserveAspect = true;
            image.color = Color.white;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            colors.fadeDuration = 0.04f;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                GameAudio.Play(GameAudio.Id.Ui, 0.7f);
                onClick();
            });
            var nav = button.navigation;
            nav.mode = Navigation.Mode.Automatic;
            button.navigation = nav;
            UIFactory.SetHeight(button, height);
            return button;
        }

        private static Sprite LoadMenuArt(string fileName)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), "Assets/arts/UI", fileName);
            if (!File.Exists(full)) return null;
            byte[] bytes;
            try { bytes = File.ReadAllBytes(full); }
            catch { return null; }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes, false))
            {
                Destroy(texture);
                return null;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.name = Path.GetFileNameWithoutExtension(fileName);
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private void BuildLeaderboard(RectTransform parent)
        {
            var panel = UIFactory.CreateImage("Leaderboard", parent, UIFactory.PanelColor);
            UIFactory.Place(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(-290f, 0f), new Vector2(500f, 650f));

            var listArt = LoadMenuArt("ｒａｎｋ ｌｉｓｔ.png");
            if (listArt != null)
            {
                var headingArt = UIFactory.CreateImage("HeadingArt", panel.transform, Color.white);
                headingArt.sprite = listArt;
                headingArt.preserveAspect = true;
                UIFactory.Place(headingArt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(360f, 150f));
            }
            else
            {
                var heading = UIFactory.CreateText("Heading", panel.transform, "LEADERBOARD", 48, style: FontStyle.BoldAndItalic);
                UIFactory.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(460f, 70f));
                heading.color = new Color(1f, 0.6f, 0.25f);
            }

            for (int i = 0; i < 5; i++)
            {
                leaderboardRows[i] = UIFactory.CreateText("Rank" + (i + 1), panel.transform, "", 28, TextAnchor.MiddleLeft, FontStyle.Normal);
                UIFactory.Place(leaderboardRows[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -248f - i * 70f), new Vector2(420f, 64f));
            }

            leaderboardStatus = UIFactory.CreateText("ResetHint", panel.transform, "SHIFT + ENTER: RESET SCORES", 18, TextAnchor.MiddleCenter, FontStyle.Normal);
            UIFactory.Place(leaderboardStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(460f, 34f));
            leaderboardStatus.color = new Color(1f, 1f, 1f, 0.55f);
            RefreshLeaderboard();
        }

        private void RefreshLeaderboard()
        {
            var entries = RunLeaderboard.GetTopScores();
            for (int i = 0; i < leaderboardRows.Length; i++)
            {
                bool hasEntry = i < entries.Count;
                leaderboardRows[i].text = hasEntry
                    ? $"{i + 1}.  {entries[i].nickname}   {entries[i].score:N0}"
                    : $"{i + 1}.  ---";
                leaderboardRows[i].color = new Color(1f, 1f, 1f, hasEntry ? 1f : 0.4f);
            }
            leaderboardStatus.text = "SHIFT + ENTER: RESET SCORES";
        }

        private static bool IsResetShortcutPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.shiftKey.isPressed && keyboard.enterKey.wasPressedThisFrame;
        }

        // ─── 스테이지 ───────────────────────────────────────

        private void BuildStages()
        {
            stagePanel = UIFactory.Stretch(UIFactory.CreateRect("Stages", root));

            var header = UIFactory.CreateText("Header", stagePanel, "SELECT STAGE", 70, style: FontStyle.BoldAndItalic);
            UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1000f, 100f));

            var gridRt = UIFactory.Place(UIFactory.CreateRect("Grid", stagePanel), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1500f, 640f));
            var grid = gridRt.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(340f, 200f);
            grid.spacing = new Vector2(30f, 30f);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            var stages = StageData.LoadAll();
            foreach (var stage in stages)
            {
                var s = stage;
                var b = UIFactory.CreateButton("Stage_" + s.name, gridRt, "", 40, () => StageSession.Play(s));
                var label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.UpperCenter;
                label.rectTransform.offsetMax = new Vector2(0f, -24f);
                label.text = s.displayName;

                string song = s.music != null ? Gstarr.Rhythm.BeatChart.StripBpmSuffix(s.music.name) : "(no music)";
                if (song.Length > 26) song = song.Substring(0, 24) + "...";
                string bpmText = s.HasBpm(out float bpm) ? "  " + bpm + " BPM" : "";
                var info = UIFactory.CreateText("Info", b.transform, song + "\n" + bpmText.Trim() + "\nBEST  " + s.BestScore.ToString("N0"), 22,
                    TextAnchor.LowerCenter, FontStyle.Normal);
                UIFactory.Stretch(info.rectTransform).offsetMin = new Vector2(10f, 20f);
                info.color = new Color(1f, 1f, 1f, 0.75f);
                if (!s.IsPlayable) b.interactable = false;

                if (stageFirst == null) stageFirst = b;
            }

            if (stages.Length == 0)
            {
                var empty = UIFactory.CreateText("Empty", stagePanel,
                    "No stages. Put a wav + json into a folder like Assets/Music/stage1", 30, style: FontStyle.Normal);
                UIFactory.Place(empty.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 60f));
            }

            var back = UIFactory.CreateButton("Back", stagePanel, "BACK", 34, Back);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(260f, 70f));
            if (stageFirst == null) stageFirst = back;
        }

    }
}
