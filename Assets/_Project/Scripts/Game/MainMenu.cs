using System.Collections;
using System.Collections.Generic;
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
        private RectTransform settingsContent;
        private Selectable mainFirst, stageFirst;
        private readonly List<Button> settingsTabs = new List<Button>();
        private int currentTab;
        private RectTransform current;
        private bool rebinding;

        private static readonly string[] TabNames = { "CONTROLS", "AUDIO", "GRAPHICS" };

        private void Awake()
        {
            Time.timeScale = 1f;
            UIFactory.EnsureEventSystem();
            var canvas = UIFactory.CreateCanvas(transform, "MenuCanvas", 10);
            root = (RectTransform)canvas.transform;

            var bg = UIFactory.CreateImage("Background", root, new Color(0.06f, 0.05f, 0.1f, 1f));
            UIFactory.Stretch(bg.rectTransform);

            BuildMain();
            BuildStages();
            BuildSettings();
            Show(mainPanel, mainFirst);
        }

        private void Update()
        {
            if (rebinding) return;
            if (UIFactory.Cancel.WasPressedThisFrame()) Back();

            // 마우스 클릭으로 선택이 풀렸을 때 컨트롤러로 다시 잡을 수 있게
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            {
                var gp = Gamepad.current;
                bool nav = (gp != null && (gp.dpad.ReadValue().sqrMagnitude > 0.1f || gp.leftStick.ReadValue().sqrMagnitude > 0.3f))
                           || (Keyboard.current != null && (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame));
                if (nav) UIFactory.Select(current == mainPanel ? mainFirst : current == stagePanel ? stageFirst : settingsTabs[currentTab]);
            }
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
            UIFactory.Place(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1200f, 200f));
            titleText.color = new Color(1f, 0.6f, 0.25f);

            var list = UIFactory.Place(UIFactory.CreateRect("Buttons", mainPanel), new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(420f, 220f));
            UIFactory.AddVerticalLayout(list, 24f);

            var start = UIFactory.CreateButton("Start", list, "START", 44, () => Show(stagePanel, stageFirst));
            UIFactory.SetHeight(start, 90f);
            var settings = UIFactory.CreateButton("Settings", list, "SETTINGS", 44, () => Show(settingsPanel, settingsTabs[currentTab]));
            UIFactory.SetHeight(settings, 90f);
            mainFirst = start;
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

                string song = s.music != null ? s.music.name : "(no music)";
                if (song.Length > 26) song = song.Substring(0, 24) + "...";
                var info = UIFactory.CreateText("Info", b.transform, song + "\nBEST  " + s.BestScore.ToString("N0"), 22,
                    TextAnchor.LowerCenter, FontStyle.Normal);
                UIFactory.Stretch(info.rectTransform).offsetMin = new Vector2(10f, 20f);
                info.color = new Color(1f, 1f, 1f, 0.75f);
                if (s.music == null || s.beatChart == null) b.interactable = false;

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

        // ─── 설정 ───────────────────────────────────────────

        private void BuildSettings()
        {
            settingsPanel = UIFactory.Stretch(UIFactory.CreateRect("Settings", root));

            var header = UIFactory.CreateText("Header", settingsPanel, "SETTINGS", 70, style: FontStyle.BoldAndItalic);
            UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1000f, 100f));

            var tabs = UIFactory.Place(UIFactory.CreateRect("Tabs", settingsPanel), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1020f, 70f));
            var hl = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 20f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = true;
            for (int i = 0; i < TabNames.Length; i++)
            {
                int idx = i;
                settingsTabs.Add(UIFactory.CreateButton("Tab_" + TabNames[i], tabs, TabNames[i], 32, () => OpenTab(idx)));
            }

            var panelBg = UIFactory.CreateImage("ContentBg", settingsPanel, UIFactory.PanelColor);
            UIFactory.Place(panelBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1100f, 640f));

            settingsContent = UIFactory.Place(UIFactory.CreateRect("Content", settingsPanel), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1040f, 600f));
            UIFactory.AddVerticalLayout(settingsContent, 12f);

            var back = UIFactory.CreateButton("Back", settingsPanel, "BACK", 34, Back);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(260f, 70f));

            OpenTab(0, select: false);
        }

        private void OpenTab(int index, bool select = true)
        {
            currentTab = index;
            for (int i = 0; i < settingsTabs.Count; i++)
            {
                var img = (Image)settingsTabs[i].targetGraphic;
                img.color = i == index ? new Color(1f, 0.8f, 0.6f) : Color.white;
            }

            for (int i = settingsContent.childCount - 1; i >= 0; i--)
                Destroy(settingsContent.GetChild(i).gameObject);

            Selectable first;
            switch (index)
            {
                case 0: first = BuildControls(); break;
                case 1: first = BuildAudio(); break;
                default: first = BuildGraphics(); break;
            }
            if (select) UIFactory.Select(first != null ? first : settingsTabs[index]);
        }

        private OptionRow AddOption(string label, System.Func<string> get, System.Action<int> change)
        {
            var row = UIFactory.CreateButton<OptionRow>("Opt_" + label, settingsContent, "", 30, null);
            UIFactory.SetHeight(row, 64f);
            var labelText = row.GetComponentInChildren<Text>();
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.rectTransform.offsetMin = new Vector2(30f, 0f);
            labelText.text = label;

            row.valueText = UIFactory.CreateText("Value", row.transform, "", 30, TextAnchor.MiddleRight);
            UIFactory.Stretch(row.valueText.rectTransform).offsetMax = new Vector2(-30f, 0f);
            row.getValue = get;
            row.change = change;
            row.onClick.AddListener(() => { change(1); row.Refresh(); });
            row.Refresh();
            return row;
        }

        private Selectable BuildAudio()
        {
            var first = AddOption("MASTER VOLUME", () => Mathf.RoundToInt(GameSettings.MasterVolume * 100) + "%",
                d => GameSettings.SetMasterVolume(Step(GameSettings.MasterVolume, d)));
            AddOption("MUSIC VOLUME", () => Mathf.RoundToInt(GameSettings.MusicVolume * 100) + "%",
                d => GameSettings.SetMusicVolume(Step(GameSettings.MusicVolume, d)));
            return first;
        }

        private static float Step(float v, int d)
        {
            float n = Mathf.Round(v * 10f) / 10f + d * 0.1f;
            if (n > 1.001f) n = 0f;
            else if (n < -0.001f) n = 1f;
            return n;
        }

        private Selectable BuildGraphics()
        {
            var first = AddOption("QUALITY", () => QualitySettings.names[GameSettings.Quality],
                d => GameSettings.Quality = GameSettings.Quality + d);

            AddOption("RESOLUTION", () =>
            {
                var r = GameSettings.CurrentResolution;
                return r.x + " x " + r.y;
            }, d =>
            {
                var list = GameSettings.Resolutions;
                if (list.Count == 0) return;
                int i = list.IndexOf(GameSettings.CurrentResolution);
                i = (int)Mathf.Repeat(i < 0 ? list.Count - 1 : i + d, list.Count);
                GameSettings.SetResolution(list[i]);
            });

            AddOption("FULLSCREEN", () => GameSettings.Fullscreen ? "ON" : "OFF",
                d => GameSettings.Fullscreen = !GameSettings.Fullscreen);
            AddOption("VSYNC", () => GameSettings.VSync ? "ON" : "OFF",
                d => GameSettings.VSync = !GameSettings.VSync);
            return first;
        }

        // ─── 키 설정 ────────────────────────────────────────

        private Selectable BuildControls()
        {
            var actions = GameSettings.Actions;
            if (actions == null)
            {
                var t = UIFactory.CreateText("NoInput", settingsContent, "No Input Actions assigned in GameAssets", 30);
                UIFactory.SetHeight(t, 60f);
                return null;
            }

            var head = UIFactory.CreateRect("Head", settingsContent);
            UIFactory.SetHeight(head, 36f);
            AddCell(head, "ACTION", 0f, 0.4f, 24);
            AddCell(head, "CONTROLLER", 0.4f, 0.7f, 24);
            AddCell(head, "KEYBOARD", 0.7f, 1f, 24);

            Selectable first = null;
            var map = actions.FindActionMap("Player");
            foreach (var action in map.actions)
            {
                if (action.name == "Move") continue;
                var row = UIFactory.CreateRect("Row_" + action.name, settingsContent);
                UIFactory.SetHeight(row, 46f);
                AddCell(row, Pretty(action.name), 0f, 0.4f, 26).alignment = TextAnchor.MiddleLeft;

                var pad = AddBindButton(row, action, "<Gamepad>", 0.4f, 0.69f);
                AddBindButton(row, action, "<Keyboard>", 0.71f, 1f);
                if (first == null) first = pad;
            }

            var reset = UIFactory.CreateButton("Reset", settingsContent, "RESET TO DEFAULT", 28, () =>
            {
                actions.RemoveAllBindingOverrides();
                GameSettings.SaveBindings();
                OpenTab(0);
            });
            UIFactory.SetHeight(reset, 54f);
            return first;
        }

        private static Text AddCell(RectTransform row, string text, float x0, float x1, int size)
        {
            var t = UIFactory.CreateText("Cell", row, text, size);
            t.rectTransform.anchorMin = new Vector2(x0, 0f);
            t.rectTransform.anchorMax = new Vector2(x1, 1f);
            t.rectTransform.offsetMin = new Vector2(10f, 0f);
            t.rectTransform.offsetMax = Vector2.zero;
            return t;
        }

        private Button AddBindButton(RectTransform row, InputAction action, string device, float x0, float x1)
        {
            int index = -1;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (!b.isComposite && !b.isPartOfComposite && b.path.StartsWith(device)) { index = i; break; }
            }

            Button button = null;
            button = UIFactory.CreateButton("Bind" + device, row, "", 24, () => StartRebind(action, index, device, button));
            var rt = (RectTransform)button.transform;
            rt.anchorMin = new Vector2(x0, 0f);
            rt.anchorMax = new Vector2(x1, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            SetBindLabel(button, action, index);
            if (index < 0) button.interactable = false;
            return button;
        }

        private static void SetBindLabel(Button button, InputAction action, int index)
        {
            button.GetComponentInChildren<Text>().text = index < 0 ? "-" : action.GetBindingDisplayString(index);
        }

        private void StartRebind(InputAction action, int index, string device, Button button)
        {
            if (index < 0 || rebinding) return;
            rebinding = true;
            button.GetComponentInChildren<Text>().text = "PRESS...";
            var es = EventSystem.current;
            if (es != null) es.sendNavigationEvents = false;

            bool wasEnabled = action.enabled;
            action.Disable();
            action.PerformInteractiveRebinding(index)
                .WithControlsHavingToMatchPath(device)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op => { op.Dispose(); FinishRebind(action, index, button, wasEnabled); })
                .OnCancel(op => { op.Dispose(); FinishRebind(action, index, button, wasEnabled); })
                .Start();
        }

        private void FinishRebind(InputAction action, int index, Button button, bool wasEnabled)
        {
            if (wasEnabled) action.Enable();
            GameSettings.SaveBindings();
            SetBindLabel(button, action, index);
            StartCoroutine(EndRebindNextFrames());
        }

        private IEnumerator EndRebindNextFrames()
        {
            // 방금 누른 버튼이 UI 확인/뒤로로 같이 처리되지 않도록 잠깐 대기
            yield return new WaitForSecondsRealtime(0.25f);
            rebinding = false;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
        }

        private static string Pretty(string actionName)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < actionName.Length; i++)
            {
                if (i > 0 && char.IsUpper(actionName[i])) sb.Append(' ');
                sb.Append(char.ToUpperInvariant(actionName[i]));
            }
            return sb.ToString();
        }
    }
}
