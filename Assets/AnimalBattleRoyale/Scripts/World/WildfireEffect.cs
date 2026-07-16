using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Local flames and embers shown while an animal is outside the safe area.</summary>
    public sealed class WildfireEffect : MonoBehaviour
    {
        private static Material sharedParticleMaterial;
        private static Texture2D sharedParticleTexture;
        private ParticleSystem flames;
        private GameObject fireObject;

        internal static Material SharedParticleMaterial => GetParticleMaterial();

        public static void SetActiveFor(ThirdPersonAnimalController fighter, bool isOutside)
        {
            if (fighter == null) return;
            WildfireEffect effect = fighter.GetComponent<WildfireEffect>();
            if (effect == null && isOutside) effect = fighter.gameObject.AddComponent<WildfireEffect>();
            if (effect != null) effect.SetOutside(isOutside);
        }

        private void Awake()
        {
            fireObject = new GameObject("WildfireFlames");
            fireObject.transform.SetParent(transform, false);
            fireObject.transform.localPosition = Vector3.up * 0.18f;
            flames = fireObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = flames.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 0.82f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.42f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.16f, 0.025f, 0.92f),
                new Color(1f, 0.78f, 0.08f, 0.96f));

            ParticleSystem.EmissionModule emission = flames.emission;
            emission.rateOverTime = 32f;
            ParticleSystem.ShapeModule shape = flames.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.25f;
            shape.radiusThickness = 1f;
            fireObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            ParticleSystem.VelocityOverLifetimeModule velocity = flames.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // Unity requires X, Y and Z velocity curves to use the same mode. Keeping all
            // three constant prevents the per-frame console error that stalled the Editor.
            velocity.x = 0f;
            velocity.y = 1.65f;
            velocity.z = 0f;

            ParticleSystem.NoiseModule noise = flames.noise;
            noise.enabled = true;
            noise.strength = 0.32f;
            noise.frequency = 0.75f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            ParticleSystem.ColorOverLifetimeModule color = flames.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.78f, 0.08f), 0f),
                    new GradientColorKey(new Color(1f, 0.18f, 0.02f), 0.62f),
                    new GradientColorKey(new Color(0.16f, 0.03f, 0.01f), 1f)
                },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.78f, 0.62f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            ParticleSystemRenderer renderer = fireObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 8;
            renderer.sharedMaterial = GetParticleMaterial();
            fireObject.SetActive(false);
        }

        private static Material GetParticleMaterial()
        {
            if (sharedParticleMaterial != null) return sharedParticleMaterial;
            Shader shader = ShaderLibrary.Sprite;
            if (shader == null) return null;
            sharedParticleMaterial = new Material(shader)
            {
                name = "WildfireParticleMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
            sharedParticleMaterial.mainTexture = GetParticleTexture();
            return sharedParticleMaterial;
        }

        private static Texture2D GetParticleTexture()
        {
            if (sharedParticleTexture != null) return sharedParticleTexture;
            const int size = 32;
            sharedParticleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "WildfireParticleTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] pixels = new Color[size * size];
            Vector2 center = Vector2.one * ((size - 1) * 0.5f);
            float radius = size * 0.48f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normalizedDistance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Clamp01((1f - normalizedDistance) * 3.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            sharedParticleTexture.SetPixels(pixels);
            sharedParticleTexture.Apply(false, true);
            return sharedParticleTexture;
        }

        private void SetOutside(bool isOutside)
        {
            if (fireObject != null && fireObject.activeSelf != isOutside) fireObject.SetActive(isOutside);
        }
    }
}
