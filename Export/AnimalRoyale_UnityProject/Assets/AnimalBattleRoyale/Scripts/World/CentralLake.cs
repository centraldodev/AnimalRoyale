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
