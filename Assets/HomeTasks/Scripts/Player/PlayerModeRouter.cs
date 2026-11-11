using UnityEngine;
using UnityEngine.XR;

namespace HomeTasks
{
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerModeRouter : MonoBehaviour
    {
        public GameObject questRig;
        public GameObject desktopRig;

        void Awake()
        {
            var useVR = Application.platform == RuntimePlatform.Android || XRSettings.isDeviceActive;
            questRig.SetActive(useVR);
            desktopRig.SetActive(!useVR);
            if (useVR) { Cursor.lockState = CursorLockMode.None; Cursor.visible = false; }
        }
    }
}
