using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Procedural cartoon flashes so melee attacks and abilities are readable without external assets.</summary>
    public sealed class AttackVfx : MonoBehaviour
    {
        private const int MaxPoolSize = 48;
        private static readonly Stack<AttackVfx> pool = new Stack<AttackVfx>();
        private static Material sharedLineMaterial;
        private LineRenderer line;
        private float startedAt;
        private float expiresAt;
        private float startWidth;
        private bool expanding;
        private Vector3 center;
        private float radius;
        private Color color;

        public static void CreateSlash(Vector3 position, Vector3 direction, Color color, float size)
        {
            AttackVfx vfx = CreateEffect("Slash", color, 0.26f);
            vfx.transform.position = position;
            vfx.ConfigureArc(position, direction, color, size);
        }

        public static void CreateBurst(Vector3 position, Color color, float radius)
        {
            AttackVfx vfx = CreateEffect("Burst", color, 0.38f);
            vfx.ConfigureRing(position, color, radius, true);
        }

        public static void CreateHitSpark(Vector3 position, Color color)
        {
            CreateBurst(position, color, 0.85f);
            CreateSlash(position, Vector3.forward, Color.white, 0.45f);
        }

        public static void CreateFruitExplosion(Vector3 position, WeaponAmmoType ammoType, float effectRadius)
        {
            bool watermelon = ammoType == WeaponAmmoType.Watermelon;
            Color outerColor = watermelon
                ? new Color(0.12f, 0.78f, 0.16f)
                : new Color(0.96f, 0.1f, 0.045f);
            Color pulpColor = watermelon
                ? new Color(1f, 0.08f, 0.16f)
                : new Color(1f, 0.42f, 0.08f);

            CreateBurst(position, outerColor, effectRadius);
            CreateBurst(position + Vector3.up * 0.06f, pulpColor, effectRadius * (watermelon ? 0.62f : 0.7f));
            int slashCount = watermelon ? 4 : 3;
            for (int i = 0; i < slashCount; i++)
            {
                float angle = i * 360f / slashCount + (watermelon ? 30f : 45f);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                CreateSlash(position + Vector3.up * 0.12f, direction, i % 2 == 0 ? pulpColor : outerColor,
                    Mathf.Min(1.05f, effectRadius * 0.34f));
            }
            FruitSplashVfx.Create(position, ammoType, effectRadius);
        }

        public static void CreatePower(AnimalType type, int slot, Vector3 position, Vector3 direction)
        {
            switch (type)
            {
                case AnimalType.Tiger:
                    // Pulo Alto - upward burst.
                    CreateBurst(position + Vector3.up * 0.4f, new Color(1f, 0.5f, 0.12f), 3.2f);
                    break;
                case AnimalType.Ant:
                    // Túnel Subterrâneo - low dust ring.
                    CreateBurst(position, new Color(0.7f, 0.45f, 0.22f), 3.4f);
                    break;
                case AnimalType.Eagle:
                    // Voo - wing-sweep arc.
                    CreateSlash(position + Vector3.up, direction, new Color(0.85f, 0.8f, 0.66f), 3.4f);
                    break;
                case AnimalType.Monkey:
                    // Subir no Cipó - leafy leap burst.
                    CreateBurst(position + Vector3.up * 0.5f, new Color(0.5f, 0.72f, 0.28f), 3f);
                    break;
                case AnimalType.Cow:
                    // Investida - dusty forward-facing charge burst.
                    CreateBurst(position, new Color(0.75f, 0.55f, 0.32f), 2.6f);
                    CreateSlash(position + Vector3.up * 0.3f, direction, new Color(0.75f, 0.55f, 0.32f), 2.4f);
                    break;
            }
        }

        private static AttackVfx CreateEffect(string effectName, Color color, float duration)
        {
            AttackVfx pending = null;
            while (pool.Count > 0 && pending == null) pending = pool.Pop();
            if (pending == null)
            {
                GameObject effect = new GameObject(effectName + "Vfx");
                pending = effect.AddComponent<AttackVfx>();
            }
            else
            {
                pending.gameObject.name = effectName + "Vfx";
                pending.gameObject.SetActive(true);
            }

            pending.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            pending.startedAt = Time.time;
            pending.expiresAt = Time.time + duration;
            pending.color = color;
            pending.expanding = false;
            if (pending.line != null) pending.line.enabled = true;
            return pending;
        }

        private void ConfigureArc(Vector3 position, Vector3 direction, Color newColor, float size)
        {
            color = newColor;
            line = CreateLine(10, newColor, 0.16f);
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, direction).normalized;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = Mathf.Lerp(-72f, 72f, i / (float)(line.positionCount - 1)) * Mathf.Deg2Rad;
                Vector3 point = position + direction * (Mathf.Cos(angle) * size) + right * (Mathf.Sin(angle) * size) + up * (Mathf.Sin(angle * 1.7f) * 0.32f + 0.18f);
                line.SetPosition(i, point);
            }
            startWidth = line.widthMultiplier;
        }

        private void ConfigureRing(Vector3 position, Color newColor, float newRadius, bool shouldExpand)
        {
            center = position;
            radius = newRadius;
            expanding = shouldExpand;
            color = newColor;
            line = CreateLine(28, newColor, shouldExpand ? 0.15f : 0.1f);
            startWidth = line.widthMultiplier;
            DrawRing(shouldExpand ? radius * 0.2f : radius);
        }

        private LineRenderer CreateLine(int count, Color newColor, float width)
        {
            LineRenderer newLine = line != null ? line : gameObject.AddComponent<LineRenderer>();
            line = newLine;
            newLine.useWorldSpace = true;
            newLine.loop = true;
            newLine.positionCount = count;
            newLine.widthMultiplier = width;
            newLine.numCapVertices = 3;
            newLine.numCornerVertices = 3;
            newLine.sharedMaterial = GetSharedLineMaterial();
            newLine.startColor = newColor;
            newLine.endColor = newColor;
            newLine.enabled = true;
            return newLine;
        }

        private void Update()
        {
            if (line == null) return;
            float progress = Mathf.InverseLerp(startedAt, expiresAt, Time.time);
            if (expanding) DrawRing(Mathf.Lerp(radius * 0.2f, radius, progress));
            Color faded = color;
            faded.a = Mathf.Lerp(1f, 0f, progress);
            line.startColor = faded;
            line.endColor = faded;
            line.widthMultiplier = Mathf.Lerp(startWidth, 0f, progress);
            if (Time.time >= expiresAt) Release();
        }

        private void DrawRing(float currentRadius)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * currentRadius, 0.08f, Mathf.Sin(angle) * currentRadius));
            }
        }

        private void Release()
        {
            if (line != null) line.enabled = false;
            if (pool.Count >= MaxPoolSize)
            {
                Destroy(gameObject);
                return;
            }

            gameObject.SetActive(false);
            pool.Push(this);
        }

        private static Material GetSharedLineMaterial()
        {
            if (sharedLineMaterial != null) return sharedLineMaterial;
            Shader shader = ShaderLibrary.Sprite;
            sharedLineMaterial = new Material(shader)
            {
                name = "SharedCartoonAttackVfx",
                hideFlags = HideFlags.HideAndDontSave
            };
            return sharedLineMaterial;
        }
    }

    /// <summary>Pooled cartoon fruit pieces thrown out by tomato and watermelon impacts.</summary>
    internal sealed class FruitSplashVfx : MonoBehaviour
    {
        private const int FragmentCount = 14;
        private const int MaxPoolSize = 20;
        private const float Duration = 0.72f;
        private static readonly Stack<FruitSplashVfx> pool = new Stack<FruitSplashVfx>();
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        private readonly Transform[] fragments = new Transform[FragmentCount];
        private readonly Renderer[] fragmentRenderers = new Renderer[FragmentCount];
        private readonly Vector3[] velocities = new Vector3[FragmentCount];
        private readonly Vector3[] angularVelocities = new Vector3[FragmentCount];
        private readonly Vector3[] startScales = new Vector3[FragmentCount];
        private float startedAt;
        private float expiresAt;

        public static void Create(Vector3 position, WeaponAmmoType ammoType, float effectRadius)
        {
            FruitSplashVfx splash = null;
            while (pool.Count > 0 && splash == null) splash = pool.Pop();
            if (splash == null)
            {
                GameObject root = new GameObject("FruitSplashVfx");
                splash = root.AddComponent<FruitSplashVfx>();
                splash.BuildFragments();
            }
            splash.gameObject.SetActive(true);
            splash.transform.SetPositionAndRotation(position, Quaternion.identity);
            splash.Configure(ammoType, effectRadius);
        }

        private void BuildFragments()
        {
            for (int i = 0; i < FragmentCount; i++)
            {
                GameObject fragment = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                fragment.name = "FruitPiece_" + i;
                fragment.transform.SetParent(transform, false);
                Collider collider = fragment.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
                fragments[i] = fragment.transform;
                fragmentRenderers[i] = fragment.GetComponent<Renderer>();
            }
        }

        private void Configure(WeaponAmmoType ammoType, float effectRadius)
        {
            bool watermelon = ammoType == WeaponAmmoType.Watermelon;
            Material skin = GetMaterial(watermelon ? "WatermelonRindPiece" : "TomatoSkinPiece",
                watermelon ? new Color(0.12f, 0.66f, 0.12f) : new Color(0.95f, 0.06f, 0.035f));
            Material pulp = GetMaterial(watermelon ? "WatermelonPulpPiece" : "TomatoPulpPiece",
                watermelon ? new Color(1f, 0.06f, 0.14f) : new Color(1f, 0.32f, 0.055f));
            Material accent = GetMaterial(watermelon ? "WatermelonSeedPiece" : "TomatoLeafPiece",
                watermelon ? new Color(0.035f, 0.025f, 0.02f) : new Color(0.08f, 0.42f, 0.06f));

            startedAt = Time.time;
            expiresAt = startedAt + Duration;
            float speedScale = Mathf.Lerp(0.85f, 1.45f, Mathf.InverseLerp(1.35f, 3.4f, effectRadius));
            for (int i = 0; i < FragmentCount; i++)
            {
                Transform fragment = fragments[i];
                fragment.gameObject.SetActive(true);
                fragment.localPosition = Vector3.up * Random.Range(0.04f, 0.2f);
                fragment.localRotation = Random.rotation;
                float size = Random.Range(watermelon ? 0.11f : 0.075f, watermelon ? 0.23f : 0.16f);
                startScales[i] = new Vector3(size * Random.Range(0.7f, 1.35f), size, size * Random.Range(0.55f, 1.2f));
                fragment.localScale = startScales[i];

                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) + Random.Range(0.25f, 0.7f);
                velocities[i] = direction.normalized * Random.Range(3.1f, 5.6f) * speedScale;
                angularVelocities[i] = Random.onUnitSphere * Random.Range(260f, 640f);
                if (fragmentRenderers[i] != null)
                    fragmentRenderers[i].sharedMaterial = i % 7 == 0 ? accent : (i % 3 == 0 ? skin : pulp);
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            float progress = Mathf.InverseLerp(startedAt, expiresAt, Time.time);
            float scale = 1f - Mathf.SmoothStep(0f, 1f, progress);
            for (int i = 0; i < FragmentCount; i++)
            {
                Transform fragment = fragments[i];
                velocities[i] += Vector3.down * (11.5f * deltaTime);
                fragment.position += velocities[i] * deltaTime;
                fragment.Rotate(angularVelocities[i] * deltaTime, Space.Self);
                fragment.localScale = startScales[i] * Mathf.Max(0.02f, scale);
            }
            if (Time.time >= expiresAt) Release();
        }

        private void Release()
        {
            for (int i = 0; i < FragmentCount; i++) fragments[i].gameObject.SetActive(false);
            if (pool.Count >= MaxPoolSize)
            {
                Destroy(gameObject);
                return;
            }
            gameObject.SetActive(false);
            pool.Push(this);
        }

        private static Material GetMaterial(string key, Color color)
        {
            if (materials.TryGetValue(key, out Material material)) return material;
            material = new Material(ShaderLibrary.Lit)
            {
                name = key,
                color = color,
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.36f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.36f);
            materials.Add(key, material);
            return material;
        }
    }
}
