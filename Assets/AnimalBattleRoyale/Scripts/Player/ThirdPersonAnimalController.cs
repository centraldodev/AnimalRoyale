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

        // Seed ("nozes") is the precision one-shot-then-reload sniper slot — small reserve,
        // one round per magazine on purpose (see FireRateMultiplierFor/reload flow).
        private const int SeedMaxAmmo = 20;
        private const int SeedMagazineCapacity = 1;
        // Tomato is now the primary sustained-fire weapon.
        private const int TomatoMaxAmmo = 120;
        private const int TomatoMagazineCapacity = 12;
        private const int WatermelonMaxAmmo = 60;
        private const int WatermelonMagazineCapacity = 5;
        private const float DefeatedCleanupDelay = 1.5f;

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
        private float nextStuckCheckTime;
        private float nextAutoPickupCheck;
        private float lastStuckSampleTime;
        private float obstructedSeconds;
        private float immobileSeconds;
        private Vector3 lastStuckSamplePosition;
        private Vector3 lastKnownSafePosition;
        private Vector3 lastRequestedMoveDirection;
        private bool stuckSampleInitialized;
        private bool hasLastKnownSafePosition;
        private bool movementRequestedSinceStuckCheck;
        private bool wasGroundedForLandingSfx = true;
        private Vector3 verticalVelocity;
        private Vector3 extraVelocity;
        private bool tigerRoarLaunching;
        private Vector3 tigerRoarLaunchDestination;
        private float tigerRoarLaunchSpeed;
        private float tigerRoarLaunchUntil;
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
        private float rootedUntil;
        private float resistanceUntil;
        private float eagleVisionUntil;
        private float abilityDashUntil;
        private float nextAbilityDamage;
        private float abilityDashSpeed;
        private float abilityDamage;
        private float abilityKnockback;
        private float abilityRadius;
        private float abilitySlow;
        private Vector3 dashDirection;
        private Color abilityColor;
        // Total carried ammunition per type (loaded magazine included). A fresh fighter
        // starts with exactly one loaded magazine of each type and no spare reloads.
        private readonly int[] weaponReserveAmmo = { 0, 0, 0 };
        private readonly int[] weaponMagazineAmmo = { 0, 0, 0 };
        private WeaponAmmoType selectedWeapon = WeaponAmmoType.Tomato;
        private bool rangedReloading;
        private float rangedReloadEndsAt;
        private int lastPowerSlot = -1;
        private bool defeated;
        private bool eliminated;
        private int livesRemaining = MaxLives;
        private float spawnProtectedUntil;
        private bool tigerPouncing;
        private float tigerPounceStartedAt;
        private Vector3 tigerPounceVelocity;
        private Vector3 tigerPounceFallbackAimDirection;
        private float flyingUntil;
        private Vector3 eagleGlideDirection;
        private Transform heldVine;
        private Transform targetVine;
        private bool hangingVine;
        private bool isAiming;
        private bool vineLeaping;
        private bool cowCharging;
        private float vineLeapStartedAt;
        private float vineLeapEndsAt;
        private Vector3 vineLeapStart;
        private Vector3 vineLeapEnd;
        private Vector3 vineLeapSideOffset;
        private Vector3 previousVineLeapPosition;
        private Vector3 vineLeapVelocity;
        private Vector3 vineLeapStartVelocity;
        private bool burrowed;
        private bool burrowEntering;
        private float burrowEntryUntil;
        private const float BurrowJumpDuration = 0.35f;
        private const float AntTunnelAimExitRange = 45f;
        private const float TigerPounceMaxRange = 50f;
        private const float TigerPounceMaxDuration = 2.1f;
        private const float TigerPounceCollisionGrace = 0.09f;
        private const float TigerPounceLaunchLift = 0.18f;
        private bool networkProxy;
        private Vector3 networkTargetPosition;
        private Quaternion networkTargetRotation;
        private Vector3 lastNetworkVisualPosition;

        public const int MaxLives = 3;
        private const float SpawnProtectionSeconds = 2.5f;
        private const float StuckCheckInterval = 0.35f;
        private const float ObstructedRecoveryDelay = 0.7f;
        private const float ImmobileRecoveryDelay = 2.1f;

        private const float VineLeapSpeed = 22f;
        private const float VineSideControlSpeed = 8f;
        private const float VineMaximumSideOffset = 5f;
        private const float VineRetargetMomentumCorrection = 8f;
        private const float VineLaunchForward = 15f;
        private const float VineLaunchUp = 9f;

        private const float CowChargeDistance = 30f;
        // Slightly faster than the Cow's 8.4 m/s normal sprint, while remaining controllable.
        private const float CowChargeSpeed = 11f;
        private const float CowChargeMaxDuration = CowChargeDistance / CowChargeSpeed;
        private const float CowChargeDamage = 20f;
        private const float CowChargeKnockback = 12f;
        private const float CowChargeRadius = 1.8f;
        private const float CowChargeTurnRateDegreesPerSecond = 220f;
        private static readonly Color CowChargeColor = new Color(0.75f, 0.55f, 0.32f);
        private const float EagleGlideTurnRateDegreesPerSecond = 260f;
        private const float TigerPounceTurnRateDegreesPerSecond = 420f;
        private const float TigerRoarRadius = 14f;
        private const float TigerRoarSlowDuration = 2.5f;
        private const float TigerRoarLaunchDuration = 0.65f;
        private const float TigerRoarUpwardSpeed = 4.2f;
        private const float AntAcidMaximumRange = 12f;
        private const float AntAcidRadius = 6f;
        private const float MonkeySnareMaximumRange = 18f;
        private const float MonkeySnareDuration = 2f;
        private const float MonkeySnarePullDistance = 4f;
        public const float EagleVisionRange = 55f;

        public AnimalType AnimalType => animalType;
        public AnimalStats Stats { get { EnsureRuntimeReferences(); return stats; } }
        public Health Health => health;
        public bool IsLocalPlayer => isLocalPlayer;
        public bool IsDefeated => defeated;
        public bool IsEliminated => eliminated;
        public int LivesRemaining => livesRemaining;
        public bool IsSpawnProtected => Time.time < spawnProtectedUntil;
        public bool IsFlying => Time.time < flyingUntil;
        public bool IsAiming => isAiming;
        public float GlideSecondsRemaining => Mathf.Max(0f, flyingUntil - Time.time);
        public bool IsBurrowed => burrowed;
        public bool IsNetworkProxy => networkProxy;
        public bool IsInAntTunnel => burrowed;
        public bool IsHangingVine => hangingVine;
        public bool IsVineLeaping => vineLeaping;
        public bool IsCowCharging => cowCharging;
        public bool IsRooted => Time.time < rootedUntil;
        public bool IsEagleVisionActive => animalType == AnimalType.Eagle && Time.time < eagleVisionUntil;
        // Grappling is continuous locomotion: while attached, the monkey can always choose
        // another valid point instead of hitting an artificial chain limit.
        public bool CanChainToAnotherVine => hangingVine || vineLeaping;
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
        public WeaponAmmoType CurrentWeaponAmmo => selectedWeapon;
        public int SelectedWeaponSlot => (int)selectedWeapon;

        /// <summary>Backpack reserve for a given ammo type, regardless of which one is
        /// currently equipped — used by the HUD to show what's been collected of each.</summary>
        public int ReserveAmmoFor(WeaponAmmoType weapon) => weaponReserveAmmo[(int)weapon];
        public static int MaxAmmoForWeapon(WeaponAmmoType weapon) => MaxAmmoFor(weapon);
        public bool HasAmmoFor(WeaponAmmoType weapon)
        {
            int slot = (int)weapon;
            return slot >= 0 && slot < weaponReserveAmmo.Length && weaponReserveAmmo[slot] > 0;
        }

        /// <summary>Switches the active ammunition while that type still has at least one
        /// round. Empty types remain visible in the HUD but unavailable.</summary>
        public bool TrySelectWeapon(WeaponAmmoType weapon)
        {
            int slot = (int)weapon;
            if (slot < 0 || slot >= weaponReserveAmmo.Length || weaponReserveAmmo[slot] <= 0)
                return false;
            if (selectedWeapon == weapon) return false;
            selectedWeapon = weapon;
            rangedReloading = false;
            rangedReloadEndsAt = 0f;
            if (SelectedMagazineAmmo <= 0 && SelectedReserveAmmo > 0) BeginRangedReload();
            return true;
        }

        // Tomato (primary) targets ~3 shots/sec at the default 7 shots/sec base — was too fast
        // at the old 0.5 multiplier (3.5/s). Watermelon is unchanged. Seed ("nozes") doesn't
        // really have a "rate" since its 1-round magazine forces a reload after every shot —
        // multiplier left at 1 just means the single shot fires the instant you click.
        private static float FireRateMultiplierFor(WeaponAmmoType ammoType) => ammoType switch
        {
            WeaponAmmoType.Tomato => 3f / 7f,
            WeaponAmmoType.Watermelon => 0.5f / 3f,
            _ => 1f
        };

        // Nozes ("quase instantâneo" — a precision weapon you have to aim to land) is much
        // faster than the base speed; tomato travels at twice its previous 1.2x speed while
        // keeping damage/rate/range unchanged; watermelon stays at the unmultiplied base.
        public static float ProjectileSpeedMultiplierFor(WeaponAmmoType ammoType) => ammoType switch
        {
            WeaponAmmoType.Tomato => 2.4f,
            WeaponAmmoType.Watermelon => 1f,
            _ => 4.5f
        };

        private static int MagazineCapacityFor(WeaponAmmoType ammoType) => ammoType switch
        {
            WeaponAmmoType.Tomato => TomatoMagazineCapacity,
            WeaponAmmoType.Watermelon => WatermelonMagazineCapacity,
            _ => SeedMagazineCapacity
        };

        public static float ReloadSecondsForWeapon(WeaponAmmoType ammoType) => ammoType switch
        {
            WeaponAmmoType.Tomato => 2f,
            WeaponAmmoType.Watermelon => 3f,
            _ => 4f
        };

        public static WeaponAmmoType WeaponForSelectionSlot(int slot) => slot switch
        {
            0 => WeaponAmmoType.Tomato,
            1 => WeaponAmmoType.Watermelon,
            _ => WeaponAmmoType.Seed
        };

        private static int SelectionSlotForWeapon(WeaponAmmoType weapon) => weapon switch
        {
            WeaponAmmoType.Tomato => 0,
            WeaponAmmoType.Watermelon => 1,
            _ => 2
        };

        private void LoadStartingMagazines()
        {
            for (int slot = 0; slot < weaponReserveAmmo.Length; slot++)
            {
                WeaponAmmoType weapon = (WeaponAmmoType)slot;
                int loadedRounds = MagazineCapacityFor(weapon);
                weaponReserveAmmo[slot] = loadedRounds;
                weaponMagazineAmmo[slot] = loadedRounds;
            }
        }

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
            _ => "NOZES"
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
            // The grapple is locomotion, so the monkey can reconnect after releasing instead
            // of being grounded for the normal combat-ability cooldown.
            if (slot == 0 && animalType == AnimalType.Monkey) return 0f;
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
                HandleVineRetargetInput();
                HandleVineLeap();
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
            RecoverIfBelowTerrainOrStuck();
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
            int replicatedNozesAmmo, int replicatedTomatoAmmo, int replicatedWatermelonAmmo, int replicatedMagazineAmmo,
            int replicatedSelectedWeapon, bool replicatedEliminated, bool applyTransform)
        {
            livesRemaining = Mathf.Clamp(replicatedLives, 0, MaxLives);
            selectedWeapon = (WeaponAmmoType)Mathf.Clamp(replicatedSelectedWeapon, 0, 2);
            // All three ammo types replicate independently now (no more single "current
            // weapon ammo" value) so switching weapons on a remote fighter shows the right
            // backpack counts instead of a stale/zeroed reserve.
            weaponReserveAmmo[(int)WeaponAmmoType.Seed] = Mathf.Clamp(replicatedNozesAmmo, 0, MaxAmmoFor(WeaponAmmoType.Seed));
            weaponReserveAmmo[(int)WeaponAmmoType.Tomato] = Mathf.Clamp(replicatedTomatoAmmo, 0, MaxAmmoFor(WeaponAmmoType.Tomato));
            weaponReserveAmmo[(int)WeaponAmmoType.Watermelon] = Mathf.Clamp(replicatedWatermelonAmmo, 0, MaxAmmoFor(WeaponAmmoType.Watermelon));
            SelectedMagazineAmmo = Mathf.Clamp(replicatedMagazineAmmo, 0, Mathf.Min(MagazineCapacityFor(CurrentWeaponAmmo), SelectedReserveAmmo));
            health?.ApplyReplicatedState(replicatedHealth);
            eliminated = replicatedEliminated;
            defeated = replicatedEliminated;
            Transform visual = transform.Find("VisualRoot");
            if (visual != null) visual.gameObject.SetActive(!replicatedEliminated);
            if (characterController != null) characterController.enabled = !replicatedEliminated;
            if (applyTransform) ApplyNetworkTransform(position, rotation, false);
        }

        public bool ExecuteNetworkAction(OnlineActionType action, Vector3 direction)
        {
            if (eliminated || health == null || health.IsDead) return false;
            Vector3 actionDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            switch (action)
            {
                case OnlineActionType.RangedAttack:
                    return TryRangedAttack(actionDirection);
                case OnlineActionType.MeleeAttack:
                    TryBasicAttack(actionDirection);
                    return true;
                case OnlineActionType.Ability:
                    return TryUsePower(0, actionDirection);
                case OnlineActionType.AbilitySecondary:
                    return TryUsePower(1, actionDirection);
                case OnlineActionType.Consume:
                    if (!RangedAmmoPickup.TryCollectNearest(this))
                        FoodPickup.TryConsumeNearest(this);
                    return true;
                case OnlineActionType.SelectWeapon:
                    return TrySelectWeapon((WeaponAmmoType)Mathf.RoundToInt(direction.x));
                case OnlineActionType.Reload:
                    return BeginRangedReload(true);
                default:
                    return false;
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
            Vector3 visualDisplacement = transform.position - lastNetworkVisualPosition;
            float speed = new Vector2(visualDisplacement.x, visualDisplacement.z).magnitude
                          / Mathf.Max(0.001f, Time.deltaTime);
            bool moving = speed > 0.12f;
            visualMotion?.SetLocomotion(moving, speed > stats.MoveSpeed * 1.15f, false,
                horizontalSpeed: speed, referenceWalkSpeed: stats.MoveSpeed);
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
            ClearSecondaryAim();
            CancelTigerRoarLaunch();
            tigerPouncing = false;
            tigerPounceVelocity = Vector3.zero;
            abilityDashUntil = 0f;
            cowCharging = false;
            rootedUntil = 0f;
            eagleVisionUntil = 0f;
            vineLeaping = false;
            hangingVine = false;
            VineAnchor.DestroyThrown(heldVine);
            heldVine = null;
            targetVine = null;
            characterController.enabled = true;
            characterController.radius = stats.ControllerRadius;
            characterController.height = stats.ControllerHeight;
            characterController.center = new Vector3(0f, stats.ControllerHeight * 0.5f, 0f);
            characterController.stepOffset = Mathf.Min(0.35f, stats.ControllerHeight * 0.25f);
            characterController.skinWidth = 0.04f;
            Transform visualRoot = AnimalVisualFactory.Build(transform, type, stats.MainColor, stats.VisualScale);
            visualMotion = visualRoot != null ? visualRoot.GetComponent<AnimalVisualMotion>() : null;
            selectedWeapon = WeaponAmmoType.Tomato;
            LoadStartingMagazines();
            rangedReloading = false;
            rangedReloadEndsAt = 0f;
            health.Initialize(stats.MaxHealth, this);
            if (!refillHealth) health.ReconfigureMaxHealth(stats.MaxHealth, false);
        }

        public void SnapToTerrain(JungleGenerator terrain, float clearance = 0.12f)
        {
            if (terrain == null) return;
            jungle = terrain;
            Vector3 safePosition;
            if (!terrain.TryFindSafeAnimalPosition(transform.position, stats.ControllerRadius,
                    stats.ControllerHeight, out safePosition, 18f, transform))
                safePosition = terrain.GetGroundPosition(transform.position, clearance);
            bool wasEnabled = characterController != null && characterController.enabled;
            if (wasEnabled) characterController.enabled = false;
            transform.position = safePosition;
            verticalVelocity = Vector3.zero;
            if (wasEnabled) characterController.enabled = true;
            lastKnownSafePosition = safePosition;
            hasLastKnownSafePosition = true;
            ResetStuckDetection();
        }

        private void RecoverIfBelowTerrainOrStuck()
        {
            if (jungle == null) return;
            if (Time.time >= nextGroundCheckTime)
            {
                nextGroundCheckTime = Time.time + 0.1f;
                // Cheap height check first; only perform a complete safe-position search
                // when the animal actually fell through the rendered terrain.
                float groundHeight = jungle.GroundHeightAt(transform.position);
                if (transform.position.y < groundHeight - 1f)
                {
                    SnapToTerrain(jungle);
                    return;
                }
            }

            if (hangingVine || IsFlying || tigerPouncing || cowCharging
                || Time.time < abilityDashUntil)
            {
                ResetStuckDetection();
                return;
            }
            if (Time.time < nextStuckCheckTime) return;

            float now = Time.time;
            float elapsed = stuckSampleInitialized
                ? Mathf.Max(0.01f, now - lastStuckSampleTime)
                : StuckCheckInterval;
            nextStuckCheckTime = now + StuckCheckInterval;
            lastStuckSampleTime = now;

            Vector3 current = transform.position;
            Vector3 planarDelta = current - lastStuckSamplePosition;
            planarDelta.y = 0f;
            bool moved = planarDelta.sqrMagnitude >= 0.08f * 0.08f;
            bool grounded = characterController != null && characterController.isGrounded;
            float inset = -Mathf.Min(0.07f, stats.ControllerRadius * 0.18f);
            bool positionClear = jungle.IsAnimalPositionClear(current, stats.ControllerRadius,
                stats.ControllerHeight, transform, inset);

            if (!stuckSampleInitialized)
            {
                stuckSampleInitialized = true;
                lastStuckSamplePosition = current;
                if (positionClear && grounded)
                {
                    lastKnownSafePosition = current;
                    hasLastKnownSafePosition = true;
                }
                movementRequestedSinceStuckCheck = false;
                return;
            }

            if (positionClear)
            {
                obstructedSeconds = 0f;
                if (grounded && (moved || !hasLastKnownSafePosition))
                {
                    lastKnownSafePosition = current;
                    hasLastKnownSafePosition = true;
                }
            }
            else
            {
                obstructedSeconds += elapsed;
            }

            if (movementRequestedSinceStuckCheck && grounded && !moved)
                immobileSeconds += elapsed;
            else
                immobileSeconds = 0f;

            lastStuckSamplePosition = current;
            movementRequestedSinceStuckCheck = false;
            if (obstructedSeconds < ObstructedRecoveryDelay
                && immobileSeconds < ImmobileRecoveryDelay)
                return;

            Vector3 rescuePosition = default;
            bool foundRescue = false;
            if (hasLastKnownSafePosition)
            {
                Vector3 toLastSafe = lastKnownSafePosition - current;
                toLastSafe.y = 0f;
                foundRescue = toLastSafe.sqrMagnitude >= 0.35f * 0.35f
                              && toLastSafe.sqrMagnitude <= 12f * 12f
                              && jungle.IsAnimalPositionClear(lastKnownSafePosition,
                                  stats.ControllerRadius, stats.ControllerHeight, transform, 0.16f);
                if (foundRescue) rescuePosition = lastKnownSafePosition;
            }

            if (!foundRescue)
            {
                Vector3 searchOrigin = current;
                Vector3 requestedFlat = new Vector3(
                    lastRequestedMoveDirection.x, 0f, lastRequestedMoveDirection.z);
                if (immobileSeconds >= ImmobileRecoveryDelay && requestedFlat.sqrMagnitude > 0.01f)
                    searchOrigin -= requestedFlat.normalized * Mathf.Max(1.5f, stats.ControllerRadius * 2.5f);

                foundRescue = jungle.TryFindSafeAnimalPosition(searchOrigin,
                    stats.ControllerRadius, stats.ControllerHeight, out rescuePosition, 14f, transform);
            }

            if (foundRescue)
            {
                TeleportTo(rescuePosition);
                lastKnownSafePosition = rescuePosition;
                hasLastKnownSafePosition = true;
                AttackVfx.CreateBurst(rescuePosition + Vector3.up * 0.45f,
                    new Color(0.35f, 0.9f, 0.55f), 0.9f);
            }
            ResetStuckDetection();
        }

        private void ResetStuckDetection()
        {
            obstructedSeconds = 0f;
            immobileSeconds = 0f;
            stuckSampleInitialized = false;
            movementRequestedSinceStuckCheck = false;
            nextStuckCheckTime = Time.time + StuckCheckInterval;
        }

        private void HandleLocalInput()
        {
            Vector2 rawInput = GameInput.ReadMovement();
            Vector3 movement = GetCameraRelativeDirection(rawInput);
            // Secondary aim is available with tomato, watermelon and seed ammo.
            isAiming = GameInput.AimHeld();
            if (cameraTransform != null) cameraTransform.GetComponent<ThirdPersonCamera>()?.SetAiming(isAiming);
            bool sprint = !isAiming && (GameSettings.AutomaticSprint || GameInput.SprintHeld());
            SimulateMovement(movement, sprint, GameInput.JumpPressed(), rawInput, useStrafing: true);
            bool rangedAttackRequested = GameSettings.RangedFireMode == RangedFireMode.Automatic
                ? GameInput.RangedAttackHeld()
                : GameInput.RangedAttackPressed();
            if (GameInput.ReloadPressed() && BeginRangedReload(true))
            {
                OnlineMultiplayerManager.Instance?.ReportAction(OnlineActionType.Reload, Vector3.zero);
            }
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
                // Grapple replication needs the center-camera ray, not the direction the
                // monkey happens to be walking, so remote peers hit the same aimed surface.
                Vector3 direction = animalType == AnimalType.Monkey || animalType == AnimalType.Tiger
                    ? ViewAimDirection
                    : GetAttackDirection(movement);
                OnlineMultiplayerManager.Instance?.ReportAction(OnlineActionType.Ability, direction);
                TryUsePower(0, direction);
            }
            if (GameInput.AbilityTwoPressed())
            {
                Vector3 direction = ViewAimDirection;
                if (TryUsePower(1, direction))
                    OnlineMultiplayerManager.Instance?.ReportAction(OnlineActionType.AbilitySecondary, direction);
            }
            // Walking over cura/ammo now collects them automatically; F (below) still
            // works too so both ways of picking things up are available.
            if (Time.time >= nextAutoPickupCheck)
            {
                nextAutoPickupCheck = Time.time + 0.2f;
                OnlineMultiplayerManager autoPickupOnline = OnlineMultiplayerManager.Instance;
                if (autoPickupOnline == null || !autoPickupOnline.UsesRemoteAuthority)
                {
                    if (!RangedAmmoPickup.TryCollectNearest(this))
                        FoodPickup.TryConsumeNearest(this);
                }
            }
            if (GameInput.ConsumePressed())
            {
                OnlineMultiplayerManager online = OnlineMultiplayerManager.Instance;
                online?.ReportAction(OnlineActionType.Consume, transform.forward);
                if (online == null || !online.UsesRemoteAuthority)
                {
                    if (!RangedAmmoPickup.TryCollectNearest(this))
                        FoodPickup.TryConsumeNearest(this);
                }
            }
            int weaponSelection = GameInput.ReadWeaponSelection();
            WeaponAmmoType requestedWeapon = WeaponForSelectionSlot(weaponSelection);
            if (weaponSelection >= 0 && TrySelectWeapon(requestedWeapon))
            {
                OnlineMultiplayerManager.Instance?.ReportAction(OnlineActionType.SelectWeapon,
                    new Vector3((int)requestedWeapon, 0f, 0f));
            }
            int weaponScroll = GameInput.ReadWeaponScroll();
            if (weaponScroll != 0 && TryCycleWeapon(weaponScroll))
            {
                OnlineMultiplayerManager.Instance?.ReportAction(OnlineActionType.SelectWeapon,
                    new Vector3((int)selectedWeapon, 0f, 0f));
            }
        }

        // The normal input path is skipped while the grapple owns movement, so both abilities
        // are sampled here as well. Q retargets the traversal vine; E throws the combat snare.
        private void HandleVineRetargetInput()
        {
            if (!isLocalPlayer || animalType != AnimalType.Monkey) return;
            if (GameInput.AbilityOnePressed())
            {
                Vector3 direction = ViewAimDirection;
                OnlineMultiplayerManager.Instance?.ReportAction(OnlineActionType.Ability, direction);
                TryUsePower(0, direction);
            }
            if (GameInput.AbilityTwoPressed())
            {
                Vector3 direction = ViewAimDirection;
                if (TryUsePower(1, direction))
                    OnlineMultiplayerManager.Instance?.ReportAction(OnlineActionType.AbilitySecondary, direction);
            }
        }

        private const int WeaponTypeCount = 3;

        /// <summary>Cycles among all three weapons; +1 = next, -1 = previous, wrapping around.</summary>
        private bool TryCycleWeapon(int direction)
        {
            int current = SelectionSlotForWeapon(selectedWeapon);
            int step = direction >= 0 ? 1 : -1;
            for (int offset = 1; offset <= WeaponTypeCount; offset++)
            {
                int next = ((current + step * offset) % WeaponTypeCount + WeaponTypeCount)
                           % WeaponTypeCount;
                WeaponAmmoType candidate = WeaponForSelectionSlot(next);
                if (HasAmmoFor(candidate)) return TrySelectWeapon(candidate);
            }
            return false;
        }

        private void HandleAIInput()
        {
            SimulateMovement(aiMoveDirection, aiSprint, false);
            if (aiAttack)
            {
                if (aiRangedAttack) TryRangedAttack(aiAttackDirection);
                else TryBasicAttack(aiAttackDirection);
            }
            if (aiAbilitySlot >= 0)
            {
                Vector3 abilityDirection = aiAttackDirection.sqrMagnitude > 0.01f
                    ? aiAttackDirection : aiMoveDirection;
                if (TryUsePower(aiAbilitySlot, abilityDirection) && aiAbilitySlot == 1)
                {
                    OnlineMultiplayerManager.Instance?.ReportAuthoritativeAction(this,
                        OnlineActionType.AbilitySecondary, abilityDirection);
                }
            }
            aiAttack = false;
            aiRangedAttack = false;
            aiAbilitySlot = -1;
        }

        private void SimulateMovement(Vector3 direction, bool sprint, bool jumpPressed,
            Vector2 rawInput = default, bool useStrafing = false)
        {
            if (tigerRoarLaunching && Time.time >= tigerRoarLaunchUntil)
                CancelTigerRoarLaunch();
            bool beingLaunchedByTigerRoar = tigerRoarLaunching;
            if (IsRooted || beingLaunchedByTigerRoar)
            {
                direction = Vector3.zero;
                rawInput = Vector2.zero;
                sprint = false;
                jumpPressed = false;
            }
            if (direction.sqrMagnitude > 0.01f)
            {
                movementRequestedSinceStuckCheck = true;
                lastRequestedMoveDirection = direction.normalized;
            }
            if (hangingVine)
            {
                HandleHangingMovement(direction, jumpPressed);
                return;
            }
            // Strafing/backpedaling keeps the current facing and never benefits from sprint —
            // only genuine forward input (W) turns the body and can run. Flight steers freely.
            bool hasForwardInput = !useStrafing || IsFlying || rawInput.y > 0.05f;
            sprint = sprint && hasForwardInput;

            // Backpedaling/strafing covered as much ground per step as running forward, which
            // read as too large — those two axes are now scaled down independently before
            // being blended into a single world-space direction. Forward also no longer snaps
            // straight to full speed the instant W is pressed; it ramps up from a lower
            // starting fraction over ForwardAccelerationTime instead.
            if (useStrafing && !IsFlying) direction = ApplyDirectionalSpeedShaping(direction, rawInput);

            bool grounded = characterController.isGrounded;
            if (grounded && !wasGroundedForLandingSfx) CombatFeedback.PlayJump(transform.position);
            wasGroundedForLandingSfx = grounded;
            if (grounded && IsFlying && verticalVelocity.y < 0f) flyingUntil = 0f;
            bool gliding = IsFlying;
            bool abilityDashing = Time.time < abilityDashUntil;
            if (cowCharging && !abilityDashing) cowCharging = false;
            if (tigerPouncing && !abilityDashing) EndTigerPounce(null, transform.position);
            abilityDashing = Time.time < abilityDashUntil;
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

            // The tiger follows the full 3D center-camera ray, including its vertical
            // component. The cow remains a ground charge and only steers horizontally.
            if (abilityDashing && tigerPouncing)
            {
                UpdateTigerPounceGuidance();
            }
            else if (abilityDashing && cowCharging)
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
            if (isAiming) speed *= AimMovementSpeedMultiplier;
            if (Time.time < slowUntil) speed *= slowMultiplier;
            if (gliding) speed *= ServerGameTuning.EagleFlySpeedBonus;
            if (swimming) speed *= 0.72f;
            else if (wading && !(ForestMissionDirector.Instance != null && ForestMissionDirector.Instance.LakePassageOpen))
                speed *= waterMovementMultiplier;

            Vector3 horizontal = tigerPouncing
                ? new Vector3(tigerPounceVelocity.x, 0f, tigerPounceVelocity.z)
                : abilityDashing ? dashDirection * abilityDashSpeed : movementDirection * speed;
            Vector3 roarLaunchVelocity = Vector3.zero;
            if (beingLaunchedByTigerRoar)
            {
                Vector3 remaining = tigerRoarLaunchDestination - transform.position;
                remaining.y = 0f;
                float remainingDistance = remaining.magnitude;
                if (remainingDistance <= 0.08f)
                {
                    CancelTigerRoarLaunch();
                    beingLaunchedByTigerRoar = false;
                }
                else
                {
                    float step = Mathf.Min(remainingDistance,
                        tigerRoarLaunchSpeed * Time.deltaTime);
                    roarLaunchVelocity = remaining.normalized
                                         * (step / Mathf.Max(0.001f, Time.deltaTime));
                    horizontal = Vector3.zero;
                }
            }

            // Feed the effective horizontal speed to the visual instead of only the sprint
            // flag. The running pose reuses the walk cycle and speeds it up in proportion to
            // the distance covered, including modifiers such as aiming, slows and water.
            visualMotion?.SetLocomotion(direction.sqrMagnitude > 0.01f || abilityDashing,
                sprint || abilityDashing, !grounded, verticalVelocity.y, gliding, jumped,
                horizontal.magnitude, stats.MoveSpeed);
            if (abilityDashing && Time.time >= nextAbilityDamage)
            {
                nextAbilityDamage = Time.time + 0.24f;
                DamageArea(abilityRadius, abilityDamage, abilityKnockback, dashDirection, abilityColor, abilitySlow);
            }

            if (tigerPouncing)
            {
                verticalVelocity.y = tigerPounceVelocity.y;
            }
            else if (gliding)
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
            CollisionFlags collisionFlags =
                characterController.Move((horizontal + verticalVelocity + extraVelocity
                                          + roarLaunchVelocity) * Time.deltaTime);
            if (beingLaunchedByTigerRoar)
            {
                Vector3 remaining = tigerRoarLaunchDestination - transform.position;
                remaining.y = 0f;
                if (remaining.sqrMagnitude <= 0.08f * 0.08f
                    || (collisionFlags & CollisionFlags.Sides) != 0)
                    CancelTigerRoarLaunch();
            }
            if (tigerPouncing && Time.time >= tigerPounceStartedAt + TigerPounceCollisionGrace
                && collisionFlags != CollisionFlags.None)
            {
                EndTigerPounce(null, transform.position);
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!tigerPouncing || hit == null || hit.collider == null) return;

            Transform hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) return;

            Health target = hit.collider.GetComponentInParent<Health>();
            bool hitAnimal = target != null && target != health && !target.IsDead && target.Owner != null;
            if (!hitAnimal && Time.time < tigerPounceStartedAt + TigerPounceCollisionGrace) return;

            EndTigerPounce(hitAnimal ? target : null, hit.point);
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

        // Time to reach full forward speed from a standstill once W is held, and the fraction
        // of full speed available the instant it's first pressed (ramps linearly from there to
        // 1x). Strafe (A/D) and backpedal (S) are simple flat multipliers — no ramp requested
        // for those, just a smaller step.
        private const float ForwardAccelerationTime = 0.45f;
        private const float ForwardStartSpeedFraction = 0.45f;
        private const float LateralSpeedMultiplier = 0.5f;
        private const float BackwardSpeedMultiplier = 0.5f;
        // Precision-aim (RMB, nozes ammo) plants your feet more like a real sniper stance.
        private const float AimMovementSpeedMultiplier = 0.4f;
        private float forwardSpeedBlend;

        private Vector3 ApplyDirectionalSpeedShaping(Vector3 fallbackDirection, Vector2 rawInput)
        {
            bool forwardHeld = rawInput.y > 0.05f;
            forwardSpeedBlend = Mathf.MoveTowards(forwardSpeedBlend, forwardHeld ? 1f : 0f,
                Time.deltaTime / ForwardAccelerationTime);
            float forwardAccelMultiplier = Mathf.Lerp(ForwardStartSpeedFraction, 1f, forwardSpeedBlend);

            float forwardScale = rawInput.y >= 0f ? forwardAccelMultiplier : BackwardSpeedMultiplier;
            Vector2 scaledInput = new Vector2(rawInput.x * LateralSpeedMultiplier, rawInput.y * forwardScale);

            if (cameraTransform == null) return fallbackDirection;
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            return Vector3.ClampMagnitude(forward.normalized * scaledInput.y + right.normalized * scaledInput.x, 1f);
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

        public bool TryGetWeaponMuzzle(out Vector3 position, out Quaternion rotation)
        {
            if (visualMotion != null) return visualMotion.TryGetWeaponMuzzle(out position, out rotation);
            position = default;
            rotation = default;
            return false;
        }


        // Re-enabled now that the new animal models (weapon built into the hand/back mesh)
        // and their muzzle sockets are wired up (see AnimalVisualFactory.AttachWeaponMuzzle).
        private const bool RangedCombatEnabled = true;

        private bool TryRangedAttack(Vector3 direction)
        {
            if (!RangedCombatEnabled) return false;
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

        private bool BeginRangedReload(bool allowPartialMagazine = false)
        {
            int capacity = MagazineCapacityFor(CurrentWeaponAmmo);
            if (rangedReloading || SelectedReserveAmmo <= 0
                || SelectedMagazineAmmo >= capacity
                || (SelectedMagazineAmmo > 0 && !allowPartialMagazine)
                || RangedReserveAmmo <= 0)
                return false;
            rangedReloading = true;
            rangedReloadEndsAt = Time.time + ReloadSecondsForWeapon(CurrentWeaponAmmo);
            // Each ammo type uses the exact duration of its authored reload recording.
            // Keep playback local so simultaneous bot reloads do not clutter the player's mix.
            if (isLocalPlayer) CombatFeedback.PlayWeaponReload(transform.position, CurrentWeaponAmmo);
            return true;
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

        private bool TryUsePower(int slot, Vector3 direction)
        {
            if (BattleRoyaleManager.Instance != null && !BattleRoyaleManager.Instance.CombatEnabled) return false;
            if (slot < 0 || slot >= nextPowerTimes.Length || IsRooted) return false;
            if (stats.AbilityNames == null || slot >= stats.AbilityNames.Length
                || string.Equals(stats.AbilityNames[slot], "DESATIVADO",
                    System.StringComparison.OrdinalIgnoreCase)) return false;

            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            if (slot == 1)
            {
                if (Time.time < nextPowerTimes[slot] || !UseSecondaryAbility(direction)) return false;
                nextPowerTimes[slot] = Time.time + stats.AbilityCooldowns[slot];
                lastPowerSlot = slot;
                rangedReloading = false;
                rangedReloadEndsAt = 0f;
                CombatFeedback.PlayPower(animalType, slot, transform.position);
                AttackVfx.CreatePower(animalType, slot, transform.position, direction);
                visualMotion?.TriggerPower(slot);
                ForestMissionDirector.Instance?.NotifyAbilityUsed(this);
                return true;
            }

            if (slot != 0 || (vineLeaping && animalType != AnimalType.Monkey)) return false;

            // A second Q while charging ends the run early instead of queuing a new charge.
            if (animalType == AnimalType.Cow && cowCharging)
            {
                cowCharging = false;
                abilityDashUntil = Time.time;
                return true;
            }

            bool monkeyGrapple = animalType == AnimalType.Monkey;
            bool chainingVine = monkeyGrapple && (hangingVine || vineLeaping);
            if (chainingVine && !CanChainToAnotherVine) return false;
            if (!monkeyGrapple && Time.time < nextPowerTimes[0]) return false;
            if (!monkeyGrapple) nextPowerTimes[0] = Time.time + stats.AbilityCooldowns[0];
            lastPowerSlot = 0;
            CombatFeedback.PlayPower(animalType, 0, transform.position);
            AttackVfx.CreatePower(animalType, 0, transform.position, direction);
            visualMotion?.TriggerPower(0);
            if (!chainingVine) ForestMissionDirector.Instance?.NotifyAbilityUsed(this);
            UseAnimalAbility(direction);
            return true;
        }

        private bool UseSecondaryAbility(Vector3 direction)
        {
            switch (animalType)
            {
                case AnimalType.Tiger:
                    abilityHitTargets.Clear();
                    BattleRoyaleManager manager = BattleRoyaleManager.Instance;
                    if (manager != null)
                    {
                        foreach (ThirdPersonAnimalController fighter in manager.Fighters)
                        {
                            if (fighter != null) ApplyTigerRoarTo(fighter.Health);
                        }
                    }
                    else
                    {
                        int hitCount = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.7f,
                            TigerRoarRadius, combatHits, ~0, QueryTriggerInteraction.Ignore);
                        for (int i = 0; i < hitCount; i++)
                        {
                            Health target = combatHits[i] != null
                                ? combatHits[i].GetComponentInParent<Health>()
                                : null;
                            ApplyTigerRoarTo(target);
                        }
                    }
                    AttackVfx.CreateBurst(transform.position + Vector3.up * 0.65f,
                        new Color(1f, 0.45f, 0.08f), TigerRoarRadius);
                    return true;

                case AnimalType.Ant:
                    Vector3 acidPoint = FindSecondaryGroundTarget(direction, AntAcidMaximumRange);
                    AcidPoolEffect.Create(this, acidPoint, AntAcidRadius, 5f, 4f, 0.75f);
                    return true;

                case AnimalType.Eagle:
                    eagleVisionUntil = Time.time + 6f;
                    AttackVfx.CreateBurst(transform.position + Vector3.up,
                        new Color(1f, 0.85f, 0.2f), 4.5f);
                    return true;

                case AnimalType.Monkey:
                    return TryThrowVineSnare(direction);

                case AnimalType.Cow:
                    health.GrantTemporaryShield(30f, 6f);
                    AttackVfx.CreateBurst(transform.position + Vector3.up * 0.65f,
                        new Color(0.65f, 0.92f, 1f), 2.4f);
                    return true;

                default:
                    return false;
            }
        }

        private void ApplyTigerRoarTo(Health target)
        {
            if (target == null || target == health || target.IsDead || target.Owner == null
                || target.Owner.IsBurrowed || target.Owner.IsSpawnProtected
                || abilityHitTargets.Contains(target)) return;
            Vector3 push = target.transform.position - transform.position;
            push.y = 0f;
            if (push.sqrMagnitude > TigerRoarRadius * TigerRoarRadius) return;

            abilityHitTargets.Add(target);
            Vector3 launchDirection = push.sqrMagnitude > 0.01f
                ? push.normalized
                : transform.forward;
            Vector3 launchDestination = transform.position + launchDirection * TigerRoarRadius;
            launchDestination.y = target.transform.position.y;
            // Every target is carried to its corresponding point on the roar's border.
            // Therefore a target near the tiger crosses much more ground than one near the rim.
            target.Owner.ReceiveTigerRoarLaunch(launchDestination,
                TigerRoarLaunchDuration, TigerRoarUpwardSpeed);
            target.Owner.ApplySlow(0.65f, TigerRoarSlowDuration);
        }

        private Vector3 FindSecondaryGroundTarget(Vector3 direction, float maximumRange)
        {
            Vector3 origin = cameraTransform != null
                ? cameraTransform.position
                : transform.position + Vector3.up * (stats.ControllerHeight * 0.65f);
            float nearestDistance = maximumRange;
            Vector3 targetPoint = origin + direction * maximumRange;
            int hitCount = Physics.RaycastNonAlloc(new Ray(origin, direction), rangedAimHits,
                maximumRange, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = rangedAimHits[i];
                if (hit.collider == null) continue;
                Transform hitTransform = hit.collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
                if (hit.distance >= nearestDistance) continue;
                nearestDistance = hit.distance;
                targetPoint = hit.point;
            }

            if (jungle == null) jungle = FindAnyObjectByType<JungleGenerator>();
            if (jungle != null) targetPoint.y = jungle.GroundHeightAt(targetPoint);
            return targetPoint;
        }

        private bool TryThrowVineSnare(Vector3 direction)
        {
            Vector3 origin = cameraTransform != null
                ? cameraTransform.position
                : transform.position + Vector3.up * (stats.ControllerHeight * 0.65f);
            int hitCount = Physics.SphereCastNonAlloc(origin, 0.18f, direction, rangedAimHits,
                MonkeySnareMaximumRange, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit nearestHit = default;
            float nearestDistance = MonkeySnareMaximumRange + 1f;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = rangedAimHits[i];
                if (hit.collider == null) continue;
                Transform hitTransform = hit.collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
                if (hit.distance >= nearestDistance) continue;
                nearestHit = hit;
                nearestDistance = hit.distance;
            }

            if (nearestHit.collider == null) return false;
            Health target = nearestHit.collider.GetComponentInParent<Health>();
            if (target == null || target == health || target.IsDead || target.Owner == null
                || target.Owner.IsBurrowed || target.Owner.IsSpawnProtected) return false;

            VineSnareEffect.Create(transform, target.transform, target.Owner.Stats.ControllerHeight,
                MonkeySnareDuration);
            target.Owner.ApplyVineSnare(transform.position, MonkeySnareDuration,
                MonkeySnarePullDistance);
            AttackVfx.CreateHitSpark(nearestHit.point, new Color(0.25f, 0.82f, 0.08f));
            return true;
        }

        private void UseAnimalAbility(Vector3 direction)
        {
            abilityHitTargets.Clear();
            switch (animalType)
            {
                case AnimalType.Tiger:
                    // Bote guiado: enquanto estiver no ar, o destino acompanha continuamente
                    // o centro da mira em vez de ficar travado no ponto inicial.
                    BeginTigerPounce(direction);
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
                    // The center-camera ray can anchor to any surface or another animal;
                    // additional Q presses retarget the grapple without touching the ground.
                    bool wasHangingVine = hangingVine;
                    if (!VineAnchor.TryThrowVine(this, direction) && !wasHangingVine)
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

        /// <summary>Starts a guided 3D pounce. Its destination is recomputed from the
        /// center-camera aim every frame, so moving the reticle also redirects the tiger.</summary>
        private void BeginTigerPounce(Vector3 initialAimDirection)
        {
            tigerPounceFallbackAimDirection = initialAimDirection.sqrMagnitude > 0.001f
                ? initialAimDirection.normalized
                : transform.forward;
            Vector3 guidedDirection = GetTigerPounceAimDirection();
            guidedDirection = (guidedDirection + Vector3.up * TigerPounceLaunchLift).normalized;

            tigerPounceStartedAt = Time.time;
            tigerPouncing = true;
            tigerPounceVelocity = guidedDirection * ServerGameTuning.TigerLeapSpeed;

            Vector3 flatDirection = new Vector3(guidedDirection.x, 0f, guidedDirection.z);
            if (flatDirection.sqrMagnitude < 0.01f) flatDirection = transform.forward;
            BeginDash(flatDirection, TigerPounceMaxDuration, ServerGameTuning.TigerLeapSpeed,
                0f, 0f, 0f, 1f, new Color(1f, 0.5f, 0.12f));
            verticalVelocity.y = tigerPounceVelocity.y;
            extraVelocity = Vector3.zero;

            AttackVfx.CreateBurst(transform.position + Vector3.up * 0.2f,
                new Color(1f, 0.5f, 0.12f), 1.4f);
        }

        private void UpdateTigerPounceGuidance()
        {
            Vector3 desiredDirection = GetTigerPounceAimDirection();
            float launchBlend = Mathf.Clamp01(1f - (Time.time - tigerPounceStartedAt) / 0.28f);
            desiredDirection =
                (desiredDirection + Vector3.up * TigerPounceLaunchLift * launchBlend).normalized;

            Vector3 currentDirection = tigerPounceVelocity.sqrMagnitude > 0.001f
                ? tigerPounceVelocity.normalized
                : desiredDirection;
            float maxRadians = TigerPounceTurnRateDegreesPerSecond * Mathf.Deg2Rad * Time.deltaTime;
            tigerPounceVelocity = Vector3.RotateTowards(currentDirection, desiredDirection,
                maxRadians, 0f).normalized * ServerGameTuning.TigerLeapSpeed;

            Vector3 flatDirection = new Vector3(tigerPounceVelocity.x, 0f, tigerPounceVelocity.z);
            if (flatDirection.sqrMagnitude > 0.01f)
            {
                dashDirection = flatDirection.normalized;
                transform.rotation = Quaternion.LookRotation(dashDirection, Vector3.up);
            }
        }

        private Vector3 GetTigerPounceAimDirection()
        {
            Vector3 aimDirection = cameraTransform != null
                ? ViewAimDirection
                : tigerPounceFallbackAimDirection;
            if (aimDirection.sqrMagnitude < 0.001f) aimDirection = transform.forward;
            aimDirection.Normalize();

            Vector3 aimOrigin = cameraTransform != null
                ? cameraTransform.position
                : transform.position + Vector3.up * (stats.ControllerHeight * 0.5f);
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
            Vector3 tigerCenter = transform.position + Vector3.up * (stats.ControllerHeight * 0.5f);
            Vector3 direction = targetPoint - tigerCenter;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : aimDirection;
        }

        private void EndTigerPounce(Health hitAnimal, Vector3 impactPoint)
        {
            if (!tigerPouncing) return;

            tigerPouncing = false;
            abilityDashUntil = Time.time;
            tigerPounceVelocity = Vector3.zero;
            verticalVelocity.y = 0f;
            extraVelocity = Vector3.zero;
            visualMotion?.SetLocomotion(false, false, false);
            AttackVfx.CreateBurst(impactPoint + Vector3.up * 0.15f,
                new Color(1f, 0.5f, 0.12f), 1.6f);

            if (hitAnimal == null || hitAnimal == health || hitAnimal.IsDead
                || hitAnimal.Owner == null || hitAnimal.Owner.IsBurrowed
                || hitAnimal.Owner.IsSpawnProtected) return;

            Vector3 pushDirection = hitAnimal.transform.position - transform.position;
            pushDirection.y = 0f;
            if (pushDirection.sqrMagnitude < 0.01f) pushDirection = transform.forward;
            else pushDirection.Normalize();

            hitAnimal.TakeDamage(ServerGameTuning.TigerLeapDamage, this);
            CombatFeedback.NotifyHit(AnimalType.Tiger, hitAnimal.transform.position,
                ServerGameTuning.TigerLeapDamage);
            hitAnimal.Owner.ReceiveKnockback((pushDirection + Vector3.up * 0.18f).normalized
                * ServerGameTuning.TigerLeapKnockback);
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

        public void ApplyVineSnare(Vector3 pullOrigin, float duration, float pullDistance)
        {
            if (defeated || health == null || health.IsDead || IsSpawnProtected) return;

            rootedUntil = Mathf.Max(rootedUntil, Time.time + Mathf.Max(0f, duration));
            rangedReloading = false;
            rangedReloadEndsAt = 0f;

            // A successful combat vine interrupts every movement ability immediately.
            CancelTigerRoarLaunch();
            tigerPouncing = false;
            tigerPounceVelocity = Vector3.zero;
            flyingUntil = 0f;
            abilityDashUntil = Time.time;
            cowCharging = false;
            vineLeaping = false;
            hangingVine = false;
            VineAnchor.DestroyThrown(heldVine);
            heldVine = null;
            targetVine = null;
            visualMotion?.SetVineHanging(false);
            if (characterController != null) characterController.enabled = true;

            Vector3 pullDirection = pullOrigin - transform.position;
            pullDirection.y = 0f;
            if (pullDirection.sqrMagnitude > 0.01f)
            {
                // extraVelocity decays at 4.5/s in SimulateMovement. Multiplying by that
                // decay rate converts the requested distance into a short collision-aware pull.
                extraVelocity = pullDirection.normalized * (Mathf.Max(0f, pullDistance) * 4.5f);
            }
            verticalVelocity.y = Mathf.Min(verticalVelocity.y, 0f);
        }

        public void ReceiveKnockback(Vector3 force) => extraVelocity += force;

        public void ReceiveLaunch(Vector3 direction, float horizontalSpeed, float upwardSpeed)
        {
            if (defeated || health == null || health.IsDead) return;
            CancelTigerRoarLaunch();
            Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z).normalized;
            if (flatDirection.sqrMagnitude < 0.01f) flatDirection = transform.forward;

            tigerPouncing = false;
            tigerPounceVelocity = Vector3.zero;
            flyingUntil = 0f;
            abilityDashUntil = 0f;
            cowCharging = false;
            vineLeaping = false;
            hangingVine = false;
            VineAnchor.DestroyThrown(heldVine);
            heldVine = null;
            targetVine = null;
            visualMotion?.SetVineHanging(false);
            if (characterController != null) characterController.enabled = true;
            verticalVelocity.y = Mathf.Max(verticalVelocity.y, upwardSpeed);
            extraVelocity = flatDirection * horizontalSpeed;
        }

        public void ReceiveTigerRoarLaunch(Vector3 destination, float duration, float upwardSpeed)
        {
            if (defeated || health == null || health.IsDead || IsSpawnProtected) return;
            Vector3 remaining = destination - transform.position;
            remaining.y = 0f;
            float travelDistance = remaining.magnitude;
            if (travelDistance <= 0.08f) return;

            // Reuse the regular launch preparation to interrupt every movement ability,
            // then replace its approximate impulse with an exact border destination.
            ReceiveLaunch(remaining.normalized, 0f, upwardSpeed);
            float safeDuration = Mathf.Max(0.1f, duration);
            tigerRoarLaunchDestination = destination;
            tigerRoarLaunchDestination.y = transform.position.y;
            tigerRoarLaunchSpeed = travelDistance / safeDuration;
            tigerRoarLaunchUntil = Time.time + safeDuration + 0.2f;
            tigerRoarLaunching = true;
        }

        private void CancelTigerRoarLaunch()
        {
            tigerRoarLaunching = false;
            tigerRoarLaunchSpeed = 0f;
            tigerRoarLaunchUntil = 0f;
        }

        /// <summary>Fills the reserve for whichever ammo type the pickup was, regardless of
        /// what's currently equipped — matches a dedicated per-type pickup instead of the old
        /// "refills whatever you're holding" generic supply.</summary>
        public bool TryRefillRangedAmmo(WeaponAmmoType type, int amount)
        {
            if (defeated || health == null || health.IsDead || amount <= 0) return false;
            int max = MaxAmmoFor(type);
            int current = weaponReserveAmmo[(int)type];
            if (current >= max) return false;
            bool currentSelectionEmpty = SelectedReserveAmmo <= 0;
            weaponReserveAmmo[(int)type] = Mathf.Min(max, current + amount);

            // If the equipped type is empty, collecting another type equips it immediately
            // so the newly found ammunition is useful without a separate 1/2/3 key press.
            if (currentSelectionEmpty && type != CurrentWeaponAmmo)
            {
                selectedWeapon = type;
                rangedReloading = false;
                rangedReloadEndsAt = 0f;
            }
            if (type == CurrentWeaponAmmo && SelectedMagazineAmmo <= 0) BeginRangedReload();
            AttackVfx.CreateBurst(transform.position + Vector3.up * 0.6f, new Color(1f, 0.76f, 0.18f), 1.25f);
            return true;
        }

        public void RestoreMobilityEnergy(float amount) { }

        public bool IsHoldingVine(Transform vine) => hangingVine && heldVine == vine;

        public bool TryGrabVine(Transform vine)
        {
            if (animalType != AnimalType.Monkey || vine == null || defeated || hangingVine || vineLeaping) return false;
            if (!VineAnchor.IsWithinUseRange(this, vine)) return false;
            return BeginVineLeap(vine);
        }

        public bool TryLaunchToVine(Transform vine)
        {
            if (vine == null || vine == heldVine || vine == targetVine
                || !CanChainToAnotherVine) return false;
            if (!VineAnchor.IsWithinUseRange(this, vine)) return false;
            return BeginVineLeap(vine);
        }

        private bool BeginVineLeap(Transform vine)
        {
            if (vine == null) return false;
            bool retargetingDuringLeap = vineLeaping;
            Transform departingVine = retargetingDuringLeap ? targetVine : heldVine;
            Vector3 carriedVelocity = retargetingDuringLeap
                ? Vector3.ClampMagnitude(vineLeapVelocity, VineLeapSpeed)
                : Vector3.zero;

            targetVine = vine;
            vineLeapStart = transform.position;
            VineAnchor vineAnchor = vine.GetComponent<VineAnchor>();
            vineLeapEnd = GetVinePullDestination(vineAnchor, vine.position);
            float distance = Vector3.Distance(vineLeapStart, vineLeapEnd);
            float duration = Mathf.Clamp(distance / VineLeapSpeed, 0.2f, 2.4f);
            vineLeapStartedAt = Time.time;
            vineLeapEndsAt = Time.time + duration;
            vineLeapSideOffset = Vector3.zero;
            previousVineLeapPosition = transform.position;
            Vector3 directVelocity = (vineLeapEnd - vineLeapStart) / duration;
            vineLeapStartVelocity = retargetingDuringLeap ? carriedVelocity : directVelocity;
            vineLeapVelocity = vineLeapStartVelocity;
            vineLeaping = true;
            hangingVine = false;
            // Only tear down the old strand after the new one is valid and owns the target.
            // This makes a missed Q harmless and a successful Q look like an instantaneous
            // web-to-web handoff instead of briefly leaving the monkey without a grapple.
            VineAnchor.DestroyThrown(departingVine);
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
            visualMotion?.SetVineHanging(true, ShouldGrabWithLeftHand(vine));
            return true;
        }

        private Vector3 GetVinePullDestination(VineAnchor vineAnchor, Vector3 fallbackPoint)
        {
            Vector3 attachmentPoint = vineAnchor != null ? vineAnchor.AttachmentPosition : fallbackPoint;
            Vector3 surfaceNormal = vineAnchor != null ? vineAnchor.AttachmentNormal : Vector3.up;
            if (surfaceNormal.sqrMagnitude < 0.01f) surfaceNormal = Vector3.up;
            // Stop outside the hit collider so reenabling CharacterController cannot eject
            // the monkey sideways from an overlapping wall, tree or another animal.
            float surfaceClearance = stats.ControllerRadius + 0.22f;
            float verticalDrop = stats.ControllerHeight * 0.32f;
            return attachmentPoint + surfaceNormal.normalized * surfaceClearance
                   - Vector3.up * verticalDrop;
        }

        private void HandleVineLeap()
        {
            if (targetVine == null)
            {
                vineLeaping = false;
                if (characterController != null) characterController.enabled = true;
                visualMotion?.SetVineHanging(false);
                return;
            }

            float duration = Mathf.Max(0.01f, vineLeapEndsAt - vineLeapStartedAt);
            float progress = Mathf.Clamp01((Time.time - vineLeapStartedAt) / duration);
            VineAnchor vineAnchor = targetVine.GetComponent<VineAnchor>();
            vineLeapEnd = GetVinePullDestination(vineAnchor, targetVine.position);

            // A/D bends the route sideways while the grapple continues pulling toward its
            // attachment. The offset fades back into the destination near the end so the
            // monkey still reaches the aimed point instead of missing the surface.
            if (isLocalPlayer)
            {
                float lateralInput = GameInput.ReadMovement().x;
                Vector3 cameraRight = cameraTransform != null ? cameraTransform.right : transform.right;
                cameraRight.y = 0f;
                if (cameraRight.sqrMagnitude > 0.01f)
                {
                    vineLeapSideOffset += cameraRight.normalized * lateralInput
                                          * VineSideControlSpeed * Time.deltaTime;
                    vineLeapSideOffset = Vector3.ClampMagnitude(vineLeapSideOffset,
                        VineMaximumSideOffset);
                }
            }

            float sideInfluence = Mathf.Sin(progress * Mathf.PI);
            Vector3 directVelocity = (vineLeapEnd - vineLeapStart) / duration;
            // Blend the velocity carried from the previous strand into the new route. The
            // correction is strongest at release and fades completely at the destination,
            // preventing a sharp direction snap when Q switches grapple points mid-flight.
            Vector3 velocityCorrection = Vector3.ClampMagnitude(
                (vineLeapStartVelocity - directVelocity) * duration,
                VineRetargetMomentumCorrection);
            float velocityInfluence = progress * (1f - progress) * (1f - progress);
            Vector3 position = Vector3.Lerp(vineLeapStart, vineLeapEnd, progress)
                               + velocityCorrection * velocityInfluence
                               + vineLeapSideOffset * sideInfluence;
            vineLeapVelocity = (position - previousVineLeapPosition)
                               / Mathf.Max(0.001f, Time.deltaTime);
            previousVineLeapPosition = position;
            transform.position = position;
            vineAnchor?.SetGrappleVisualEndPoint(transform.position
                + Vector3.up * (stats.ControllerHeight * 0.82f));
            visualMotion?.SetHandAimTarget(vineAnchor != null
                ? vineAnchor.AttachmentPosition
                : targetVine.position);
            visualMotion?.SetLocomotion(true, true, true);
            if (progress < 1f) return;

            transform.position = vineLeapEnd;
            Vector3 exitHorizontal = Vector3.ClampMagnitude(
                Vector3.ProjectOnPlane(vineLeapVelocity, Vector3.up), 6f);
            verticalVelocity.y = Mathf.Clamp(vineLeapVelocity.y, -2f, 4f);
            extraVelocity = exitHorizontal;
            VineAnchor.DestroyThrown(targetVine);
            heldVine = null;
            targetVine = null;
            vineLeaping = false;
            hangingVine = false;
            if (characterController != null) characterController.enabled = true;
            AttackVfx.CreateBurst(transform.position + Vector3.up * 0.4f,
                new Color(0.55f, 0.85f, 0.3f), 0.8f);
            visualMotion?.SetVineHanging(false);
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
            VineAnchor.DestroyThrown(heldVine);
            heldVine = null;
            targetVine = null;
            visualMotion?.SetVineHanging(false);
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            Vector3 forward = flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;
            Vector3 carriedHorizontal = Vector3.ClampMagnitude(
                Vector3.ProjectOnPlane(carriedSwingVelocity, Vector3.up), 18f);
            verticalVelocity.y = VineLaunchUp + Mathf.Clamp(carriedSwingVelocity.y * 0.35f, 0f, 6f);
            extraVelocity += forward * VineLaunchForward
                             + carriedHorizontal * 0.8f;
            AttackVfx.CreateBurst(transform.position + Vector3.up * 0.6f, new Color(0.5f, 0.9f, 0.35f), 1.2f);
            return true;
        }

        public void TeleportTo(Vector3 worldPosition)
        {
            CancelTigerRoarLaunch();
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
            ClearSecondaryAim();
            CancelTigerRoarLaunch();
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
            ClearSecondaryAim();
            CancelTigerRoarLaunch();
            defeated = false;
            tigerPouncing = false;
            tigerPounceVelocity = Vector3.zero;
            abilityDashUntil = 0f;
            cowCharging = false;
            rootedUntil = 0f;
            eagleVisionUntil = 0f;
            flyingUntil = 0f;
            vineLeaping = false;
            hangingVine = false;
            VineAnchor.DestroyThrown(heldVine);
            heldVine = null;
            targetVine = null;
            visualMotion?.SetVineHanging(false);
            if (characterController != null) characterController.enabled = true;
            burrowed = false;
            burrowEntering = false;
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
            selectedWeapon = WeaponAmmoType.Tomato;
            LoadStartingMagazines();
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
            ClearSecondaryAim();
            CancelTigerRoarLaunch();
            defeated = true;
            eliminated = true;
            rootedUntil = 0f;
            eagleVisionUntil = 0f;
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

        private void ClearSecondaryAim()
        {
            isAiming = false;
            if (cameraTransform != null)
                cameraTransform.GetComponent<ThirdPersonCamera>()?.SetAiming(false);
        }

        private void OnDestroy()
        {
            if (Application.isPlaying) BattleRoyaleManager.Instance?.HandleFighterDisconnected(this);
        }
    }
}
