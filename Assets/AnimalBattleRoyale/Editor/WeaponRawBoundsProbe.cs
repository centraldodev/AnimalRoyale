using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: the new SeedLauncher.fbx (a Tripo export, like the animal rigs)
    // may not need the same x100 "Blender nested prefab" scale compensation that
    // ImportShoulderWeapon always applies — logs the RAW imported bounds before any scaling
    // so the right answer can be read off directly instead of guessed from a blown-up probe.
    public static class WeaponRawBoundsProbe
    {
        [MenuItem("AnimalBattleRoyale/Debug/Probe Raw Weapon FBX Bounds")]
        public static void Probe()
        {
            const string modelPath = "Assets/AnimalBattleRoyale/Art/Weapons/SeedLauncher/Models/SeedLauncher.fbx";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogError("[WeaponRawBoundsProbe] Could not load " + modelPath);
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogError("[WeaponRawBoundsProbe] No renderers found.");
                Object.DestroyImmediate(instance);
                return;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            float length = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            Debug.Log($"[WeaponRawBoundsProbe] raw bounds size={bounds.size} maxDimension={length:0.####} " +
                $"(instance transform scale={instance.transform.localScale})");

            Object.DestroyImmediate(instance);
        }
    }
}
