using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Hang point on jungle trees used by the monkey's Q leap.</summary>
    public sealed class VineAnchor : MonoBehaviour
    {
        public const float GroundUseRange = 11f;
        public const float ChainUseRange = 14f;
        private const int IndicatorSegments = 24;
        private static readonly List<VineAnchor> anchors = new List<VineAnchor>();
        private static Material sharedIndicatorMaterial;
        private LineRenderer gripIndicator;

        private void OnEnable()
        {
            if (!anchors.Contains(this)) anchors.Add(this);
        }

        private void OnDisable()
        {
            anchors.Remove(this);
            if (gripIndicator != null) gripIndicator.enabled = false;
        }

        internal static void TickIndicators(ThirdPersonAnimalController player, Camera camera, float time)
        {
            bool monkeyActive = false;
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
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            sharedIndicatorMaterial = new Material(shader)
            {
                name = "SharedMonkeyVineIndicator",
                color = new Color(1f, 0.86f, 0.1f, 0.96f),
                enableInstancing = true
            };
        }

        public static VineAnchor Create(Transform tree, Vector3 localStart, Vector3 localEnd, Material material)
        {
            GameObject anchor = new GameObject("VineAnchor");
            anchor.transform.SetParent(tree, false);
            anchor.transform.localPosition = localEnd;
            anchor.AddComponent<VineAnchor>();

            Vector3 direction = localEnd - localStart;
            GameObject vine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            vine.name = "ClimbableVine";
            vine.transform.SetParent(tree, false);
            vine.transform.localPosition = (localStart + localEnd) * 0.5f;
            vine.transform.localScale = new Vector3(0.07f, direction.magnitude * 0.5f, 0.07f);
            vine.transform.up = direction.normalized;
            vine.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = vine.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            vine.isStatic = true;
            return anchor.GetComponent<VineAnchor>();
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
            VineAnchor nearest = null;
            VineAnchor lookedAt = null;
            float bestScore = float.MaxValue;
            float bestLookDot = 0.7f;
            float lookedAtDistance = float.MaxValue;
            Camera camera = Camera.main;
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
                    Vector3 fromCamera = anchor.transform.position - camera.transform.position;
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
            return false;
        }
    }
}
