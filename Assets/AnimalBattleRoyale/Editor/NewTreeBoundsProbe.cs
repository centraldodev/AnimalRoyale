using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: the new tree prefabs (CartoonTree/StylizedTree/TreeA/TreeB) that
    // SpawnNewTrees actually uses have no vine attach point defined anywhere, and their local
    // (pre-scale) bounds are unknown, needed to place a vine sensibly (out near a branch, not
    // buried in the trunk or floating past the canopy) across trees that get scaled anywhere
    // from 8 to 30 world units tall.
    public static class NewTreeBoundsProbe
    {
        [MenuItem("AnimalBattleRoyale/Debug/Probe New Tree Prefab Bounds")]
        public static void Probe()
        {
            string[] names = { "CartoonTree", "StylizedTree", "TreeA", "TreeB" };
            foreach (string name in names)
            {
                GameObject prefab = Resources.Load<GameObject>("EnvironmentModels/NewNaturePack/" + name);
                if (prefab == null)
                {
                    Debug.LogWarning($"[NewTreeBoundsProbe] {name}: prefab not found.");
                    continue;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;

                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    Debug.LogWarning($"[NewTreeBoundsProbe] {name}: no renderers found.");
                    Object.DestroyImmediate(instance);
                    continue;
                }

                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);

                Debug.Log($"[NewTreeBoundsProbe] {name}: worldBounds center={bounds.center} size={bounds.size} " +
                    $"min={bounds.min} max={bounds.max} (instance at origin, scale=1, so this IS local space)");

                Object.DestroyImmediate(instance);
            }
        }
    }
}
