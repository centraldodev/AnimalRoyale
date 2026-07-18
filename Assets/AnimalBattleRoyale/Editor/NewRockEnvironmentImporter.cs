using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale.EditorTools
{
    /// <summary>Prepares the user-supplied Tripo rocks as normalized, static environment prefabs.</summary>
    public static class NewRockEnvironmentImporter
    {
        private const string PackRoot = "Assets/AnimalBattleRoyale/Art/Environment/NewRockPack";
        private const string PrefabRoot = "Assets/AnimalBattleRoyale/Resources/EnvironmentModels/NewRockPack";

        private readonly struct RockDefinition
        {
            public RockDefinition(string name)
            {
                Name = name;
                ModelPath = $"{PackRoot}/Models/{name}.fbx";
                TexturePath = $"{PackRoot}/Textures/{name}_BaseColor.jpg";
                MaterialPath = $"{PackRoot}/Materials/{name}.mat";
                PrefabPath = $"{PrefabRoot}/{name}.prefab";
            }

            public string Name { get; }
            public string ModelPath { get; }
            public string TexturePath { get; }
            public string MaterialPath { get; }
            public string PrefabPath { get; }
        }

        private static readonly RockDefinition[] Rocks =
        {
            new RockDefinition("GreenRockyHill"),
            new RockDefinition("Rocha1"),
            new RockDefinition("RockFormationA"),
            new RockDefinition("RockFormationB"),
            new RockDefinition("RockPillar"),
            new RockDefinition("RockyIslandLarge"),
            new RockDefinition("RockyHill"),
            new RockDefinition("RockyIslandLong"),
            new RockDefinition("StoneRock")
        };

        [InitializeOnLoadMethod]
        private static void ScheduleFirstImport()
        {
            EditorApplication.update -= ImportWhenEditorIsReady;
            EditorApplication.update += ImportWhenEditorIsReady;
        }

        private static void ImportWhenEditorIsReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= ImportWhenEditorIsReady;
            foreach (RockDefinition rock in Rocks)
            {
                bool prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(rock.PrefabPath) != null;
                bool meshSupportsRuntimeNavigation = AssetImporter.GetAtPath(rock.ModelPath) is ModelImporter importer
                                                     && importer.isReadable;
                if (prefabExists && meshSupportsRuntimeNavigation) continue;
                ImportAll();
                return;
            }
        }

        [MenuItem("AnimalBattleRoyale/Importar novo ambiente rochoso")]
        public static void ImportAll()
        {
            Directory.CreateDirectory(PrefabRoot);
            foreach (RockDefinition rock in Rocks) ImportOne(rock);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[New Rock Environment] {Rocks.Length} modelos preparados com texturas 2K e escala normalizada.");
        }

        private static void ImportOne(RockDefinition rock)
        {
            if (!File.Exists(rock.ModelPath))
                throw new FileNotFoundException("Modelo rochoso ausente", rock.ModelPath);
            if (!File.Exists(rock.TexturePath))
                throw new FileNotFoundException("Textura rochosa ausente", rock.TexturePath);

            ConfigureModel(rock.ModelPath);
            Material material = BuildMaterial(rock);
            BuildPrefab(rock, material);
        }

        private static void ConfigureModel(string modelPath)
        {
            if (AssetImporter.GetAtPath(modelPath) is not ModelImporter importer)
                throw new InvalidOperationException("FBX ainda nao foi reconhecido pelo Unity: " + modelPath);

            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.useFileScale = false;
            importer.globalScale = 1f;
            // RuntimeNavMeshSurface builds from these MeshColliders in the player.
            // Unity therefore requires read access even though the meshes are static.
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        private static Material BuildMaterial(RockDefinition rock)
        {
            if (AssetImporter.GetAtPath(rock.TexturePath) is TextureImporter textureImporter)
            {
                bool requiresReimport = textureImporter.textureType != TextureImporterType.Default
                                        || !textureImporter.sRGBTexture
                                        || textureImporter.alphaIsTransparency
                                        || !textureImporter.mipmapEnabled
                                        || !textureImporter.streamingMipmaps
                                        || textureImporter.maxTextureSize != 2048
                                        || textureImporter.textureCompression != TextureImporterCompression.CompressedHQ
                                        || textureImporter.compressionQuality != 100
                                        || textureImporter.crunchedCompression;
                if (requiresReimport)
                {
                    textureImporter.textureType = TextureImporterType.Default;
                    textureImporter.sRGBTexture = true;
                    textureImporter.alphaIsTransparency = false;
                    textureImporter.mipmapEnabled = true;
                    textureImporter.streamingMipmaps = true;
                    textureImporter.maxTextureSize = 2048;
                    textureImporter.textureCompression = TextureImporterCompression.CompressedHQ;
                    textureImporter.compressionQuality = 100;
                    textureImporter.crunchedCompression = false;
                    textureImporter.SaveAndReimport();
                }
            }

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(rock.TexturePath);
            if (baseColor == null) throw new InvalidOperationException("Textura nao carregou: " + rock.TexturePath);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(rock.MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, rock.MaterialPath);
            }

            material.shader = Shader.Find("Standard");
            material.name = rock.Name;
            material.color = Color.white;
            material.SetTexture("_MainTex", baseColor);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", 0.18f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildPrefab(RockDefinition rock, Material material)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(rock.ModelPath);
            if (source == null) throw new InvalidOperationException("Modelo nao carregou: " + rock.ModelPath);

            GameObject root = new GameObject(rock.Name);
            try
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                instance.name = rock.Name + "Model";
                instance.transform.SetParent(root.transform, false);

                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int index = 0; index < materials.Length; index++) materials[index] = material;
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                Bounds originalBounds = CalculateBounds(root);
                float largestDimension = Mathf.Max(originalBounds.size.x, originalBounds.size.y, originalBounds.size.z);
                instance.transform.localScale = Vector3.one / Mathf.Max(largestDimension, 0.0001f);

                Bounds normalizedBounds = CalculateBounds(root);
                instance.transform.localPosition -= new Vector3(
                    normalizedBounds.center.x,
                    normalizedBounds.min.y,
                    normalizedBounds.center.z);

                foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (meshFilter.sharedMesh == null) continue;
                    MeshCollider collider = meshFilter.GetComponent<MeshCollider>();
                    if (collider == null) collider = meshFilter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = meshFilter.sharedMesh;
                    collider.convex = false;
                }

                PrefabUtility.SaveAsPrefabAsset(root, rock.PrefabPath);
                Debug.Log($"[New Rock Environment] {rock.Name}: dimensao original {largestDimension:0.###}, " +
                          $"normalizado para 1 m antes da escala do mapa.");
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

    }
}
