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
        private JungleGenerator jungle;
        private float matchStartTime;
        private float nextDamageTick;
        private bool matchActive;
        private float acceleratedUntil;
        private float shrinkTimeBonus;

        public Vector3 Center => transform.position;
        public float CurrentRadius { get; private set; }
        public float TimeUntilShrink => matchActive ? Mathf.Max(0f, waitBeforeShrink - (Time.time - matchStartTime)) : waitBeforeShrink;

        private void Awake()
        {
            Instance = this;
            CurrentRadius = initialRadius;
            matchStartTime = Time.time;
            jungle = FindAnyObjectByType<JungleGenerator>();
            CreateLineRenderer();
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
            }

            DrawCircle();

            if (Time.time >= nextDamageTick)
            {
                nextDamageTick = Time.time + 1f;
                DamageOutsidePlayers();
            }
        }

        public void BeginMatch()
        {
            matchStartTime = Time.time;
            CurrentRadius = initialRadius;
            nextDamageTick = Time.time + 1f;
            matchActive = true;
            shrinkTimeBonus = 0f;
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
                    AcidRainEffect.SetActiveFor(fighter, false);
                    continue;
                }

                bool outside = IsOutside(fighter.transform.position);
                AcidRainEffect.SetActiveFor(fighter, outside);
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

            Shader shader = Shader.Find("Sprites/Default");
            lineRenderer.sharedMaterial = new Material(shader);
            lineRenderer.startColor = new Color(0.52f, 1f, 0.08f, 0.95f);
            lineRenderer.endColor = new Color(0.2f, 0.72f, 0.04f, 0.95f);
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
