using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    [DefaultExecutionOrder(-200)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Prototype")]
        [SerializeField, Range(0, DiamondObjectiveManager.MaxPlayers - 1)] private int botCount = 15;
        [SerializeField] private AnimalType initialAnimal = AnimalType.Tiger;
        [SerializeField] private bool generateJungleOnStart = true;
        [SerializeField, Range(3f, 12f)] private float openingProtectionDuration = 6f;

        private JungleGenerator jungle;
        private Camera gameCamera;
        private bool matchStarted;
        private static Material cartoonSkyMaterial;

        public bool MatchStarted => matchStarted;

        private void Awake()
        {
            ConfigureRendering();
            EnsureManager();
            EnsureGameMenu();
            EnsureOnlineMultiplayer();
            jungle = EnsureJungle();
            if (generateJungleOnStart) jungle.Generate();
            EnsureSafeZone();
            // Crystals (DiamondObjective) and Missions (ForestMissionDirector) were removed:
            // the game is a pure 3-lives last-man-standing battle royale. Their managers are no
            // longer created, so their Instance stays null and all null-conditional calls no-op.
            EnsureAmbientAudio();
            gameCamera = EnsureCamera();
            ConfigureMenuCamera(gameCamera);
            CreateCharacterMenu();
        }

        public void StartMatch(AnimalType selectedAnimal)
        {
            if (matchStarted) return;
            matchStarted = true;
            GameMenuController.Instance?.SetInGame(true);
            BattleRoyaleManager.Instance?.BeginOpeningPhase(openingProtectionDuration);
            ThirdPersonAnimalController player = SpawnPlayer(selectedAnimal, gameCamera.transform);
            ConfigureCamera(gameCamera, player.transform);
            ThirdPersonCamera.SetCursorLocked(true);
            SpawnBots(jungle.MapSize, player.transform.position);
            AmbientAudioController.Instance?.BeginMatch();
            SafeZoneController.Instance?.BeginMatch();
            ForestMissionDirector.Instance?.BeginMatch(player);
        }

        private static void ConfigureRendering()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            Time.maximumDeltaTime = 0.05f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.28f, 0.59f, 0.76f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0028f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.38f, 0.62f, 0.84f);
            RenderSettings.ambientEquatorColor = new Color(0.25f, 0.48f, 0.3f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.2f, 0.085f);
            RenderSettings.reflectionIntensity = 0.82f;

            Shader skyShader = ShaderLibrary.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                if (cartoonSkyMaterial == null) cartoonSkyMaterial = new Material(skyShader) { name = "CartoonJungleSky" };
                cartoonSkyMaterial.SetColor("_SkyTint", new Color(0.055f, 0.43f, 1f));
                cartoonSkyMaterial.SetColor("_GroundColor", new Color(0.19f, 0.39f, 0.25f));
                cartoonSkyMaterial.SetFloat("_AtmosphereThickness", 0.72f);
                cartoonSkyMaterial.SetFloat("_Exposure", 1.42f);
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
            light.intensity = 1.78f;
            light.color = new Color(1f, 0.9f, 0.72f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.72f;
            light.shadowBias = 0.055f;
            light.shadowNormalBias = 0.32f;
            light.transform.rotation = Quaternion.Euler(46f, -34f, 0f);

            GameObject fillObject = GameObject.Find("JungleSkyFill");
            if (fillObject == null) fillObject = new GameObject("JungleSkyFill");
            Light fillLight = fillObject.GetComponent<Light>();
            if (fillLight == null) fillLight = fillObject.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.28f;
            fillLight.color = new Color(0.35f, 0.58f, 1f);
            fillLight.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(32f, 142f, 0f);

            QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, 110f);
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.antiAliasing = Mathf.Max(QualitySettings.antiAliasing, 4);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        }

        private static void EnsureManager()
        {
            if (BattleRoyaleManager.Instance != null) return;
            GameObject managerObject = new GameObject("BattleRoyaleManager");
            managerObject.AddComponent<BattleRoyaleManager>();
        }

        private static void EnsureGameMenu()
        {
            if (GameMenuController.Instance != null) return;
            GameObject menuObject = new GameObject("GameMenuController");
            menuObject.AddComponent<GameMenuController>();
        }

        private void EnsureOnlineMultiplayer()
        {
            OnlineMultiplayerManager online = OnlineMultiplayerManager.Instance;
            if (online == null)
            {
                GameObject onlineObject = new GameObject("OnlineMultiplayerManager");
                online = onlineObject.AddComponent<OnlineMultiplayerManager>();
            }
            online.Initialize(this);
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
            ThirdPersonAnimalController player = AnimalFactory.Create("Player", selectedAnimal, spawnPosition, true, cameraTransform);
            FaceMapCenter(player.transform);
            AttackVfx.CreateBurst(spawnPosition + Vector3.up * 0.8f,
                Color.Lerp(AnimalDefinition.Get(selectedAnimal).MainColor, Color.white, 0.22f), 2.1f);
            return player;
        }

        public void PrepareNetworkWorld(int synchronizedSeed)
        {
            jungle.Generate(synchronizedSeed);
            ConfigureMenuCamera(gameCamera);
        }

        public Vector3[] CreateNetworkSpawnPositions(int participantCount)
        {
            int count = Mathf.Max(1, participantCount);
            Vector3[] positions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float angle = 220f + i * (360f / count);
                positions[i] = jungle.GetShoreSpawnPosition(angle);
            }
            return positions;
        }

        public void StartNetworkMatch(int synchronizedSeed, IReadOnlyList<NetworkSpawnDefinition> roster,
            ulong localClientId, bool isHost, bool worldAlreadyPrepared, OnlineMultiplayerManager online)
        {
            if (matchStarted || roster == null || roster.Count == 0) return;
            matchStarted = true;
            if (!worldAlreadyPrepared) jungle.Generate(synchronizedSeed);

            GameMenuController.Instance?.SetInGame(true);
            BattleRoyaleManager.Instance?.BeginOpeningPhase(openingProtectionDuration);
            ThirdPersonAnimalController localPlayer = null;

            foreach (NetworkSpawnDefinition definition in roster)
            {
                bool isLocalPlayer = !definition.IsBot && definition.OwnerClientId == localClientId;
                bool runBotAI = isHost && definition.IsBot;
                string fighterName = definition.IsBot
                    ? $"Bot_{definition.EntityId}_{definition.AnimalType}"
                    : isLocalPlayer ? "Player" : $"OnlinePlayer_{definition.OwnerClientId}_{definition.AnimalType}";
                ThirdPersonAnimalController fighter = AnimalFactory.Create(fighterName, definition.AnimalType,
                    definition.Position, isLocalPlayer, isLocalPlayer ? gameCamera.transform : null, runBotAI);
                if (!isLocalPlayer && !runBotAI) fighter.SetNetworkProxy(true);
                online?.RegisterSpawnedFighter(definition, fighter);
                if (isLocalPlayer) localPlayer = fighter;
            }

            if (localPlayer != null)
            {
                ConfigureCamera(gameCamera, localPlayer.transform);
                ThirdPersonCamera.SetCursorLocked(true);
            }

            CharacterSelectionMenu selectionMenu = FindAnyObjectByType<CharacterSelectionMenu>();
            if (selectionMenu != null) Destroy(selectionMenu.gameObject);
            AmbientAudioController.Instance?.BeginMatch();
            SafeZoneController.Instance?.BeginMatch();
            ForestMissionDirector.Instance?.BeginMatch(localPlayer);
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

        private void SpawnBots(float mapSize, Vector3 playerSpawn)
        {
            int registeredFighters = BattleRoyaleManager.Instance != null
                ? BattleRoyaleManager.Instance.Fighters.Count
                : 1;
            int availableSlots = Mathf.Max(0, DiamondObjectiveManager.MaxPlayers - registeredFighters);
            int botsToSpawn = Mathf.Clamp(botCount, 0, availableSlots);
            if (botsToSpawn <= 0) return;

            const float minimumPlayerDistance = 72f;
            const float minimumBotDistance = 32f;
            float innerRadius = Mathf.Min(86f, mapSize * 0.25f);
            float outerRadius = Mathf.Min(148f, mapSize * 0.41f);
            int botsPerRing = Mathf.CeilToInt(botsToSpawn / 3f);
            List<Vector3> occupiedSpawns = new List<Vector3>(botsToSpawn + 1) { playerSpawn };

            for (int i = 0; i < botsToSpawn; i++)
            {
                Vector3 position = FindSeparatedBotSpawn(i, botsPerRing, innerRadius, outerRadius,
                    playerSpawn, occupiedSpawns, minimumPlayerDistance, minimumBotDistance);
                position = jungle.GetGroundPosition(position);
                AnimalType type = (AnimalType)Random.Range(0, AnimalRoster.Count);
                ThirdPersonAnimalController bot = AnimalFactory.Create($"Bot_{i + 1}_{type}", type, position, false);
                FaceMapCenter(bot.transform);
                AttackVfx.CreateBurst(position + Vector3.up * 0.65f,
                    Color.Lerp(AnimalDefinition.Get(type).MainColor, Color.white, 0.18f), 1.45f);
                occupiedSpawns.Add(position);
            }
        }

        private static Vector3 FindSeparatedBotSpawn(int botIndex, int botsPerRing, float innerRadius,
            float outerRadius, Vector3 playerSpawn, List<Vector3> occupiedSpawns,
            float minimumPlayerDistance, float minimumBotDistance)
        {
            Vector3 bestCandidate = Vector3.zero;
            float bestClearance = -1f;
            int ring = botIndex % 3;
            int sector = botIndex / 3;

            for (int attempt = 0; attempt < 64; attempt++)
            {
                float ringBlend = ring * 0.5f;
                float radius = Mathf.Lerp(innerRadius, outerRadius, ringBlend) + Random.Range(-6f, 6f);
                float baseAngle = sector * Mathf.PI * 2f / Mathf.Max(1, botsPerRing)
                                  + ring * 0.37f + 0.22f;
                float angle = attempt == 0
                    ? baseAngle + Random.Range(-0.08f, 0.08f)
                    : Random.Range(0f, Mathf.PI * 2f);
                if (attempt > 0) radius = Random.Range(innerRadius, outerRadius);

                Vector3 candidate = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                float playerDistance = HorizontalDistance(candidate, playerSpawn);
                float clearance = float.MaxValue;
                for (int occupiedIndex = 1; occupiedIndex < occupiedSpawns.Count; occupiedIndex++)
                {
                    clearance = Mathf.Min(clearance, HorizontalDistance(candidate, occupiedSpawns[occupiedIndex]));
                }
                if (occupiedSpawns.Count == 1) clearance = outerRadius;

                float score = Mathf.Min(playerDistance, clearance);
                if (score > bestClearance)
                {
                    bestClearance = score;
                    bestCandidate = candidate;
                }

                if (playerDistance >= minimumPlayerDistance && clearance >= minimumBotDistance)
                {
                    return candidate;
                }
            }

            return bestCandidate;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static void FaceMapCenter(Transform animal)
        {
            Vector3 towardCenter = -animal.position;
            towardCenter.y = 0f;
            if (towardCenter.sqrMagnitude > 0.01f)
            {
                animal.rotation = Quaternion.LookRotation(towardCenter.normalized, Vector3.up);
            }
        }
    }
}
