using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    public enum FoodKind
    {
        Fruit,
        Nectar,
        Fish,
        Meat,
        GoldenFruit
    }

    /// <summary>Food fully restores (100%) the animal's health, regardless of kind.</summary>
    public sealed class FoodPickup : MonoBehaviour
    {
        private static readonly List<FoodPickup> activePickups = new List<FoodPickup>();
        private static readonly Dictionary<int, Material> sharedMaterials = new Dictionary<int, Material>();
        private static JungleGenerator cachedJungle;
        private static int nextMotionGroup;
        private static GameObject cachedCuraPrefab;
        private static bool curaPrefabLookedUp;
        private static Material cachedCuraMaterial;
        private FoodKind foodKind;
        private Color effectColor;
        private bool collected;
        private int motionGroup;
        private Transform display;

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

        public static FoodPickup Create(Vector3 position, FoodKind kind)
        {
            if (cachedJungle == null) cachedJungle = FindAnyObjectByType<JungleGenerator>();
            if (cachedJungle != null) position.y = cachedJungle.GroundHeightAt(position);

            GameObject root = new GameObject("Food_" + kind);
            root.transform.position = position;
            FoodPickup pickup = root.AddComponent<FoodPickup>();
            pickup.foodKind = kind;
            pickup.effectColor = FoodColor(kind);
            pickup.BuildVisual();
            pickup.SnapVisualToGround(position.y);
            return pickup;
        }

        private void Update()
        {
            if ((Time.frameCount & 1) != motionGroup) return;
            transform.Rotate(0f, 24f * Time.deltaTime, 0f, Space.World);
        }

        public static bool TryConsumeNearest(ThirdPersonAnimalController animal)
        {
            if (animal == null || animal.IsDefeated || animal.Health.IsDead) return false;
            FoodPickup nearest = null;
            float nearestSqrDistance = 1.8f * 1.8f;
            foreach (FoodPickup pickup in activePickups)
            {
                if (pickup == null || pickup.collected || !pickup.CanBenefit(animal)) continue;
                float sqrDistance = (pickup.transform.position - animal.transform.position).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearest = pickup;
                    nearestSqrDistance = sqrDistance;
                }
            }
            if (nearest == null) return false;
            nearest.Collect(animal);
            return true;
        }

        private bool CanBenefit(ThirdPersonAnimalController animal)
        {
            return animal.Health.CurrentHealth < animal.Health.MaxHealth - 0.5f || animal.NeedsMobilityEnergy;
        }

        private void Collect(ThirdPersonAnimalController animal)
        {
            collected = true;
            animal.Health.Heal(animal.Health.MaxHealth); // clamps to max -> full 100% heal
            ForestMissionDirector.Instance?.RecordFoodConsumed(animal, foodKind);
            AttackVfx.CreateBurst(transform.position, effectColor, 1.7f);
            CombatFeedback.PlayAmmoPickup(transform.position);
            Destroy(gameObject);
        }

        private void BuildVisual()
        {
            GameObject displayObject = new GameObject("CartoonPickupDisplay");
            displayObject.transform.SetParent(transform, false);
            display = displayObject.transform;

            if (!TryBuildCuraModel(display))
            {
                switch (foodKind)
                {
                    case FoodKind.Fruit: BuildFruitBundle(display); break;
                    case FoodKind.Nectar: BuildNectarFlower(display); break;
                    case FoodKind.Fish: BuildFish(display); break;
                    case FoodKind.Meat: BuildMeat(display); break;
                    case FoodKind.GoldenFruit: BuildFruitBundle(display); break;
                }
            }
            CollectibleHighlight.Attach(transform, effectColor, foodKind == FoodKind.GoldenFruit ? 1.15f : 0.92f, -0.28f);
            CreateLabel();
        }

        // A single imported model ("cura") is shared by every FoodKind; only the healing
        // amount and label text vary by kind. Falls back to the procedural shapes below
        // if the model resource is missing.
        private static bool TryBuildCuraModel(Transform parent)
        {
            if (!curaPrefabLookedUp)
            {
                cachedCuraPrefab = Resources.Load<GameObject>("Pickups/Cura/cura");
                curaPrefabLookedUp = true;
            }
            if (cachedCuraPrefab == null) return false;

            GameObject instance = Instantiate(cachedCuraPrefab, parent, false);
            instance.name = "CuraVisual";
            instance.transform.localPosition = Vector3.zero;
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                if (collider != null) collider.enabled = false;

            Material material = GetCuraMaterial();
            if (material != null)
            {
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = material;
            }

            // The source FBX's authored scale isn't guaranteed; rescale to a known
            // footprint here so SnapVisualToGround (called by Create right after) has
            // sane bounds to work with instead of whatever raw size the import came in at.
            ImportedPropVisual.NormalizeScale(instance, 0.5f, out _);
            return true;
        }

        private static Material GetCuraMaterial()
        {
            if (cachedCuraMaterial != null) return cachedCuraMaterial;
            Texture2D albedo = Resources.Load<Texture2D>("Pickups/Cura/cura_basecolor");
            if (albedo == null) return null;
            Material material = new Material(ShaderLibrary.Lit)
            {
                name = "Cura_RuntimePBR",
                color = Color.white,
                enableInstancing = true
            };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", albedo);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.3f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.3f);
            cachedCuraMaterial = material;
            return cachedCuraMaterial;
        }

        private void SnapVisualToGround(float groundHeight)
        {
            if (display == null) return;

            Renderer[] renderers = display.GetComponentsInChildren<Renderer>(true);
            Bounds visualBounds = default;
            bool hasVisualBounds = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                if (!hasVisualBounds)
                {
                    visualBounds = renderer.bounds;
                    hasVisualBounds = true;
                }
                else
                {
                    visualBounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasVisualBounds) return;
            transform.position += Vector3.up * (groundHeight - visualBounds.min.y - 0.015f);
        }

        private static void BuildNectarFlower(Transform parent)
        {
            Material petal = GetMaterial(new Color(1f, 0.28f, 0.56f));
            Material petalLight = GetMaterial(new Color(1f, 0.66f, 0.24f));
            Material nectar = GetMaterial(new Color(1f, 0.82f, 0.08f), true);
            Material leaf = GetMaterial(new Color(0.08f, 0.42f, 0.08f));
            CreatePrimitive(parent, PrimitiveType.Cylinder, "NectarStem", new Vector3(0f, -0.3f, 0f),
                new Vector3(0.07f, 0.32f, 0.07f), leaf);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI * 2f / 6f;
                CreatePrimitive(parent, PrimitiveType.Sphere, "NectarPetal",
                    new Vector3(Mathf.Cos(angle) * 0.37f, 0.05f, Mathf.Sin(angle) * 0.37f),
                    new Vector3(0.42f, 0.12f, 0.25f), i % 2 == 0 ? petal : petalLight,
                    Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f));
            }
            CreatePrimitive(parent, PrimitiveType.Sphere, "GoldenNectar", new Vector3(0f, 0.12f, 0f),
                new Vector3(0.34f, 0.24f, 0.34f), nectar);
        }

        private static void BuildFruitBundle(Transform parent)
        {
            Material mango = GetMaterial(new Color(1f, 0.56f, 0.04f));
            Material blush = GetMaterial(new Color(0.94f, 0.09f, 0.035f));
            Material leaf = GetMaterial(new Color(0.06f, 0.4f, 0.055f));
            CreatePrimitive(parent, PrimitiveType.Sphere, "GoldenMango", new Vector3(-0.2f, 0f, 0f),
                new Vector3(0.48f, 0.62f, 0.42f), mango, Quaternion.Euler(0f, 0f, 12f));
            CreatePrimitive(parent, PrimitiveType.Sphere, "RedMango", new Vector3(0.28f, -0.06f, 0.05f),
                new Vector3(0.42f, 0.54f, 0.38f), blush, Quaternion.Euler(0f, 0f, -14f));
            CreatePrimitive(parent, PrimitiveType.Cylinder, "FruitStem", new Vector3(0f, 0.55f, 0f),
                new Vector3(0.055f, 0.22f, 0.055f), leaf, Quaternion.Euler(0f, 0f, -8f));
            CreatePrimitive(parent, PrimitiveType.Sphere, "FruitLeaf", new Vector3(0.2f, 0.67f, 0f),
                new Vector3(0.38f, 0.06f, 0.18f), leaf, Quaternion.Euler(10f, 0f, -18f));
        }

        private static void BuildMeat(Transform parent)
        {
            Material meat = GetMaterial(new Color(0.82f, 0.055f, 0.045f));
            Material meatLight = GetMaterial(new Color(1f, 0.25f, 0.17f));
            Material bone = GetMaterial(new Color(1f, 0.84f, 0.58f));
            CreatePrimitive(parent, PrimitiveType.Sphere, "CartoonSteak", Vector3.zero,
                new Vector3(0.82f, 0.3f, 0.62f), meat, Quaternion.Euler(8f, 0f, -7f));
            CreatePrimitive(parent, PrimitiveType.Sphere, "SteakHighlight", new Vector3(-0.12f, 0.16f, -0.03f),
                new Vector3(0.48f, 0.08f, 0.32f), meatLight);
            CreatePrimitive(parent, PrimitiveType.Cylinder, "SteakBone", new Vector3(0.38f, 0f, 0f),
                new Vector3(0.09f, 0.42f, 0.09f), bone, Quaternion.Euler(0f, 0f, 90f));
            CreatePrimitive(parent, PrimitiveType.Sphere, "BoneEnd", new Vector3(0.76f, 0f, 0f),
                new Vector3(0.2f, 0.17f, 0.2f), bone);
        }

        private static void BuildFish(Transform parent)
        {
            Material fish = GetMaterial(new Color(0.08f, 0.62f, 0.88f));
            Material belly = GetMaterial(new Color(0.58f, 0.9f, 0.96f));
            Material fin = GetMaterial(new Color(0.04f, 0.34f, 0.67f));
            Material eye = GetMaterial(new Color(0.025f, 0.02f, 0.025f));
            CreatePrimitive(parent, PrimitiveType.Sphere, "RiverFish", Vector3.zero,
                new Vector3(0.78f, 0.34f, 0.32f), fish);
            CreatePrimitive(parent, PrimitiveType.Sphere, "FishBelly", new Vector3(0f, -0.16f, 0f),
                new Vector3(0.52f, 0.1f, 0.25f), belly);
            CreatePrimitive(parent, PrimitiveType.Cube, "FishTailTop", new Vector3(-0.72f, 0.2f, 0f),
                new Vector3(0.45f, 0.08f, 0.32f), fin, Quaternion.Euler(0f, 0f, -34f));
            CreatePrimitive(parent, PrimitiveType.Cube, "FishTailBottom", new Vector3(-0.72f, -0.2f, 0f),
                new Vector3(0.45f, 0.08f, 0.32f), fin, Quaternion.Euler(0f, 0f, 34f));
            CreatePrimitive(parent, PrimitiveType.Sphere, "FishEye", new Vector3(0.52f, 0.11f, -0.26f),
                Vector3.one * 0.085f, eye);
        }

        private static GameObject CreatePrimitive(Transform parent, PrimitiveType type, string name,
            Vector3 position, Vector3 scale, Material material, Quaternion? rotation = null)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localRotation = rotation ?? Quaternion.identity;
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            return part;
        }

        private static Material GetMaterial(Color color, bool emissive = false)
        {
            Color32 packed = color;
            int key = packed.GetHashCode() ^ (emissive ? int.MinValue : 0);
            if (sharedMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;
            Shader shader = ShaderLibrary.Lit;
            Material material = new Material(shader) { color = color, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", emissive ? 0.48f : 0.22f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", emissive ? 0.48f : 0.22f);
            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.72f);
            }
            sharedMaterials[key] = material;
            return material;
        }

        private void CreateLabel()
        {
            GameObject label = new GameObject("Label");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = Vector3.up * 1.05f;
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = $"F  {FoodLabel(foodKind)}\n+100% VIDA";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.045f;
            text.fontSize = 48;
            text.color = effectColor;
            label.AddComponent<PickupLabel>();
        }

        private static string FoodLabel(FoodKind kind)
        {
            return kind switch
            {
                FoodKind.Fruit => "FRUTA",
                FoodKind.Nectar => "NÉCTAR",
                FoodKind.Fish => "PEIXE",
                FoodKind.Meat => "CARNE",
                FoodKind.GoldenFruit => "FRUTA DOURADA",
                _ => "COMIDA"
            };
        }

        private static Color FoodColor(FoodKind kind)
        {
            return kind switch
            {
                FoodKind.Fruit => new Color(1f, 0.84f, 0.14f),
                FoodKind.Nectar => new Color(0.95f, 0.35f, 0.12f),
                FoodKind.Fish => new Color(0.24f, 0.72f, 0.94f),
                FoodKind.Meat => new Color(0.9f, 0.08f, 0.06f),
                FoodKind.GoldenFruit => new Color(1f, 0.72f, 0.05f),
                _ => Color.white
            };
        }
    }

    public sealed class PickupLabel : MonoBehaviour
    {
        private static Transform cachedCamera;
        private static int nextUpdateGroup;
        private int updateGroup;

        private void Awake()
        {
            updateGroup = nextUpdateGroup++ % 3;
        }

        private void LateUpdate()
        {
            if (Time.frameCount % 3 != updateGroup) return;
            if (cachedCamera == null && Camera.main != null) cachedCamera = Camera.main.transform;
            if (cachedCamera != null) transform.rotation = Quaternion.LookRotation(transform.position - cachedCamera.position);
        }
    }
}
