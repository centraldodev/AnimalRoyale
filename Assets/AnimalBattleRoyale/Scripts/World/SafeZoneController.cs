using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    public sealed class SafeZoneController : MonoBehaviour
    {
        public static SafeZoneController Instance { get; private set; }

        [SerializeField] private float initialRadius = 162f;
        [SerializeField] private float finalRadius = 14f;
        [SerializeField] private int circleSegments = 96;
        [SerializeField, Min(0f)] private float respawnEdgeMargin = 6f;
        [SerializeField, Range(30f, 180f)] private float boundaryParticlesPerSecond = 110f;
        [SerializeField, Range(2f, 40f)] private float fireSoundFadeDistance = 16f;

        private LineRenderer lineRenderer;
        private ParticleSystem boundaryFire;
        private AudioSource fireAudioSource;
        private float fireSoundVolume;
        private JungleGenerator jungle;
        private float matchStartTime;
        private bool matchActive;
        private float acceleratedUntil;
        private Vector3 startCenter;
        private Vector3 finalCenter;
        private float boundaryEmissionAccumulator;
        private readonly HashSet<ThirdPersonAnimalController> fightersOutsideWildfire = new HashSet<ThirdPersonAnimalController>();

        public Vector3 Center => transform.position;
        public float CurrentRadius { get; private set; }
        public float TimeUntilShrink => matchActive
            ? Mathf.Max(0f, ServerGameTuning.SafeZoneWaitBeforeShrink - (Time.time - matchStartTime))
            : ServerGameTuning.SafeZoneWaitBeforeShrink;

        private void Awake()
        {
            Instance = this;
            CurrentRadius = initialRadius;
            matchStartTime = Time.time;
            jungle = FindAnyObjectByType<JungleGenerator>();
            startCenter = transform.position;
            finalCenter = startCenter;
            CreateLineRenderer();
            CreateBoundaryFire();
            CreateFireAudio();
        }

        private void Update()
        {
            if (!matchActive)
            {
                DrawCircle();
                return;
            }
            float elapsed = Time.time - matchStartTime;
            if (elapsed > ServerGameTuning.SafeZoneWaitBeforeShrink)
            {
                float speedMultiplier = Time.time < acceleratedUntil ? 2.4f : 1f;
                CurrentRadius = Mathf.MoveTowards(CurrentRadius, finalRadius,
                    ServerGameTuning.SafeZoneShrinkSpeed * speedMultiplier * Time.deltaTime);
                float progress = Mathf.Clamp01((initialRadius - CurrentRadius) /
                    Mathf.Max(0.01f, initialRadius - finalRadius));
                float centerProgress = Mathf.SmoothStep(0f, 1f, progress);
                transform.position = Vector3.Lerp(startCenter, finalCenter, centerProgress);
            }

            DrawCircle();
            EmitBoundaryFire();
            UpdateFireSound();
            if (OnlineMultiplayerManager.Instance != null && OnlineMultiplayerManager.Instance.IsClientOnly) return;
            UpdateWildfireExposure();
        }

        public void BeginMatch()
        {
            startCenter = transform.position;
            finalCenter = PickRandomFinalCenter();
            matchStartTime = Time.time;
            CurrentRadius = initialRadius;
            matchActive = true;
            acceleratedUntil = 0f;
            boundaryEmissionAccumulator = 0f;
            fireSoundVolume = 0f;
            if (fireAudioSource != null)
            {
                fireAudioSource.volume = 0f;
                fireAudioSource.Play();
            }
            ResetWildfireExposure();
        }

        public void SetFinalCenter(Vector3 worldPosition)
        {
            finalCenter = new Vector3(worldPosition.x, 0f, worldPosition.z);
        }

        // A different spot each match, always fully on dry land, so the zone never
        // closes in the same place (or inside the lake) twice.
        private Vector3 PickRandomFinalCenter()
        {
            if (jungle == null) jungle = FindAnyObjectByType<JungleGenerator>();
            if (jungle == null) return Vector3.zero;

            float maxDistance = jungle.MapSize * 0.5f - finalRadius - 6f;
            float minDistance = jungle.LakeRadius + finalRadius + 4f;
            if (maxDistance <= minDistance) return Vector3.zero;

            float angle = Random.value * Mathf.PI * 2f;
            float distance = Random.Range(minDistance, maxDistance);
            return new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        }

        public void AccelerateShrink(float duration)
        {
            acceleratedUntil = Mathf.Max(acceleratedUntil, Time.time + Mathf.Max(0f, duration));
        }

        public bool IsOutside(Vector3 worldPosition, float margin = 0f)
        {
            Vector2 flatOffset = new Vector2(worldPosition.x - Center.x, worldPosition.z - Center.z);
            return flatOffset.magnitude > CurrentRadius - margin;
        }

        public float GetWildfireSecondsRemaining(ThirdPersonAnimalController fighter)
        {
            if (fighter == null || fighter.Health == null) return 0f;
            return fighter.Health.CurrentHealth / Mathf.Max(0.01f, ServerGameTuning.SafeZoneDamagePerSecond);
        }

        public Vector3 GetRandomRespawnPoint()
        {
            float radius = Mathf.Max(0f, CurrentRadius - respawnEdgeMargin);
            Vector2 offset = Random.insideUnitCircle * radius;
            return Center + new Vector3(offset.x, 0f, offset.y);
        }

        public Vector3 ClampRespawnPoint(Vector3 worldPosition)
        {
            float radius = Mathf.Max(0f, CurrentRadius - respawnEdgeMargin);
            Vector2 offset = new Vector2(worldPosition.x - Center.x, worldPosition.z - Center.z);
            if (offset.sqrMagnitude <= radius * radius) return worldPosition;

            Vector2 clampedOffset = offset.sqrMagnitude > 0.0001f
                ? offset.normalized * radius
                : Vector2.zero;
            return new Vector3(
                Center.x + clampedOffset.x,
                worldPosition.y,
                Center.z + clampedOffset.y);
        }

        private void UpdateWildfireExposure()
        {
            BattleRoyaleManager manager = BattleRoyaleManager.Instance;
            if (manager == null) return;

            foreach (ThirdPersonAnimalController fighter in manager.Fighters)
            {
                if (fighter == null || fighter.Health == null) continue;

                bool outside = !fighter.Health.IsDead && IsOutside(fighter.transform.position);
                bool wasOutside = fightersOutsideWildfire.Contains(fighter);
                if (outside != wasOutside)
                {
                    if (outside) fightersOutsideWildfire.Add(fighter);
                    else fightersOutsideWildfire.Remove(fighter);
                    WildfireEffect.SetActiveFor(fighter, outside);
                }

                if (!outside || !manager.CombatEnabled)
                {
                    continue;
                }

                float healthAfterDamage = fighter.Health.CurrentHealth
                    - ServerGameTuning.SafeZoneDamagePerSecond * Time.deltaTime;
                fighter.Health.ApplyEnvironmentalHealthCeiling(healthAfterDamage);
            }
        }

        private void ResetWildfireExposure()
        {
            foreach (ThirdPersonAnimalController fighter in fightersOutsideWildfire)
            {
                if (fighter != null) WildfireEffect.SetActiveFor(fighter, false);
            }
            fightersOutsideWildfire.Clear();
        }

        private void CreateLineRenderer()
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.loop = true;
            lineRenderer.positionCount = circleSegments;
            lineRenderer.widthMultiplier = 0.22f;
            lineRenderer.useWorldSpace = true;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.numCapVertices = 2;

            Shader shader = ShaderLibrary.Sprite;
            lineRenderer.sharedMaterial = new Material(shader);
            lineRenderer.widthMultiplier = 0.38f;
            lineRenderer.startColor = new Color(1f, 0.72f, 0.06f, 0.98f);
            lineRenderer.endColor = new Color(1f, 0.12f, 0.015f, 0.98f);
        }

        private void CreateBoundaryFire()
        {
            GameObject fireObject = new GameObject("WildfireBoundary");
            fireObject.transform.SetParent(transform, false);
            boundaryFire = fireObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = boundaryFire.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.15f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.24f, 0.72f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.12f, 0.015f, 0.9f),
                new Color(1f, 0.82f, 0.08f, 0.98f));
            main.maxParticles = 240;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = boundaryFire.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = boundaryFire.shape;
            shape.enabled = false;
            ParticleSystem.ColorOverLifetimeModule color = boundaryFire.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.86f, 0.12f), 0f),
                    new GradientColorKey(new Color(1f, 0.16f, 0.02f), 0.58f),
                    new GradientColorKey(new Color(0.12f, 0.025f, 0.01f), 1f)
                },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.72f, 0.68f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            ParticleSystemRenderer renderer = fireObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 4;
            renderer.sharedMaterial = WildfireEffect.SharedParticleMaterial;
        }

        private void CreateFireAudio()
        {
            GameObject audioObject = new GameObject("WildfireSound");
            audioObject.transform.SetParent(transform, false);
            fireAudioSource = audioObject.AddComponent<AudioSource>();
            fireAudioSource.clip = Resources.Load<AudioClip>("Audio/SFX/FireSound");
            fireAudioSource.loop = true;
            fireAudioSource.playOnAwake = false;
            fireAudioSource.spatialBlend = 0f;
            fireAudioSource.volume = 0f;
            fireAudioSource.priority = 140;
        }

        private void EmitBoundaryFire()
        {
            if (!matchActive || boundaryFire == null) return;
            boundaryEmissionAccumulator = Mathf.Min(
                6f,
                boundaryEmissionAccumulator + Time.deltaTime * boundaryParticlesPerSecond);
            int emitCount = Mathf.Min(6, Mathf.FloorToInt(boundaryEmissionAccumulator));
            if (emitCount <= 0) return;
            boundaryEmissionAccumulator -= emitCount;
            for (int i = 0; i < emitCount; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                Vector3 position = Center + new Vector3(Mathf.Cos(angle) * CurrentRadius, 0f, Mathf.Sin(angle) * CurrentRadius);
                position.y = (jungle != null ? jungle.GroundHeightAt(position) : 0f) + 0.18f;
                ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = new Vector3(Random.Range(-0.18f, 0.18f), Random.Range(1.1f, 2.2f), Random.Range(-0.18f, 0.18f)),
                    startSize = Random.Range(0.24f, 0.72f),
                    startLifetime = Random.Range(0.65f, 1.15f)
                };
                boundaryFire.Emit(emit, 1);
            }
        }

        // Non-positional: the danger zone surrounds the player rather than sitting at one
        // point, so the fire crackle fades in globally as they approach the edge from inside
        // and reaches full volume once they step outside.
        private void UpdateFireSound()
        {
            if (fireAudioSource == null) return;
            ThirdPersonAnimalController localPlayer = BattleRoyaleManager.Instance != null
                ? BattleRoyaleManager.Instance.LocalPlayer
                : null;

            float targetVolume = 0f;
            if (localPlayer != null && !localPlayer.IsDefeated && localPlayer.Health != null && !localPlayer.Health.IsDead)
            {
                Vector2 flatOffset = new Vector2(
                    localPlayer.transform.position.x - Center.x,
                    localPlayer.transform.position.z - Center.z);
                float distanceFromEdge = CurrentRadius - flatOffset.magnitude;
                targetVolume = distanceFromEdge <= 0f
                    ? 1f
                    : Mathf.Clamp01(1f - distanceFromEdge / Mathf.Max(0.01f, fireSoundFadeDistance));
            }

            fireSoundVolume = Mathf.MoveTowards(fireSoundVolume, targetVolume, Time.deltaTime * 2.5f);
            fireAudioSource.volume = fireSoundVolume;
        }

        private void DrawCircle()
        {
            if (lineRenderer == null) return;
            for (int i = 0; i < circleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / circleSegments;
                Vector3 position = Center + new Vector3(Mathf.Cos(angle) * CurrentRadius, 0f, Mathf.Sin(angle) * CurrentRadius);
                position.y = (jungle != null ? jungle.GroundHeightAt(position) : 0f) + 0.22f;
                lineRenderer.SetPosition(i, position);
            }
        }
    }
}
