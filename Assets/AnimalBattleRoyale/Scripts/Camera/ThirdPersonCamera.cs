using UnityEngine;

namespace AnimalBattleRoyale
{
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 6.2f;
        [SerializeField] private float targetHeight = 1.25f;
        [SerializeField] private float sensitivity = 0.16f;
        [SerializeField] private float smoothTime = 0.06f;
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private float minPitch = -25f;
        [SerializeField] private float maxPitch = 70f;

        private float yaw;
        private float pitch = 18f;
        private Vector3 smoothVelocity;
        private readonly RaycastHit[] collisionHits = new RaycastHit[32];

        public Transform Target => target;

        private void Start()
        {
            yaw = transform.eulerAngles.y;
            LockCursor(true);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if (GameInput.EscapePressed())
            {
                LockCursor(Cursor.lockState != CursorLockMode.Locked);
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = GameInput.ReadLook();
                yaw += look.x * sensitivity;
                pitch -= look.y * sensitivity;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focusPoint = target.position + Vector3.up * targetHeight;
            Vector3 desiredPosition = focusPoint - rotation * Vector3.forward * distance;
            desiredPosition = ResolveCollision(focusPoint, desiredPosition);

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref smoothVelocity, smoothTime);
            transform.rotation = rotation;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private Vector3 ResolveCollision(Vector3 origin, Vector3 desiredPosition)
        {
            Vector3 direction = desiredPosition - origin;
            float desiredDistance = direction.magnitude;
            if (desiredDistance <= 0.01f) return desiredPosition;

            int hitCount = Physics.SphereCastNonAlloc(origin, collisionRadius, direction.normalized,
                collisionHits, desiredDistance, ~0, QueryTriggerInteraction.Ignore);

            float nearestDistance = desiredDistance;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = collisionHits[i];
                if (hit.transform == target || hit.transform.IsChildOf(target)) continue;
                nearestDistance = Mathf.Min(nearestDistance, Mathf.Max(0.15f, hit.distance - 0.12f));
            }

            return origin + direction.normalized * nearestDistance;
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
