using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    public sealed partial class JungleGenerator
    {
        private static readonly Vector2 SwampLakeCenter = new Vector2(105f, -105f);
        private const float SwampLakeRotationDegrees = -28f;
        private const float SwampLakeHalfLength = 38f;
        private const float SwampLakeHalfWidth = 14.5f;
        private const float SwampShoreScale = 1.32f;
        private const int SwampTreeCount = 42;
        private const string SwampTreeResourceRoot = "EnvironmentModels/NewNaturePack/";

        private float swampWaterHeightCache;
        private bool swampWaterHeightCached;

        private static readonly string[] SwampTreePrefabs =
        {
            "SwampTree1", "SwampTree2", "SwampTree3"
        };

        private void CreateNewSwampEnvironment(Transform parent)
        {
            float waterHeight = CalculateSwampWaterHeight();
            Transform swampRoot = new GameObject("SoutheastSwampLake").transform;
            swampRoot.SetParent(parent, false);
            swampRoot.position = new Vector3(SwampLakeCenter.x, waterHeight, SwampLakeCenter.y);
            swampRoot.rotation = Quaternion.Euler(0f, SwampLakeRotationDegrees, 0f);

            SwampLake swampVolume = swampRoot.gameObject.AddComponent<SwampLake>();
            swampVolume.Configure(SwampLakeHalfLength * 0.98f, SwampLakeHalfWidth * 0.98f,
                waterHeight, 0.43f);

            Material mudMaterial = CreateMaterial(new Color(0.24f, 0.19f, 0.075f));
            Material depthMaterial = CreateMaterial(new Color(0.025f, 0.11f, 0.075f));
            Material waterMaterial = CreateWaterMaterial(new Color(0.055f, 0.31f, 0.19f, 0.78f));
            if (depthMaterial.HasProperty("_Cull")) depthMaterial.SetFloat("_Cull", (float)CullMode.Off);

            GameObject shore = new GameObject("TerrainFollowingSwampShore");
            shore.transform.SetParent(swampRoot, false);
            shore.AddComponent<MeshFilter>().sharedMesh = CreateTerrainFollowingSwampShoreMesh(waterHeight);
            MeshRenderer shoreRenderer = shore.AddComponent<MeshRenderer>();
            shoreRenderer.sharedMaterial = mudMaterial;
            shoreRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shoreRenderer.receiveShadows = true;
            shore.isStatic = true;

            GameObject depth = CreateLakeSurface(swampRoot, "SwampDepth", GetLakeDiscMesh(), depthMaterial,
                Vector3.down * 0.16f, new Vector3(SwampLakeHalfLength, 1f, SwampLakeHalfWidth), false);
            depth.GetComponent<MeshRenderer>().receiveShadows = false;

            GameObject water = CreateLakeSurface(swampRoot, "SwampWater", GetLakeDiscMesh(), waterMaterial,
                Vector3.zero, new Vector3(SwampLakeHalfLength * 0.98f, 1f, SwampLakeHalfWidth * 0.98f), false);
            water.GetComponent<MeshRenderer>().receiveShadows = false;
            water.AddComponent<LakeWaterMotion>();

            System.Random random = new System.Random(unchecked(seed ^ 0x735A19C1));
            int trees = SpawnSwampTrees(parent, random);
            bool houseCreated = SpawnSwampHouse(parent);
            Debug.Log($"[Jungle] Pantano sudeste criado: lago alongado, {trees} arvores exclusivas" +
                      (houseCreated ? " e casa na entrada." : "; casa nao encontrada."));
        }

        private int SpawnSwampTrees(Transform parent, System.Random random)
        {
            Transform treesRoot = new GameObject("SwampTrees").transform;
            treesRoot.SetParent(parent, false);
            int created = 0;

            for (int index = 0; index < SwampTreeCount; index++)
            {
                float angle = index * Mathf.PI * 2f / SwampTreeCount
                              + NextNatureFloat(random, -0.075f, 0.075f);
                // Keep a readable opening at the near end for the swamp house.
                if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, 180f)) < 22f) continue;

                float band = index % 3 == 0
                    ? NextNatureFloat(random, 1.08f, 1.18f)
                    : NextNatureFloat(random, 1.2f, 1.48f);
                Vector2 local = new Vector2(
                    Mathf.Cos(angle) * SwampLakeHalfLength * band,
                    Mathf.Sin(angle) * SwampLakeHalfWidth * band);
                Vector2 planar = SwampLocalToWorld(local);
                float size = index % 13 == 0
                    ? NextNatureFloat(random, 17f, 21f)
                    : NextNatureFloat(random, 10f, 16f);
                Vector3 position = new Vector3(planar.x,
                    CalculateRenderedGroundHeight(planar.x, planar.y) - size * TreeGroundEmbedRatio,
                    planar.y);
                GameObject prefab = Resources.Load<GameObject>(
                    SwampTreeResourceRoot + SwampTreePrefabs[index % SwampTreePrefabs.Length]);
                if (prefab == null) continue;

                Quaternion rotation = Quaternion.Euler(0f, NextNatureFloat(random, 0f, 360f), 0f);
                SpawnScaledPrefab(treesRoot, prefab, $"SwampTree_{index + 1:00}", position, rotation,
                    Vector3.one * size);
                created++;
            }

            return created;
        }

        private bool SpawnSwampHouse(Transform parent)
        {
            GameObject prefab = Resources.Load<GameObject>(SwampTreeResourceRoot + "SwampHouse");
            if (prefab == null) return false;

            Vector2 planar = SwampLocalToWorld(new Vector2(-SwampLakeHalfLength - 5.5f, 0f));
            const float size = 20f;
            Vector3 position = new Vector3(planar.x,
                CalculateRenderedGroundHeight(planar.x, planar.y) - size * 0.045f,
                planar.y);
            Vector3 lookDirection = new Vector3(SwampLakeCenter.x - planar.x, 0f,
                SwampLakeCenter.y - planar.y);
            Quaternion rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            SpawnScaledPrefab(parent, prefab, "SwampHouse_LakeEntrance", position, rotation,
                Vector3.one * size);
            return true;
        }

        private Mesh CreateTerrainFollowingSwampShoreMesh(float waterHeight)
        {
            const int angularSegments = 128;
            const int radialSegments = 7;
            int rowSize = radialSegments + 1;
            Vector3[] vertices = new Vector3[(angularSegments + 1) * rowSize];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[angularSegments * radialSegments * 6];

            for (int angleIndex = 0; angleIndex <= angularSegments; angleIndex++)
            {
                float angle = angleIndex * Mathf.PI * 2f / angularSegments;
                float wobble = 1f + Mathf.Sin(angleIndex * 1.73f) * 0.035f
                                   + Mathf.Sin(angleIndex * 0.39f) * 0.025f;
                for (int radialIndex = 0; radialIndex <= radialSegments; radialIndex++)
                {
                    float progress = radialIndex / (float)radialSegments;
                    float scale = Mathf.Lerp(0.92f, SwampShoreScale * wobble, progress);
                    Vector2 local = new Vector2(Mathf.Cos(angle) * SwampLakeHalfLength * scale,
                        Mathf.Sin(angle) * SwampLakeHalfWidth * scale);
                    Vector2 world = SwampLocalToWorld(local);
                    int vertexIndex = angleIndex * rowSize + radialIndex;
                    vertices[vertexIndex] = new Vector3(local.x,
                        CalculateRenderedGroundHeight(world.x, world.y) - waterHeight + 0.018f,
                        local.y);
                    uvs[vertexIndex] = new Vector2(angleIndex / (float)angularSegments * 14f,
                        progress * 2f);
                }
            }

            int triangleIndex = 0;
            for (int angleIndex = 0; angleIndex < angularSegments; angleIndex++)
            {
                for (int radialIndex = 0; radialIndex < radialSegments; radialIndex++)
                {
                    int inner = angleIndex * rowSize + radialIndex;
                    int next = inner + rowSize;
                    triangles[triangleIndex++] = inner;
                    triangles[triangleIndex++] = next;
                    triangles[triangleIndex++] = inner + 1;
                    triangles[triangleIndex++] = inner + 1;
                    triangles[triangleIndex++] = next;
                    triangles[triangleIndex++] = next + 1;
                }
            }

            Mesh mesh = new Mesh { name = "TerrainFollowingSwampShoreMesh" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private float ApplySwampLakeBasin(float currentHeight, float worldX, float worldZ)
        {
            float normalizedDistance = SwampNormalizedDistance(new Vector2(worldX, worldZ));
            if (normalizedDistance >= SwampShoreScale) return currentHeight;

            float surfaceHeight = CalculateSwampWaterHeight();
            if (normalizedDistance <= 1f)
            {
                float depthProgress = Mathf.SmoothStep(0f, 1f, normalizedDistance);
                return Mathf.Lerp(surfaceHeight - 2.45f, surfaceHeight - 0.3f, depthProgress);
            }

            float transition = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(1f, SwampShoreScale, normalizedDistance));
            return Mathf.Lerp(surfaceHeight - 0.3f, currentHeight, transition);
        }

        private float CalculateSwampWaterHeight()
        {
            // The swamp center is fixed for the whole match, so recomputing its noise
            // height on every nearby ground-height query (once per DrawCircle/particle
            // sample near the swamp) was pure waste. Cache it per generated world.
            if (!swampWaterHeightCached)
            {
                swampWaterHeightCache = CalculateNaturalLandHeight(SwampLakeCenter.x, SwampLakeCenter.y) - 1.15f;
                swampWaterHeightCached = true;
            }
            return swampWaterHeightCache;
        }

        private static bool IsInsideSwampReserve(Vector2 worldPosition, float margin)
        {
            Vector2 local = SwampWorldToLocal(worldPosition);
            float x = local.x / (SwampLakeHalfLength * SwampShoreScale + margin);
            float z = local.y / (SwampLakeHalfWidth * SwampShoreScale + margin);
            return x * x + z * z <= 1f;
        }

        private static float SwampNormalizedDistance(Vector2 worldPosition)
        {
            Vector2 local = SwampWorldToLocal(worldPosition);
            float x = local.x / SwampLakeHalfLength;
            float z = local.y / SwampLakeHalfWidth;
            return Mathf.Sqrt(x * x + z * z);
        }

        private static Vector2 SwampLocalToWorld(Vector2 localPosition)
        {
            Vector3 rotated = Quaternion.Euler(0f, SwampLakeRotationDegrees, 0f)
                * new Vector3(localPosition.x, 0f, localPosition.y);
            return SwampLakeCenter + new Vector2(rotated.x, rotated.z);
        }

        private static Vector2 SwampWorldToLocal(Vector2 worldPosition)
        {
            Vector2 delta = worldPosition - SwampLakeCenter;
            Vector3 local = Quaternion.Euler(0f, -SwampLakeRotationDegrees, 0f)
                * new Vector3(delta.x, 0f, delta.y);
            return new Vector2(local.x, local.z);
        }
    }
}
