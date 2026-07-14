using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    public enum RangedSupplyKind
    {
        BananaBunch,
        StonePile,
        EagleNest
    }

    public sealed class RangedProjectile : MonoBehaviour
    {
        private static Material sharedTrailMaterial;
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

        private void Configure(ThirdPersonAnimalController source, Vector3 direction)
        {
            owner = source;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : source.transform.forward;
            float speed;
            float lift;
            string modelPath;
            float visualScale;
            switch (source.AnimalType)
            {
                case AnimalType.Monkey:
                    speed = 22f; lift = 1.8f; gravity = 5.2f; damage = 12f; radius = 0.26f;
                    modelPath = "RangedModels/BananaProjectile/BananaProjectile"; visualScale = 0.88f;
                    impactColor = new Color(1f, 0.74f, 0.08f);
                    break;
                case AnimalType.Ant:
                    speed = 24f; lift = 1.25f; gravity = 7.2f; damage = 14f; radius = 0.28f;
                    modelPath = "RangedModels/RockProjectile/RockProjectile"; visualScale = 0.72f;
                    impactColor = new Color(0.58f, 0.66f, 0.58f);
                    break;
                case AnimalType.Tiger:
                    speed = 25f; lift = 1.4f; gravity = 7.8f; damage = 16f; radius = 0.32f;
                    modelPath = "RangedModels/RockProjectile/RockProjectile"; visualScale = 0.92f;
                    impactColor = new Color(0.72f, 0.58f, 0.38f);
                    break;
                case AnimalType.Eagle:
                    speed = 21f; lift = 0.65f; gravity = 9.5f; damage = 11f; radius = 0.3f;
                    modelPath = "RangedModels/EagleDropping/EagleDropping"; visualScale = 0.78f;
                    impactColor = new Color(0.48f, 0.3f, 0.11f);
                    break;
                default:
                    speed = 22f; lift = 1f; gravity = 7f; damage = 12f; radius = 0.28f;
                    modelPath = string.Empty; visualScale = 0.7f; impactColor = Color.white;
                    break;
            }

            Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);
            Vector3 forwardOffset = flatDirection.sqrMagnitude > 0.01f ? flatDirection.normalized * 0.9f : source.transform.forward * 0.9f;
            transform.position = source.transform.position + Vector3.up * (source.Stats.ControllerHeight * 0.62f + 0.3f) + forwardOffset;
            velocity = direction * speed + Vector3.up * lift;
            expiresAt = Time.time + 4.5f;
            BuildVisual(modelPath, visualScale);
            BuildTrail();
            AttackVfx.CreateBurst(transform.position, impactColor, 0.5f);
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

        private void BuildVisual(string modelPath, float scale)
        {
            GameObject model = !string.IsNullOrEmpty(modelPath) ? Resources.Load<GameObject>(modelPath) : null;
            GameObject instance;
            if (model != null)
            {
                instance = Instantiate(model, transform);
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                instance.transform.SetParent(transform, false);
                Collider fallbackCollider = instance.GetComponent<Collider>();
                if (fallbackCollider != null) fallbackCollider.enabled = false;
            }
            instance.name = "ProjectileVisual";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = Vector3.one * scale;
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
                Shader shader = Shader.Find("Sprites/Default");
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
        private const int RefillAmount = 12;
        private const float CollectRange = 2.6f;
        private const float RespawnSeconds = 32f;
        private static readonly List<RangedAmmoPickup> activePickups = new List<RangedAmmoPickup>();

        private RangedSupplyKind supplyKind;
        private Transform visual;
        private GameObject labelObject;
        private Vector3 visualBasePosition;
        private bool available = true;
        private float respawnAt;

        public RangedSupplyKind SupplyKind => supplyKind;
        public bool IsAvailable => available;

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
            return true;
        }

        private void Update()
        {
            if (!available)
            {
                if (Time.time < respawnAt) return;
                available = true;
                if (visual != null) visual.gameObject.SetActive(true);
                if (labelObject != null) labelObject.SetActive(true);
                AttackVfx.CreateBurst(transform.position + Vector3.up * 0.35f, SupplyColor(), 1f);
            }

            if (visual != null)
            {
                visual.localPosition = visualBasePosition + Vector3.up * (Mathf.Sin(Time.time * 2.1f + transform.position.x) * 0.08f);
                visual.Rotate(0f, 22f * Time.deltaTime, 0f, Space.Self);
            }
        }

        private void BuildVisual()
        {
            string modelPath = supplyKind switch
            {
                RangedSupplyKind.BananaBunch => "RangedModels/BananaBunch/BananaBunch",
                RangedSupplyKind.StonePile => "RangedModels/StonePile/StonePile",
                RangedSupplyKind.EagleNest => "RangedModels/EagleNest/EagleNest",
                _ => string.Empty
            };
            GameObject model = Resources.Load<GameObject>(modelPath);
            GameObject instance;
            if (model != null) instance = Instantiate(model, transform);
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.transform.SetParent(transform, false);
                Collider fallbackCollider = instance.GetComponent<Collider>();
                if (fallbackCollider != null) fallbackCollider.enabled = false;
            }
            instance.name = "SupplyVisual";
            instance.transform.localScale = Vector3.one * SupplyScale();
            visualBasePosition = Vector3.up * 0.08f;
            instance.transform.localPosition = visualBasePosition;
            visual = instance.transform;

            labelObject = new GameObject("SupplyLabel");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = Vector3.up * 1.45f;
            TextMesh text = labelObject.AddComponent<TextMesh>();
            text.text = "F  " + SupplyLabel() + "\n+12 " + SupplyAmmoLabel();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.043f;
            text.fontSize = 48;
            text.color = SupplyColor();
            labelObject.AddComponent<PickupLabel>();
        }

        private string SupplyLabel()
        {
            return supplyKind switch
            {
                RangedSupplyKind.BananaBunch => "CACHO DE BANANAS",
                RangedSupplyKind.StonePile => "PILHA DE PEDRAS",
                RangedSupplyKind.EagleNest => "NINHO DE SUPRIMENTOS",
                _ => "MUNIÇÃO"
            };
        }

        private string SupplyAmmoLabel()
        {
            return supplyKind switch
            {
                RangedSupplyKind.BananaBunch => "BANANAS",
                RangedSupplyKind.StonePile => "PEDRAS",
                RangedSupplyKind.EagleNest => "CARGAS",
                _ => "MUNIÇÃO"
            };
        }

        private Color SupplyColor()
        {
            return supplyKind switch
            {
                RangedSupplyKind.BananaBunch => new Color(1f, 0.78f, 0.12f),
                RangedSupplyKind.StonePile => new Color(0.68f, 0.78f, 0.72f),
                RangedSupplyKind.EagleNest => new Color(0.82f, 0.56f, 0.22f),
                _ => Color.white
            };
        }

        private float SupplyScale()
        {
            return supplyKind switch
            {
                RangedSupplyKind.BananaBunch => 1.05f,
                RangedSupplyKind.StonePile => 1.15f,
                RangedSupplyKind.EagleNest => 1.05f,
                _ => 1f
            };
        }
    }
}
