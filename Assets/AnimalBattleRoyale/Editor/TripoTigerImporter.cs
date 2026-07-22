using System;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    /// <summary>Builds the static, fully rigged gameplay tiger from the Tripo FBX.</summary>
    [InitializeOnLoad]
    public static class TripoTigerImporter
    {
        private const string ModelPath = "Assets/AnimalBattleRoyale/Art/Characters/Tiger/Models/Tiger3D_Rigged.fbx";
        private const string TexturePath = "Assets/AnimalBattleRoyale/Art/Characters/Tiger/Textures/Tiger3D_BaseColor.png";
        private const string MaterialPath = "Assets/AnimalBattleRoyale/Art/Characters/Tiger/Tiger3D_Material.mat";
        private const string PrefabPath = "Assets/AnimalBattleRoyale/Resources/CharacterModels/Tiger/Tiger.prefab";
        private const string CatalogPath = "Assets/AnimalBattleRoyale/Resources/AnimalPackageCatalog.asset";
        private const string TripoModelName = "Tiger3DModel";
        private const float TargetHeight = 2f;

        static TripoTigerImporter()
        {
            EditorApplication.delayCall += TryAutomaticImport;
        }

        [MenuItem("AnimalBattleRoyale/Import Tiger3D Rigged Model")]
        public static void Import()
        {
            ConfigureModelImporter();
            Material material = BuildMaterial();
            GameObject prefab = BuildPrefab(material);
            WireCatalog(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Tiger3D] Modelo estático instalado com malha, skin e esqueleto preservados; animação procedural por código.");
        }

        private static void TryAutomaticImport()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutomaticImport;
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null || IsAlreadyInstalled()) return;
            Import();
        }

        private static bool IsAlreadyInstalled()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || prefab.transform.Find(TripoModelName) == null) return false;
            // Generic (this importer's own default) or Human (manually upgraded for
            // Mixamo retargeting, see HumanoidRigSetup) both count as "already set up" —
            // only re-run for a genuinely fresh/untouched import.
            return AssetImporter.GetAtPath(ModelPath) is ModelImporter importer
                && importer.animationType != ModelImporterAnimationType.None;
        }

        private static void ConfigureModelImporter()
        {
            if (AssetImporter.GetAtPath(ModelPath) is not ModelImporter importer)
                throw new InvalidOperationException("Tripo Tiger FBX is missing or has not been imported: " + ModelPath);

            // Cross-species Humanoid retargeting distorted non-mapped bones (e.g. tails),
            // so locomotion is driven procedurally by ThirdPersonAnimalController/
            // AnimalVisualMotion instead of by imported/retargeted animation clips.
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.clipAnimations = Array.Empty<ModelImporterClipAnimation>();
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.optimizeGameObjects = false;
            importer.preserveHierarchy = true;
            importer.useFileScale = false;
            importer.globalScale = 1f;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static Material BuildMaterial()
        {
            if (AssetImporter.GetAtPath(TexturePath) is TextureImporter textureImporter)
            {
                textureImporter.textureType = TextureImporterType.Default;
                textureImporter.sRGBTexture = true;
                textureImporter.alphaIsTransparency = false;
                textureImporter.mipmapEnabled = true;
                textureImporter.maxTextureSize = 2048;
                textureImporter.SaveAndReimport();
            }

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (baseColor == null) throw new InvalidOperationException("Tripo Tiger texture is missing: " + TexturePath);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.shader = Shader.Find("Standard");
            material.name = "Tiger3D_Material";
            material.color = Color.white;
            material.SetTexture("_MainTex", baseColor);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", 0.28f);
            material.SetFloat("_Mode", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = -1;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject BuildPrefab(Material material)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("Unable to load " + ModelPath);

            GameObject root = new GameObject("Tiger");
            try
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.name = TripoModelName;
                instance.transform.SetParent(root.transform, false);

                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int index = 0; index < materials.Length; index++) materials[index] = material;
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                Animator animator = instance.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.runtimeAnimatorController = null;
                    animator.applyRootMotion = false;
                    animator.enabled = false;
                }

                Bounds bounds = CalculateBounds(root);
                float height = Mathf.Max(bounds.size.y, 0.0001f);
                instance.transform.localScale = Vector3.one * (TargetHeight / height);
                bounds = CalculateBounds(root);
                instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

                foreach (SkinnedMeshRenderer skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Bounds localBounds = skinned.localBounds;
                    localBounds.Expand(localBounds.size * 0.35f);
                    skinned.localBounds = localBounds;
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[Tiger3D] Prefab estático salvo em {PrefabPath}; {height:0.###} m, " +
                          $"escala {instance.transform.localScale.x:0.###}.");
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static void WireCatalog(GameObject prefab)
        {
            AnimalPackageCatalog catalog = AssetDatabase.LoadAssetAtPath<AnimalPackageCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidOperationException("Animal package catalog is missing: " + CatalogPath);
            SerializedObject serializedCatalog = new SerializedObject(catalog);
            SerializedProperty tiger = serializedCatalog.FindProperty("tiger");
            tiger.objectReferenceValue = prefab;
            serializedCatalog.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
        }
    }
}
