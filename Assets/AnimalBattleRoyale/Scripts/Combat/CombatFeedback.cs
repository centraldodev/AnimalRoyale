using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Runtime feedback: lightweight procedural sounds and floating damage confirmation.</summary>
    public static class CombatFeedback
    {
        private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private static readonly List<AudioSource> voices = new List<AudioSource>();

        public static void PlayBasic(AnimalType type, Vector3 position)
        {
            float tone = type switch
            {
                AnimalType.Ant => 130f,
                AnimalType.Monkey => 105f,
                AnimalType.Tiger => 82f,
                AnimalType.Eagle => 260f,
                _ => 160f
            };
            Play(position, "basic_" + type, tone, 0.17f, 0.52f, 0.18f);
        }

        public static void PlayPower(AnimalType type, int slot, Vector3 position)
        {
            float tone = type switch
            {
                AnimalType.Ant => new[] { 220f, 75f, 180f }[slot],
                AnimalType.Monkey => new[] { 310f, 90f, 150f }[slot],
                AnimalType.Tiger => new[] { 170f, 240f, 65f }[slot],
                AnimalType.Eagle => new[] { 380f, 230f, 290f }[slot],
                _ => 160f
            };
            Play(position, "power_" + type + "_" + slot, tone, 0.32f, 0.65f, 0.24f);
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
            Play(position, "hit_" + attacker, 330f, 0.1f, 0.38f, 0.12f);
        }

        public static void PlayFoodPickup(Vector3 position)
        {
            Play(position, "pickup_health", 520f, 0.24f, 0.5f, 0.05f);
        }

        public static void PlayDiamond(Vector3 position)
        {
            Play(position, "objective_diamond", 760f, 0.32f, 0.62f, 0.025f);
        }

        public static void PlayPortal(Vector3 position)
        {
            Play(position, "objective_portal", 420f, 0.72f, 0.82f, 0.04f);
        }

        private static void Play(Vector3 position, string key, float tone, float duration, float volume, float noise)
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
            source.spatialBlend = 0.55f;
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
