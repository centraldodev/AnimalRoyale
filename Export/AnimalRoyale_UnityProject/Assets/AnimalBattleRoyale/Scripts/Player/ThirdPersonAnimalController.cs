using UnityEngine;

namespace AnimalBattleRoyale
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public sealed class ThirdPersonAnimalController : MonoBehaviour
    {
        [SerializeField] private AnimalType animalType = AnimalType.Tiger;
        [SerializeField] private bool isLocalPlayer;
        [SerializeField] private float rotationSpeed = 14f;
        [SerializeField] private float gravity = -24f;

        private const float MaxMobilityEnergy = 100f;
        private const float EagleFlightDrainPerSecond = 4.5f;
        private const float EagleMinimumTakeoffEnergy = 6f;
        private const float MonkeyVineLeapEnergyCost = 20f;
        private const float MonkeyAirJumpEnergyCost = 20f;
        private const int MonkeyMaxAirJumps = 5;
        private const float CorpseLifetime = 60f;

        private CharacterController characterController;
        private Health health;
        private AnimalStats stats;
        private Transform cameraTransform;
        private AnimalVisualMotion visualMotion;
        private JungleGenerator jungle;
        private Vector3 verticalVelocity;
        private Vector3 extraVelocity;
        private Vector3 aiMoveDirection;
        private bool aiSprint;
        private bool aiAttack;
        private int aiAbilitySlot = -1;
        private readonly float[] nextPowerTimes = new float[3];
        private float nextBasicAttackTime;
        private float burrowUntil;
        private float antFuryUntil;
        private float tigerPounceUntil;
        private float tigerClawComboUntil;
        private float eagleDiveUntil;
        private float climbUntil;
        private float flightUntil;
        private float monkeyFuryUntil;
        private float slowUntil;
        private float slowMultiplier = 1f;
        private float nextDashDamage;
        private float mobilityEnergy = MaxMobilityEnergy;
        private int monkeyAirJumpsUsed;
        private Vector3 dashDirection;
        private int lastPowerSlot = -1;
        private bool defeated;
        private bool tunnelControllerDisabled;
        private Transform heldVine;
        private Transform pendingVineGrab;
        private float pendingVineGrabAllowedAt;
        private float pendingVineGrabExpiresAt;
        private bool vineControllerDisabled;
        private readonly Collider[] combatHits = new Collider[64];
        private readonly RaycastHit[] surfaceHits = new RaycastHit[32];

        public AnimalType AnimalType => animalType;
        public AnimalStats Stats
        {
            get
            {
                EnsureRuntimeReferences();
                return stats;
            }
        }
        public Health Health => health;
        public bool IsLocalPlayer => isLocalPlayer;
        public bool IsDefeated => defeated;
        public bool IsFlying => Time.time < flightUntil;
        public bool IsBurrowed => Time.time < burrowUntil || AntTunnelEntrance.IsTraveling(this);
        public bool IsInAntTunnel => AntTunnelEntrance.IsTraveling(this);
        public bool IsHangingVine => heldVine != null;
        public bool IsWading => !IsFlying && !IsClimbing && !IsBurrowed
                                && CentralLake.Instance != null && CentralLake.Instance.Contains(transform.position);
        /// <summary>Deep water: the animal floats and paddles instead of walking the lakebed.</summary>
        public bool IsSwimming => IsWading
                                  && transform.position.y < CentralLake.Instance.SurfaceHeight - stats.ControllerHeight * 0.35f;
        public float TunnelSecondsRemaining => AntTunnelEntrance.SecondsRemaining(this);
        public bool UsesMobilityEnergy => animalType == AnimalType.Eagle || animalType == AnimalType.Monkey;
        public float MobilityEnergy => mobilityEnergy;
        public float MaxMobilityEnergyValue => MaxMobilityEnergy;
        public string MobilityEnergyName => animalType == AnimalType.Eagle ? "ENERGIA DE VOO" : "ENERGIA DE CIPÓ";
        public bool NeedsMobilityEnergy => UsesMobilityEnergy && mobilityEnergy < MaxMobilityEnergy - 0.5f;
        public int LastPowerSlot => lastPowerSlot;
        public string LastPowerName => Stats.AbilityNames != null && lastPowerSlot >= 0 && lastPowerSlot < Stats.AbilityNames.Length
            ? Stats.AbilityNames[lastPowerSlot]
            : "Ataque base";
        public string BasicActionName => animalType switch
        {
            AnimalType.Ant => "Mordida",
            AnimalType.Monkey => "Soco Duplo",
            AnimalType.Tiger => Time.time < tigerClawComboUntil ? "Patada Aérea de Garras" : "Patada de Garras",
            AnimalType.Eagle => "Golpe de Garras",
            _ => "Ataque"
        };

        public float AbilityCooldownRemainingFor(int slot)
        {
            return slot < 0 || slot >= nextPowerTimes.Length ? 0f : Mathf.Max(0f, nextPowerTimes[slot] - Time.time);
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            ApplyAnimal(animalType, true);
        }

        private void OnEnable()
        {
            // Unity can preserve the live GameObject while clearing non-serialized
            // runtime fields after a script reload in Play Mode. Restore those caches
            // so movement and the HUD continue working without restarting the match.
            EnsureRuntimeReferences();
        }

        private void EnsureRuntimeReferences()
        {
            if (stats.AbilityNames == null) stats = AnimalDefinition.Get(animalType);
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (health == null) health = GetComponent<Health>();
            if (visualMotion == null) visualMotion = GetComponentInChildren<AnimalVisualMotion>();
        }

        private void Start()
        {
            if (isLocalPlayer && Camera.main != null) cameraTransform = Camera.main.transform;
            SnapToTerrain(jungle != null ? jungle : FindAnyObjectByType<JungleGenerator>());
        }

        private void Update()
        {
            AntTunnelEntrance.Tick(this);
            UpdateVineHang();
            UpdatePendingVineGrab();
            UpdateTunnelCollisionState();
            UpdateBurrowVisual();
            if (defeated || health.IsDead) return;
            RecoverIfBelowTerrain();
            if (isLocalPlayer) HandleLocalInput();
            else HandleAIInput();
        }

        /// <summary>Places the CharacterController's feet a small distance above the terrain.</summary>
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
            if (jungle == null || IsFlying || IsClimbing || IsBurrowed) return;
            Vector3 safePosition = jungle.GetGroundPosition(transform.position);
            if (transform.position.y < safePosition.y - 0.5f)
            {
                SnapToTerrain(jungle);
            }
        }

        public void Initialize(AnimalType type, bool localPlayer, Transform cameraReference = null)
        {
            isLocalPlayer = localPlayer;
            cameraTransform = cameraReference;
            ApplyAnimal(type, true);
        }

        public void SetCamera(Transform cameraReference) => cameraTransform = cameraReference;

        public void SetAIInput(Vector3 moveDirection, bool sprint, bool attack, int abilitySlot)
        {
            aiMoveDirection = Vector3.ClampMagnitude(moveDirection, 1f);
            aiSprint = sprint;
            aiAttack = attack;
            aiAbilitySlot = abilitySlot;
        }

        public void SetDefeated()
        {
            if (defeated) return;
            defeated = true;
            AntTunnelEntrance.CancelTravel(this);
            ReleaseVine(Vector3.zero, false);
            pendingVineGrab = null;
            flightUntil = 0f;
            climbUntil = 0f;
            tigerPounceUntil = 0f;
            eagleDiveUntil = 0f;
            verticalVelocity = Vector3.zero;
            extraVelocity = Vector3.zero;
            aiMoveDirection = Vector3.zero;
            aiAttack = false;
            aiAbilitySlot = -1;

            // Corpses always settle at their death location on the terrain, even if the
            // animal was flying, climbing, or hanging from a vine when it was defeated.
            SnapToTerrain(jungle != null ? jungle : FindAnyObjectByType<JungleGenerator>());
            characterController.enabled = false;
            Transform visual = transform.Find("VisualRoot");
            if (visual != null)
            {
                visual.gameObject.SetActive(true);
                visualMotion?.Freeze();
                visual.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            Destroy(gameObject, CorpseLifetime);
        }

        public void ApplyAnimal(AnimalType type, bool refillHealth)
        {
            animalType = type;
            stats = AnimalDefinition.Get(type);
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (health == null) health = GetComponent<Health>();
            characterController.radius = stats.ControllerRadius;
            characterController.height = stats.ControllerHeight;
            characterController.center = new Vector3(0f, stats.ControllerHeight * 0.5f, 0f);
            characterController.stepOffset = Mathf.Min(0.35f, stats.ControllerHeight * 0.25f);
            characterController.skinWidth = 0.04f;
            AnimalVisualFactory.Build(transform, type, stats.MainColor, stats.VisualScale);
            visualMotion = GetComponentInChildren<AnimalVisualMotion>();
            mobilityEnergy = MaxMobilityEnergy;
            health.Initialize(stats.MaxHealth, this);
            if (!refillHealth) health.ReconfigureMaxHealth(stats.MaxHealth, false);
        }

        private void HandleLocalInput()
        {
            Vector3 movement = GetCameraRelativeDirection(GameInput.ReadMovement());
            if (IsInAntTunnel)
            {
                AntTunnelEntrance.Navigate(this, movement);
                return;
            }
            if (IsHangingVine)
            {
                if (GameInput.AbilityOnePressed()) TryMonkeyVineChain(GetAttackDirection(movement));
                if (GameInput.AttackPressed()) ReleaseVine(GetAttackDirection(movement), true);
                if (GameInput.ConsumePressed() && !MissionNode.TryUseNearest(this) && !DiamondPickup.TryCollectNearest(this)) FoodPickup.TryConsumeNearest(this);
                return;
            }
            if (animalType == AnimalType.Ant && GameInput.AbilityTwoPressed() && AntTunnelEntrance.TryEnter(this))
            {
                return;
            }
            SimulateMovement(movement, GameInput.SprintHeld(), GameInput.JumpPressed(), GameInput.JumpHeld(), GameInput.DescendHeld());
            if (GameInput.AttackPressed()) TryBasicAttack(GetAttackDirection(movement));
            if (GameInput.ConsumePressed() && !MissionNode.TryUseNearest(this) && !DiamondPickup.TryCollectNearest(this)) FoodPickup.TryConsumeNearest(this);
            if (GameInput.AbilityOnePressed()) TryUsePower(0, GetAttackDirection(movement));
        }

        private void HandleAIInput()
        {
            if (IsInAntTunnel)
            {
                AntTunnelEntrance.Navigate(this, aiMoveDirection);
                return;
            }
            if (IsHangingVine)
            {
                if (aiAttack) ReleaseVine(aiMoveDirection, true);
                aiAttack = false;
                aiAbilitySlot = -1;
                return;
            }
            SimulateMovement(aiMoveDirection, aiSprint, false, false, false);
            if (aiAttack) TryBasicAttack(aiMoveDirection);
            if (aiAbilitySlot >= 0) TryUsePower(aiAbilitySlot, aiMoveDirection);
            aiAttack = false;
            aiAbilitySlot = -1;
        }

        private void SimulateMovement(Vector3 direction, bool sprint, bool jumpPressed, bool jumpHeld, bool descendHeld)
        {
            visualMotion?.SetLocomotion(direction.sqrMagnitude > 0.01f, sprint, IsFlying || IsClimbing || !characterController.isGrounded);
            bool grounded = characterController.isGrounded;
            if (grounded)
            {
                monkeyAirJumpsUsed = 0;
                if (verticalVelocity.y < 0f) verticalVelocity.y = -2f;
            }

            if (animalType == AnimalType.Eagle && jumpPressed && !IsFlying && CanStartEagleFlight())
            {
                BeginEagleFlight(5.5f);
            }
            else if (jumpPressed && grounded && !IsFlying && !IsClimbing && !IsBurrowed)
            {
                verticalVelocity.y = stats.JumpForce;
            }
            else if (animalType == AnimalType.Monkey && jumpPressed && !grounded && monkeyAirJumpsUsed < MonkeyMaxAirJumps && TrySpendMobilityEnergy(MonkeyAirJumpEnergyCost))
            {
                monkeyAirJumpsUsed++;
                verticalVelocity.y = 8.8f;
                extraVelocity += transform.forward * 2.8f;
                AttackVfx.CreateSlash(transform.position + Vector3.up, transform.forward, new Color(0.42f, 1f, 0.25f), 1.8f);
            }

            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            float speed = sprint ? stats.SprintSpeed : stats.MoveSpeed;
            if (IsBurrowed) speed *= 0.58f;
            if (Time.time < antFuryUntil || Time.time < monkeyFuryUntil) speed *= 1.2f;
            if (Time.time < slowUntil) speed *= slowMultiplier;
            if (IsSwimming) speed *= 0.72f;
            else if (IsWading && !(ForestMissionDirector.Instance != null && ForestMissionDirector.Instance.LakePassageOpen))
                speed *= CentralLake.Instance.MovementMultiplier;
            Vector3 horizontal = direction * speed;
            if (Time.time < tigerPounceUntil || Time.time < eagleDiveUntil) horizontal = dashDirection * 19f;

            DrainFlightEnergy();

            if (IsFlying || IsClimbing)
            {
                float verticalInput = 0f;
                if (jumpHeld) verticalInput += 1f;
                if (descendHeld) verticalInput -= 1f;
                verticalVelocity.y = Mathf.MoveTowards(verticalVelocity.y, verticalInput * 5.5f, 18f * Time.deltaTime);
                if (IsFlying) horizontal *= 1.35f;
            }
            else if (IsSwimming)
            {
                // Buoyancy: every animal floats with the head above the water and
                // paddles toward the shore or the portal ramp. Space gives a hop
                // strong enough to leave the water at the edges.
                float surfaceTarget = CentralLake.Instance.SurfaceHeight - stats.ControllerHeight * 0.55f;
                float lift = Mathf.Clamp((surfaceTarget - transform.position.y) * 4.5f, -3f, 7f);
                verticalVelocity.y = Mathf.MoveTowards(verticalVelocity.y, lift, 30f * Time.deltaTime);
                if (jumpPressed) verticalVelocity.y = stats.JumpForce * 0.8f;
            }
            else verticalVelocity.y += gravity * Time.deltaTime;

            extraVelocity = Vector3.Lerp(extraVelocity, Vector3.zero, 4.5f * Time.deltaTime);
            characterController.Move((horizontal + verticalVelocity + extraVelocity) * Time.deltaTime);

            if (IsFlying && characterController.isGrounded && verticalVelocity.y < 0f)
            {
                flightUntil = 0f;
                verticalVelocity.y = -2f;
            }

            if (Time.time < eagleDiveUntil && Time.time >= nextDashDamage)
            {
                nextDashDamage = Time.time + 0.38f;
                DamageArea(1.75f, 19f, 10f, dashDirection, new Color(0.8f, 0.95f, 1f));
            }
        }

        private bool IsClimbing => Time.time < climbUntil;

        private Vector3 GetCameraRelativeDirection(Vector2 input)
        {
            if (cameraTransform == null) return new Vector3(input.x, 0f, input.y).normalized;
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            return Vector3.ClampMagnitude(forward.normalized * input.y + right.normalized * input.x, 1f);
        }

        private Vector3 GetAttackDirection(Vector3 movementDirection)
        {
            if (GameInput.AimHeld() && cameraTransform != null)
            {
                Vector3 aim = cameraTransform.forward;
                aim.y = 0f;
                if (aim.sqrMagnitude > 0.01f) return aim.normalized;
            }
            return movementDirection.sqrMagnitude > 0.01f ? movementDirection.normalized : transform.forward;
        }

        private void TryBasicAttack(Vector3 direction)
        {
            if (IsBurrowed || IsHangingVine || Time.time < nextBasicAttackTime) return;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            nextBasicAttackTime = Time.time + BasicCooldown();
            ForestMissionDirector.Instance?.NotifyTigerAttack(this);
            CombatFeedback.PlayBasic(animalType, transform.position);
            visualMotion?.TriggerAttack();
            switch (animalType)
            {
                case AnimalType.Ant:
                    PerformMeleeAttack(direction, stats.AttackDamage, stats.AttackRange, 16f, new Color(0.9f, 0.26f, 0.06f), 1.45f);
                    AttackVfx.CreateSlash(transform.position + Vector3.up * 0.55f, direction, new Color(0.48f, 0.12f, 0.04f), 1.05f);
                    break;
                case AnimalType.Monkey:
                    PerformMeleeAttack(direction, stats.AttackDamage, stats.AttackRange, 9f, new Color(1f, 0.76f, 0.15f), 1.55f);
                    break;
                case AnimalType.Tiger:
                    bool aerialCombo = Time.time < tigerClawComboUntil;
                    float tigerDamage = stats.AttackDamage * (aerialCombo ? 1.2f : 1f);
                    PerformMeleeAttack(direction, tigerDamage, stats.AttackRange, aerialCombo ? 15f : 10f,
                        new Color(1f, 0.28f, 0.04f), aerialCombo ? 2.25f : 1.9f);
                    CreateTigerClawMarks(direction, aerialCombo);
                    if (aerialCombo) tigerClawComboUntil = 0f;
                    break;
                case AnimalType.Eagle:
                    PerformMeleeAttack(direction, stats.AttackDamage, stats.AttackRange, 7f, new Color(0.8f, 0.95f, 1f), 1.4f);
                    break;
            }
        }

        private float BasicCooldown() => stats.AttackCooldown;

        private void TryUsePower(int slot, Vector3 direction)
        {
            if (slot != 0 || IsBurrowed || IsHangingVine || Time.time < nextPowerTimes[0]) return;
            if (animalType == AnimalType.Monkey && !VineAnchor.IsLookedAtBy(this)) return;
            if (animalType == AnimalType.Monkey && slot == 0 && !HasMobilityEnergy(MonkeyVineLeapEnergyCost)) return;
            nextPowerTimes[slot] = Time.time + stats.AbilityCooldowns[slot];
            lastPowerSlot = slot;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            CombatFeedback.PlayPower(animalType, slot, transform.position);
            AttackVfx.CreatePower(animalType, slot, transform.position, direction);
            visualMotion?.TriggerPower(slot);
            switch (animalType)
            {
                case AnimalType.Ant: UseAntPower(slot, direction); break;
                case AnimalType.Monkey: UseMonkeyPower(slot, direction); break;
                case AnimalType.Tiger: UseTigerPower(slot, direction); break;
                case AnimalType.Eagle: UseEaglePower(slot, direction); break;
            }
        }

        private void TryMonkeyVineChain(Vector3 direction)
        {
            if (animalType != AnimalType.Monkey || Time.time < nextPowerTimes[0] || !HasMobilityEnergy(MonkeyVineLeapEnergyCost)) return;
            if (!VineAnchor.TryUseNearest(this, direction)) return;
            nextPowerTimes[0] = Time.time + stats.AbilityCooldowns[0];
            lastPowerSlot = 0;
            CombatFeedback.PlayPower(animalType, 0, transform.position);
            visualMotion?.TriggerPower(0);
        }

        private void UseAntPower(int slot, Vector3 direction)
        {
            if (slot == 0)
            {
                DamageArea(3.0f, 8f, 28f, direction, new Color(1f, 0.34f, 0.06f));
            }
        }

        private void UseMonkeyPower(int slot, Vector3 direction)
        {
            if (slot == 0)
            {
                VineAnchor.TryUseNearest(this, direction);
            }
        }

        private void UseTigerPower(int slot, Vector3 direction)
        {
            if (slot == 0)
            {
                dashDirection = direction;
                tigerPounceUntil = Time.time + 0.62f;
                tigerClawComboUntil = Time.time + 1.15f;
                verticalVelocity.y = 10.5f;
                extraVelocity += direction * 9f;
                AttackVfx.CreateSlash(transform.position + Vector3.up, direction, new Color(1f, 0.3f, 0.05f), 3f);
            }
        }

        private void UseEaglePower(int slot, Vector3 direction)
        {
            if (slot == 0)
            {
                StartEagleDive(direction);
            }
        }

        private void StartEagleDive(Vector3 direction)
        {
            dashDirection = direction;
            eagleDiveUntil = Time.time + 0.68f;
            nextDashDamage = Time.time;
            verticalVelocity.y = characterController.isGrounded ? 4.5f : -8f;
            extraVelocity += direction * 9f;
            AttackVfx.CreateSlash(transform.position + Vector3.up, direction, new Color(0.82f, 0.96f, 1f), 3.1f);
        }

        private void CreateTigerClawMarks(Vector3 direction, bool aerialCombo)
        {
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            float size = aerialCombo ? 2.25f : 1.65f;
            for (int claw = -1; claw <= 1; claw++)
            {
                Vector3 origin = transform.position + Vector3.up * (0.65f + (claw + 1) * 0.1f) + right * claw * 0.20f;
                AttackVfx.CreateSlash(origin, direction, new Color(1f, 0.72f, 0.22f), size - Mathf.Abs(claw) * 0.12f);
            }
        }

        private void PerformMeleeAttack(Vector3 direction, float damage, float range, float knockback, Color color, float visualSize)
        {
            AttackVfx.CreateSlash(transform.position + Vector3.up * (stats.ControllerHeight * 0.58f), direction, color, visualSize);
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position + Vector3.up * 0.8f + direction * (range * 0.48f),
                range, combatHits, ~0, QueryTriggerInteraction.Ignore);
            float dotThreshold = GameInput.AimHeld() ? 0.72f : 0.18f;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = combatHits[i];
                Health other = hit.GetComponentInParent<Health>();
                if (other == null || other == health || other.IsDead || other.Owner != null && other.Owner.IsBurrowed) continue;
                Vector3 targetDirection = other.transform.position - transform.position;
                targetDirection.y = 0f;
                if (targetDirection.sqrMagnitude > 0.01f && Vector3.Dot(direction, targetDirection.normalized) < dotThreshold) continue;
                other.TakeDamage(damage, this);
                CombatFeedback.NotifyHit(animalType, other.transform.position, damage);
                if (other.Owner != null) other.Owner.ReceiveKnockback(targetDirection.normalized * knockback);
                break;
            }
        }

        private void DamageArea(float radius, float damage, float knockback, Vector3 direction, Color color)
        {
            AttackVfx.CreateBurst(transform.position + Vector3.up * 0.2f, color, radius);
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, radius,
                combatHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = combatHits[i];
                Health other = hit.GetComponentInParent<Health>();
                if (other == null || other == health || other.IsDead || other.Owner != null && other.Owner.IsBurrowed) continue;
                other.TakeDamage(damage, this);
                CombatFeedback.NotifyHit(animalType, other.transform.position, damage);
                Vector3 push = other.transform.position - transform.position;
                push.y = 0.15f;
                if (other.Owner != null) other.Owner.ReceiveKnockback((push.sqrMagnitude > 0.01f ? push.normalized : direction) * knockback);
            }
        }

        private void ApplySlowToTargets(float radius, float multiplier, float duration)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, radius,
                combatHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = combatHits[i];
                Health other = hit.GetComponentInParent<Health>();
                if (other?.Owner == null || other == health || other.Owner.IsBurrowed) continue;
                other.Owner.slowMultiplier = multiplier;
                other.Owner.slowUntil = Time.time + duration;
            }
        }

        private bool CanClimb()
        {
            Vector3 origin = transform.position + Vector3.up * (stats.ControllerHeight * 0.65f);
            return Physics.Raycast(origin, transform.forward, 1.6f, ~0, QueryTriggerInteraction.Ignore);
        }

        private bool TryPerch()
        {
            if (cameraTransform == null) return false;
            int hitCount = Physics.RaycastNonAlloc(new Ray(cameraTransform.position, cameraTransform.forward),
                surfaceHits, 80f, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit hit = default;
            bool foundSurface = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = surfaceHits[i];
                if (candidate.transform == transform || candidate.transform.IsChildOf(transform)) continue;
                if (!foundSurface || candidate.distance < hit.distance) { hit = candidate; foundSurface = true; }
            }
            if (!foundSurface) return false;
            flightUntil = 0f;
            verticalVelocity = Vector3.zero;
            transform.position = hit.point + hit.normal * (characterController.height * 0.52f);
            AttackVfx.CreateBurst(transform.position, new Color(0.85f, 0.95f, 1f), 1.7f);
            return true;
        }

        private void UpdateBurrowVisual()
        {
            Transform visual = transform.Find("VisualRoot");
            if (visual != null && visual.gameObject.activeSelf == IsBurrowed) visual.gameObject.SetActive(!IsBurrowed);
        }

        private void UpdateTunnelCollisionState()
        {
            bool shouldDisableController = IsInAntTunnel;
            if (shouldDisableController == tunnelControllerDisabled || characterController == null) return;
            characterController.enabled = !shouldDisableController;
            tunnelControllerDisabled = shouldDisableController;
        }

        private void UpdateVineHang()
        {
            if (heldVine == null)
            {
                if (vineControllerDisabled && characterController != null && !defeated)
                {
                    characterController.enabled = true;
                    vineControllerDisabled = false;
                }
                return;
            }

            if (characterController != null && characterController.enabled)
            {
                characterController.enabled = false;
                vineControllerDisabled = true;
            }
            transform.position = heldVine.position - Vector3.up * (stats.ControllerHeight * 0.92f);
            verticalVelocity = Vector3.zero;
            extraVelocity = Vector3.zero;
        }

        public void ReceiveKnockback(Vector3 force) => extraVelocity += force;

        public bool TryGrabVine(Transform vine)
        {
            if (animalType != AnimalType.Monkey || vine == null || IsHangingVine) return false;
            if (!TrySpendMobilityEnergy(MonkeyVineLeapEnergyCost)) return false;
            return AttachToVine(vine);
        }

        private bool AttachToVine(Transform vine)
        {
            if (vine == null || IsHangingVine) return false;
            pendingVineGrab = null;
            heldVine = vine;
            UpdateVineHang();
            visualMotion?.SetVineHanging(true);
            AttackVfx.CreateBurst(vine.position, new Color(0.35f, 1f, 0.45f), 1.25f);
            return true;
        }

        public bool IsHoldingVine(Transform vine) => heldVine != null && heldVine == vine;

        private void ReleaseVine(Vector3 direction, bool launch)
        {
            if (heldVine == null) return;
            heldVine = null;
            pendingVineGrab = null;
            if (characterController != null && vineControllerDisabled && !defeated)
            {
                characterController.enabled = true;
                vineControllerDisabled = false;
            }
            visualMotion?.SetVineHanging(false);
            if (!launch) return;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            verticalVelocity.y = 6.5f;
            extraVelocity += direction * 10f;
            AttackVfx.CreateSlash(transform.position + Vector3.up, direction, new Color(0.38f, 1f, 0.28f), 2.1f);
        }

        public bool TryLaunchFromVine(Vector3 direction)
        {
            if (animalType != AnimalType.Monkey || !TrySpendMobilityEnergy(MonkeyVineLeapEnergyCost)) return false;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            verticalVelocity.y = 11.5f;
            extraVelocity += direction * 16f;
            AttackVfx.CreateSlash(transform.position + Vector3.up, direction, new Color(0.4f, 1f, 0.25f), 2.9f);
            return true;
        }

        public bool TryLaunchToVine(Transform vine)
        {
            if (animalType != AnimalType.Monkey || vine == null || !TrySpendMobilityEnergy(MonkeyVineLeapEnergyCost)) return false;
            Vector3 vinePosition = vine.position;
            Vector3 toVine = vinePosition - transform.position;
            toVine.y = 0f;
            Vector3 direction = toVine.sqrMagnitude > 0.01f ? toVine.normalized : transform.forward;
            if (heldVine != null) ReleaseVine(direction, false);
            pendingVineGrab = vine;
            pendingVineGrabAllowedAt = Time.time + 0.12f;
            pendingVineGrabExpiresAt = Time.time + 1.4f;
            float horizontalSpeed = Mathf.Clamp(toVine.magnitude * 1.05f, 16f, 26f);
            verticalVelocity.y = Mathf.Max(verticalVelocity.y, 12.5f);
            extraVelocity += direction * horizontalSpeed;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            AttackVfx.CreateSlash(transform.position + Vector3.up, direction, new Color(0.4f, 1f, 0.25f), 3.2f);
            return true;
        }

        private void UpdatePendingVineGrab()
        {
            if (pendingVineGrab == null || IsHangingVine) return;
            if (Time.time > pendingVineGrabExpiresAt)
            {
                pendingVineGrab = null;
                return;
            }
            if (Time.time < pendingVineGrabAllowedAt) return;
            if ((pendingVineGrab.position - transform.position).sqrMagnitude > 3.4f * 3.4f) return;
            AttachToVine(pendingVineGrab);
        }

        public void RestoreMobilityEnergy(float amount)
        {
            if (!UsesMobilityEnergy || amount <= 0f) return;
            mobilityEnergy = Mathf.Min(MaxMobilityEnergy, mobilityEnergy + amount);
        }

        private bool HasMobilityEnergy(float amount) => !UsesMobilityEnergy || mobilityEnergy >= amount;

        private bool TrySpendMobilityEnergy(float amount)
        {
            if (!UsesMobilityEnergy) return true;
            if (mobilityEnergy < amount) return false;
            mobilityEnergy -= amount;
            return true;
        }

        private bool CanStartEagleFlight() => mobilityEnergy >= EagleMinimumTakeoffEnergy;

        private void BeginEagleFlight(float lift)
        {
            flightUntil = float.PositiveInfinity;
            verticalVelocity.y = lift;
            AttackVfx.CreateBurst(transform.position, new Color(0.72f, 0.92f, 1f), 2.3f);
        }

        private void DrainFlightEnergy()
        {
            if (!IsFlying) return;
            mobilityEnergy = Mathf.Max(0f, mobilityEnergy - EagleFlightDrainPerSecond * Time.deltaTime);
            if (mobilityEnergy > 0f) return;
            flightUntil = 0f;
            verticalVelocity.y = Mathf.Min(verticalVelocity.y, -3.5f);
            AttackVfx.CreateBurst(transform.position, new Color(0.45f, 0.58f, 0.68f), 1.2f);
        }

        public void TeleportTo(Vector3 worldPosition)
        {
            bool controllerWasEnabled = characterController.enabled;
            characterController.enabled = false;
            transform.position = worldPosition;
            verticalVelocity = Vector3.zero;
            extraVelocity = Vector3.zero;
            characterController.enabled = controllerWasEnabled;
        }

        public float ModifyIncomingDamage(float damage)
        {
            if (animalType == AnimalType.Ant) damage *= 0.88f;
            if (IsBurrowed) damage *= 0.2f;
            return damage;
        }
    }
}
