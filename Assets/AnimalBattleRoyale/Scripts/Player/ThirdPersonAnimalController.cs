using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(AnimalOrientationStabilizer))]
    public sealed class ThirdPersonAnimalController : MonoBehaviour
    {
        [SerializeField] private AnimalType animalType = AnimalType.Tiger;
        [SerializeField] private bool isLocalPlayer;
        [SerializeField] private float rotationSpeed = 14f;
        [SerializeField] private float gravity = -24f;

        private const int SeedMaxAmmo = 120;
        private const int SeedMagazineCapacity = 30;
        private const int TomatoMaxAmmo = 60;
        private const int TomatoMagazineCapacity = 10;
        private const int WatermelonMaxAmmo = 30;
        private const int WatermelonMagazineCapacity = 5;
        private const float DefeatedCleanupDelay = 1.5f;
        public const int CrystalsPerWeaponLevel = 5;
        public const int MaxWeaponLevel = 3;

        private readonly Collider[] combatHits = new Collider[64];
        private readonly RaycastHit[] rangedAimHits = new RaycastHit[24];
        private readonly HashSet<Health> abilityHitTargets = new HashSet<Health>();
        private CharacterController characterController;
        private Health health;
        private AnimalStats stats;
        private Transform cameraTransform;
        private AnimalVisualMotion visualMotion;
        private JungleGenerator jungle;
        private AudioSource undergroundWalkSource;
        private float nextGroundCheckTime;
        private bool wasGroundedForLandingSfx = true;
        private Vector3 verticalVelocity;
        private Vector3 extraVelocity;
        private Vector3 aiMoveDirection;
        private Vector3 aiAttackDirection;
        private bool aiSprint;
        private bool aiAttack;
        private bool aiRangedAttack;
        private int aiAbilitySlot = -1;
        private readonly float[] nextPowerTimes = new float[3];
        private float nextBasicAttackTime;
        private float nextFootstepTime;
        private float slowUntil;
        private float slowMultiplier = 1f;
        private float resistanceUntil;
        private float abilityDashUntil;
        private float nextAbilityDamage;
        private float abilityDashSpeed;
        private float abilityDamage;
        private float abilityKnockback;
        private float abilityRadius;
        private float abilitySlow;
        private Vector3 dashDirection;
        private Color abilityColor;
        private readonly int[] weaponReserveAmmo = { SeedMaxAmmo, 0, 0 };
        private readonly int[] weaponMagazineAmmo = { SeedMagazineCapacity, 0, 0 };
        private WeaponAmmoType selectedWeapon = WeaponAmmoType.Seed;
        private bool rangedReloading;
        private float rangedReloadEndsAt;
        private int weaponLevel = 1;
        private int weaponCrystalProgress;
        private int lastPowerSlot = -1;
        private bool defeated;
        private bool eliminated;
        private int livesRemaining = MaxLives;
        private float spawnProtectedUntil;
        private bool tigerPouncing;
        private float tigerPounceStartedAt;
        private float tigerPounceEndsAt;
        private float tigerPounceArcHeight;
        private Vector3 tigerPounceStart;
        private Vector3 tigerPounceTarget;
        private float flyingUntil;
        private Vector3 eagleGlideDirection;
        private Transform heldVine;
        private Transform targetVine;
        private bool hangingVine;
        private bool vineLeaping;
        private bool cowCharging;
        private int vinesVisitedInChain;
        private float vineLeapStartedAt;
        private float vineLeapEndsAt;
        private float vineLeapArcHeight;
        private Vector3 vineLeapStart;
        private Vector3 vineLeapEnd;
        private bool burrowed;
        private bool burrowEntering;
        private float burrowEntryUntil;
        private bool climbingTree;
        private JungleGenerator.ClimbableSpot climbSpot;
        private float climbHeight;
        private const float BurrowJumpDuration = 0.35f;
        private const float AntClimbApproachRange = 1.4f;
        private const float AntClimbSurfaceOffset = 0.45f;
        private const float AntClimbSpeed = 3.4f;
        private const float AntClimbJumpOffSpeed = 5f;
        private const float AntTunnelAimExitRange = 45f;
        private const float TigerPounceMaxRange = 50f;
        private const float TigerPounceMinDuration = 0.22f;
        private const float TigerPounceMaxDuration = 2.1f;
        private bool networkProxy;
        private Vector3 networkTargetPosition;
        private Quaternion networkTargetRotation;
        private Vector3 lastNetworkVisualPosition;

        public const int MaxLives = 3;
        private const float SpawnProtectionSeconds = 2.5f;

        public const int MaxVinesPerChain = 5;
        private const float VineLeapSpeed = 22f;
        private const float VineLaunchForward = 15f;
        private const float VineLaunchUp = 9f;

        private const float CowChargeDistance = 30f;
        // Speed set so the 30m charge takes exactly 3s, matching the CowCharge.wav clip length.
        private const float CowChargeSpeed = CowChargeDistance / 3f;
        private const float CowChargeMaxDuration = CowChargeDistance / CowChargeSpeed;
        private const float CowChargeDamage = 26f;
        private const float CowChargeKnockback = 12f;
        private const float CowChargeRadius = 1.8f;
        private const float CowChargeTurnRateDegreesPerSecond = 220f;
        private static readonly Color CowChargeColor = new Color(0.75f, 0.55f, 0.32f);
        private const float EagleGlideTurnRateDegreesPerSecond = 260f;

        public AnimalType AnimalType => animalType;
        public AnimalStats Stats { get { EnsureRuntimeReferences(); return stats; } }
        public Health Health => health;
        public bool IsLocalPlayer => isLocalPlayer;
        public bool IsDefeated => defeated;
        public bool IsEliminated => eliminated;
        public int LivesRemaining => livesRemaining;
        public bool IsSpawnProtected => Time.time < spawnProtectedUntil;
        public bool IsFlying => Time.time < flyingUntil;
        public float GlideSecondsRemaining => Mathf.Max(0f, flyingUntil - Time.time);
        public bool IsBurrowed => burrowed;
        public bool IsNetworkProxy => networkProxy;
        public bool IsInAntTunnel => burrowed;
        public bool IsHangingVine => hangingVine;
        public bool IsVineLeaping => vineLeaping;
        public bool IsCowCharging => cowCharging;
        public int VinesVisitedInChain => vinesVisitedInChain;
        public bool CanChainToAnotherVine => hangingVine && vinesVisitedInChain < MaxVinesPerChain;
        public bool IsWading => CentralLake.TryGetWaterAt(transform.position, out _, out _);
        public bool IsSwimming => CentralLake.TryGetWaterAt(transform.position, out float surfaceHeight, out _)
                                  && transform.position.y < surfaceHeight - stats.ControllerHeight * 0.35f;
        public float TunnelSecondsRemaining => AntTunnelEntrance.SecondsRemaining(this);
        public bool UsesMobilityEnergy => false;
        public float MobilityEnergy => 0f;
        public float MaxMobilityEnergyValue => 0f;
        public string MobilityEnergyName => string.Empty;
        public bool NeedsMobilityEnergy => false;
        public bool IsMobilityRecharging => false;
        public float MobilityRechargeSecondsRemaining => 0f;
        private int SelectedReserveAmmo
        {
            get => weaponReserveAmmo[(int)selectedWeapon];
            set => weaponReserveAmmo[(int)selectedWeapon] = value;
        }

        private int SelectedMagazineAmmo
        {
            get => weaponMagazineAmmo[(int)selectedWeapon];
            set => weaponMagazineAmmo[(int)selectedWeapon] = value;
        }

        public int RangedAmmo => SelectedReserveAmmo;
        public int RangedMagazineAmmo => SelectedMagazineAmmo;
        public int RangedMagazineCapacityValue => MagazineCapacityFor(CurrentWeaponAmmo);
        public int RangedReserveAmmo => Mathf.Max(0, SelectedReserveAmmo - SelectedMagazineAmmo);
        public bool IsRangedReloading => rangedReloading;
        public float RangedReloadSecondsRemaining => rangedReloading
            ? Mathf.Max(0f, rangedReloadEndsAt - Time.time)
            : 0f;
        public int MaxRangedAmmoValue => MaxAmmoFor(CurrentWeaponAmmo);
        public bool NeedsRangedAmmo => SelectedReserveAmmo < MaxAmmoFor(CurrentWeaponAmmo);
        public RangedSupplyKind CompatibleRangedSupply => RangedSupplyKind.NaturalAmmo;
        public int WeaponLevel => weaponLevel;
        public int WeaponCrystalProgress => weaponCrystalProgress;
        public bool CanUpgradeWeapon => weaponLevel < MaxWeaponLevel;
        public WeaponAmmoType CurrentWeaponAmmo => selectedWeapon;
        public int SelectedWeaponSlot => (int)selectedWeapon;

        /// <summary>A weapon is unlocked once enough weapon crystals raised the level to reach its slot.</summary>
        public bool IsWeaponUnlocked(WeaponAmmoType weapon) => (int)weapon <= weaponLevel - 1;

        /// <summary>Switches the active weapon among the ones already unlocked. Returns false if locked.</summary>
        public bool TrySelectWeapon(WeaponAmmoType weapon)
        {
            if (!IsWeaponUnlocked(weapon) || selectedWeapon == weapon) return false;
            selectedWeapon = weapon;
            rangedReloading = false;
            rangedReloadEndsAt = 0f;
            if (SelectedMagazineAmmo <= 0 && SelectedReserveAmmo > 0) BeginRangedReload();
            return true;
        }

        // Tomato fire rate is half of the seed launcher's; watermelon is a third of the tomato's.
        private static float FireRateMultiplierFor(WeaponAmmoType ammoType) => ammoType switch
        {
            WeaponAmmoType.Tomato => 0.5f,
            WeaponAmmoType.Watermelon => 0.5f / 3f,
            _ => 1f
        };

        private static int MagazineCapacityFor(WeaponAmmoType ammoType) => ammoType switch
        {
            WeaponAmmoType.Tomato => TomatoMagazineCapacity,
            WeaponAmmoType.Watermelon => WatermelonMagazineCapacity,
            _ => SeedMagazineCapacity
        };

        private static int MaxAmmoFor(WeaponAmmoType ammoType) => ammoType switch
        {
            WeaponAmmoType.Tomato => TomatoMaxAmmo,
            WeaponAmmoType.Watermelon => WatermelonMaxAmmo,
            _ => SeedMaxAmmo
        };
        public static string DisplayNameForWeapon(WeaponAmmoType weapon) => weapon switch
        {
            WeaponAmmoType.Tomato => "TOMATE",
            WeaponAmmoType.Watermelon => "MELANCIA",
            _ => "SEMENTE"
        };

        public static Color ColorForWeapon(WeaponAmmoType weapon) => weapon switch
        {
            WeaponAmmoType.Tomato => new Color(0.96f, 0.12f, 0.055f),
            WeaponAmmoType.Watermelon => new Color(0.18f, 0.82f, 0.2f),
            _ => new Color(0.82f, 0.7f, 0.42f)
        };

        public string WeaponAmmoDisplayName => animalType == AnimalType.Cow ? "LEITE" : DisplayNameForWeapon(CurrentWeaponAmmo);
        public Color WeaponAmmoColor => animalType == AnimalType.Cow ? RangedProjectile.MilkColor : ColorForWeapon(CurrentWeaponAmmo);
        public int LastPowerSlot => lastPowerSlot;

        public Vector3 ViewAimDirection
        {
            get
            {
                if (cameraTransform == null) return transform.forward;
                ThirdPersonCamera thirdPersonCamera = cameraTransform.GetComponent<ThirdPersonCamera>();
                Vector3 direction = thirdPersonCamera != null ? thirdPersonCamera.AimDirection : cameraTransform.forward;
                return direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
            }
        }

        public string RangedAmmoName => WeaponAmmoDisplayName;

        public string RangedAttackName => animalType == AnimalType.Cow ? "JATO DE LEITE" : CurrentWeaponAmmo switch
        {
            WeaponAmmoType.Tomato => "RAJADA DE TOMATES",
            WeaponAmmoType.Watermelon => "MELANCIA EXPLOSIVA",
            _ => "RAJADA DE SEMENTES"
        };

        public string LastPowerName => Stats.AbilityNames != null && lastPowerSlot >= 0 && lastPowerSlot < Stats.AbilityNames.Length
            ? Stats.AbilityNames[lastPowerSlot] : "Ataque base";

        public string BasicActionName => animalType switch
        {
            AnimalType.Tiger => "Patada de Garras",
            AnimalType.Ant => "Mordida de Mandíbula",
            AnimalType.Eagle => "Bicada",
            AnimalType.Monkey => "Tapa Selvagem",
            AnimalType.Cow => "Chifrada",
            _ => "Ataque"
        };

        public float AbilityCooldownRemainingFor(int slot)
        {
            if (slot < 0 || slot >= nextPowerTimes.Length) return 0f;
            if (slot == 0 && animalType == AnimalType.Monkey && CanChainToAnotherVine) return 0f;
            return Mathf.Max(0f, nextPowerTimes[slot] - Time.time);
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            stats = AnimalDefinition.Get(animalType);
        }

        private void OnEnable() => EnsureRuntimeReferences();

        private void EnsureRuntimeReferences()
        {
            if (stats.AbilityNames == null) stats = AnimalDefinition.Get(animalType);
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (health == null) health = GetComponent<Health>();
            if (visualMotion == null) visualMotion = GetComponentInChildren<AnimalVisualMotion>();
        }

        private void Start()
        {
            if (transform.Find("VisualRoot") == null) ApplyAnimal(animalType, true);
            if (isLocalPlayer && Camera.main != null) cameraTransform = Camera.main.transform;
            SnapToTerrain(jungle != null ? jungle : FindAnyObjectByType<JungleGenerator>());
        }

        private void Update()
        {
            if (networkProxy)
            {
                UpdateNetworkProxyMotion();
                return;
            }
            if (defeated || health.IsDead) return;
            UpdateRangedReload();
            if (vineLeaping)
            {
                HandleVineLeap();
                return;
            }
            if (tigerPouncing)
            {
                HandleTigerPounce();
                return;
            }
            if (BattleRoyaleManager.Instance != null && BattleRoyaleManager.Instance.MatchFinished)
            {
                visualMotion?.SetLocomotion(false, false, false);
                return;
            }
            if (burrowEntering)
            {
                if (Time.time >= burrowEntryUntil) FinishBurrowEntry();
                return;
            }
            if (burrowed) { HandleBurrowed(); return; }
            if (climbingTree) { HandleClimbingTree(); return; }
            RecoverIfBelowTerrain();
            if (isLocalPlayer) HandleLocalInput();
            else HandleAIInput();
        }

        // Burrowed: the Ant is hidden/untargetable but otherwise moves and is viewed exactly
        // like normal (same camera, same physics) so the glowing tunnel-exit beacons stay
        // visible and usable while walking to one, rather than needing a separate underground
        // view that risks showing the player what's below the terrain.
        private void HandleBurrowed()
        {
            if (isLocalPlayer)
            {
                Vector2 rawInput = GameInput.ReadMovement();
                Vector3 movement = GetCameraRelativeDirection(rawInput);
                bool sprint = GameSettings.AutomaticSprint || GameInput.SprintHeld();
                SimulateMovement(movement, sprint, GameInput.JumpPressed(), rawInput, useStrafing: true);
                if (GameInput.AbilityOnePressed())
                {
                    Vector3 aimOrigin = cameraTransform != null ? cameraTransform.position : transform.position;
                    if (!AntTunnelEntrance.TryExitAimed(this, aimOrigin, ViewAimDirection, AntTunnelAimExitRange))
                        AntTunnelEntrance.TryExitNearest(this);
                }
            }
            else
            {
                SimulateMovement(aiMoveDirection, aiSprint, false);
            }

            AntTunnelEntrance.Tick(this);
            if (!AntTunnelEntrance.IsTraveling(this)) SurfaceFromBurrow();
        }

        private void EnterBurrow()
        {
            // Plays the jump-into-hole pose first; the character actually vanishes only
            // once that hop has had time to play (see FinishBurrowEntry).
            burrowEntering = true;
            burrowEntryUntil = Time.time + BurrowJumpDuration;
            verticalVelocity = Vector3.zero;
            extraVelocity = Vector3.zero;
            if (characterController != null) characterController.enabled = false;
            visualMotion?.SetLocomotion(false, false, true);
        }

        private void FinishBurrowEntry()
        {
            burrowEntering = false;
            burrowed = true;
            if (characterController != null) characterController.enabled = true;
            Transform visual = transform.Find("VisualRoot");
            if (visual != null) visual.gameObject.SetActive(false);
            AttackVfx.CreateBurst(transform.position, new Color(0.65f, 0.28f, 0.06f), 1.8f);
        }

        private void SurfaceFromBurrow()
        {
            burrowed = false;
            if (characterController != null) characterController.enabled = true;
            Transform visual = transform.Find("VisualRoot");
            if (visual != null) visual.gameObject.SetActive(true);
            if (jungle != null) SnapToTerrain(jungle);
            SetUndergroundWalkLoop(false);
        }

        // Loops while the Ant is actually moving underground and stops the instant it
        // stops (or surfaces), at which point normal footsteps take back over.
        private void SetUndergroundWalkLoop(bool shouldPlay)
        {
            if (!shouldPlay)
            {
                if (undergroundWalkSource != null && undergroundWalkSource.isPlaying) undergroundWalkSource.Stop();
                return;
            }
            if (undergroundWalkSource == null)
            {
                AudioClip clip = Resources.Load<AudioClip>("Audio/SFX/AntUnderground");
                if (clip == null) return;
                undergroundWalkSource = gameObject.AddComponent<AudioSource>();
                undergroundWalkSource.clip = clip;
                undergroundWalkSource.loop = true;
                undergroundWalkSource.playOnAwake = false;
                undergroundWalkSource.volume = 0.55f;
                undergroundWalkSource.spatialBlend = 0.85f;
                undergroundWalkSource.dopplerLevel = 0f;
                undergroundWalkSource.rolloffMode = AudioRolloffMode.Linear;
                undergroundWalkSource.minDistance = 2f;
                undergroundWalkSource.maxDistance = 24f;
            }
            if (!undergroundWalkSource.isPlaying) undergroundWalkSource.Play();
        }

        public void Initialize(AnimalType type, bool localPlayer, Transform cameraReference = null)
        {
            isLocalPlayer = localPlayer;
            cameraTransform = cameraReference;
            ApplyAnimal(type, true);
        }

        public void SetCamera(Transform cameraReference) => cameraTransform = cameraReference;

        public void SetNetworkProxy(bool value)
        {
            networkProxy = value;
            networkTargetPosition = transform.position;
            networkTargetRotation = transform.rotation;
            lastNetworkVisualPosition = transform.position;
            SimpleBotAI bot = GetComponent<SimpleBotAI>();
            if (bot != null) bot.enabled = !value;
        }

        public void ApplyNetworkTransform(Vector3 position, Quaternion rotation, bool immediate)
        {
            networkTargetPosition = position;
            networkTargetRotation = rotation;
            if (!immediate && Vector3.SqrMagnitude(transform.position - position) < 64f) return;

            bool wasEnabled = characterController != null && characterController.enabled;
            if (wasEnabled) characterController.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            if (wasEnabled) characterController.enabled = true;
            lastNetworkVisualPosition = position;
        }

        public void ApplyNetworkSnapshot(Vector3 position, Quaternion rotation, float replicatedHealth, int replicatedLives,
            int replicatedAmmo, int replicatedMagazineAmmo, int replicatedWeaponLevel,
            int replicatedCrystalProgress, int replicatedSelectedWeapon, bool replicatedEliminated, bool applyTransform)
        {
            livesRemaining = Mathf.Clamp(replicatedLives, 0, MaxLives);
            weaponLevel = Mathf.Clamp(replicatedWeaponLevel, 1, MaxWeaponLevel);
            selectedWeapon = (WeaponAmmoType)Mathf.Clamp(replicatedSelectedWeapon, 0, weaponLevel - 1);
            SelectedReserveAmmo = Mathf.Clamp(replicatedAmmo, 0, MaxAmmoFor(CurrentWeaponAmmo));
            SelectedMagazineAmmo = Mathf.Clamp(replicatedMagazineAmmo, 0, Mathf.Min(MagazineCapacityFor(CurrentWeaponAmmo), SelectedReserveAmmo));
            weaponCrystalProgress = weaponLevel >= MaxWeaponLevel
                ? 0
                : Mathf.Clamp(replicatedCrystalProgress, 0, CrystalsPerWeaponLevel - 1);
            health?.ApplyReplicatedState(replicatedHealth);
            eliminated = replicatedEliminated;
            defeated = replicatedEliminated;
            Transform visual = transform.Find("VisualRoot");
            if (visual != null) visual.gameObject.SetActive(!replicatedEliminated);
            if (characterController != null) characterController.enabled = !replicatedEliminated;
            if (applyTransform) ApplyNetworkTransform(position, rotation, false);
        }

        public void ExecuteNetworkAction(OnlineActionType action, Vector3 direction)
        {
            if (eliminated || health == null || health.IsDead) return;
            Vector3 actionDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            switch (action)
            {
                case OnlineActionType.RangedAttack:
                    TryRangedAttack(actionDirection);
                    break;
                case OnlineActionType.MeleeAttack:
                    TryBasicAttack(actionDirection);
                    break;
                case OnlineActionType.Ability:
                    TryUsePower(0, actionDirection);
                    break;
                case OnlineActionType.Consume:
                    if (!WeaponUpgradeCrystal.TryCollectNearest(this) && !RangedAmmoPickup.TryCollectNearest(this))
                        FoodPickup.TryConsumeNearest(this);
                    break;
                case OnlineActionType.SelectWeapon:
                    TrySelectWeapon((WeaponAmmoType)Mathf.RoundToInt(direction.x));
                    break;
            }
        }

        private void UpdateNetworkProxyMotion()
        {
            if (eliminated) return;
            float distance = Vector3.Distance(transform.position, networkTargetPosition);
            if (distance > 8f)
            {
                bool wasEnabled = characterController != null && characterController.enabled;
                if (wasEnabled) characterController.enabled = false;
                transform.position = networkTargetPosition;
                if (wasEnabled) characterController.enabled = true;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, networkTargetPosition, 1f - Mathf.Exp(-14f * Time.deltaTime));
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, networkTargetRotation, 1f - Mathf.Exp(-16f * Time.deltaTime));
            float speed = Vector3.Distance(lastNetworkVisualPosition, transform.position) / Mathf.Max(0.001f, Time.deltaTime);
            bool moving = speed > 0.12f;
            visualMotion?.SetLocomotion(moving, speed > stats.MoveSpeed * 1.15f, false);
            lastNetworkVisualPosition = transform.position;
        }

        public void SetAIInput(Vector3 moveDirection, Vector3 attackDirection, bool sprint, bool attack, bool rangedAttack, int abilitySlot)
        {
            aiMoveDirection = Vector3.ClampMagnitude(moveDirection, 1f);
            aiAttackDirection = Vector3.ClampMagnitude(attackDirection, 1f);
            aiSprint = sprint;
            aiAttack = attack;
            aiRangedAttack = rangedAttack;
            aiAbilitySlot = abilitySlot;
        }

        public void ApplyAnimal(AnimalType type, bool refillHealth)
        {
            animalType = type;
            stats = AnimalDefinition.Get(type);
            EnsureRuntimeReferences();
            vineLeaping = false;
            hangingVine = false;
            heldVine = null;
            targetVine = null;
            vinesVisitedInChain = 0;
            characterController.enabled = true;
            characterController.radius = stats.ControllerRadius;
            characterController.height = stats.ControllerHeight;
            characterController.center = new Vector3(0f, stats.ControllerHeight * 0.5f, 0f);
            characterController.stepOffset = Mathf.Min(0.35f, stats.ControllerHeight * 0.25f);
            characterController.skinWidth = 0.04f;
            Transform visualRoot = AnimalVisualFactory.Build(transform, type, stats.MainColor, stats.VisualScale);
            visualMotion = visualRoot != null ? visualRoot.GetComponent<AnimalVisualMotion>() : null;
            weaponLevel = 1;
            selectedWeapon = WeaponAmmoType.Seed;
            for (int slot = 0; slot < weaponReserveAmmo.Length; slot++)
            {
                weaponReserveAmmo[slot] = 0;
                weaponMagazineAmmo[slot] = 0;
            }
            weaponReserveAmmo[(int)WeaponAmmoType.Seed] = SeedMaxAmmo;
            weaponMagazineAmmo[(int)WeaponAmmoType.Seed] = SeedMagazineCapacity;
            rangedReloading = false;
            rangedReloadEndsAt = 0f;
            weaponCrystalProgress = 0;
            health.Initialize(stats.MaxHealth, this);
            if (!refillHealth) health.ReconfigureMaxHealth(stats.MaxHealth, false);
        }

        public void SnapToTerrain(JungleGenerator terrain, float clearance = 0.12f)
        {
            if (terrain == null) return;
            jungle = terrain;
            Vector3 safePosition = terrain.GetGroundPosition(transform.position, clearance);
            bool wasEnabled = characterController != null && characterController.enabled;
            if (wasEnabled) characterController.enabled = false;
            transform.position = safePosition;
            verticalVelocity = Vector3.zero;
            if (wasEnabled) characterController.enabled = true;
        }

        private void RecoverIfBelowTerrain()
        {
            if (jungle == null) return;
            if (Time.time < nextGroundCheckTime) return;
            nextGroundCheckTime = Time.time + 0.1f;

            // Cheap procedural height check first; only pay for the raycast-accurate
            // GetGroundPosition when the character is actually near/under the terrain.
            float approxGroundHeight = jungle.GroundHeightAt(transform.position);
            if (transform.position.y >= approxGroundHeight - 1f) return;

            Vector3 safePosition = jungle.GetGroundPosition(transform.position);
            if (transform.position.y < safePosition.y - 0.5f) SnapToTerrain(jungle);
        }

        private void HandleLocalInput()
        {
            Vector2 rawInput = GameInput.ReadMovement();
            Vector3 movement = GetCameraRelativeDirection(rawInput);
            bool sprint = GameSettings.AutomaticSprint || GameInput.SprintHeld();
            SimulateMovement(movement, sprint, GameInput.JumpPressed(), rawInput, useStrafing: true);
            bool rangedAttackRequested = GameSettings.RangedFireMode == RangedFireMode.Automatic
                ? GameInput.RangedAttackHeld()
                : GameInput.RangedAttackPressed();
            if (rangedAttackRequested)
            {
                Vector3 direction = GetRangedAttackDirection(movement);
                if (TryRangedAttack(direction))
                    OnlineMultiplayerManager.Instance?.ReportAction(OnlineActionType.RangedAttack, direction);
            }
            if (GameInput.MeleeAttackPressed())
            {
                Vector3 direction = GetAttackDirection(movement);
                OnlineMultiplayerManager.Instance?.ReportAction(OnlineActionType.MeleeAttack, direction);
                TryBasicAttack(direction);
            }
            if (GameInput.AbilityOnePressed())
            {
                Vector3 direction = GetAttackDirection(movement);
                OnlineMultiplayerManager.Instance?.ReportAction(OnlineActionType.Ability, direction);
                TryUsePower(0, direction);
            }
            if (GameInput.ConsumePressed())
            {
                OnlineMultiplayerManager online = OnlineMultiplayerManager.Instance;
                online?.ReportAction(OnlineActionType.Consume, transform.forward);
                if (online == null || !online.UsesRemoteAuthority)
                {
                    if (!WeaponUpgradeCrystal.TryCollectNearest(this) && !RangedAmmoPickup.TryCollectNearest(this))
                        FoodPickup.TryConsumeNearest(this);
                }
            }
            int weaponSelection = GameInput.ReadWeaponSelection();
            if (weaponSelection >= 0 && TrySelectWeapon((WeaponAmmoType)weaponSelection))
            {
                OnlineMultiplayerManager.Instance?.ReportAction(OnlineActionType.SelectWeapon,
                    new Vector3(weaponSelection, 0f, 0f));
            }
            int weaponScroll = GameInput.ReadWeaponScroll();
            if (weaponScroll != 0 && TryCycleWeapon(weaponScroll))
            {
                OnlineMultiplayerManager.Instance?.ReportAction(OnlineActionType.SelectWeapon,
                    new Vector3((int)selectedWeapon, 0f, 0f));
            }
        }

        /// <summary>Cycles among the weapons unlocked so far; +1 = next, -1 = previous, wrapping around.</summary>
        private bool TryCycleWeapon(int direction)
        {
            if (weaponLevel <= 1) return false;
            int current = (int)selectedWeapon;
            int next = ((current + direction) % weaponLevel + weaponLevel) % weaponLevel;
            return TrySelectWeapon((WeaponAmmoType)next);
        }

        private void HandleAIInput()
        {
            SimulateMovement(aiMoveDirection, aiSprint, false);
            if (aiAttack)
            {
                if (aiRangedAttack) TryRangedAttack(aiAttackDirection);
                else TryBasicAttack(aiAttackDirection);
            }
            if (aiAbilitySlot >= 0) TryUsePower(aiAbilitySlot, aiAttackDirection.sqrMagnitude > 0.01f ? aiAttackDirection : aiMoveDirection);
            aiAttack = false;
            aiRangedAttack = false;
            aiAbilitySlot = -1;
        }

        private void SimulateMovement(Vector3 direction, bool sprint, bool jumpPressed,
            Vector2 rawInput = default, bool useStrafing = false)
        {
            if (hangingVine)
            {
                HandleHangingMovement(direction, jumpPressed);
                return;
            }
            // Strafing/backpedaling keeps the current facing and never benefits from sprint —
            // only genuine forward input (W) turns the body and can run. Flight steers freely.
            bool hasForwardInput = !useStrafing || IsFlying || rawInput.y > 0.05f;
            sprint = sprint && hasForwardInput;

            bool grounded = characterController.isGrounded;
            if (grounded && !wasGroundedForLandingSfx) CombatFeedback.PlayJump(transform.position);
            wasGroundedForLandingSfx = grounded;
            if (grounded && IsFlying && verticalVelocity.y < 0f) flyingUntil = 0f;
            bool gliding = IsFlying;
            bool abilityDashing = Time.time < abilityDashUntil;
            if (cowCharging && !abilityDashing) cowCharging = false;
            visualMotion?.SetLocomotion(direction.sqrMagnitude > 0.01f || abilityDashing,
                sprint || abilityDashing, !grounded, verticalVelocity.y, gliding);
            if (grounded && verticalVelocity.y < 0f && !gliding) verticalVelocity.y = -2f;
            bool jumped = jumpPressed && grounded && !gliding;
            if (jumped)
            {
                verticalVelocity.y = stats.JumpForce;
                wasGroundedForLandingSfx = false;
            }

            bool wading = CentralLake.TryGetWaterAt(transform.position, out float waterSurfaceHeight,
                out float waterMovementMultiplier);
            bool swimming = wading
                            && transform.position.y < waterSurfaceHeight - stats.ControllerHeight * 0.35f;

            bool movingOnGround = grounded && !jumped && !gliding
                && (direction.sqrMagnitude > 0.01f || abilityDashing);
            if (burrowed)
            {
                SetUndergroundWalkLoop(movingOnGround);
            }
            else
            {
                if (movingOnGround && Time.time >= nextFootstepTime)
                {
                    nextFootstepTime = Time.time + (sprint || abilityDashing ? 0.3f : 0.43f);
                    if (wading) CombatFeedback.PlayLakeFootstep(transform.position);
                    else CombatFeedback.PlayFootstep(transform.position);
                }
            }

            Vector3 movementDirection = direction;
            if (gliding)
            {
                // Glide direction follows the camera aim continuously (like the cow's
                // charge) instead of only turning while a WASD key is held, so looking
                // around alone is enough to steer instead of flying dead straight.
                Vector3 aimFlat = new Vector3(ViewAimDirection.x, 0f, ViewAimDirection.z);
                if (aimFlat.sqrMagnitude > 0.01f)
                {
                    float maxRadians = EagleGlideTurnRateDegreesPerSecond * Mathf.Deg2Rad * Time.deltaTime;
                    eagleGlideDirection = Vector3.RotateTowards(eagleGlideDirection, aimFlat.normalized, maxRadians, 0f).normalized;
                }
                movementDirection = eagleGlideDirection;
            }

            if (movementDirection.sqrMagnitude > 0.01f && !abilityDashing && hasForwardInput)
            {
                Quaternion targetRotation = Quaternion.LookRotation(movementDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // Cow's charge steers toward wherever the camera is aiming instead of holding
            // the direction it was started in, so it isn't a rigid straight line.
            if (abilityDashing && cowCharging)
            {
                Vector3 aimFlat = new Vector3(ViewAimDirection.x, 0f, ViewAimDirection.z);
                if (aimFlat.sqrMagnitude > 0.01f)
                {
                    float maxRadians = CowChargeTurnRateDegreesPerSecond * Mathf.Deg2Rad * Time.deltaTime;
                    dashDirection = Vector3.RotateTowards(dashDirection, aimFlat.normalized, maxRadians, 0f).normalized;
                    transform.rotation = Quaternion.LookRotation(dashDirection, Vector3.up);
                }
            }

            float speed = sprint ? stats.SprintSpeed : stats.MoveSpeed;
            if (Time.time < slowUntil) speed *= slowMultiplier;
            if (gliding) speed *= ServerGameTuning.EagleFlySpeedBonus;
            if (swimming) speed *= 0.72f;
            else if (wading && !(ForestMissionDirector.Instance != null && ForestMissionDirector.Instance.LakePassageOpen))
                speed *= waterMovementMultiplier;

            Vector3 horizontal = abilityDashing ? dashDirection * abilityDashSpeed
                : movementDirection * speed;
            if (abilityDashing && Time.time >= nextAbilityDamage)
            {
                nextAbilityDamage = Time.time + 0.24f;
                DamageArea(abilityRadius, abilityDamage, abilityKnockback, dashDirection, abilityColor, abilitySlow);
            }

            if (gliding)
            {
                // Salto longo: sobe uma vez e plana suavemente, sem subida manual.
                verticalVelocity.y = Mathf.Max(ServerGameTuning.EagleMaximumFallSpeed,
                    verticalVelocity.y + gravity * ServerGameTuning.EagleGlideGravityMultiplier * Time.deltaTime);
            }
            else if (swimming)
            {
                float surfaceTarget = waterSurfaceHeight - stats.ControllerHeight * 0.55f;
                float lift = Mathf.Clamp((surfaceTarget - transform.position.y) * 4.5f, -3f, 7f);
                verticalVelocity.y = Mathf.MoveTowards(verticalVelocity.y, lift, 30f * Time.deltaTime);
            }
            else verticalVelocity.y += gravity * ServerGameTuning.JumpGravityMultiplier * Time.deltaTime;

            extraVelocity = Vector3.Lerp(extraVelocity, Vector3.zero, 4.5f * Time.deltaTime);
            characterController.Move((horizontal + verticalVelocity + extraVelocity) * Time.deltaTime);
        }

        private void HandleHangingMovement(Vector3 swingDirection, bool releasePressed)
        {
            if (heldVine == null) { hangingVine = false; return; }
            visualMotion?.SetLocomotion(false, false, true);
            visualMotion?.SetHandAimTarget(heldVine.position);
            verticalVelocity = Vector3.zero;
            extraVelocity = Vector3.zero;

            VineAnchor vineAnchor = heldVine.GetComponent<VineAnchor>();
            vineAnchor?.DriveSwing(swingDirection, Time.deltaTime);

            if (releasePressed)
            {
                TryLaunchFromVine(swingDirection.sqrMagnitude > 0.01f ? swingDirection : ViewAimDirection);
                return;
            }

            // Follow the moving grip point while WASD pumps the vine's pendulum.
            Vector3 target = heldVine.position - Vector3.up * (stats.ControllerHeight * 0.82f);
            Vector3 delta = target - transform.position;
            if (delta.sqrMagnitude > 0.0001f) characterController.Move(delta * Mathf.Clamp01(12f * Time.deltaTime));
            Vector3 facing = swingDirection.sqrMagnitude > 0.01f ? swingDirection : ViewAimDirection;
            Vector3 facingFlat = new Vector3(facing.x, 0f, facing.z);
            if (facingFlat.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facingFlat.normalized, Vector3.up), rotationSpeed * Time.deltaTime);
        }

        private Vector3 GetCameraRelativeDirection(Vector2 input)
        {
            if (cameraTransform == null) return new Vector3(input.x, 0f, input.y).normalized;
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            return Vector3.ClampMagnitude(forward.normalized * input.y + right.normalized * input.x, 1f);
        }

        private Vector3 GetAttackDirection(Vector3 movementDirection) => movementDirection.sqrMagnitude > 0.01f
            ? movementDirection.normalized : transform.forward;

        private Vector3 GetRangedAttackDirection(Vector3 movementDirection)
        {
            if (cameraTransform == null || cameraTransform.GetComponent<Camera>() is not Camera aimCamera)
                return GetAttackDirection(movementDirection);

            const float maximumAimDistance = 120f;
            Ray centerRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float targetDistance = maximumAimDistance;
            int hitCount = Physics.RaycastNonAlloc(centerRay, rangedAimHits, maximumAimDistance, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = rangedAimHits[i];
                if (hit.collider == null) continue;
                Transform hitTransform = hit.collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
                if (hit.distance < targetDistance) targetDistance = hit.distance;
            }

            Vector3 targetPoint = centerRay.GetPoint(targetDistance);
            Vector3 launchPoint = RangedProjectile.GetLaunchPosition(this, centerRay.direction);
            Vector3 direction = targetPoint - launchPoint;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : centerRay.direction;
        }

        public bool TryGetRangedAimViewportPosition(out Vector2 viewportPosition)
        {
            viewportPosition = new Vector2(0.5f, 0.5f);
            return cameraTransform != null && cameraTransform.GetComponent<Camera>() != null;
        }

        private bool TryRangedAttack(Vector3 direction)
        {
            if (BattleRoyaleManager.Instance != null && !BattleRoyaleManager.Instance.CombatEnabled) return false;
            UpdateRangedReload();
            if (rangedReloading || SelectedReserveAmmo <= 0) return false;
            if (SelectedMagazineAmmo <= 0)
            {
                BeginRangedReload();
                return false;
            }
            if (Time.time < nextBasicAttackTime) return false;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            SelectedReserveAmmo--;
            SelectedMagazineAmmo--;
            nextBasicAttackTime = Time.time + RangedAttackCooldown();
            Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);
            if (flatDirection.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            visualMotion?.TriggerAttack(false);
            RangedProjectile.Fire(this, direction);
            if (SelectedMagazineAmmo <= 0 && SelectedReserveAmmo > 0) BeginRangedReload();
            return true;
        }

        private void BeginRangedReload()
        {
            if (rangedReloading || SelectedMagazineAmmo > 0 || SelectedReserveAmmo <= 0) return;
            rangedReloading = true;
            rangedReloadEndsAt = Time.time + ServerGameTuning.RangedReloadSeconds;
            // The authored clip is two seconds long, matching the default reload time.
            // Keep it local so simultaneous bot reloads do not clutter the player's mix.
            if (isLocalPlayer) CombatFeedback.PlayWeaponReload(transform.position);
        }

        private void UpdateRangedReload()
        {
            if (!rangedReloading || Time.time < rangedReloadEndsAt) return;
            rangedReloading = false;
            rangedReloadEndsAt = 0f;
            SelectedMagazineAmmo = Mathf.Min(MagazineCapacityFor(CurrentWeaponAmmo), SelectedReserveAmmo);
        }

        // Seed-launcher cadence is controlled by the host in shots per second; other
        // ammo types fire slower relative to it (see FireRateMultiplierFor).
        private float RangedAttackCooldown()
        {
            float shotsPerSecond = ServerGameTuning.RangedShotsPerSecond * FireRateMultiplierFor(CurrentWeaponAmmo);
            return 1f / Mathf.Max(0.01f, shotsPerSecond);
        }

        private void TryBasicAttack(Vector3 direction)
        {
            if (BattleRoyaleManager.Instance != null && !BattleRoyaleManager.Instance.CombatEnabled) return;
            if (Time.time < nextBasicAttackTime) return;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            nextBasicAttackTime = Time.time + stats.AttackCooldown;
            CombatFeedback.PlayBasic(animalType, transform.position);
            visualMotion?.TriggerAttack();
            StartCoroutine(ResolveBasicAttackAfterDelay(direction, visualMotion != null ? visualMotion.MeleeImpactDelay : 0f));
        }

        private IEnumerator ResolveBasicAttackAfterDelay(Vector3 direction, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (IsDefeated || health.IsDead) yield break;
            Color color = Color.Lerp(stats.MainColor, Color.white, 0.24f);
            PerformMeleeAttack(direction, stats.AttackDamage, stats.AttackRange, MeleeKnockback(), color, 1.65f);
        }

        private float MeleeKnockback() => animalType switch
        {
            AnimalType.Tiger => 11f,
            AnimalType.Cow => 10f,
            AnimalType.Monkey => 9f,
            AnimalType.Eagle => 8f,
            AnimalType.Ant => 6f,
            _ => 8f
        };

        private void TryUsePower(int slot, Vector3 direction)
        {
            if (BattleRoyaleManager.Instance != null && !BattleRoyaleManager.Instance.CombatEnabled) return;
            if (slot != 0 || vineLeaping) return;

            // A second Q while charging ends the run early instead of queuing a new charge.
            if (animalType == AnimalType.Cow && cowCharging)
            {
                cowCharging = false;
                abilityDashUntil = Time.time;
                return;
            }

            bool chainingVine = animalType == AnimalType.Monkey && hangingVine;
            if (chainingVine && !CanChainToAnotherVine) return;
            if (!chainingVine && Time.time < nextPowerTimes[0]) return;
            if (!chainingVine) nextPowerTimes[0] = Time.time + stats.AbilityCooldowns[0];
            lastPowerSlot = 0;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            CombatFeedback.PlayPower(animalType, 0, transform.position);
            AttackVfx.CreatePower(animalType, 0, transform.position, direction);
            visualMotion?.TriggerPower(0);
            if (!chainingVine) ForestMissionDirector.Instance?.NotifyAbilityUsed(this);
            UseAnimalAbility(direction);
        }

        private void UseAnimalAbility(Vector3 direction)
        {
            abilityHitTargets.Clear();
            switch (animalType)
            {
                case AnimalType.Tiger:
                    // Bote certeiro: pula exatamente para onde a câmera está mirando
                    // (inclusive para cima/baixo) em vez de só avançar na direção do movimento.
                    BeginTigerPounce();
                    break;
                case AnimalType.Ant:
                    // Climbing trees/rocks is disabled for now — tunnel entry only.
                    if (AntTunnelEntrance.TryEnter(this)) EnterBurrow();
                    break;
                case AnimalType.Eagle:
                    // Salto longo com planeio: impulso único e duração máxima de cinco segundos.
                    flyingUntil = Time.time + ServerGameTuning.EagleFlightDuration;
                    eagleGlideDirection = new Vector3(direction.x, 0f, direction.z).normalized;
                    if (eagleGlideDirection.sqrMagnitude < 0.01f) eagleGlideDirection = transform.forward;
                    verticalVelocity.y = Mathf.Max(verticalVelocity.y, ServerGameTuning.EagleJumpSpeed);
                    AttackVfx.CreateBurst(transform.position + Vector3.up * 0.4f, new Color(0.85f, 0.8f, 0.66f), 1.8f);
                    break;
                case AnimalType.Monkey:
                    // The first Q starts the chain; additional Q presses can visit up to five vines.
                    bool wasHangingVine = hangingVine;
                    if (!VineAnchor.TryUseNearest(this, direction) && !wasHangingVine)
                    {
                        verticalVelocity.y = 9f;
                        extraVelocity += new Vector3(direction.x, 0f, direction.z).normalized * 9f;
                    }
                    break;
                case AnimalType.Cow:
                    // Investida: straight-line charge that shoves anything in its path;
                    // a second Q (handled in TryUsePower) ends it before the max distance.
                    cowCharging = true;
                    BeginDash(direction, CowChargeMaxDuration, CowChargeSpeed, CowChargeDamage,
                        CowChargeKnockback, CowChargeRadius, 1f, CowChargeColor);
                    break;
            }
        }

        private void BeginDash(Vector3 direction, float duration, float speed, float damage, float knockback,
            float radius, float targetSlow, Color color)
        {
            dashDirection = new Vector3(direction.x, 0f, direction.z).normalized;
            if (dashDirection.sqrMagnitude < 0.01f) dashDirection = transform.forward;
            transform.rotation = Quaternion.LookRotation(dashDirection, Vector3.up);
            abilityDashUntil = Time.time + duration;
            nextAbilityDamage = Time.time;
            abilityDashSpeed = speed;
            abilityDamage = damage;
            abilityKnockback = knockback;
            abilityRadius = radius;
            abilitySlow = targetSlow;
            abilityColor = color;
        }

        /// <summary>Snaps onto the nearest in-range tree/rock trunk so <see cref="HandleClimbingTree"/> takes over.</summary>
        private bool TryEnterClimbingTree()
        {
            if (jungle == null) jungle = FindAnyObjectByType<JungleGenerator>();
            if (jungle == null || !jungle.TryFindNearestClimbable(transform.position, AntClimbApproachRange, out JungleGenerator.ClimbableSpot spot))
                return false;

            // Cling to the outside of the trunk (offset out from its center by its own
            // radius) rather than standing dead-center inside it — otherwise the camera's
            // own collision-avoidance treats the trunk as an obstacle right on top of the
            // ant and pulls itself in until it's practically inside the character.
            Vector3 outward = new Vector3(transform.position.x - spot.BasePosition.x, 0f, transform.position.z - spot.BasePosition.z);
            if (outward.sqrMagnitude < 0.01f) outward = -transform.forward;
            outward.Normalize();
            Vector3 surfaceBase = spot.BasePosition + outward * (spot.Radius + AntClimbSurfaceOffset);

            climbingTree = true;
            climbSpot = new JungleGenerator.ClimbableSpot(surfaceBase, spot.Height, spot.Radius);
            climbHeight = 0f;
            verticalVelocity = Vector3.zero;
            extraVelocity = Vector3.zero;
            if (characterController != null) characterController.enabled = false;
            transform.rotation = Quaternion.LookRotation(-outward, Vector3.up);
            transform.position = surfaceBase;
            return true;
        }

        /// <summary>While climbing, W/S move gradually up/down the trunk; the ant stays pinned to it (no drifting/flying).</summary>
        private void HandleClimbingTree()
        {
            if (isLocalPlayer && GameInput.JumpPressed())
            {
                ExitClimbingTree(hopOff: true);
                return;
            }

            float climbAxis = isLocalPlayer ? GameInput.ReadMovement().y : 0f;
            climbHeight = Mathf.Clamp(climbHeight + climbAxis * AntClimbSpeed * Time.deltaTime, 0f, climbSpot.Height);
            transform.position = climbSpot.BasePosition + Vector3.up * climbHeight;
            visualMotion?.SetLocomotion(false, false, false);

            if (climbHeight <= 0f && climbAxis <= 0f) ExitClimbingTree(hopOff: false);
        }

        private void ExitClimbingTree(bool hopOff)
        {
            climbingTree = false;
            if (characterController != null) characterController.enabled = true;
            if (hopOff)
            {
                verticalVelocity = Vector3.up * AntClimbJumpOffSpeed;
                extraVelocity = -transform.forward * 3f;
            }
            else
            {
                verticalVelocity = Vector3.zero;
            }
        }

        /// <summary>Rays out from the camera's aim (now free to point anywhere, including straight
        /// up) to find exactly where the tiger should land, then leaps there on a fixed arc.</summary>
        private void BeginTigerPounce()
        {
            Vector3 aimOrigin = cameraTransform != null ? cameraTransform.position : transform.position + Vector3.up;
            Vector3 aimDirection = ViewAimDirection;

            float targetDistance = TigerPounceMaxRange;
            int hitCount = Physics.RaycastNonAlloc(new Ray(aimOrigin, aimDirection), rangedAimHits,
                TigerPounceMaxRange, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = rangedAimHits[i];
                if (hit.collider == null) continue;
                Transform hitTransform = hit.collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
                if (hit.distance < targetDistance) targetDistance = hit.distance;
            }

            Vector3 targetPoint = aimOrigin + aimDirection * targetDistance;
            if (jungle == null) jungle = FindAnyObjectByType<JungleGenerator>();
            if (jungle != null) targetPoint = jungle.GetGroundPosition(targetPoint, 0.05f);

            tigerPounceStart = transform.position;
            tigerPounceTarget = targetPoint;
            float distance = Vector3.Distance(tigerPounceStart, tigerPounceTarget);
            float duration = Mathf.Clamp(distance / ServerGameTuning.TigerLeapSpeed, TigerPounceMinDuration, TigerPounceMaxDuration);
            tigerPounceStartedAt = Time.time;
            tigerPounceEndsAt = Time.time + duration;
            tigerPounceArcHeight = Mathf.Clamp(distance * 0.22f, 1.2f, 4.5f);
            tigerPouncing = true;
            verticalVelocity = Vector3.zero;
            extraVelocity = Vector3.zero;
            if (characterController != null) characterController.enabled = false;

            Vector3 flatDirection = tigerPounceTarget - tigerPounceStart;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            AttackVfx.CreateBurst(transform.position + Vector3.up * 0.2f, new Color(1f, 0.5f, 0.12f), 1.4f);
        }

        private void HandleTigerPounce()
        {
            float duration = Mathf.Max(0.01f, tigerPounceEndsAt - tigerPounceStartedAt);
            float progress = Mathf.Clamp01((Time.time - tigerPounceStartedAt) / duration);
            Vector3 position = Vector3.Lerp(tigerPounceStart, tigerPounceTarget, progress);
            position += Vector3.up * (Mathf.Sin(progress * Mathf.PI) * tigerPounceArcHeight);
            transform.position = position;
            visualMotion?.SetLocomotion(true, true, true);
            if (progress < 1f) return;

            transform.position = tigerPounceTarget;
            tigerPouncing = false;
            if (characterController != null) characterController.enabled = true;
            DamageTigerPounceImpact();
        }

        // Low damage on impact only, once, on whatever the tiger actually landed on —
        // this is a positioning tool first and a knockdown hit second.
        private void DamageTigerPounceImpact()
        {
            AttackVfx.CreateBurst(transform.position + Vector3.up * 0.2f, new Color(1f, 0.5f, 0.12f), 1.6f);
            Vector3 impactCenter = transform.position + Vector3.up * (stats.ControllerHeight * 0.5f);
            int hitCount = Physics.OverlapSphereNonAlloc(impactCenter, ServerGameTuning.TigerLeapHitRadius,
                combatHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Health target = combatHits[i].GetComponentInParent<Health>();
                if (target == null || target == health || target.IsDead || target.Owner == null) continue;
                if (target.Owner.IsBurrowed || target.Owner.IsSpawnProtected) continue;

                Vector3 pushDirection = target.transform.position - transform.position;
                pushDirection.y = 0f;
                if (pushDirection.sqrMagnitude < 0.01f) pushDirection = transform.forward;
                else pushDirection.Normalize();

                target.TakeDamage(ServerGameTuning.TigerLeapDamage, this);
                CombatFeedback.NotifyHit(AnimalType.Tiger, target.transform.position, ServerGameTuning.TigerLeapDamage);
                target.Owner.ReceiveKnockback((pushDirection + Vector3.up * 0.18f).normalized * ServerGameTuning.TigerLeapKnockback);
            }
        }

        private void PerformMeleeAttack(Vector3 direction, float damage, float range, float knockback, Color color, float visualSize)
        {
            AttackVfx.CreateSlash(transform.position + Vector3.up * (stats.ControllerHeight * 0.58f), direction, color, visualSize);
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.8f + direction * (range * 0.48f),
                range, combatHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Health other = combatHits[i].GetComponentInParent<Health>();
                if (other == null || other == health || other.IsDead || (other.Owner != null && other.Owner.IsBurrowed)) continue;
                Vector3 targetDirection = other.transform.position - transform.position;
                targetDirection.y = 0f;
                if (targetDirection.sqrMagnitude > 0.01f && Vector3.Dot(direction, targetDirection.normalized) < 0.18f) continue;
                other.TakeDamage(damage, this);
                CombatFeedback.NotifyHit(animalType, other.transform.position, damage);
                if (other.Owner != null) other.Owner.ReceiveKnockback(targetDirection.normalized * knockback);
                break;
            }
        }

        private void DamageArea(float radius, float damage, float knockback, Vector3 direction, Color color, float targetSlow)
        {
            AttackVfx.CreateBurst(transform.position + Vector3.up * 0.2f, color, radius);
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, radius, combatHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Health other = combatHits[i].GetComponentInParent<Health>();
                if (other == null || other == health || other.IsDead || abilityHitTargets.Contains(other)
                    || (other.Owner != null && other.Owner.IsBurrowed)) continue;
                abilityHitTargets.Add(other);
                other.TakeDamage(damage, this);
                CombatFeedback.NotifyHit(animalType, other.transform.position, damage);
                if (animalType == AnimalType.Cow) CombatFeedback.PlayCowImpact(other.transform.position);
                Vector3 push = other.transform.position - transform.position;
                push.y = 0.15f;
                if (other.Owner != null)
                {
                    other.Owner.ReceiveKnockback((push.sqrMagnitude > 0.01f ? push.normalized : direction) * knockback);
                    if (targetSlow < 0.99f) other.Owner.ApplySlow(targetSlow, 2.8f);
                }
            }
        }

        public void ApplySlow(float multiplier, float duration)
        {
            slowMultiplier = Mathf.Clamp(multiplier, 0.25f, 1f);
            slowUntil = Mathf.Max(slowUntil, Time.time + Mathf.Max(0f, duration));
        }

        public void ReceiveKnockback(Vector3 force) => extraVelocity += force;

        public void ReceiveLaunch(Vector3 direction, float horizontalSpeed, float upwardSpeed)
        {
            if (defeated || health == null || health.IsDead) return;
            Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z).normalized;
            if (flatDirection.sqrMagnitude < 0.01f) flatDirection = transform.forward;

            tigerPouncing = false;
            flyingUntil = 0f;
            abilityDashUntil = 0f;
            cowCharging = false;
            vineLeaping = false;
            hangingVine = false;
            heldVine = null;
            targetVine = null;
            vinesVisitedInChain = 0;
            visualMotion?.SetVineHanging(false);
            if (characterController != null) characterController.enabled = true;
            verticalVelocity.y = Mathf.Max(verticalVelocity.y, upwardSpeed);
            extraVelocity = flatDirection * horizontalSpeed;
        }

        public bool TryRefillRangedAmmo(RangedSupplyKind supplyKind, int amount)
        {
            if (defeated || health == null || health.IsDead || supplyKind != CompatibleRangedSupply || amount <= 0 || !NeedsRangedAmmo) return false;
            SelectedReserveAmmo = Mathf.Min(MaxAmmoFor(CurrentWeaponAmmo), SelectedReserveAmmo + amount);
            if (SelectedMagazineAmmo <= 0) BeginRangedReload();
            AttackVfx.CreateBurst(transform.position + Vector3.up * 0.6f, new Color(1f, 0.76f, 0.18f), 1.25f);
            return true;
        }

        public bool TryCollectWeaponCrystal()
        {
            if (defeated || health == null || health.IsDead || !CanUpgradeWeapon) return false;

            weaponCrystalProgress++;
            bool upgraded = weaponCrystalProgress >= CrystalsPerWeaponLevel;
            if (upgraded)
            {
                weaponLevel = Mathf.Min(MaxWeaponLevel, weaponLevel + 1);
                weaponCrystalProgress = 0;
                // The newly unlocked weapon comes with a full clip and reserve, ready to select.
                int newSlot = weaponLevel - 1;
                weaponReserveAmmo[newSlot] = MaxAmmoFor((WeaponAmmoType)newSlot);
                weaponMagazineAmmo[newSlot] = MagazineCapacityFor((WeaponAmmoType)newSlot);
            }

            Color crystalColor = upgraded
                ? new Color(0.18f, 1f, 0.95f)
                : new Color(0.08f, 0.78f, 1f);
            AttackVfx.CreateBurst(transform.position + Vector3.up * 0.75f, crystalColor, upgraded ? 2.8f : 1.45f);
            BattleRoyaleManager.Instance?.NotifyWeaponCrystalCollected(this, upgraded);
            return true;
        }

        public void RestoreMobilityEnergy(float amount) { }

        public bool IsHoldingVine(Transform vine) => hangingVine && heldVine == vine;

        public bool TryGrabVine(Transform vine)
        {
            if (animalType != AnimalType.Monkey || vine == null || defeated || hangingVine || vineLeaping) return false;
            if (!VineAnchor.IsWithinUseRange(this, vine)) return false;
            vinesVisitedInChain = 1;
            return BeginVineLeap(vine);
        }

        public bool TryLaunchToVine(Transform vine)
        {
            if (vine == null || vine == heldVine || !CanChainToAnotherVine || vineLeaping) return false;
            if (!VineAnchor.IsWithinUseRange(this, vine)) return false;
            vinesVisitedInChain++;
            return BeginVineLeap(vine);
        }

        private bool BeginVineLeap(Transform vine)
        {
            if (vine == null) return false;
            targetVine = vine;
            vineLeapStart = transform.position;
            vineLeapEnd = vine.position - Vector3.up * (stats.ControllerHeight * 0.82f);
            float distance = Vector3.Distance(vineLeapStart, vineLeapEnd);
            float duration = Mathf.Clamp(distance / VineLeapSpeed, 0.24f, 0.55f);
            vineLeapStartedAt = Time.time;
            vineLeapEndsAt = Time.time + duration;
            vineLeapArcHeight = Mathf.Clamp(distance * 0.16f, 0.8f, 2.2f);
            vineLeaping = true;
            hangingVine = false;
            heldVine = null;
            flyingUntil = 0f;
            verticalVelocity = Vector3.zero;
            extraVelocity = Vector3.zero;
            if (characterController != null) characterController.enabled = false;

            Vector3 flatDirection = vineLeapEnd - vineLeapStart;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            AttackVfx.CreateBurst(transform.position + Vector3.up * 0.5f, new Color(0.55f, 0.85f, 0.3f), 1.1f);
            visualMotion?.TriggerPower(0);
            return true;
        }

        private void HandleVineLeap()
        {
            if (targetVine == null)
            {
                vineLeaping = false;
                if (characterController != null) characterController.enabled = true;
                return;
            }

            float duration = Mathf.Max(0.01f, vineLeapEndsAt - vineLeapStartedAt);
            float progress = Mathf.Clamp01((Time.time - vineLeapStartedAt) / duration);
            Vector3 position = Vector3.Lerp(vineLeapStart, vineLeapEnd, progress);
            position += Vector3.up * (Mathf.Sin(progress * Mathf.PI) * vineLeapArcHeight);
            transform.position = position;
            visualMotion?.SetLocomotion(true, true, true);
            if (progress < 1f) return;

            transform.position = vineLeapEnd;
            heldVine = targetVine;
            targetVine = null;
            vineLeaping = false;
            hangingVine = true;
            if (characterController != null) characterController.enabled = true;
            AttackVfx.CreateBurst(heldVine.position, new Color(0.55f, 0.85f, 0.3f), 1.1f);
            visualMotion?.SetVineHanging(true, ShouldGrabWithLeftHand(heldVine));
        }

        // Which hand visually grips the vine depends on which side it's on relative to
        // the monkey at the moment of the grab — cheap and good enough without IK.
        private bool ShouldGrabWithLeftHand(Transform vine)
        {
            return vine == null || transform.InverseTransformPoint(vine.position).x <= 0f;
        }

        public bool TryLaunchFromVine(Vector3 direction)
        {
            if (!hangingVine) return false;
            VineAnchor vineAnchor = heldVine != null ? heldVine.GetComponent<VineAnchor>() : null;
            Vector3 carriedSwingVelocity = vineAnchor != null ? vineAnchor.SwingVelocity : Vector3.zero;
            hangingVine = false;
            heldVine = null;
            targetVine = null;
            vinesVisitedInChain = 0;
            visualMotion?.SetVineHanging(false);
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            Vector3 forward = flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;
            verticalVelocity.y = VineLaunchUp + Mathf.Max(0f, carriedSwingVelocity.y * 0.35f);
            extraVelocity += forward * VineLaunchForward
                             + Vector3.ProjectOnPlane(carriedSwingVelocity, Vector3.up) * 0.8f;
            AttackVfx.CreateBurst(transform.position + Vector3.up * 0.6f, new Color(0.5f, 0.9f, 0.35f), 1.2f);
            return true;
        }

        public void TeleportTo(Vector3 worldPosition)
        {
            bool wasEnabled = characterController.enabled;
            characterController.enabled = false;
            transform.position = worldPosition;
            verticalVelocity = Vector3.zero;
            extraVelocity = Vector3.zero;
            characterController.enabled = wasEnabled;
        }

        public float ModifyIncomingDamage(float damage)
        {
            if (IsSpawnProtected) return 0f;
            if (Time.time < resistanceUntil) damage *= 0.52f;
            return damage;
        }

        /// <summary>Spends one life. Returns true if the fighter still has lives left (respawns).</summary>
        public bool ConsumeLife()
        {
            livesRemaining = Mathf.Max(0, livesRemaining - 1);
            return livesRemaining > 0;
        }

        /// <summary>Hides the body during the countdown before a respawn (kept alive, not eliminated).</summary>
        public void BeginRespawnCountdown()
        {
            if (characterController != null) characterController.enabled = false;
            foreach (Collider collider in GetComponentsInChildren<Collider>()) if (collider != null) collider.enabled = false;
            Transform visual = transform.Find("VisualRoot");
            if (visual != null)
            {
                visualMotion?.Freeze();
                visual.gameObject.SetActive(false);
            }
        }

        /// <summary>Revive at a fresh position with brief spawn protection (keeps the same GameObject).</summary>
        public void Respawn(Vector3 position)
        {
            if (eliminated) return;
            EnsureRuntimeReferences();
            defeated = false;
            tigerPouncing = false;
            flyingUntil = 0f;
            vineLeaping = false;
            hangingVine = false;
            heldVine = null;
            targetVine = null;
            vinesVisitedInChain = 0;
            visualMotion?.SetVineHanging(false);
            if (characterController != null) characterController.enabled = true;
            burrowed = false;
            burrowEntering = false;
            climbingTree = false;
            SetUndergroundWalkLoop(false);
            AntTunnelEntrance.CancelTravel(this);
            verticalVelocity = Vector3.zero;
            extraVelocity = Vector3.zero;
            abilityDashUntil = 0f;
            cowCharging = false;
            slowUntil = 0f;
            slowMultiplier = 1f;

            Transform visual = transform.Find("VisualRoot");
            if (visual != null) visual.gameObject.SetActive(true);
            visualMotion?.Unfreeze();
            foreach (Collider collider in GetComponentsInChildren<Collider>()) if (collider != null) collider.enabled = true;

            health.Initialize(stats.MaxHealth, this);
            for (int slot = 0; slot < weaponLevel; slot++)
            {
                weaponReserveAmmo[slot] = MaxAmmoFor((WeaponAmmoType)slot);
                weaponMagazineAmmo[slot] = MagazineCapacityFor((WeaponAmmoType)slot);
            }
            rangedReloading = false;
            rangedReloadEndsAt = 0f;
            SafeZoneController safeZone = SafeZoneController.Instance;
            if (safeZone != null) position = safeZone.ClampRespawnPoint(position);
            TeleportTo(position);
            SnapToTerrain(jungle != null ? jungle : FindAnyObjectByType<JungleGenerator>());
            spawnProtectedUntil = Time.time + SpawnProtectionSeconds;
            AttackVfx.CreateBurst(transform.position + Vector3.up * 0.6f, new Color(0.4f, 0.85f, 1f), 2.2f);
        }

        public void SetDefeated()
        {
            if (defeated) return;
            defeated = true;
            eliminated = true;
            verticalVelocity = Vector3.zero;
            extraVelocity = Vector3.zero;
            SetUndergroundWalkLoop(false);
            if (characterController != null) characterController.enabled = false;
            foreach (Collider collider in GetComponentsInChildren<Collider>()) if (collider != null) collider.enabled = false;
            Transform visual = transform.Find("VisualRoot");
            if (visual != null)
            {
                visualMotion?.Freeze();
                visual.gameObject.SetActive(false);
            }
            AttackVfx.CreateBurst(transform.position + Vector3.up * (stats.ControllerHeight * 0.45f), new Color(0.92f, 0.08f, 0.045f), 1.9f);
            Destroy(gameObject, DefeatedCleanupDelay);
        }

        private void OnDestroy()
        {
            if (Application.isPlaying) BattleRoyaleManager.Instance?.HandleFighterDisconnected(this);
        }
    }
}
