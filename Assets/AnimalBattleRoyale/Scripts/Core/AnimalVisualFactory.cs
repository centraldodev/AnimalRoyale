using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    public static class AnimalVisualFactory
    {
        private static AnimalPackageCatalog catalog;

        public static Transform Build(Transform parent, AnimalType type, Color mainColor, Vector3 scale)
        {
            Transform existing = parent.Find("VisualRoot");
            if (existing != null)
            {
                if (Application.isPlaying) Object.Destroy(existing.gameObject);
                else Object.DestroyImmediate(existing.gameObject);
            }

            GameObject visualRootObject = new GameObject("VisualRoot");
            Transform visualRoot = visualRootObject.transform;
            visualRoot.SetParent(parent, false);
            visualRoot.localScale = scale;

            catalog ??= Resources.Load<AnimalPackageCatalog>("AnimalPackageCatalog");
            GameObject prefab = catalog != null ? catalog.GetPrefab(type) : null;
            if (prefab == null)
            {
                Debug.LogError($"Modelo do pacote não configurado para {type}. Verifique AnimalPackageCatalog.");
                BuildMissingModelMarker(visualRoot, mainColor);
            }
            else
            {
                GameObject instance = Object.Instantiate(prefab, visualRoot, false);
                instance.name = type + "_PackageModel";
                instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
                DisablePackageGameplayComponents(instance);
                ConfigureRenderers(instance);
            }

            visualRootObject.AddComponent<AnimalVisualMotion>().Initialize(type);
            return visualRoot;
        }

        private static void DisablePackageGameplayComponents(GameObject instance)
        {
            CharacterController nestedController = instance.GetComponent<CharacterController>();
            if (nestedController != null) nestedController.enabled = false;

            // The Asset Store prefabs include their own input/movement demonstration
            // scripts. The battle-royale root owns movement, physics and camera input.
            MonoBehaviour[] behaviours = instance.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour != null) behaviour.enabled = false;
            }

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
            {
                if (collider != null) collider.enabled = false;
            }
        }

        private static void ConfigureRenderers(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null) material.enableInstancing = true;
                }
            }
        }

        private static void BuildMissingModelMarker(Transform root, Color color)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            marker.name = "MissingPackageModel";
            marker.transform.SetParent(root, false);
            marker.transform.localPosition = Vector3.up * 0.5f;
            Collider collider = marker.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer == null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            renderer.sharedMaterial = new Material(shader) { color = color };
        }
    }
}
