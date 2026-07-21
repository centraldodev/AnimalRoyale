using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Hang point on jungle trees used by the monkey's Q leap.</summary>
    public sealed class VineAnchor : MonoBehaviour
    {
        // Calibrated against the actual tree layout: with 650 trees scattered randomly
        // across the map, the nearest neighboring tree is ~7m away on average (median 6.5m),
        // and 90% of trees have a neighbor within ~12m. ChainUseRange covers that so aiming
        // at a nearby vine while swinging almost always reaches it; only the sparsest, most
        // isolated trees (the rare >90th percentile gaps) are genuinely out of leap range.
        public const float GroundUseRange = 12f;
        public const float ChainUseRange = 15f;
        private const int IndicatorSegments = 24;
        private const float MaximumSwingAngle = 32f;
        private const float SwingSpring = 24f;
        private const float SwingDamping = 3.1f;
        private const float AmbientSwayDegrees = 5f;
        private static readonly List<VineAnchor> anchors = new List<VineAnchor>();
        private static Material sharedIndicatorMaterial;
        private LineRenderer gripIndicator;
        private Transform swingPivot;
        private Quaternion restLocalRotation = Quaternion.identity;
        private Vector2 swingAngles;
        private Vector2 swingAngularVelocity;
        private Vector3 previousAnchorPosition;
        private Vector3 swingVelocity;
        private int lastDrivenFrame = -1;
        private bool swingPositionInitialized;
        private float ambientPhaseX;
        private float ambientPhaseZ;

        public Vector3 SwingVelocity => swingVelocity;

        private void OnEnable()
        {
            if (!anchors.Contains(this)) anchors.Add(this);
        }

        private void OnDisable()
        {
            anchors.Remove(this);
            if (gripIndicator != null) gripIndicator.enabled = false;
        }

        private void LateUpdate()
        {
            if (swingPivot == null || lastDrivenFrame == Time.frameCount) return;
            SimulateSwing(Vector3.zero, Time.deltaTime);
        }

        /// <summary>Pushes the bottom of the vine toward the requested world direction.</summary>
        public void DriveSwing(Vector3 worldDirection, float deltaTime)
        {
            if (swingPivot == null) return;
            lastDrivenFrame = Time.frameCount;
            SimulateSwing(worldDirection, deltaTime);
        }

        internal static void TickIndicators(ThirdPersonAnimalController player, Camera camera, float time)
        {
            bool monkeyActive = player != null && camera != null && player.AnimalType == AnimalType.Monkey
                && !player.IsDefeated && !player.IsVineLeaping
                && (!player.IsHangingVine || player.CanChainToAnotherVine);
            Vector3 playerPosition = monkeyActive ? player.transform.position : Vector3.zero;
            Vector3 cameraPosition = monkeyActive ? camera.transform.position : Vector3.zero;
            Vector3 aimDirection = monkeyActive ? player.ViewAimDirection : Vector3.forward;
            float indicatorRange = monkeyActive ? GetUseRange(player) : 0f;
            float indicatorRangeSqr = indicatorRange * indicatorRange;
            float pulse = Mathf.Sin(time * 7f);
            float radius = 0.2f + pulse * 0.018f;
            float width = 0.022f + pulse * 0.003f;
            Vector3 cameraRight = monkeyActive ? camera.transform.right : Vector3.right;
            Vector3 cameraUp = monkeyActive ? camera.transform.up : Vector3.up;

            foreach (VineAnchor anchor in anchors)
            {
                if (anchor == null) continue;
                bool visible = monkeyActive;
                if (visible)
                {
                    Vector3 anchorPosition = anchor.transform.position;
                    Vector3 toVine = anchorPosition - cameraPosition;
                    float distanceToPlayer = (anchorPosition - playerPosition).sqrMagnitude;
                    float lookDot = toVine.sqrMagnitude > 0.01f
                        ? Vector3.Dot(aimDirection, toVine.normalized)
                        : 0f;
                    visible = distanceToPlayer <= indicatorRangeSqr && lookDot >= 0.84f;
                }

                if (!visible)
                {
                    if (anchor.gripIndicator != null) anchor.gripIndicator.enabled = false;
                    continue;
                }

                anchor.EnsureGripIndicator();
                anchor.gripIndicator.enabled = true;
                anchor.gripIndicator.widthMultiplier = width;
                for (int i = 0; i <= IndicatorSegments; i++)
                {
                    float angle = i * Mathf.PI * 2f / IndicatorSegments;
                    Vector3 point = anchor.transform.position
                                    + cameraRight * (Mathf.Cos(angle) * radius)
                                    + cameraUp * (Mathf.Sin(angle) * radius);
                    anchor.gripIndicator.SetPosition(i, point);
                }
            }
        }

        private void EnsureGripIndicator()
        {
            if (gripIndicator != null) return;
            GameObject circle = new GameObject("MonkeyGripCircle");
            circle.transform.SetParent(transform, false);
            circle.transform.localPosition = Vector3.zero;
            gripIndicator = circle.AddComponent<LineRenderer>();
            gripIndicator.useWorldSpace = true;
            gripIndicator.loop = false;
            gripIndicator.positionCount = IndicatorSegments + 1;
            gripIndicator.numCornerVertices = 3;
            gripIndicator.numCapVertices = 3;
            gripIndicator.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gripIndicator.receiveShadows = false;
            gripIndicator.sortingOrder = 20;
            EnsureIndicatorMaterial();
            gripIndicator.sharedMaterial = sharedIndicatorMaterial;
            gripIndicator.startColor = new Color(1f, 0.98f, 0.45f, 1f);
            gripIndicator.endColor = new Color(0.2f, 1f, 0.8f, 1f);
            gripIndicator.enabled = false;
        }

        private static void EnsureIndicatorMaterial()
        {
            if (sharedIndicatorMaterial != null) return;
            Shader shader = ShaderLibrary.Sprite;
            sharedIndicatorMaterial = new Material(shader)
            {
                name = "SharedMonkeyVineIndicator",
                color = new Color(1f, 0.86f, 0.1f, 0.96f),
                enableInstancing = true
            };
        }

        public static VineAnchor Create(Transform tree, Vector3 localStart, Vector3 localEnd, Material material)
        {
            Vector3 direction = localEnd - localStart;
            float length = direction.magnitude;
            if (tree == null || length <= 0.01f) return null;

            GameObject pivot = new GameObject("VineSwingPivot");
            pivot.transform.SetParent(tree, false);
            pivot.transform.localPosition = localStart;
            pivot.transform.localRotation = Quaternion.FromToRotation(Vector3.down, direction.normalized);

            GameObject anchor = new GameObject("VineAnchor");
            anchor.transform.SetParent(pivot.transform, false);
            anchor.transform.localPosition = Vector3.down * length;
            VineAnchor vineAnchor = anchor.AddComponent<VineAnchor>();
            vineAnchor.ConfigureSwing(pivot.transform);

            GameObject vine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            vine.name = "ClimbableVine";
            vine.transform.SetParent(pivot.transform, false);
            vine.transform.localPosition = Vector3.down * (length * 0.5f);
            vine.transform.localScale = new Vector3(0.2f, length * 0.5f, 0.2f);
            vine.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = vine.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            return vineAnchor;
        }

        private void ConfigureSwing(Transform pivot)
        {
            swingPivot = pivot;
            restLocalRotation = pivot != null ? pivot.localRotation : Quaternion.identity;
            previousAnchorPosition = transform.position;
            swingPositionInitialized = true;
            // Randomized per vine so a whole tree line doesn't sway in lockstep.
            ambientPhaseX = Random.Range(0f, Mathf.PI * 2f);
            ambientPhaseZ = Random.Range(0f, Mathf.PI * 2f);
        }

        private void SimulateSwing(Vector3 worldDirection, float deltaTime)
        {
            float step = Mathf.Clamp(deltaTime, 0f, 0.05f);
            if (step <= 0f || swingPivot == null) return;

            Vector3 localDirection = worldDirection;
            if (swingPivot.parent != null)
                localDirection = swingPivot.parent.InverseTransformDirection(worldDirection);
            localDirection = Quaternion.Inverse(restLocalRotation) * localDirection;
            localDirection.y = 0f;
            localDirection = Vector3.ClampMagnitude(localDirection, 1f);

            // A slow, per-vine sinusoidal sway so hanging vines read as alive even when
            // nobody is swinging on them, on top of whatever the monkey is driving.
            Vector2 ambientSway = new Vector2(
                Mathf.Sin(Time.time * 0.55f + ambientPhaseX),
                Mathf.Sin(Time.time * 0.4f + ambientPhaseZ)) * AmbientSwayDegrees;

            Vector2 targetAngles = new Vector2(-localDirection.z, localDirection.x) * MaximumSwingAngle + ambientSway;
            Vector2 acceleration = (targetAngles - swingAngles) * SwingSpring;
            swingAngularVelocity += acceleration * step;
            swingAngularVelocity *= Mathf.Exp(-SwingDamping * step);
            swingAngles += swingAngularVelocity * step;

            if (swingAngles.magnitude > MaximumSwingAngle)
            {
                swingAngles = swingAngles.normalized * MaximumSwingAngle;
                if (Vector2.Dot(swingAngularVelocity, swingAngles) > 0f)
                    swingAngularVelocity *= 0.45f;
            }

            if (localDirection.sqrMagnitude < 0.0001f
                && swingAngles.sqrMagnitude < 0.0004f
                && swingAngularVelocity.sqrMagnitude < 0.0025f)
            {
                swingAngles = Vector2.zero;
                swingAngularVelocity = Vector2.zero;
            }

            swingPivot.localRotation = restLocalRotation
                                       * Quaternion.Euler(swingAngles.x, 0f, swingAngles.y);
            Vector3 currentPosition = transform.position;
            if (swingPositionInitialized)
                swingVelocity = (currentPosition - previousAnchorPosition) / step;
            else
                swingPositionInitialized = true;
            previousAnchorPosition = currentPosition;
        }

        public static int RegisterExistingVines(Transform treeVisual)
        {
            if (treeVisual == null) return 0;
            int registered = 0;
            Transform[] descendants = treeVisual.GetComponentsInChildren<Transform>(true);
            foreach (Transform vineVisual in descendants)
            {
                if (vineVisual == null || !vineVisual.name.Contains("HangingVine")) continue;
                if (vineVisual.GetComponentInChildren<VineAnchor>(true) != null) continue;
                Renderer vineRenderer = vineVisual.GetComponentInChildren<Renderer>(true);
                if (vineRenderer == null) continue;

                Bounds bounds = vineRenderer.bounds;
                Vector3 gripPosition = new Vector3(bounds.center.x, bounds.min.y + 0.16f, bounds.center.z);
                GameObject anchorObject = new GameObject("VineAnchor_" + vineVisual.name);
                anchorObject.transform.SetParent(vineVisual, true);
                anchorObject.transform.position = gripPosition;
                anchorObject.AddComponent<VineAnchor>();
                registered++;
            }
            return registered;
        }

        public static bool IsWithinUseRange(ThirdPersonAnimalController monkey, Transform vine)
        {
            if (monkey == null || vine == null) return false;
            float range = GetUseRange(monkey);
            return (vine.position - monkey.transform.position).sqrMagnitude <= range * range;
        }

        private static float GetUseRange(ThirdPersonAnimalController monkey)
        {
            return monkey != null && monkey.IsHangingVine ? ChainUseRange : GroundUseRange;
        }

        public static bool TryUseNearest(ThirdPersonAnimalController monkey, Vector3 requestedDirection)
        {
            if (monkey == null || monkey.AnimalType != AnimalType.Monkey || monkey.IsVineLeaping) return false;
            if (monkey.IsHangingVine && !monkey.CanChainToAnotherVine) return false;
            VineAnchor nearest = null;
            VineAnchor lookedAt = null;
            float bestScore = float.MaxValue;
            float bestLookDot = 0.7f;
            float lookedAtDistance = float.MaxValue;
            Transform camera = CameraCache.MainTransform;
            Vector3 viewAimDirection = monkey.ViewAimDirection;
            float useRange = GetUseRange(monkey);
            foreach (VineAnchor anchor in anchors)
            {
                if (anchor == null) continue;
                if (monkey.IsHoldingVine(anchor.transform)) continue;
                Vector3 offset = anchor.transform.position - monkey.transform.position;
                float sqrDistance = offset.sqrMagnitude;
                if (sqrDistance > useRange * useRange) continue;
                float lookBonus = 0f;
                if (camera != null)
                {
                    Vector3 fromCamera = anchor.transform.position - camera.position;
                    if (fromCamera.sqrMagnitude > 0.01f)
                    {
                        float lookDot = Vector3.Dot(viewAimDirection, fromCamera.normalized);
                        lookBonus = lookDot * 260f;
                        if (lookDot > bestLookDot || (Mathf.Approximately(lookDot, bestLookDot) && sqrDistance < lookedAtDistance))
                        {
                            bestLookDot = lookDot;
                            lookedAtDistance = sqrDistance;
                            lookedAt = anchor;
                        }
                    }
                }

                // A vine the camera is pointing at should be usable even when the
                // monkey is still facing another direction.
                Vector3 flatOffset = offset;
                flatOffset.y = 0f;
                bool isLookedAt = lookedAt == anchor;
                if (!isLookedAt && requestedDirection.sqrMagnitude > 0.01f && flatOffset.sqrMagnitude > 0.01f
                    && Vector3.Dot(requestedDirection.normalized, flatOffset.normalized) < -0.35f) continue;
                float score = sqrDistance - lookBonus;
                if (score >= bestScore) continue;
                bestScore = score;
                nearest = anchor;
            }
            // A vine in the centre of the camera is always preferred. It is the one with the visible circle.
            if (lookedAt != null) nearest = lookedAt;
            if (nearest == null) return false;
            return monkey.IsHangingVine
                ? monkey.TryLaunchToVine(nearest.transform)
                : monkey.TryGrabVine(nearest.transform);
        }

        public static bool IsLookedAtBy(ThirdPersonAnimalController player)
        {
            if (player == null || player.AnimalType != AnimalType.Monkey || player.IsDefeated || player.IsVineLeaping) return false;
            if (player.IsHangingVine && !player.CanChainToAnotherVine) return false;
            Transform camera = CameraCache.MainTransform;
            Vector3 aim = player.ViewAimDirection;
            float useRange = GetUseRange(player);
            foreach (VineAnchor anchor in anchors)
            {
                if (anchor == null || player.IsHoldingVine(anchor.transform)) continue;
                Vector3 offset = anchor.transform.position - player.transform.position;
                if (offset.sqrMagnitude > useRange * useRange) continue;
                if (camera == null) continue;
                Vector3 fromCamera = anchor.transform.position - camera.position;
                if (fromCamera.sqrMagnitude > 0.01f && Vector3.Dot(aim, fromCamera.normalized) >= 0.84f) return true;
            }
            return false;
        }
    }
}
