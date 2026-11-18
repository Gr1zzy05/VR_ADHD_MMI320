using UnityEngine;
using UnityEngine.XR;

namespace HomeTasks
{
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerModeRouter : MonoBehaviour
    {
        public GameObject questRig;
        public GameObject desktopRig;
        public GameObject worldTaskBoard;
        public GameObject armTaskBoard;
        public bool IsVR { get; private set; }

        void Awake()
        {
            SetVRMode(Application.platform == RuntimePlatform.Android || XRSettings.isDeviceActive);
        }

        public void SetVRMode(bool useVR)
        {
            IsVR = useVR;
            if (questRig != null) questRig.SetActive(useVR);
            if (desktopRig != null) desktopRig.SetActive(!useVR);
            if (worldTaskBoard != null) worldTaskBoard.SetActive(!useVR);
            if (armTaskBoard != null) armTaskBoard.SetActive(useVR);
            var activeRig = useVR ? questRig : desktopRig;
            var activeCamera = activeRig != null ? activeRig.GetComponentInChildren<Camera>(true) : null;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas.gameObject.scene == gameObject.scene && canvas.renderMode == RenderMode.WorldSpace)
                    canvas.worldCamera = activeCamera;
            if (useVR) { Cursor.lockState = CursorLockMode.None; Cursor.visible = false; }
        }
    }
}
