using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Runtime feedback: authored animal sounds and floating damage confirmation.</summary>
    public static class CombatFeedback
    {
        private const string SfxResourcePath = "Audio/Sfx";
        private const float CombatSoundInterval = 0.08f;
        private const int MaxConcurrentSoundsPerAnimal = 3;

        private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private static readonly Dictionary<string, AudioClip[]> sfxGroups = new Dictionary<string, AudioClip[]>();
        private static readonly Dictionary<string, AudioClip> lastPlayedClip = new Dictionary<string, AudioClip>();
        private static readonly Dictionary<string, float> nextMixGroupTimes = new Dictionary<string, float>();
        private static readonly Dictionary<AudioSource, string> voiceMixGroups = new Dictionary<AudioSource, string>();
        private static readonly List<AudioSource> voices = new List<AudioSource>();
        private static AudioClip[] allSfxClips;

        public static void PlayBasic(AnimalType type, Vector3 position)
        {
            float volume = type switch
            {
                AnimalType.Ant => 0.36f,
                AnimalType.Monkey => 0.34f,
                AnimalType.Tiger => 0.3f,
                AnimalType.Eagle => 0.32f,
                _ => 0.34f
            };
            if (PlayAuthored(position, "basic_" + type, BasicKeywords(type), volume, 0.94f, 1.06f,
                    28f, 0.85f, CombatMixGroup(type), CombatSoundInterval, MaxConcurrentSoundsPerAnimal)) return;

            float tone = type switch
            {
                AnimalType.Ant => 130f,
                AnimalType.Monkey => 105f,
                AnimalType.Tiger => 82f,
                AnimalType.Eagle => 260f,
                _ => 160f
            };
            PlayProcedural(position, "basic_" + type, tone, 0.17f, volume, 0.18f);
        }

        public static void PlayPower(AnimalType type, int slot, Vector3 position)
        {
            float volume = type switch
            {
                AnimalType.Ant => 0.48f,
                AnimalType.Monkey => 0.46f,
                AnimalType.Tiger => 0.45f,
                AnimalType.Eagle => 0.44f,
                _ => 0.46f
            };
            if (PlayAuthored(position, "power_" + type + "_" + slot, PowerKeywords(type, slot), volume, 0.96f, 1.04f,
                    32f, 0.85f, CombatMixGroup(type), CombatSoundInterval, MaxConcurrentSoundsPerAnimal)) return;

            float tone = type switch
            {
                AnimalType.Ant => new[] { 220f, 75f, 180f }[slot],
                AnimalType.Monkey => new[] { 310f, 90f, 150f }[slot],
                AnimalType.Tiger => new[] { 170f, 240f, 65f }[slot],
                AnimalType.Eagle => new[] { 380f, 230f, 290f }[slot],
                _ => 160f
            };
            PlayProcedural(position, "power_" + type + "_" + slot, tone, 0.32f, volume, 0.24f);
        }

        public static void NotifyHit(AnimalType attacker, Vector3 position, float damage)
        {
            Color color = attacker switch
            {
                AnimalType.Ant => new Color(1f, 0.36f, 0.08f),
                AnimalType.Monkey => new Color(1f, 0.82f, 0.16f),
                AnimalType.Tiger => new Color(1f, 0.22f, 0.04f),
                AnimalType.Eagle => new Color(0.7f, 0.94f, 1f),
                _ => Color.white
            };
            AttackVfx.CreateHitSpark(position + Vector3.up * 0.8f, color);
            DamageNumber.Create(position + Vector3.up * 1.4f, Mathf.RoundToInt(damage), color);
            const float hitVolume = 0.24f;
            if (PlayAuthored(position, "hit_" + attacker, HitKeywords(attacker), hitVolume, 0.92f, 1.08f,
                    24f, 0.9f, CombatMixGroup(attacker), CombatSoundInterval, MaxConcurrentSoundsPerAnimal)) return;
            PlayProcedural(position, "hit_" + attacker, 330f, 0.1f, hitVolume, 0.12f);
        }

        public static void PlayFoodPickup(Vector3 position)
        {
            PlayProcedural(position, "pickup_health", 520f, 0.24f, 0.5f, 0.05f);
        }

        public static void PlayDiamond(Vector3 position)
        {
            PlayProcedural(position, "objective_diamond", 760f, 0.32f, 0.62f, 0.025f);
        }

        public static void PlayPortal(Vector3 position)
        {
            PlayProcedural(position, "objective_portal", 420f, 0.72f, 0.82f, 0.04f);
        }

        private static bool PlayAuthored(Vector3 position, string key, string[] keywords, float volume, float minPitch, float maxPitch,
            float maxDistance, float spatialBlend, string mixGroup, float minInterval, int maxConcurrent)
        {
            AudioClip[] group = GetSfxGroup(key, keywords);
            if (group.Length == 0) return false;

            if (!string.IsNullOrEmpty(mixGroup))
            {
                if (nextMixGroupTimes.TryGetValue(mixGroup, out float nextAllowedAt) && Time.time < nextAllowedAt) return true;
                if (maxConcurrent > 0 && CountPlayingInMixGroup(mixGroup) >= maxConcurrent) return true;
                nextMixGroupTimes[mixGroup] = Time.time + minInterval;
            }

            AudioClip clip = PickClip(key, group);
            AudioSource source = GetAvailableVoice();
            if (string.IsNullOrEmpty(mixGroup)) voiceMixGroups.Remove(source);
            else voiceMixGroups[source] = mixGroup;
            source.gameObject.name = "CombatSound_" + key;
            source.transform.position = position;
            source.clip = clip;
            source.volume = volume;
            source.pitch = Random.Range(minPitch, maxPitch);
            source.loop = false;
            source.spatialBlend = spatialBlend;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = maxDistance;
            source.Play();
            return true;
        }

        private static int CountPlayingInMixGroup(string mixGroup)
        {
            int count = 0;
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                AudioSource voice = voices[i];
                if (voice == null)
                {
                    voices.RemoveAt(i);
                    continue;
                }
                if (voice.isPlaying && voiceMixGroups.TryGetValue(voice, out string voiceGroup) && voiceGroup == mixGroup) count++;
            }
            return count;
        }

        private static string CombatMixGroup(AnimalType type)
        {
            return type switch
            {
                AnimalType.Ant => "combat_ant",
                AnimalType.Monkey => "combat_monkey",
                AnimalType.Tiger => "combat_tiger",
                AnimalType.Eagle => "combat_eagle",
                _ => "combat_other"
            };
        }

        private static AudioClip[] GetSfxGroup(string key, string[] keywords)
        {
            if (sfxGroups.TryGetValue(key, out AudioClip[] group)) return group;
            if (allSfxClips == null) allSfxClips = Resources.LoadAll<AudioClip>(SfxResourcePath);

            List<AudioClip> matches = new List<AudioClip>();
            for (int i = 0; i < allSfxClips.Length; i++)
            {
                AudioClip clip = allSfxClips[i];
                if (clip == null) continue;
                string clipName = clip.name.ToLowerInvariant();
                for (int keywordIndex = 0; keywordIndex < keywords.Length; keywordIndex++)
                {
                    if (!clipName.Contains(keywords[keywordIndex])) continue;
                    matches.Add(clip);
                    break;
                }
            }

            group = matches.ToArray();
            sfxGroups[key] = group;
            return group;
        }

        private static AudioClip PickClip(string key, AudioClip[] group)
        {
            if (group.Length == 1) return group[0];
            lastPlayedClip.TryGetValue(key, out AudioClip previous);
            AudioClip selected = previous;
            for (int attempt = 0; attempt < 8 && selected == previous; attempt++)
            {
                selected = group[Random.Range(0, group.Length)];
            }
            if (selected == previous)
            {
                int previousIndex = System.Array.IndexOf(group, previous);
                selected = group[(previousIndex + 1) % group.Length];
            }
            lastPlayedClip[key] = selected;
            return selected;
        }

        private static string[] BasicKeywords(AnimalType type)
        {
            return type switch
            {
                AnimalType.Ant => new[] { "giant_ant_mandibles" },
                AnimalType.Monkey => new[] { "monkey_hands" },
                AnimalType.Tiger => new[] { "large_tiger_claws" },
                AnimalType.Eagle => new[] { "powerful_eagle_wings" },
                _ => new string[0]
            };
        }

        private static string[] PowerKeywords(AnimalType type, int slot)
        {
            return type switch
            {
                AnimalType.Ant => new[] { "giant_ant", "insect", "sharp_crea" },
                AnimalType.Monkey => new[] { "monkey_swinging", "monkey_releasing", "agile_monkey_jumping", "light_monkey_landing" },
                AnimalType.Tiger when slot == 0 => new[] { "short_powerful_tiger", "powerful_tiger_launc", "large_tiger_crouchin" },
                AnimalType.Tiger => new[] { "large_tiger_crouchin", "heavy_tiger_landing" },
                AnimalType.Eagle => new[] { "giant_eagle", "large_eagle_flying", "powerful_eagle_wings", "large_eagle_gliding" },
                _ => new string[0]
            };
        }

        private static string[] HitKeywords(AnimalType type)
        {
            return type switch
            {
                AnimalType.Ant => new[] { "small_but_sharp_crea" },
                AnimalType.Monkey => new[] { "light_monkey_landing" },
                AnimalType.Tiger => new[] { "heavy_tiger_landing" },
                AnimalType.Eagle => new[] { "a_giant_eagle_rapidl" },
                _ => new string[0]
            };
        }

        private static void PlayProcedural(Vector3 position, string key, float tone, float duration, float volume, float noise)
        {
            if (!clips.TryGetValue(key, out AudioClip clip))
            {
                clip = CreateClip(key, tone, duration, noise);
                clips[key] = clip;
            }

            AudioSource source = GetAvailableVoice();
            source.gameObject.name = "CombatSound_" + key;
            source.transform.position = position;
            source.clip = clip;
            source.volume = volume;
            source.pitch = 1f;
            source.loop = false;
            voiceMixGroups.Remove(source);
            source.spatialBlend = 0.55f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 38f;
            source.Play();
        }

        private static AudioSource GetAvailableVoice()
        {
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                AudioSource voice = voices[i];
                if (voice == null)
                {
                    voices.RemoveAt(i);
                    continue;
                }
                if (!voice.isPlaying) return voice;
            }

            GameObject sound = new GameObject("CombatSoundVoice");
            AudioSource source = sound.AddComponent<AudioSource>();
            source.playOnAwake = false;
            voices.Add(source);
            return source;
        }

        private static AudioClip CreateClip(string key, float tone, float duration, float noise)
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[sampleCount];
            int seed = key.GetHashCode();
            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Clamp01(1f - time / duration);
                envelope *= envelope;
                float sine = Mathf.Sin(time * tone * Mathf.PI * 2f);
                seed = unchecked(seed * 1103515245 + 12345);
                float crackle = ((seed >> 16) & 0x7fff) / 16384f - 1f;
                data[i] = (sine * (1f - noise) + crackle * noise) * envelope * 0.7f;
            }
            AudioClip clip = AudioClip.Create(key, sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    public sealed class DamageNumber : MonoBehaviour
    {
        private TextMesh textMesh;
        private float expiresAt;
        private Color color;

        public static void Create(Vector3 position, int damage, Color color)
        {
            GameObject popup = new GameObject("Damage_" + damage);
            popup.transform.position = position;
            DamageNumber number = popup.AddComponent<DamageNumber>();
            number.color = color;
            number.expiresAt = Time.time + 0.7f;
            number.textMesh = popup.AddComponent<TextMesh>();
            number.textMesh.text = "-" + damage;
            number.textMesh.anchor = TextAnchor.MiddleCenter;
            number.textMesh.alignment = TextAlignment.Center;
            number.textMesh.characterSize = 0.075f;
            number.textMesh.fontSize = 64;
            number.textMesh.color = color;
        }

        private void Update()
        {
            transform.position += Vector3.up * (1.35f * Time.deltaTime);
            if (Camera.main != null) transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
            float progress = Mathf.InverseLerp(expiresAt - 0.7f, expiresAt, Time.time);
            Color faded = color;
            faded.a = 1f - progress;
            textMesh.color = faded;
            if (Time.time >= expiresAt) Destroy(gameObject);
        }
    }
}
