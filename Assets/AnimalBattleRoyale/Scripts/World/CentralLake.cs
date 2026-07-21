using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Shared gameplay description of the central lake.</summary>
    public sealed class CentralLake : MonoBehaviour
    {
        public static CentralLake Instance { get; private set; }

        [SerializeField] private float radius = 31f;
        [SerializeField] private float surfaceHeight = -0.45f;
        [SerializeField] private float movementMultiplier = 0.52f;

        public float Radius => radius;
        public float SurfaceHeight => surfaceHeight;
        public float MovementMultiplier => movementMultiplier;

        private void Awake()
        {
            Instance = this;
        }

        public void Configure(float newRadius, float newSurfaceHeight, float newMovementMultiplier)
        {
            radius = Mathf.Max(1f, newRadius);
            surfaceHeight = newSurfaceHeight;
            movementMultiplier = Mathf.Clamp(newMovementMultiplier, 0.2f, 1f);
        }

        public bool Contains(Vector3 worldPosition)
        {
            Vector2 offset = new Vector2(worldPosition.x - transform.position.x, worldPosition.z - transform.position.z);
            return offset.sqrMagnitude <= radius * radius && worldPosition.y <= surfaceHeight + 1.25f;
        }

        public static bool TryGetWaterAt(Vector3 worldPosition, out float waterSurfaceHeight,
            out float waterMovementMultiplier)
        {
            if (Instance != null && Instance.Contains(worldPosition))
            {
                waterSurfaceHeight = Instance.SurfaceHeight;
                waterMovementMultiplier = Instance.MovementMultiplier;
                return true;
            }

            if (SwampLake.Instance != null && SwampLake.Instance.Contains(worldPosition))
            {
                waterSurfaceHeight = SwampLake.Instance.SurfaceHeight;
                waterMovementMultiplier = SwampLake.Instance.MovementMultiplier;
                return true;
            }

            waterSurfaceHeight = 0f;
            waterMovementMultiplier = 1f;
            return false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    /// <summary>Elliptical gameplay volume for the elongated swamp lake.</summary>
    public sealed class SwampLake : MonoBehaviour
    {
        public static SwampLake Instance { get; private set; }

        [SerializeField] private float halfLength = 38f;
        [SerializeField] private float halfWidth = 14.5f;
        [SerializeField] private float surfaceHeight;
        [SerializeField] private float movementMultiplier = 0.43f;

        public float HalfLength => halfLength;
        public float HalfWidth => halfWidth;
        public float SurfaceHeight => surfaceHeight;
        public float MovementMultiplier => movementMultiplier;

        private void Awake() => Instance = this;

        public void Configure(float newHalfLength, float newHalfWidth, float newSurfaceHeight,
            float newMovementMultiplier)
        {
            halfLength = Mathf.Max(1f, newHalfLength);
            halfWidth = Mathf.Max(1f, newHalfWidth);
            surfaceHeight = newSurfaceHeight;
            movementMultiplier = Mathf.Clamp(newMovementMultiplier, 0.2f, 1f);
        }

        public bool Contains(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            float x = local.x / halfLength;
            float z = local.z / halfWidth;
            return x * x + z * z <= 1f && worldPosition.y <= surfaceHeight + 1.25f;
        }

        public float AmbienceBlendAt(Vector3 worldPosition, float transitionDistance)
        {
            Vector3 local3 = transform.InverseTransformPoint(worldPosition);
            Vector2 local = new Vector2(local3.x, local3.z);
            float normalized = Mathf.Sqrt(local.x * local.x / (halfLength * halfLength)
                                          + local.y * local.y / (halfWidth * halfWidth));
            if (normalized <= 1f) return 1f;

            float angle = Mathf.Atan2(local.y / halfWidth, local.x / halfLength);
            Vector2 edge = new Vector2(Mathf.Cos(angle) * halfLength,
                Mathf.Sin(angle) * halfWidth);
            float distance = Vector2.Distance(local, edge);
            float progress = Mathf.Clamp01(distance / Mathf.Max(1f, transitionDistance));
            return 1f - Mathf.SmoothStep(0f, 1f, progress);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    /// <summary>Small visual movement so the lake does not look like a rigid blue plate.</summary>
    public sealed class LakeWaterMotion : MonoBehaviour
    {
        private Vector3 restPosition;

        private void Awake()
        {
            restPosition = transform.localPosition;
        }

        private void Update()
        {
            transform.localPosition = restPosition + Vector3.up * (Mathf.Sin(Time.time * 0.85f) * 0.035f);
        }
    }
}
