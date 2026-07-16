using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    public enum RangedSupplyKind
    {
        NaturalAmmo
    }

    public sealed class RangedProjectile : MonoBehaviour
    {
        private static Material sharedTrailMaterial;
        private static readonly Dictionary<AnimalType, Material> sharedProjectileMaterials = new Dictionary<AnimalType, Material>();
        private readonly RaycastHit[] hitBuffer = new RaycastHit[20];
        private ThirdPersonAnimalController owner;
        private Transform visual;
        private Vector3 velocity;
        private float gravity;
        private float damage;
        private float radius;
        private float expiresAt;
        private Color impactColor;

        public static void Fire(ThirdPersonAnimalController source, Vector3 direction)
        {
            if (source == null) return;
            GameObject projectileObject = new GameObject("RangedProjectile_" + source.AnimalType);
            RangedProjectile projectile = projectileObject.AddComponent<RangedProjectile>();
            projectile.Configure(source, direction);
        }

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
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : source.transform.forward;
            // Every animal fires seeds from the back-mounted launcher. Projectile
            // speed is controlled globally by the host; damage and size vary by animal.
            float speed = ServerGameTuning.ProjectileSpeed;
            float lift;
            float visualScale;
            switch (source.AnimalType)
            {
                case AnimalType.Tiger:
                    lift = 0.35f; gravity = 2.4f; damage = 11f; radius = 0.18f;
                    visualScale = 0.34f; impactColor = new Color(0.85f, 0.72f, 0.42f);
                    break;
                case AnimalType.Ant:
                    lift = 0.3f; gravity = 2.2f; damage = 8f; radius = 0.15f;
                    visualScale = 0.28f; impactColor = new Color(0.82f, 0.68f, 0.36f);
                    break;
                case AnimalType.Eagle:
                    lift = 0.4f; gravity = 2.3f; damage = 10f; radius = 0.17f;
                    visualScale = 0.32f; impactColor = new Color(0.8f, 0.74f, 0.5f);
                    break;
                case AnimalType.Monkey:
                    lift = 0.35f; gravity = 2.3f; damage = 9.5f; radius = 0.16f;
                    visualScale = 0.3f; impactColor = new Color(0.78f, 0.7f, 0.4f);
                    break;
                default:
                    lift = 0.35f; gravity = 2.3f; damage = 10f; radius = 0.17f;
                    visualScale = 0.3f; impactColor = new Color(0.82f, 0.7f, 0.42f);
                    break;
            }

            lift *= ServerGameTuning.ProjectileLiftMultiplier;
            gravity *= ServerGameTuning.ProjectileGravityMultiplier;
            damage *= ServerGameTuning.ProjectileDamageMultiplier;
            radius *= ServerGameTuning.ProjectileRadiusMultiplier;

            transform.position = GetLaunchPosition(source, direction);
            velocity = direction * speed + Vector3.up * lift;
            expiresAt = Time.time + ServerGameTuning.ProjectileRangeSeconds;
            BuildVisual(source.AnimalType, visualScale);
            BuildTrail();
            AttackVfx.CreateBurst(transform.position, impactColor, 0.5f);
            CombatFeedback.PlaySeedShot(transform.position);
            CombatFeedback.PlayProjectileFly(transform.position);
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
            CombatFeedback.PlayProjectileImpact(hit.point);
            Health target = hit.collider != null ? hit.collider.GetComponentInParent<Health>() : null;
            if (target != null && target != owner.Health && !target.IsDead && (target.Owner == null || !target.Owner.IsBurrowed))
            {
                target.TakeDamage(damage, owner);
                CombatFeedback.NotifyHit(owner.AnimalType, hit.point, damage);
                if (target.Owner != null)
                {
                    Vector3 knockback = velocity.sqrMagnitude > 0.01f ? velocity.normalized : owner.transform.forward;
                    target.Owner.ReceiveKnockback(new Vector3(knockback.x, 0.12f, knockback.z).normalized * 4.2f);
                }
            }
            AttackVfx.CreateBurst(hit.point + hit.normal * 0.08f, impactColor, 0.9f);
            Destroy(gameObject);
        }

        private void BuildVisual(AnimalType type, float scale)
        {
            // Seeds: a small elongated pellet for every animal.
            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            instance.transform.SetParent(transform, false);
            instance.name = "SeedProjectileVisual";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = new Vector3(0.62f, 0.62f, 1f) * scale;
            Collider fallbackCollider = instance.GetComponent<Collider>();
            if (fallbackCollider != null) fallbackCollider.enabled = false;
            Renderer renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (!sharedProjectileMaterials.TryGetValue(type, out Material material))
                {
                    Shader shader = ShaderLibrary.Lit;
                    material = new Material(shader) { name = type + "_ProjectileMaterial", color = impactColor, enableInstancing = true };
                    sharedProjectileMaterials.Add(type, material);
                }
                renderer.sharedMaterial = material;
            }
            visual = instance.transform;
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
