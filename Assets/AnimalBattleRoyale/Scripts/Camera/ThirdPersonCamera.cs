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
        [SerializeField] private float minPitch = -90f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float baseVerticalFov = 58f;

        // Tuned against 16:9. Unity's Camera.fieldOfView is the VERTICAL fov, so on a
        // narrower/smaller window the horizontal fov shrinks and everything reads as more
        // "zoomed in" — most noticeably the shoulder weapon, since it sits much closer to the
        // camera than the rest of the character and near objects are far more sensitive to
        // fov changes than distant ones. Below the reference aspect we widen the vertical fov
        // to hold the horizontal fov constant (Hor+); at/above it we leave it alone, so 16:9
        // and wider behave exactly as before.
        private const float ReferenceAspect = 16f / 9f;

        // Secondary aim shared by all ammo types. A smaller fraction gives the scope
        // a noticeably stronger zoom while retaining the existing smooth transition.
        private const float AimZoomFovFraction = 0.30f;
        private const float AimZoomBlendSpeed = 7f;
        private const float GrappleDistanceMultiplier = 1.55f;
        private const float GrappleFovMultiplier = 1.08f;
        private const float GrappleViewBlendSpeed = 3.5f;

        private Camera cachedCamera;
        private float yaw;
        private float pitch = 18f;
        private Vector3 smoothVelocity;
        private readonly RaycastHit[] collisionHits = new RaycastHit[32];
        private bool aiming;
        private float aimZoomBlend;
        private float grappleViewBlend;

        public bool IsAiming => aiming;
        public float AimZoomBlend01 => aimZoomBlend;

        public Transform Target => target;
        // Aim follows the exact center ray of the rendered camera.
        public Vector3 AimDirection => transform.forward;

        private void Start()
        {
            cachedCamera = GetComponent<Camera>();
            yaw = transform.eulerAngles.y;
            bool resultScreenOpen = BattleRoyaleManager.Instance != null
                                    && BattleRoyaleManager.Instance.MatchFinished;
            SetCursorLocked(!resultScreenOpen);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            ThirdPersonAnimalController followedPlayer = target.GetComponent<ThirdPersonAnimalController>();
            bool grappling = followedPlayer != null
                             && (followedPlayer.IsVineLeaping || followedPlayer.IsHangingVine);
            grappleViewBlend = Mathf.MoveTowards(grappleViewBlend, grappling ? 1f : 0f,
                Time.deltaTime * GrappleViewBlendSpeed);
            ApplyAspectCorrectedFov();

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

            if (Cursor.lockState == CursorLockMode.Locked || MobileInputController.ControlsEnabled)
            {
                Vector2 look = GameInput.ReadLook();
                float sensitivityMultiplier = aiming
                    ? GameSettings.AimMouseSensitivity
                    : GameSettings.MouseSensitivity;
                yaw += look.x * sensitivity * sensitivityMultiplier;
                pitch -= look.y * sensitivity * sensitivityMultiplier;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focusPoint = target.position + Vector3.up * targetHeight;
            // Move the camera to the selected shoulder so the animal is framed on
            // the chosen side while the screen center remains the firing ray.
            float shoulderSide = GameSettings.CharacterSide == CharacterScreenSide.Left ? 1f : -1f;
            float effectiveDistance = distance * Mathf.Lerp(1f, GrappleDistanceMultiplier, grappleViewBlend);
            Vector3 desiredPosition = focusPoint
                                      + rotation * Vector3.right * (shoulderOffset * shoulderSide)
                                      - rotation * Vector3.forward * effectiveDistance;
            desiredPosition = ResolveCollision(focusPoint, desiredPosition);

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref smoothVelocity, smoothTime);
            transform.rotation = rotation;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>Held state for the secondary zoom, set every frame by the local controller.</summary>
        public void SetAiming(bool value) => aiming = value;

        private void ApplyAspectCorrectedFov()
        {
            if (cachedCamera == null) return;
            aimZoomBlend = Mathf.MoveTowards(aimZoomBlend, aiming ? 1f : 0f, Time.deltaTime * AimZoomBlendSpeed);
            float grappleFov = baseVerticalFov * Mathf.Lerp(1f, GrappleFovMultiplier, grappleViewBlend);
            float zoomedVerticalFov = grappleFov * Mathf.Lerp(1f, AimZoomFovFraction, aimZoomBlend);

            float aspect = cachedCamera.aspect;
            if (aspect >= ReferenceAspect)
            {
                cachedCamera.fieldOfView = zoomedVerticalFov;
                return;
            }

            float halfVerticalReference = zoomedVerticalFov * 0.5f * Mathf.Deg2Rad;
            float halfHorizontalReference = Mathf.Atan(Mathf.Tan(halfVerticalReference) * ReferenceAspect);
            float halfVerticalForAspect = Mathf.Atan(Mathf.Tan(halfHorizontalReference) / aspect);
            cachedCamera.fieldOfView = halfVerticalForAspect * 2f * Mathf.Rad2Deg;
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
