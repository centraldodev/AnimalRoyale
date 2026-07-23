using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    public sealed partial class JungleGenerator
    {
        private const string NewNatureResourceRoot = "EnvironmentModels/NewNaturePack/";
        private const string WaterfallLoopResourcePath = "Audio/World/WaterfallLoop";
        private const int NewTreeCount = 72;
        private const int NewLakesideTreeCount = 64;
        private const int NewOuterForestTreeCount = 120;
        private const int NewMushroomCount = 32;
        private const float TreeGroundEmbedRatio = 0.035f;
        private static readonly Dictionary<string, GameObject> NewNaturePrefabCache =
            new Dictionary<string, GameObject>();
        private static readonly Dictionary<Texture2D, Material> NatureStarterBushMaterialCache =
            new Dictionary<Texture2D, Material>();
        private static Mesh natureStarterBushFallbackMesh;

        private static readonly string[] NewTreePrefabs =
        {
            "CartoonTree", "StylizedTree", "TreeA", "TreeB"
        };

        private static readonly string[] NewMushroomPrefabs =
        {
            "CartoonMushroom", "RedMushroom"
        };

        private void CreateNewCentralLake(Transform parent)
        {
            Material shoreMaterial = CreateMaterial(new Color(0.48f, 0.34f, 0.16f));
            Material depthMaterial = CreateMaterial(new Color(0.025f, 0.24f, 0.42f));
            Material waterMaterial = CreateWaterMaterial(new Color(0.045f, 0.55f, 0.82f, 0.72f));
            if (depthMaterial.HasProperty("_Cull")) depthMaterial.SetFloat("_Cull", (float)CullMode.Off);

            GameObject lake = new GameObject("CentralLakeRebuild");
            lake.transform.SetParent(parent, false);
            lake.transform.position = new Vector3(0f, lakeSurfaceHeight, 0f);
            CentralLake lakeVolume = lake.AddComponent<CentralLake>();
            lakeVolume.Configure(lakeRadius, lakeSurfaceHeight, lakeMovementMultiplier);

            GameObject shore = new GameObject("TerrainFollowingLakeShore");
            shore.transform.SetParent(lake.transform, false);
            shore.AddComponent<MeshFilter>().sharedMesh = CreateTerrainFollowingLakeShoreMesh();
            MeshRenderer shoreRenderer = shore.AddComponent<MeshRenderer>();
            shoreRenderer.sharedMaterial = shoreMaterial;
            shoreRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shoreRenderer.receiveShadows = true;
            shore.isStatic = true;
            CreateLakeShoreStones(lake.transform);

            GameObject depth = CreateLakeSurface(
                lake.transform, "DeepLakeBase", GetLakeDiscMesh(), depthMaterial,
                Vector3.down * 0.12f, Vector3.one * (lakeRadius * 0.99f), false);
            depth.GetComponent<MeshRenderer>().receiveShadows = false;

            GameObject water = CreateLakeSurface(
                lake.transform, "TransparentLakeWater", GetLakeDiscMesh(), waterMaterial,
                Vector3.zero, Vector3.one * lakeRadius, false);
            water.GetComponent<MeshRenderer>().receiveShadows = false;
            water.AddComponent<LakeWaterMotion>();

            CreateCentralWaterfallIsland(lake.transform);
            CreateNewLakeBridge(lake.transform);

        }

        private Mesh CreateTerrainFollowingLakeShoreMesh()
        {
            const int angularSegments = 128;
            const int radialSegments = 8;
            int rowSize = radialSegments + 1;
            Vector3[] vertices = new Vector3[(angularSegments + 1) * rowSize];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[angularSegments * radialSegments * 6];

            for (int angleIndex = 0; angleIndex <= angularSegments; angleIndex++)
            {
                int wrappedIndex = angleIndex % angularSegments;
                float angle = angleIndex * Mathf.PI * 2f / angularSegments;
                float wobble = 1f
                               + Mathf.Sin(wrappedIndex * 1.91f) * 0.025f
                               + Mathf.Sin(wrappedIndex * 0.47f) * 0.018f;
                float innerRadius = lakeRadius * 0.92f;
                float outerRadius = lakeRadius * 1.14f * wobble;

                for (int radialIndex = 0; radialIndex <= radialSegments; radialIndex++)
                {
                    float radialProgress = radialIndex / (float)radialSegments;
                    float radius = Mathf.Lerp(innerRadius, outerRadius, radialProgress);
                    float worldX = Mathf.Cos(angle) * radius;
                    float worldZ = Mathf.Sin(angle) * radius;
                    float worldHeight = CalculateRenderedGroundHeight(worldX, worldZ) + 0.018f;
                    int vertexIndex = angleIndex * rowSize + radialIndex;
                    // The mesh is a child of the lake, whose origin is at water level.
                    vertices[vertexIndex] = new Vector3(worldX, worldHeight - lakeSurfaceHeight, worldZ);
                    uvs[vertexIndex] = new Vector2(angleIndex / (float)angularSegments * 12f,
                        radialProgress * 2f);
                }
            }

            int triangleIndex = 0;
            for (int angleIndex = 0; angleIndex < angularSegments; angleIndex++)
            {
                for (int radialIndex = 0; radialIndex < radialSegments; radialIndex++)
                {
                    int inner = angleIndex * rowSize + radialIndex;
                    int nextAngle = inner + rowSize;
                    triangles[triangleIndex++] = inner;
                    triangles[triangleIndex++] = nextAngle;
                    triangles[triangleIndex++] = inner + 1;
                    triangles[triangleIndex++] = inner + 1;
                    triangles[triangleIndex++] = nextAngle;
                    triangles[triangleIndex++] = nextAngle + 1;
                }
            }

            Mesh mesh = new Mesh { name = "TerrainFollowingLakeShoreMesh" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private float CalculateRenderedGroundHeight(float worldX, float worldZ)
        {
            // Match CreateGround's coarse triangulated grid exactly. Sampling the raw
            // height function here would differ between terrain vertices and make the
            // coloured shore alternately sink below and rise above the rendered ground.
            float half = mapSize * 0.5f;
            float gridX = Mathf.Clamp01((worldX + half) / mapSize) * terrainResolution;
            float gridZ = Mathf.Clamp01((worldZ + half) / mapSize) * terrainResolution;
            int cellX = Mathf.Min(Mathf.FloorToInt(gridX), terrainResolution - 1);
            int cellZ = Mathf.Min(Mathf.FloorToInt(gridZ), terrainResolution - 1);
            float progressX = gridX - cellX;
            float progressZ = gridZ - cellZ;
            float cellSize = mapSize / terrainResolution;
            float x0 = -half + cellX * cellSize;
            float z0 = -half + cellZ * cellSize;
            float x1 = x0 + cellSize;
            float z1 = z0 + cellSize;
            float height00 = CalculateGroundHeight(x0, z0);
            float height10 = CalculateGroundHeight(x1, z0);
            float height01 = CalculateGroundHeight(x0, z1);
            float height11 = CalculateGroundHeight(x1, z1);

            if (progressX + progressZ <= 1f)
            {
                return height00
                       + (height10 - height00) * progressX
                       + (height01 - height00) * progressZ;
            }

            return height11
                   + (height01 - height11) * (1f - progressX)
                   + (height10 - height11) * (1f - progressZ);
        }

        private static void CreateCentralWaterfallIsland(Transform lake)
        {
            GameObject prefab = LoadNewNaturePrefab("WaterfallIsland");
            if (prefab == null)
            {
                Debug.LogWarning("[Jungle] A nova ilha com cachoeira nao foi encontrada.");
                return;
            }

            GameObject waterfall = Object.Instantiate(prefab, lake);
            waterfall.name = "WaterfallIsland_LakeCenter";
            // Keep only the lowest rim submerged so the island remains visually anchored
            // in the lake while almost all of its sculpted rock base stays visible.
            waterfall.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            waterfall.transform.localRotation = Quaternion.Euler(0f, 205f, 0f);
            waterfall.transform.localScale = Vector3.one * 19f;
            waterfall.isStatic = true;
            AddWaterfallAudio(waterfall);
        }

        private static void AddWaterfallAudio(GameObject waterfall)
        {
            AudioClip clip = Resources.Load<AudioClip>(WaterfallLoopResourcePath);
            if (clip == null)
            {
                Debug.LogWarning("[Jungle] O audio espacial da cachoeira nao foi encontrado.");
                return;
            }

            AudioSource source = waterfall.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = true;
            source.volume = 0.42f;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.spread = 25f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 7f;
            source.maxDistance = 50f;
            source.priority = 110;
            source.Play();
        }

        private static void CreateNewLakeBridge(Transform lake)
        {
            GameObject leftPrefab = LoadNewNaturePrefab("LakeBridgeLeft");
            GameObject middlePrefab = LoadNewNaturePrefab("LakeBridgeMiddle");
            GameObject rightPrefab = LoadNewNaturePrefab("LakeBridgeRight");
            if (leftPrefab == null || middlePrefab == null || rightPrefab == null)
            {
                Debug.LogWarning("[Jungle] As tres pecas da nova ponte do lago nao foram encontradas.");
                return;
            }

            Transform bridgeRoot = new GameObject("LakeBridge_Modular").transform;
            bridgeRoot.SetParent(lake, false);
            // The crossing sits in front of the central waterfall. At this offset the
            // two approaches meet the sandy shore while the middle clears the island.
            // Sink both approaches slightly into the irregular shore. Keeping the
            // five modules on one root preserves their seams and walkable alignment.
            bridgeRoot.localPosition = new Vector3(0f, -0.5f, -16f);

            const float uniformScale = 11.9f;
            const float moduleSpacing = 11.45f;
            // FBX axis conversion mirrors the longitudinal direction seen in Blender,
            // so the authored right piece belongs on the left shore (and vice versa).
            PlaceNewLakeBridgePiece(bridgeRoot, rightPrefab, "LeftApproach", -moduleSpacing * 2f, uniformScale);
            PlaceNewLakeBridgePiece(bridgeRoot, middlePrefab, "Middle_01", -moduleSpacing, uniformScale);
            PlaceNewLakeBridgePiece(bridgeRoot, middlePrefab, "Middle_02", 0f, uniformScale);
            PlaceNewLakeBridgePiece(bridgeRoot, middlePrefab, "Middle_03", moduleSpacing, uniformScale);
            PlaceNewLakeBridgePiece(bridgeRoot, leftPrefab, "RightApproach", moduleSpacing * 2f, uniformScale);
            bridgeRoot.gameObject.isStatic = true;
        }

        private static void PlaceNewLakeBridgePiece(Transform parent, GameObject prefab, string name,
            float localX, float uniformScale)
        {
            GameObject piece = Object.Instantiate(prefab, parent);
            piece.name = name;
            piece.transform.localPosition = new Vector3(localX, 0f, 0f);
            piece.transform.localRotation = Quaternion.identity;
            piece.transform.localScale = Vector3.one * uniformScale;
            piece.isStatic = true;
        }

        private static GameObject CreateLakeSurface(Transform parent, string name, Mesh mesh, Material material,
            Vector3 localPosition, Vector3 localScale, bool receivesShadows)
        {
            GameObject surface = new GameObject(name);
            surface.transform.SetParent(parent, false);
            surface.transform.localPosition = localPosition;
            surface.transform.localScale = localScale;
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = receivesShadows;
            return surface;
        }

        private void CreateNewNatureEnvironment(Transform parent)
        {
            Transform natureRoot = new GameObject("NewNatureEnvironment").transform;
            natureRoot.SetParent(parent, false);

            System.Random random = new System.Random(unchecked(seed ^ 0x274B91D3));
            int trees = SpawnNewTrees(natureRoot, random);
            int bushes = SpawnNatureStarterBushes(natureRoot, random);
            int mushrooms = SpawnNewMushrooms(natureRoot, random);
            CreateCartoonMeadow(natureRoot, random, out int grassTufts, out int flowers);

            Debug.Log($"[Jungle] Novo ambiente natural: {trees} arvores, {bushes} arbustos, {mushrooms} cogumelos, " +
                      $"{grassTufts} tufos de grama e {flowers} flores cartoon; nova cachoeira ativa.");
        }

        private int SpawnNewTrees(Transform parent, System.Random random)
        {
            Transform treesRoot = new GameObject("NewTrees").transform;
            treesRoot.SetParent(parent, false);
            int created = 0;

            for (int index = 0; index < NewTreeCount; index++)
            {
                if (!TryFindNaturePosition(random, lakeRadius + 13f, 157f, out Vector3 position)) continue;
                string prefabName = NewTreePrefabs[index % NewTreePrefabs.Length];
                GameObject prefab = LoadNewNaturePrefab(prefabName);
                if (prefab == null) continue;

                bool giant = index % 12 == 0;
                float size = giant ? NextNatureFloat(random, 24f, 30f) : NextNatureFloat(random, 11f, 18f);
                position.y -= size * TreeGroundEmbedRatio;
                Quaternion rotation = Quaternion.Euler(0f, NextNatureFloat(random, 0f, 360f), 0f);
                GameObject treeInstance = SpawnScaledPrefab(treesRoot, prefab, $"{prefabName}_{index + 1:00}_{size:0}m",
                    position, rotation, Vector3.one * size);
                RegisterTreeAsThrowable(treeInstance);
                created++;
            }

            created += SpawnLakesideTrees(treesRoot, random, created);
            created += SpawnOuterForestTrees(treesRoot, random, created);

            return created;
        }

        private int SpawnLakesideTrees(Transform treesRoot, System.Random random, int firstTreeIndex)
        {
            int created = 0;
            for (int index = 0; index < NewLakesideTreeCount; index++)
            {
                // Two staggered, irregular rings make the shore read as dense forest
                // without producing an artificial-looking circular row of trees.
                bool innerRing = index % 2 == 0;
                float angle = index * Mathf.PI * 2f / NewLakesideTreeCount
                              + NextNatureFloat(random, -0.075f, 0.075f);
                float radius = innerRing
                    ? NextNatureFloat(random, lakeRadius + 7.5f, lakeRadius + 16f)
                    : NextNatureFloat(random, lakeRadius + 18f, lakeRadius + 31f);
                Vector2 planar = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (!IsLakesideTreePositionAllowed(planar) || IsInsideSwampReserve(planar, 7f)) continue;

                int treeIndex = firstTreeIndex + created;
                string prefabName = NewTreePrefabs[treeIndex % NewTreePrefabs.Length];
                GameObject prefab = LoadNewNaturePrefab(prefabName);
                if (prefab == null) continue;

                bool giant = index % 19 == 0;
                float size = giant ? NextNatureFloat(random, 19f, 24f) : NextNatureFloat(random, 10f, 16.5f);
                Vector3 position = new Vector3(planar.x, CalculateRenderedGroundHeight(planar.x, planar.y), planar.y);
                position.y -= size * TreeGroundEmbedRatio;
                Quaternion rotation = Quaternion.Euler(0f, NextNatureFloat(random, 0f, 360f), 0f);
                GameObject treeInstance = SpawnScaledPrefab(treesRoot, prefab, $"Lakeside_{prefabName}_{index + 1:00}_{size:0}m",
                    position, rotation, Vector3.one * size);
                RegisterTreeAsThrowable(treeInstance);
                created++;
            }

            return created;
        }

        private int SpawnOuterForestTrees(Transform treesRoot, System.Random random, int firstTreeIndex)
        {
            const int ringCount = 3;
            int slotsPerRing = NewOuterForestTreeCount / ringCount;
            int created = 0;

            for (int index = 0; index < NewOuterForestTreeCount; index++)
            {
                int ring = index % ringCount;
                int slot = index / ringCount;
                float ringPhase = ring * 0.34f;
                float angle = (slot + ringPhase) * Mathf.PI * 2f / slotsPerRing
                              + NextNatureFloat(random, -0.055f, 0.055f);

                float minimumRadius = ring switch
                {
                    0 => lakeRadius + 31f,
                    1 => 92f,
                    _ => 122f
                };
                float maximumRadius = ring switch
                {
                    0 => 89f,
                    1 => 119f,
                    _ => 150f
                };
                float radius = NextNatureFloat(random, minimumRadius, maximumRadius);
                Vector2 planar = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (!IsLakesideTreePositionAllowed(planar) || IsInsideSwampReserve(planar, 7f)) continue;

                int treeIndex = firstTreeIndex + created;
                string prefabName = NewTreePrefabs[treeIndex % NewTreePrefabs.Length];
                GameObject prefab = LoadNewNaturePrefab(prefabName);
                if (prefab == null) continue;

                bool giant = index % 31 == 0;
                float size;
                if (giant) size = NextNatureFloat(random, 17f, 22f);
                else if (ring == 2) size = NextNatureFloat(random, 8f, 13.5f);
                else size = NextNatureFloat(random, 9.5f, 15.5f);

                Vector3 position = new Vector3(planar.x, CalculateRenderedGroundHeight(planar.x, planar.y), planar.y);
                position.y -= size * TreeGroundEmbedRatio;
                Quaternion rotation = Quaternion.Euler(0f, NextNatureFloat(random, 0f, 360f), 0f);
                GameObject treeInstance = SpawnScaledPrefab(treesRoot, prefab, $"OuterForest_{ring + 1}_{slot + 1:00}_{prefabName}",
                    position, rotation, Vector3.one * size);
                RegisterTreeAsThrowable(treeInstance);
                created++;
            }

            return created;
        }

        private int SpawnNatureStarterBushes(Transform parent, System.Random random)
        {
            if (!useNatureStarterVegetation
                || natureStarterBushCount <= 0
                || natureStarterBushDiffuseTextures == null
                || natureStarterBushDiffuseTextures.Length == 0)
                return 0;

            Transform bushesRoot = new GameObject("NatureStarterBushes").transform;
            bushesRoot.SetParent(parent, false);
            int created = 0;
            int maximumAttempts = Mathf.Max(64, natureStarterBushCount * 3);

            for (int attempt = 0; attempt < maximumAttempts && created < natureStarterBushCount; attempt++)
            {
                if (!TryFindNaturePosition(random, lakeRadius + 5.5f, mapSize * 0.47f, out Vector3 position))
                    continue;

                Vector2 planar = new Vector2(position.x, position.z);
                if (!IsLakesideTreePositionAllowed(planar)) continue;

                // The six Unity 5 Tree Creator prefabs deserialize as the obsolete
                // "Prefab" reference type in Unity 6 and cannot be assigned to GameObject.
                // Cycle their original foliage textures instead and build compatible
                // lightweight geometry directly at runtime.
                int prefabIndex = created % natureStarterBushDiffuseTextures.Length;
                Texture2D diffuse = natureStarterBushDiffuseTextures[prefabIndex];
                if (diffuse == null)
                {
                    for (int offset = 1; offset < natureStarterBushDiffuseTextures.Length; offset++)
                    {
                        int candidateIndex = (prefabIndex + offset)
                                             % natureStarterBushDiffuseTextures.Length;
                        if (natureStarterBushDiffuseTextures[candidateIndex] == null) continue;
                        prefabIndex = candidateIndex;
                        diffuse = natureStarterBushDiffuseTextures[candidateIndex];
                        break;
                    }
                }
                if (diffuse == null) break;

                GameObject bush = new GameObject("NatureStarterBush");
                bush.transform.SetParent(bushesRoot, false);
                bush.name = $"NatureStarterBush_{prefabIndex + 1:00}_{created + 1:000}";
                bush.transform.position = position;
                bush.transform.rotation = Quaternion.Euler(0f, NextNatureFloat(random, 0f, 360f), 0f);
                bush.transform.localScale = Vector3.one * NextNatureFloat(random, 0.68f, 1.16f);

                AddNatureStarterBushFallbackGeometry(bush, diffuse);
                ApplyNatureStarterBushMaterial(bush, diffuse);
                ApplyNatureRendererSettings(bush, false);
                foreach (Collider collider in bush.GetComponentsInChildren<Collider>(true))
                    if (collider != null) Object.Destroy(collider);

                SetVisualOnGround(bush, CalculateRenderedGroundHeight(position.x, position.z), 0.025f);
                ConfigureVisualOptimization(bush, 0.012f);
                bush.isStatic = true;
                created++;
            }

            if (created < natureStarterBushCount)
                Debug.LogWarning($"[Jungle] Apenas {created}/{natureStarterBushCount} arbustos puderam ser posicionados.");
            StaticBatchingUtility.Combine(bushesRoot.gameObject);
            return created;
        }

        private static void AddNatureStarterBushFallbackGeometry(GameObject bush, Texture2D diffuse)
        {
            if (bush == null) return;

            GameObject foliage = new GameObject("Unity6BushFallback");
            foliage.transform.SetParent(bush.transform, false);
            MeshFilter meshFilter = foliage.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = GetNatureStarterBushFallbackMesh();
            MeshRenderer renderer = foliage.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetNatureStarterBushMaterial(diffuse);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private static Mesh GetNatureStarterBushFallbackMesh()
        {
            if (natureStarterBushFallbackMesh != null) return natureStarterBushFallbackMesh;

            const int planeCount = 3;
            const float halfWidth = 0.82f;
            const float height = 1.7f;
            Vector3[] vertices = new Vector3[planeCount * 4];
            Vector2[] uvs = new Vector2[planeCount * 4];
            int[] triangles = new int[planeCount * 6];

            for (int plane = 0; plane < planeCount; plane++)
            {
                float angle = plane * Mathf.PI / planeCount;
                Vector3 width = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * halfWidth;
                int vertex = plane * 4;
                vertices[vertex] = -width;
                vertices[vertex + 1] = -width + Vector3.up * height;
                vertices[vertex + 2] = width + Vector3.up * height;
                vertices[vertex + 3] = width;

                // The legacy atlas keeps bark on the left and the complete leafy bush on
                // the right, so the fallback cards sample only the foliage half.
                uvs[vertex] = new Vector2(0.5f, 0f);
                uvs[vertex + 1] = new Vector2(0.5f, 1f);
                uvs[vertex + 2] = new Vector2(1f, 1f);
                uvs[vertex + 3] = new Vector2(1f, 0f);

                int triangle = plane * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 1;
                triangles[triangle + 2] = vertex + 2;
                triangles[triangle + 3] = vertex;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }

            natureStarterBushFallbackMesh = new Mesh
            {
                name = "NatureStarterBush_Unity6Fallback",
                vertices = vertices,
                uv = uvs,
                triangles = triangles,
                hideFlags = HideFlags.HideAndDontSave
            };
            natureStarterBushFallbackMesh.RecalculateNormals();
            natureStarterBushFallbackMesh.RecalculateBounds();
            natureStarterBushFallbackMesh.UploadMeshData(true);
            return natureStarterBushFallbackMesh;
        }

        private static void ApplyNatureStarterBushMaterial(GameObject bush, Texture2D diffuse)
        {
            if (bush == null || diffuse == null) return;

            Material material = GetNatureStarterBushMaterial(diffuse);
            if (material == null) return;

            foreach (Renderer renderer in bush.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++) materials[index] = material;
                renderer.sharedMaterials = materials;
            }
        }

        private static Material GetNatureStarterBushMaterial(Texture2D diffuse)
        {
            if (diffuse == null) return null;
            if (NatureStarterBushMaterialCache.TryGetValue(diffuse, out Material cached)
                && cached != null)
                return cached;

            Color brightFoliage = new Color(1.28f, 1.42f, 1.2f, 1f);
            Material material = new Material(ShaderLibrary.Lit)
            {
                name = diffuse.name + "_NatureStarterBush_Runtime",
                color = brightFoliage,
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", diffuse);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", diffuse);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", brightFoliage);
            if (material.HasProperty("_Color")) material.SetColor("_Color", brightFoliage);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.28f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.06f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.06f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            NatureStarterBushMaterialCache[diffuse] = material;
            return material;
        }

        private bool IsLakesideTreePositionAllowed(Vector2 position)
        {
            // Keep both bridge approaches readable and navigable from the shore.
            if (Mathf.Abs(position.y + 16f) < 8f
                && Mathf.Abs(position.x) > 18f
                && Mathf.Abs(position.x) < 48f)
                return false;

            // Preserve the initial shore spawn and a small maneuvering area around it.
            float spawnAngle = 220f * Mathf.Deg2Rad;
            Vector2 shoreSpawn = new Vector2(Mathf.Cos(spawnAngle), Mathf.Sin(spawnAngle)) * (lakeRadius + 11f);
            return Vector2.Distance(position, shoreSpawn) >= 11f;
        }

        private int SpawnNewMushrooms(Transform parent, System.Random random)
        {
            Transform mushroomsRoot = new GameObject("NewMushroomClusters").transform;
            mushroomsRoot.SetParent(parent, false);
            int created = 0;

            for (int cluster = 0; cluster < NewMushroomCount / 2; cluster++)
            {
                if (!TryFindNaturePosition(random, lakeRadius + 9f, 150f, out Vector3 center)) continue;
                for (int member = 0; member < 2; member++)
                {
                    int index = cluster * 2 + member;
                    Vector3 offset = member == 0
                        ? Vector3.zero
                        : new Vector3(NextNatureFloat(random, -2.2f, 2.2f), 0f,
                            NextNatureFloat(random, -2.2f, 2.2f));
                    Vector3 position = center + offset;
                    position.y = CalculateRenderedGroundHeight(position.x, position.z);
                    string prefabName = NewMushroomPrefabs[index % NewMushroomPrefabs.Length];
                    GameObject prefab = LoadNewNaturePrefab(prefabName);
                    if (prefab == null) continue;

                    bool giant = index % 11 == 0;
                    float size = giant ? NextNatureFloat(random, 3.6f, 4.8f) : NextNatureFloat(random, 1.35f, 2.8f);
                    Quaternion rotation = Quaternion.Euler(0f, NextNatureFloat(random, 0f, 360f), 0f);
                    SpawnScaledPrefab(mushroomsRoot, prefab, $"{prefabName}_{index + 1:00}",
                        position, rotation, Vector3.one * size);
                    created++;
                }
            }

            return created;
        }

        private bool TryFindNaturePosition(System.Random random, float minimumRadius, float maximumExtent,
            out Vector3 position)
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                float x = NextNatureFloat(random, -maximumExtent, maximumExtent);
                float z = NextNatureFloat(random, -maximumExtent, maximumExtent);
                Vector2 planar = new Vector2(x, z);
                if (planar.magnitude < minimumRadius || IsInsideSwampReserve(planar, 7f)) continue;
                position = new Vector3(x, CalculateRenderedGroundHeight(x, z), z);
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private static GameObject LoadNewNaturePrefab(string prefabName)
        {
            if (NewNaturePrefabCache.TryGetValue(prefabName, out GameObject cached)) return cached;
            GameObject prefab = Resources.Load<GameObject>(NewNatureResourceRoot + prefabName);
            NewNaturePrefabCache[prefabName] = prefab;
            if (prefab == null)
                Debug.LogWarning("[Jungle] Prefab do novo ambiente natural nao encontrado: " + prefabName);
            return prefab;
        }

        private GameObject SpawnScaledPrefab(Transform parent, GameObject prefab, string name,
            Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (prefab == null) return null;
            GameObject instance = Instantiate(prefab, position, rotation, parent);
            instance.name = name;
            instance.transform.localScale = scale;
            return instance;
        }

        private static float NextNatureFloat(System.Random random, float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        }

        // Trees no longer carry a permanent decorative vine — the monkey throws one at
        // whatever tree it's aiming at instead (see VineAnchor.TryThrowVine), created on
        // demand and torn down once left. This just makes the tree a valid throw target.
        private static void RegisterTreeAsThrowable(GameObject treeInstance)
        {
            if (treeInstance != null) VineAnchor.RegisterThrowableTree(treeInstance.transform);
        }
    }
}
