using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Local toxic rain shown only while an animal is outside the safe area.</summary>
    public sealed class AcidRainEffect : MonoBehaviour
    {
        private ParticleSystem particles;
        private GameObject rainObject;

        public static void SetActiveFor(ThirdPersonAnimalController fighter, bool isOutside)
        {
            if (fighter == null) return;
            AcidRainEffect effect = fighter.GetComponent<AcidRainEffect>();
            if (effect == null && isOutside) effect = fighter.gameObject.AddComponent<AcidRainEffect>();
            if (effect != null) effect.SetOutside(isOutside);
        }

        private void Awake()
        {
            rainObject = new GameObject("AcidRainParticles");
            rainObject.transform.SetParent(transform, false);
            rainObject.transform.localPosition = Vector3.up * 4.2f;
            particles = rainObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = 0.65f;
            main.startSpeed = 8f;
            main.startSize = 0.09f;
            main.maxParticles = 90;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startColor = new Color(0.55f, 1f, 0.08f, 0.9f);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 70f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(4.2f, 0.1f, 4.2f);
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.y = -6f;

            ParticleSystemRenderer renderer = rainObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 3f;
            renderer.velocityScale = 0.25f;
            rainObject.SetActive(false);
        }

        private void SetOutside(bool isOutside)
        {
            if (rainObject != null) rainObject.SetActive(isOutside);
        }
    }
}
