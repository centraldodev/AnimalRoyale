using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    public static class AnimalVisualFactory
    {
        private static AnimalPackageCatalog catalog;
        private static GameObject shoulderWeaponPrefab;
        private static GameObject tomatoLauncherPrefab;
        private static GameObject watermelonLauncherPrefab;
        private static Material tomatoLauncherMaterial;
        private static Material watermelonLauncherMaterial;
        private static bool shoulderWeaponLookedUp;

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
                AttachShoulderWeapon(instance.transform, type);
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

        private static void AttachShoulderWeapon(Transform modelRoot, AnimalType type)
        {
            if (!shoulderWeaponLookedUp)
            {
                shoulderWeaponPrefab = Resources.Load<GameObject>("Weapons/SeedLauncher");
                tomatoLauncherPrefab = Resources.Load<GameObject>("Weapons/TomatoLauncher/TomatoLauncher");
                watermelonLauncherPrefab = Resources.Load<GameObject>("Weapons/WatermelonLauncher/WatermelonLauncher");
                shoulderWeaponLookedUp = true;
            }
            if (modelRoot == null || (shoulderWeaponPrefab == null && tomatoLauncherPrefab == null
                                      && watermelonLauncherPrefab == null)) return;

            GameObject socketObject = new GameObject("ShoulderWeaponSocket");
            Transform socket = socketObject.transform;
            socket.SetParent(modelRoot, false);
            socket.localPosition = ShoulderWeaponPosition(type);
            socket.localRotation = Quaternion.identity;

            GameObject seedWeapon = null;
            if (shoulderWeaponPrefab != null)
            {
                seedWeapon = Object.Instantiate(shoulderWeaponPrefab, socket, false);
                seedWeapon.name = "SeedLauncherVisual";
                seedWeapon.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                seedWeapon.transform.localScale = Vector3.one * ShoulderWeaponScale(type);
                PrepareShoulderWeapon(seedWeapon, null);
            }

            GameObject tomatoWeapon = null;
            if (tomatoLauncherPrefab != null)
            {
                tomatoWeapon = Object.Instantiate(tomatoLauncherPrefab, socket, false);
                tomatoWeapon.name = "TomatoLauncherVisual";
                tomatoWeapon.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                tomatoWeapon.transform.localScale = Vector3.one * ShoulderWeaponScale(type);
                PrepareShoulderWeapon(tomatoWeapon, GetTomatoLauncherMaterial());
            }

            GameObject watermelonWeapon = null;
            if (watermelonLauncherPrefab != null)
            {
                watermelonWeapon = Object.Instantiate(watermelonLauncherPrefab, socket, false);
                watermelonWeapon.name = "WatermelonLauncherVisual";
                watermelonWeapon.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                watermelonWeapon.transform.localScale = Vector3.one * ShoulderWeaponScale(type);
                PrepareShoulderWeapon(watermelonWeapon, GetWatermelonLauncherMaterial());
            }

            ShoulderWeaponVisual switcher = socketObject.AddComponent<ShoulderWeaponVisual>();
            switcher.Initialize(seedWeapon, tomatoWeapon, watermelonWeapon);
        }

        private static void PrepareShoulderWeapon(GameObject weapon, Material overrideMaterial)
        {
            if (weapon == null) return;
            foreach (Collider collider in weapon.GetComponentsInChildren<Collider>(true))
                if (collider != null) collider.enabled = false;
            ConfigureRenderers(weapon);
            if (overrideMaterial == null) return;
            foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>(true))
                if (renderer != null) renderer.sharedMaterial = overrideMaterial;
        }

        private static Material GetTomatoLauncherMaterial()
        {
            if (tomatoLauncherMaterial != null) return tomatoLauncherMaterial;
            tomatoLauncherMaterial = new Material(ShaderLibrary.Lit)
            {
                name = "TomatoLauncher_RuntimePBR",
                color = Color.white,
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave
            };

            Texture2D albedo = Resources.Load<Texture2D>("Weapons/TomatoLauncher/texture_diffuse");
            if (albedo != null)
            {
                if (tomatoLauncherMaterial.HasProperty("_BaseMap")) tomatoLauncherMaterial.SetTexture("_BaseMap", albedo);
                if (tomatoLauncherMaterial.HasProperty("_MainTex")) tomatoLauncherMaterial.SetTexture("_MainTex", albedo);
            }
            Texture2D normal = Resources.Load<Texture2D>("Weapons/TomatoLauncher/texture_normal");
            if (normal != null && tomatoLauncherMaterial.HasProperty("_BumpMap"))
            {
                tomatoLauncherMaterial.SetTexture("_BumpMap", normal);
                tomatoLauncherMaterial.SetFloat("_BumpScale", 0.82f);
                tomatoLauncherMaterial.EnableKeyword("_NORMALMAP");
            }
            if (tomatoLauncherMaterial.HasProperty("_Metallic")) tomatoLauncherMaterial.SetFloat("_Metallic", 0.08f);
            if (tomatoLauncherMaterial.HasProperty("_Smoothness")) tomatoLauncherMaterial.SetFloat("_Smoothness", 0.46f);
            if (tomatoLauncherMaterial.HasProperty("_Glossiness")) tomatoLauncherMaterial.SetFloat("_Glossiness", 0.46f);
            return tomatoLauncherMaterial;
        }

        private static Material GetWatermelonLauncherMaterial()
        {
            if (watermelonLauncherMaterial != null) return watermelonLauncherMaterial;
            watermelonLauncherMaterial = new Material(ShaderLibrary.Lit)
            {
                name = "WatermelonLauncher_RuntimePBR",
                color = Color.white,
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave
            };

            Texture2D albedo = Resources.Load<Texture2D>("Weapons/WatermelonLauncher/texture_diffuse");
            if (albedo != null)
            {
                if (watermelonLauncherMaterial.HasProperty("_BaseMap")) watermelonLauncherMaterial.SetTexture("_BaseMap", albedo);
                if (watermelonLauncherMaterial.HasProperty("_MainTex")) watermelonLauncherMaterial.SetTexture("_MainTex", albedo);
            }
            Texture2D normal = Resources.Load<Texture2D>("Weapons/WatermelonLauncher/texture_normal");
            if (normal != null && watermelonLauncherMaterial.HasProperty("_BumpMap"))
            {
                watermelonLauncherMaterial.SetTexture("_BumpMap", normal);
                watermelonLauncherMaterial.SetFloat("_BumpScale", 0.82f);
                watermelonLauncherMaterial.EnableKeyword("_NORMALMAP");
            }
            if (watermelonLauncherMaterial.HasProperty("_Metallic")) watermelonLauncherMaterial.SetFloat("_Metallic", 0.08f);
            if (watermelonLauncherMaterial.HasProperty("_Smoothness")) watermelonLauncherMaterial.SetFloat("_Smoothness", 0.46f);
            if (watermelonLauncherMaterial.HasProperty("_Glossiness")) watermelonLauncherMaterial.SetFloat("_Glossiness", 0.46f);
            return watermelonLauncherMaterial;
        }

        // Prefabs are normalized to two Unity units high. These offsets place the
        // launcher on the animal's left shoulder (viewer-right when facing it).
        private static Vector3 ShoulderWeaponPosition(AnimalType type) => type switch
        {
            AnimalType.Tiger => new Vector3(-0.46f, 1.12f, 0.02f),
            AnimalType.Ant => new Vector3(-0.38f, 0.98f, 0.03f),
            AnimalType.Eagle => new Vector3(-0.60f, 0.94f, 0.03f),
            AnimalType.Monkey => new Vector3(-0.42f, 1.05f, 0.03f),
            _ => new Vector3(-0.42f, 1.05f, 0.02f)
        };

        private static float ShoulderWeaponScale(AnimalType type) => type switch
        {
            AnimalType.Ant => 1.00f,
            AnimalType.Eagle => 1.05f,
            AnimalType.Monkey => 1.08f,
            _ => 1.12f
        };

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
            Shader shader = ShaderLibrary.Lit;
            renderer.sharedMaterial = new Material(shader) { color = color };
        }
    }

    /// <summary>Shows the launcher that matches the fighter's replicated crystal level.</summary>
    public sealed class ShoulderWeaponVisual : MonoBehaviour
    {
        private ThirdPersonAnimalController owner;
        private GameObject seedLauncher;
        private GameObject tomatoLauncher;
        private GameObject watermelonLauncher;
        private int displayedLevel = -1;

        public void Initialize(GameObject seed, GameObject tomato, GameObject watermelon)
        {
            owner = GetComponentInParent<ThirdPersonAnimalController>();
            seedLauncher = seed;
            tomatoLauncher = tomato;
            watermelonLauncher = watermelon;
            Refresh(true);
        }

        private void Update() => Refresh(false);

        private void Refresh(bool force)
        {
            int weaponLevel = owner != null ? owner.WeaponLevel : 1;
            if (!force && weaponLevel == displayedLevel) return;
            displayedLevel = weaponLevel;
            bool useWatermelonLauncher = weaponLevel >= 3 && watermelonLauncher != null;
            bool useTomatoLauncher = !useWatermelonLauncher && weaponLevel >= 2 && tomatoLauncher != null;
            if (seedLauncher != null) seedLauncher.SetActive(!useTomatoLauncher && !useWatermelonLauncher);
            if (tomatoLauncher != null) tomatoLauncher.SetActive(useTomatoLauncher);
            if (watermelonLauncher != null) watermelonLauncher.SetActive(useWatermelonLauncher);
        }
    }
}
