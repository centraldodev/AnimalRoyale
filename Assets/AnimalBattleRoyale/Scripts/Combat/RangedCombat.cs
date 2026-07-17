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

    public enum RangedSupplyKind
    {
        NaturalAmmo
    }

    public sealed class RangedProjectile : MonoBehaviour
    {
        public const float WatermelonExplosionRadius = 3.4f;
        public const float SeedDamage = 6f;
        public const float TomatoDamage = 12f;
        public const float WatermelonDamage = 15f;
        private static Material sharedTrailMaterial;
        private static readonly Dictionary<AnimalType, Material> sharedSeedMaterials = new Dictionary<AnimalType, Material>();
        private static readonly Dictionary<string, Material> sharedFruitMaterials = new Dictionary<string, Material>();
        private readonly RaycastHit[] hitBuffer = new RaycastHit[20];
        private readonly Collider[] areaHitBuffer = new Collider[64];
        private readonly HashSet<Health> areaHitTargets = new HashSet<Health>();
        private ThirdPersonAnimalController owner;
        private Transform visual;
        private Vector3 velocity;
        private float gravity;
        private float damage;
        private float radius;
        private float expiresAt;
        private Color impactColor;
        private WeaponAmmoType ammoType;

        public static void Fire(ThirdPersonAnimalController source, Vector3 direction)
        {
            if (source == null) return;
            GameObject projectileObject = new GameObject("RangedProjectile_" + source.AnimalType);
            RangedProjectile projectile = projectileObject.AddComponent<RangedProjectile>();
            projectile.Configure(source, direction);
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
            // Projectile speed is controlled globally by the host; flight feel varies
            // by animal, while damage depends on the ammo type the crystal level grants.
            float speed = ServerGameTuning.ProjectileSpeed;
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

            transform.position = GetLaunchPosition(source, direction);
            velocity = direction * speed + Vector3.up * lift;
            expiresAt = Time.time + ServerGameTuning.ProjectileRangeSeconds;
            BuildVisual(source.AnimalType, visualScale);
            BuildTrail();
            AttackVfx.CreateBurst(transform.position, impactColor, 0.5f);
            CombatFeedback.PlayProjectileLaunch(transform.position, ammoType);
        }

        private void Update()
        {
            if (owner == null || Time.time >= expiresAt)
            {
                Destroy(gameObject);
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
            Destroy(gameObject);
        }

        private void DamageDirectTarget(Health target, Vector3 hitPoint)
        {
            if (!CanDamage(target)) return;
            target.TakeDamage(damage, owner);
            CombatFeedback.NotifyHit(owner.AnimalType, hitPoint, damage);
            if (target.Owner == null) return;
            Vector3 knockback = velocity.sqrMagnitude > 0.01f ? velocity.normalized : owner.transform.forward;
            target.Owner.ReceiveKnockback(new Vector3(knockback.x, 0.12f, knockback.z).normalized * 4.2f);
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

                target.TakeDamage(damage, owner);
                Vector3 targetPoint = target.Owner != null
                    ? target.Owner.transform.position + Vector3.up * (target.Owner.Stats.ControllerHeight * 0.45f)
                    : target.transform.position;
                CombatFeedback.NotifyHit(owner.AnimalType, targetPoint, damage);
                if (target.Owner == null) continue;

                Vector3 knockback = target.Owner.transform.position - center;
                knockback.y = 0.16f;
                if (knockback.sqrMagnitude < 0.01f)
                    knockback = velocity.sqrMagnitude > 0.01f ? velocity.normalized : owner.transform.forward;
                target.Owner.ReceiveKnockback(knockback.normalized * 5.4f);
            }
            areaHitTargets.Clear();
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
        }
    }

    public sealed class RangedAmmoPickup : MonoBehaviour
    {
        private const int RefillAmount = 120; // full magazine reload
        private const float CollectRange = 2.6f;
        private const float SpinDegreesPerSecond = 24f;
        private const float RespawnSeconds = Countdown.DurationSeconds;
        private static readonly List<RangedAmmoPickup> activePickups = new List<RangedAmmoPickup>();
        private static int nextMotionGroup;

        private RangedSupplyKind supplyKind;
        private Transform visual;
        private GameObject labelObject;
        private GameObject highlightObject;
        private Vector3 visualBasePosition;
        private bool available = true;
        private float respawnAt;
        private int motionGroup;

        public static IReadOnlyList<RangedAmmoPickup> ActivePickups => activePickups;
        public RangedSupplyKind SupplyKind => supplyKind;
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

        public static RangedAmmoPickup Create(Vector3 groundPosition, RangedSupplyKind kind)
        {
            GameObject root = new GameObject("RangedSupply_" + kind);
            root.transform.position = groundPosition;
            RangedAmmoPickup pickup = root.AddComponent<RangedAmmoPickup>();
            pickup.supplyKind = kind;
            pickup.BuildVisual();
            return pickup;
        }

        public static bool TryCollectNearest(ThirdPersonAnimalController animal)
        {
            if (animal == null || !animal.NeedsRangedAmmo) return false;
            RangedAmmoPickup nearest = null;
            float nearestDistance = CollectRange * CollectRange;
            foreach (RangedAmmoPickup pickup in activePickups)
            {
                if (pickup == null || !pickup.available || pickup.supplyKind != animal.CompatibleRangedSupply) continue;
                float distance = (pickup.transform.position - animal.transform.position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = pickup;
                nearestDistance = distance;
            }
            return nearest != null && nearest.Collect(animal);
        }

        public static RangedAmmoPickup FindClosestCompatible(ThirdPersonAnimalController animal)
        {
            if (animal == null) return null;
            RangedAmmoPickup closest = null;
            float closestDistance = float.MaxValue;
            foreach (RangedAmmoPickup pickup in activePickups)
            {
                if (pickup == null || !pickup.available || pickup.supplyKind != animal.CompatibleRangedSupply) continue;
                float distance = (pickup.transform.position - animal.transform.position).sqrMagnitude;
                if (distance >= closestDistance) continue;
                closest = pickup;
                closestDistance = distance;
            }
            return closest;
        }

        private bool Collect(ThirdPersonAnimalController animal)
        {
            if (!available || !animal.TryRefillRangedAmmo(supplyKind, RefillAmount)) return false;
            available = false;
            respawnAt = Time.time + RespawnSeconds;
            if (visual != null) visual.gameObject.SetActive(false);
            if (labelObject != null) labelObject.SetActive(false);
            if (highlightObject != null) highlightObject.SetActive(false);
            CombatFeedback.PlayAmmoPickup(transform.position);
            Countdown.Spawn(transform.position, RespawnSeconds);
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
                AttackVfx.CreateBurst(transform.position + Vector3.up * 0.35f, SupplyColor(), 1f);
            }

            if (visual != null)
            {
                visual.localPosition = visualBasePosition + Vector3.up * (Mathf.Sin(Time.time * 2.1f + transform.position.x) * 0.08f);
                visual.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
            }
        }

        private static GameObject cachedAmmoPrefab;
        private static bool ammoPrefabLookedUp;

        private void BuildVisual()
        {
            if (!ammoPrefabLookedUp)
            {
                cachedAmmoPrefab = Resources.Load<GameObject>("Pickups/AmmoBox");
                ammoPrefabLookedUp = true;
            }

            GameObject instance;
            if (cachedAmmoPrefab != null)
            {
                instance = Instantiate(cachedAmmoPrefab, transform, false);
                foreach (Collider c in instance.GetComponentsInChildren<Collider>(true)) if (c != null) c.enabled = false;
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                instance.transform.SetParent(transform, false);
                Collider fallbackCollider = instance.GetComponent<Collider>();
                if (fallbackCollider != null) fallbackCollider.enabled = false;
                Renderer renderer = instance.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = MissionNode.CreateMaterial(SupplyColor(), true);
            }
            instance.name = "SupplyVisual";
            instance.transform.localScale = Vector3.one * SupplyScale();
            instance.transform.localPosition = Vector3.up * 0.08f;
            PrepareImportedVisual(instance);
            visualBasePosition = instance.transform.localPosition;
            visual = instance.transform;

            CollectibleHighlight highlight = CollectibleHighlight.Attach(transform, SupplyColor(), 1.02f, 0.02f);
            highlightObject = highlight != null ? highlight.gameObject : null;

            labelObject = new GameObject("SupplyLabel");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = Vector3.up * 1.45f;
            TextMesh text = labelObject.AddComponent<TextMesh>();
            text.text = "F  " + SupplyLabel() + "\nRECARGA " + SupplyAmmoLabel();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.043f;
            text.fontSize = 48;
            text.color = SupplyColor();
            labelObject.AddComponent<PickupLabel>();
        }

        private void PrepareImportedVisual(GameObject instance)
        {
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                child.gameObject.SetActive(true);
            foreach (LODGroup lodGroup in instance.GetComponentsInChildren<LODGroup>(true))
                lodGroup.enabled = false;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = true;
                renderer.forceRenderingOff = false;
                bounds.Encapsulate(renderer.bounds);
            }

            float desiredBottom = transform.position.y + 0.08f;
            instance.transform.position += Vector3.up * (desiredBottom - bounds.min.y);
        }

        private string SupplyLabel()
        {
            return "MUNIÇÃO";
        }

        private string SupplyAmmoLabel()
        {
            return "120 BALAS";
        }

        private Color SupplyColor()
        {
            return new Color(0.36f, 0.86f, 0.62f);
        }

        private float SupplyScale()
        {
            return 0.88f;
        }
    }
}
