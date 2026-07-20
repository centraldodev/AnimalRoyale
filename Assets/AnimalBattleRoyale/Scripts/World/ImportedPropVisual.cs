using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// User-supplied prop FBX (pickups, tunnel hole, ...) can be authored at any scale
    /// or pivot. This rescales an instantiated model to a known footprint and, optionally,
    /// repositions it so its bottom sits exactly at a given ground height — independent of
    /// whatever scale/pivot the source file happens to have, so a badly-scaled import can't
    /// end up buried underground or too tiny/huge to see.
    /// </summary>
    public static class ImportedPropVisual
    {
        public static bool NormalizeScale(GameObject instance, float targetSize, out Bounds bounds)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = CombinedBounds(renderers);
            float largestAxis = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (largestAxis > 0.0001f)
            {
                instance.transform.localScale *= targetSize / largestAxis;
                bounds = CombinedBounds(renderers);
            }
            return true;
        }

        public static void NormalizeToGround(GameObject instance, float targetSize, float groundY, float clearance = 0f)
        {
            if (!NormalizeScale(instance, targetSize, out Bounds bounds)) return;
            instance.transform.position += Vector3.up * (groundY + clearance - bounds.min.y);
        }

        private static Bounds CombinedBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
