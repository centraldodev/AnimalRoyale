using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    public sealed partial class JungleGenerator
    {
        private const string NewRockResourceRoot = "EnvironmentModels/NewRockPack/";
        private const float RockGroundEmbedRatio = 0.055f;
        private static readonly Dictionary<string, GameObject> NewRockPrefabCache =
            new Dictionary<string, GameObject>();

        private readonly struct NewRockPlacement
        {
            public NewRockPlacement(string prefabName, float minimumSize, float maximumSize)
            {
                PrefabName = prefabName;
                MinimumSize = minimumSize;
                MaximumSize = maximumSize;
            }

            public string PrefabName { get; }
            public float MinimumSize { get; }
            public float MaximumSize { get; }
        }

        // Prefabs are normalized to one metre on their largest axis by the editor importer.
        // These values therefore represent the final largest dimension in world metres.
        private static readonly NewRockPlacement[] OuterMountainPlacements =
        {
            new NewRockPlacement("GreenRockyHill", 52f, 68f),
            new NewRockPlacement("RockyHill", 50f, 66f),
            new NewRockPlacement("RockyIslandLarge", 56f, 70f),
            new NewRockPlacement("RockFormationA", 42f, 55f),
            new NewRockPlacement("RockFormationB", 42f, 55f),
            new NewRockPlacement("RockPillar", 34f, 46f),
            new NewRockPlacement("GreenRockyHill", 48f, 64f),
            new NewRockPlacement("RockyHill", 52f, 68f),
            new NewRockPlacement("RockFormationA", 40f, 52f),
            new NewRockPlacement("RockPillar", 36f, 48f),
            new NewRockPlacement("RockyIslandLarge", 54f, 68f),
            new NewRockPlacement("RockFormationB", 40f, 54f),
            new NewRockPlacement("GreenRockyHill", 50f, 66f),
            new NewRockPlacement("RockyHill", 48f, 64f),
            new NewRockPlacement("RockPillar", 34f, 45f)
        };

        private static readonly NewRockPlacement[] InnerRockPlacements =
        {
            new NewRockPlacement("Rocha1", 12f, 18f),
            new NewRockPlacement("StoneRock", 9f, 15f),
            new NewRockPlacement("RockyIslandLong", 18f, 28f),
            new NewRockPlacement("StoneRock", 10f, 16f),
            new NewRockPlacement("Rocha1", 11f, 17f),
            new NewRockPlacement("StoneRock", 9f, 14f),
            new NewRockPlacement("RockyIslandLong", 17f, 26f),
            new NewRockPlacement("Rocha1", 12f, 18f),
            new NewRockPlacement("StoneRock", 10f, 15f),
            new NewRockPlacement("RockyIslandLong", 18f, 27f),
            new NewRockPlacement("Rocha1", 11f, 16f),
            new NewRockPlacement("StoneRock", 9f, 14f),
            new NewRockPlacement("RockyIslandLong", 19f, 28f),
            new NewRockPlacement("StoneRock", 10f, 16f),
            new NewRockPlacement("Rocha1", 12f, 17f),
            new NewRockPlacement("StoneRock", 9f, 15f)
        };

        private void CreateNewRockEnvironment(Transform parent)
        {
            Transform rocksRoot = new GameObject("NewRockEnvironment").transform;
            rocksRoot.SetParent(parent, false);

            System.Random placementRandom = new System.Random(unchecked(seed ^ 0x4A3F2C19));
            NewRockPlacement[] outer = ShufflePlacements(OuterMountainPlacements, placementRandom);
            NewRockPlacement[] inner = ShufflePlacements(InnerRockPlacements, placementRandom);

            int created = 0;
            created += SpawnRockRing(rocksRoot, outer, placementRandom, 135f, 158f, 0f);
            created += SpawnRockRing(rocksRoot, inner, placementRandom, 76f, 126f, 11.25f);

            Debug.Log($"[Jungle] Novo ambiente rochoso: {created} de " +
                      $"{outer.Length + inner.Length} formacoes gigantes posicionadas.");
        }

        private int SpawnRockRing(Transform parent, NewRockPlacement[] placements, System.Random random,
            float minimumRadius, float maximumRadius, float angleOffsetDegrees)
        {
            int created = 0;
            float angleStep = 360f / Mathf.Max(1, placements.Length);
            float randomOffset = NextFloat(random, 0f, 360f);

            for (int index = 0; index < placements.Length; index++)
            {
                NewRockPlacement placement = placements[index];
                GameObject prefab = LoadNewRockPrefab(placement.PrefabName);
                if (prefab == null) continue;

                float angleDegrees = randomOffset + angleOffsetDegrees + index * angleStep
                                     + NextFloat(random, -angleStep * 0.22f, angleStep * 0.22f);
                // Alternate two radii in the inner ring so the rocks do not form a visible circle.
                float ringRadius = placements.Length == InnerRockPlacements.Length
                    ? (index % 2 == 0
                        ? NextFloat(random, minimumRadius, Mathf.Lerp(minimumRadius, maximumRadius, 0.48f))
                        : NextFloat(random, Mathf.Lerp(minimumRadius, maximumRadius, 0.55f), maximumRadius))
                    : NextFloat(random, minimumRadius, maximumRadius);

                float finalSize = NextFloat(random, placement.MinimumSize, placement.MaximumSize);
                Vector3 position = default;
                for (int placementAttempt = 0; placementAttempt < 8; placementAttempt++)
                {
                    float candidateAngle = (angleDegrees + placementAttempt * angleStep * 0.55f) * Mathf.Deg2Rad;
                    position = new Vector3(Mathf.Cos(candidateAngle) * ringRadius, 0f,
                        Mathf.Sin(candidateAngle) * ringRadius);
                    if (!IsInsideSwampReserve(new Vector2(position.x, position.z), 10f)) break;
                }
                if (IsInsideSwampReserve(new Vector2(position.x, position.z), 10f)) continue;
                // Sample the triangles players actually see, then bury wide bases enough
                // to hide gaps caused by the rolling terrain beneath large formations.
                position.y = CalculateRenderedGroundHeight(position.x, position.z)
                             - finalSize * RockGroundEmbedRatio;

                Quaternion rotation = Quaternion.Euler(0f, NextFloat(random, 0f, 360f), 0f);
                GameObject instance = Instantiate(prefab, position, rotation, parent);
                instance.name = $"{placement.PrefabName}_{index + 1:00}_{finalSize:0}m";
                instance.transform.localScale = Vector3.one * finalSize;
                RegisterClimbable(position, finalSize * 0.3f, finalSize);
                created++;
            }

            return created;
        }

        private static GameObject LoadNewRockPrefab(string prefabName)
        {
            if (NewRockPrefabCache.TryGetValue(prefabName, out GameObject cached)) return cached;
            GameObject prefab = Resources.Load<GameObject>(NewRockResourceRoot + prefabName);
            NewRockPrefabCache[prefabName] = prefab;
            if (prefab == null)
                Debug.LogWarning("[Jungle] Prefab do novo ambiente nao encontrado: " + prefabName);
            return prefab;
        }

        private static NewRockPlacement[] ShufflePlacements(NewRockPlacement[] source, System.Random random)
        {
            NewRockPlacement[] shuffled = (NewRockPlacement[])source.Clone();
            for (int index = shuffled.Length - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                (shuffled[index], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[index]);
            }
            return shuffled;
        }

        private static float NextFloat(System.Random random, float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        }
    }
}
