using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Hang point on jungle trees used by the monkey's Q leap.</summary>
    public sealed class VineAnchor : MonoBehaviour
    {
        private const float UseRange = 34f;
        private const float IndicatorRange = 34f;
        private const int IndicatorSegments = 28;
        private static readonly List<VineAnchor> anchors = new List<VineAnchor>();
        private LineRenderer gripIndicator;
        private Material indicatorMaterial;

        private void Awake()
        {
            CreateGripIndicator();
        }

        private void OnEnable()
        {
            if (!anchors.Contains(this)) anchors.Add(this);
        }

        private void OnDisable() => anchors.Remove(this);

        private void OnDestroy()
        {
            if (indicatorMaterial != null) Destroy(indicatorMaterial);
        }

        internal static void TickIndicators(ThirdPersonAnimalController player, Camera camera, float time)
        {
            foreach (VineAnchor anchor in anchors)
            {
                if (anchor == null || anchor.gripIndicator == null) continue;
                bool visible = player != null && player.AnimalType == AnimalType.Monkey && camera != null;
                if (visible)
                {
                    Vector3 toVine = anchor.transform.position - camera.transform.position;
                    float distanceToPlayer = (anchor.transform.position - player.transform.position).sqrMagnitude;
                    float lookDot = toVine.sqrMagnitude > 0.01f
                        ? Vector3.Dot(camera.transform.forward, toVine.normalized)
                        : 0f;
                    visible = distanceToPlayer <= IndicatorRange * IndicatorRange && lookDot >= 0.91f;
                }

                if (!visible)
                {
                    anchor.gripIndicator.enabled = false;
                    continue;
                }

                anchor.gripIndicator.enabled = true;
                anchor.gripIndicator.transform.rotation = camera.transform.rotation;
                float radius = 0.32f + Mathf.Sin(time * 7f) * 0.035f;
                float width = 0.035f + Mathf.Sin(time * 7f) * 0.006f;
                anchor.gripIndicator.widthMultiplier = width;
                for (int i = 0; i <= IndicatorSegments; i++)
                {
                    float angle = i * Mathf.PI * 2f / IndicatorSegments;
                    anchor.gripIndicator.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
                }
            }
        }

        private void CreateGripIndicator()
        {
            GameObject circle = new GameObject("MonkeyGripCircle");
            circle.transform.SetParent(transform, false);
            circle.transform.localPosition = Vector3.zero;
            gripIndicator = circle.AddComponent<LineRenderer>();
            gripIndicator.useWorldSpace = false;
            gripIndicator.loop = false;
            gripIndicator.positionCount = IndicatorSegments + 1;
            gripIndicator.numCornerVertices = 3;
            gripIndicator.numCapVertices = 3;
            gripIndicator.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gripIndicator.receiveShadows = false;
            gripIndicator.sortingOrder = 20;
            indicatorMaterial = new Material(Shader.Find("Sprites/Default"));
            indicatorMaterial.color = new Color(1f, 0.86f, 0.1f, 0.96f);
            gripIndicator.material = indicatorMaterial;
            gripIndicator.startColor = new Color(1f, 0.98f, 0.45f, 1f);
            gripIndicator.endColor = new Color(0.2f, 1f, 0.8f, 1f);
            gripIndicator.enabled = false;
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

        public static bool TryUseNearest(ThirdPersonAnimalController monkey, Vector3 requestedDirection)
        {
            VineAnchor nearest = null;
            VineAnchor lookedAt = null;
            float bestScore = float.MaxValue;
            float bestLookDot = 0.78f;
            float lookedAtDistance = float.MaxValue;
            Camera camera = Camera.main;
            foreach (VineAnchor anchor in anchors)
            {
                if (anchor == null) continue;
                Vector3 offset = anchor.transform.position - monkey.transform.position;
                float sqrDistance = offset.sqrMagnitude;
                if (sqrDistance >= UseRange * UseRange) continue;
                float lookBonus = 0f;
                if (camera != null)
                {
                    Vector3 fromCamera = anchor.transform.position - camera.transform.position;
                    if (fromCamera.sqrMagnitude > 0.01f)
                    {
                        float lookDot = Vector3.Dot(camera.transform.forward, fromCamera.normalized);
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
            return monkey.TryGrabVine(nearest.transform);
        }

        public static bool IsLookedAtBy(ThirdPersonAnimalController player)
        {
            if (player == null || player.AnimalType != AnimalType.Monkey || Camera.main == null) return false;
            Camera camera = Camera.main;
            foreach (VineAnchor anchor in anchors)
            {
                if (anchor == null) continue;
                Vector3 toVine = anchor.transform.position - camera.transform.position;
                if ((anchor.transform.position - player.transform.position).sqrMagnitude > IndicatorRange * IndicatorRange
                    || toVine.sqrMagnitude < 0.01f) continue;
                if (Vector3.Dot(camera.transform.forward, toVine.normalized) >= 0.91f) return true;
            }
            return false;
        }
    }
}
