using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    /// <summary>Owns the ten-diamond economy and the single escape victory condition.</summary>
    public sealed class DiamondObjectiveManager : MonoBehaviour
    {
        public const int RequiredDiamonds = 10;
        public const int TotalDiamonds = 10;
        public static DiamondObjectiveManager Instance { get; private set; }

        private readonly Dictionary<ThirdPersonAnimalController, int> carried = new Dictionary<ThirdPersonAnimalController, int>();
        private JungleGenerator jungle;
        private EscapePortal portal;
        private float nextSafetyCheck;
        private bool initialized;

        public Vector3 PortalPosition => portal != null ? portal.transform.position : Vector3.zero;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize(JungleGenerator generatedJungle)
        {
            if (initialized) return;
            initialized = true;
            jungle = generatedJungle;
            Physics.SyncTransforms();
            Vector3 portalPosition = FindPortalPosition(out bool usesLakeAccess);
            portal = EscapePortal.Create(portalPosition, usesLakeAccess);
            SafeZoneController.Instance?.SetFinalCenter(portalPosition);
            for (int i = 0; i < TotalDiamonds; i++) SpawnDiamond(FindInitialSpawn(i));
        }

        public int GetCount(ThirdPersonAnimalController fighter)
        {
            return fighter != null && carried.TryGetValue(fighter, out int count) ? count : 0;
        }

        public void Collect(ThirdPersonAnimalController fighter, DiamondPickup pickup)
        {
            if (fighter == null || pickup == null) return;
            carried[fighter] = Mathf.Min(RequiredDiamonds, GetCount(fighter) + 1);
            AttackVfx.CreateBurst(pickup.transform.position, new Color(0.08f, 0.78f, 1f), 2.1f);
            CombatFeedback.PlayDiamond(pickup.transform.position);
            pickup.RemoveFromWorld();
        }

        public void DropAll(ThirdPersonAnimalController fighter, Vector3 deathPosition)
        {
            int count = GetCount(fighter);
            if (count <= 0) return;
            carried[fighter] = 0;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / Mathf.Max(1, count);
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (1.3f + (i % 3) * 0.35f);
                Vector3 position = jungle.GetGroundPosition(deathPosition + offset);
                if (SafeZoneController.Instance != null && SafeZoneController.Instance.IsOutside(position, 1.5f))
                {
                    position = FindSafeSpawn();
                }
                SpawnDiamond(position);
            }
        }

        public bool TryEscape(ThirdPersonAnimalController fighter)
        {
            if (fighter == null || fighter.IsDefeated || fighter.Health.IsDead || GetCount(fighter) < RequiredDiamonds) return false;
            CombatFeedback.PlayPortal(PortalPosition);
            AttackVfx.CreateBurst(PortalPosition + Vector3.up * 2.4f, new Color(0.4f, 0.32f, 1f), 4.8f);
            BattleRoyaleManager.Instance?.CompleteEscape(fighter);
            return true;
        }

        private void Update()
        {
            if (!initialized || Time.time < nextSafetyCheck) return;
            nextSafetyCheck = Time.time + 0.75f;
            SafeZoneController zone = SafeZoneController.Instance;
            if (zone != null)
            {
                foreach (DiamondPickup pickup in DiamondPickup.ActivePickups)
                {
                    if (pickup != null && pickup.IsAvailable && zone.IsOutside(pickup.transform.position, 1.5f))
                    {
                        pickup.Relocate(FindSafeSpawn());
                    }
                }
            }
            EnsureTenDiamondsExist();
        }

        private void EnsureTenDiamondsExist()
        {
            int total = 0;
            foreach (KeyValuePair<ThirdPersonAnimalController, int> entry in carried) total += entry.Value;
            foreach (DiamondPickup pickup in DiamondPickup.ActivePickups)
            {
                if (pickup != null && pickup.IsAvailable) total++;
            }
            while (total < TotalDiamonds)
            {
                SpawnDiamond(FindSafeSpawn());
                total++;
            }
        }

        private Vector3 FindInitialSpawn(int index)
        {
            float angle = (index * (360f / TotalDiamonds) + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(jungle.LakeRadius + 22f, jungle.MapSize * 0.43f, (index % 5) / 4f);
            radius += Random.Range(-7f, 7f);
            return jungle.GetGroundPosition(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }

        private Vector3 FindPortalPosition(out bool usesLakeAccess)
        {
            if (Random.value < 0.22f)
            {
                usesLakeAccess = true;
                return new Vector3(0f, jungle.LakeSurfaceHeight + 0.05f, 0f);
            }

            usesLakeAccess = false;
            float minRadius = jungle.MapSize * 0.23f;
            float maxRadius = jungle.MapSize * 0.34f;
            Vector3 fallback = jungle.GetGroundPosition(new Vector3(maxRadius, 0f, 0f), 0.08f);
            for (int attempt = 0; attempt < 28; attempt++)
            {
                int sector = Random.Range(0, 8);
                float angle = (sector * 45f + Random.Range(-14f, 14f)) * Mathf.Deg2Rad;
                float radius = Random.Range(minRadius, maxRadius);
                Vector3 candidate = jungle.GetGroundPosition(
                    new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius), 0.08f);
                fallback = candidate;
                if (IsPortalAreaClear(candidate)) return candidate;
            }
            return fallback;
        }

        private static bool IsPortalAreaClear(Vector3 position)
        {
            Collider[] overlaps = Physics.OverlapSphere(position + Vector3.up * 1.8f, 4.5f, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
            {
                if (overlap == null || !overlap.enabled || overlap.name == "RollingForestGround") continue;
                return false;
            }
            return true;
        }

        private Vector3 FindSafeSpawn()
        {
            SafeZoneController zone = SafeZoneController.Instance;
            Vector3 center = zone != null ? zone.Center : Vector3.zero;
            float radius = zone != null ? zone.CurrentRadius - 4f : jungle.MapSize * 0.42f;
            radius = Mathf.Clamp(radius, 7f, jungle.MapSize * 0.45f);
            for (int attempt = 0; attempt < 18; attempt++)
            {
                Vector2 point = Random.insideUnitCircle * radius;
                if (point.magnitude < Mathf.Min(5f, radius * 0.45f)) continue;
                return jungle.GetGroundPosition(center + new Vector3(point.x, 0f, point.y));
            }
            return jungle.GetGroundPosition(center + Vector3.forward * Mathf.Min(6f, radius));
        }

        private static void SpawnDiamond(Vector3 position)
        {
            DiamondPickup.Create(position);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    /// <summary>Always-open portal; only a fighter carrying all ten diamonds can escape.</summary>
    public sealed class EscapePortal : MonoBehaviour
    {
        private Transform ring;
        private Transform core;
        private TextMesh label;
        private bool usesLakeAccess;

        public static EscapePortal Create(Vector3 position, bool lakeAccess)
        {
            GameObject root = new GameObject("EscapePortal");
            root.transform.position = position;
            EscapePortal portal = root.AddComponent<EscapePortal>();
            portal.usesLakeAccess = lakeAccess;
            portal.BuildVisual();
            return portal;
        }

        private void Update()
        {
            if (ring != null) ring.Rotate(0f, 26f * Time.deltaTime, 0f, Space.World);
            if (core != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 2.2f) * 0.08f;
                core.localScale = new Vector3(0.18f, 2.15f * pulse, 2.15f * pulse);
            }

            BattleRoyaleManager manager = BattleRoyaleManager.Instance;
            DiamondObjectiveManager objective = DiamondObjectiveManager.Instance;
            if (manager == null || objective == null || manager.MatchFinished) return;
            foreach (ThirdPersonAnimalController fighter in manager.Fighters)
            {
                if (fighter == null || fighter.Health.IsDead) continue;
                Vector2 distance = new Vector2(fighter.transform.position.x - transform.position.x, fighter.transform.position.z - transform.position.z);
                if (distance.sqrMagnitude <= 3.2f * 3.2f) objective.TryEscape(fighter);
            }

            if (label != null && manager.LocalPlayer != null)
            {
                int count = objective.GetCount(manager.LocalPlayer);
                label.text = count >= DiamondObjectiveManager.RequiredDiamonds
                    ? "PORTAL LIBERADO\nENTRE PARA ESCAPAR"
                    : $"PORTAL BLOQUEADO\n{count}/{DiamondObjectiveManager.RequiredDiamonds} DIAMANTES";
            }
        }

        private void LateUpdate()
        {
            if (label != null && Camera.main != null)
            {
                label.transform.rotation = Quaternion.LookRotation(label.transform.position - Camera.main.transform.position);
            }
        }

        private void BuildVisual()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material stone = CreateMaterial(shader, new Color(0.16f, 0.2f, 0.28f), false);
            Material rampStone = CreateMaterial(shader, new Color(0.24f, 0.28f, 0.35f), false);
            Material glowBlue = CreateMaterial(shader, new Color(0.05f, 0.62f, 1f), true);
            Material glowPurple = CreateMaterial(shader, new Color(0.5f, 0.12f, 1f), true);

            GameObject baseObject = CreatePrimitive(transform, PrimitiveType.Cylinder, "PortalIsland",
                new Vector3(0f, -0.42f, 0f), new Vector3(4.2f, 0.5f, 4.2f), stone);
            baseObject.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            // The primitive capsule collider turns spherical under this squashed
            // scale; a convex mesh collider matches the flat island exactly.
            MeshCollider islandCollider = baseObject.AddComponent<MeshCollider>();
            islandCollider.convex = true;

            int rampCount = usesLakeAccess ? 8 : 4;
            float outerDistance = usesLakeAccess ? 9f : 5.8f;
            float outerHeight = usesLakeAccess ? -2.9f : -0.18f;
            for (int i = 0; i < rampCount; i++)
            {
                float angle = i * Mathf.PI * 2f / rampCount;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 outerEnd = direction * outerDistance + Vector3.up * outerHeight;
                Vector3 innerEnd = direction * 1.9f + Vector3.up * 0.1f;
                Vector3 slope = innerEnd - outerEnd;
                GameObject ramp = CreatePrimitive(transform, PrimitiveType.Cube, "PortalRamp",
                    (outerEnd + innerEnd) * 0.5f, new Vector3(3.4f, 0.3f, slope.magnitude + 0.6f),
                    rampStone, keepCollider: true);
                ramp.transform.localRotation = Quaternion.LookRotation(slope.normalized, Vector3.up);
            }

            ring = new GameObject("PortalRing").transform;
            ring.SetParent(transform, false);
            ring.localPosition = Vector3.up * 2.55f;
            const int segments = 30;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 point = new Vector3(Mathf.Cos(angle) * 2.6f, Mathf.Sin(angle) * 2.6f, 0f);
                CreatePrimitive(ring, PrimitiveType.Sphere, "PortalRune", point, Vector3.one * 0.34f,
                    i % 2 == 0 ? glowBlue : glowPurple);
            }

            GameObject coreObject = CreatePrimitive(transform, PrimitiveType.Sphere, "PortalEnergy",
                Vector3.up * 2.55f, new Vector3(0.18f, 2.15f, 2.15f), glowPurple);
            core = coreObject.transform;

            GameObject labelObject = new GameObject("PortalLabel");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = Vector3.up * 5.9f;
            label = labelObject.AddComponent<TextMesh>();
            label.text = "PORTAL BLOQUEADO\n0/10 DIAMANTES";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.065f;
            label.fontSize = 54;
            label.color = new Color(0.48f, 0.9f, 1f);
        }

        private static GameObject CreatePrimitive(Transform parent, PrimitiveType type, string name,
            Vector3 position, Vector3 scale, Material material, bool keepCollider = false)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) collider.enabled = keepCollider;
            return part;
        }

        private static Material CreateMaterial(Shader shader, Color color, bool emissive)
        {
            Material material = new Material(shader) { color = color, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.7f);
            }
            return material;
        }
    }
}
