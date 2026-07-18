using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    public sealed partial class JungleGenerator
    {
        private const int LakeStoneVariantCount = 18;
        private const int LakeStoneRingCount = 3;
        private const int LakeStonesPerRing = 112;
        private static GameObject[] cachedLakeStonePrefabs;

        private void CreateLakeShoreStones(Transform lake)
        {
            GameObject[] prefabs = GetLakeStonePrefabs();
            if (prefabs.Length == 0)
            {
                Debug.LogWarning("[Jungle] As pedras separadas da margem ainda nao foram importadas.");
                return;
            }

            Transform stonesRoot = new GameObject("LakeShoreStones").transform;
            stonesRoot.SetParent(lake, false);
            System.Random random = new System.Random(unchecked(seed ^ 0x61A4C82D));
            int created = 0;

            for (int ring = 0; ring < LakeStoneRingCount; ring++)
            {
                for (int slot = 0; slot < LakeStonesPerRing; slot++)
                {
                    float angleStep = Mathf.PI * 2f / LakeStonesPerRing;
                    float angle = (slot + ring * 0.31f) * angleStep
                                  + NextNatureFloat(random, -angleStep * 0.24f, angleStep * 0.24f);
                    float sampleIndex = Mathf.Repeat(angle, Mathf.PI * 2f)
                                        / (Mathf.PI * 2f) * 128f;
                    float wobble = 1f
                                   + Mathf.Sin(sampleIndex * 1.91f) * 0.025f
                                   + Mathf.Sin(sampleIndex * 0.47f) * 0.018f;
                    float innerRadius = lakeRadius * 0.93f;
                    float outerRadius = lakeRadius * 1.125f * wobble;
                    float radialProgress = Mathf.Clamp01((ring + 0.5f) / LakeStoneRingCount
                                                         + NextNatureFloat(random, -0.11f, 0.11f));
                    float radius = Mathf.Lerp(innerRadius, outerRadius, radialProgress);
                    float worldX = Mathf.Cos(angle) * radius;
                    float worldZ = Mathf.Sin(angle) * radius;

                    // Do not let decorative stones visibly pierce the bridge deck or ramps.
                    if (Mathf.Abs(worldZ + 16f) < 4.2f && Mathf.Abs(worldX) < 35f) continue;

                    float size = ring switch
                    {
                        0 => NextNatureFloat(random, 0.58f, 1.05f),
                        1 => NextNatureFloat(random, 0.72f, 1.28f),
                        _ => NextNatureFloat(random, 0.82f, 1.48f)
                    };
                    size *= 0.5f;
                    float thickness = NextNatureFloat(random, 1.35f, 2.05f);

                    float groundHeight = CalculateRenderedGroundHeight(worldX, worldZ);
                    Vector3 normal = CalculateRenderedGroundNormal(worldX, worldZ);
                    float sinkDepth = Mathf.Lerp(0.025f, 0.075f,
                        Mathf.InverseLerp(0.29f, 0.74f, size));
                    Vector3 position = new Vector3(worldX, groundHeight, worldZ) - normal * sinkDepth;
                    float yaw = NextNatureFloat(random, 0f, 360f);
                    Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal)
                                          * Quaternion.Euler(0f, yaw, 0f);
                    GameObject prefab = prefabs[(slot * 5 + ring * 7 + random.Next(prefabs.Length))
                                               % prefabs.Length];
                    GameObject stone = Instantiate(prefab, position, rotation, stonesRoot);
                    stone.name = $"ShoreStone_{ring + 1}_{slot + 1:000}";
                    stone.transform.localScale = new Vector3(size, size * thickness, size);
                    foreach (Collider collider in stone.GetComponentsInChildren<Collider>(true))
                        collider.enabled = false;
                    foreach (Renderer renderer in stone.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.shadowCastingMode = ShadowCastingMode.On;
                        renderer.receiveShadows = true;
                    }
                    stone.isStatic = true;
                    created++;
                }
            }

            StaticBatchingUtility.Combine(stonesRoot.gameObject);
            Debug.Log($"[Jungle] Margem do lago decorada com {created} pedras ajustadas ao terreno.");
        }

        private Vector3 CalculateRenderedGroundNormal(float worldX, float worldZ)
        {
            const float sampleDistance = 0.32f;
            float left = CalculateRenderedGroundHeight(worldX - sampleDistance, worldZ);
            float right = CalculateRenderedGroundHeight(worldX + sampleDistance, worldZ);
            float back = CalculateRenderedGroundHeight(worldX, worldZ - sampleDistance);
            float forward = CalculateRenderedGroundHeight(worldX, worldZ + sampleDistance);
            return new Vector3(left - right, sampleDistance * 2f, back - forward).normalized;
        }

        private static GameObject[] GetLakeStonePrefabs()
        {
            if (cachedLakeStonePrefabs != null) return cachedLakeStonePrefabs;
            GameObject[] loaded = new GameObject[LakeStoneVariantCount];
            int count = 0;
            for (int index = 0; index < LakeStoneVariantCount; index++)
            {
                GameObject prefab = Resources.Load<GameObject>(
                    $"EnvironmentModels/LakeStonePack/LakeStone_{index:00}");
                if (prefab != null) loaded[count++] = prefab;
            }

            cachedLakeStonePrefabs = new GameObject[count];
            System.Array.Copy(loaded, cachedLakeStonePrefabs, count);
            return cachedLakeStonePrefabs;
        }
    }
}
