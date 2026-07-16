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

        private const int MaxRangedAmmo = 120;
        private const int RangedMagazineCapacity = 30;
        private const float RangedReloadSeconds = 2f;
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
        private int rangedAmmo = MaxRangedAmmo;
        private int rangedMagazineAmmo = RangedMagazineCapacity;
        private bool rangedReloading;
        private float rangedReloadEndsAt;
        private int lastPowerSlot = -1;
        private bool defeated;
        private bool eliminated;
        private int livesRemaining = MaxLives;
        private float spawnProtectedUntil;
        private float tigerLeapUntil;
        private Vector3 tigerLeapDirection;
        private float flyingUntil;
        private Vector3 eagleGlideDirection;
        private Transform heldVine;
        private Transform targetVine;
        private bool hangingVine;
        private bool vineLeaping;
        private int vinesVisitedInChain;
        private float vineLeapStartedAt;
        private float vineLeapEndsAt;
        private float vineLeapArcHeight;
        private Vector3 vineLeapStart;
        private Vector3 vineLeapEnd;
        private bool burrowed;
        private bool networkProxy;
        private Vector3 networkTargetPosition;
        private Quaternion networkTargetRotation;
        private Vector3 lastNetworkVisualPosition;

        public const int MaxLives = 3;
        private const float SpawnProtectionSeconds = 2.5f;

        private const float TigerLeapDuration = 0.72f;
        private const float TigerLeapSpeed = 19.5f;
        private const float TigerLeapUpSpeed = 7.6f;
        private const float TigerLeapHitRadius = 1.25f;
        private const float TigerLeapDamage = 28f;
        private const float TigerLeapKnockback = 12f;
        private const float AntThrowRange = 4.8f;
        private const float AntThrowHorizontalSpeed = 30f;
        private const float AntThrowUpSpeed = 9.5f;
        private const float EagleFlightDuration = 5f;
        private const float EagleJumpSpeed = 8.2f;
        private const float EagleFlySpeedBonus = 1.35f;
        private const float EagleGlideGravityMultiplier = 0.18f;
        private const float EagleMaximumFallSpeed = -2.4f;
        public const int MaxVinesPerChain = 5;
        private const float VineLeapSpeed = 22f;
        private const float VineLaunchForward = 15f;
        private const float VineLaunchUp = 9f;

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
        public int VinesVisitedInChain => vinesVisitedInChain;
        public bool CanChainToAnotherVine => hangingVine && vinesVisitedInChain < MaxVinesPerChain;
        public bool IsWading => CentralLake.Instance != null && CentralLake.Instance.Contains(transform.position);
        public bool IsSwimming => IsWading && transform.position.y < CentralLake.Instance.SurfaceHeight - stats.ControllerHeight * 0.35f;
        public float TunnelSecondsRemaining => AntTunnelEntrance.SecondsRemaining(this);
        public bool UsesMobilityEnergy => false;
        public float MobilityEnergy => 0f;
        public float MaxMobilityEnergyValue => 0f;
        public string MobilityEnergyName => string.Empty;
        public bool NeedsMobilityEnergy => false;
        public bool IsMobilityRecharging => false;
        public float MobilityRechargeSecondsRemaining => 0f;
        public int RangedAmmo => rangedAmmo;
        public int RangedMagazineAmmo => rangedMagazineAmmo;
        public int RangedMagazineCapacityValue => RangedMagazineCapacity;
        public int RangedReserveAmmo => Mathf.Max(0, rangedAmmo - rangedMagazineAmmo);
        public bool IsRangedReloading => rangedReloading;
        public float RangedReloadSecondsRemaining => rangedReloading
            ? Mathf.Max(0f, rangedReloadEndsAt - Time.time)
            : 0f;
        public int MaxRangedAmmoValue => MaxRangedAmmo;
        public bool NeedsRangedAmmo => rangedAmmo < MaxRangedAmmo;
        public RangedSupplyKind CompatibleRangedSupply => RangedSupplyKind.NaturalAmmo;
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

        // Every animal now fires seeds from the seed-launcher mounted on its back.
        public string RangedAmmoName => "SEMENTES";

        public string RangedAttackName => "RAJADA DE SEMENTES";

        public string LastPowerName => Stats.AbilityNames != null && lastPowerSlot >= 0 && lastPowerSlot < Stats.AbilityNames.Length
            ? Stats.AbilityNames[lastPowerSlot] : "Ataque base";

        public string BasicActionName => animalType switch
        {
            AnimalType.Tiger => "Patada de Garras",
            AnimalType.Ant => "Mordida de Mandíbula",
            AnimalType.Eagle => "Bicada",
            AnimalType.Monkey => "Tapa Selvagem",
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
            if (BattleRoyaleManager.Instance != null && BattleRoyaleManager.Instance.MatchFinished)
            {
                visualMotion?.SetLocomotion(false, false, false);
                return;
            }
            if (burrowed) { HandleBurrowed(); return; }
            RecoverIfBelowTerrain();
            if (isLocalPlayer) HandleLocalInput();
            else HandleAIInput();
        }

        private void HandleBurrowed()
        {
            Vector3 moveDir = isLocalPlayer ? GetCameraRelativeDirection(GameInput.ReadMovement()) : aiMoveDirection;
            AntTunnelEntrance.Navigate(this, moveDir);
            AntTunnelEntrance.Tick(this);
            if (!AntTunnelEntrance.IsTraveling(this)) SurfaceFromBurrow();
        }

        private void EnterBurrow()
        {
            burrowed = true;
            verticalVelocity = Vector3.zero;
            extraVelocity = Vector3.zero;
            if (characterController != null) characterController.enabled = false;
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
            int replicatedAmmo, int replicatedMagazineAmmo, bool replicatedEliminated, bool applyTransform)
        {
            livesRemaining = Mathf.Clamp(replicatedLives, 0, MaxLives);
            rangedAmmo = Mathf.Clamp(replicatedAmmo, 0, MaxRangedAmmo);
            rangedMagazineAmmo = Mathf.Clamp(replicatedMagazineAmmo, 0, Mathf.Min(RangedMagazineCapacity, rangedAmmo));
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
                    if (!RangedAmmoPickup.TryCollectNearest(this)) LifePickup.TryConsumeNearest(this);
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
            rangedAmmo = MaxRangedAmmo;
            rangedMagazineAmmo = RangedMagazineCapacity;
            rangedReloading = false;
            rangedReloadEndsAt = 0f;
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
            Vector3 safePosition = jungle.GetGroundPosition(transform.position);
            if (transform.position.y < safePosition.y - 0.5f) SnapToTerrain(jungle);
        }

        private void HandleLocalInput()
        {
            Vector3 movement = GetCameraRelativeDirection(GameInput.ReadMovement());
            SimulateMovement(movement, GameInput.SprintHeld(), GameInput.JumpPressed());
            bool aerialAutoFire = animalType == AnimalType.Eagle && IsFlying && GameInput.RangedAttackHeld();
            if (GameInput.RangedAttackPressed() || aerialAutoFire)
            {
                Vector3 direction = GetRangedAttackDirection(movement);
                OnlineMultiplayerManager.Instance?.ReportAction(OnlineActionType.RangedAttack, direction);
                TryRangedAttack(direction);
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
                    if (!RangedAmmoPickup.TryCollectNearest(this)) LifePickup.TryConsumeNearest(this);
                }
            }
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

        private void SimulateMovement(Vector3 direction, bool sprint, bool jumpPressed)
        {
            if (hangingVine)
            {
                HandleHangingMovement(jumpPressed);
                return;
            }
            bool grounded = characterController.isGrounded;
            bool tigerLeaping = Time.time < tigerLeapUntil;
            if (grounded && tigerLeaping && verticalVelocity.y < 0f)
            {
                tigerLeapUntil = 0f;
                tigerLeaping = false;
            }
            if (grounded && IsFlying && verticalVelocity.y < 0f) flyingUntil = 0f;
            bool gliding = IsFlying;
            bool abilityDashing = Time.time < abilityDashUntil;
            visualMotion?.SetLocomotion(direction.sqrMagnitude > 0.01f || abilityDashing || tigerLeaping,
                sprint || abilityDashing || tigerLeaping, !grounded, verticalVelocity.y, gliding);
            if (grounded && verticalVelocity.y < 0f && !gliding) verticalVelocity.y = -2f;
            bool jumped = jumpPressed && grounded && !gliding && !tigerLeaping;
            if (jumped)
            {
                verticalVelocity.y = stats.JumpForce;
                CombatFeedback.PlayJump(transform.position);
            }

            bool movingOnGround = grounded && !jumped && !gliding && !tigerLeaping
                && (direction.sqrMagnitude > 0.01f || abilityDashing);
            if (movingOnGround && Time.time >= nextFootstepTime)
            {
                nextFootstepTime = Time.time + (sprint || abilityDashing ? 0.3f : 0.43f);
                CombatFeedback.PlayFootstep(transform.position);
            }

            Vector3 movementDirection = direction;
            if (gliding && movementDirection.sqrMagnitude <= 0.01f) movementDirection = eagleGlideDirection;

            if (movementDirection.sqrMagnitude > 0.01f && !abilityDashing && !tigerLeaping)
            {
                Quaternion targetRotation = Quaternion.LookRotation(movementDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            float speed = sprint ? stats.SprintSpeed : stats.MoveSpeed;
            if (Time.time < slowUntil) speed *= slowMultiplier;
            if (gliding) speed *= EagleFlySpeedBonus;
            if (IsSwimming) speed *= 0.72f;
            else if (IsWading && !(ForestMissionDirector.Instance != null && ForestMissionDirector.Instance.LakePassageOpen))
                speed *= CentralLake.Instance.MovementMultiplier;

            Vector3 horizontal = abilityDashing ? dashDirection * abilityDashSpeed
                : tigerLeaping ? tigerLeapDirection * TigerLeapSpeed
                : movementDirection * speed;
            if (abilityDashing && Time.time >= nextAbilityDamage)
            {
                nextAbilityDamage = Time.time + 0.24f;
                DamageArea(abilityRadius, abilityDamage, abilityKnockback, dashDirection, abilityColor, abilitySlow);
            }

            if (gliding)
            {
                // Salto longo: sobe uma vez e plana suavemente, sem subida manual.
                verticalVelocity.y = Mathf.Max(EagleMaximumFallSpeed,
                    verticalVelocity.y + gravity * EagleGlideGravityMultiplier * Time.deltaTime);
            }
            else if (IsSwimming)
            {
                float surfaceTarget = CentralLake.Instance.SurfaceHeight - stats.ControllerHeight * 0.55f;
                float lift = Mathf.Clamp((surfaceTarget - transform.position.y) * 4.5f, -3f, 7f);
                verticalVelocity.y = Mathf.MoveTowards(verticalVelocity.y, lift, 30f * Time.deltaTime);
            }
            else verticalVelocity.y += gravity * Time.deltaTime;

            extraVelocity = Vector3.Lerp(extraVelocity, Vector3.zero, 4.5f * Time.deltaTime);
            characterController.Move((horizontal + verticalVelocity + extraVelocity) * Time.deltaTime);
            if (tigerLeaping) DamageTigerLeapTargets();
        }

        private void HandleHangingMovement(bool releasePressed)
        {
            if (heldVine == null) { hangingVine = false; return; }
            visualMotion?.SetLocomotion(false, false, true);
            verticalVelocity = Vector3.zero;
            extraVelocity = Vector3.zero;

            if (releasePressed)
            {
                TryLaunchFromVine(ViewAimDirection);
                return;
            }

            // Hold the grip point smoothly and face the aim direction.
            Vector3 target = heldVine.position - Vector3.up * (stats.ControllerHeight * 0.82f);
            Vector3 delta = target - transform.position;
            if (delta.sqrMagnitude > 0.0001f) characterController.Move(delta * Mathf.Clamp01(12f * Time.deltaTime));
            Vector3 aimFlat = new Vector3(ViewAimDirection.x, 0f, ViewAimDirection.z);
            if (aimFlat.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(aimFlat.normalized, Vector3.up), rotationSpeed * Time.deltaTime);
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

        private void TryRangedAttack(Vector3 direction)
        {
            if (BattleRoyaleManager.Instance != null && !BattleRoyaleManager.Instance.CombatEnabled) return;
            UpdateRangedReload();
            if (rangedReloading || rangedAmmo <= 0) return;
            if (rangedMagazineAmmo <= 0)
            {
                BeginRangedReload();
                return;
            }
            if (Time.time < nextBasicAttackTime) return;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            rangedAmmo--;
            rangedMagazineAmmo--;
            nextBasicAttackTime = Time.time + RangedAttackCooldown();
            Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);
            if (flatDirection.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            visualMotion?.TriggerAttack(false);
            RangedProjectile.Fire(this, direction);
            if (rangedMagazineAmmo <= 0 && rangedAmmo > 0) BeginRangedReload();
        }

        private void BeginRangedReload()
        {
            if (rangedReloading || rangedMagazineAmmo > 0 || rangedAmmo <= 0) return;
            rangedReloading = true;
            rangedReloadEndsAt = Time.time + RangedReloadSeconds;
        }

        private void UpdateRangedReload()
        {
            if (!rangedReloading || Time.time < rangedReloadEndsAt) return;
            rangedReloading = false;
            rangedReloadEndsAt = 0f;
            rangedMagazineAmmo = Mathf.Min(RangedMagazineCapacity, rangedAmmo);
        }

        // Seed-launcher fire cadence: medium machine-gun (~7 shots/s). Lighter animals spray a touch faster.
        private float RangedAttackCooldown() => animalType switch
        {
            AnimalType.Ant => 0.12f,
            AnimalType.Monkey => 0.13f,
            AnimalType.Eagle => 0.14f,
            AnimalType.Tiger => 0.15f,
            _ => 0.14f
        };

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
            AnimalType.Monkey => 9f,
            AnimalType.Eagle => 8f,
            AnimalType.Ant => 6f,
            _ => 8f
        };

        private void TryUsePower(int slot, Vector3 direction)
        {
            if (BattleRoyaleManager.Instance != null && !BattleRoyaleManager.Instance.CombatEnabled) return;
            if (slot != 0 || vineLeaping) return;
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
                    // Pulo longo e rápido: arco baixo com forte impulso horizontal.
                    tigerLeapDirection = new Vector3(direction.x, 0f, direction.z).normalized;
                    if (tigerLeapDirection.sqrMagnitude < 0.01f) tigerLeapDirection = transform.forward;
                    transform.rotation = Quaternion.LookRotation(tigerLeapDirection, Vector3.up);
                    tigerLeapUntil = Time.time + TigerLeapDuration;
                    verticalVelocity.y = Mathf.Max(verticalVelocity.y, TigerLeapUpSpeed);
                    AttackVfx.CreateBurst(transform.position + Vector3.up * 0.2f, new Color(1f, 0.5f, 0.12f), 1.4f);
                    break;
                case AnimalType.Ant:
                    // Enter a nearby anthill; without one, throw the aimed nearby enemy away.
                    if (AntTunnelEntrance.TryEnter(this)) EnterBurrow();
                    else TryThrowNearestEnemy(direction);
                    break;
                case AnimalType.Eagle:
                    // Salto longo com planeio: impulso único e duração máxima de cinco segundos.
                    flyingUntil = Time.time + EagleFlightDuration;
                    eagleGlideDirection = new Vector3(direction.x, 0f, direction.z).normalized;
                    if (eagleGlideDirection.sqrMagnitude < 0.01f) eagleGlideDirection = transform.forward;
                    verticalVelocity.y = Mathf.Max(verticalVelocity.y, EagleJumpSpeed);
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

        private bool TryThrowNearestEnemy(Vector3 direction)
        {
            Vector3 aimDirection = new Vector3(direction.x, 0f, direction.z).normalized;
            if (aimDirection.sqrMagnitude < 0.01f) aimDirection = transform.forward;

            Health target = null;
            float bestScore = float.MaxValue;
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.7f,
                AntThrowRange, combatHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Health candidate = combatHits[i].GetComponentInParent<Health>();
                if (candidate == null || candidate == health || candidate.IsDead || candidate.Owner == null) continue;
                if (candidate.Owner.IsBurrowed || candidate.Owner.IsSpawnProtected) continue;

                Vector3 offset = candidate.transform.position - transform.position;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance <= 0.01f) continue;
                float aimDot = Vector3.Dot(aimDirection, offset / distance);
                if (aimDot < 0.15f) continue;
                float score = distance - aimDot * 1.6f;
                if (score >= bestScore) continue;
                bestScore = score;
                target = candidate;
            }

            if (target == null) return false;
            Vector3 throwDirection = target.transform.position - transform.position;
            throwDirection.y = 0f;
            if (throwDirection.sqrMagnitude < 0.01f) throwDirection = aimDirection;
            else throwDirection.Normalize();
            transform.rotation = Quaternion.LookRotation(throwDirection, Vector3.up);
            target.Owner.ReceiveLaunch(throwDirection, AntThrowHorizontalSpeed, AntThrowUpSpeed);
            AttackVfx.CreateHitSpark(target.transform.position + Vector3.up * 0.7f, new Color(0.86f, 0.36f, 0.14f));
            CombatFeedback.PlayPlayerHit(target.transform.position);
            return true;
        }

        private void DamageTigerLeapTargets()
        {
            Vector3 impactCenter = transform.position + Vector3.up * (stats.ControllerHeight * 0.5f);
            int hitCount = Physics.OverlapSphereNonAlloc(impactCenter, TigerLeapHitRadius,
                combatHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Health target = combatHits[i].GetComponentInParent<Health>();
                if (target == null || target == health || target.IsDead || target.Owner == null) continue;
                if (abilityHitTargets.Contains(target) || target.Owner.IsBurrowed || target.Owner.IsSpawnProtected) continue;
                abilityHitTargets.Add(target);

                Vector3 impactPosition = target.transform.position;
                Vector3 pushDirection = impactPosition - transform.position;
                pushDirection.y = 0f;
                if (pushDirection.sqrMagnitude < 0.01f) pushDirection = tigerLeapDirection;
                else pushDirection.Normalize();

                float healthBeforeHit = target.CurrentHealth;
                target.TakeDamage(TigerLeapDamage, this);
                CombatFeedback.NotifyHit(AnimalType.Tiger, impactPosition, TigerLeapDamage);
                if (!target.IsDead && target.CurrentHealth <= healthBeforeHit)
                {
                    target.Owner.ReceiveKnockback((pushDirection + Vector3.up * 0.18f).normalized * TigerLeapKnockback);
                }
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
                if (other == null || other == health || other.IsDead) continue;
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
                if (other == null || other == health || other.IsDead || abilityHitTargets.Contains(other)) continue;
                abilityHitTargets.Add(other);
                other.TakeDamage(damage, this);
                CombatFeedback.NotifyHit(animalType, other.transform.position, damage);
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

            tigerLeapUntil = 0f;
            flyingUntil = 0f;
            abilityDashUntil = 0f;
            vineLeaping = false;
            hangingVine = false;
            heldVine = null;
            targetVine = null;
            vinesVisitedInChain = 0;
            if (characterController != null) characterController.enabled = true;
            verticalVelocity.y = Mathf.Max(verticalVelocity.y, upwardSpeed);
            extraVelocity = flatDirection * horizontalSpeed;
        }

        public bool TryRefillRangedAmmo(RangedSupplyKind supplyKind, int amount)
        {
            if (defeated || health == null || health.IsDead || supplyKind != CompatibleRangedSupply || amount <= 0 || !NeedsRangedAmmo) return false;
            rangedAmmo = Mathf.Min(MaxRangedAmmo, rangedAmmo + amount);
            if (rangedMagazineAmmo <= 0) BeginRangedReload();
            AttackVfx.CreateBurst(transform.position + Vector3.up * 0.6f, new Color(1f, 0.76f, 0.18f), 1.25f);
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
        }

        public bool TryLaunchFromVine(Vector3 direction)
        {
            if (!hangingVine) return false;
            hangingVine = false;
            heldVine = null;
            targetVine = null;
            vinesVisitedInChain = 0;
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            Vector3 forward = flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;
            verticalVelocity.y = VineLaunchUp;
            extraVelocity += forward * VineLaunchForward;
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

        /// <summary>Revive at a fresh position with brief spawn protection (keeps the same GameObject).</summary>
        public void Respawn(Vector3 position)
        {
            if (eliminated) return;
            EnsureRuntimeReferences();
            defeated = false;
            tigerLeapUntil = 0f;
            flyingUntil = 0f;
            vineLeaping = false;
            hangingVine = false;
            heldVine = null;
            targetVine = null;
            vinesVisitedInChain = 0;
            if (characterController != null) characterController.enabled = true;
            burrowed = false;
            AntTunnelEntrance.CancelTravel(this);
            verticalVelocity = Vector3.zero;
            extraVelocity = Vector3.zero;
            abilityDashUntil = 0f;
            slowUntil = 0f;
            slowMultiplier = 1f;

            Transform visual = transform.Find("VisualRoot");
            if (visual != null) visual.gameObject.SetActive(true);
            foreach (Collider collider in GetComponentsInChildren<Collider>()) if (collider != null) collider.enabled = true;

            health.Initialize(stats.MaxHealth, this);
            rangedAmmo = MaxRangedAmmo;
            rangedMagazineAmmo = RangedMagazineCapacity;
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
