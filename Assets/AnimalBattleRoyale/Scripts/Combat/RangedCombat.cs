using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    public enum WeaponAmmoType : byte
    {
        Seed,
        Tomato,
        Watermelon
    }

    public sealed class RangedProjectile : MonoBehaviour
    {
        // Watermelon's splash radius was widened (was 3.4) so it reads as more of an area
        // weapon now that the seed slot is the dedicated precision option.
        public const float WatermelonExplosionRadius = 5f;
        public const float SeedDamage = 50f;
        public const float TomatoDamage = 100f / 6f;
        public const float WatermelonDamage = 100f / 3f;
        private const float TomatoFullDamageRange = 5f;
        private const float TomatoMediumDamageRange = 20f;
        private const float WatermelonFullDamageRange = 3f;
        private const float WatermelonMediumDamageRange = 10f;
        private const float WatermelonLongRangeDamage = 1f;
        // Global travel-only boost: projectiles reach the aimed point twice as fast without
        // changing fire cadence, damage, range lifetime, magazine size or reload timing.
        private const float ProjectileTravelSpeedMultiplier = 2f;
        // Only the walnut precision round can headshot: 50 body damage becomes 100.
        private const float SeedHeadshotDamageMultiplier = 2f;
        private const float LethalDamageEpsilon = 0.001f;
        // Top fraction of the target's controller height counted as the head — these are
        // chibi characters with proportionally huge heads, so this sits fairly high but not
        // razor-thin at the very top.
        private const float HeadshotHeightFraction = 0.72f;
        // Cow keeps the same seed/tomato/watermelon damage tiers under the hood — only the
        // projectile's look is swapped to milk.
        public static readonly Color MilkColor = new Color(0.96f, 0.95f, 0.9f);
        private static Material sharedMilkMaterial;
        private static Material sharedTrailMaterial;
        private static readonly Dictionary<AnimalType, Material> sharedSeedMaterials = new Dictionary<AnimalType, Material>();
        private static readonly Dictionary<string, Material> sharedFruitMaterials = new Dictionary<string, Material>();
        // Pooled by (animal, ammo) since that pair fully determines the projectile's
        // geometry/scale — reusing one avoids rebuilding primitives and materials per shot.
        private static readonly Dictionary<int, Stack<RangedProjectile>> pool = new Dictionary<int, Stack<RangedProjectile>>();
        private readonly RaycastHit[] hitBuffer = new RaycastHit[20];
        private readonly Collider[] areaHitBuffer = new Collider[64];
        private readonly HashSet<Health> areaHitTargets = new HashSet<Health>();
        private ThirdPersonAnimalController owner;
        private Transform visual;
        private TrailRenderer trailRenderer;
        private bool visualBuilt;
        private int poolKey;
        private Vector3 launchPosition;
        private Vector3 velocity;
        private float gravity;
        private float damage;
        private float radius;
        private float expiresAt;
        private Color impactColor;
        private WeaponAmmoType ammoType;

        private static int PoolKey(AnimalType animalType, WeaponAmmoType ammoType) => ((int)animalType << 8) | (int)ammoType;

        public static void Fire(ThirdPersonAnimalController source, Vector3 direction)
        {
            if (source == null) return;
            int key = PoolKey(source.AnimalType, source.CurrentWeaponAmmo);
            RangedProjectile projectile = null;
            if (pool.TryGetValue(key, out Stack<RangedProjectile> stack))
            {
                while (stack.Count > 0)
                {
                    RangedProjectile candidate = stack.Pop();
                    if (candidate != null)
                    {
                        projectile = candidate;
                        break;
                    }
                }
            }

            if (projectile == null)
            {
                GameObject projectileObject = new GameObject("RangedProjectile_" + source.AnimalType);
                projectile = projectileObject.AddComponent<RangedProjectile>();
                projectile.poolKey = key;
            }
            else
            {
                projectile.gameObject.SetActive(true);
            }
            projectile.Configure(source, direction);
        }

        private void ReturnToPool()
        {
            gameObject.SetActive(false);
            if (!pool.TryGetValue(poolKey, out Stack<RangedProjectile> stack))
            {
                stack = new Stack<RangedProjectile>();
                pool[poolKey] = stack;
            }
            stack.Push(this);
        }

        private static Color SeedColorForAnimal(AnimalType type) => type switch
        {
            AnimalType.Tiger => new Color(0.85f, 0.72f, 0.42f),
            AnimalType.Ant => new Color(0.82f, 0.68f, 0.36f),
            AnimalType.Eagle => new Color(0.8f, 0.74f, 0.5f),
            AnimalType.Monkey => new Color(0.78f, 0.7f, 0.4f),
            _ => new Color(0.82f, 0.7f, 0.42f)
        };

        public static Vector3 GetLaunchPosition(ThirdPersonAnimalController source, Vector3 direction)
        {
            if (source == null) return Vector3.zero;
            // The muzzle marker is stable by construction (see WeaponMuzzleSocket) — no
            // per-shot settling needed, unlike the old separate-prop weapon.
            if (source.TryGetWeaponMuzzle(out Vector3 muzzlePosition, out _)) return muzzlePosition;

            // Cow (no embedded gun) and any animal whose model failed to load fall back to
            // a fixed offset from the body center.
            Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);
            Vector3 forwardOffset = flatDirection.sqrMagnitude > 0.01f
                ? flatDirection.normalized * 0.9f
                : source.transform.forward * 0.9f;
            return source.transform.position
                + Vector3.up * (source.Stats.ControllerHeight * 0.62f + 0.3f)
                + forwardOffset;
        }

        private void Configure(ThirdPersonAnimalController source, Vector3 direction)
        {
            owner = source;
            ammoType = source.CurrentWeaponAmmo;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : source.transform.forward;
            // Base speed is tunable globally by the host; each ammo type scales it to its own
            // identity — nozes near-instant (precision, has to be aimed), tomato a weak-pistol
            // pace, watermelon a shotgun-like base (see ProjectileSpeedMultiplierFor).
            float speed = ServerGameTuning.ProjectileSpeed
                          * ThirdPersonAnimalController.ProjectileSpeedMultiplierFor(source.CurrentWeaponAmmo)
                          * ProjectileTravelSpeedMultiplier;
            float lift;
            float visualScale;
            switch (source.AnimalType)
            {
                case AnimalType.Tiger:
                    lift = 0.35f; gravity = 2.4f; radius = 0.18f; visualScale = 0.34f;
                    break;
                case AnimalType.Ant:
                    lift = 0.3f; gravity = 2.2f; radius = 0.15f; visualScale = 0.28f;
                    break;
                case AnimalType.Eagle:
                    lift = 0.4f; gravity = 2.3f; radius = 0.17f; visualScale = 0.32f;
                    break;
                case AnimalType.Monkey:
                    lift = 0.35f; gravity = 2.3f; radius = 0.16f; visualScale = 0.3f;
                    break;
                case AnimalType.Cow:
                    // Heavier jet of milk: bigger and falls a bit faster than the others.
                    lift = 0.3f; gravity = 2.6f; radius = 0.2f; visualScale = 0.38f;
                    break;
                default:
                    lift = 0.35f; gravity = 2.3f; radius = 0.17f; visualScale = 0.3f;
                    break;
            }
            impactColor = SeedColorForAnimal(source.AnimalType);

            // Damage depends on ammo type, not the shooter's animal.
            damage = ammoType switch
            {
                WeaponAmmoType.Tomato => TomatoDamage,
                WeaponAmmoType.Watermelon => WatermelonDamage,
                _ => SeedDamage
            };

            lift *= ServerGameTuning.ProjectileLiftMultiplier;
            gravity *= ServerGameTuning.ProjectileGravityMultiplier;
            damage *= ServerGameTuning.ProjectileDamageMultiplier;
            radius *= ServerGameTuning.ProjectileRadiusMultiplier;
            switch (ammoType)
            {
                case WeaponAmmoType.Tomato:
                    impactColor = new Color(0.96f, 0.12f, 0.055f);
                    radius *= 1.16f;
                    visualScale *= 1.3f;
                    break;
                case WeaponAmmoType.Watermelon:
                    impactColor = new Color(0.16f, 0.82f, 0.2f);
                    radius *= 1.38f;
                    visualScale *= 1.62f;
                    break;
            }
            // Cow's shots always read as milk, regardless of the tier's usual color.
            if (source.AnimalType == AnimalType.Cow) impactColor = MilkColor;

            transform.position = GetLaunchPosition(source, direction);
            launchPosition = transform.position;
            transform.rotation = Quaternion.identity;
            velocity = direction * speed + Vector3.up * lift;
            expiresAt = Time.time + ServerGameTuning.ProjectileRangeSeconds;
            poolKey = PoolKey(source.AnimalType, ammoType);
            if (!visualBuilt)
            {
                BuildVisual(source.AnimalType, visualScale);
                BuildTrail();
                visualBuilt = true;
            }
            else if (trailRenderer != null)
            {
                trailRenderer.Clear();
                trailRenderer.startWidth = radius * 0.42f;
                trailRenderer.startColor = new Color(impactColor.r, impactColor.g, impactColor.b, 0.7f);
                trailRenderer.endColor = new Color(impactColor.r, impactColor.g, impactColor.b, 0f);
            }
            AttackVfx.CreateBurst(transform.position, impactColor, 0.5f);
            CombatFeedback.PlayProjectileLaunch(transform.position, ammoType);
        }

        private void Update()
        {
            if (owner == null || Time.time >= expiresAt)
            {
                ReturnToPool();
                return;
            }

            float deltaTime = Time.deltaTime;
            velocity += Vector3.down * (gravity * deltaTime);
            Vector3 displacement = velocity * deltaTime;
            if (TryFindImpact(displacement, out RaycastHit hit))
            {
                transform.position = hit.point;
                ResolveImpact(hit);
                return;
            }

            transform.position += displacement;
            if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            if (visual != null) visual.Rotate(280f * deltaTime, 190f * deltaTime, 120f * deltaTime, Space.Self);
        }

        private bool TryFindImpact(Vector3 displacement, out RaycastHit nearest)
        {
            nearest = default;
            float distance = displacement.magnitude;
            if (distance <= 0.0001f) return false;
            int hitCount = Physics.SphereCastNonAlloc(transform.position, radius, displacement / distance,
                hitBuffer, distance, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = hitBuffer[i];
                if (candidate.collider == null) continue;
                Transform candidateTransform = candidate.collider.transform;
                if (candidateTransform == owner.transform || candidateTransform.IsChildOf(owner.transform)) continue;
                if (!found || candidate.distance < nearest.distance)
                {
                    nearest = candidate;
                    found = true;
                }
            }
            return found;
        }

        private void ResolveImpact(RaycastHit hit)
        {
            CombatFeedback.PlayProjectileImpact(hit.point, ammoType);
            if (ammoType == WeaponAmmoType.Watermelon)
            {
                DamageArea(hit.point);
                AttackVfx.CreateFruitExplosion(hit.point + hit.normal * 0.08f, ammoType, WatermelonExplosionRadius);
            }
            else
            {
                Health target = hit.collider != null ? hit.collider.GetComponentInParent<Health>() : null;
                DamageDirectTarget(target, hit.point);
                if (ammoType == WeaponAmmoType.Tomato)
                    AttackVfx.CreateFruitExplosion(hit.point + hit.normal * 0.08f, ammoType, 1.35f);
                else
                    AttackVfx.CreateBurst(hit.point + hit.normal * 0.08f, impactColor, 0.9f);
            }
            ReturnToPool();
        }

        private void DamageDirectTarget(Health target, Vector3 hitPoint)
        {
            if (!CanDamage(target)) return;
            bool headshot = ammoType == WeaponAmmoType.Seed && IsHeadshot(target, hitPoint);
            float distanceAdjustedDamage = DamageAtDistance(hitPoint);
            float appliedDamage = headshot
                ? distanceAdjustedDamage * SeedHeadshotDamageMultiplier
                : distanceAdjustedDamage;
            appliedDamage = SnapLethalDamage(target, appliedDamage);
            target.TakeDamage(appliedDamage, owner);
            CombatFeedback.NotifyHit(owner.AnimalType, hitPoint, appliedDamage);
            if (target.Owner == null) return;
            Vector3 knockback = velocity.sqrMagnitude > 0.01f ? velocity.normalized : owner.transform.forward;
            target.Owner.ReceiveKnockback(new Vector3(knockback.x, 0.12f, knockback.z).normalized * 4.2f);
        }

        // No dedicated head collider/bone to test against — these are simple capsule bodies
        // physics-wise — so this just checks how high up the controller's height the hit
        // landed instead, which reads as "aimed at the head" for these proportionally
        // big-headed characters without needing per-bone hit detection.
        private static bool IsHeadshot(Health target, Vector3 hitPoint)
        {
            if (target.Owner == null) return false;
            float height = Mathf.Max(0.01f, target.Owner.Stats.ControllerHeight);
            float heightFraction = (hitPoint.y - target.Owner.transform.position.y) / height;
            return heightFraction >= HeadshotHeightFraction;
        }

        private void DamageArea(Vector3 center)
        {
            areaHitTargets.Clear();
            int hitCount = Physics.OverlapSphereNonAlloc(center, WatermelonExplosionRadius,
                areaHitBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider candidate = areaHitBuffer[i];
                Health target = candidate != null ? candidate.GetComponentInParent<Health>() : null;
                if (!CanDamage(target) || !areaHitTargets.Add(target)) continue;

                Vector3 targetPoint = target.Owner != null
                    ? target.Owner.transform.position + Vector3.up * (target.Owner.Stats.ControllerHeight * 0.45f)
                    : target.transform.position;
                float appliedDamage = SnapLethalDamage(target, DamageAtDistance(targetPoint));
                target.TakeDamage(appliedDamage, owner);
                CombatFeedback.NotifyHit(owner.AnimalType, targetPoint, appliedDamage);
                if (target.Owner == null) continue;

                Vector3 knockback = target.Owner.transform.position - center;
                knockback.y = 0.16f;
                if (knockback.sqrMagnitude < 0.01f)
                    knockback = velocity.sqrMagnitude > 0.01f ? velocity.normalized : owner.transform.forward;
                target.Owner.ReceiveKnockback(knockback.normalized * 5.4f);
            }
            areaHitTargets.Clear();
        }

        private float DamageAtDistance(Vector3 hitPosition)
        {
            // Walnut rounds are the precision option and deliberately keep their full
            // body/headshot damage at every range.
            if (ammoType == WeaponAmmoType.Seed) return damage;

            float distance = Vector3.Distance(launchPosition, hitPosition);
            if (ammoType == WeaponAmmoType.Watermelon)
            {
                if (distance <= WatermelonFullDamageRange) return damage;
                if (distance <= WatermelonMediumDamageRange) return damage * 0.5f;
                return WatermelonLongRangeDamage;
            }

            if (distance <= TomatoFullDamageRange) return damage;
            if (distance <= TomatoMediumDamageRange) return damage * (2f / 3f);
            return damage * (1f / 3f);
        }

        private static float SnapLethalDamage(Health target, float appliedDamage)
        {
            // 100/6 and 100/3 cannot be represented exactly as floats. Snap only the tiny
            // rounding remainder on the final hit so six tomatoes or three watermelons kill
            // a 100-health target instead of leaving an invisible fraction of one HP.
            float remaining = target.CurrentHealth - appliedDamage;
            return remaining > 0f && remaining <= LethalDamageEpsilon
                ? target.CurrentHealth
                : appliedDamage;
        }

        private bool CanDamage(Health target)
        {
            return target != null && target != owner.Health && !target.IsDead
                   && (target.Owner == null || !target.Owner.IsBurrowed);
        }

        private void BuildVisual(AnimalType type, float scale)
        {
            visual = BuildProjectileVisual(ammoType, type, transform, scale, impactColor);
        }

        private static Transform BuildProjectileVisual(WeaponAmmoType ammoType, AnimalType animalType, Transform parent,
            float scale, Color seedColor)
        {
            if (animalType == AnimalType.Cow) return BuildMilkVisual(parent, scale);

            switch (ammoType)
            {
                case WeaponAmmoType.Tomato:
                    return BuildTomatoVisual(parent, scale);
                case WeaponAmmoType.Watermelon:
                    return BuildWatermelonVisual(parent, scale);
            }

            if (!sharedSeedMaterials.TryGetValue(animalType, out Material seedMaterial))
            {
                seedMaterial = CreateSharedMaterial(animalType + "_SeedProjectileMaterial", seedColor, 0.32f);
                sharedSeedMaterials.Add(animalType, seedMaterial);
            }
            GameObject seed = AddPrimitive(parent, PrimitiveType.Sphere, "SeedProjectileVisual",
                Vector3.zero, new Vector3(0.62f, 0.62f, 1f) * scale, Quaternion.identity, seedMaterial);
            return seed.transform;
        }

        private static Transform BuildMilkVisual(Transform parent, float scale)
        {
            if (sharedMilkMaterial == null)
                sharedMilkMaterial = CreateSharedMaterial("Milk_ProjectileMaterial", MilkColor, 0.55f);
            // Slightly squashed sphere so it reads as a splash blob rather than a solid ball.
            GameObject milk = AddPrimitive(parent, PrimitiveType.Sphere, "MilkProjectileVisual",
                Vector3.zero, new Vector3(0.9f, 0.75f, 0.9f) * scale, Quaternion.identity, sharedMilkMaterial);
            return milk.transform;
        }

        private static Transform BuildTomatoVisual(Transform parent, float scale)
        {
            GameObject root = new GameObject("TomatoProjectileVisual");
            root.transform.SetParent(parent, false);
            Material tomato = FruitMaterial("TomatoSkin", new Color(0.94f, 0.055f, 0.035f), 0.5f);
            Material leaf = FruitMaterial("TomatoLeaf", new Color(0.08f, 0.42f, 0.07f), 0.25f);
            AddPrimitive(root.transform, PrimitiveType.Sphere, "TomatoBody", Vector3.zero,
                new Vector3(1.25f, 1.08f, 1.25f) * scale, Quaternion.identity, tomato);
            AddPrimitive(root.transform, PrimitiveType.Cylinder, "TomatoStem", Vector3.up * (0.66f * scale),
                new Vector3(0.11f, 0.2f, 0.11f) * scale, Quaternion.Euler(9f, 0f, -12f), leaf);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 72f;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                AddPrimitive(root.transform, PrimitiveType.Sphere, "TomatoLeaf_" + i,
                    Vector3.up * (0.55f * scale) + direction * (0.16f * scale),
                    new Vector3(0.12f, 0.045f, 0.42f) * scale,
                    Quaternion.Euler(0f, angle, 0f), leaf);
            }
            return root.transform;
        }

        private static Transform BuildWatermelonVisual(Transform parent, float scale)
        {
            GameObject root = new GameObject("WatermelonProjectileVisual");
            root.transform.SetParent(parent, false);
            Material rind = FruitMaterial("WatermelonRind", new Color(0.2f, 0.72f, 0.16f), 0.44f);
            Material stripe = FruitMaterial("WatermelonStripe", new Color(0.025f, 0.27f, 0.055f), 0.3f);
            AddPrimitive(root.transform, PrimitiveType.Sphere, "WatermelonBody", Vector3.zero,
                new Vector3(1.2f, 1.2f, 1.65f) * scale, Quaternion.identity, rind);

            Vector3[] stripeOffsets =
            {
                Vector3.right * 0.57f, Vector3.left * 0.57f,
                Vector3.up * 0.57f, Vector3.down * 0.57f
            };
            for (int i = 0; i < stripeOffsets.Length; i++)
            {
                Vector3 offset = stripeOffsets[i] * scale;
                Vector3 stripeScale = i < 2
                    ? new Vector3(0.105f, 0.22f, 1.52f) * scale
                    : new Vector3(0.22f, 0.105f, 1.52f) * scale;
                AddPrimitive(root.transform, PrimitiveType.Sphere, "WatermelonStripe_" + i,
                    offset, stripeScale, Quaternion.identity, stripe);
            }
            AddPrimitive(root.transform, PrimitiveType.Cylinder, "WatermelonStem", Vector3.forward * (-0.88f * scale),
                new Vector3(0.1f, 0.15f, 0.1f) * scale, Quaternion.Euler(90f, 0f, 0f), stripe);
            return root.transform;
        }

        private static GameObject AddPrimitive(Transform parent, PrimitiveType primitiveType, string objectName,
            Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
        {
            GameObject instance = GameObject.CreatePrimitive(primitiveType);
            instance.name = objectName;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = localScale;
            Collider collider = instance.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            Renderer renderer = instance.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return instance;
        }

        private static Material FruitMaterial(string key, Color color, float smoothness)
        {
            if (sharedFruitMaterials.TryGetValue(key, out Material material)) return material;
            material = CreateSharedMaterial(key + "_ProjectileMaterial", color, smoothness);
            sharedFruitMaterials.Add(key, material);
            return material;
        }

        private static Material CreateSharedMaterial(string materialName, Color color, float smoothness)
        {
            Material material = new Material(ShaderLibrary.Lit)
            {
                name = materialName,
                color = color,
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        private void BuildTrail()
        {
            TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.2f;
            trail.minVertexDistance = 0.08f;
            trail.startWidth = radius * 0.42f;
            trail.endWidth = 0f;
            trail.startColor = new Color(impactColor.r, impactColor.g, impactColor.b, 0.7f);
            trail.endColor = new Color(impactColor.r, impactColor.g, impactColor.b, 0f);
            if (sharedTrailMaterial == null)
            {
                Shader shader = ShaderLibrary.Sprite;
                if (shader != null)
                {
                    sharedTrailMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
            }
            if (sharedTrailMaterial != null) trail.sharedMaterial = sharedTrailMaterial;
            trailRenderer = trail;
        }
    }

    public sealed class RangedAmmoPickup : MonoBehaviour
    {
        private const float CollectRange = 2.6f;
        private const float SpinDegreesPerSecond = 24f;
        private const float RespawnSeconds = Countdown.DurationSeconds;
        private static readonly List<RangedAmmoPickup> activePickups = new List<RangedAmmoPickup>();
        private static int nextMotionGroup;

        private WeaponAmmoType ammoType;
        private Transform visual;
        private GameObject labelObject;
        private GameObject highlightObject;
        private Vector3 visualBasePosition;
        private bool available = true;
        private float respawnAt;
        private int motionGroup;

        public static IReadOnlyList<RangedAmmoPickup> ActivePickups => activePickups;
        public WeaponAmmoType AmmoType => ammoType;
        public bool IsAvailable => available;

        private void Awake()
        {
            motionGroup = nextMotionGroup++ & 1;
        }

        private void OnEnable()
        {
            if (!activePickups.Contains(this)) activePickups.Add(this);
        }

        private void OnDisable()
        {
            activePickups.Remove(this);
        }

        public static RangedAmmoPickup Create(Vector3 groundPosition, WeaponAmmoType type)
        {
            GameObject root = new GameObject("RangedSupply_" + type);
            root.transform.position = groundPosition;
            RangedAmmoPickup pickup = root.AddComponent<RangedAmmoPickup>();
            pickup.ammoType = type;
            pickup.BuildVisual();
            return pickup;
        }

        /// <summary>Grabs whatever ammo pickup is nearest, regardless of type. Collecting a
        /// previously empty type unlocks it for selection and immediately makes it useful.</summary>
        public static bool TryCollectNearest(ThirdPersonAnimalController animal)
        {
            if (animal == null) return false;
            RangedAmmoPickup nearest = null;
            float nearestDistance = CollectRange * CollectRange;
            foreach (RangedAmmoPickup pickup in activePickups)
            {
                if (pickup == null || !pickup.available) continue;
                float distance = (pickup.transform.position - animal.transform.position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = pickup;
                nearestDistance = distance;
            }
            return nearest != null && nearest.Collect(animal);
        }

        /// <summary>Nearest pickup matching the animal's currently equipped weapon — used by
        /// bot AI so it seeks out what it's actually short on, not just anything.</summary>
        public static RangedAmmoPickup FindClosestCompatible(ThirdPersonAnimalController animal)
        {
            if (animal == null) return null;
            RangedAmmoPickup closest = null;
            float closestDistance = float.MaxValue;
            foreach (RangedAmmoPickup pickup in activePickups)
            {
                if (pickup == null || !pickup.available || pickup.ammoType != animal.CurrentWeaponAmmo) continue;
                float distance = (pickup.transform.position - animal.transform.position).sqrMagnitude;
                if (distance >= closestDistance) continue;
                closest = pickup;
                closestDistance = distance;
            }
            return closest;
        }

        private int PickupAmount()
        {
            return ammoType switch
            {
                WeaponAmmoType.Seed => 10,
                WeaponAmmoType.Tomato => 60,
                WeaponAmmoType.Watermelon => 30,
                _ => 0
            };
        }

        private bool Collect(ThirdPersonAnimalController animal)
        {
            if (!available || !animal.TryRefillRangedAmmo(ammoType, PickupAmount())) return false;
            available = false;
            respawnAt = Time.time + RespawnSeconds;
            if (visual != null) visual.gameObject.SetActive(false);
            if (labelObject != null) labelObject.SetActive(false);
            if (highlightObject != null) highlightObject.SetActive(false);
            CombatFeedback.PlayAmmoPickup(transform.position);
            Countdown.Spawn(transform.position, RespawnSeconds,
                SupplyPrimaryColor(), SupplySecondaryColor(), SupplyLabel());
            return true;
        }

        private void Update()
        {
            if ((Time.frameCount & 1) != motionGroup) return;
            if (!available)
            {
                if (Time.time < respawnAt) return;
                available = true;
                if (visual != null) visual.gameObject.SetActive(true);
                if (labelObject != null) labelObject.SetActive(true);
                if (highlightObject != null) highlightObject.SetActive(true);
                AttackVfx.CreateBurst(transform.position + Vector3.up * 0.35f, SupplyPrimaryColor(), 1f);
                if (HasSecondaryColor())
                    AttackVfx.CreateBurst(transform.position + Vector3.up * 0.48f, SupplySecondaryColor(), 0.78f);
            }

            if (visual != null)
            {
                visual.localPosition = visualBasePosition + Vector3.up * (Mathf.Sin(Time.time * 2.1f + transform.position.x) * 0.08f);
                visual.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
            }
        }

        private static readonly Dictionary<string, Material> pickupMaterials =
            new Dictionary<string, Material>();
        private static Mesh seedPickupMesh;
        private static Mesh tomatoPickupMesh;
        private static Mesh watermelonNoseMesh;

        private void BuildVisual()
        {
            GameObject instance = ammoType switch
            {
                WeaponAmmoType.Seed => BuildSeedPickupModel(),
                WeaponAmmoType.Tomato => BuildTomatoPickupModel(),
                WeaponAmmoType.Watermelon => BuildWatermelonPickupModel(),
                _ => BuildSeedPickupModel()
            };
            instance.name = "SupplyVisual";
            PreparePickupVisual(instance);
            visualBasePosition = instance.transform.localPosition;
            visual = instance.transform;

            GameObject effects = new GameObject("SupplyEffects");
            effects.transform.SetParent(transform, false);
            CollectibleHighlight.Attach(effects.transform, SupplyPrimaryColor(), 1.02f, 0.02f);
            if (HasSecondaryColor())
                CollectibleHighlight.Attach(effects.transform, SupplySecondaryColor(), 1.24f, 0.1f);
            PickupGlowLight.Attach(effects.transform, SupplyPrimaryColor(), SupplySecondaryColor(), 5f, 1.2f);
            highlightObject = effects;

            labelObject = new GameObject("SupplyLabel");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = Vector3.up * 1.45f;
            TextMesh text = labelObject.AddComponent<TextMesh>();
            text.text = "F  " + SupplyLabel() + "\nRECARGA " + SupplyAmmoLabel();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.043f;
            text.fontSize = 48;
            text.color = SupplyPrimaryColor();
            labelObject.AddComponent<PickupLabel>();
        }

        private GameObject BuildSeedPickupModel()
        {
            GameObject root = CreateModelRoot("WalnutBulletPickup");
            Transform model = CreateHorizontalPickupModel(root.transform, "WalnutModel");
            Material shell = GetPickupMaterial("PickupWalnutShell",
                new Color(0.55f, 0.25f, 0.075f), 0.38f);
            Material ridge = GetPickupMaterial("PickupWalnutRidge",
                new Color(0.74f, 0.39f, 0.12f), 0.32f);
            Material seam = GetPickupMaterial("PickupWalnutSeam",
                new Color(0.18f, 0.065f, 0.018f), 0.22f);

            if (seedPickupMesh == null)
            {
                seedPickupMesh = CreateLatheMesh("WalnutBulletPickupMesh", new[]
                {
                    new Vector2(0f, -0.76f),
                    new Vector2(0.28f, -0.68f),
                    new Vector2(0.4f, -0.42f),
                    new Vector2(0.42f, 0.02f),
                    new Vector2(0.34f, 0.3f),
                    new Vector2(0.2f, 0.58f),
                    new Vector2(0f, 0.92f)
                }, 24);
            }
            AddPickupMesh(model, "WalnutShell", seedPickupMesh, shell);

            // Raised organic ridges wrap the whole nut, while the two dark seams reproduce
            // the split-kernel silhouette from the supplied front-facing reference.
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                Vector3 radial = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                AddPickupPrimitive(model, PrimitiveType.Capsule, "WalnutRidge_" + i,
                    radial * 0.385f + Vector3.down * 0.08f,
                    new Vector3(0.032f, 0.42f, 0.035f),
                    Quaternion.Euler(0f, angle, Mathf.Sin(angle * Mathf.Deg2Rad) * 5f), ridge);
            }
            AddPickupPrimitive(model, PrimitiveType.Capsule, "WalnutFrontSeam",
                new Vector3(0f, -0.05f, -0.414f), new Vector3(0.025f, 0.56f, 0.018f),
                Quaternion.identity, seam);
            AddPickupPrimitive(model, PrimitiveType.Capsule, "WalnutBackSeam",
                new Vector3(0f, -0.05f, 0.414f), new Vector3(0.025f, 0.56f, 0.018f),
                Quaternion.identity, seam);
            return root;
        }

        private GameObject BuildTomatoPickupModel()
        {
            GameObject root = CreateModelRoot("TomatoBulletPickup");
            Transform model = CreateHorizontalPickupModel(root.transform, "TomatoModel");
            Material flesh = GetPickupMaterial("PickupTomatoFlesh",
                new Color(0.96f, 0.075f, 0.025f), 0.58f);
            Material inner = GetPickupMaterial("PickupTomatoInner",
                new Color(1f, 0.28f, 0.035f), 0.5f);
            Material seed = GetPickupMaterial("PickupTomatoSeed",
                new Color(1f, 0.62f, 0.08f), 0.65f);
            Material leaf = GetPickupMaterial("PickupTomatoLeaf",
                new Color(0.12f, 0.42f, 0.025f), 0.3f);

            if (tomatoPickupMesh == null)
            {
                tomatoPickupMesh = CreateLatheMesh("TomatoBulletPickupMesh", new[]
                {
                    new Vector2(0f, -0.68f),
                    new Vector2(0.34f, -0.6f),
                    new Vector2(0.48f, -0.3f),
                    new Vector2(0.49f, 0.1f),
                    new Vector2(0.42f, 0.4f),
                    new Vector2(0.25f, 0.69f),
                    new Vector2(0f, 0.94f)
                }, 28);
            }
            AddPickupMesh(model, "TomatoBulletBody", tomatoPickupMesh, flesh);

            // A bright central vein plus seed rows on both faces makes the model still read
            // as the sliced tomato bullet when it rotates through a complete turn.
            AddPickupPrimitive(model, PrimitiveType.Capsule, "TomatoFrontVein",
                new Vector3(0f, -0.02f, -0.465f), new Vector3(0.025f, 0.43f, 0.014f),
                Quaternion.identity, inner);
            AddPickupPrimitive(model, PrimitiveType.Capsule, "TomatoBackVein",
                new Vector3(0f, -0.02f, 0.465f), new Vector3(0.025f, 0.43f, 0.014f),
                Quaternion.identity, inner);
            for (int face = -1; face <= 1; face += 2)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int row = 0; row < 3; row++)
                    {
                        float y = -0.3f + row * 0.22f;
                        AddPickupPrimitive(model, PrimitiveType.Sphere,
                            $"TomatoSeed_{face}_{side}_{row}",
                            new Vector3(side * (0.17f + row * 0.012f), y, face * 0.438f),
                            new Vector3(0.055f, 0.085f, 0.022f),
                            Quaternion.Euler(0f, 0f, side * (18f - row * 4f)), seed);
                    }
                }
            }

            for (int i = 0; i < 5; i++)
            {
                float angle = i * 72f;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                AddPickupPrimitive(model, PrimitiveType.Sphere, "TomatoLeaf_" + i,
                    Vector3.down * 0.62f + direction * 0.18f,
                    new Vector3(0.09f, 0.028f, 0.3f),
                    Quaternion.Euler(0f, angle, 0f), leaf);
            }
            AddPickupPrimitive(model, PrimitiveType.Cylinder, "TomatoStem",
                Vector3.down * 0.73f, new Vector3(0.075f, 0.1f, 0.075f),
                Quaternion.identity, leaf);
            return root;
        }

        private GameObject BuildWatermelonPickupModel()
        {
            GameObject root = CreateModelRoot("WatermelonCartridgePickup");
            root.transform.localRotation = Quaternion.Euler(0f, 18f, -7f);
            Material rind = GetPickupMaterial("PickupWatermelonRind",
                new Color(0.08f, 0.48f, 0.035f), 0.62f);
            Material stripe = GetPickupMaterial("PickupWatermelonStripe",
                new Color(0.38f, 0.78f, 0.08f), 0.48f);
            Material flesh = GetPickupMaterial("PickupWatermelonFlesh",
                new Color(0.98f, 0.035f, 0.025f), 0.6f);
            Material whiteRind = GetPickupMaterial("PickupWatermelonWhiteRind",
                new Color(0.93f, 0.98f, 0.78f), 0.42f);
            Material blackSeed = GetPickupMaterial("PickupWatermelonSeed",
                new Color(0.012f, 0.008f, 0.006f), 0.72f);
            Material darkRing = GetPickupMaterial("PickupWatermelonRing",
                new Color(0.02f, 0.18f, 0.018f), 0.5f);

            AddPickupPrimitive(root.transform, PrimitiveType.Cylinder, "WatermelonCartridgeBody",
                new Vector3(0.15f, 0f, 0f), new Vector3(0.42f, 0.65f, 0.42f),
                Quaternion.Euler(0f, 0f, 90f), rind);
            AddPickupPrimitive(root.transform, PrimitiveType.Cylinder, "WatermelonWhiteRind",
                new Vector3(-0.51f, 0f, 0f), new Vector3(0.46f, 0.075f, 0.46f),
                Quaternion.Euler(0f, 0f, 90f), whiteRind);
            AddPickupPrimitive(root.transform, PrimitiveType.Cylinder, "WatermelonRearCap",
                new Vector3(0.88f, 0f, 0f), new Vector3(0.48f, 0.13f, 0.48f),
                Quaternion.Euler(0f, 0f, 90f), rind);
            AddPickupPrimitive(root.transform, PrimitiveType.Cylinder, "WatermelonRearGroove",
                new Vector3(0.73f, 0f, 0f), new Vector3(0.49f, 0.034f, 0.49f),
                Quaternion.Euler(0f, 0f, 90f), darkRing);

            if (watermelonNoseMesh == null)
            {
                watermelonNoseMesh = CreateLatheMesh("WatermelonCartridgeNoseMesh", new[]
                {
                    new Vector2(0f, -0.52f),
                    new Vector2(0.42f, -0.5f),
                    new Vector2(0.45f, -0.28f),
                    new Vector2(0.4f, 0.04f),
                    new Vector2(0.28f, 0.38f),
                    new Vector2(0f, 0.72f)
                }, 24);
            }
            AddPickupMesh(root.transform, "WatermelonRedNose", watermelonNoseMesh, flesh,
                new Vector3(-0.73f, 0f, 0f), Vector3.one, Quaternion.Euler(0f, 0f, 90f));

            Vector3[] stripePositions =
            {
                new Vector3(0.15f, 0.39f, 0f),
                new Vector3(0.15f, -0.39f, 0f),
                new Vector3(0.15f, 0f, 0.39f),
                new Vector3(0.15f, 0f, -0.39f)
            };
            for (int i = 0; i < stripePositions.Length; i++)
            {
                Vector3 scale = i < 2
                    ? new Vector3(0.055f, 0.54f, 0.115f)
                    : new Vector3(0.115f, 0.54f, 0.055f);
                AddPickupPrimitive(root.transform, PrimitiveType.Capsule, "WatermelonStripe_" + i,
                    stripePositions[i], scale, Quaternion.Euler(0f, 0f, 90f), stripe);
            }

            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f + 30f;
                float radians = angle * Mathf.Deg2Rad;
                AddPickupPrimitive(root.transform, PrimitiveType.Sphere, "WatermelonSeed_" + i,
                    new Vector3(-1.12f, Mathf.Cos(radians) * 0.24f, Mathf.Sin(radians) * 0.24f),
                    new Vector3(0.025f, 0.07f, 0.045f),
                    Quaternion.Euler(angle, 0f, 0f), blackSeed);
            }
            return root;
        }

        private GameObject CreateModelRoot(string modelName)
        {
            GameObject root = new GameObject(modelName);
            root.transform.SetParent(transform, false);
            return root;
        }

        private static Transform CreateHorizontalPickupModel(Transform root, string modelName)
        {
            // Keep the animated pickup root upright. Only the geometry is turned sideways,
            // so bobbing, grounding and the Y-axis showcase spin cannot displace the model.
            GameObject model = new GameObject(modelName);
            model.transform.SetParent(root, false);
            model.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            return model.transform;
        }

        private static GameObject AddPickupPrimitive(Transform parent, PrimitiveType type,
            string objectName, Vector3 localPosition, Vector3 localScale,
            Quaternion localRotation, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = localRotation;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return part;
        }

        private static GameObject AddPickupMesh(Transform parent, string objectName, Mesh mesh,
            Material material, Vector3? localPosition = null, Vector3? localScale = null,
            Quaternion? localRotation = null)
        {
            GameObject part = new GameObject(objectName);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition ?? Vector3.zero;
            part.transform.localScale = localScale ?? Vector3.one;
            part.transform.localRotation = localRotation ?? Quaternion.identity;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        private static Mesh CreateLatheMesh(string meshName, Vector2[] profile, int radialSegments)
        {
            int columns = radialSegments + 1;
            Vector3[] vertices = new Vector3[profile.Length * columns];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[(profile.Length - 1) * radialSegments * 6];

            for (int ring = 0; ring < profile.Length; ring++)
            {
                for (int side = 0; side <= radialSegments; side++)
                {
                    float normalizedSide = side / (float)radialSegments;
                    float angle = normalizedSide * Mathf.PI * 2f;
                    int index = ring * columns + side;
                    vertices[index] = new Vector3(Mathf.Cos(angle) * profile[ring].x,
                        profile[ring].y, Mathf.Sin(angle) * profile[ring].x);
                    uvs[index] = new Vector2(normalizedSide, ring / (float)(profile.Length - 1));
                }
            }

            int triangleIndex = 0;
            for (int ring = 0; ring < profile.Length - 1; ring++)
            {
                for (int side = 0; side < radialSegments; side++)
                {
                    int lower = ring * columns + side;
                    int upper = (ring + 1) * columns + side;
                    triangles[triangleIndex++] = lower;
                    triangles[triangleIndex++] = upper;
                    triangles[triangleIndex++] = lower + 1;
                    triangles[triangleIndex++] = lower + 1;
                    triangles[triangleIndex++] = upper;
                    triangles[triangleIndex++] = upper + 1;
                }
            }

            Mesh mesh = new Mesh
            {
                name = meshName,
                vertices = vertices,
                uv = uvs,
                triangles = triangles,
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material GetPickupMaterial(string key, Color color, float smoothness)
        {
            if (pickupMaterials.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            Material material = new Material(ShaderLibrary.Lit)
            {
                name = key,
                color = color,
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            pickupMaterials[key] = material;
            return material;
        }

        private void PreparePickupVisual(GameObject instance)
        {
            // All three composite models use different proportions, so normalize their
            // largest dimension to the same readable pickup size and ground them uniformly.
            ImportedPropVisual.NormalizeToGround(instance, 0.82f, transform.position.y, 0.08f);
        }

        private string SupplyLabel()
        {
            return ThirdPersonAnimalController.DisplayNameForWeapon(ammoType);
        }

        private string SupplyAmmoLabel()
        {
            return "+" + PickupAmount();
        }

        private Color SupplyPrimaryColor()
        {
            return ammoType switch
            {
                WeaponAmmoType.Tomato => new Color(0.96f, 0.12f, 0.055f),
                WeaponAmmoType.Watermelon => new Color(0.96f, 0.12f, 0.055f),
                _ => new Color(0.82f, 0.7f, 0.42f)
            };
        }

        private Color SupplySecondaryColor()
        {
            return ammoType == WeaponAmmoType.Watermelon
                ? new Color(0.18f, 0.82f, 0.2f)
                : SupplyPrimaryColor();
        }

        private bool HasSecondaryColor()
        {
            return ammoType == WeaponAmmoType.Watermelon;
        }
    }
}
