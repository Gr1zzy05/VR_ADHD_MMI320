using System.Collections;
using UnityEngine;

namespace HomeTasks
{
    public static class SceneFade
    {
        public const float Duration = 0.75f;

        public static IEnumerator FadeOut() => Fade(0, 1);
        public static IEnumerator FadeIn() => Fade(1, 0);

        static IEnumerator Fade(float from, float to)
        {
            var camera = Camera.main;
            if (camera == null) yield break;
            var go = new GameObject("Scene fade", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(camera.transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.sortingOrder = short.MaxValue;
            canvas.worldCamera = camera;
            if (camera.stereoEnabled || Application.platform == RuntimePlatform.Android)
            {
                // A camera-attached plane covers both eyes, including the headset's periphery.
                canvas.renderMode = RenderMode.WorldSpace;
                var rect = (RectTransform)go.transform;
                rect.sizeDelta = Vector2.one * 4;
                rect.localPosition = new Vector3(0, 0, camera.nearClipPlane + 0.01f);
            }
            else canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var imageObject = new GameObject("Black", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var imageRect = (RectTransform)imageObject.transform;
            imageRect.SetParent(go.transform, false);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = imageRect.offsetMax = Vector2.zero;
            var image = imageObject.GetComponent<UnityEngine.UI.Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = from;
            for (var elapsed = 0f; elapsed < Duration; elapsed += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, elapsed / Duration);
                yield return null;
            }
            group.alpha = to;
            if (to == 0) Object.Destroy(go);
        }
    }
}
