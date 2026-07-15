using UnityEngine;

namespace AnimalBattleRoyale
{
    public sealed class SafeZoneController : MonoBehaviour
    {
        public static SafeZoneController Instance { get; private set; }

        [SerializeField] private float initialRadius = 162f;
        [SerializeField] private float finalRadius = 14f;
        [SerializeField] private float waitBeforeShrink = 35f;
        [SerializeField] private float totalShrinkDuration = 240f;
        [SerializeField] private float outsideDamagePerSecond = 10f;
        [SerializeField] private int circleSegments = 96;

        private LineRenderer lineRenderer;
        private ParticleSystem boundaryFire;
        private JungleGenerator jungle;
        private float matchStartTime;
        private float nextDamageTick;
        private bool matchActive;
        private float acceleratedUntil;
        private float shrinkTimeBonus;
        private Vector3 startCenter;
        private Vector3 finalCenter;

        public Vector3 Center => transform.position;
        public float CurrentRadius { get; private set; }
        public float TimeUntilShrink => matchActive ? Mathf.Max(0f, waitBeforeShrink - (Time.time - matchStartTime)) : waitBeforeShrink;

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
        }

        private void Update()
        {
            if (!matchActive)
            {
                DrawCircle();
                return;
            }
            if (Time.time < acceleratedUntil) shrinkTimeBonus += Time.deltaTime * 1.4f;
            float elapsed = Time.time - matchStartTime + shrinkTimeBonus;
            if (elapsed > waitBeforeShrink)
            {
                float progress = Mathf.Clamp01((elapsed - waitBeforeShrink) / totalShrinkDuration);
                CurrentRadius = Mathf.Lerp(initialRadius, finalRadius, progress);
                float centerProgress = Mathf.SmoothStep(0f, 1f, progress);
                transform.position = Vector3.Lerp(startCenter, finalCenter, centerProgress);
            }

            DrawCircle();
            EmitBoundaryFire();

            if (Time.time >= nextDamageTick)
            {
                nextDamageTick = Time.time + 1f;
                DamageOutsidePlayers();
            }
        }

        public void BeginMatch()
        {
            startCenter = transform.position;
            matchStartTime = Time.time;
            CurrentRadius = initialRadius;
            nextDamageTick = Time.time + 1f;
            matchActive = true;
            acceleratedUntil = 0f;
            shrinkTimeBonus = 0f;
        }

        public void SetFinalCenter(Vector3 worldPosition)
        {
            finalCenter = new Vector3(worldPosition.x, 0f, worldPosition.z);
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

        private void DamageOutsidePlayers()
        {
            BattleRoyaleManager manager = BattleRoyaleManager.Instance;
            if (manager == null) return;

            foreach (ThirdPersonAnimalController fighter in manager.Fighters)
            {
                if (fighter == null) continue;
                if (fighter.Health.IsDead)
                {
                    WildfireEffect.SetActiveFor(fighter, false);
                    continue;
                }

                bool outside = IsOutside(fighter.transform.position);
                WildfireEffect.SetActiveFor(fighter, outside);
                if (outside)
                {
                    fighter.Health.TakeDamage(outsideDamagePerSecond);
                }
            }
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
            main.maxParticles = 620;
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
        }

        private void EmitBoundaryFire()
        {
            if (!matchActive || boundaryFire == null) return;
            int emitCount = Mathf.Clamp(Mathf.RoundToInt(CurrentRadius / 34f), 2, 5);
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
