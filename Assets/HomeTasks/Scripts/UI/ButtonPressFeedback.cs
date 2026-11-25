using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HomeTasks
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class ButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        static readonly Color PressFill = new Color32(255, 211, 99, 255);
        static readonly Color PressText = new Color32(21, 42, 35, 255);
        const float FlashSeconds = 0.24f;

        UnityEngine.UI.Button button;
        Graphic background;
        TMP_Text caption;
        Color normalFill;
        Color normalText;
        int? pressedPointer;
        float flashUntil;
        bool showingPress;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterScenes()
        {
            SceneManager.sceneLoaded -= AddToSceneButtons;
            SceneManager.sceneLoaded += AddToSceneButtons;
        }

        static void AddToSceneButtons(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var control in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    if (control.targetGraphic != null && !control.TryGetComponent<ButtonPressFeedback>(out _))
                        control.gameObject.AddComponent<ButtonPressFeedback>();
        }

        void Awake()
        {
            button = GetComponent<UnityEngine.UI.Button>();
            background = button.targetGraphic;
            caption = GetComponentInChildren<TMP_Text>(true);
            if (button.transition == Selectable.Transition.ColorTint)
            {
                var colors = button.colors;
                colors.pressedColor = Color.white;
                button.colors = colors;
            }
        }

        void OnEnable() => button.onClick.AddListener(FlashClick);

        void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(FlashClick);
            Restore();
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !CanPress()) return;
            pressedPointer = data.pointerId;
            ShowPressed();
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left && pressedPointer == data.pointerId) pressedPointer = null;
        }

        public void OnPointerExit(PointerEventData data)
        {
            if (pressedPointer == data.pointerId) pressedPointer = null;
        }

        bool CanPress() => background != null && button.isActiveAndEnabled && button.IsInteractable();

        void FlashClick()
        {
            if (!isActiveAndEnabled || (!showingPress && !CanPress())) return;
            ShowPressed();
            flashUntil = Time.unscaledTime + FlashSeconds;
        }

        void ShowPressed()
        {
            if (showingPress) return;
            showingPress = true;
            normalFill = background.color;
            background.color = PressFill;
            if (caption != null)
            {
                normalText = caption.color;
                caption.color = PressText;
            }
        }

        void LateUpdate()
        {
            if (showingPress && (!pressedPointer.HasValue || !CanPress()) && Time.unscaledTime >= flashUntil) Restore();
        }

        void Restore()
        {
            pressedPointer = null;
            flashUntil = 0;
            if (!showingPress) return;
            showingPress = false;
            if (background != null) background.color = normalFill;
            if (caption != null) caption.color = normalText;
        }
    }
}
