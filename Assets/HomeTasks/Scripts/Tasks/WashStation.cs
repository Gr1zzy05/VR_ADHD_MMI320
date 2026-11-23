using UnityEngine;

namespace HomeTasks
{
    public sealed class WashStation : MonoBehaviour
    {
        public BoxCollider washVolume;
        public SpongeTool sponge;
        public GameObject waterStream;
        public AudioSource waterAudio;
        public bool WaterOn { get; private set; }
        Vector3 previousSpongePosition;
        AudioClip waterSound;

        void Start()
        {
            previousSpongePosition = sponge.transform.position;
            var samples = new float[24000];
            var random = new System.Random(17);
            var smoothed = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                smoothed = smoothed * 0.5f + ((float)random.NextDouble() * 2 - 1) * 0.5f;
                samples[i] = smoothed * 0.025f;
            }
            waterSound = AudioClip.Create("Running tap", samples.Length, 1, 24000, false);
            waterSound.SetData(samples, 0);
            if (waterAudio != null) { waterAudio.clip = waterSound; waterAudio.loop = true; }
            UpdateWater();
        }

        public void ToggleWater()
        {
            if (HomeSession.Instance != null && !HomeSession.Instance.CanPerform(HomeQuest.Washing, true)) return;
            WaterOn = !WaterOn;
            UpdateWater();
            HomeSession.Instance?.Notify(WaterOn ? GameText.Get("Water on. Rub a dish in the sink with the sponge.") : GameText.Get("Water off."));
        }

        void UpdateWater()
        {
            if (waterStream != null) waterStream.SetActive(WaterOn);
            if (waterAudio == null) return;
            if (WaterOn) waterAudio.Play(); else waterAudio.Stop();
        }

        void Update()
        {
            if (sponge == null) return;
            var speed = Vector3.Distance(sponge.transform.position, previousSpongePosition) / Mathf.Max(Time.deltaTime, 0.0001f);
            previousSpongePosition = sponge.transform.position;
            var session = HomeSession.Instance;
            if (session != null && !session.IsQuestActive(HomeQuest.Washing) && WaterOn)
            {
                WaterOn = false;
                UpdateWater();
            }
            if (!WaterOn || !sponge.IsHeld || session == null) return;
            if (speed < session.settings.minimumScrubSpeed && !sponge.DesktopScrubbing) return;
            foreach (var dish in session.dishes)
                Scrub(dish, Time.deltaTime);
        }

        public bool Scrub(DishItem dish, float seconds)
        {
            if (HomeSession.Instance != null && !HomeSession.Instance.IsQuestActive(HomeQuest.Washing)) return false;
            if (!WaterOn || sponge == null || !sponge.IsHeld || dish == null || dish.IsClean || dish.IsStored) return false;
            if (!TaskVolume.Contains(washVolume, dish.transform.position)) return false;
            var distance = HomeSession.Instance != null ? HomeSession.Instance.settings.spongeContactDistance : 0.16f;
            if (Vector3.Distance(dish.Shape.ClosestPoint(sponge.transform.position), sponge.transform.position) > distance) return false;
            dish.ApplyScrub(seconds);
            return true;
        }

        void OnDestroy() { if (waterSound != null) Destroy(waterSound); }
    }
}
