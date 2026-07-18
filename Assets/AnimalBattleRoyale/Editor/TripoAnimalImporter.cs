using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    /// <summary>Installs the user-supplied Tripo monkey, eagle and ant as gameplay prefabs.</summary>
    public static class TripoAnimalImporter
    {
        private const string CharacterBase = "Assets/AnimalBattleRoyale/Art/Characters";
        private const string CatalogPath = "Assets/AnimalBattleRoyale/Resources/AnimalPackageCatalog.asset";
        private const float TargetHeight = 2f;

        private readonly struct CharacterDefinition
        {
            public CharacterDefinition(string animal)
            {
                Animal = animal;
                ModelPath = $"{CharacterBase}/{animal}/Models/{animal}3D_Rigged.fbx";
                TexturePath = $"{CharacterBase}/{animal}/Textures/{animal}3D_BaseColor.png";
                MaterialPath = $"{CharacterBase}/{animal}/{animal}3D_Material.mat";
                PrefabPath = $"Assets/AnimalBattleRoyale/Resources/CharacterModels/{animal}/{animal}.prefab";
            }

            public string Animal { get; }
            public string ModelPath { get; }
            public string TexturePath { get; }
            public string MaterialPath { get; }
            public string PrefabPath { get; }
        }

        private static readonly CharacterDefinition[] Characters =
        {
            new CharacterDefinition("Monkey"),
            new CharacterDefinition("Eagle"),
            new CharacterDefinition("Ant")
        };

        [MenuItem("AnimalBattleRoyale/Import Macaco, Aguia e Formiga 3D")]
        public static void ImportAll()
        {
            foreach (CharacterDefinition character in Characters) ImportOne(character);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Tripo Animals] Macaco, aguia e formiga instalados nos prefabs de gameplay.");
        }

        private static void ImportOne(CharacterDefinition character)
        {
            if (!File.Exists(character.ModelPath))
                throw new FileNotFoundException($"Modelo 3D ausente para {character.Animal}", character.ModelPath);
            if (!File.Exists(character.TexturePath))
                throw new FileNotFoundException($"Textura ausente para {character.Animal}", character.TexturePath);

            ConfigureModel(character.ModelPath);
            Material material = BuildMaterial(character);
            GameObject prefab = BuildPrefab(character, material);
            WireCatalog(character.Animal, prefab);
        }

        private static void ConfigureModel(string modelPath)
        {
            if (AssetImporter.GetAtPath(modelPath) is not ModelImporter importer)
                throw new InvalidOperationException("FBX ainda nao foi reconhecido pelo Unity: " + modelPath);

            // The ant contains only run/jump clips and the other two contain none.
            // Gameplay therefore drives all three UniRig skeletons procedurally.
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Generic;
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

        private static Material BuildMaterial(CharacterDefinition character)
        {
            if (AssetImporter.GetAtPath(character.TexturePath) is TextureImporter textureImporter)
            {
                textureImporter.textureType = TextureImporterType.Default;
                textureImporter.sRGBTexture = true;
                textureImporter.alphaIsTransparency = false;
                textureImporter.mipmapEnabled = true;
                textureImporter.maxTextureSize = 2048;
                textureImporter.textureCompression = TextureImporterCompression.CompressedHQ;
                textureImporter.compressionQuality = 100;
                textureImporter.crunchedCompression = false;
                textureImporter.SaveAndReimport();
            }

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(character.TexturePath);
            if (baseColor == null) throw new InvalidOperationException("Textura nao carregou: " + character.TexturePath);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(character.MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, character.MaterialPath);
            }

            material.shader = Shader.Find("Standard");
            material.name = character.Animal + "3D_Material";
            material.color = Color.white;
            material.SetTexture("_MainTex", baseColor);
            material.SetTexture("_BumpMap", null);
            material.SetTexture("_MetallicGlossMap", null);
            material.DisableKeyword("_NORMALMAP");
            material.DisableKeyword("_METALLICGLOSSMAP");
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

        private static GameObject BuildPrefab(CharacterDefinition character, Material material)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(character.ModelPath);
            if (source == null) throw new InvalidOperationException("Modelo nao carregou: " + character.ModelPath);

            GameObject root = new GameObject(character.Animal);
            try
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                instance.name = character.Animal + "3DModel";
                instance.transform.SetParent(root.transform, false);

                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int index = 0; index < materials.Length; index++) materials[index] = material;
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                Bounds bounds = CalculateBounds(root);
                float sourceHeight = Mathf.Max(bounds.size.y, 0.0001f);
                instance.transform.localScale = Vector3.one * (TargetHeight / sourceHeight);
                bounds = CalculateBounds(root);
                instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

                int boneCount = 0;
                foreach (SkinnedMeshRenderer skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    boneCount = Mathf.Max(boneCount, skinned.bones.Length);
                    Bounds localBounds = skinned.localBounds;
                    localBounds.Expand(localBounds.size * 0.35f);
                    skinned.localBounds = localBounds;
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, character.PrefabPath);
                Debug.Log($"[Tripo Animals] {character.Animal}: prefab salvo, altura {sourceHeight:0.###}, " +
                          $"escala {instance.transform.localScale.x:0.###}, {boneCount} ossos vinculados.");
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

        private static void WireCatalog(string animal, GameObject prefab)
        {
            AnimalPackageCatalog catalog = AssetDatabase.LoadAssetAtPath<AnimalPackageCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidOperationException("Catalogo de animais ausente: " + CatalogPath);

            SerializedObject serializedCatalog = new SerializedObject(catalog);
            SerializedProperty property = serializedCatalog.FindProperty(animal.ToLowerInvariant());
            if (property == null) throw new InvalidOperationException("Campo ausente no catalogo: " + animal);
            property.objectReferenceValue = prefab;
            serializedCatalog.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
        }
    }
}
