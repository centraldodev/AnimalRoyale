using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Runtime feedback using only the current generic SFX bank.</summary>
    public static class CombatFeedback
    {
        private const string SfxRoot = "Audio/SFX/";
        private const int MaxVoices = 18;

        private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private static readonly Dictionary<string, float> nextMixGroupTimes = new Dictionary<string, float>();
        private static readonly Dictionary<AudioSource, string> voiceMixGroups = new Dictionary<AudioSource, string>();
        private static readonly List<AudioSource> voices = new List<AudioSource>();
        private static int nextVoiceToReuse;

        // Melee has no dedicated sound in the new bank; its hit is heard on contact.
        public static void PlayBasic(AnimalType type, Vector3 position) { }

        public static void PlayPower(AnimalType type, int slot, Vector3 position)
        {
            if (type == AnimalType.Tiger) PlayTigerPounce(position);
            else if (type == AnimalType.Monkey) PlayMonkeyVine(position);
            else if (type == AnimalType.Eagle) PlayEagleFlight(position);
            else if (type == AnimalType.Cow) PlayCowCharge(position);
        }

        public static void NotifyHit(AnimalType attacker, Vector3 position, float damage)
        {
            Color color = attacker switch
            {
                AnimalType.Tiger => new Color(1f, 0.22f, 0.04f),
                AnimalType.Ant => new Color(0.86f, 0.36f, 0.14f),
                AnimalType.Eagle => new Color(0.9f, 0.82f, 0.6f),
                AnimalType.Monkey => new Color(0.6f, 0.4f, 0.22f),
                AnimalType.Cow => RangedProjectile.MilkColor,
                _ => Color.white
            };
            AttackVfx.CreateHitSpark(position + Vector3.up * 0.8f, color);
            DamageNumber.Create(position + Vector3.up * 1.4f, Mathf.RoundToInt(damage), color);
        }

        public static void PlayFootstep(Vector3 position) =>
            PlaySfx(position, "Footstep", 0.22f, 0.94f, 1.06f, 24f, 0.9f, 0.07f, 6);

        public static void PlayLakeFootstep(Vector3 position) =>
            PlaySfx(position, "LakeFootstep", 0.26f, 0.94f, 1.06f, 24f, 0.9f, 0.07f, 6);

        public static void PlayJump(Vector3 position) =>
            PlaySfx(position, "LongJump", 0.42f, 0.96f, 1.04f, 32f, 0.85f, 0.14f, 3);

        public static void PlayTigerPounce(Vector3 position) =>
            PlaySfx(position, "AbilityTigerQ", 0.52f, 0.98f, 1.02f, 42f, 0.85f, 0.14f, 2);

        public static void PlayMonkeyVine(Vector3 position) =>
            PlaySfx(position, "AbilityMonkeyQ", 0.5f, 0.98f, 1.02f, 42f, 0.85f, 0.12f, 2);

        public static void PlayEagleFlight(Vector3 position) =>
            PlaySfx(position, "EagleFlight", 0.48f, 0.97f, 1.03f, 40f, 0.9f, 0.4f, 2);

        public static void PlayCowCharge(Vector3 position) =>
            PlaySfx(position, "AbilityCowQ", 0.54f, 0.98f, 1.02f, 44f, 0.9f, 1.5f, 2);

        public static void PlayCowImpact(Vector3 position) =>
            PlaySfx(position, "CowImpact", 0.55f, 0.95f, 1.05f, 34f, 0.9f, 0.08f, 4);

        public static void PlayProjectileLaunch(Vector3 position, WeaponAmmoType ammoType)
        {
            switch (ammoType)
            {
                case WeaponAmmoType.Tomato:
                    PlaySfx(position, "AmmoShotTomato", 0.24f, 0.98f, 1.02f,
                        34f, 0.9f, 0.055f, 6);
                    break;
                case WeaponAmmoType.Watermelon:
                    PlaySfx(position, "AmmoShotWatermelon", 0.32f, 0.98f, 1.02f,
                        38f, 0.9f, 0.1f, 4);
                    break;
                default:
                    PlaySfx(position, "AmmoShotSeed", 0.28f, 0.98f, 1.02f,
                        36f, 0.9f, 0.055f, 6);
                    break;
            }
        }

        public static void PlaySeedShot(Vector3 position) =>
            PlayProjectileLaunch(position, WeaponAmmoType.Seed);

        public static void PlayProjectileFly(Vector3 position) =>
            PlaySfx(position, "ProjectileFly", 0.2f, 0.98f, 1.04f, 30f, 1f, 0.18f, 3);

        public static void PlayProjectileImpact(Vector3 position, WeaponAmmoType ammoType)
        {
            switch (ammoType)
            {
                case WeaponAmmoType.Tomato:
                    PlaySfx(position, "TomatoBurst", 0.56f, 0.98f, 1.025f, 42f, 1f, 0.07f, 4);
                    break;
                case WeaponAmmoType.Watermelon:
                    PlaySfx(position, "WatermelonBurst", 0.68f, 0.97f, 1.015f, 52f, 1f, 0.12f, 3);
                    break;
                default:
                    PlaySfx(position, "ProjectileImpact", 0.34f, 0.96f, 1.04f, 36f, 1f, 0.055f, 6);
                    break;
            }
        }

        public static void PlayProjectileImpact(Vector3 position) =>
            PlayProjectileImpact(position, WeaponAmmoType.Seed);

        public static void PlayPlayerHit(Vector3 position) =>
            PlaySfx(position, "PlayerHit", 0.38f, 0.95f, 1.05f, 32f, 0.9f, 0.075f, 5);

        public static void PlayAnimalHit(Vector3 position) =>
            PlaySfx(position, "AnimalHit", 0.38f, 0.95f, 1.05f, 32f, 0.9f, 0.075f, 5);

        public static void PlayPlayerDeath(Vector3 position) =>
            PlaySfx(position, "PlayerDeath", 0.52f, 0.98f, 1.02f, 44f, 0.85f, 0.12f, 3);

        public static void PlayAmmoPickup(Vector3 position) =>
            PlaySfx(position, "AmmoPickup", 0.5f, 0.98f, 1.03f, 30f, 0.72f, 0.12f, 2);

        public static void PlayWeaponReload(Vector3 position, WeaponAmmoType ammoType)
        {
            string key = ammoType switch
            {
                WeaponAmmoType.Tomato => "AmmoReloadTomato",
                WeaponAmmoType.Watermelon => "AmmoReloadWatermelon",
                _ => "AmmoReloadSeed"
            };
            PlaySfx(position, key, 0.38f, 1f, 1f, 12f, 0.05f, 0.1f, 1);
        }

        // There are no replacement sounds for these events in the new bank.
        public static void PlayDiamond(Vector3 position) { }
        public static void PlayPortal(Vector3 position) { }

        private static void PlaySfx(Vector3 position, string key, float volume, float minPitch, float maxPitch,
            float maxDistance, float spatialBlend, float minInterval, int maxConcurrent)
        {
            if (nextMixGroupTimes.TryGetValue(key, out float nextAllowedAt) && Time.time < nextAllowedAt) return;
            if (maxConcurrent > 0 && CountPlayingInMixGroup(key) >= maxConcurrent) return;

            if (!clips.TryGetValue(key, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>(SfxRoot + key);
                if (clip == null && key == "AmmoPickup") clip = CreatePickupChime();
                clips[key] = clip;
            }
            if (clip == null) return;

            nextMixGroupTimes[key] = Time.time + minInterval;
            AudioSource source = GetAvailableVoice();
            voiceMixGroups[source] = key;
            source.gameObject.name = "GameSfx_" + key;
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
        }

        private static AudioClip CreatePickupChime()
        {
            const int sampleRate = 44100;
            const float duration = 0.32f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float attack = Mathf.Clamp01(time / 0.012f);
                float decay = Mathf.Exp(-8f * time);
                float upwardSweep = Mathf.Lerp(620f, 1040f, time / duration);
                float tone = Mathf.Sin(2f * Mathf.PI * upwardSweep * time)
                             + Mathf.Sin(2f * Mathf.PI * upwardSweep * 1.5f * time) * 0.32f;
                samples[i] = tone * attack * decay * 0.3f;
            }

            AudioClip clip = AudioClip.Create("GeneratedAmmoPickup", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
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

            if (voices.Count >= MaxVoices)
            {
                // Steal whichever voice is closest to finishing anyway, instead of a blind
                // round-robin — otherwise a long clip (e.g. an ability's 3s charge sound)
                // that just started can get cut off by an unrelated one-shot like a footstep.
                int bestIndex = -1;
                float leastRemaining = float.MaxValue;
                for (int i = 0; i < voices.Count; i++)
                {
                    AudioSource candidate = voices[i];
                    if (candidate == null) continue;
                    float remaining = candidate.isPlaying && candidate.clip != null
                        ? candidate.clip.length - candidate.time
                        : 0f;
                    if (remaining >= leastRemaining) continue;
                    leastRemaining = remaining;
                    bestIndex = i;
                }
                AudioSource reused = bestIndex >= 0 ? voices[bestIndex] : voices[nextVoiceToReuse++ % voices.Count];
                reused.Stop();
                return reused;
            }

            GameObject sound = new GameObject("CombatSoundVoice");
            AudioSource source = sound.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.priority = 128;
            voices.Add(source);
            return source;
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
            Transform viewer = CameraCache.MainTransform;
            if (viewer != null) transform.rotation = Quaternion.LookRotation(transform.position - viewer.position);
            float progress = Mathf.InverseLerp(expiresAt - 0.7f, expiresAt, Time.time);
            Color faded = color;
            faded.a = 1f - progress;
            textMesh.color = faded;
            if (Time.time >= expiresAt) Destroy(gameObject);
        }
    }
}
