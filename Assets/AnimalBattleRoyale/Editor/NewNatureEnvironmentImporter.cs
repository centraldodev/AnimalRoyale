using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale.EditorTools
{
    /// <summary>Builds normalized prefabs for the new trees, mushrooms, bridges and waterfall island.</summary>
    public static class NewNatureEnvironmentImporter
    {
        private const string PackRoot = "Assets/AnimalBattleRoyale/Art/Environment/NewNaturePack";
        private const string PrefabRoot = "Assets/AnimalBattleRoyale/Resources/EnvironmentModels/NewNaturePack";
        private const string GeneratedMeshRoot = PackRoot + "/Models/Generated";

        private enum NatureKind
        {
            Tree,
            Mushroom,
            Bridge,
            BridgePiece,
            WaterfallIsland,
            House
        }

        private readonly struct NatureDefinition
        {
            public NatureDefinition(string name, NatureKind kind)
            {
                Name = name;
                Kind = kind;
                ModelPath = $"{PackRoot}/Models/{name}.fbx";
                TexturePath = $"{PackRoot}/Textures/{name}_BaseColor.jpg";
                MaterialPath = $"{PackRoot}/Materials/{name}.mat";
                PrefabPath = $"{PrefabRoot}/{name}.prefab";
            }

            public string Name { get; }
            public NatureKind Kind { get; }
            public string ModelPath { get; }
            public string TexturePath { get; }
            public string MaterialPath { get; }
            public string PrefabPath { get; }
        }

        private static readonly NatureDefinition[] Assets =
        {
            new NatureDefinition("CartoonMushroom", NatureKind.Mushroom),
            new NatureDefinition("RedMushroom", NatureKind.Mushroom),
            new NatureDefinition("CartoonTree", NatureKind.Tree),
            new NatureDefinition("StylizedTree", NatureKind.Tree),
            new NatureDefinition("TreeA", NatureKind.Tree),
            new NatureDefinition("TreeB", NatureKind.Tree),
            new NatureDefinition("RopeBridge", NatureKind.Bridge),
            new NatureDefinition("WoodenRopeBridge", NatureKind.Bridge),
            new NatureDefinition("LakeBridgeLeft", NatureKind.BridgePiece),
            new NatureDefinition("LakeBridgeMiddle", NatureKind.BridgePiece),
            new NatureDefinition("LakeBridgeRight", NatureKind.BridgePiece),
            new NatureDefinition("WaterfallIsland", NatureKind.WaterfallIsland),
            new NatureDefinition("SwampTree1", NatureKind.Tree),
            new NatureDefinition("SwampTree2", NatureKind.Tree),
            new NatureDefinition("SwampTree3", NatureKind.Tree),
            new NatureDefinition("SwampHouse", NatureKind.House)
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
            foreach (NatureDefinition definition in Assets)
            {
                bool prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(definition.PrefabPath) != null;
                bool runtimeMeshReady = definition.Kind != NatureKind.WaterfallIsland
                                        && definition.Kind != NatureKind.Bridge
                                        && definition.Kind != NatureKind.BridgePiece
                                        && definition.Kind != NatureKind.House
                                        || AssetImporter.GetAtPath(definition.ModelPath) is ModelImporter importer
                                        && importer.isReadable;
                bool modularBridgeReady = definition.Kind != NatureKind.Bridge
                                          || IsNarrowBridgeModuleReady(definition);
                if (prefabExists && runtimeMeshReady && modularBridgeReady) continue;
                ImportAll();
                return;
            }
        }

        [MenuItem("AnimalBattleRoyale/Importar ambiente natural")]
        public static void ImportAll()
        {
            Directory.CreateDirectory(PrefabRoot);
            Directory.CreateDirectory(GeneratedMeshRoot);
            foreach (NatureDefinition definition in Assets) ImportOne(definition);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[New Nature Environment] {Assets.Length} modelos preparados com texturas 2K e escala normalizada.");
        }

        [MenuItem("AnimalBattleRoyale/Focar pantano gerado")]
        private static void FocusGeneratedSwamp()
        {
            GameObject swamp = GameObject.Find("SoutheastSwampLake");
            if (swamp == null)
            {
                Debug.LogWarning("[Jungle] Entre no Play Mode antes de focar o pantano gerado.");
                return;
            }

            Selection.activeGameObject = swamp;
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.FrameSelected();
                sceneView.Repaint();
            }
        }

        private static void ImportOne(NatureDefinition definition)
        {
            if (!File.Exists(definition.ModelPath))
                throw new FileNotFoundException("Modelo de ambiente ausente", definition.ModelPath);
            if (!File.Exists(definition.TexturePath))
                throw new FileNotFoundException("Textura de ambiente ausente", definition.TexturePath);

            ConfigureModel(definition);
            Material material = BuildMaterial(definition);
            BuildPrefab(definition, material);
            if (definition.Kind == NatureKind.Bridge) BuildModularBridgePrefabs(definition, material);
        }

        private static void ConfigureModel(NatureDefinition definition)
        {
            if (AssetImporter.GetAtPath(definition.ModelPath) is not ModelImporter importer)
                throw new InvalidOperationException("FBX ainda nao foi reconhecido pelo Unity: " + definition.ModelPath);

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
            importer.isReadable = definition.Kind == NatureKind.WaterfallIsland
                                  || definition.Kind == NatureKind.Bridge
                                  || definition.Kind == NatureKind.BridgePiece
                                  || definition.Kind == NatureKind.House;
            importer.SaveAndReimport();
        }

        private static Material BuildMaterial(NatureDefinition definition)
        {
            if (AssetImporter.GetAtPath(definition.TexturePath) is TextureImporter textureImporter)
            {
                bool requiresReimport = textureImporter.textureType != TextureImporterType.Default
                                        || !textureImporter.sRGBTexture
                                        || textureImporter.alphaIsTransparency
                                        || !textureImporter.mipmapEnabled
                                        || !textureImporter.streamingMipmaps
                                        || textureImporter.maxTextureSize != 2048
                                        || textureImporter.textureCompression != TextureImporterCompression.Compressed
                                        || textureImporter.compressionQuality != 75
                                        || textureImporter.crunchedCompression;
                if (requiresReimport)
                {
                    textureImporter.textureType = TextureImporterType.Default;
                    textureImporter.sRGBTexture = true;
                    textureImporter.alphaIsTransparency = false;
                    textureImporter.mipmapEnabled = true;
                    textureImporter.streamingMipmaps = true;
                    textureImporter.maxTextureSize = 2048;
                    textureImporter.textureCompression = TextureImporterCompression.Compressed;
                    textureImporter.compressionQuality = 75;
                    textureImporter.crunchedCompression = false;
                    textureImporter.SaveAndReimport();
                }
            }

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(definition.TexturePath);
            if (baseColor == null) throw new InvalidOperationException("Textura nao carregou: " + definition.TexturePath);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(definition.MaterialPath);
            if (material == null)
            {
                material = new Material(ShaderLibrary.Lit);
                AssetDatabase.CreateAsset(material, definition.MaterialPath);
            }

            material.shader = ShaderLibrary.Lit;
            material.name = definition.Name;
            material.color = Color.white;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", baseColor);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", baseColor);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.18f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildPrefab(NatureDefinition definition, Material material)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(definition.ModelPath);
            if (source == null) throw new InvalidOperationException("Modelo nao carregou: " + definition.ModelPath);

            GameObject root = new GameObject(definition.Name);
            try
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                instance.name = definition.Name + "Model";
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

                AddGameplayCollider(root, definition.Kind);
                PrefabUtility.SaveAsPrefabAsset(root, definition.PrefabPath);
                Debug.Log($"[New Nature Environment] {definition.Name}: dimensao original {largestDimension:0.###}, " +
                          "normalizado para 1 m antes da escala do mapa.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AddGameplayCollider(GameObject root, NatureKind kind)
        {
            if (kind == NatureKind.Tree)
            {
                CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
                collider.direction = 1;
                collider.center = new Vector3(0f, 0.32f, 0f);
                collider.height = 0.64f;
                collider.radius = 0.075f;
                return;
            }

            if (kind == NatureKind.Mushroom)
            {
                CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
                collider.direction = 1;
                collider.center = new Vector3(0f, 0.36f, 0f);
                collider.height = 0.72f;
                collider.radius = 0.24f;
                return;
            }

            if (kind != NatureKind.WaterfallIsland && kind != NatureKind.BridgePiece
                && kind != NatureKind.House) return;
            foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null) continue;
                MeshCollider collider = meshFilter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = meshFilter.sharedMesh;
                collider.convex = false;
            }
        }

        private static void BuildModularBridgePrefabs(NatureDefinition definition, Material material)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(definition.ModelPath);
            MeshFilter sourceFilter = source != null ? source.GetComponentInChildren<MeshFilter>(true) : null;
            Mesh sourceMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
            if (sourceMesh == null)
                throw new InvalidOperationException("Malha da ponte nao encontrada: " + definition.ModelPath);

            Bounds bounds = sourceMesh.bounds;
            float length = Mathf.Max(bounds.size.x, 0.0001f);
            // Preserve the sculpted approaches in the two ends. Only the nearly-flat
            // 12% strip around the bridge centre is tiled across long spans.
            float leftThreshold = bounds.min.x + length * 0.44f;
            float rightThreshold = bounds.max.x - length * 0.44f;

            BuildBridgePart(definition, material, source, sourceMesh, "Left",
                centroid => centroid <= leftThreshold);
            BuildBridgePart(definition, material, source, sourceMesh, "Middle",
                centroid => centroid > leftThreshold && centroid < rightThreshold);
            BuildBridgePart(definition, material, source, sourceMesh, "Right",
                centroid => centroid >= rightThreshold);
        }

        private static bool IsNarrowBridgeModuleReady(NatureDefinition definition)
        {
            Mesh middle = AssetDatabase.LoadAssetAtPath<Mesh>(
                $"{GeneratedMeshRoot}/{definition.Name}Middle.asset");
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(definition.ModelPath);
            MeshFilter sourceFilter = source != null ? source.GetComponentInChildren<MeshFilter>(true) : null;
            Mesh sourceMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
            if (middle == null || sourceMesh == null) return false;
            // Triangles that cross a cut slightly expand the generated bounds. The
            // narrow module stays below 28%; the previous wide cut was about 48%.
            return middle.bounds.size.x <= sourceMesh.bounds.size.x * 0.28f;
        }

        private static void BuildBridgePart(NatureDefinition definition, Material material, GameObject source,
            Mesh sourceMesh, string suffix, Func<float, bool> acceptsCentroid)
        {
            int[] triangles = sourceMesh.triangles;
            Vector3[] vertices = sourceMesh.vertices;
            List<int> selectedTriangles = new List<int>(triangles.Length / 2);
            HashSet<int> usedVertices = new HashSet<int>();

            for (int index = 0; index < triangles.Length; index += 3)
            {
                int a = triangles[index];
                int b = triangles[index + 1];
                int c = triangles[index + 2];
                float centroid = (vertices[a].x + vertices[b].x + vertices[c].x) / 3f;
                if (!acceptsCentroid(centroid)) continue;
                selectedTriangles.Add(a);
                selectedTriangles.Add(b);
                selectedTriangles.Add(c);
                usedVertices.Add(a);
                usedVertices.Add(b);
                usedVertices.Add(c);
            }

            Mesh generated = new Mesh
            {
                name = definition.Name + suffix,
                indexFormat = sourceMesh.indexFormat,
                vertices = vertices,
                normals = sourceMesh.normals,
                tangents = sourceMesh.tangents,
                colors = sourceMesh.colors,
                uv = sourceMesh.uv,
                triangles = selectedTriangles.ToArray()
            };
            generated.bounds = CalculateUsedVertexBounds(vertices, usedVertices);

            string meshPath = $"{GeneratedMeshRoot}/{definition.Name}{suffix}.asset";
            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (savedMesh == null)
            {
                AssetDatabase.CreateAsset(generated, meshPath);
                savedMesh = generated;
            }
            else
            {
                EditorUtility.CopySerialized(generated, savedMesh);
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(savedMesh);
            }

            BuildBridgePartPrefab(definition, material, source, savedMesh, suffix);
        }

        private static void BuildBridgePartPrefab(NatureDefinition definition, Material material,
            GameObject source, Mesh partMesh, string suffix)
        {
            GameObject root = new GameObject(definition.Name + suffix);
            try
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                instance.name = definition.Name + suffix + "Model";
                instance.transform.SetParent(root.transform, false);

                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int index = 0; index < materials.Length; index++) materials[index] = material;
                    renderer.sharedMaterials = materials;
                }

                Bounds fullBounds = CalculateBounds(root);
                float largestDimension = Mathf.Max(fullBounds.size.x, fullBounds.size.y, fullBounds.size.z);
                instance.transform.localScale = Vector3.one / Mathf.Max(largestDimension, 0.0001f);
                fullBounds = CalculateBounds(root);
                instance.transform.localPosition -= new Vector3(fullBounds.center.x, fullBounds.min.y, fullBounds.center.z);

                MeshFilter filter = root.GetComponentInChildren<MeshFilter>(true);
                if (filter == null) throw new InvalidOperationException("MeshFilter ausente na ponte modular.");
                filter.sharedMesh = partMesh;
                string prefabPath = $"{PrefabRoot}/{definition.Name}{suffix}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Bounds CalculateUsedVertexBounds(Vector3[] vertices, HashSet<int> usedVertices)
        {
            if (usedVertices.Count == 0) return new Bounds(Vector3.zero, Vector3.one * 0.001f);
            bool initialized = false;
            Bounds bounds = default;
            foreach (int index in usedVertices)
            {
                if (!initialized)
                {
                    bounds = new Bounds(vertices[index], Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(vertices[index]);
                }
            }
            return bounds;
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
