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
        private Selectable mainFirst, stageFirst;
        private RectTransform current;
        private SettingsView settingsView;

        private void Awake()
        {
            GamePause.Clear();
            UIFactory.EnsureEventSystem();
            var canvas = UIFactory.CreateCanvas(transform, "MenuCanvas", 10);
            root = (RectTransform)canvas.transform;

            var bg = UIFactory.CreateImage("Background", root, new Color(0.06f, 0.05f, 0.1f, 1f));
            UIFactory.Stretch(bg.rectTransform);

            BuildMain();
            BuildStages();
            settingsView = SettingsView.Create(root, Back);
            settingsPanel = settingsView.Panel;
            Show(mainPanel, mainFirst);
        }

        private void Update()
        {
            if (settingsView.IsRebinding) return;
            if (UIFactory.Cancel.WasPressedThisFrame()) Back();

            // 마우스 클릭으로 선택이 풀렸을 때 컨트롤러로 다시 잡을 수 있게
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            {
                var gp = Gamepad.current;
                bool nav = (gp != null && (gp.dpad.ReadValue().sqrMagnitude > 0.1f || gp.leftStick.ReadValue().sqrMagnitude > 0.3f))
                           || (Keyboard.current != null && (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame));
                if (nav) UIFactory.Select(current == mainPanel ? mainFirst : current == stagePanel ? stageFirst : settingsView.FirstSelectable);
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
            var settings = UIFactory.CreateButton("Settings", list, "SETTINGS", 44, () => Show(settingsPanel, settingsView.FirstSelectable));
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
