using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Keeps the gameplay capsule upright regardless of camera pitch, animation
    /// root curves, knockback or ability transitions. Only heading around world Y
    /// is allowed; position and all internal bone animation remain untouched.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class AnimalOrientationStabilizer : MonoBehaviour
    {
        private float stableYaw;

        private void Awake()
        {
            CaptureCurrentHeading();
        }

        private void OnEnable()
        {
            CaptureCurrentHeading();
        }

        public void CaptureCurrentHeading()
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (IsFinite(flatForward) && flatForward.sqrMagnitude > 0.0001f)
            {
                stableYaw = Mathf.Atan2(flatForward.x, flatForward.z) * Mathf.Rad2Deg;
            }
        }

        private void LateUpdate()
        {
            Quaternion current = transform.rotation;
            if (IsFinite(current))
            {
                Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (IsFinite(flatForward) && flatForward.sqrMagnitude > 0.0001f)
                {
                    stableYaw = Mathf.Atan2(flatForward.x, flatForward.z) * Mathf.Rad2Deg;
                }
            }

            transform.rotation = Quaternion.Euler(0f, stableYaw, 0f);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y)
                   && float.IsFinite(value.z) && float.IsFinite(value.w);
        }
    }
}
