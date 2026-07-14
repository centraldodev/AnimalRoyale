using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    /// <summary>Bright pulsing aura used only for objects the player can collect.</summary>
    public sealed class CollectibleHighlight : MonoBehaviour
    {
        private const int RingSegments = 48;
        private static Material ringMaterial;

        private readonly LineRenderer[] rings = new LineRenderer[3];
        private Color color = Color.white;
        private float radius = 0.9f;
        private float height = 0.18f;
        private float phaseOffset;

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
            EnsureMaterial();
            BuildRings();
        }

        private void BuildRings()
        {
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
        }

        private void Update()
        {
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
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            ringMaterial = new Material(shader) { color = Color.white, enableInstancing = true };
        }
    }
}
