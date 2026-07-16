using UnityEngine;

namespace AnimalBattleRoyale
{
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 4.6f;
        [SerializeField] private float targetHeight = 1.25f;
        [SerializeField] private float shoulderOffset = 1.05f;
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
        // Aim follows the exact center ray of the rendered camera.
        public Vector3 AimDirection => transform.forward;

        private void Start()
        {
            yaw = transform.eulerAngles.y;
            bool resultScreenOpen = BattleRoyaleManager.Instance != null
                                    && BattleRoyaleManager.Instance.MatchFinished;
            SetCursorLocked(!resultScreenOpen);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            bool resultScreenOpen = BattleRoyaleManager.Instance != null
                                    && BattleRoyaleManager.Instance.MatchFinished;
            bool menuOpen = GameMenuController.Instance != null && GameMenuController.Instance.IsOpen;
            if (resultScreenOpen || menuOpen)
            {
                if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
                {
                    SetCursorLocked(false);
                }
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = GameInput.ReadLook();
                float sensitivityMultiplier = GameSettings.MouseSensitivity;
                yaw += look.x * sensitivity * sensitivityMultiplier;
                pitch -= look.y * sensitivity * sensitivityMultiplier;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focusPoint = target.position + Vector3.up * targetHeight;
            // Move the camera to the selected shoulder so the animal is framed on
            // the chosen side while the screen center remains the firing ray.
            float shoulderSide = GameSettings.CharacterSide == CharacterScreenSide.Left ? 1f : -1f;
            Vector3 desiredPosition = focusPoint
                                      + rotation * Vector3.right * (shoulderOffset * shoulderSide)
                                      - rotation * Vector3.forward * distance;
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

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
