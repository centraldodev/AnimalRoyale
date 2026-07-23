using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    /// <summary>Bright pulsing aura used only for objects the player can collect.</summary>
    public sealed class CollectibleHighlight : MonoBehaviour
    {
        private const int RingSegments = 48;
        private const float VisibleDistanceSqr = 72f * 72f;
        private static Material ringMaterial;
        private static Transform cachedViewer;
        private static int nextUpdateGroup;

        private readonly LineRenderer[] rings = new LineRenderer[3];
        private Color color = Color.white;
        private float radius = 0.9f;
        private float height = 0.18f;
        private float phaseOffset;
        private int updateGroup;
        private bool ringsBuilt;
        private bool ringsVisible;

        public static CollectibleHighlight Attach(Transform parent, Color color, float radius = 0.9f, float height = 0.18f)
        {
            if (parent == null) return null;
            GameObject highlightObject = new GameObject("CollectibleHighlight");
            highlightObject.transform.SetParent(parent, false);
            CollectibleHighlight highlight = highlightObject.AddComponent<CollectibleHighlight>();
            highlight.Configure(color, radius, height);
            return highlight;
        }

        public void Configure(Color highlightColor, float highlightRadius, float highlightHeight)
        {
            color = highlightColor;
            radius = Mathf.Max(0.05f, highlightRadius);
            height = highlightHeight;
            phaseOffset = Random.value * Mathf.PI * 2f;
            updateGroup = nextUpdateGroup++ & 1;
            EnsureMaterial();
        }

        private void BuildRings()
        {
            if (ringsBuilt) return;
            for (int i = 0; i < rings.Length; i++)
            {
                GameObject ring = new GameObject("AuraRing_" + i);
                ring.transform.SetParent(transform, false);
                LineRenderer line = ring.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = RingSegments;
                line.numCornerVertices = 4;
                line.numCapVertices = 4;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sortingOrder = 8;
                line.sharedMaterial = ringMaterial;
                rings[i] = line;
            }
            ringsBuilt = true;
            ringsVisible = true;
        }

        private void Update()
        {
            if ((Time.frameCount & 1) != updateGroup) return;
            if (cachedViewer == null && Camera.main != null) cachedViewer = Camera.main.transform;
            bool shouldBeVisible = cachedViewer == null
                                   || (cachedViewer.position - transform.position).sqrMagnitude <= VisibleDistanceSqr;
            if (shouldBeVisible && !ringsBuilt) BuildRings();
            if (!ringsBuilt) return;
            if (shouldBeVisible != ringsVisible)
            {
                ringsVisible = shouldBeVisible;
                foreach (LineRenderer ring in rings)
                {
                    if (ring != null) ring.enabled = ringsVisible;
                }
            }
            if (!ringsVisible) return;

            float pulse = (Mathf.Sin(Time.time * 4.4f + phaseOffset) + 1f) * 0.5f;
            for (int i = 0; i < rings.Length; i++)
            {
                float progress = i / (float)rings.Length;
                float ringRadius = radius * (0.86f + progress * 0.34f + pulse * 0.08f);
                float ringHeight = height + Mathf.Sin(Time.time * 3.1f + phaseOffset + i) * 0.035f + i * 0.035f;
                float width = Mathf.Lerp(0.035f, 0.065f, pulse) * (1f - progress * 0.18f);
                Color ringColor = new Color(color.r, color.g, color.b, Mathf.Lerp(0.32f, 0.82f, pulse) * (1f - progress * 0.18f));

                LineRenderer ring = rings[i];
                if (ring == null) continue;
                ring.widthMultiplier = width;
                ring.startColor = ringColor;
                ring.endColor = ringColor;
                for (int segment = 0; segment < RingSegments; segment++)
                {
                    float angle = segment * Mathf.PI * 2f / RingSegments + Time.time * (0.35f + i * 0.18f);
                    ring.SetPosition(segment, new Vector3(Mathf.Cos(angle) * ringRadius, ringHeight, Mathf.Sin(angle) * ringRadius));
                }
            }
        }

        private static void EnsureMaterial()
        {
            if (ringMaterial != null) return;
            Shader shader = ShaderLibrary.Sprite;
            ringMaterial = new Material(shader) { color = Color.white, enableInstancing = true };
        }
    }

    /// <summary>
    /// Soft local illumination for active pickups. Lights outside the player's immediate
    /// area are disabled so a map containing many supplies does not pay for every light.
    /// </summary>
    public sealed class PickupGlowLight : MonoBehaviour
    {
        private const float VisibleDistanceSqr = 48f * 48f;
        private static Transform cachedViewer;
        private static int nextUpdateGroup;

        private Light glow;
        private Color primaryColor;
        private Color secondaryColor;
        private float baseIntensity;
        private float phaseOffset;
        private int updateGroup;

        public static PickupGlowLight Attach(Transform parent, Color primary, Color secondary,
            float range = 5f, float intensity = 1.15f)
        {
            if (parent == null) return null;

            GameObject lightObject = new GameObject("PickupGlowLight");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = Vector3.up * 0.72f;

            PickupGlowLight effect = lightObject.AddComponent<PickupGlowLight>();
            effect.primaryColor = primary;
            effect.secondaryColor = secondary;
            effect.baseIntensity = Mathf.Max(0f, intensity);
            effect.phaseOffset = Random.value * Mathf.PI * 2f;
            effect.updateGroup = nextUpdateGroup++ & 3;

            effect.glow = lightObject.AddComponent<Light>();
            effect.glow.type = LightType.Point;
            effect.glow.color = primary;
            effect.glow.range = Mathf.Max(0.5f, range);
            effect.glow.intensity = effect.baseIntensity;
            effect.glow.bounceIntensity = 0f;
            effect.glow.shadows = LightShadows.None;
            effect.glow.renderMode = LightRenderMode.Auto;
            return effect;
        }

        private void Update()
        {
            if ((Time.frameCount & 3) != updateGroup || glow == null) return;
            if (cachedViewer == null && Camera.main != null) cachedViewer = Camera.main.transform;

            bool shouldIlluminate = cachedViewer == null
                                    || (cachedViewer.position - transform.position).sqrMagnitude <= VisibleDistanceSqr;
            if (glow.enabled != shouldIlluminate) glow.enabled = shouldIlluminate;
            if (!shouldIlluminate) return;

            float pulse = (Mathf.Sin(Time.time * 3.2f + phaseOffset) + 1f) * 0.5f;
            float colorBlend = (Mathf.Sin(Time.time * 1.65f + phaseOffset) + 1f) * 0.5f;
            glow.color = Color.Lerp(primaryColor, secondaryColor, colorBlend);
            glow.intensity = baseIntensity * Mathf.Lerp(0.78f, 1.12f, pulse);
        }
    }
}
