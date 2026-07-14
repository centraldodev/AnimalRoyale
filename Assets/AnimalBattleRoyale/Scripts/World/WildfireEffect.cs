using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Local flames and embers shown while an animal is outside the safe area.</summary>
    public sealed class WildfireEffect : MonoBehaviour
    {
        private ParticleSystem flames;
        private GameObject fireObject;

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
            main.maxParticles = 110;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.16f, 0.025f, 0.92f),
                new Color(1f, 0.78f, 0.08f, 0.96f));

            ParticleSystem.EmissionModule emission = flames.emission;
            emission.rateOverTime = 58f;
            ParticleSystem.ShapeModule shape = flames.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.25f;
            shape.radiusThickness = 1f;
            fireObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            ParticleSystem.VelocityOverLifetimeModule velocity = flames.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(1.1f, 2.2f);

            ParticleSystem.NoiseModule noise = flames.noise;
            noise.enabled = true;
            noise.strength = 0.32f;
            noise.frequency = 0.75f;

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
            fireObject.SetActive(false);
        }

        private void SetOutside(bool isOutside)
        {
            if (fireObject != null) fireObject.SetActive(isOutside);
        }
    }
}
