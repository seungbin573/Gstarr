using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gstarr.Game
{
    /// <summary>키 설정 / 볼륨 / 그래픽 설정 화면. 메인 메뉴와 일시정지 메뉴에서 같이 쓴다.</summary>
    public class SettingsView : MonoBehaviour
    {
        private RectTransform settingsPanel;
        private RectTransform settingsContent;
        private ScrollRect settingsScroll;
        private readonly List<Button> settingsTabs = new List<Button>();
        private int currentTab;
        private bool rebinding;
        private Action onBack;
        private bool calibrating;
        private int calibrateBeats;
        private int calibrateTaps;
        private double nextBeatAt;
        private double lastBeatAt = double.NaN;
        private double errorSum;
        private Text calibrateStatus;
        private Image calibrateFlash;
        private const int CalibrateCount = 8;
        private const double CalibrateInterval = 0.5;

        private static readonly string[] TabNames = { "CONTROLS", "AUDIO", "GAMEPLAY", "GRAPHICS" };

        public RectTransform Panel => settingsPanel;
        public bool IsRebinding => rebinding;
        public bool IsCalibrating => calibrating || ignoreCancelFrame == Time.frameCount;
        private int ignoreCancelFrame = -1;
        public Selectable FirstSelectable =>
            currentTab >= 0 && currentTab < settingsTabs.Count ? settingsTabs[currentTab] : null;

        public static SettingsView Create(Transform parent, Action onBack, bool withBackground = false)
        {
            var rt = UIFactory.Stretch(UIFactory.CreateRect("Settings", parent));
            if (withBackground) UIFactory.Stretch(UIFactory.CreateImage("Dim", rt, new Color(0.04f, 0.04f, 0.08f, 0.95f)).rectTransform);
            var view = rt.gameObject.AddComponent<SettingsView>();
            view.onBack = onBack;
            view.BuildSettings();
            return view;
        }

        // ─── 설정 ───────────────────────────────────────────

        private void BuildSettings()
        {
            settingsPanel = (RectTransform)transform;

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

            var viewport = UIFactory.Place(UIFactory.CreateRect("Viewport", settingsPanel), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1040f, 600f));
            viewport.gameObject.AddComponent<RectMask2D>();
            settingsScroll = viewport.gameObject.AddComponent<ScrollRect>();
            settingsScroll.horizontal = false;
            settingsScroll.movementType = ScrollRect.MovementType.Clamped;
            settingsScroll.scrollSensitivity = 40f;
            settingsScroll.viewport = viewport;

            settingsContent = UIFactory.CreateRect("Content", viewport);
            settingsContent.anchorMin = new Vector2(0f, 1f);
            settingsContent.anchorMax = new Vector2(1f, 1f);
            settingsContent.pivot = new Vector2(0.5f, 1f);
            settingsContent.anchoredPosition = Vector2.zero;
            settingsContent.sizeDelta = new Vector2(0f, 0f);
            var layout = UIFactory.AddVerticalLayout(settingsContent, 12f);
            layout.padding = new RectOffset(8, 8, 8, 8);
            var fitter = settingsContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            settingsScroll.content = settingsContent;

            var back = UIFactory.CreateButton("Back", settingsPanel, "BACK", 34, () => onBack?.Invoke());
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(260f, 70f));

            calibrateFlash = UIFactory.CreateImage("CalibrateFlash", settingsPanel, new Color(1f, 0.75f, 0.35f, 0f));
            UIFactory.Stretch(calibrateFlash.rectTransform);
            calibrateFlash.raycastTarget = false;

            OpenTab(0, select: false);
        }

        private void Update()
        {
            if (calibrateFlash != null)
            {
                var color = calibrateFlash.color;
                color.a = Mathf.MoveTowards(color.a, 0f, Time.unscaledDeltaTime * 4f);
                calibrateFlash.color = color;
            }

            if (!calibrating) return;

            if (UIFactory.Cancel.WasPressedThisFrame())
            {
                StopCalibration("CALIBRATION CANCELED");
                return;
            }

            if (CalibratePressed()) RegisterCalibrationTap();

            if (Time.unscaledTimeAsDouble < nextBeatAt) return;
            lastBeatAt = Time.unscaledTimeAsDouble;
            nextBeatAt += CalibrateInterval;
            calibrateBeats++;
            GameAudio.Play(GameAudio.Id.Calibrate);
            if (calibrateFlash != null)
            {
                var color = calibrateFlash.color;
                color.a = 0.45f;
                calibrateFlash.color = color;
            }

            if (calibrateBeats >= CalibrateCount) FinishCalibration();
            else if (calibrateStatus != null)
                calibrateStatus.text = "TAP  " + calibrateBeats + " / " + CalibrateCount + "     ENTER OR A ON THE CLICK";
        }

        private void OnDisable()
        {
            if (!calibrating) return;
            calibrating = false;
            ignoreCancelFrame = Time.frameCount;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
        }

        private void StartCalibration()
        {
            calibrating = true;
            calibrateBeats = 0;
            calibrateTaps = 0;
            errorSum = 0;
            lastBeatAt = double.NaN;
            nextBeatAt = Time.unscaledTimeAsDouble + 0.65;
            if (calibrateStatus != null)
                calibrateStatus.text = "LISTEN, THEN TAP ENTER OR A ON EACH CLICK";
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.sendNavigationEvents = false;
            }
        }

        private void RegisterCalibrationTap()
        {
            if (double.IsNaN(lastBeatAt)) return;
            double error = Time.unscaledTimeAsDouble - lastBeatAt;
            if (Math.Abs(error) > 0.22) return;
            errorSum += error;
            calibrateTaps++;
        }

        private void FinishCalibration()
        {
            if (calibrateTaps >= 3)
            {
                int ms = (int)Math.Round(errorSum / calibrateTaps * 1000.0);
                GameSettings.SetAudioOffsetMs(ms);
                StopCalibration("OFFSET SET TO  " + GameSettings.AudioOffsetLabel);
            }
            else
            {
                StopCalibration("NEED AT LEAST 3 TAPS. OFFSET UNCHANGED.");
            }
        }

        private void StopCalibration(string message)
        {
            calibrating = false;
            ignoreCancelFrame = Time.frameCount;
            lastBeatAt = double.NaN;
            if (calibrateStatus != null) calibrateStatus.text = message;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
            if (currentTab >= 0 && currentTab < settingsTabs.Count) UIFactory.Select(settingsTabs[currentTab]);
        }

        private static bool CalibratePressed()
        {
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            bool key = keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.zKey.wasPressedThisFrame);
            bool button = pad != null && pad.buttonEast.wasPressedThisFrame;
            return key || button;
        }

        private void OpenTab(int index, bool select = true)
        {
            if (calibrating) StopCalibration("CALIBRATION CANCELED");
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
                case 2: first = BuildGameplay(); break;
                default: first = BuildGraphics(); break;
            }
            if (select) UIFactory.Select(first != null ? first : settingsTabs[index]);
            if (settingsScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                settingsScroll.verticalNormalizedPosition = 1f;
            }
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
            AddOption("SFX VOLUME", () => Mathf.RoundToInt(GameSettings.SfxVolume * 100) + "%",
                d => GameSettings.SetSfxVolume(Step(GameSettings.SfxVolume, d)));
            AddOption("AUDIO OFFSET", () => GameSettings.AudioOffsetLabel,
                d => GameSettings.SetAudioOffsetMs(GameSettings.AudioOffsetMs + d * 5));
            var calibrate = UIFactory.CreateButton("Calibrate", settingsContent, "CALIBRATE", 28, StartCalibration);
            UIFactory.SetHeight(calibrate, 54f);
            calibrateStatus = UIFactory.CreateText("CalibrateHelp", settingsContent,
                "Late hits: raise offset. Early hits: lower it. Calibrate taps Enter or A on each click.", 20);
            UIFactory.SetHeight(calibrateStatus, 72f);
            return first;
        }

        private Selectable BuildGameplay()
        {
            var first = AddOption("RHYTHM ASSIST", () => GameSettings.RhythmAssist ? "ON" : "OFF",
                d => GameSettings.SetRhythmAssist(!GameSettings.RhythmAssist));
            AddOption("ARCADE MODE", () => GameSettings.ArcadeMode ? "ON" : "OFF",
                d => GameSettings.SetArcadeMode(!GameSettings.ArcadeMode));
            var help = UIFactory.CreateText("GameplayHelp", settingsContent,
                "Assist: missed attacks deal reduced damage. Arcade: return to title after inactivity.", 20);
            UIFactory.SetHeight(help, 54f);
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
            if (map == null)
            {
                var missing = UIFactory.CreateText("NoMap", settingsContent, "Player action map was not found", 30);
                UIFactory.SetHeight(missing, 60f);
                return null;
            }

            foreach (var action in map.actions)
            {
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
                if (b.isComposite || b.isPartOfComposite) continue;
                var path = string.IsNullOrEmpty(b.effectivePath) ? b.path : b.effectivePath;
                if (!string.IsNullOrEmpty(path) && path.StartsWith(device)) { index = i; break; }
            }

            Button button = null;
            button = UIFactory.CreateButton("Bind" + device, row, "", 24, () => StartRebind(action, index, device, button));
            var rt = (RectTransform)button.transform;
            rt.anchorMin = new Vector2(x0, 0f);
            rt.anchorMax = new Vector2(x1, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (index >= 0)
                SetBindLabel(button, action, index);
            else
            {
                string composite = CompositeLabel(action, device);
                button.GetComponentInChildren<Text>().text = string.IsNullOrEmpty(composite) ? "-" : composite;
                button.interactable = false;
            }
            return button;
        }

        private static string CompositeLabel(InputAction action, string device)
        {
            var names = new List<string>();
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (!b.isPartOfComposite) continue;
                var path = string.IsNullOrEmpty(b.effectivePath) ? b.path : b.effectivePath;
                if (string.IsNullOrEmpty(path) || !path.StartsWith(device)) continue;
                names.Add(action.GetBindingDisplayString(i));
            }
            if (names.Count == 0) return null;
            return names.Count > 2 ? "Arrow Keys" : string.Join(" ", names);
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
