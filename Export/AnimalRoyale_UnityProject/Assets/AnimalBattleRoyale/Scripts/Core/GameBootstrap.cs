using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    [DefaultExecutionOrder(-200)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Prototype")]
        [SerializeField, Range(1, 30)] private int botCount = 15;
        [SerializeField] private AnimalType initialAnimal = AnimalType.Tiger;
        [SerializeField] private bool generateJungleOnStart = true;

        private JungleGenerator jungle;
        private Camera gameCamera;
        private bool matchStarted;
        private static Material cartoonSkyMaterial;

        private void Awake()
        {
            ConfigureRendering();
            EnsureManager();
            jungle = EnsureJungle();
            if (generateJungleOnStart) jungle.Generate();
            EnsureSafeZone();
            EnsureDiamondObjective(jungle);
            EnsureMissionDirector(jungle);
            EnsureAmbientAudio();
            gameCamera = EnsureCamera();
            ConfigureMenuCamera(gameCamera);
            CreateCharacterMenu();
        }

        public void StartMatch(AnimalType selectedAnimal)
        {
            if (matchStarted) return;
            matchStarted = true;
            ThirdPersonAnimalController player = SpawnPlayer(selectedAnimal, gameCamera.transform);
            ConfigureCamera(gameCamera, player.transform);
            SpawnBots(jungle.MapSize);
            AmbientAudioController.Instance?.BeginMatch();
            SafeZoneController.Instance?.BeginMatch();
            ForestMissionDirector.Instance?.BeginMatch(player);
        }

        private static void ConfigureRendering()
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.31f, 0.61f, 0.68f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0038f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.46f, 0.57f, 0.39f);
            RenderSettings.reflectionIntensity = 0.72f;

            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                if (cartoonSkyMaterial == null) cartoonSkyMaterial = new Material(skyShader) { name = "CartoonJungleSky" };
                cartoonSkyMaterial.SetColor("_SkyTint", new Color(0.12f, 0.48f, 0.95f));
                cartoonSkyMaterial.SetColor("_GroundColor", new Color(0.26f, 0.45f, 0.31f));
                cartoonSkyMaterial.SetFloat("_AtmosphereThickness", 0.82f);
                cartoonSkyMaterial.SetFloat("_Exposure", 1.25f);
                cartoonSkyMaterial.SetFloat("_SunSize", 0.035f);
                RenderSettings.skybox = cartoonSkyMaterial;
            }

            Light light = FindAnyObjectByType<Light>();
            if (light == null)
            {
                GameObject lightObject = new GameObject("Sun");
                light = lightObject.AddComponent<Light>();
            }
            light.type = LightType.Directional;
            light.intensity = 1.42f;
            light.color = new Color(1f, 0.89f, 0.69f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.72f;
            light.shadowBias = 0.055f;
            light.shadowNormalBias = 0.32f;
            light.transform.rotation = Quaternion.Euler(46f, -34f, 0f);
        }

        private static void EnsureManager()
        {
            if (BattleRoyaleManager.Instance != null) return;
            GameObject managerObject = new GameObject("BattleRoyaleManager");
            managerObject.AddComponent<BattleRoyaleManager>();
        }

        private JungleGenerator EnsureJungle()
        {
            JungleGenerator jungle = FindAnyObjectByType<JungleGenerator>();
            if (jungle != null) return jungle;

            GameObject world = new GameObject("JungleWorld");
            return world.AddComponent<JungleGenerator>();
        }

        private static void EnsureSafeZone()
        {
            if (SafeZoneController.Instance != null) return;
            GameObject zone = new GameObject("SafeZone");
            zone.transform.position = Vector3.zero;
            zone.AddComponent<SafeZoneController>();
        }

        private static void EnsureDiamondObjective(JungleGenerator jungle)
        {
            DiamondObjectiveManager objective = DiamondObjectiveManager.Instance;
            if (objective == null)
            {
                GameObject objectiveObject = new GameObject("DiamondEscapeObjective");
                objective = objectiveObject.AddComponent<DiamondObjectiveManager>();
            }
            objective.Initialize(jungle);
        }

        private static void EnsureAmbientAudio()
        {
            if (AmbientAudioController.Instance != null) return;
            GameObject ambienceObject = new GameObject("AmbientAudioController");
            ambienceObject.AddComponent<AmbientAudioController>();
        }

        private static void EnsureMissionDirector(JungleGenerator jungle)
        {
            ForestMissionDirector director = ForestMissionDirector.Instance;
            if (director == null)
            {
                GameObject directorObject = new GameObject("ForestMissionDirector");
                director = directorObject.AddComponent<ForestMissionDirector>();
            }
            director.Initialize(jungle);
        }

        private static Camera EnsureCamera()
        {
            Camera camera = Camera.main;
            if (camera != null) return camera;

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private ThirdPersonAnimalController SpawnPlayer(AnimalType selectedAnimal, Transform cameraTransform)
        {
            Vector3 spawnPosition = jungle.GetShoreSpawnPosition();
            return AnimalFactory.Create("Player", selectedAnimal, spawnPosition, true, cameraTransform);
        }

        private void CreateCharacterMenu()
        {
            GameObject menuObject = new GameObject("CharacterSelectionMenu");
            CharacterSelectionMenu menu = menuObject.AddComponent<CharacterSelectionMenu>();
            menu.Initialize(this, initialAnimal);
        }

        private void ConfigureMenuCamera(Camera camera)
        {
            Vector3 cameraGround = jungle.GetGroundPosition(new Vector3(0f, 0f, -5.5f));
            Vector3 targetGround = jungle.GetGroundPosition(new Vector3(0f, 0f, 38f));
            camera.transform.position = cameraGround + Vector3.up * 16.5f;
            camera.transform.rotation = Quaternion.LookRotation(targetGround + Vector3.up * 4.5f - camera.transform.position, Vector3.up);
            camera.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.52f, 0.9f);
            camera.nearClipPlane = 0.12f;
            camera.farClipPlane = 680f;
            camera.fieldOfView = 58f;
        }

        private static void ConfigureCamera(Camera camera, Transform target)
        {
            camera.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.52f, 0.9f);
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 680f);
            camera.allowHDR = true;
            ThirdPersonCamera thirdPersonCamera = camera.GetComponent<ThirdPersonCamera>();
            if (thirdPersonCamera == null) thirdPersonCamera = camera.gameObject.AddComponent<ThirdPersonCamera>();
            thirdPersonCamera.SetTarget(target);

            ThirdPersonAnimalController controller = target.GetComponent<ThirdPersonAnimalController>();
            controller.SetCamera(camera.transform);
        }

        private void SpawnBots(float mapSize)
        {
            float spawnRadius = Mathf.Min(125f, mapSize * 0.36f);
            for (int i = 0; i < botCount; i++)
            {
                float angle = i * Mathf.PI * 2f / botCount + Random.Range(-0.18f, 0.18f);
                float radius = Random.Range(spawnRadius * 0.45f, spawnRadius);
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                position = jungle.GetGroundPosition(position);
                AnimalType type = (AnimalType)Random.Range(0, 4);
                AnimalFactory.Create($"Bot_{i + 1}_{type}", type, position, false);
            }
        }
    }
}
