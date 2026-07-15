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

        private const int MaxRangedAmmo = 12;
        private const float DefeatedCleanupDelay = 1.5f;

        private readonly Collider[] combatHits = new Collider[64];
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
        private int lastPowerSlot = -1;
        private bool defeated;

        public AnimalType AnimalType => animalType;
        public AnimalStats Stats { get { EnsureRuntimeReferences(); return stats; } }
        public Health Health => health;
        public bool IsLocalPlayer => isLocalPlayer;
        public bool IsDefeated => defeated;
        public bool IsFlying => false;
        public bool IsBurrowed => false;
        public bool IsInAntTunnel => false;
        public bool IsHangingVine => false;
        public bool IsWading => CentralLake.Instance != null && CentralLake.Instance.Contains(transform.position);
        public bool IsSwimming => IsWading && transform.position.y < CentralLake.Instance.SurfaceHeight - stats.ControllerHeight * 0.35f;
        public float TunnelSecondsRemaining => 0f;
        public bool UsesMobilityEnergy => false;
        public float MobilityEnergy => 0f;
        public float MaxMobilityEnergyValue => 0f;
        public string MobilityEnergyName => string.Empty;
        public bool NeedsMobilityEnergy => false;
        public bool IsMobilityRecharging => false;
        public float MobilityRechargeSecondsRemaining => 0f;
        public int RangedAmmo => rangedAmmo;
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

        public string RangedAmmoName => animalType switch
        {
            AnimalType.Deer => "BOLOTAS",
            AnimalType.Chicken => "OVOS",
            AnimalType.Dog => "OSSOS",
            AnimalType.Cat => "NOVELOS",
            AnimalType.Penguin => "BOLAS DE NEVE",
            _ => "CARGAS"
        };

        public string RangedAttackName => animalType switch
        {
            AnimalType.Tiger => "ONDA DE GARRAS",
            AnimalType.Deer => "DISPARO DE BOLOTA",
            AnimalType.Horse => "PEDRA DE CASCO",
            AnimalType.Chicken => "ARREMESSO DE OVO",
            AnimalType.Dog => "ARREMESSO DE OSSO",
            AnimalType.Cat => "NOVELO VELOZ",
            AnimalType.Penguin => "BOLA DE NEVE",
            _ => "ATAQUE À DISTÂNCIA"
        };

        public string LastPowerName => Stats.AbilityNames != null && lastPowerSlot >= 0 && lastPowerSlot < Stats.AbilityNames.Length
            ? Stats.AbilityNames[lastPowerSlot] : "Ataque base";

        public string BasicActionName => animalType switch
        {
            AnimalType.Tiger => "Patada de Garras",
            AnimalType.Deer => "Golpe de Chifres",
            AnimalType.Horse => "Coice",
            AnimalType.Chicken => "Bicada",
            AnimalType.Dog => "Mordida",
            AnimalType.Cat => "Arranhão",
            AnimalType.Penguin => "Golpe de Nadadeira",
            _ => "Ataque"
        };

        public float AbilityCooldownRemainingFor(int slot) => slot < 0 || slot >= nextPowerTimes.Length
            ? 0f : Mathf.Max(0f, nextPowerTimes[slot] - Time.time);

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
            if (defeated || health.IsDead) return;
            if (BattleRoyaleManager.Instance != null && BattleRoyaleManager.Instance.MatchFinished)
            {
                visualMotion?.SetLocomotion(false, false, false);
                return;
            }
            RecoverIfBelowTerrain();
            if (isLocalPlayer) HandleLocalInput();
            else HandleAIInput();
        }

        public void Initialize(AnimalType type, bool localPlayer, Transform cameraReference = null)
        {
            isLocalPlayer = localPlayer;
            cameraTransform = cameraReference;
            ApplyAnimal(type, true);
        }

        public void SetCamera(Transform cameraReference) => cameraTransform = cameraReference;

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
            characterController.radius = stats.ControllerRadius;
            characterController.height = stats.ControllerHeight;
            characterController.center = new Vector3(0f, stats.ControllerHeight * 0.5f, 0f);
            characterController.stepOffset = Mathf.Min(0.35f, stats.ControllerHeight * 0.25f);
            characterController.skinWidth = 0.04f;
            Transform visualRoot = AnimalVisualFactory.Build(transform, type, stats.MainColor, stats.VisualScale);
            visualMotion = visualRoot != null ? visualRoot.GetComponent<AnimalVisualMotion>() : null;
            rangedAmmo = MaxRangedAmmo;
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
            if (GameInput.RangedAttackPressed()) TryRangedAttack(GetRangedAttackDirection(movement));
            if (GameInput.MeleeAttackPressed()) TryBasicAttack(GetAttackDirection(movement));
            if (GameInput.AbilityOnePressed()) TryUsePower(0, GetAttackDirection(movement));
            if (GameInput.ConsumePressed() && !MissionNode.TryUseNearest(this) && !DiamondPickup.TryCollectNearest(this)
                && !RangedAmmoPickup.TryCollectNearest(this)) FoodPickup.TryConsumeNearest(this);
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
            bool grounded = characterController.isGrounded;
            bool abilityDashing = Time.time < abilityDashUntil;
            visualMotion?.SetLocomotion(direction.sqrMagnitude > 0.01f || abilityDashing, sprint || abilityDashing, !grounded);
            if (grounded && verticalVelocity.y < 0f) verticalVelocity.y = -2f;
            if (jumpPressed && grounded) verticalVelocity.y = stats.JumpForce;

            if (direction.sqrMagnitude > 0.01f && !abilityDashing)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            float speed = sprint ? stats.SprintSpeed : stats.MoveSpeed;
            if (Time.time < slowUntil) speed *= slowMultiplier;
            if (IsSwimming) speed *= 0.72f;
            else if (IsWading && !(ForestMissionDirector.Instance != null && ForestMissionDirector.Instance.LakePassageOpen))
                speed *= CentralLake.Instance.MovementMultiplier;

            Vector3 horizontal = abilityDashing ? dashDirection * abilityDashSpeed : direction * speed;
            if (abilityDashing && Time.time >= nextAbilityDamage)
            {
                nextAbilityDamage = Time.time + 0.24f;
                DamageArea(abilityRadius, abilityDamage, abilityKnockback, dashDirection, abilityColor, abilitySlow);
            }

            if (IsSwimming)
            {
                float surfaceTarget = CentralLake.Instance.SurfaceHeight - stats.ControllerHeight * 0.55f;
                float lift = Mathf.Clamp((surfaceTarget - transform.position.y) * 4.5f, -3f, 7f);
                verticalVelocity.y = Mathf.MoveTowards(verticalVelocity.y, lift, 30f * Time.deltaTime);
            }
            else verticalVelocity.y += gravity * Time.deltaTime;

            extraVelocity = Vector3.Lerp(extraVelocity, Vector3.zero, 4.5f * Time.deltaTime);
            characterController.Move((horizontal + verticalVelocity + extraVelocity) * Time.deltaTime);
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

        private Vector3 GetRangedAttackDirection(Vector3 movementDirection) => cameraTransform != null
            ? ViewAimDirection : GetAttackDirection(movementDirection);

        public bool TryGetRangedAimViewportPosition(out Vector2 viewportPosition)
        {
            viewportPosition = new Vector2(0.5f, 0.5f);
            if (cameraTransform == null || cameraTransform.GetComponent<Camera>() is not Camera aimCamera) return false;
            Vector3 launchPoint = transform.position + Vector3.up * (stats.ControllerHeight * 0.62f + 0.3f);
            Vector3 projected = aimCamera.WorldToViewportPoint(launchPoint + ViewAimDirection * 60f);
            if (projected.z <= 0f) return false;
            viewportPosition = new Vector2(Mathf.Clamp(projected.x, 0.06f, 0.94f), Mathf.Clamp(projected.y, 0.1f, 0.92f));
            return true;
        }

        private void TryRangedAttack(Vector3 direction)
        {
            if (BattleRoyaleManager.Instance != null && !BattleRoyaleManager.Instance.CombatEnabled) return;
            if (Time.time < nextBasicAttackTime || rangedAmmo <= 0) return;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            rangedAmmo--;
            nextBasicAttackTime = Time.time + RangedAttackCooldown();
            Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);
            if (flatDirection.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            visualMotion?.TriggerAttack(false);
            RangedProjectile.Fire(this, direction);
        }

        private float RangedAttackCooldown() => animalType switch
        {
            AnimalType.Chicken => 0.6f,
            AnimalType.Cat => 0.64f,
            AnimalType.Dog => 0.7f,
            AnimalType.Deer => 0.76f,
            AnimalType.Penguin => 0.78f,
            AnimalType.Tiger => 0.86f,
            AnimalType.Horse => 0.9f,
            _ => 0.8f
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
            AnimalType.Horse => 15f,
            AnimalType.Deer => 13f,
            AnimalType.Tiger => 11f,
            AnimalType.Penguin => 10f,
            _ => 8f
        };

        private void TryUsePower(int slot, Vector3 direction)
        {
            if (BattleRoyaleManager.Instance != null && !BattleRoyaleManager.Instance.CombatEnabled) return;
            if (slot != 0 || Time.time < nextPowerTimes[0]) return;
            nextPowerTimes[0] = Time.time + stats.AbilityCooldowns[0];
            lastPowerSlot = 0;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            CombatFeedback.PlayPower(animalType, 0, transform.position);
            AttackVfx.CreatePower(animalType, 0, transform.position, direction);
            visualMotion?.TriggerPower(0);
            ForestMissionDirector.Instance?.NotifyAbilityUsed(this);
            UseAnimalAbility(direction);
        }

        private void UseAnimalAbility(Vector3 direction)
        {
            abilityHitTargets.Clear();
            switch (animalType)
            {
                case AnimalType.Tiger:
                    BeginDash(direction, 0.58f, 21f, 24f, 12f, 1.85f, 1f, new Color(1f, 0.28f, 0.04f));
                    verticalVelocity.y = 7.5f;
                    break;
                case AnimalType.Deer:
                    BeginDash(direction, 0.78f, 23f, 22f, 18f, 1.9f, 1f, new Color(0.88f, 0.64f, 0.28f));
                    break;
                case AnimalType.Horse:
                    BeginDash(direction, 1.05f, 25f, 19f, 22f, 2.1f, 1f, new Color(0.72f, 0.44f, 0.2f));
                    break;
                case AnimalType.Chicken:
                    verticalVelocity.y = 8.5f;
                    DamageArea(4f, 14f, 14f, direction, new Color(1f, 0.82f, 0.22f), 0.62f);
                    break;
                case AnimalType.Dog:
                    health.Heal(28f);
                    DamageArea(3.6f, 12f, 16f, direction, new Color(0.35f, 0.78f, 1f), 0.78f);
                    break;
                case AnimalType.Cat:
                    health.Heal(22f);
                    resistanceUntil = Time.time + 4.5f;
                    extraVelocity += direction * 9f;
                    break;
                case AnimalType.Penguin:
                    BeginDash(direction, 0.95f, 20f, 16f, 12f, 2.2f, 0.55f, new Color(0.38f, 0.86f, 1f));
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

        public bool TryRefillRangedAmmo(RangedSupplyKind supplyKind, int amount)
        {
            if (defeated || health == null || health.IsDead || supplyKind != CompatibleRangedSupply || amount <= 0 || !NeedsRangedAmmo) return false;
            rangedAmmo = Mathf.Min(MaxRangedAmmo, rangedAmmo + amount);
            AttackVfx.CreateBurst(transform.position + Vector3.up * 0.6f, new Color(1f, 0.76f, 0.18f), 1.25f);
            return true;
        }

        public void RestoreMobilityEnergy(float amount) { }
        public bool TryGrabVine(Transform vine) => false;
        public bool IsHoldingVine(Transform vine) => false;
        public bool TryLaunchFromVine(Vector3 direction) => false;
        public bool TryLaunchToVine(Transform vine) => false;

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
            if (Time.time < resistanceUntil) damage *= 0.52f;
            return damage;
        }

        public void SetDefeated()
        {
            if (defeated) return;
            defeated = true;
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
