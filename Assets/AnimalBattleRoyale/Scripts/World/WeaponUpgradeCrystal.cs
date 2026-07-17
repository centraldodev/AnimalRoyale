using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    /// <summary>Collectible crystal used to improve the seed launcher every five pickups.</summary>
    public sealed class WeaponUpgradeCrystal : MonoBehaviour
    {
        private const float CollectRange = 2.35f;
        private const float SpinDegreesPerSecond = 24f;
        private const float RespawnSeconds = Countdown.DurationSeconds;
        private static readonly Color CrystalColor = new Color(0.08f, 0.78f, 1f);
        private static readonly List<WeaponUpgradeCrystal> activePickups = new List<WeaponUpgradeCrystal>();
        private static GameObject cachedPrefab;
        private static Material cachedMaterial;
        private static bool prefabLookedUp;
        private static int nextMotionGroup;

        private Vector3 basePosition;
        private Transform visual;
        private GameObject labelObject;
        private GameObject highlightObject;
        private bool available = true;
        private float respawnAt;
        private int motionGroup;

        public static IReadOnlyList<WeaponUpgradeCrystal> ActivePickups => activePickups;
        public bool IsAvailable => available;

        private void Awake() => motionGroup = nextMotionGroup++ & 1;

        private void OnEnable()
        {
            if (!activePickups.Contains(this)) activePickups.Add(this);
        }

        private void OnDisable() => activePickups.Remove(this);

        public static WeaponUpgradeCrystal Create(Vector3 groundPosition)
        {
            GameObject root = new GameObject("WeaponUpgradeCrystal");
            root.transform.position = groundPosition + Vector3.up * 0.18f;
            WeaponUpgradeCrystal pickup = root.AddComponent<WeaponUpgradeCrystal>();
            pickup.basePosition = root.transform.position;
            pickup.BuildVisual();
            return pickup;
        }

        public static bool TryCollectNearest(ThirdPersonAnimalController animal)
        {
            if (animal == null || animal.IsDefeated || animal.Health == null || animal.Health.IsDead
                || !animal.CanUpgradeWeapon) return false;

            WeaponUpgradeCrystal nearest = null;
            float nearestDistance = CollectRange * CollectRange;
            foreach (WeaponUpgradeCrystal pickup in activePickups)
            {
                if (pickup == null || !pickup.available) continue;
                float distance = (pickup.transform.position - animal.transform.position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = pickup;
                nearestDistance = distance;
            }
            return nearest != null && nearest.Collect(animal);
        }

        private bool Collect(ThirdPersonAnimalController animal)
        {
            if (!available || !animal.TryCollectWeaponCrystal()) return false;
            available = false;
            respawnAt = Time.time + RespawnSeconds;
            SetVisualsActive(false);
            CombatFeedback.PlayAmmoPickup(transform.position);
            Countdown.Spawn(basePosition, RespawnSeconds);
            return true;
        }

        private void Update()
        {
            if ((Time.frameCount & 1) != motionGroup) return;
            if (!available)
            {
                if (Time.time < respawnAt) return;
                available = true;
                SetVisualsActive(true);
                AttackVfx.CreateBurst(basePosition + Vector3.up * 0.65f, CrystalColor, 1.25f);
            }

            transform.position = basePosition
                                 + Vector3.up * (Mathf.Sin(Time.time * 2.2f + basePosition.x) * 0.14f);
            transform.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
        }

        private void BuildVisual()
        {
            if (!prefabLookedUp)
            {
                cachedPrefab = Resources.Load<GameObject>("Pickups/WeaponCrystal/WeaponCrystal");
                prefabLookedUp = true;
            }

            GameObject instance;
            if (cachedPrefab != null)
            {
                instance = Instantiate(cachedPrefab, transform, false);
                instance.name = "WeaponCrystalVisual";
                instance.transform.localPosition = Vector3.zero;
                ConfigureLods(instance);
                ApplyCrystalMaterial(instance);
                foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                {
                    if (collider != null) collider.enabled = false;
                }
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                instance.name = "WeaponCrystalFallback";
                instance.transform.SetParent(transform, false);
                instance.transform.localScale = new Vector3(0.65f, 1.25f, 0.65f);
                Collider collider = instance.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
                Renderer renderer = instance.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = GetCrystalMaterial();
            }
            visual = instance.transform;

            CollectibleHighlight highlight = CollectibleHighlight.Attach(transform, CrystalColor, 1.05f, 0.03f);
            highlightObject = highlight != null ? highlight.gameObject : null;

            labelObject = new GameObject("WeaponCrystalLabel");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = Vector3.up * 1.9f;
            TextMesh text = labelObject.AddComponent<TextMesh>();
            text.text = $"F  CRISTAL DE ARMA\n{ThirdPersonAnimalController.CrystalsPerWeaponLevel} = UPGRADE";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.042f;
            text.fontSize = 48;
            text.color = CrystalColor;
            labelObject.AddComponent<PickupLabel>();
        }

        private static void ConfigureLods(GameObject instance)
        {
            List<Renderer> near = new List<Renderer>();
            List<Renderer> middle = new List<Renderer>();
            List<Renderer> far = new List<Renderer>();
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.Contains("_LOD2")) far.Add(renderer);
                else if (renderer.name.Contains("_LOD1")) middle.Add(renderer);
                else near.Add(renderer);
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            if (near.Count == 0) return;
            if (middle.Count == 0) middle.AddRange(near);
            if (far.Count == 0) far.AddRange(middle);

            LODGroup group = instance.GetComponentInChildren<LODGroup>(true);
            if (group == null) group = instance.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = true;
            group.SetLODs(new[]
            {
                new LOD(0.34f, near.ToArray()),
                new LOD(0.13f, middle.ToArray()),
                new LOD(0.018f, far.ToArray())
            });
            group.RecalculateBounds();
        }

        private static void ApplyCrystalMaterial(GameObject instance)
        {
            Material material = GetCrystalMaterial();
            if (material == null) return;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
            }
        }

        private static Material GetCrystalMaterial()
        {
            if (cachedMaterial != null) return cachedMaterial;
            Texture2D albedo = Resources.Load<Texture2D>("Pickups/WeaponCrystal/texture_diffuse");
            Material material = new Material(ShaderLibrary.Lit)
            {
                name = "WeaponCrystal_RuntimePBR",
                color = Color.white,
                enableInstancing = true
            };
            if (albedo != null)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", albedo);
            }

            Texture2D normal = Resources.Load<Texture2D>("Pickups/WeaponCrystal/texture_normal");
            if (normal != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 0.82f);
                material.EnableKeyword("_NORMALMAP");
            }

            Texture2D emission = Resources.Load<Texture2D>("Pickups/WeaponCrystal/texture_emissive");
            if (emission != null)
            {
                if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", emission);
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", CrystalColor * 1.8f);
                material.EnableKeyword("_EMISSION");
            }
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.08f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.58f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.58f);
            cachedMaterial = material;
            return cachedMaterial;
        }

        private void SetVisualsActive(bool value)
        {
            if (visual != null) visual.gameObject.SetActive(value);
            if (labelObject != null) labelObject.SetActive(value);
            if (highlightObject != null) highlightObject.SetActive(value);
        }
    }
}
