using UnityEngine;

namespace AnimalBattleRoyale
{
    public sealed class AmbientAudioController : MonoBehaviour
    {
        private const string DefaultResourcePath = "Audio/World/DefaultAmbience";
        private const string SwampResourcePath = "Audio/World/SwampAmbience";

        public static AmbientAudioController Instance { get; private set; }

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float defaultVolume = 0.38f;
        [SerializeField, Range(0f, 1f)] private float swampVolume = 0.34f;
        [SerializeField, Range(0f, 1f)] private float defaultVolumeInsideSwamp = 0.24f;
        [SerializeField, Min(0.5f)] private float swampTransitionSeconds = 4f;
        [SerializeField, Min(1f)] private float swampApproachDistance = 30f;

        private AudioSource defaultSource;
        private AudioSource swampSource;
        private AudioClip defaultClip;
        private AudioClip swampClip;
        private float swampBlend;
        private bool matchAmbiencePlaying;
        private bool applicationFocused = true;
        private bool applicationPaused;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            defaultSource = CreateSource("DefaultForestAmbience", 150);
            swampSource = CreateSource("SwampAmbience", 145);
            LoadClips();
        }

        public void BeginMatch()
        {
            if (matchAmbiencePlaying) return;

            LoadClips();
            if (defaultClip == null)
            {
                Debug.LogWarning($"Default ambience not found at Resources/{DefaultResourcePath}.");
                return;
            }

            matchAmbiencePlaying = true;
            swampBlend = 0f;
            PlayLoop(defaultSource, defaultClip, defaultVolume);

            if (swampClip != null) PlayLoop(swampSource, swampClip, 0f);
        }

        public void StopMatchAmbience()
        {
            if (!matchAmbiencePlaying) return;

            matchAmbiencePlaying = false;
            StopSource(defaultSource);
            StopSource(swampSource);
            swampBlend = 0f;
        }

        private void LateUpdate()
        {
            if (!matchAmbiencePlaying) return;

            float targetBlend = 0f;
            ThirdPersonAnimalController player = BattleRoyaleManager.Instance != null
                ? BattleRoyaleManager.Instance.LocalPlayer
                : null;

            if (swampClip != null && player != null && SwampLake.Instance != null)
            {
                targetBlend = SwampLake.Instance.AmbienceBlendAt(
                    player.transform.position,
                    swampApproachDistance);
            }

            swampBlend = Mathf.MoveTowards(
                swampBlend,
                targetBlend,
                Time.unscaledDeltaTime / Mathf.Max(0.5f, swampTransitionSeconds));

            float smoothBlend = Mathf.SmoothStep(0f, 1f, swampBlend);
            defaultSource.volume = defaultVolume
                * Mathf.Lerp(1f, defaultVolumeInsideSwamp, smoothBlend);
            swampSource.volume = swampVolume * smoothBlend;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            applicationFocused = hasFocus;
            ApplyApplicationAudioState();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            applicationPaused = pauseStatus;
            ApplyApplicationAudioState();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private bool ShouldPauseForApplication => !applicationFocused || applicationPaused;

        private void LoadClips()
        {
            if (defaultClip == null) defaultClip = Resources.Load<AudioClip>(DefaultResourcePath);
            if (swampClip == null) swampClip = Resources.Load<AudioClip>(SwampResourcePath);
        }

        private AudioSource CreateSource(string sourceName, int priority)
        {
            GameObject sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.priority = priority;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.volume = 0f;
            return source;
        }

        private void PlayLoop(AudioSource source, AudioClip clip, float volume)
        {
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.Play();
            if (ShouldPauseForApplication) source.Pause();
        }

        private void ApplyApplicationAudioState()
        {
            SetSourcePaused(defaultSource, ShouldPauseForApplication);
            SetSourcePaused(swampSource, ShouldPauseForApplication);
        }

        private static void StopSource(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            source.clip = null;
            source.volume = 0f;
        }

        private static void SetSourcePaused(AudioSource source, bool paused)
        {
            if (source == null || source.clip == null) return;

            if (paused)
            {
                if (source.isPlaying) source.Pause();
            }
            else
            {
                source.UnPause();
            }
        }
    }
}
