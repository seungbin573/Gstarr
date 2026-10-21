using Gstarr.Rhythm;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gstarr.Game
{
    /// <summary>
    /// 메뉴 키 (프로콘 + / Esc) 로 여는 일시정지 창: RESUME / SETTINGS / MAIN MENU.
    /// 음악과 박자 시계도 같이 멈춘다.
    /// </summary>
    [RequireComponent(typeof(RhythmConductor))]
    public class PauseMenu : MonoBehaviour
    {
        private RhythmConductor conductor;
        private RectTransform panel;
        private RectTransform listPanel;
        private SettingsView settings;
        private Button resumeButton;
        private InputAction menuAction;
        private int openedFrame = -1;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            conductor = GetComponent<RhythmConductor>();

            menuAction = new InputAction("Menu", InputActionType.Button);
            menuAction.AddBinding("<Gamepad>/start");
            menuAction.AddBinding("<Keyboard>/escape");
            menuAction.AddBinding("<Keyboard>/p");

            var canvas = UIFactory.CreateCanvas(transform, "PauseCanvas", 130);
            panel = UIFactory.Stretch(UIFactory.CreateRect("Pause", canvas.transform));
            var dim = UIFactory.CreateImage("Dim", panel, new Color(0f, 0f, 0f, 0.7f));
            UIFactory.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            listPanel = UIFactory.Stretch(UIFactory.CreateRect("List", panel));
            var head = UIFactory.CreateText("Head", listPanel, "PAUSED", 110, style: FontStyle.BoldAndItalic);
            UIFactory.Place(head.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1000f, 150f));

            var list = UIFactory.Place(UIFactory.CreateRect("Buttons", listPanel), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(440f, 320f));
            UIFactory.AddVerticalLayout(list, 22f);
            resumeButton = UIFactory.CreateButton("Resume", list, "RESUME", 42, Close);
            UIFactory.SetHeight(resumeButton, 84f);
            UIFactory.SetHeight(UIFactory.CreateButton("Settings", list, "SETTINGS", 42, OpenSettings), 84f);
            UIFactory.SetHeight(UIFactory.CreateButton("Menu", list, "MAIN MENU", 42, StageSession.ToMainMenu), 84f);

            settings = SettingsView.Create(panel, CloseSettings, withBackground: true);
            settings.Panel.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
        }

        private void OnEnable() => menuAction.Enable();
        private void OnDisable() => menuAction.Disable();
        private void OnDestroy() => menuAction.Dispose();

        private void Update()
        {
            if (conductor.IsFinished) return;
            bool menu = menuAction.WasPressedThisFrame();

            if (!IsOpen)
            {
                if (menu) Open();
                return;
            }
            if (Time.frameCount == openedFrame || settings.IsRebinding) return;

            bool cancel = UIFactory.Cancel.WasPressedThisFrame();
            if (settings.Panel.gameObject.activeSelf)
            {
                if (cancel) CloseSettings();
                else if (menu) Close();
            }
            else if (menu || cancel)
            {
                Close();
            }
        }

        public void Open()
        {
            IsOpen = true;
            openedFrame = Time.frameCount;
            GamePause.IsPaused = true;
            Time.timeScale = 0f;
            conductor.Pause();
            UIFactory.EnsureEventSystem();
            panel.gameObject.SetActive(true);
            listPanel.gameObject.SetActive(true);
            settings.Panel.gameObject.SetActive(false);
            UIFactory.Select(resumeButton);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GameSettings.Save();
            panel.gameObject.SetActive(false);
            Time.timeScale = 1f;
            GamePause.IsPaused = false;
            conductor.Resume();
        }

        private void OpenSettings()
        {
            listPanel.gameObject.SetActive(false);
            settings.Panel.gameObject.SetActive(true);
            UIFactory.Select(settings.FirstSelectable);
        }

        private void CloseSettings()
        {
            GameSettings.Save();
            settings.Panel.gameObject.SetActive(false);
            listPanel.gameObject.SetActive(true);
            UIFactory.Select(resumeButton);
        }
    }
}
