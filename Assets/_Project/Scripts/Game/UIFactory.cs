using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Gstarr.Game
{
    /// <summary>메뉴/HUD 를 코드로 만드는 공용 헬퍼.</summary>
    public static class UIFactory
    {
        public static readonly Color ButtonColor = new Color(0.12f, 0.13f, 0.2f, 0.92f);
        public static readonly Color ButtonHighlight = new Color(1f, 0.55f, 0.2f, 1f);
        public static readonly Color PanelColor = new Color(0.05f, 0.05f, 0.09f, 0.85f);

        private static Font font;
        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static InputAction cancelAction;

        /// <summary>뒤로가기 (B / Esc)</summary>
        public static InputAction Cancel
        {
            get
            {
                if (cancelAction == null)
                {
                    cancelAction = new InputAction("MenuCancel", InputActionType.Button);
                    cancelAction.AddBinding("<Gamepad>/buttonSouth");
                    cancelAction.AddBinding("<Keyboard>/escape");
                    cancelAction.AddBinding("<Keyboard>/backspace");
                    cancelAction.Enable();
                }
                return cancelAction;
            }
        }

        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>
        /// 컨트롤러로 UI 를 조작할 수 있게 EventSystem 생성.
        /// 프로콘 기준 A(오른쪽) = 확인, B(아래) = 뒤로.
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = go.GetComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();

            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap("MenuOverride");
            var submit = map.AddAction("Submit", InputActionType.Button, "<Gamepad>/buttonEast");
            submit.AddBinding("<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/space");
            submit.AddBinding("<Keyboard>/z");
            module.submit = InputActionReference.Create(submit);
            map.Enable();
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Text CreateText(string name, Transform parent, string text, int size,
            TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var rt = CreateRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        public static T CreateButton<T>(string name, Transform parent, string label, int fontSize, Action onClick)
            where T : Button
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Color.white;
            var button = rt.gameObject.AddComponent<T>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = Color.Lerp(ButtonColor, ButtonHighlight, 0.5f);
            colors.selectedColor = ButtonHighlight;
            colors.pressedColor = Color.Lerp(ButtonHighlight, Color.white, 0.4f);
            colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            var text = CreateText("Label", rt, label, fontSize);
            Stretch(text.rectTransform);
            return button;
        }

        public static Button CreateButton(string name, Transform parent, string label, int fontSize, Action onClick) =>
            CreateButton<Button>(name, parent, label, fontSize, onClick);

        public static VerticalLayoutGroup AddVerticalLayout(RectTransform rt, float spacing, TextAnchor align = TextAnchor.UpperCenter)
        {
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = align;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static void SetHeight(Component c, float h)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
        }

        public static void Select(Selectable s)
        {
            if (s == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(s.gameObject);
        }
    }

    /// <summary>좌/우 입력으로 값을 바꾸는 설정 줄. 클릭(확인)하면 다음 값.</summary>
    public class OptionRow : Button
    {
        public Text valueText;
        public Func<string> getValue;
        public Action<int> change;

        public override void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right)
            {
                change?.Invoke(eventData.moveDir == MoveDirection.Right ? 1 : -1);
                Refresh();
                return;
            }
            base.OnMove(eventData);
        }

        public void Refresh()
        {
            if (valueText != null && getValue != null) valueText.text = "<  " + getValue() + "  >";
        }
    }
}
