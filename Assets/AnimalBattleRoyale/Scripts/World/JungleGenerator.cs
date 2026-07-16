using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    public sealed partial class JungleGenerator : MonoBehaviour
    {
        [Header("Map")]
        [SerializeField, Min(40f)] private float mapSize = 360f;
        [SerializeField] private int seed = 106;
        [SerializeField] private bool randomizeSeedEveryMatch = true;

        [Header("Terrain")]
        [SerializeField, Range(32, 160)] private int terrainResolution = 96;
        [SerializeField, Range(1f, 12f)] private float terrainHeight = 8.2f;

        [Header("Central Lake")]
        [SerializeField, Range(18f, 48f)] private float lakeRadius = 31f;
        [SerializeField] private float lakeSurfaceHeight = -0.45f;
        [SerializeField, Range(0.3f, 0.8f)] private float lakeMovementMultiplier = 0.52f;

        [Header("Vegetation")]
        [SerializeField, Range(20, 900)] private int treeCount = 650;
        [SerializeField, Range(20, 1200)] private int bushCount = 880;
        [SerializeField, Range(0, 4000)] private int grassTuftCount = 1600;
        [SerializeField, Range(0, 220)] private int rockCount = 96;
        [SerializeField, Range(0, 100)] private int anthillCount = 40;
        [SerializeField, Range(0, 48)] private int mountainCount = 26;
        [SerializeField, Range(0, 100)] private int lifePickupCount = 24;
        [SerializeField, Range(0, 90)] private int rangedSupplyCount = 45;
        [SerializeField, Range(0, 40)] private int houseCount = 16;
        [SerializeField, Range(0, 16)] private int eagleMountainCount = 8;

        [Header("NatureStarterKit2")]
        [SerializeField] private bool useNatureStarterVegetation = true;
        [SerializeField, Range(0f, 0.5f)] private float natureTreeShare = 0.22f;
        [SerializeField, Range(0f, 0.75f)] private float natureBushShare = 0.4f;

        [Header("Stylized Details")]
        [SerializeField, Range(0, 500)] private int flowerPatchCount = 340;
        [SerializeField, Range(0, 8)] private int waterfallCount = 2;
        [SerializeField, Range(0, 48)] private int skyCloudCount = 22;
        [SerializeField, Range(1, 10)] private int trailBranchCount = 5;
        [SerializeField, Range(8, 40)] private int backdropMountainCount = 22;
        [SerializeField, Range(20, 180)] private int distantCanopyCount = 128;

        private MeshCollider groundCollider;
        private static GameObject cachedTreePrefab;
        private static GameObject cachedBushPrefab;
        private static GameObject cachedMountainPrefab;
        private static GameObject cachedRockPrefab;
        private static GameObject cachedFlowerPrefab;
        private static GameObject cachedHighDetailTreePrefab;
        private static GameObject cachedBroadleafPrefab;
        private static GameObject cachedHighDetailMountainPrefab;
        private static GameObject cachedMossyRockPrefab;
        private static GameObject cachedFlowerClusterPrefab;
        private static NatureEnvironmentCatalog cachedNatureCatalog;
        private static readonly Dictionary<string, Material> detailedMaterialCache = new Dictionary<string, Material>();
        private static Mesh cachedCrystalMesh;
        private static Mesh cachedBackdropMountainMesh;
        private static Mesh cachedLakeDiscMesh;
        private static Mesh cachedLakeShoreMesh;
        private readonly List<TrailRoute> trailRoutes = new List<TrailRoute>();

        public float MapSize => mapSize;
        public float LakeRadius => lakeRadius;
        public float LakeSurfaceHeight => lakeSurfaceHeight;

        public float GroundHeightAt(Vector3 worldPosition)
        {
            return CalculateGroundHeight(worldPosition.x, worldPosition.z);
        }

        /// <summary>
        /// Returns a point safely above the actual terrain collider. Using the collider keeps
        /// characters aligned with the rendered terrain, including between mesh vertices.
        /// </summary>
        public Vector3 GetGroundPosition(Vector3 worldPosition, float clearance = 0.12f)
        {
            float half = mapSize * 0.5f - 0.5f;
            Vector3 planarPosition = new Vector3(
                Mathf.Clamp(worldPosition.x, -half, half),
                0f,
                Mathf.Clamp(worldPosition.z, -half, half));

            if (groundCollider != null)
            {
                RaycastHit[] hits = Physics.RaycastAll(
                    new Vector3(planarPosition.x, 100f, planarPosition.z),
                    Vector3.down,
                    220f,
                    ~0,
                    QueryTriggerInteraction.Ignore);

                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider == groundCollider)
                    {
                        return hit.point + Vector3.up * clearance;
                    }
                }
            }

            return new Vector3(planarPosition.x, GroundHeightAt(planarPosition) + clearance, planarPosition.z);
        }

        public Vector3 GetShoreSpawnPosition(float angleDegrees = 220f, float clearance = 0.12f)
        {
            float angle = angleDegrees * Mathf.Deg2Rad;
            float radius = lakeRadius + 11f;
            Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            return GetGroundPosition(position, clearance);
        }

        public Vector3 GetMissionSpawnPosition(float centerClearance = 18f)
        {
            return RandomMapPosition(centerClearance);
        }

        public void Generate()
        {
            ClearGeneratedWorld();
            if (randomizeSeedEveryMatch) seed = unchecked(System.Environment.TickCount ^ (int)System.DateTime.UtcNow.Ticks);
            Random.InitState(seed);

            GameObject generated = new GameObject("GeneratedJungle");
            generated.transform.SetParent(transform, false);

            if (cachedNatureCatalog == null)
                cachedNatureCatalog = Resources.Load<NatureEnvironmentCatalog>("NatureEnvironmentCatalog");
            NatureEnvironmentCatalog natureCatalog = useNatureStarterVegetation ? cachedNatureCatalog : null;

            Material groundMaterial = CreateGroundMaterial(natureCatalog != null ? natureCatalog.GrassGround : null);
            Material trunkMaterial = CreateMaterial(new Color(0.31f, 0.12f, 0.035f));
            Material leafMaterial = CreateMaterial(new Color(0.035f, 0.39f, 0.075f));
            Material leafLightMaterial = CreateMaterial(new Color(0.19f, 0.62f, 0.11f));
            Material leafDeepMaterial = CreateMaterial(new Color(0.018f, 0.22f, 0.055f));
            Material bushMaterial = CreateMaterial(new Color(0.055f, 0.43f, 0.085f));
            Material rockMaterial = CreateMaterial(new Color(0.30f, 0.34f, 0.31f));
            Material distantRockMaterial = CreateMaterial(new Color(0.28f, 0.36f, 0.43f));
            Material distantPeakMaterial = CreateMaterial(new Color(0.78f, 0.86f, 0.91f));
            Material moundMaterial = CreateMaterial(new Color(0.48f, 0.19f, 0.055f));
            Material moundDarkMaterial = CreateMaterial(new Color(0.12f, 0.045f, 0.012f));
            Material houseWallMaterial = CreateMaterial(new Color(0.82f, 0.55f, 0.28f));
            Material houseRoofMaterial = CreateMaterial(new Color(0.42f, 0.1f, 0.06f));
            Material vineMaterial = CreateMaterial(new Color(0.12f, 0.28f, 0.045f));
            Material fruitRedMaterial = CreateMaterial(new Color(0.95f, 0.12f, 0.06f));
            Material fruitGoldMaterial = CreateMaterial(new Color(1f, 0.58f, 0.06f));
            Material flowerPinkMaterial = CreateMaterial(new Color(1f, 0.16f, 0.52f));
            Material flowerPurpleMaterial = CreateMaterial(new Color(0.62f, 0.18f, 0.95f));
            Material flowerYellowMaterial = CreateMaterial(new Color(1f, 0.84f, 0.12f));
            Material flowerRedMaterial = CreateMaterial(new Color(0.95f, 0.15f, 0.1f));
            Material flowerWhiteMaterial = CreateMaterial(new Color(0.97f, 0.95f, 0.88f));
            Material flowerStemMaterial = CreateMaterial(new Color(0.12f, 0.42f, 0.08f));
            Material waterMaterial = CreateMaterial(new Color(0.08f, 0.58f, 0.95f));
            Material cloudMaterial = CreateMaterial(new Color(0.96f, 0.98f, 1f));
            Material cloudShadeMaterial = CreateMaterial(new Color(0.7f, 0.84f, 0.95f));
            Material trailMaterial = CreateDirtTrailMaterial(natureCatalog != null ? natureCatalog.DirtGround : null);
            Material[] grassBladeMaterials = CreateGrassDetailMaterials(natureCatalog);
            Material shoreMaterial = CreateNaturalSurfaceMaterial(
                natureCatalog != null ? natureCatalog.DryGround : null,
                new Color(0.72f, 0.58f, 0.38f), 3.5f);
            Material reedMaterial = CreateMaterial(new Color(0.24f, 0.48f, 0.055f));
            Material lilyMaterial = CreateMaterial(new Color(0.08f, 0.48f, 0.13f));
            Material lakeDepthMaterial = CreateMaterial(new Color(0.025f, 0.3f, 0.5f));
            if (lakeDepthMaterial.HasProperty("_Cull")) lakeDepthMaterial.SetFloat("_Cull", (float)CullMode.Off);
            Material lakeWaterMaterial = CreateWaterMaterial(new Color(0.045f, 0.55f, 0.82f, 0.72f));

            BuildTrailRoutes();

            CreateGround(generated.transform, groundMaterial);
            CreateBoundaries(generated.transform);
            CreateCentralLake(generated.transform, lakeWaterMaterial, lakeDepthMaterial, shoreMaterial, rockMaterial, reedMaterial, lilyMaterial);

            Transform treesRoot = new GameObject("Trees").transform;
            treesRoot.SetParent(generated.transform, false);
            treesRoot.gameObject.AddComponent<TreeWindSystem>();
            WindZone treeWind = treesRoot.gameObject.AddComponent<WindZone>();
            treeWind.mode = WindZoneMode.Directional;
            treeWind.windMain = 0.24f;
            treeWind.windTurbulence = 0.32f;
            treeWind.windPulseMagnitude = 0.28f;
            treeWind.windPulseFrequency = 0.18f;
            treesRoot.gameObject.AddComponent<VineIndicatorSystem>();
            for (int i = 0; i < treeCount; i++)
            {
                Vector3 position = RandomMapPosition(8f, 4.2f);
                GameObject natureTree = natureCatalog != null && natureCatalog.HasTrees && Random.value < natureTreeShare
                    ? natureCatalog.GetRandomTree()
                    : null;
                CreateTree(treesRoot, position, trunkMaterial, Random.value > 0.45f ? leafMaterial : leafLightMaterial,
                    vineMaterial, fruitRedMaterial, fruitGoldMaterial, natureTree);
            }

            Transform bushesRoot = new GameObject("Bushes").transform;
            bushesRoot.SetParent(generated.transform, false);
            for (int i = 0; i < bushCount; i++)
            {
                Vector3 position = RandomMapPosition(5f, 3.5f);
                GameObject natureBush = natureCatalog != null && natureCatalog.HasBushes && Random.value < natureBushShare
                    ? natureCatalog.GetRandomBush()
                    : null;
                CreateBush(bushesRoot, position, bushMaterial, natureBush);
            }

            Transform grassRoot = new GameObject("GrassFields").transform;
            grassRoot.SetParent(generated.transform, false);
            CreateGrassField(grassRoot, grassBladeMaterials);

            Transform rocksRoot = new GameObject("Rocks").transform;
            rocksRoot.SetParent(generated.transform, false);
            for (int i = 0; i < rockCount; i++)
            {
                Vector3 position = RandomMapPosition(7f);
                CreateRock(rocksRoot, position, rockMaterial);
            }

            Transform anthillsRoot = new GameObject("Anthills").transform;
            anthillsRoot.SetParent(generated.transform, false);
            for (int i = 0; i < anthillCount; i++)
            {
                Vector3 position = RandomMapPosition(15f);
                CreateAnthill(anthillsRoot, position, moundMaterial, moundDarkMaterial);
            }

            Transform mountainsRoot = new GameObject("RockFormations").transform;
            mountainsRoot.SetParent(generated.transform, false);
            for (int i = 0; i < mountainCount; i++)
            {
                Vector3 position = GetMountainPosition(i);
                CreateRockFormation(mountainsRoot, position, rockMaterial);
            }

            Transform eagleMountainsRoot = new GameObject("EagleMountains").transform;
            eagleMountainsRoot.SetParent(generated.transform, false);
            for (int i = 0; i < eagleMountainCount; i++)
            {
                Vector3 position = RandomMapPosition(52f, 15f);
                CreateEagleMountain(eagleMountainsRoot, position, rockMaterial);
            }

            Transform backdropRoot = new GameObject("JungleBackdrop").transform;
            backdropRoot.SetParent(generated.transform, false);
            CreateJungleBackdrop(backdropRoot, distantRockMaterial, distantPeakMaterial, leafDeepMaterial, leafMaterial, leafLightMaterial);

            Transform housesRoot = new GameObject("HideoutHouses").transform;
            housesRoot.SetParent(generated.transform, false);
            for (int i = 0; i < houseCount; i++)
            {
                Vector3 position = RandomMapPosition(24f);
                CreateHouse(housesRoot, position, houseWallMaterial, houseRoofMaterial);
            }

            // Scattered life orbs: each fully restores (100%) the animal's health.
            Transform lifeRoot = new GameObject("LifePickups").transform;
            lifeRoot.SetParent(generated.transform, false);
            Vector3 playerShoreSpawn = GetShoreSpawnPosition();
            Vector3 pickupInward = new Vector3(-playerShoreSpawn.x, 0f, -playerShoreSpawn.z).normalized;
            Vector3 pickupRight = Vector3.Cross(Vector3.up, pickupInward).normalized;
            for (int i = 0; i < lifePickupCount; i++)
            {
                Vector3 position = i switch
                {
                    0 => GetGroundPosition(playerShoreSpawn + pickupInward * 9f - pickupRight * 4f),
                    1 => GetGroundPosition(playerShoreSpawn + pickupInward * 17f + pickupRight * 5f),
                    _ => RandomMapPosition(9f)
                };
                LifePickup.Create(position).transform.SetParent(lifeRoot, true);
            }

            Transform rangedSuppliesRoot = new GameObject("RangedAmmoSupplies").transform;
            rangedSuppliesRoot.SetParent(generated.transform, false);
            for (int i = 0; i < rangedSupplyCount; i++)
            {
                RangedSupplyKind kind = RangedSupplyKind.NaturalAmmo;
                Vector3 position = i switch
                {
                    0 => GetGroundPosition(playerShoreSpawn + pickupInward * 7f + pickupRight * 4f),
                    1 => GetGroundPosition(playerShoreSpawn + pickupInward * 14f - pickupRight * 5f),
                    2 => GetGroundPosition(playerShoreSpawn + pickupInward * 22f + pickupRight * 2f),
                    _ => RandomMapPosition(12f)
                };
                RangedAmmoPickup.Create(position, kind).transform.SetParent(rangedSuppliesRoot, true);
            }

            Transform flowersRoot = new GameObject("FlowerPatches").transform;
            flowersRoot.SetParent(generated.transform, false);
            Material[] petalPalette = { flowerPinkMaterial, flowerPurpleMaterial, flowerYellowMaterial, flowerRedMaterial, flowerWhiteMaterial };
            for (int i = 0; i < flowerPatchCount; i++)
            {
                CreateFlowerPatch(flowersRoot, RandomMapPosition(6f), flowerStemMaterial, petalPalette[Random.Range(0, petalPalette.Length)]);
            }

            Transform waterfallsRoot = new GameObject("Waterfalls").transform;
            waterfallsRoot.SetParent(generated.transform, false);
            for (int i = 0; i < waterfallCount; i++)
            {
                Vector3 position = RandomMapPosition(46f);
                CreateWaterfall(waterfallsRoot, position, waterMaterial, rockMaterial);
            }

            Transform cloudsRoot = new GameObject("StylizedSkyClouds").transform;
            cloudsRoot.SetParent(generated.transform, false);
            for (int i = 0; i < skyCloudCount; i++)
            {
                Vector3 position = new Vector3(Random.Range(-mapSize * 0.65f, mapSize * 0.65f), Random.Range(32f, 62f), Random.Range(-mapSize * 0.65f, mapSize * 0.65f));
                CreateCloud(cloudsRoot, position, cloudMaterial, cloudShadeMaterial);
            }

            Transform trailsRoot = new GameObject("JungleTrails").transform;
            trailsRoot.SetParent(generated.transform, false);
            CreateTrailNetwork(trailsRoot, trailMaterial);

            // Trees move in the centralized wind system. Their shared FBX meshes and
            // instanced materials are cheaper than static batching objects that move.
            StaticBatchingUtility.Combine(bushesRoot.gameObject);
            StaticBatchingUtility.Combine(grassRoot.gameObject);
            StaticBatchingUtility.Combine(rocksRoot.gameObject);
            StaticBatchingUtility.Combine(anthillsRoot.gameObject);
            StaticBatchingUtility.Combine(mountainsRoot.gameObject);
            StaticBatchingUtility.Combine(eagleMountainsRoot.gameObject);
            StaticBatchingUtility.Combine(backdropRoot.gameObject);
            StaticBatchingUtility.Combine(housesRoot.gameObject);
            StaticBatchingUtility.Combine(flowersRoot.gameObject);
            StaticBatchingUtility.Combine(cloudsRoot.gameObject);
            StaticBatchingUtility.Combine(trailsRoot.gameObject);

            RuntimeNavMeshSurface navigation = generated.AddComponent<RuntimeNavMeshSurface>();
            navigation.Configure(mapSize);
        }

        private void CreateGround(Transform parent, Material material)
        {
            GameObject ground = new GameObject("RollingForestGround");
            ground.transform.SetParent(parent, false);
            MeshFilter meshFilter = ground.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = ground.AddComponent<MeshRenderer>();
            groundCollider = ground.AddComponent<MeshCollider>();

            int resolution = terrainResolution;
            int vertexCount = (resolution + 1) * (resolution + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[resolution * resolution * 6];
            float half = mapSize * 0.5f;

            for (int z = 0; z <= resolution; z++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    int index = z * (resolution + 1) + x;
                    float worldX = Mathf.Lerp(-half, half, x / (float)resolution);
                    float worldZ = Mathf.Lerp(-half, half, z / (float)resolution);
                    vertices[index] = new Vector3(worldX, CalculateGroundHeight(worldX, worldZ), worldZ);
                    uvs[index] = new Vector2(x / (float)resolution * 12f, z / (float)resolution * 12f);
                }
            }

            int triangleIndex = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int root = z * (resolution + 1) + x;
                    triangles[triangleIndex++] = root;
                    triangles[triangleIndex++] = root + resolution + 1;
                    triangles[triangleIndex++] = root + 1;
                    triangles[triangleIndex++] = root + 1;
                    triangles[triangleIndex++] = root + resolution + 1;
                    triangles[triangleIndex++] = root + resolution + 2;
                }
            }

            Mesh mesh = new Mesh { name = "RollingForestGroundMesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshFilter.sharedMesh = mesh;
            groundCollider.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;
            ground.isStatic = true;
        }

        private Transform CreateCentralLake(Transform parent, Material waterMaterial, Material depthMaterial, Material shoreMaterial,
            Material rockMaterial, Material reedMaterial, Material lilyMaterial)
        {
            GameObject lake = new GameObject("CentralJungleLake");
            lake.transform.SetParent(parent, false);
            lake.transform.position = new Vector3(0f, lakeSurfaceHeight, 0f);
            CentralLake lakeVolume = lake.AddComponent<CentralLake>();
            lakeVolume.Configure(lakeRadius, lakeSurfaceHeight, lakeMovementMultiplier);

            GameObject shore = new GameObject("SandyLakeShore");
            shore.transform.SetParent(lake.transform, false);
            shore.transform.localPosition = Vector3.down * 0.2f;
            shore.AddComponent<MeshFilter>().sharedMesh = GetLakeShoreMesh();
            MeshRenderer shoreRenderer = shore.AddComponent<MeshRenderer>();
            shoreRenderer.sharedMaterial = shoreMaterial;
            shore.transform.localScale = Vector3.one * lakeRadius;
            shore.isStatic = true;

            GameObject depth = new GameObject("DeepBlueLakeBase");
            depth.transform.SetParent(lake.transform, false);
            depth.transform.localPosition = Vector3.down * 0.12f;
            depth.AddComponent<MeshFilter>().sharedMesh = GetLakeDiscMesh();
            MeshRenderer depthRenderer = depth.AddComponent<MeshRenderer>();
            depthRenderer.sharedMaterial = depthMaterial;
            depthRenderer.shadowCastingMode = ShadowCastingMode.Off;
            depthRenderer.receiveShadows = false;
            depth.transform.localScale = Vector3.one * (lakeRadius * 0.99f);

            GameObject water = new GameObject("TransparentLakeWater");
            water.transform.SetParent(lake.transform, false);
            water.AddComponent<MeshFilter>().sharedMesh = GetLakeDiscMesh();
            MeshRenderer waterRenderer = water.AddComponent<MeshRenderer>();
            waterRenderer.sharedMaterial = waterMaterial;
            waterRenderer.shadowCastingMode = ShadowCastingMode.Off;
            waterRenderer.receiveShadows = false;
            water.transform.localScale = Vector3.one * lakeRadius;
            water.AddComponent<LakeWaterMotion>();

            for (int i = 0; i < 18; i++)
            {
                float angle = i * Mathf.PI * 2f / 18f + Random.Range(-0.14f, 0.14f);
                float radius = lakeRadius * Random.Range(0.91f, 1.06f);
                Vector3 localPosition = new Vector3(Mathf.Cos(angle) * radius, -0.05f, Mathf.Sin(angle) * radius);
                if (i % 3 == 0)
                {
                    CreateLakePrimitive(lake.transform, PrimitiveType.Sphere, "ShoreRock", localPosition,
                        new Vector3(Random.Range(0.8f, 1.45f), Random.Range(0.45f, 0.8f), Random.Range(0.75f, 1.35f)), rockMaterial);
                }
                else
                {
                    int reedCount = Random.Range(2, 5);
                    for (int reed = 0; reed < reedCount; reed++)
                    {
                        Vector3 offset = new Vector3(Random.Range(-0.45f, 0.45f), 0f, Random.Range(-0.45f, 0.45f));
                        float height = Random.Range(0.8f, 1.65f);
                        CreateLakePrimitive(lake.transform, PrimitiveType.Cylinder, "LakeReed", localPosition + offset + Vector3.up * height * 0.5f,
                            new Vector3(0.055f, height * 0.5f, 0.055f), reedMaterial,
                            Quaternion.Euler(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f)));
                    }
                }
            }

            for (int i = 0; i < 14; i++)
            {
                Vector2 point = Random.insideUnitCircle * lakeRadius * 0.78f;
                CreateLakePrimitive(lake.transform, PrimitiveType.Cylinder, "LilyPad",
                    new Vector3(point.x, 0.055f, point.y), new Vector3(Random.Range(0.42f, 0.78f), 0.025f, Random.Range(0.35f, 0.68f)),
                    lilyMaterial, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }
            return lake.transform;
        }

        private static GameObject CreateLakePrimitive(Transform parent, PrimitiveType type, string name,
            Vector3 localPosition, Vector3 localScale, Material material, Quaternion? localRotation = null)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = localRotation ?? Quaternion.identity;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            part.isStatic = true;
            return part;
        }

        private void CreateBoundaries(Transform parent)
        {
            float half = mapSize * 0.5f;
            CreateBoundary(parent, new Vector3(0f, 10f, half), new Vector3(mapSize, 20f, 1f));
            CreateBoundary(parent, new Vector3(0f, 10f, -half), new Vector3(mapSize, 20f, 1f));
            CreateBoundary(parent, new Vector3(half, 10f, 0f), new Vector3(1f, 20f, mapSize));
            CreateBoundary(parent, new Vector3(-half, 10f, 0f), new Vector3(1f, 20f, mapSize));
        }

        private static void CreateBoundary(Transform parent, Vector3 position, Vector3 scale)
        {
            GameObject boundary = new GameObject("InvisibleBoundary");
            boundary.transform.SetParent(parent, false);
            boundary.transform.localPosition = position;
            BoxCollider collider = boundary.AddComponent<BoxCollider>();
            collider.size = scale;
            boundary.isStatic = true;
        }

        private static void CreateTree(Transform parent, Vector3 position, Material trunkMaterial, Material leafMaterial,
            Material vineMaterial, Material fruitRedMaterial, Material fruitGoldMaterial, GameObject natureTreePrefab)
        {
            if (natureTreePrefab != null)
            {
                CreateNatureTree(parent, position, natureTreePrefab, vineMaterial, fruitRedMaterial, fruitGoldMaterial);
                return;
            }

            if (cachedTreePrefab == null) cachedTreePrefab = Resources.Load<GameObject>("EnvironmentModels/JungleTree/JungleTree");
            if (cachedHighDetailTreePrefab == null) cachedHighDetailTreePrefab = Resources.Load<GameObject>("EnvironmentModels/JungleTreeHD/JungleTreeHD");
            bool useHighDetail = cachedHighDetailTreePrefab != null && Random.value < 0.18f;
            GameObject blenderTreePrefab = useHighDetail ? cachedHighDetailTreePrefab : cachedTreePrefab;
            if (blenderTreePrefab != null)
            {
                GameObject treeRoot = new GameObject(useHighDetail ? "HighDetailJungleTree" : "BlenderJungleTree");
                treeRoot.transform.SetParent(parent, false);
                treeRoot.transform.position = position;
                treeRoot.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                treeRoot.transform.localScale = Vector3.one * (useHighDetail ? Random.Range(0.76f, 1.08f) : Random.Range(0.9f, 1.45f));

                GameObject treeVisual = Object.Instantiate(blenderTreePrefab, treeRoot.transform, false);
                treeVisual.name = "TreeVisual";
                if (useHighDetail) EnhanceImportedMaterials(treeVisual);
                EnableRendererInstancing(treeVisual);
                treeVisual.AddComponent<TreeWindSway>();
                BoxCollider treeCollider = treeRoot.AddComponent<BoxCollider>();
                treeCollider.center = useHighDetail ? new Vector3(0f, 3.6f, 0f) : new Vector3(0f, 2.4f, 0f);
                treeCollider.size = useHighDetail ? new Vector3(1.75f, 7.2f, 1.75f) : new Vector3(1.2f, 4.8f, 1.2f);

                int registeredVines = VineAnchor.RegisterExistingVines(treeVisual.transform);
                if (registeredVines == 0)
                {
                    VineAnchor.Create(treeVisual.transform,
                        useHighDetail ? new Vector3(1.35f, 7.8f, 0.25f) : new Vector3(0.9f, 5.8f, 0.2f),
                        useHighDetail ? new Vector3(1.55f, 3.4f, 0.45f) : new Vector3(0.95f, 2.8f, 0.35f), vineMaterial);
                }
                if (Random.value < 0.24f)
                {
                    CreateDecorativeTreeFruit(treeRoot.transform, fruitRedMaterial, fruitGoldMaterial, vineMaterial);
                }
                ConfigureVisualOptimization(treeRoot, 0.018f);
                return;
            }

            GameObject tree = new GameObject("Tree");
            tree.transform.SetParent(parent, false);
            tree.transform.position = position;
            tree.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-2f, 2f));
            float scale = Random.Range(1.0f, 1.75f);
            tree.transform.localScale = Vector3.one * scale;
            tree.isStatic = true;

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            trunk.transform.localScale = new Vector3(0.42f, 2.6f, 0.42f);
            trunk.GetComponent<Renderer>().sharedMaterial = trunkMaterial;
            trunk.isStatic = true;

            for (int rootIndex = 0; rootIndex < 5; rootIndex++)
            {
                float angle = rootIndex * Mathf.PI * 2f / 5f + Random.Range(-0.28f, 0.28f);
                Vector3 rootStart = new Vector3(Mathf.Cos(angle) * 0.18f, 0.7f, Mathf.Sin(angle) * 0.18f);
                Vector3 rootEnd = new Vector3(Mathf.Cos(angle) * Random.Range(1.1f, 1.8f), 0.05f, Mathf.Sin(angle) * Random.Range(1.1f, 1.8f));
                CreateWoodLimb(tree.transform, "ButtressRoot", rootStart, rootEnd, Random.Range(0.14f, 0.22f), trunkMaterial);
            }

            for (int branchIndex = 0; branchIndex < 3; branchIndex++)
            {
                float angle = branchIndex * Mathf.PI * 2f / 3f + Random.Range(-0.3f, 0.3f);
                Vector3 branchStart = new Vector3(0f, Random.Range(3.8f, 4.8f), 0f);
                Vector3 branchEnd = new Vector3(Mathf.Cos(angle) * Random.Range(1.8f, 2.8f), Random.Range(5.0f, 6.2f), Mathf.Sin(angle) * Random.Range(1.8f, 2.8f));
                CreateWoodLimb(tree.transform, "Branch", branchStart, branchEnd, Random.Range(0.14f, 0.2f), trunkMaterial);
            }

            for (int i = 0; i < 3; i++)
            {
                GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.name = "Leaves";
                crown.transform.SetParent(tree.transform, false);
                crown.transform.localPosition = new Vector3(Random.Range(-0.7f, 0.7f), 5.1f + i * 0.7f, Random.Range(-0.7f, 0.7f));
                crown.transform.localScale = new Vector3(Random.Range(2.9f, 4.1f), Random.Range(1.7f, 2.7f), Random.Range(2.9f, 4.1f));
                Renderer renderer = crown.GetComponent<Renderer>();
                renderer.sharedMaterial = leafMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                Collider collider = crown.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                crown.isStatic = true;
            }

            Vector3 vineStart = new Vector3(Random.Range(-1.1f, 1.1f), Random.Range(4.8f, 6.6f), Random.Range(-1.1f, 1.1f));
            Vector3 vineEnd = vineStart + new Vector3(Random.Range(-1.4f, 1.4f), -Random.Range(2.4f, 4.2f), Random.Range(-1.4f, 1.4f));
            VineAnchor.Create(tree.transform, vineStart, vineEnd, vineMaterial);

            if (Random.value < 0.38f)
            {
                int fruitCount = Random.Range(4, 9);
                for (int i = 0; i < fruitCount; i++)
                {
                    GameObject fruit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    fruit.name = "JungleFruit";
                    fruit.transform.SetParent(tree.transform, false);
                    fruit.transform.localPosition = new Vector3(Random.Range(-1.8f, 1.8f), Random.Range(4.2f, 6.8f), Random.Range(-1.8f, 1.8f));
                    fruit.transform.localScale = Vector3.one * Random.Range(0.22f, 0.42f);
                    fruit.GetComponent<Renderer>().sharedMaterial = Random.value > 0.48f ? fruitRedMaterial : fruitGoldMaterial;
                    Collider fruitCollider = fruit.GetComponent<Collider>();
                    if (fruitCollider != null) Destroy(fruitCollider);
                    fruit.isStatic = true;
                }
            }
        }

        private static void CreateNatureTree(Transform parent, Vector3 position, GameObject prefab,
            Material vineMaterial, Material fruitRedMaterial, Material fruitGoldMaterial)
        {
            GameObject treeRoot = new GameObject("NatureStarterTree");
            treeRoot.transform.SetParent(parent, false);
            treeRoot.transform.position = position;
            treeRoot.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            treeRoot.transform.localScale = Vector3.one * Random.Range(0.82f, 1.18f);

            GameObject visual = InstantiateLegacyPrefab(prefab, treeRoot.transform);
            if (visual == null)
            {
                Object.Destroy(treeRoot);
                return;
            }
            visual.name = prefab.name + "_Visual";
            ApplyNatureRendererSettings(visual, true);

            // Tree Creator vegetation reacts to the shared Unity WindZone. The custom sway
            // component remains reserved for imported static meshes so trunks do not bend as one piece.
            if (VineAnchor.RegisterExistingVines(visual.transform) == 0)
            {
                VineAnchor.Create(visual.transform,
                    new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(5.6f, 7.4f), Random.Range(-0.5f, 0.5f)),
                    new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(2.0f, 3.2f), Random.Range(-0.8f, 0.8f)),
                    vineMaterial);
            }

            if (Random.value < 0.16f)
                CreateDecorativeTreeFruit(treeRoot.transform, fruitRedMaterial, fruitGoldMaterial, vineMaterial);

            ConfigureVisualOptimization(treeRoot, 0.014f);
        }

        private static void CreateWoodLimb(Transform parent, string name, Vector3 start, Vector3 end, float width, Material material)
        {
            Vector3 direction = end - start;
            GameObject limb = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            limb.name = name;
            limb.transform.SetParent(parent, false);
            limb.transform.localPosition = (start + end) * 0.5f;
            limb.transform.localScale = new Vector3(width, direction.magnitude * 0.5f, width);
            limb.transform.up = direction.normalized;
            limb.GetComponent<Renderer>().sharedMaterial = material;
            limb.isStatic = true;
        }

        private static void EnableRendererInstancing(GameObject root)
        {
            if (root == null) return;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                foreach (Material material in materials)
                {
                    if (material != null) material.enableInstancing = true;
                }
            }
        }

        private static void CreateDecorativeTreeFruit(Transform parent, Material redMaterial, Material goldMaterial, Material leafMaterial)
        {
            int count = Random.Range(2, 4);
            for (int i = 0; i < count; i++)
            {
                Vector3 position = new Vector3(Random.Range(-1.65f, 1.65f), Random.Range(4.9f, 6.8f), Random.Range(-1.65f, 1.65f));
                GameObject fruit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fruit.name = "TreeMango";
                fruit.transform.SetParent(parent, false);
                fruit.transform.localPosition = position;
                fruit.transform.localScale = new Vector3(Random.Range(0.22f, 0.32f), Random.Range(0.3f, 0.45f), Random.Range(0.22f, 0.32f));
                fruit.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-18f, 18f));
                fruit.GetComponent<Renderer>().sharedMaterial = Random.value > 0.45f ? goldMaterial : redMaterial;
                Collider fruitCollider = fruit.GetComponent<Collider>();
                if (fruitCollider != null) Destroy(fruitCollider);

                GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                leaf.name = "FruitLeaf";
                leaf.transform.SetParent(parent, false);
                leaf.transform.localPosition = position + new Vector3(0.12f, 0.31f, 0f);
                leaf.transform.localScale = new Vector3(0.3f, 0.045f, 0.14f);
                leaf.transform.localRotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-24f, 24f));
                leaf.GetComponent<Renderer>().sharedMaterial = leafMaterial;
                Collider leafCollider = leaf.GetComponent<Collider>();
                if (leafCollider != null) Destroy(leafCollider);
            }
        }

        private static void CreateBush(Transform parent, Vector3 position, Material material, GameObject natureBushPrefab)
        {
            if (natureBushPrefab != null)
            {
                GameObject natureBush = InstantiateLegacyPrefab(natureBushPrefab, parent);
                if (natureBush == null) return;
                natureBush.name = "NatureStarterBush";
                natureBush.transform.position = position;
                natureBush.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                natureBush.transform.localScale = Vector3.one * Random.Range(0.72f, 1.28f);
                ApplyNatureRendererSettings(natureBush, false);
                foreach (Collider natureCollider in natureBush.GetComponentsInChildren<Collider>(true))
                    Object.Destroy(natureCollider);
                ConfigureVisualOptimization(natureBush, 0.01f);
                return;
            }

            if (cachedBushPrefab == null) cachedBushPrefab = Resources.Load<GameObject>("EnvironmentModels/JungleBush/JungleBush");
            if (cachedBroadleafPrefab == null) cachedBroadleafPrefab = Resources.Load<GameObject>("EnvironmentModels/BroadleafClusterHD/BroadleafClusterHD");
            bool useHighDetail = cachedBroadleafPrefab != null && Random.value < 0.42f;
            GameObject bushPrefab = useHighDetail ? cachedBroadleafPrefab : cachedBushPrefab;
            if (bushPrefab != null)
            {
                GameObject bushModel = Object.Instantiate(bushPrefab, parent, false);
                bushModel.name = useHighDetail ? "HighDetailBroadleafPlant" : "BlenderJungleBush";
                bushModel.transform.position = position;
                bushModel.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                bushModel.transform.localScale = Vector3.one * (useHighDetail ? Random.Range(0.72f, 1.3f) : Random.Range(0.8f, 1.35f));
                if (useHighDetail) EnhanceImportedMaterials(bushModel);
                ConfigureVisualOptimization(bushModel, 0.012f);
                return;
            }

            GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bush.name = "Bush";
            bush.transform.SetParent(parent, false);
            bush.transform.position = position + Vector3.up * 0.7f;
            bush.transform.rotation = Random.rotationUniform;
            bush.transform.localScale = new Vector3(Random.Range(1.5f, 3.5f), Random.Range(1.0f, 2.1f), Random.Range(1.5f, 3.5f));
            Renderer renderer = bush.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            Collider collider = bush.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            bush.isStatic = true;
        }

        private static void ApplyNatureRendererSettings(GameObject root, bool castsShadows)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = castsShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    material.enableInstancing = true;
                    if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.08f);
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
                }
            }
        }

        private static GameObject InstantiateLegacyPrefab(GameObject prefab, Transform parent)
        {
            if (prefab == null) return null;

            // Force T to UnityEngine.Object. Unity 5 Tree Creator prefabs can clone through a
            // native subobject type, which makes Instantiate<GameObject> throw in Unity 6.
            Object clone = Object.Instantiate<Object>(prefab);
            GameObject instance = clone as GameObject;
            if (instance == null && clone is Component component)
                instance = component.gameObject;

            if (instance == null)
            {
                Debug.LogWarning($"O prefab antigo '{prefab.name}' não gerou um GameObject e foi ignorado.");
                if (clone != null) Object.Destroy(clone);
                return null;
            }

            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static void CreateRock(Transform parent, Vector3 position, Material material)
        {
            if (cachedRockPrefab == null) cachedRockPrefab = Resources.Load<GameObject>("EnvironmentModels/JungleRock/JungleRock");
            if (cachedMossyRockPrefab == null) cachedMossyRockPrefab = Resources.Load<GameObject>("EnvironmentModels/MossyRockHD/MossyRockHD");
            bool useHighDetail = cachedMossyRockPrefab != null && Random.value < 0.58f;
            GameObject rockPrefab = useHighDetail ? cachedMossyRockPrefab : cachedRockPrefab;
            if (rockPrefab != null)
            {
                GameObject boulder = Object.Instantiate(rockPrefab, parent, false);
                boulder.name = useHighDetail ? "HighDetailMossyRock" : "BlenderJungleRock";
                boulder.transform.position = position;
                boulder.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                float size = Random.Range(0.55f, 1.35f);
                boulder.transform.localScale = new Vector3(
                    size * Random.Range(0.85f, 1.2f), size * Random.Range(0.7f, 1.1f), size * Random.Range(0.85f, 1.2f));
                if (useHighDetail) EnhanceImportedMaterials(boulder);
                BoxCollider rockCollider = boulder.AddComponent<BoxCollider>();
                rockCollider.center = new Vector3(0f, 0.7f, 0f);
                rockCollider.size = new Vector3(2.2f, 1.4f, 1.8f);
                ConfigureVisualOptimization(boulder, 0.012f);
                return;
            }

            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.SetParent(parent, false);
            rock.transform.position = position + Vector3.up * 0.45f;
            rock.transform.rotation = Random.rotationUniform;
            rock.transform.localScale = new Vector3(Random.Range(0.7f, 2f), Random.Range(0.5f, 1.25f), Random.Range(0.7f, 2f));
            rock.GetComponent<Renderer>().sharedMaterial = material;
            rock.isStatic = true;
        }

        private static GameObject cachedTunnelHolePrefab;

        private static void CreateAnthill(Transform parent, Vector3 position, Material moundMaterial, Material entranceMaterial)
        {
            GameObject anthill = new GameObject("GiantAnthill");
            anthill.transform.SetParent(parent, false);
            anthill.transform.position = position;
            anthill.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            anthill.transform.localScale = Vector3.one * Random.Range(0.9f, 1.2f);
            anthill.isStatic = true;

            if (cachedTunnelHolePrefab == null)
                cachedTunnelHolePrefab = Resources.Load<GameObject>("Environment/TunnelHole");

            if (cachedTunnelHolePrefab != null)
            {
                GameObject hole = Object.Instantiate(cachedTunnelHolePrefab, anthill.transform, false);
                hole.name = "TunnelHoleVisual";
                foreach (Collider c in hole.GetComponentsInChildren<Collider>(true)) if (c != null) c.enabled = false;
            }
            else
            {
                // Fallback: the old procedural dirt mound.
                CreateMoundPart(anthill.transform, "MoundBase", new Vector3(0f, 0.72f, 0f), new Vector3(4.4f, 1.45f, 4.1f), moundMaterial);
                CreateMoundPart(anthill.transform, "MoundTop", new Vector3(0.15f, 1.5f, 0.05f), new Vector3(2.9f, 1.7f, 2.7f), moundMaterial);
                GameObject entrance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                entrance.name = "AnthillEntrance";
                entrance.transform.SetParent(anthill.transform, false);
                entrance.transform.localPosition = new Vector3(0f, 0.5f, 2.05f);
                entrance.transform.localScale = new Vector3(1.35f, 0.85f, 0.25f);
                entrance.GetComponent<Renderer>().sharedMaterial = entranceMaterial;
                Collider entranceCollider = entrance.GetComponent<Collider>();
                if (entranceCollider != null) Destroy(entranceCollider);
                entrance.isStatic = true;
            }

            // Make the hole a working entrance for the Ant's underground tunnel network.
            anthill.AddComponent<AntTunnelEntrance>();
        }

        private static void CreateRockFormation(Transform parent, Vector3 position, Material material)
        {
            if (cachedRockPrefab == null) cachedRockPrefab = Resources.Load<GameObject>("EnvironmentModels/JungleRock/JungleRock");

            GameObject formation = new GameObject("ClimbableRockFormation");
            formation.transform.SetParent(parent, false);
            formation.transform.position = position;
            formation.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float formationWidth = Random.Range(1.55f, 2.35f);
            formation.transform.localScale = new Vector3(
                formationWidth,
                formationWidth * Random.Range(0.82f, 1.08f),
                formationWidth * Random.Range(0.82f, 1.18f));
            formation.isStatic = true;

            if (cachedRockPrefab != null)
            {
                // A stepped pile of real boulders keeps the formation climbable
                // while reading as natural rock instead of stacked spheres.
                int boulderCount = Random.Range(4, 6);
                for (int i = 0; i < boulderCount; i++)
                {
                    float progress = i / (float)(boulderCount - 1);
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float spread = Mathf.Lerp(1.7f, 0.25f, progress);
                    GameObject boulder = Object.Instantiate(cachedRockPrefab, formation.transform, false);
                    boulder.name = "ClimbableBoulder";
                    boulder.transform.localPosition = new Vector3(
                        Mathf.Cos(angle) * spread, progress * 3.4f, Mathf.Sin(angle) * spread);
                    boulder.transform.localRotation = Quaternion.Euler(
                        Random.Range(-9f, 9f), Random.Range(0f, 360f), Random.Range(-9f, 9f));
                    float size = Mathf.Lerp(2.8f, 1.5f, progress) * Random.Range(0.88f, 1.15f);
                    boulder.transform.localScale = new Vector3(size, size * Random.Range(0.85f, 1.05f), size);
                    BoxCollider boulderCollider = boulder.AddComponent<BoxCollider>();
                    boulderCollider.center = new Vector3(0f, 0.7f, 0f);
                    boulderCollider.size = new Vector3(2.2f, 1.4f, 1.8f);
                    ConfigureVisualOptimization(boulder, 0.01f);
                }
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                GameObject boulder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                boulder.name = "ClimbableBoulder";
                boulder.transform.SetParent(formation.transform, false);
                boulder.transform.localPosition = new Vector3(Random.Range(-2.2f, 2.2f), 1.2f + i * 1.15f, Random.Range(-2.2f, 2.2f));
                boulder.transform.localScale = new Vector3(Random.Range(2.7f, 4.8f), Random.Range(2.5f, 4.7f), Random.Range(2.7f, 4.8f));
                boulder.transform.localRotation = Random.rotationUniform;
                boulder.GetComponent<Renderer>().sharedMaterial = material;
                boulder.isStatic = true;
            }
        }

        private static void CreateEagleMountain(Transform parent, Vector3 position, Material material)
        {
            if (cachedMountainPrefab == null) cachedMountainPrefab = Resources.Load<GameObject>("EnvironmentModels/JungleMountain/JungleMountain");
            if (cachedHighDetailMountainPrefab == null) cachedHighDetailMountainPrefab = Resources.Load<GameObject>("EnvironmentModels/MountainSpireHD/MountainSpireHD");
            bool useHighDetail = cachedHighDetailMountainPrefab != null && Random.value < 0.72f;
            GameObject mountainPrefab = useHighDetail ? cachedHighDetailMountainPrefab : cachedMountainPrefab;
            if (mountainPrefab != null)
            {
                GameObject mountainModel = Object.Instantiate(mountainPrefab, parent, false);
                mountainModel.name = useHighDetail ? "HighDetailMountainSpire" : "BlenderEagleMountain";
                mountainModel.transform.position = position;
                mountainModel.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                mountainModel.transform.localScale = Vector3.one * (useHighDetail ? Random.Range(0.92f, 1.38f) : Random.Range(2.2f, 3.4f));
                if (useHighDetail) EnhanceImportedMaterials(mountainModel);
                // The FBX root has no mesh of its own; the peaks are children. Box
                // colliders keep them solid and NavMesh-compatible even when the
                // imported mountain meshes do not have CPU read access in builds.
                foreach (MeshFilter meshFilter in mountainModel.GetComponentsInChildren<MeshFilter>())
                {
                    meshFilter.gameObject.AddComponent<BoxCollider>();
                }
                ConfigureVisualOptimization(mountainModel, 0.008f);
                return;
            }

            GameObject mountain = new GameObject("EaglePerchMountain");
            mountain.transform.SetParent(parent, false);
            mountain.transform.position = position;
            mountain.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            mountain.transform.localScale = Vector3.one * Random.Range(1.35f, 1.8f);
            mountain.isStatic = true;

            for (int i = 0; i < 5; i++)
            {
                GameObject peak = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                peak.name = "EagleMountainPeak";
                peak.transform.SetParent(mountain.transform, false);
                peak.transform.localPosition = new Vector3(Random.Range(-2.4f, 2.4f), 2.3f + i * 2.25f, Random.Range(-2.4f, 2.4f));
                peak.transform.localScale = new Vector3(Random.Range(5.2f, 8.4f), Random.Range(4.8f, 7.8f), Random.Range(5.2f, 8.4f));
                peak.transform.localRotation = Random.rotationUniform;
                peak.GetComponent<Renderer>().sharedMaterial = material;
                peak.isStatic = true;
            }
        }

        private void CreateJungleBackdrop(Transform parent, Material rockMaterial, Material peakMaterial,
            Material leafDeepMaterial, Material leafMaterial, Material leafLightMaterial)
        {
            float half = mapSize * 0.5f;
            for (int i = 0; i < backdropMountainCount; i++)
            {
                float angle = i * Mathf.PI * 2f / backdropMountainCount + Random.Range(-0.08f, 0.08f);
                float radius = Random.Range(half * 1.18f, half * 1.58f);
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                position.y = CalculateGroundHeight(position.x, position.z) - Random.Range(7f, 15f);

                GameObject mountain = new GameObject("DistantCartoonMountain");
                mountain.transform.SetParent(parent, false);
                mountain.transform.position = position;
                mountain.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                mountain.transform.localScale = new Vector3(
                    Random.Range(62f, 96f), Random.Range(58f, 94f), Random.Range(50f, 82f));
                MeshFilter filter = mountain.AddComponent<MeshFilter>();
                filter.sharedMesh = GetBackdropMountainMesh();
                MeshRenderer renderer = mountain.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { rockMaterial, peakMaterial };
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                mountain.isStatic = true;
            }

            for (int i = 0; i < distantCanopyCount; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(half * 1.02f, half * 1.34f);
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                position.y = CalculateGroundHeight(position.x, position.z) - 1.5f;
                CreateDistantCanopy(parent, position, leafDeepMaterial,
                    Random.value > 0.48f ? leafMaterial : leafLightMaterial);
            }
        }

        private static void CreateDistantCanopy(Transform parent, Vector3 position, Material deepMaterial, Material highlightMaterial)
        {
            GameObject canopy = new GameObject("DistantJungleCanopy");
            canopy.transform.SetParent(parent, false);
            canopy.transform.position = position;
            canopy.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float scale = Random.Range(1.5f, 3.1f);
            canopy.transform.localScale = Vector3.one * scale;
            canopy.isStatic = true;

            for (int i = 0; i < 3; i++)
            {
                GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.name = "LayeredCanopyCrown";
                crown.transform.SetParent(canopy.transform, false);
                crown.transform.localPosition = new Vector3((i - 1) * 1.35f, Random.Range(5.2f, 6.5f), Random.Range(-0.55f, 0.55f));
                crown.transform.localScale = new Vector3(Random.Range(2.2f, 3.2f), Random.Range(1.15f, 1.8f), Random.Range(2.0f, 2.9f));
                Renderer renderer = crown.GetComponent<Renderer>();
                renderer.sharedMaterial = i == 1 ? highlightMaterial : deepMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                Collider collider = crown.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                crown.isStatic = true;
            }
        }

        private static void CreateHouse(Transform parent, Vector3 position, Material wallMaterial, Material roofMaterial)
        {
            GameObject house = new GameObject("AnimalHideout");
            house.transform.SetParent(parent, false);
            house.transform.position = position;
            house.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            house.isStatic = true;

            CreateHousePart(house.transform, "Floor", new Vector3(0f, 0.1f, 0f), new Vector3(6f, 0.2f, 5.2f), wallMaterial);
            CreateHousePart(house.transform, "FrontDeck", new Vector3(0f, 0.12f, 3.45f), new Vector3(4.2f, 0.18f, 2.0f), wallMaterial);
            CreateHousePart(house.transform, "BackWall", new Vector3(0f, 1.6f, -2.45f), new Vector3(6f, 3.2f, 0.25f), wallMaterial);
            CreateHousePart(house.transform, "LeftWall", new Vector3(-2.85f, 1.6f, 0f), new Vector3(0.25f, 3.2f, 5.2f), wallMaterial);
            CreateHousePart(house.transform, "RightWall", new Vector3(2.85f, 1.6f, 0f), new Vector3(0.25f, 3.2f, 5.2f), wallMaterial);
            CreateHousePart(house.transform, "FrontLeft", new Vector3(-2.05f, 1.6f, 2.45f), new Vector3(1.5f, 3.2f, 0.25f), wallMaterial);
            CreateHousePart(house.transform, "FrontRight", new Vector3(2.05f, 1.6f, 2.45f), new Vector3(1.5f, 3.2f, 0.25f), wallMaterial);
            CreateHousePart(house.transform, "RoofLeft", new Vector3(-1.35f, 3.55f, 0f), new Vector3(3.35f, 0.25f, 5.65f), roofMaterial, new Vector3(0f, 0f, 24f));
            CreateHousePart(house.transform, "RoofRight", new Vector3(1.35f, 3.55f, 0f), new Vector3(3.35f, 0.25f, 5.65f), roofMaterial, new Vector3(0f, 0f, -24f));

            for (int i = -1; i <= 1; i += 2)
            {
                CreateHousePart(house.transform, "DeckPost", new Vector3(i * 1.75f, 0.8f, 4.12f), new Vector3(0.16f, 1.4f, 0.16f), roofMaterial);
                CreateHousePart(house.transform, "DeckRail", new Vector3(i * 1.75f, 1.25f, 3.45f), new Vector3(0.16f, 0.1f, 1.35f), roofMaterial);
            }
            CreateHousePart(house.transform, "DoorCanopy", new Vector3(0f, 3.05f, 2.75f), new Vector3(2.7f, 0.18f, 1.1f), roofMaterial, new Vector3(16f, 0f, 0f));
        }

        private static void CreateHousePart(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, Vector3? localEuler = null)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localEulerAngles = localEuler ?? Vector3.zero;
            part.GetComponent<Renderer>().sharedMaterial = material;
            part.isStatic = true;
        }

        private static void CreateFlowerPatch(Transform parent, Vector3 position, Material stemMaterial, Material petalMaterial)
        {
            if (cachedFlowerPrefab == null) cachedFlowerPrefab = Resources.Load<GameObject>("EnvironmentModels/JungleFlower/JungleFlower");
            if (cachedFlowerClusterPrefab == null) cachedFlowerClusterPrefab = Resources.Load<GameObject>("EnvironmentModels/FlowerClusterHD/FlowerClusterHD");
            if (cachedFlowerClusterPrefab != null && Random.value < 0.34f)
            {
                GameObject cluster = Object.Instantiate(cachedFlowerClusterPrefab, parent, false);
                cluster.name = "HighDetailFlowerCluster";
                cluster.transform.position = position;
                cluster.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                cluster.transform.localScale = Vector3.one * Random.Range(0.7f, 1.22f);
                EnhanceImportedMaterials(cluster);
                ConfigureVisualOptimization(cluster, 0.008f);
                return;
            }
            if (cachedFlowerPrefab != null)
            {
                GameObject flowerPatch = new GameObject("FlowerPatch");
                flowerPatch.transform.SetParent(parent, false);
                flowerPatch.transform.position = position;
                flowerPatch.isStatic = true;
                int flowerCount = Random.Range(4, 9);
                for (int i = 0; i < flowerCount; i++)
                {
                    GameObject flower = Object.Instantiate(cachedFlowerPrefab, flowerPatch.transform, false);
                    flower.name = "JungleFlower";
                    flower.transform.localPosition = new Vector3(Random.Range(-1.6f, 1.6f), 0f, Random.Range(-1.6f, 1.6f));
                    flower.transform.localRotation = Quaternion.Euler(Random.Range(-7f, 7f), Random.Range(0f, 360f), Random.Range(-7f, 7f));
                    flower.transform.localScale = Vector3.one * Random.Range(0.8f, 1.5f);
                    // Most of the patch shares one color; the strays keep the
                    // model's own petal color so fields never look uniform.
                    if (Random.value < 0.8f) TintPetals(flower, petalMaterial);
                }
                return;
            }

            GameObject patch = new GameObject("FlowerPatch");
            patch.transform.SetParent(parent, false);
            patch.transform.position = position;
            patch.isStatic = true;
            int count = Random.Range(3, 7);
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-1.2f, 1.2f));
                float height = Random.Range(0.24f, 0.52f);
                GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stem.name = "FlowerStem";
                stem.transform.SetParent(patch.transform, false);
                stem.transform.localPosition = offset + Vector3.up * (height * 0.5f);
                stem.transform.localScale = new Vector3(0.035f, height * 0.5f, 0.035f);
                stem.GetComponent<Renderer>().sharedMaterial = stemMaterial;
                Collider stemCollider = stem.GetComponent<Collider>();
                if (stemCollider != null) Destroy(stemCollider);

                GameObject flower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flower.name = "Flower";
                flower.transform.SetParent(patch.transform, false);
                flower.transform.localPosition = offset + Vector3.up * height;
                flower.transform.localScale = new Vector3(0.25f, 0.1f, 0.25f);
                flower.GetComponent<Renderer>().sharedMaterial = petalMaterial;
                Collider flowerCollider = flower.GetComponent<Collider>();
                if (flowerCollider != null) Destroy(flowerCollider);
            }
        }

        private static void TintPetals(GameObject flower, Material petalMaterial)
        {
            foreach (Renderer renderer in flower.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materials[i].name.StartsWith("FlowerPetal"))
                    {
                        materials[i] = petalMaterial;
                        changed = true;
                    }
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        private static void CreateCrystalCluster(Transform parent, Vector3 position, Material blueMaterial, Material purpleMaterial, Material rockMaterial)
        {
            GameObject cluster = new GameObject("CrystalCluster");
            cluster.transform.SetParent(parent, false);
            cluster.transform.position = position;
            cluster.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            cluster.isStatic = true;

            GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "CrystalGlowBase";
            glow.transform.SetParent(cluster.transform, false);
            glow.transform.localPosition = Vector3.up * 0.12f;
            glow.transform.localScale = new Vector3(1.6f, 0.12f, 1.6f);
            glow.GetComponent<Renderer>().sharedMaterial = Random.value > 0.5f ? blueMaterial : purpleMaterial;
            Collider glowCollider = glow.GetComponent<Collider>();
            if (glowCollider != null) Destroy(glowCollider);

            for (int i = 0; i < 4; i++)
            {
                GameObject baseRock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                baseRock.name = "CrystalBaseRock";
                baseRock.transform.SetParent(cluster.transform, false);
                float angle = i * Mathf.PI * 0.5f + Random.Range(-0.3f, 0.3f);
                baseRock.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.72f, 0.16f, Mathf.Sin(angle) * 0.72f);
                baseRock.transform.localScale = new Vector3(Random.Range(0.45f, 0.75f), Random.Range(0.2f, 0.38f), Random.Range(0.45f, 0.75f));
                baseRock.GetComponent<Renderer>().sharedMaterial = rockMaterial;
                Collider rockCollider = baseRock.GetComponent<Collider>();
                if (rockCollider != null) Destroy(rockCollider);
            }

            int count = Random.Range(3, 7);
            for (int i = 0; i < count; i++)
            {
                GameObject crystal = new GameObject("MagicCrystal");
                crystal.name = "MagicCrystal";
                crystal.transform.SetParent(cluster.transform, false);
                crystal.transform.localPosition = new Vector3(Random.Range(-0.72f, 0.72f), Random.Range(0.55f, 0.95f), Random.Range(-0.72f, 0.72f));
                float height = i == 0 ? Random.Range(1.5f, 2.15f) : Random.Range(0.72f, 1.5f);
                crystal.transform.localScale = new Vector3(height * Random.Range(0.22f, 0.3f), height, height * Random.Range(0.22f, 0.3f));
                crystal.transform.localRotation = Quaternion.Euler(Random.Range(-14f, 14f), Random.Range(0f, 360f), Random.Range(-14f, 14f));
                MeshFilter filter = crystal.AddComponent<MeshFilter>();
                filter.sharedMesh = GetCrystalMesh();
                MeshRenderer renderer = crystal.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Random.value > 0.48f ? blueMaterial : purpleMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static void CreateWaterfall(Transform parent, Vector3 position, Material waterMaterial, Material rockMaterial)
        {
            GameObject landmark = new GameObject("JungleWaterfall");
            landmark.transform.SetParent(parent, false);
            landmark.transform.position = position;
            landmark.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            landmark.isStatic = true;

            GameObject cliff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cliff.name = "WaterfallCliff";
            cliff.transform.SetParent(landmark.transform, false);
            cliff.transform.localPosition = new Vector3(0f, 5.2f, 0f);
            cliff.transform.localScale = new Vector3(10f, 10f, 4.5f);
            cliff.GetComponent<Renderer>().sharedMaterial = rockMaterial;
            cliff.isStatic = true;

            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
            water.name = "Waterfall";
            water.transform.SetParent(landmark.transform, false);
            water.transform.localPosition = new Vector3(0f, 5.1f, 2.35f);
            water.transform.localScale = new Vector3(3.1f, 9.8f, 0.16f);
            water.GetComponent<Renderer>().sharedMaterial = waterMaterial;
            Collider waterCollider = water.GetComponent<Collider>();
            if (waterCollider != null) Destroy(waterCollider);
            water.isStatic = true;

            GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pool.name = "WaterfallPool";
            pool.transform.SetParent(landmark.transform, false);
            pool.transform.localPosition = new Vector3(0f, 0.18f, 2.3f);
            pool.transform.localScale = new Vector3(3.8f, 0.08f, 2.4f);
            pool.GetComponent<Renderer>().sharedMaterial = waterMaterial;
            Collider poolCollider = pool.GetComponent<Collider>();
            if (poolCollider != null) Destroy(poolCollider);

            GameObject spray = new GameObject("WaterfallSpray");
            spray.transform.SetParent(landmark.transform, false);
            spray.transform.localPosition = new Vector3(0f, 0.8f, 2.45f);
            ParticleSystem particles = spray.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = 1.1f;
            main.startSpeed = 2.2f;
            main.startSize = 0.18f;
            main.startColor = new Color(0.7f, 0.95f, 1f, 0.85f);
            main.maxParticles = 90;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 38f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 1.3f;
        }

        private static void CreateCloud(Transform parent, Vector3 position, Material cloudMaterial, Material cloudShadeMaterial)
        {
            GameObject cloud = new GameObject("CartoonCloud");
            cloud.transform.SetParent(parent, false);
            cloud.transform.position = position;
            cloud.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            cloud.transform.localScale = Vector3.one * Random.Range(1.6f, 3.4f);
            cloud.isStatic = true;
            int puffCount = Random.Range(3, 6);
            for (int i = 0; i < puffCount; i++)
            {
                GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.name = "CloudPuff";
                puff.transform.SetParent(cloud.transform, false);
                puff.transform.localPosition = new Vector3(i * 1.1f - puffCount * 0.55f, Random.Range(-0.25f, 0.35f), Random.Range(-0.35f, 0.35f));
                puff.transform.localScale = new Vector3(Random.Range(1.3f, 2.2f), Random.Range(0.7f, 1.25f), Random.Range(1.1f, 1.8f));
                puff.GetComponent<Renderer>().sharedMaterial = i == 0 ? cloudShadeMaterial : cloudMaterial;
                Collider puffCollider = puff.GetComponent<Collider>();
                if (puffCollider != null) Destroy(puffCollider);
                puff.isStatic = true;
            }
        }

        private void CreateTrailNetwork(Transform parent, Material trailMaterial)
        {
            foreach (TrailRoute route in trailRoutes)
            {
                CreateTrailRibbon(parent, route, trailMaterial);
            }
        }

        private void CreateGrassField(Transform parent, Material[] materials)
        {
            const int tuftsPerChunk = 240;
            int created = 0;
            int chunkIndex = 0;
            while (created < grassTuftCount)
            {
                int tuftCount = Mathf.Min(tuftsPerChunk, grassTuftCount - created);
                List<Vector3> vertices = new List<Vector3>(tuftCount * 8);
                List<Vector2> uvs = new List<Vector2>(tuftCount * 8);
                List<int> triangles = new List<int>(tuftCount * 12);

                for (int i = 0; i < tuftCount; i++)
                {
                    Vector3 center = RandomMapPosition(6f, 5.3f) + Vector3.up * 0.015f;
                    float height = Random.Range(0.28f, 0.68f);
                    float width = Random.Range(0.07f, 0.14f);
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    Vector3 firstAxis = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 secondAxis = Vector3.Cross(Vector3.up, firstAxis).normalized;
                    Vector3 lean = new Vector3(Random.Range(-0.08f, 0.08f), 0f, Random.Range(-0.08f, 0.08f));
                    AddGrassQuad(vertices, uvs, triangles, center, firstAxis, lean, width, height);
                    AddGrassQuad(vertices, uvs, triangles, center, secondAxis, lean, width, height * Random.Range(0.86f, 1.05f));
                }

                GameObject chunk = new GameObject($"GrassMeshChunk_{chunkIndex++}");
                chunk.transform.SetParent(parent, false);
                Mesh mesh = new Mesh { name = "CrossedGrassTuftsMesh" };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = chunk.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = materials[(chunkIndex - 1) % materials.Length];
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                chunk.isStatic = true;
                created += tuftCount;
            }
        }

        private static void AddGrassQuad(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles,
            Vector3 center, Vector3 widthAxis, Vector3 lean, float halfWidth, float height)
        {
            int start = vertices.Count;
            Vector3 bottomLeft = center - widthAxis * halfWidth;
            Vector3 bottomRight = center + widthAxis * halfWidth;
            Vector3 tipCenter = center + Vector3.up * height + lean;
            Vector3 topLeft = tipCenter - widthAxis * (halfWidth * 0.32f);
            Vector3 topRight = tipCenter + widthAxis * (halfWidth * 0.32f);
            vertices.Add(bottomLeft);
            vertices.Add(topLeft);
            vertices.Add(topRight);
            vertices.Add(bottomRight);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private void BuildTrailRoutes()
        {
            trailRoutes.Clear();
            int routeCount = Mathf.Max(1, trailBranchCount);
            for (int branch = 0; branch < routeCount; branch++)
            {
                float angle = branch * Mathf.PI * 2f / routeCount + Random.Range(-0.16f, 0.16f);
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 start = direction * (lakeRadius + 5f);
                Vector3 destination = direction * Random.Range(112f, 158f);
                Vector3 lateral = Vector3.Cross(Vector3.up, direction).normalized;
                trailRoutes.Add(new TrailRoute(start, destination, lateral, Random.Range(0f, Mathf.PI * 2f)));
            }
        }

        private Vector3 EvaluateTrailPosition(TrailRoute route, float progress)
        {
            progress = Mathf.Clamp01(progress);
            float edgeFade = Mathf.Sin(progress * Mathf.PI);
            float curve = Mathf.Sin(progress * Mathf.PI * 2f + route.WavePhase) * edgeFade * 8f;
            Vector3 position = Vector3.Lerp(route.Start, route.Destination, progress) + route.Lateral * curve;
            position.y = CalculateGroundHeight(position.x, position.z);
            return position;
        }

        private void CreateTrailRibbon(Transform parent, TrailRoute route, Material material)
        {
            const int segments = 32;
            const float halfWidth = 2.35f;
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float progress = i / (float)segments;
                Vector3 center = EvaluateTrailPosition(route, progress);
                Vector3 before = EvaluateTrailPosition(route, Mathf.Max(0f, progress - 0.015f));
                Vector3 after = EvaluateTrailPosition(route, Mathf.Min(1f, progress + 0.015f));
                Vector3 tangent = after - before;
                tangent.y = 0f;
                if (tangent.sqrMagnitude < 0.001f) tangent = route.Destination - route.Start;
                Vector3 right = Vector3.Cross(Vector3.up, tangent.normalized);

                float widthVariation = 1f + Mathf.Sin(progress * 23f + route.WavePhase) * 0.1f;
                Vector3 leftPosition = center - right * (halfWidth * widthVariation);
                Vector3 rightPosition = center + right * (halfWidth * widthVariation);
                leftPosition.y = CalculateGroundHeight(leftPosition.x, leftPosition.z) + 0.09f;
                rightPosition.y = CalculateGroundHeight(rightPosition.x, rightPosition.z) + 0.09f;
                vertices[i * 2] = leftPosition;
                vertices[i * 2 + 1] = rightPosition;
                uvs[i * 2] = new Vector2(0f, progress * 12f);
                uvs[i * 2 + 1] = new Vector2(1f, progress * 12f);

                if (i == segments) continue;
                int triangle = i * 6;
                int vertex = i * 2;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }

            GameObject trail = new GameObject("TerrainFollowingDirtTrail");
            trail.transform.SetParent(parent, false);
            Mesh mesh = new Mesh { name = "DirtPathRibbonMesh" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            trail.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = trail.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            trail.isStatic = true;
        }

        private static void CreateMoundPart(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            part.isStatic = true;
        }

        private Vector3 RandomMapPosition(float centerClearRadius, float trailClearance = 0f)
        {
            float half = mapSize * 0.5f - 3f;
            float requiredClearRadius = Mathf.Max(centerClearRadius, lakeRadius + 4f);
            Vector2 point;
            int attempts = 0;
            do
            {
                point = new Vector2(Random.Range(-half, half), Random.Range(-half, half));
                attempts++;
            } while ((point.magnitude < requiredClearRadius || IsNearTrail(point, trailClearance)) && attempts < 50);

            return new Vector3(point.x, CalculateGroundHeight(point.x, point.y), point.y);
        }

        private bool IsNearTrail(Vector2 point, float clearance)
        {
            if (clearance <= 0f || trailRoutes.Count == 0) return false;
            float clearanceSqr = clearance * clearance;
            foreach (TrailRoute route in trailRoutes)
            {
                const int samples = 18;
                Vector3 previous = EvaluateTrailPosition(route, 0f);
                for (int i = 1; i <= samples; i++)
                {
                    Vector3 current = EvaluateTrailPosition(route, i / (float)samples);
                    if (SqrDistanceToSegment(point, new Vector2(previous.x, previous.z),
                            new Vector2(current.x, current.z)) <= clearanceSqr)
                    {
                        return true;
                    }
                    previous = current;
                }
            }
            return false;
        }

        private static float SqrDistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            if (segment.sqrMagnitude < 0.0001f) return (point - start).sqrMagnitude;
            float progress = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
            return (point - (start + segment * progress)).sqrMagnitude;
        }

        private Vector3 GetMountainPosition(int index)
        {
            int corridorSlots = trailRoutes.Count * 4;
            if (index >= corridorSlots || trailRoutes.Count == 0) return RandomMapPosition(34f, 11f);

            TrailRoute route = trailRoutes[(index / 4) % trailRoutes.Count];
            int slot = index % 4;
            float progress = slot < 2 ? Random.Range(0.4f, 0.5f) : Random.Range(0.68f, 0.8f);
            float side = (slot & 1) == 0 ? -1f : 1f;
            Vector3 position = EvaluateTrailPosition(route, progress)
                + route.Lateral * (side * Random.Range(12f, 16f));
            position.y = CalculateGroundHeight(position.x, position.z);
            return position;
        }

        private float CalculateGroundHeight(float x, float z)
        {
            // PerlinNoise loses precision and can return NaN when a randomized
            // 32-bit seed is multiplied directly into billion-scale coordinates.
            // Fold the seed into stable offsets while preserving all seed bits.
            uint seedBits = unchecked((uint)seed);
            float seedX = (seedBits & 0xffffu) * (2048f / 65535f);
            float seedZ = (seedBits >> 16) * (2048f / 65535f);
            float broadNoise = Mathf.PerlinNoise((x + seedX * 1.37f) * 0.012f, (z - seedZ * 0.73f) * 0.012f) - 0.5f;
            float detailNoise = Mathf.PerlinNoise((x - seedZ * 0.91f) * 0.042f, (z + seedX * 0.58f) * 0.042f) - 0.5f;
            float ridgeSample = Mathf.PerlinNoise((x + seedX * 0.43f) * 0.0075f, (z - seedZ * 0.31f) * 0.0075f);
            float ridgeNoise = Mathf.Pow(1f - Mathf.Abs(ridgeSample * 2f - 1f), 2.8f);
            float rolling = broadNoise * terrainHeight * 1.45f + detailNoise * terrainHeight * 0.32f;
            float shallowValley = Mathf.Sin((x + z) * 0.018f) * terrainHeight * 0.18f;
            float naturalHeight = rolling + shallowValley + ridgeNoise * terrainHeight * 0.38f;
            float distanceFromCenter = new Vector2(x, z).magnitude;
            float shoreProgress = Mathf.InverseLerp(lakeRadius, lakeRadius + 9f, distanceFromCenter);
            float lakeInfluence = 1f - Mathf.SmoothStep(0f, 1f, shoreProgress);
            if (lakeInfluence <= 0f) return naturalHeight;
            float depthProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distanceFromCenter / lakeRadius));
            float lakeBed = Mathf.Lerp(lakeSurfaceHeight - 3.15f, lakeSurfaceHeight - 0.38f, depthProgress);
            return Mathf.Lerp(naturalHeight, lakeBed, lakeInfluence);
        }

        private void ClearGeneratedWorld()
        {
            Transform existing = transform.Find("GeneratedJungle");
            if (existing == null) return;
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        private readonly struct TrailRoute
        {
            public readonly Vector3 Start;
            public readonly Vector3 Destination;
            public readonly Vector3 Lateral;
            public readonly float WavePhase;

            public TrailRoute(Vector3 start, Vector3 destination, Vector3 lateral, float wavePhase)
            {
                Start = start;
                Destination = destination;
                Lateral = lateral;
                WavePhase = wavePhase;
            }
        }

    }
}
