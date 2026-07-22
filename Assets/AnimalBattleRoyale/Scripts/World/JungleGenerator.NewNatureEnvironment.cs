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

        private Material newTreeVineMaterial;
        private const float NewTreeVineChance = 0.5f;

        private void CreateNewNatureEnvironment(Transform parent)
        {
            Transform natureRoot = new GameObject("NewNatureEnvironment").transform;
            natureRoot.SetParent(parent, false);

            newTreeVineMaterial = CreateMaterial(new Color(0.48f, 0.88f, 0.16f), new Color(0.1f, 0.22f, 0.02f));

            System.Random random = new System.Random(unchecked(seed ^ 0x274B91D3));
            int trees = SpawnNewTrees(natureRoot, random);
            int mushrooms = SpawnNewMushrooms(natureRoot, random);
            CreateCartoonMeadow(natureRoot, random, out int grassTufts, out int flowers);

            Debug.Log($"[Jungle] Novo ambiente natural: {trees} arvores, {mushrooms} cogumelos, " +
                      $"{grassTufts} tufos de grama e {flowers} flores cartoon; " +
                      "nova cachoeira e ponte modular do lago ativas.");
        }

        private void CreateLakeBridge(Transform parent)
        {
            float shoreDistance = lakeRadius + 7f;
            Vector3 start = new Vector3(-shoreDistance, 0f, -7f);
            Vector3 end = new Vector3(shoreDistance, 0f, -7f);
            start.y = Mathf.Max(CalculateGroundHeight(start.x, start.z) + 0.38f, lakeSurfaceHeight + 1.05f);
            end.y = Mathf.Max(CalculateGroundHeight(end.x, end.z) + 0.38f, lakeSurfaceHeight + 1.05f);

            float midpointHeight = (start.y + end.y) * 0.5f;
            float availableClearance = midpointHeight - (lakeSurfaceHeight + 0.82f);
            float sagDepth = Mathf.Clamp(availableClearance, 0.25f, 0.85f);
            SpawnModularBridge(parent, "RopeBridge", "LakeCrossingBridge", start, end,
                Vector3.Distance(start, end) + 2f, 10f, -sagDepth, 1.1f, 4.4f);
        }

        private void CreateElevatedBridgeLandmark(Transform parent)
        {
            Vector3 leftHill = new Vector3(52f, 0f, 104f);
            Vector3 rightHill = new Vector3(102f, 0f, 104f);
            leftHill.y = CalculateGroundHeight(leftHill.x, leftHill.z) - 0.65f;
            rightHill.y = CalculateGroundHeight(rightHill.x, rightHill.z) - 0.65f;

            SpawnScaledPrefab(parent, LoadNewRockPrefab("GreenRockyHill"), "BridgeHill_Left",
                leftHill, Quaternion.Euler(0f, 24f, 0f), Vector3.one * 34f);
            SpawnScaledPrefab(parent, LoadNewRockPrefab("GreenRockyHill"), "BridgeHill_Right",
                rightHill, Quaternion.Euler(0f, 204f, 0f), Vector3.one * 34f);

            float deckHeight = (leftHill.y + rightHill.y) * 0.5f + 9.2f;
            Vector3 start = new Vector3(66f, deckHeight, 104f);
            Vector3 end = new Vector3(88f, deckHeight, 104f);
            SpawnModularBridge(parent, "WoodenRopeBridge", "MountainCrossingBridge", start, end,
                30f, 9f, 0.85f, 1.1f, 4.4f);
        }

        private void SpawnModularBridge(Transform parent, string prefabName, string instanceName,
            Vector3 start, Vector3 end, float targetLength, float preferredUniformScale,
            float curveHeight, float visualVerticalOffset, float walkwayWidth)
        {
            GameObject leftPrefab = LoadNewNaturePrefab(prefabName + "Left");
            GameObject middlePrefab = LoadNewNaturePrefab(prefabName + "Middle");
            GameObject rightPrefab = LoadNewNaturePrefab(prefabName + "Right");
            if (leftPrefab == null || middlePrefab == null || rightPrefab == null) return;

            Vector3 direction = end - start;
            Vector3 horizontalDirection = new Vector3(direction.x, 0f, direction.z).normalized;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.right, horizontalDirection);
            Transform bridgeRoot = new GameObject(instanceName).transform;
            bridgeRoot.SetParent(parent, false);
            bridgeRoot.position = (start + end) * 0.5f - Vector3.up * visualVerticalOffset;
            bridgeRoot.rotation = rotation;

            Bounds leftBounds = CalculatePrefabMeshBounds(leftPrefab);
            Bounds middleBounds = CalculatePrefabMeshBounds(middlePrefab);
            Bounds rightBounds = CalculatePrefabMeshBounds(rightPrefab);
            float preferredNormalizedLength = targetLength / Mathf.Max(preferredUniformScale, 0.001f);
            int middleCount = Mathf.Clamp(Mathf.RoundToInt(
                (preferredNormalizedLength - leftBounds.size.x - rightBounds.size.x)
                / Mathf.Max(middleBounds.size.x, 0.001f)), 1, 96);
            float normalizedLength = leftBounds.size.x + rightBounds.size.x + middleBounds.size.x * middleCount;
            const float seamOverlap = 0.12f;
            float uniformScale = (targetLength + seamOverlap * (middleCount + 1))
                                 / Mathf.Max(normalizedLength, 0.001f);

            float cursor = -targetLength * 0.5f;
            cursor = PlaceBridgePart(bridgeRoot, leftPrefab, leftBounds, "LeftEnd", cursor,
                targetLength, uniformScale, start.y, end.y, curveHeight, seamOverlap);
            for (int index = 0; index < middleCount; index++)
            {
                cursor = PlaceBridgePart(bridgeRoot, middlePrefab, middleBounds, $"Middle_{index + 1:00}", cursor,
                    targetLength, uniformScale, start.y, end.y, curveHeight, seamOverlap);
            }
            PlaceBridgePart(bridgeRoot, rightPrefab, rightBounds, "RightEnd", cursor,
                targetLength, uniformScale, start.y, end.y, curveHeight, 0f);

            Transform walkwayRoot = new GameObject(instanceName + "_WalkableSurface").transform;
            walkwayRoot.SetParent(parent, false);
            const int segments = 12;
            for (int index = 0; index < segments; index++)
            {
                float startProgress = index / (float)segments;
                float endProgress = (index + 1) / (float)segments;
                Vector3 segmentStart = EvaluateBridgePoint(start, end, startProgress, curveHeight);
                Vector3 segmentEnd = EvaluateBridgePoint(start, end, endProgress, curveHeight);
                CreateWalkableBridgeSegment(walkwayRoot, index, segmentStart, segmentEnd, walkwayWidth);
            }
        }

        private static float PlaceBridgePart(Transform parent, GameObject prefab, Bounds bounds, string name,
            float cursor, float targetLength, float uniformScale, float startHeight, float endHeight,
            float curveHeight, float seamOverlap)
        {
            float worldLength = bounds.size.x * uniformScale;
            float centerAlongBridge = cursor + worldLength * 0.5f;
            float progress = Mathf.Clamp01(centerAlongBridge / targetLength + 0.5f);
            float linearHeight = Mathf.Lerp(startHeight, endHeight, progress) - (startHeight + endHeight) * 0.5f;
            float curvedHeight = Mathf.Sin(progress * Mathf.PI) * curveHeight;

            GameObject part = Instantiate(prefab, parent);
            part.name = name;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = Vector3.one * uniformScale;
            part.transform.localPosition = new Vector3(
                cursor - bounds.min.x * uniformScale,
                linearHeight + curvedHeight,
                0f);
            return cursor + worldLength - seamOverlap;
        }

        private static Bounds CalculatePrefabMeshBounds(GameObject prefab)
        {
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            bool initialized = false;
            Bounds result = default;
            Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;
            foreach (MeshFilter filter in filters)
            {
                if (filter.sharedMesh == null) continue;
                Matrix4x4 matrix = rootInverse * filter.transform.localToWorldMatrix;
                Bounds meshBounds = filter.sharedMesh.bounds;
                Vector3 min = meshBounds.min;
                Vector3 max = meshBounds.max;
                for (int x = 0; x < 2; x++)
                {
                    for (int y = 0; y < 2; y++)
                    {
                        for (int z = 0; z < 2; z++)
                        {
                            Vector3 corner = new Vector3(x == 0 ? min.x : max.x,
                                y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                            Vector3 point = matrix.MultiplyPoint3x4(corner);
                            if (!initialized)
                            {
                                result = new Bounds(point, Vector3.zero);
                                initialized = true;
                            }
                            else
                            {
                                result.Encapsulate(point);
                            }
                        }
                    }
                }
            }

            return initialized ? result : new Bounds(Vector3.zero, Vector3.one);
        }

        private static Vector3 EvaluateBridgePoint(Vector3 start, Vector3 end, float progress, float curveHeight)
        {
            Vector3 point = Vector3.Lerp(start, end, progress);
            point.y += Mathf.Sin(progress * Mathf.PI) * curveHeight;
            return point;
        }

        private static void CreateWalkableBridgeSegment(Transform parent, int index,
            Vector3 start, Vector3 end, float width)
        {
            Vector3 delta = end - start;
            GameObject segment = new GameObject($"WalkableBridgeSegment_{index + 1:00}");
            segment.transform.SetParent(parent, false);
            segment.transform.position = (start + end) * 0.5f;
            segment.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            BoxCollider collider = segment.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, 0.32f, delta.magnitude + 0.28f);
            segment.isStatic = true;
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
                AttachNewTreeVine(treeInstance, random);
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
                AttachNewTreeVine(treeInstance, random);
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
                AttachNewTreeVine(treeInstance, random);
                created++;
            }

            return created;
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
                        position, rotation, Vector3.one * size, climbable: false);
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
            Vector3 position, Quaternion rotation, Vector3 scale, bool climbable = true)
        {
            if (prefab == null) return null;
            GameObject instance = Instantiate(prefab, position, rotation, parent);
            instance.name = name;
            instance.transform.localScale = scale;
            // scale.y approximates the tree/rock's own height since these prefabs are
            // normalized to one metre on their largest axis before being scaled up here.
            if (climbable) RegisterClimbable(position, Mathf.Max(scale.x, scale.z) * 0.3f, scale.y);
            return instance;
        }

        private static float NextNatureFloat(System.Random random, float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        }

        // CartoonTree/StylizedTree/TreeA/TreeB (see NewTreeBoundsProbe) all sit roughly in
        // local bounds x:[-0.5,0.5] y:[0,~1] z:[-0.3,0.3] before the per-instance scale is
        // applied, so normalized (fractional) local coordinates here land near the canopy on
        // any of them regardless of that instance's actual world-space size — a giant 30m
        // tree gets a proportionally long vine, a small one a short vine, and the bottom end
        // always ends up close enough to the ground (~15% of height) to grab from
        // VineAnchor.GroundUseRange.
        private void AttachNewTreeVine(GameObject treeInstance, System.Random random)
        {
            if (treeInstance == null || newTreeVineMaterial == null) return;
            if (random.NextDouble() > NewTreeVineChance) return;

            // Lower/further out than the canopy mass: these prefabs' foliage blob covers
            // roughly the upper half of the bounds (see NewTreeBoundsProbe), and attaching a
            // vine up inside it just buries the tie-in point in leaf geometry with no visible
            // branch under it. Around the trunk/canopy boundary, further from center, reads as
            // coming off a real branch instead.
            float side = random.Next(0, 2) == 0 ? -1f : 1f;
            float front = random.Next(0, 2) == 0 ? -1f : 1f;
            Vector3 localStart = new Vector3(
                side * NextNatureFloat(random, 0.14f, 0.2f),
                NextNatureFloat(random, 0.42f, 0.56f),
                front * NextNatureFloat(random, 0.14f, 0.2f));
            Vector3 localEnd = new Vector3(
                side * NextNatureFloat(random, 0.18f, 0.26f),
                NextNatureFloat(random, 0.1f, 0.18f),
                front * NextNatureFloat(random, 0.16f, 0.24f));

            VineAnchor.Create(treeInstance.transform, localStart, localEnd, newTreeVineMaterial);
        }
    }
}
