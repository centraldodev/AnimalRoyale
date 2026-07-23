using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Persistent corrosive area created by the Ant's secondary ability.</summary>
    public sealed class AcidPoolEffect : MonoBehaviour
    {
        private const float TickInterval = 1f;
        private const float AudioOccupancyCheckInterval = 0.12f;
        private const float AcidAudioVolume = 0.22f;
        private const float AcidAudioFadeSpeed = 0.55f;
        private const string AcidAudioResourcePath = "Audio/SFX/AcidLoop";
        private const int TerrainDiscSegments = 48;
        private const int TerrainDiscRings = 12;
        private static Material poolMaterial;
        private static AudioClip acidLoopClip;

        private readonly Collider[] hits = new Collider[128];
        private readonly HashSet<Health> damagedThisTick = new HashSet<Health>();
        private ThirdPersonAnimalController owner;
        private float radius;
        private float damagePerSecond;
        private float slowMultiplier;
        private float expiresAt;
        private float nextTick;
        private float nextAudioOccupancyCheck;
        private bool hasAnimalInside;
        private JungleGenerator jungle;
        private AudioSource acidAudioSource;

        public static void Create(ThirdPersonAnimalController owner, Vector3 position, float radius,
            float duration, float damagePerSecond, float slowMultiplier)
        {
            GameObject root = new GameObject("AntAcidPool");
            root.transform.position = position + Vector3.up * 0.035f;
            AcidPoolEffect effect = root.AddComponent<AcidPoolEffect>();
            effect.owner = owner;
            effect.radius = radius;
            effect.damagePerSecond = damagePerSecond;
            effect.slowMultiplier = slowMultiplier;
            effect.expiresAt = Time.time + duration;
            effect.nextTick = Time.time;
            effect.jungle = FindAnyObjectByType<JungleGenerator>();
            effect.BuildVisual();
            effect.BuildAudio();
        }

        private void BuildVisual()
        {
            GameObject disc = new GameObject("AcidDisc");
            disc.name = "AcidDisc";
            disc.transform.SetParent(transform, false);
            MeshFilter filter = disc.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildTerrainDisc();
            disc.AddComponent<MeshRenderer>().sharedMaterial = GetPoolMaterial();

            AbilityLineVisual.CreateTerrainRing(transform, transform.position, radius * 0.98f,
                new Color(0.52f, 1f, 0.08f, 0.95f), 0.11f, jungle);
            AbilityLineVisual.CreateTerrainRing(transform, transform.position, radius * 0.64f,
                new Color(0.12f, 0.55f, 0.04f, 0.9f), 0.08f, jungle);
            PickupGlowLight.Attach(transform, new Color(0.42f, 1f, 0.08f),
                new Color(0.1f, 0.75f, 0.04f), radius * 2.2f, 1.15f);
        }

        private void BuildAudio()
        {
            if (acidLoopClip == null)
                acidLoopClip = Resources.Load<AudioClip>(AcidAudioResourcePath);
            if (acidLoopClip == null) return;

            acidAudioSource = gameObject.AddComponent<AudioSource>();
            acidAudioSource.clip = acidLoopClip;
            acidAudioSource.playOnAwake = false;
            acidAudioSource.loop = true;
            acidAudioSource.volume = 0f;
            acidAudioSource.priority = 176;
            acidAudioSource.spatialBlend = 0.78f;
            acidAudioSource.dopplerLevel = 0f;
            acidAudioSource.rolloffMode = AudioRolloffMode.Linear;
            acidAudioSource.minDistance = Mathf.Max(2f, radius * 0.35f);
            acidAudioSource.maxDistance = Mathf.Max(24f, radius * 4f);
            acidAudioSource.Play();
        }

        private Mesh BuildTerrainDisc()
        {
            int vertexCount = 1 + TerrainDiscRings * TerrainDiscSegments;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[TerrainDiscSegments * 3
                                      + (TerrainDiscRings - 1) * TerrainDiscSegments * 6];

            vertices[0] = new Vector3(0f, SampleTerrainHeight(transform.position) - transform.position.y + 0.055f, 0f);
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (int ring = 1; ring <= TerrainDiscRings; ring++)
            {
                float ringRadius = radius * ring / TerrainDiscRings;
                int ringStart = 1 + (ring - 1) * TerrainDiscSegments;
                for (int segment = 0; segment < TerrainDiscSegments; segment++)
                {
                    float angle = segment * Mathf.PI * 2f / TerrainDiscSegments;
                    float x = Mathf.Cos(angle) * ringRadius;
                    float z = Mathf.Sin(angle) * ringRadius;
                    Vector3 worldPoint = transform.position + new Vector3(x, 0f, z);
                    vertices[ringStart + segment] = new Vector3(x,
                        SampleTerrainHeight(worldPoint) - transform.position.y + 0.055f, z);
                    uvs[ringStart + segment] = new Vector2(
                        0.5f + x / (radius * 2f), 0.5f + z / (radius * 2f));
                }
            }

            int triangle = 0;
            int firstRing = 1;
            for (int segment = 0; segment < TerrainDiscSegments; segment++)
            {
                int next = (segment + 1) % TerrainDiscSegments;
                triangles[triangle++] = 0;
                triangles[triangle++] = firstRing + next;
                triangles[triangle++] = firstRing + segment;
            }

            for (int ring = 2; ring <= TerrainDiscRings; ring++)
            {
                int previousStart = 1 + (ring - 2) * TerrainDiscSegments;
                int currentStart = 1 + (ring - 1) * TerrainDiscSegments;
                for (int segment = 0; segment < TerrainDiscSegments; segment++)
                {
                    int next = (segment + 1) % TerrainDiscSegments;
                    int previous = previousStart + segment;
                    int previousNext = previousStart + next;
                    int current = currentStart + segment;
                    int currentNext = currentStart + next;
                    triangles[triangle++] = previous;
                    triangles[triangle++] = currentNext;
                    triangles[triangle++] = current;
                    triangles[triangle++] = previous;
                    triangles[triangle++] = previousNext;
                    triangles[triangle++] = currentNext;
                }
            }

            Mesh mesh = new Mesh { name = "TerrainConformingAcidPool" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private float SampleTerrainHeight(Vector3 worldPoint)
        {
            if (jungle != null) return jungle.GroundHeightAt(worldPoint);
            Vector3 origin = worldPoint + Vector3.up * 24f;
            return Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 64f, ~0,
                QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : transform.position.y;
        }

        private void Update()
        {
            UpdateAcidAudio();
            if (Time.time >= expiresAt)
            {
                Destroy(gameObject);
                return;
            }

            if (Time.time < nextTick) return;
            nextTick = Time.time + TickInterval;
            damagedThisTick.Clear();
            BattleRoyaleManager manager = BattleRoyaleManager.Instance;
            if (manager != null)
            {
                foreach (ThirdPersonAnimalController fighter in manager.Fighters)
                {
                    if (fighter != null) TryDamageTarget(fighter.Health);
                }
                return;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 2f,
                radius + 4f, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Health target = hits[i] != null ? hits[i].GetComponentInParent<Health>() : null;
                TryDamageTarget(target);
            }
        }

        private void UpdateAcidAudio()
        {
            if (acidAudioSource == null) return;
            if (Time.time >= nextAudioOccupancyCheck)
            {
                nextAudioOccupancyCheck = Time.time + AudioOccupancyCheckInterval;
                hasAnimalInside = HasDamageableAnimalInside();
            }

            float targetVolume = hasAnimalInside ? AcidAudioVolume : 0f;
            acidAudioSource.volume = Mathf.MoveTowards(acidAudioSource.volume,
                targetVolume, AcidAudioFadeSpeed * Time.deltaTime);
        }

        private bool HasDamageableAnimalInside()
        {
            BattleRoyaleManager manager = BattleRoyaleManager.Instance;
            if (manager != null)
            {
                foreach (ThirdPersonAnimalController fighter in manager.Fighters)
                {
                    if (fighter != null && IsAnimalInside(fighter.Health)) return true;
                }
                return false;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 2f,
                radius + 4f, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Health target = hits[i] != null ? hits[i].GetComponentInParent<Health>() : null;
                if (IsAnimalInside(target)) return true;
            }
            return false;
        }

        private void TryDamageTarget(Health target)
        {
            if (!IsDamageableTargetInside(target) || damagedThisTick.Contains(target)) return;

            damagedThisTick.Add(target);
            float tickDamage = damagePerSecond * TickInterval;
            target.TakeDamage(tickDamage, owner);
            target.Owner.ApplySlow(slowMultiplier, TickInterval + 0.28f);
            DamageNumber.Create(target.transform.position + Vector3.up * 1.35f,
                Mathf.RoundToInt(tickDamage), new Color(0.52f, 1f, 0.08f));
        }

        private bool IsDamageableTargetInside(Health target)
        {
            return IsAnimalInside(target) && target.Owner != owner && !target.Owner.IsSpawnProtected;
        }

        private bool IsAnimalInside(Health target)
        {
            if (target == null || target.IsDead || target.Owner == null
                || target.Owner.IsBurrowed) return false;
            Vector3 planarOffset = target.transform.position - transform.position;
            planarOffset.y = 0f;
            return planarOffset.sqrMagnitude <= radius * radius;
        }

        private static Material GetPoolMaterial()
        {
            if (poolMaterial != null) return poolMaterial;
            poolMaterial = new Material(ShaderLibrary.Lit)
            {
                name = "AntAcidPoolMaterial",
                color = new Color(0.31f, 0.86f, 0.04f)
            };
            if (poolMaterial.HasProperty("_BaseColor"))
                poolMaterial.SetColor("_BaseColor", new Color(0.31f, 0.86f, 0.04f));
            if (poolMaterial.HasProperty("_EmissionColor"))
            {
                poolMaterial.EnableKeyword("_EMISSION");
                poolMaterial.SetColor("_EmissionColor", new Color(0.12f, 0.7f, 0.015f) * 1.8f);
            }
            return poolMaterial;
        }
    }

    /// <summary>Green tether and coils that follow an animal trapped by the Monkey's vine.</summary>
    public sealed class VineSnareEffect : MonoBehaviour
    {
        private Transform caster;
        private Transform target;
        private float targetHeight;
        private float expiresAt;
        private LineRenderer tether;
        private readonly List<LineRenderer> coils = new List<LineRenderer>();

        public static void Create(Transform caster, Transform target, float targetHeight, float duration)
        {
            if (caster == null || target == null) return;
            GameObject root = new GameObject("MonkeyVineSnare");
            VineSnareEffect effect = root.AddComponent<VineSnareEffect>();
            effect.caster = caster;
            effect.target = target;
            effect.targetHeight = Mathf.Max(0.8f, targetHeight);
            effect.expiresAt = Time.time + duration;
            effect.BuildLines();
        }

        private void BuildLines()
        {
            tether = AbilityLineVisual.CreateLine(transform, 12,
                new Color(0.18f, 0.72f, 0.08f), 0.085f, false);
            for (int i = 0; i < 3; i++)
            {
                coils.Add(AbilityLineVisual.CreateLine(transform, 30,
                    i == 1 ? new Color(0.42f, 0.9f, 0.12f) : new Color(0.12f, 0.52f, 0.055f),
                    0.075f, true));
            }
            UpdateLines();
        }

        private void Update()
        {
            if (Time.time >= expiresAt || caster == null || target == null)
            {
                Destroy(gameObject);
                return;
            }
            UpdateLines();
        }

        private void UpdateLines()
        {
            Vector3 start = caster.position + Vector3.up * 0.72f;
            Vector3 end = target.position + Vector3.up * (targetHeight * 0.55f);
            for (int i = 0; i < tether.positionCount; i++)
            {
                float t = i / (float)(tether.positionCount - 1);
                Vector3 point = Vector3.Lerp(start, end, t);
                point.y -= Mathf.Sin(t * Mathf.PI) * 0.18f;
                tether.SetPosition(i, point);
            }

            float coilRadius = Mathf.Clamp(targetHeight * 0.38f, 0.34f, 0.68f);
            for (int coil = 0; coil < coils.Count; coil++)
            {
                LineRenderer line = coils[coil];
                float height = targetHeight * Mathf.Lerp(0.22f, 0.72f, coil / 2f);
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / line.positionCount;
                    line.SetPosition(i, target.position + new Vector3(Mathf.Cos(angle) * coilRadius,
                        height + Mathf.Sin(angle * 2f + Time.time * 5f) * 0.035f,
                        Mathf.Sin(angle) * coilRadius));
                }
            }
        }
    }

    /// <summary>Animated milk-blue rings that remain while the Cow has temporary armor.</summary>
    public sealed class DefensiveShieldEffect : MonoBehaviour
    {
        private Health health;
        private float expiresAt;
        private LineRenderer[] rings;

        public static void Show(Transform target, Health health, float duration)
        {
            if (target == null || health == null) return;
            DefensiveShieldEffect existing = target.GetComponentInChildren<DefensiveShieldEffect>();
            if (existing != null)
            {
                existing.expiresAt = Time.time + duration;
                existing.health = health;
                return;
            }

            GameObject root = new GameObject("MilkFortificationShield");
            root.transform.SetParent(target, false);
            DefensiveShieldEffect effect = root.AddComponent<DefensiveShieldEffect>();
            effect.health = health;
            effect.expiresAt = Time.time + duration;
            effect.rings = new[]
            {
                AbilityLineVisual.CreateRing(root.transform, Vector3.up * 0.35f, 0.82f,
                    new Color(0.7f, 0.94f, 1f), 0.075f),
                AbilityLineVisual.CreateRing(root.transform, Vector3.up * 0.95f, 0.68f,
                    new Color(0.42f, 0.78f, 1f), 0.075f)
            };
            PickupGlowLight.Attach(root.transform, new Color(0.65f, 0.92f, 1f),
                Color.white, 3.5f, 0.75f);
        }

        private void Update()
        {
            if (Time.time >= expiresAt || health == null || health.TemporaryShield <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            for (int i = 0; i < rings.Length; i++)
            {
                float scale = 1f + Mathf.Sin(Time.time * 4f + i * 1.7f) * 0.08f;
                rings[i].transform.localScale = Vector3.one * scale;
                rings[i].transform.localRotation = Quaternion.Euler(0f, Time.time * (i == 0 ? 45f : -55f), 0f);
            }
        }
    }

    internal static class AbilityLineVisual
    {
        private static Material sharedMaterial;

        public static LineRenderer CreateRing(Transform parent, Vector3 localCenter, float radius,
            Color color, float width)
        {
            LineRenderer line = CreateLine(parent, 32, color, width, true);
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, localCenter + new Vector3(Mathf.Cos(angle) * radius, 0f,
                    Mathf.Sin(angle) * radius));
            }
            line.useWorldSpace = false;
            return line;
        }

        public static LineRenderer CreateTerrainRing(Transform parent, Vector3 worldCenter,
            float radius, Color color, float width, JungleGenerator jungle)
        {
            LineRenderer line = CreateLine(parent, 48, color, width, true);
            line.useWorldSpace = true;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                Vector3 point = worldCenter + new Vector3(Mathf.Cos(angle) * radius, 0f,
                    Mathf.Sin(angle) * radius);
                if (jungle != null)
                {
                    point.y = jungle.GroundHeightAt(point) + 0.095f;
                }
                else
                {
                    Vector3 origin = point + Vector3.up * 24f;
                    point.y = Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                        64f, ~0, QueryTriggerInteraction.Ignore)
                        ? hit.point.y + 0.095f
                        : worldCenter.y + 0.095f;
                }
                line.SetPosition(i, point);
            }
            return line;
        }

        public static LineRenderer CreateLine(Transform parent, int points, Color color,
            float width, bool loop)
        {
            GameObject lineObject = new GameObject("AbilityLine");
            lineObject.transform.SetParent(parent, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = points;
            line.widthMultiplier = width;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            line.sharedMaterial = SharedMaterial;
            line.startColor = color;
            line.endColor = color;
            return line;
        }

        private static Material SharedMaterial
        {
            get
            {
                if (sharedMaterial != null) return sharedMaterial;
                sharedMaterial = new Material(ShaderLibrary.Sprite)
                {
                    name = "SharedSecondaryAbilityLine",
                    hideFlags = HideFlags.HideAndDontSave
                };
                return sharedMaterial;
            }
        }
    }
}
