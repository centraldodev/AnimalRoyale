using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    public sealed class AmbientAudioController : MonoBehaviour
    {
        private const string BedResourcePath = "Audio/Ambience/Beds";
        private const string LoopResourcePath = "Audio/Ambience/Loops";
        private const string MusicResourcePath = "Audio/Ambience/Music";

        public static AmbientAudioController Instance { get; private set; }

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float bedVolume = 0.38f;
        [SerializeField, Range(0f, 1f)] private float loopVolume = 0.16f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.2f;
        [SerializeField, Min(0.1f)] private float fadeSeconds = 2.5f;

        [Header("Variation")]
        [SerializeField] private Vector2 bedChangeSeconds = new Vector2(24f, 42f);
        [SerializeField] private Vector2 loopChangeSeconds = new Vector2(10f, 22f);
        [SerializeField, Range(1, 4)] private int shortLoopLayers = 2;

        private readonly List<Coroutine> runningRoutines = new List<Coroutine>();
        private readonly List<AudioSource[]> shortLoopSources = new List<AudioSource[]>();
        private AudioSource[] bedSources;
        private AudioSource musicSource;
        private AudioClip[] bedClips;
        private AudioClip[] loopClips;
        private AudioClip[] musicClips;
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
            bedSources = CreateSourcePair("JungleAmbienceBed");
            musicSource = CreateMusicSource();
            for (int i = 0; i < shortLoopLayers; i++)
            {
                shortLoopSources.Add(CreateSourcePair($"JungleAmbienceLoop_{i + 1}"));
            }
            LoadClips();
        }

        public void BeginMatch()
        {
            if (matchAmbiencePlaying) return;
            LoadClips();
            if ((bedClips == null || bedClips.Length == 0) && (loopClips == null || loopClips.Length == 0)
                && (musicClips == null || musicClips.Length == 0))
            {
                Debug.LogWarning("No ambient audio clips found in Resources/Audio/Ambience.");
                return;
            }

            matchAmbiencePlaying = true;

            if (musicClips != null && musicClips.Length > 0)
            {
                musicSource.clip = PickClip(musicClips, null);
                musicSource.volume = musicVolume;
                musicSource.Play();
                if (ShouldPauseForApplication) musicSource.Pause();
            }

            if (bedClips != null && bedClips.Length > 0)
            {
                runningRoutines.Add(StartCoroutine(RunLoopingLayer(bedSources, bedClips, bedVolume, bedChangeSeconds, 0f)));
            }

            if (loopClips == null || loopClips.Length == 0) return;
            int layerCount = Mathf.Min(shortLoopLayers, loopClips.Length, shortLoopSources.Count);
            for (int i = 0; i < layerCount; i++)
            {
                float staggerSeconds = i * 3.5f;
                runningRoutines.Add(StartCoroutine(RunLoopingLayer(shortLoopSources[i], loopClips, loopVolume, loopChangeSeconds, staggerSeconds)));
            }
        }

        public void StopMatchAmbience()
        {
            if (!matchAmbiencePlaying) return;
            matchAmbiencePlaying = false;

            for (int i = 0; i < runningRoutines.Count; i++)
            {
                if (runningRoutines[i] != null) StopCoroutine(runningRoutines[i]);
            }
            runningRoutines.Clear();

            StopSources(bedSources);
            if (musicSource != null)
            {
                musicSource.Stop();
                musicSource.clip = null;
            }
            for (int i = 0; i < shortLoopSources.Count; i++)
            {
                StopSources(shortLoopSources[i]);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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

        private bool ShouldPauseForApplication => !applicationFocused || applicationPaused;

        private void ApplyApplicationAudioState()
        {
            SetSourcesPaused(bedSources, ShouldPauseForApplication);
            SetSourcePaused(musicSource, ShouldPauseForApplication);
            for (int i = 0; i < shortLoopSources.Count; i++)
            {
                SetSourcesPaused(shortLoopSources[i], ShouldPauseForApplication);
            }
        }

        private void LoadClips()
        {
            if (bedClips == null || bedClips.Length == 0) bedClips = Resources.LoadAll<AudioClip>(BedResourcePath);
            if (loopClips == null || loopClips.Length == 0) loopClips = Resources.LoadAll<AudioClip>(LoopResourcePath);
            if (musicClips == null || musicClips.Length == 0) musicClips = Resources.LoadAll<AudioClip>(MusicResourcePath);
        }

        private IEnumerator RunLoopingLayer(AudioSource[] sources, AudioClip[] clips, float targetVolume, Vector2 changeSeconds, float initialDelay)
        {
            if (initialDelay > 0f) yield return new WaitForSeconds(initialDelay);
            if (!matchAmbiencePlaying || clips == null || clips.Length == 0) yield break;

            int activeIndex = 0;
            AudioClip currentClip = PickClip(clips, null);
            PlayLoop(sources[activeIndex], currentClip, 0f);
            yield return Fade(sources[activeIndex], 0f, targetVolume, fadeSeconds);

            while (matchAmbiencePlaying)
            {
                float waitSeconds = Random.Range(Mathf.Min(changeSeconds.x, changeSeconds.y), Mathf.Max(changeSeconds.x, changeSeconds.y));
                yield return new WaitForSeconds(waitSeconds);
                if (!matchAmbiencePlaying) yield break;

                int nextIndex = 1 - activeIndex;
                AudioClip nextClip = PickClip(clips, sources[activeIndex].clip);
                PlayLoop(sources[nextIndex], nextClip, 0f);
                yield return Crossfade(sources[activeIndex], sources[nextIndex], targetVolume, fadeSeconds);
                sources[activeIndex].Stop();
                activeIndex = nextIndex;
            }
        }

        private static AudioClip PickClip(AudioClip[] clips, AudioClip previous)
        {
            if (clips.Length == 1) return clips[0];

            AudioClip selected = previous;
            for (int attempt = 0; attempt < 8 && selected == previous; attempt++)
            {
                selected = clips[Random.Range(0, clips.Length)];
            }
            return selected == previous ? clips[(System.Array.IndexOf(clips, previous) + 1) % clips.Length] : selected;
        }

        private IEnumerator Crossfade(AudioSource from, AudioSource to, float targetVolume, float duration)
        {
            float elapsed = 0f;
            float fromStart = from.volume;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                from.volume = Mathf.Lerp(fromStart, 0f, t);
                to.volume = Mathf.Lerp(0f, targetVolume, t);
                yield return null;
            }

            from.volume = 0f;
            to.volume = targetVolume;
        }

        private static IEnumerator Fade(AudioSource source, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            source.volume = to;
        }

        private void PlayLoop(AudioSource source, AudioClip clip, float volume)
        {
            source.clip = clip;
            source.volume = volume;
            source.pitch = Random.Range(0.96f, 1.04f);
            source.Play();
            if (ShouldPauseForApplication) source.Pause();
        }

        private AudioSource[] CreateSourcePair(string layerName)
        {
            AudioSource[] sources = new AudioSource[2];
            for (int i = 0; i < sources.Length; i++)
            {
                GameObject sourceObject = new GameObject($"{layerName}_{i + 1}");
                sourceObject.transform.SetParent(transform, false);
                AudioSource source = sourceObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.priority = 150;
                source.dopplerLevel = 0f;
                source.rolloffMode = AudioRolloffMode.Linear;
                sources[i] = source;
            }
            return sources;
        }

        private AudioSource CreateMusicSource()
        {
            GameObject sourceObject = new GameObject("JungleMusic");
            sourceObject.transform.SetParent(transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.priority = 160;
            source.dopplerLevel = 0f;
            return source;
        }

        private static void StopSources(AudioSource[] sources)
        {
            if (sources == null) return;
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] == null) continue;
                sources[i].Stop();
                sources[i].clip = null;
                sources[i].volume = 0f;
            }
        }

        private static void SetSourcesPaused(AudioSource[] sources, bool paused)
        {
            if (sources == null) return;
            for (int i = 0; i < sources.Length; i++) SetSourcePaused(sources[i], paused);
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
