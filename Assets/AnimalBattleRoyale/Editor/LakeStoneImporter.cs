using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale.Editor
{
    /// <summary>Extracts lightweight individual stones from the connected-island Tripo mesh.</summary>
    public static class LakeStoneImporter
    {
        private const string PackRoot = "Assets/AnimalBattleRoyale/Art/Environment/LakeStonePack";
        private const string SourceModelPath = PackRoot + "/Models/LakeStonesSource.fbx";
        private const string TexturePath = PackRoot + "/Textures/LakeStones_BaseColor.jpg";
        private const string MaterialPath = PackRoot + "/Materials/LakeStones.mat";
        private const string MeshRoot = PackRoot + "/Generated";
        private const string PrefabRoot = "Assets/AnimalBattleRoyale/Resources/EnvironmentModels/LakeStonePack";
        private const int VariantCount = 18;

        [InitializeOnLoadMethod]
        private static void ScheduleFirstImport()
        {
            EditorApplication.update -= ImportWhenReady;
            EditorApplication.update += ImportWhenReady;
        }

        private static void ImportWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= ImportWhenReady;
            if (!File.Exists(SourceModelPath) || !File.Exists(TexturePath)) return;

            bool prefabsReady = true;
            for (int index = 0; index < VariantCount; index++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(index)) != null) continue;
                prefabsReady = false;
                break;
            }

            bool modelReady = AssetImporter.GetAtPath(SourceModelPath) is ModelImporter importer
                              && importer.isReadable;
            if (!prefabsReady || !modelReady) ImportAll();
        }

        [MenuItem("AnimalBattleRoyale/Importar pedras da margem do lago")]
        public static void ImportAll()
        {
            Directory.CreateDirectory(MeshRoot);
            Directory.CreateDirectory(PrefabRoot);
            ConfigureModel();
            Material material = BuildMaterial();
            BuildStoneVariants(material);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Lake Stones] {VariantCount} variacoes extraidas da malha de 135 pedras.");
        }

        private static void ConfigureModel()
        {
            if (AssetImporter.GetAtPath(SourceModelPath) is not ModelImporter importer)
                throw new InvalidOperationException("FBX das pedras ainda nao foi importado: " + SourceModelPath);

            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.isReadable = true;
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
                textureImporter.streamingMipmaps = true;
                textureImporter.maxTextureSize = 2048;
                textureImporter.textureCompression = TextureImporterCompression.Compressed;
                textureImporter.compressionQuality = 75;
                textureImporter.crunchedCompression = false;
                textureImporter.SaveAndReimport();
            }

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(ShaderLibrary.Lit);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.shader = ShaderLibrary.Lit;
            material.name = "LakeStones";
            material.color = Color.white;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", baseColor);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", baseColor);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.16f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.16f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildStoneVariants(Material material)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
            MeshFilter sourceFilter = source != null ? source.GetComponentInChildren<MeshFilter>(true) : null;
            Mesh sourceMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
            if (sourceMesh == null) throw new InvalidOperationException("Malha das pedras nao encontrada.");

            List<List<int>> components = FindTriangleComponents(sourceMesh)
                .Where(component => CountUniqueVertices(sourceMesh.triangles, component) >= 24)
                .OrderByDescending(component => ComponentPlanarArea(sourceMesh, component))
                .ToList();
            if (components.Count < VariantCount)
                throw new InvalidOperationException($"Apenas {components.Count} pedras separadas foram encontradas.");

            for (int index = 0; index < VariantCount; index++)
            {
                int sourceIndex = Mathf.RoundToInt(index * (components.Count - 1f) / (VariantCount - 1f));
                Mesh generated = ExtractComponent(sourceMesh, components[sourceIndex], index);
                string meshPath = $"{MeshRoot}/LakeStone_{index:00}.asset";
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

                GameObject root = new GameObject($"LakeStone_{index:00}");
                try
                {
                    root.AddComponent<MeshFilter>().sharedMesh = savedMesh;
                    MeshRenderer renderer = root.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    root.isStatic = true;
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(index));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static List<List<int>> FindTriangleComponents(Mesh mesh)
        {
            int[] triangles = mesh.triangles;
            UnionFind union = new UnionFind(mesh.vertexCount);
            for (int triangle = 0; triangle < triangles.Length; triangle += 3)
            {
                union.Join(triangles[triangle], triangles[triangle + 1]);
                union.Join(triangles[triangle], triangles[triangle + 2]);
            }

            Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>();
            for (int triangle = 0; triangle < triangles.Length; triangle += 3)
            {
                int root = union.Root(triangles[triangle]);
                if (!groups.TryGetValue(root, out List<int> group))
                {
                    group = new List<int>();
                    groups.Add(root, group);
                }
                group.Add(triangle);
            }
            return groups.Values.ToList();
        }

        private static int CountUniqueVertices(int[] triangles, List<int> component)
        {
            HashSet<int> vertices = new HashSet<int>();
            foreach (int triangle in component)
            {
                vertices.Add(triangles[triangle]);
                vertices.Add(triangles[triangle + 1]);
                vertices.Add(triangles[triangle + 2]);
            }
            return vertices.Count;
        }

        private static float ComponentPlanarArea(Mesh mesh, List<int> component)
        {
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            Bounds bounds = new Bounds(vertices[triangles[component[0]]], Vector3.zero);
            foreach (int triangle in component)
            {
                bounds.Encapsulate(vertices[triangles[triangle]]);
                bounds.Encapsulate(vertices[triangles[triangle + 1]]);
                bounds.Encapsulate(vertices[triangles[triangle + 2]]);
            }
            return bounds.size.x * bounds.size.z;
        }

        private static Mesh ExtractComponent(Mesh source, List<int> component, int variantIndex)
        {
            int[] sourceTriangles = source.triangles;
            Vector3[] sourceVertices = source.vertices;
            Vector3[] sourceNormals = source.normals;
            Vector2[] sourceUvs = source.uv;
            Dictionary<int, int> remap = new Dictionary<int, int>();
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>(component.Count * 3);

            foreach (int triangle in component)
            {
                for (int corner = 0; corner < 3; corner++)
                {
                    int sourceIndex = sourceTriangles[triangle + corner];
                    if (!remap.TryGetValue(sourceIndex, out int destinationIndex))
                    {
                        destinationIndex = vertices.Count;
                        remap.Add(sourceIndex, destinationIndex);
                        vertices.Add(sourceVertices[sourceIndex]);
                        normals.Add(sourceNormals.Length == sourceVertices.Length
                            ? sourceNormals[sourceIndex] : Vector3.up);
                        uvs.Add(sourceUvs.Length == sourceVertices.Length
                            ? sourceUvs[sourceIndex] : Vector2.zero);
                    }
                    triangles.Add(destinationIndex);
                }
            }

            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (Vector3 vertex in vertices) bounds.Encapsulate(vertex);
            float horizontalSize = Mathf.Max(bounds.size.x, bounds.size.z);
            float inverseSize = 1f / Mathf.Max(horizontalSize, 0.0001f);
            Vector3 pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            for (int index = 0; index < vertices.Count; index++)
                vertices[index] = (vertices[index] - pivot) * inverseSize;

            Mesh result = new Mesh { name = $"LakeStone_{variantIndex:00}" };
            result.SetVertices(vertices);
            result.SetNormals(normals);
            result.SetUVs(0, uvs);
            result.SetTriangles(triangles, 0);
            result.RecalculateBounds();
            return result;
        }

        private static string PrefabPath(int index) => $"{PrefabRoot}/LakeStone_{index:00}.prefab";

        private sealed class UnionFind
        {
            private readonly int[] parent;

            public UnionFind(int count)
            {
                parent = new int[count];
                for (int index = 0; index < count; index++) parent[index] = index;
            }

            public int Root(int value)
            {
                while (parent[value] != value)
                {
                    parent[value] = parent[parent[value]];
                    value = parent[value];
                }
                return value;
            }

            public void Join(int first, int second)
            {
                int firstRoot = Root(first);
                int secondRoot = Root(second);
                if (firstRoot != secondRoot) parent[secondRoot] = firstRoot;
            }
        }
    }
}
